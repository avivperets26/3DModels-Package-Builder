"""Native PIE acceptance against saved customer assets; only this test uses the editor helper."""

import json
import math
import os
import time
import traceback
from pathlib import Path

import unreal as u

evidence = Path(os.environ["PB_PREVIEW_EVIDENCE"])
repository = Path(os.environ["PACKAGEBUILDER_WORKERS_ROOT"]).parent
contract = json.loads(
    Path(os.environ["PACKAGEBUILDER_UNREAL_REQUEST"])
    .parent.joinpath("input/preview-experience.json")
    .read_text("utf-8")
)
receipt = {"passed": False, "realPIE": False, "checks": []}
level = u.get_editor_subsystem(u.LevelEditorSubsystem) or u.get_default_object(
    u.LevelEditorSubsystem
)
editor = u.get_editor_subsystem(u.UnrealEditorSubsystem) or u.get_default_object(
    u.UnrealEditorSubsystem
)
started = time.monotonic()
phase = "start"
handle = None
play_key = os.environ.get("PB_PREVIEW_PLAY_KEY", "play")
u.EditorPythonScripting.set_keep_python_script_alive(True)


def check(condition, name):
    if not condition:
        raise AssertionError(name)
    receipt["checks"].append(name)


def near_vector(a, b):
    return all(abs(getattr(a, k) - getattr(b, k)) < 0.001 for k in ("x", "y", "z"))


def near_rotation(a, b):
    return all(
        abs((getattr(a, k) - getattr(b, k) + 180) % 360 - 180) < 0.001
        for k in ("pitch", "yaw", "roll")
    )


def close(error=None):
    """Retain compact acceptance evidence and allow the host's finally cleanup to remove the project."""
    global phase
    if error:
        receipt["error"] = error
        receipt["passed"] = False
    if level.is_in_play_in_editor():
        level.editor_request_end_play()
    phase = "finish"


def test(world, widget):
    """Exercise the generated input handlers, shared state vectors and actual camera/light actors."""
    actors = {
        a.get_actor_label(): a for a in u.GameplayStatics.get_all_actors_of_class(world, u.Actor)
    }
    product = actors["PB_Product"]
    initial = product.get_actor_transform()
    camera = actors["PB_Camera"]
    original_camera = camera.get_actor_location()
    light = actors["PB_Key"]
    original_light = light.get_actor_rotation()
    check(
        light.light_component.get_editor_property("forward_shading_priority")
        > actors["PB_Fill"].light_component.get_editor_property("forward_shading_priority"),
        "unambiguous-primary-light",
    )
    if os.environ.get("PB_PREVIEW_RUNTIME_ONLY") == "1":
        check(not hasattr(u, "PackageBuilderPreviewEditorLibrary"), "editor-helper-absent")
        widget.call_method("Orbit", (35.0, 20.0))
        check(
            not near_vector(camera.get_actor_location(), original_camera), "delivered-camera-orbit"
        )
        widget.call_method("ResetView")
        check(near_vector(camera.get_actor_location(), original_camera), "delivered-camera-reset")
        widget.call_method("AdjustLight", (15.0, 10.0))
        check(
            not near_rotation(light.get_actor_rotation(), original_light),
            "delivered-light-adjustment",
        )
        widget.call_method("ResetLight")
        widget.call_method("ToggleOverlay")
        check(not widget.get_editor_property("OverlayVisible"), "delivered-hide")
        widget.call_method("ToggleOverlay")
        check(widget.get_editor_property("OverlayVisible"), "delivered-restore")
        check(product.get_actor_transform().equals(initial), "delivered-product-unchanged")
        receipt["realPIE"] = True
        return
    helper = u.PackageBuilderPreviewEditorLibrary

    def state(names):
        return [widget.get_editor_property(n) for n in names]

    def key(name, shift=False):
        check(
            helper.send_preview_key(widget, name, shift),
            "key:" + name + (":shift" if shift else ""),
        )

    def pointer(action, dx=0, dy=0, wheel=0):
        check(
            helper.send_preview_pointer(widget, action, u.Vector2D(dx, dy), wheel),
            "pointer:" + action,
        )

    vectors = json.loads(
        (repository / "tests/fixtures/preview/preview-experience-v1-vectors.json").read_text(
            "utf-8"
        )
    )
    for vector in vectors["camera"]:
        widget.call_method("ResetView")
        widget.call_method("Orbit", tuple(vector["start"][:2]))
        if "orbit" in vector:
            widget.call_method("Orbit", tuple(vector["orbit"]))
        else:
            widget.call_method("Zoom", (vector["zoomSteps"], vector["step"]))
        check(
            all(
                math.isclose(a, b, abs_tol=1e-5)
                for a, b in zip(
                    state(("Yaw", "Pitch", "Distance")), vector["expected"], strict=True
                )
            ),
            vector["name"],
        )
    key("R")
    for vector in vectors["light"]:
        widget.call_method("ResetLight")
        initial_light = state(("LightYaw", "LightPitch"))
        widget.call_method(
            "AdjustLight", tuple(a - b for a, b in zip(vector["start"], initial_light, strict=True))
        )
        widget.call_method("AdjustLight", tuple(vector["adjust"]))
        check(
            all(
                math.isclose(a, b, abs_tol=1e-5)
                for a, b in zip(state(("LightYaw", "LightPitch")), vector["expected"], strict=True)
            ),
            vector["name"],
        )
    key("L")
    pointer("down")
    pointer("move", 100, -50)
    pointer("up")
    check(state(("Yaw", "Pitch")) == [22, 11], "pointer-shared-orbit-step")
    check(not near_vector(camera.get_actor_location(), original_camera), "camera-really-moved")
    pointer("wheel", wheel=1)
    check(
        math.isclose(
            widget.get_editor_property("Distance"),
            1 - contract["navigation"]["pointerZoomStep"],
            abs_tol=1e-6,
        ),
        "pointer-shared-zoom-step",
    )
    key("R")
    check(near_vector(camera.get_actor_location(), original_camera), "camera-reset")
    key("Right")
    key("Up")
    check(state(("Yaw", "Pitch")) == [5, 5], "keyboard-orbit")
    key("PageUp")
    key("PageDown")
    key("R")
    check(not helper.send_preview_key(widget, "F9", False), "unrelated-key-unhandled")
    key("Tab", True)
    check(widget.get_editor_property("FocusIndex") == 3, "reverse-focus-from-studio")
    pointer("down")
    pointer("up")
    key("Tab")
    check(widget.get_editor_property("ResetViewButton").has_keyboard_focus(), "visible-reset-focus")
    key("Tab")
    check(widget.get_editor_property("LightButton").has_keyboard_focus(), "visible-light-focus")
    before = state(("Yaw", "Pitch"))
    key("Right")
    key("Up")
    check(state(("Yaw", "Pitch")) == before, "focused-light-does-not-orbit")
    check(not near_rotation(light.get_actor_rotation(), original_light), "light-really-moved")
    key("L")
    check(near_rotation(light.get_actor_rotation(), original_light), "light-reset")
    key("Tab", True)
    check(widget.get_editor_property("FocusIndex") == 0, "reverse-focus")
    key("Enter")
    key("H")
    check(not widget.get_editor_property("OverlayVisible"), "hide-controls")
    check(
        widget.get_editor_property("ControlsPanel").get_visibility() == u.SlateVisibility.COLLAPSED,
        "panel-collapsed",
    )
    check(widget.get_editor_property("ShowButton").has_keyboard_focus(), "restore-visible-focused")
    key("SpaceBar")
    check(widget.get_editor_property("OverlayVisible"), "restore-controls")
    check(helper.set_preview_slider(widget, "YawSlider", 0.75), "native-slider-delegate")
    check(math.isclose(widget.get_editor_property("LightYaw"), 90, abs_tol=1e-5), "slider-yaw")
    check(helper.click_preview_button(widget, "ResetLightButton"), "native-button-delegate")
    check(near_rotation(light.get_actor_rotation(), original_light), "button-light-reset")
    check(product.get_actor_transform().equals(initial), "product-transform-unchanged")
    check(u.GameplayStatics.get_player_pawn(world, 0) is None, "no-extra-default-pawn")
    receipt["realPIE"] = True


def tick(_delta):
    global phase, preview_widget
    try:
        if phase != "finish" and time.monotonic() - started > 120:
            raise TimeoutError("PIE acceptance did not complete within 120 seconds.")
        if phase == "start":
            if os.environ.get("PB_PREVIEW_BASELINE") == "1":
                # A stock engine cube exercises TSR; an entirely empty world skips that pass.
                u.EditorLoadingAndSavingUtils.new_blank_map(False)
                actors = u.get_editor_subsystem(u.EditorActorSubsystem) or u.get_default_object(
                    u.EditorActorSubsystem
                )
                cube = actors.spawn_actor_from_class(u.StaticMeshActor, u.Vector(300, 0, 0))
                cube.static_mesh_component.set_static_mesh(
                    u.EditorAssetLibrary.load_asset("/Engine/BasicShapes/Cube")
                )
                light = actors.spawn_actor_from_class(u.DirectionalLight, u.Vector())
                light.light_component.set_mobility(u.ComponentMobility.MOVABLE)
            level.editor_request_begin_play()
            phase = "wait"
        elif phase == "wait" and level.is_in_play_in_editor():
            world = editor.get_game_world()
            if os.environ.get("PB_PREVIEW_BASELINE") == "1":
                if time.monotonic() - started > 5:
                    check(
                        not u.AssetRegistryHelpers.get_asset_registry().get_assets_by_path(
                            "/Game", recursive=True
                        ),
                        "blank-engine-baseline",
                    )
                    receipt.update(passed=True, realPIE=True)
                    close()
                return
            widgets = u.WidgetLibrary.get_all_widgets_of_class(world, u.UserWidget, True)
            selected = [w for w in widgets if w.get_class().get_name() == "WBP_Preview_C"]
            if len(selected) == 1 and time.monotonic() - started > 5:
                test(world, selected[0])
                preview_widget = selected[0]
                if os.environ.get("PB_PREVIEW_RUNTIME_ONLY") == "1":
                    receipt["passed"] = True
                    close()
                else:
                    phase = "capture"
        elif phase == "capture":
            check(
                u.PackageBuilderPreviewEditorLibrary.capture_preview(
                    preview_widget, str(evidence / "preview-controls.png")
                ),
                "viewport-capture",
            )
            preview_widget.call_method("ToggleOverlay")
            phase = "capture-hidden"
        elif phase == "capture-hidden":
            check(
                u.PackageBuilderPreviewEditorLibrary.capture_preview(
                    preview_widget, str(evidence / "preview-hidden.png")
                ),
                "hidden-viewport-capture",
            )
            receipt["passed"] = True
            close()
        elif phase == "finish" and not level.is_in_play_in_editor():
            (evidence / (play_key + ".json")).write_text(
                json.dumps(receipt, indent=2), encoding="utf-8"
            )
            u.unregister_slate_post_tick_callback(handle)
            u.EditorPythonScripting.set_keep_python_script_alive(False)
    except Exception:
        close(traceback.format_exc())


handle = u.register_slate_post_tick_callback(tick)
