"""Generate a product-local static overview using native actors and the shared studio intent."""

from pathlib import Path

from package_builder_protocol import WorkerInputError, resolve_logical_reference

from package_builder_unreal.capture_geometry import camera_frame
from package_builder_unreal.import_plan import file_identity, load_plan
from package_builder_unreal.materials import MaterialGraph
from package_builder_unreal.overview_plan import load_overview_plan
from package_builder_unreal.surface_plan import load_surface_plan
from package_builder_unreal.surfaces import import_surfaces

# Renderer-relative studio intensity mapped to native lux at the fixed manual exposure.
LIGHT_INTENSITY_SCALE = 12.0


def context(u, workspace, source):
    """Verify the owned clone and all plans before exposing any writable Unreal objects."""
    project = resolve_logical_reference(workspace, "project")
    if Path(u.Paths.project_dir()).resolve(strict=True) != project:
        raise WorkerInputError("Unexpected running overview project.")
    content = load_plan(resolve_logical_reference(source, "unreal-import-plan.json"), source)
    surfaces = load_surface_plan(source, content)
    plan = load_overview_plan(source, surfaces)
    return project, content, surfaces, plan


def actor_editor(u):
    """Editor commandlets use stateless class-default operations when no subsystem exists."""
    return u.get_editor_subsystem(u.EditorActorSubsystem) or u.get_default_object(
        u.EditorActorSubsystem
    )


def map_name(plan):
    return "/Game/" + plan["projectName"] + "/Maps/L_Overview"


def bounds_of(actor):
    centre, extent = actor.get_actor_bounds(False)
    return (centre.x - extent.x, centre.y - extent.y, centre.z - extent.z), (
        centre.x + extent.x,
        centre.y + extent.y,
        centre.z + extent.z,
    )


def configure_camera(u, actor, component, product, view, plan):
    """Apply projection and computed framing to a camera or capture, leaving the mesh untouched."""
    frame = camera_frame(*bounds_of(product), view["kind"], plan["fieldOfView"], plan["padding"])
    position = u.Vector(*frame["position"])
    actor.set_actor_location(position, False, False)
    actor.set_actor_rotation(
        u.MathLibrary.find_look_at_rotation(position, u.Vector(*frame["centre"])), False
    )
    component.set_editor_property(
        "projection_type"
        if isinstance(component, u.SceneCaptureComponent2D)
        else "projection_mode",
        u.CameraProjectionMode.PERSPECTIVE
        if view["kind"] == "hero"
        else u.CameraProjectionMode.ORTHOGRAPHIC,
    )
    component.set_editor_property("ortho_width", frame["orthoWidth"])
    manual_exposure(u, component)
    component.set_editor_property(
        "fov_angle" if isinstance(component, u.SceneCaptureComponent2D) else "field_of_view",
        plan["fieldOfView"],
    )
    return frame


def studio_material(u, root, background):
    """Unlit screen-space radial studio shared by floor and inward-facing background sphere."""
    material = u.AssetToolsHelpers.get_asset_tools().create_asset(
        "M_PBStudio", root + "/Materials", u.Material, u.MaterialFactoryNew()
    )
    if material is None:
        raise RuntimeError("Studio material creation failed.")
    material.set_editor_property("two_sided", True)
    material.set_editor_property("shading_model", u.MaterialShadingModel.MSM_UNLIT)
    graph = MaterialGraph(u, material)
    uv = graph.node("ScreenPosition")
    offset = graph.node("Subtract")
    graph.edge(uv, offset, "A", "ViewportUV")
    graph.edge(
        graph.node("Constant2Vector", r=background["centreX"], g=background["centreY"]), offset, "B"
    )
    scaled = graph.product(
        offset, graph.node("Constant2Vector", r=background["horizontalScale"], g=1)
    )
    length = graph.node("Distance")
    graph.edge(scaled, length, "A")
    graph.edge(graph.node("Constant2Vector", r=0, g=0), length, "B")
    falloff = graph.product(length, graph.node("Constant", r=1 / background["radius"]))
    clamp = graph.node("Saturate")
    graph.edge(falloff, clamp, "")
    lerp = graph.node("LinearInterpolate")
    graph.edge(
        graph.node("Constant3Vector", constant=u.LinearColor(*background["centre"], 1)), lerp, "A"
    )
    graph.edge(
        graph.node("Constant3Vector", constant=u.LinearColor(*background["outer"], 1)), lerp, "B"
    )
    graph.edge(clamp, lerp, "Alpha")
    graph.output(lerp, "EMISSIVE_COLOR")
    if u.MaterialEditingLibrary.recompile_material(
        material
    ) or not u.EditorAssetLibrary.save_loaded_asset(material, only_if_is_dirty=False):
        raise RuntimeError("Studio material compilation/save failed.")
    return material


def create_overview(u, workspace, source):
    """Build one blank map; never reuse an earlier world or overwrite an existing map/studio."""
    project, content, _, plan = context(u, workspace, source)
    config_path = resolve_logical_reference(project, "Config/DefaultEngine.ini")
    config = config_path.read_text("utf-8")
    if config.count("EditorStartupMap=\n") != 1 or config.count("GameDefaultMap=\n") != 1:
        raise WorkerInputError("Overview requires an unassigned template startup map.")
    root = content["packRoot"]
    for path in (map_name(plan), root + "/Materials/M_PBStudio"):
        if u.EditorAssetLibrary.does_asset_exist(path):
            raise WorkerInputError("Overview destination already exists.")
    artifacts = import_surfaces(u, workspace, source, True)
    world = u.EditorLoadingAndSavingUtils.new_blank_map(False)
    world.get_world_settings().set_editor_property("force_no_precomputed_lighting", True)
    editor = actor_editor(u)

    def spawn(kind, name, position=(0, 0, 0), rotation=(0, 0, 0)):
        actor = editor.spawn_actor_from_class(
            kind,
            u.Vector(*position),
            u.Rotator(pitch=rotation[0], yaw=rotation[1], roll=rotation[2]),
        )
        if actor is None:
            raise RuntimeError("Overview actor creation failed.")
        actor.set_actor_label(name)
        actor.set_editor_property("tags", [name])
        return actor

    product = spawn(u.StaticMeshActor, "PB_Product")
    product.static_mesh_component.set_static_mesh(
        u.EditorAssetLibrary.load_asset(root + "/Meshes/SM_" + plan["meshId"])
    )
    for i, intent in enumerate(plan["lights"]):
        light = spawn(
            u.DirectionalLight,
            "PB_Key" if i == 0 else "PB_Fill",
            rotation=(-intent["pitchDegrees"], intent["yawDegrees"], 0),
        )
        light.light_component.set_mobility(u.ComponentMobility.MOVABLE)
        light.light_component.set_intensity(intent["intensity"] * LIGHT_INTENSITY_SCALE)
        light.light_component.set_light_color(u.LinearColor(*intent["colour"], 1))
    minimum, maximum = bounds_of(product)
    span = max(b - a for a, b in zip(minimum, maximum, strict=True))
    material = studio_material(u, root, plan["background"])
    for name, mesh, position, scale in (
        ("PB_Background", "Sphere", (0, 0, 0), (span * 2, span * 2, span * 2)),
        ("PB_Floor", "Plane", (0, 0, minimum[2] - 0.1), (span, span, 1)),
    ):
        actor = spawn(u.StaticMeshActor, name, position)
        component = actor.static_mesh_component
        component.set_static_mesh(u.EditorAssetLibrary.load_asset("/Engine/BasicShapes/" + mesh))
        component.set_material(0, material)
        component.set_collision_profile_name("NoCollision")
        component.set_collision_enabled(u.CollisionEnabled.NO_COLLISION)
        component.set_cast_shadow(False)
        actor.set_actor_scale3d(u.Vector(*scale))
    camera = spawn(u.CameraActor, "PB_Camera")
    configure_camera(
        u,
        camera,
        camera.camera_component,
        product,
        next(v for v in plan["views"] if v["kind"] == "hero"),
        plan,
    )
    if plan["label"] is not None:
        label = spawn(u.TextRenderActor, "PB_Label", (maximum[0] + span * 0.1, 0, minimum[2]))
        label.text_render.set_text(plan["label"])
        label.text_render.set_world_size(span * 0.04)
    if not u.EditorLoadingAndSavingUtils.save_map(world, map_name(plan)):
        raise RuntimeError("Overview map save failed.")
    config_path.write_text(
        config.replace("EditorStartupMap=\n", "EditorStartupMap=" + map_name(plan) + "\n").replace(
            "GameDefaultMap=\n", "GameDefaultMap=" + map_name(plan) + "\n"
        ),
        encoding="utf-8",
    )
    for path in ("Maps/L_Overview.umap", "Materials/M_PBStudio.uasset"):
        artifacts.append(artifact(workspace, plan, path))
    return artifacts


def artifact(workspace, plan, relative):
    """Hash only a contained native package after a successful save."""
    reference = "project/Content/" + plan["projectName"] + "/" + relative
    path = resolve_logical_reference(workspace, reference)
    digest, size = file_identity(path)
    if not size:
        raise RuntimeError("Empty overview asset.")
    return {
        "artifactId": path.stem,
        "role": "engine-asset",
        "logicalReference": reference,
        "target": "unreal",
        "sha256": digest,
        "byteCount": size,
    }


def manual_exposure(u, component):
    """Use fixed exposure in both persisted camera and offscreen capture."""
    settings = component.get_editor_property("post_process_settings")
    for key, value in {
        "override_auto_exposure_method": True,
        "auto_exposure_method": u.AutoExposureMethod.AEM_MANUAL,
        "override_auto_exposure_bias": True,
        "auto_exposure_bias": 0.0,
        "override_auto_exposure_apply_physical_camera_exposure": True,
        "auto_exposure_apply_physical_camera_exposure": False,
    }.items():
        settings.set_editor_property(key, value)
    component.set_editor_property("post_process_settings", settings)
