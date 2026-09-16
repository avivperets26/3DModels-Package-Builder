"""Native map, lighting, dependency closure and diagnostic release gates."""

import re
from pathlib import Path

from package_builder_protocol import WorkerInputError, resolve_logical_reference

from package_builder_unreal.capture_geometry import camera_frame
from package_builder_unreal.materials import equivalent_float
from package_builder_unreal.overview import (
    LIGHT_INTENSITY_SCALE,
    actor_editor,
    artifact,
    bounds_of,
    context,
    map_name,
)
from package_builder_unreal.surfaces import import_surfaces


class OverviewValidationError(WorkerInputError):
    """Stable, public-safe finding code; native diagnostics remain in the local log."""


def log_findings(text):
    """Conservatively block every native warning/error, even if package attribution is unclear."""
    return (
        ["UNREAL_LOG_DIAGNOSTIC"]
        if re.search(r"\b(?:Warning|Error|Fatal):|Fatal error:|\bError Summary\b", text, re.I)
        else []
    )


def repair_diagnostics(text, editor_directory, content_directory):
    """Classify only UE's fixed local-SCC deletion notice, never package/compiler warnings.

    This maintenance notice must name an owned content package and have a matching native
    unreferenced-redirector deletion event. A subsequent independent scan is still mandatory.
    """
    remaining, notices = [], 0
    content = Path(content_directory).resolve()
    for line in text.splitlines():
        match = re.search(
            r"LogContentCommandlet: Warning: '([^']+)' is in an unknown revision control state, "
            r"attempting to delete from disk\.\.\.$",
            line,
        )
        if match:
            name = match[1]
            path = (Path(editor_directory) / name).resolve()
            if (
                path.is_relative_to(content)
                and path.suffix == ".uasset"
                and "Deleting unreferenced redirector [" + name + "]" in text
            ):
                notices += 1
                continue
        remaining.append(line)
    if notices:
        remaining = [
            line for line in remaining if "Warning/Error Summary (Unique only)" not in line
        ]
    return {
        "localRedirectorDeletionNoticeLines": notices,
        "findings": log_findings("\n".join(remaining)),
    }


def unused_packages(packages, dependencies, entry):
    """Traverse package references from the overview; no file is silently deleted as unused."""
    reached, pending = set(), [entry]
    while pending:
        item = pending.pop()
        if item in reached:
            continue
        reached.add(item)
        pending.extend(dependencies.get(item, ()))
    return set(packages) - reached


def validate_overview(u, workspace, source):
    """Independently load the saved map, compare scene state and block stale/missing content."""
    project, content, surfaces, plan = context(u, workspace, source)
    root = content["packRoot"]
    config = resolve_logical_reference(project, "Config/DefaultEngine.ini").read_text("utf-8")
    if any(
        config.count(key + "=" + map_name(plan) + "\n") != 1
        for key in ("EditorStartupMap", "GameDefaultMap")
    ):
        raise OverviewValidationError("UNREAL_STARTUP_MAP")
    # Reject linked content before the asset registry or map loader can follow it.
    for path in (project / "Content").rglob("*"):
        resolve_logical_reference(project, path.relative_to(project).as_posix())
    registry = u.AssetRegistryHelpers.get_asset_registry()
    registry.scan_paths_synchronous(["/Game"], True)
    assets = list(registry.get_assets_by_path("/Game", recursive=True))
    if len(assets) > 1024:
        raise OverviewValidationError("UNREAL_ASSET_LIMIT")
    if any(a.is_redirector() for a in assets):
        raise OverviewValidationError("UNREAL_REDIRECTOR_REMAINS")
    expected = {root + "/Textures/" + t["assetName"] for t in content["textures"]}
    expected |= {
        root + "/Materials/" + prefix + m["id"]
        for m in surfaces["materials"]
        for prefix in ("M_", "MI_")
    }
    expected |= {root + "/Meshes/SM_" + m["id"] for m in surfaces["meshes"]}
    expected |= {map_name(plan), root + "/Materials/M_PBStudio"}
    packages = {str(a.package_name) for a in assets}
    if packages != expected:
        u.log("Overview package inventory difference: " + str(sorted(packages ^ expected)))
        raise OverviewValidationError("UNREAL_ASSET_INVENTORY")
    options = u.AssetRegistryDependencyOptions(
        include_soft_package_references=True, include_hard_package_references=True
    )
    dependencies = {
        name: [str(x) for x in registry.get_dependencies(name, options)] for name in packages
    }
    if unused_packages(packages, dependencies, map_name(plan)):
        raise OverviewValidationError("UNREAL_UNUSED_ASSET")
    # Physical inventory must agree too: registry scans can omit damaged or foreign packages.
    actual = {
        p.relative_to(project / "Content").as_posix()
        for p in (project / "Content").rglob("*")
        if p.is_file()
    }
    required = {
        name.removeprefix("/Game/") + (".umap" if name == map_name(plan) else ".uasset")
        for name in expected
    }
    # The reviewed clone template carries one empty source-control placeholder, not an asset.
    placeholder = project / "Content" / plan["projectName"] / ".gitkeep"
    if (
        placeholder.is_file()
        and placeholder.stat().st_size <= 2
        and placeholder.read_bytes() in (b"", b"\n", b"\r\n")
    ):
        required.add(plan["projectName"] + "/.gitkeep")
    if actual != required:
        u.log("Overview file inventory difference: " + str(sorted(actual ^ required)))
        raise OverviewValidationError("UNREAL_CONTENT_FILES")
    world = u.EditorLoadingAndSavingUtils.load_map(map_name(plan))
    if world is None:
        raise OverviewValidationError("UNREAL_MAP_LOAD")
    editor = actor_editor(u)
    actors = {a.get_actor_label(): a for a in editor.get_all_level_actors()}
    names = {"PB_Product", "PB_Key", "PB_Fill", "PB_Floor", "PB_Background", "PB_Camera"}
    if plan["label"] is not None:
        names.add("PB_Label")
    # WorldSettings and the native builder brush are engine-owned, not leftover product actors.
    custom = [
        a for a in editor.get_all_level_actors() if not isinstance(a, (u.WorldSettings, u.Brush))
    ]
    if len(custom) != len(names) or {a.get_actor_label() for a in custom} != names:
        raise OverviewValidationError("UNREAL_MAP_ACTORS")
    if any(a.get_editor_property("hidden") for a in custom):
        raise OverviewValidationError("UNREAL_ACTOR_HIDDEN")
    product = actors["PB_Product"]
    if not isinstance(product, u.StaticMeshActor):
        raise OverviewValidationError("UNREAL_PRODUCT_REFERENCE")
    mesh = product.static_mesh_component.static_mesh
    if mesh is None or mesh.get_path_name().split(".")[0] != root + "/Meshes/SM_" + plan["meshId"]:
        raise OverviewValidationError("UNREAL_PRODUCT_REFERENCE")
    location, scale, rotation = (
        product.get_actor_location(),
        product.get_actor_scale3d(),
        product.get_actor_rotation(),
    )
    if (
        location.x,
        location.y,
        location.z,
        scale.x,
        scale.y,
        scale.z,
        rotation.pitch,
        rotation.yaw,
        rotation.roll,
    ) != (0, 0, 0, 1, 1, 1, 0, 0, 0):
        raise OverviewValidationError("UNREAL_PRODUCT_TRANSFORM")
    component = product.static_mesh_component
    for index, material in enumerate(surfaces["meshes"][0]["materials"]):
        value = component.get_material(index)
        if (
            value is None
            or value.get_path_name().split(".")[0] != root + "/Materials/MI_" + material
        ):
            raise OverviewValidationError("UNREAL_COMPONENT_MATERIAL")
    camera = actors["PB_Camera"]
    if not isinstance(camera, u.CameraActor):
        raise OverviewValidationError("UNREAL_CAMERA_STATE")
    frame = camera_frame(*bounds_of(product), "hero", plan["fieldOfView"], plan["padding"])
    location = camera.get_actor_location()
    wanted_rotation = u.MathLibrary.find_look_at_rotation(
        u.Vector(*frame["position"]), u.Vector(*frame["centre"])
    )
    rotation = camera.get_actor_rotation()
    if not all(
        equivalent_float(a, b)
        for a, b in zip(
            (
                location.x,
                location.y,
                location.z,
                rotation.pitch,
                rotation.yaw,
                rotation.roll,
                camera.camera_component.field_of_view,
            ),
            (
                *frame["position"],
                wanted_rotation.pitch,
                wanted_rotation.yaw,
                wanted_rotation.roll,
                plan["fieldOfView"],
            ),
            strict=True,
        )
    ):
        raise OverviewValidationError("UNREAL_CAMERA_STATE")
    for name, mesh_name in (("PB_Floor", "Plane"), ("PB_Background", "Sphere")):
        actor = actors[name]
        if not isinstance(actor, u.StaticMeshActor):
            raise OverviewValidationError("UNREAL_STUDIO_STATE")
        component = actor.static_mesh_component
        mesh, material = component.static_mesh, component.get_material(0)
        if (
            mesh is None
            or mesh.get_path_name().split(".")[0] != "/Engine/BasicShapes/" + mesh_name
            or material is None
            or material.get_path_name().split(".")[0] != root + "/Materials/M_PBStudio"
            or component.get_editor_property("cast_shadow")
            or component.get_collision_enabled() != u.CollisionEnabled.NO_COLLISION
        ):
            u.log(
                "Studio state: "
                + str(
                    (
                        name,
                        mesh.get_path_name() if mesh else None,
                        material.get_path_name() if material else None,
                        component.get_editor_property("cast_shadow"),
                        str(component.get_collision_enabled()),
                    )
                )
            )
            raise OverviewValidationError("UNREAL_STUDIO_STATE")
    if not world.get_world_settings().get_editor_property("force_no_precomputed_lighting"):
        u.log("Overview lighting state: precomputed lighting was not disabled.")
        raise OverviewValidationError("UNREAL_LIGHTING_STATE")
    for name, intent in zip(("PB_Key", "PB_Fill"), plan["lights"], strict=True):
        actor = actors[name]
        if not isinstance(actor, u.DirectionalLight):
            raise OverviewValidationError("UNREAL_LIGHTING_STATE")
        light = actor.light_component
        rotation = actor.get_actor_rotation()
        colour = light.get_light_color()
        u.log(
            "Overview light state: "
            + str(
                (
                    name,
                    str(light.get_editor_property("mobility")),
                    light.get_editor_property("intensity"),
                    rotation.pitch,
                    rotation.yaw,
                    colour.r,
                    colour.g,
                    colour.b,
                )
            )
        )
        if light.get_editor_property("mobility") != u.ComponentMobility.MOVABLE or not all(
            equivalent_float(a, b)
            for a, b in zip(
                (
                    light.get_editor_property("intensity"),
                    rotation.pitch,
                    rotation.yaw,
                ),
                (
                    intent["intensity"] * LIGHT_INTENSITY_SCALE,
                    -intent["pitchDegrees"],
                    intent["yawDegrees"],
                ),
                strict=True,
            )
        ):
            raise OverviewValidationError("UNREAL_LIGHTING_STATE")
        # Unreal persists light colour as 8-bit sRGB, then exposes quantized linear RGB.
        if any(
            abs(a - b) > 0.009
            for a, b in zip((colour.r, colour.g, colour.b), intent["colour"], strict=True)
        ):
            raise OverviewValidationError("UNREAL_LIGHTING_STATE")
    if plan["label"] is not None and str(actors["PB_Label"].text_render.text) != plan["label"]:
        raise OverviewValidationError("UNREAL_LABEL_STATE")
    results = import_surfaces(u, workspace, source, True)
    results += [
        artifact(workspace, plan, p)
        for p in ("Maps/L_Overview.umap", "Materials/M_PBStudio.uasset")
    ]
    return results, world, actors, plan
