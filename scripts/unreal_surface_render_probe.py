"""Trusted acceptance-only lit captures; not the product overview/preview implementation."""

import os
import sys
from pathlib import Path

import unreal as u

REPO = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]
from package_builder_protocol import atomic_write_result, resolve_logical_reference  # noqa: E402

from package_builder_unreal.import_plan import load_plan  # noqa: E402
from package_builder_unreal.surface_plan import load_surface_plan  # noqa: E402


def run():
    """Render the actual saved instances and exercise material changes without saving them."""
    job = Path(os.environ["PACKAGEBUILDER_UNREAL_REQUEST"]).parent
    if not job.resolve().is_relative_to(REPO / "artifacts/ue"):
        raise RuntimeError("Not an owned fixture job.")
    source = resolve_logical_reference(job, "input")
    output = resolve_logical_reference(job, "output/render")
    output.mkdir()
    content = load_plan(source / "unreal-import-plan.json", source)
    plan = load_surface_plan(source, content)
    root = content["packRoot"]
    world = u.EditorLoadingAndSavingUtils.new_blank_map(False)
    actors = u.get_editor_subsystem(u.EditorActorSubsystem) or u.get_default_object(
        u.EditorActorSubsystem
    )
    light = actors.spawn_actor_from_class(
        u.DirectionalLight, u.Vector(0, 0, 500), u.Rotator(-40, -130, 0)
    )
    light.light_component.set_mobility(u.ComponentMobility.MOVABLE)
    light.light_component.set_intensity(6)
    actor = actors.spawn_actor_from_class(u.StaticMeshActor, u.Vector(0, 0, 0))
    component = actor.static_mesh_component
    component.set_static_mesh(u.EditorAssetLibrary.load_asset(root + "/Meshes/SM_Box"))
    camera_position = u.Vector(600, 500, 350)
    camera = actors.spawn_actor_from_class(
        u.SceneCapture2D,
        camera_position,
        u.MathLibrary.find_look_at_rotation(camera_position, u.Vector(0, 0, 150)),
    )
    capture = camera.get_component_by_class(u.SceneCaptureComponent2D)
    target = u.RenderingLibrary.create_render_target2d(
        world, 256, 256, u.TextureRenderTargetFormat.RTF_RGBA8
    )
    capture.set_editor_property("texture_target", target)
    capture.set_editor_property("capture_every_frame", False)
    capture.set_editor_property("capture_on_movement", False)
    capture.set_editor_property("capture_source", u.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
    capture.set_editor_property("fov_angle", 40)
    settings = capture.get_editor_property("post_process_settings")
    for key, value in {
        "override_auto_exposure_method": True,
        "auto_exposure_method": u.AutoExposureMethod.AEM_MANUAL,
        "override_auto_exposure_bias": True,
        "auto_exposure_bias": 0.0,
        "override_auto_exposure_apply_physical_camera_exposure": True,
        "auto_exposure_apply_physical_camera_exposure": False,
    }.items():
        settings.set_editor_property(key, value)
    capture.set_editor_property("post_process_settings", settings)
    images = {}

    def snapshot(name):
        u.SystemLibrary.execute_console_command(world, "Editor.AsyncAssetCompilationFinishAll")
        # Repeated captures settle GPU resources; ReadRenderTarget synchronizes the readback.
        for _ in range(3):
            capture.capture_scene()
        pixels = u.RenderingLibrary.read_render_target(world, target, False)
        if pixels is None or len(pixels) != 256 * 256:
            raise RuntimeError("Native render readback failed.")
        rgb = [(p.r, p.g, p.b) for p in pixels]
        u.RenderingLibrary.export_render_target(world, target, str(output), name + ".png")
        if not (output / (name + ".png")).is_file():
            raise RuntimeError("Native capture was not exported.")
        images[name] = rgb
        return rgb

    for entry in plan["materials"]:
        material = u.EditorAssetLibrary.load_asset(root + "/Materials/MI_" + entry["id"])
        parent = material.get_editor_property("parent")
        if u.MaterialEditingLibrary.recompile_material(parent):
            raise RuntimeError("Material shader compile errors.")
        component.set_material(0, material)
        snapshot(entry["surface"])
    opaque = u.EditorAssetLibrary.load_asset(root + "/Materials/MI_opaque")
    component.set_material(0, opaque)
    for name, parameter, value in (
        ("emission_off", "emissionIntensity", 0),
        ("emission_bright", "emissionIntensity", 8),
    ):
        u.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(
            opaque, parameter, value
        )
        snapshot(name)
    u.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(
        opaque, "emissionIntensity", 0.4
    )
    u.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(opaque, "roughness", 0.05)
    snapshot("smooth")
    u.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(opaque, "roughness", 1)
    snapshot("rough")
    u.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(opaque, "roughness", 0.4)
    u.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(opaque, "metallic", 0)
    snapshot("dielectric")
    u.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(opaque, "metallic", 1)
    snapshot("metal")
    u.MaterialEditingLibrary.set_material_instance_vector_parameter_value(
        opaque, "normalScale", u.LinearColor(0, 0, 1, 1)
    )
    snapshot("flat_normal")
    u.MaterialEditingLibrary.set_material_instance_vector_parameter_value(
        opaque, "normalScale", u.LinearColor(2, 2, 1, 1)
    )
    snapshot("strong_normal")
    # Camera inside the closed mesh sees only back faces. Two-sided must make them visible.
    camera.set_actor_location(u.Vector(0, 0, 150), False, False)
    camera.set_actor_rotation(u.Rotator(0, 0, 0), False)
    u.MaterialEditingLibrary.set_material_instance_scalar_parameter_value(
        opaque, "emissionIntensity", 8
    )
    snapshot("back_faces")
    parent = opaque.get_editor_property("parent")
    parent.set_editor_property("two_sided", False)
    if u.MaterialEditingLibrary.recompile_material(parent):
        raise RuntimeError("One-sided shader compilation failed.")
    snapshot("back_faces_culled")

    def difference(first, second):
        return sum(
            sum(abs(x - y) for x, y in zip(a, b, strict=True))
            for a, b in zip(images[first], images[second], strict=True)
        ) / (256 * 256 * 3)

    metrics = {
        "opaqueCutoutDifference": difference("opaque", "cutout"),
        "opaqueTransparentDifference": difference("opaque", "transparent"),
        "emissionDifference": difference("emission_off", "emission_bright"),
        "roughnessDifference": difference("smooth", "rough"),
        "metallicDifference": difference("dielectric", "metal"),
        "normalDifference": difference("flat_normal", "strong_normal"),
        "twoSidedDifference": difference("back_faces", "back_faces_culled"),
    }
    foreground = {name: sum(max(p) > 5 for p in pixels) for name, pixels in images.items()}
    passed = all(value > 0.05 for value in metrics.values()) and foreground["opaque"] > 100
    atomic_write_result(
        output / "render-receipt.json",
        {"passed": passed, "metrics": metrics, "foreground": foreground},
    )
    if not passed:
        raise RuntimeError("Rendered material comparisons failed: " + str(metrics))


run()
