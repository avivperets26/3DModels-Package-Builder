"""Generate a content-only interactive overview using the shared preview contract."""

import math

from package_builder_unreal.overview import actor_editor, artifact, bounds_of, context, map_name
from package_builder_unreal.preview_graphs import Graph
from package_builder_unreal.preview_input import keyboard, pointer
from package_builder_unreal.preview_plan import PREVIEW_ASSETS, load_preview_plan
from package_builder_unreal.preview_widget import (
    apply_functions,
    bind_controls,
    compile_widget,
    focus_functions,
    variables,
    widget_tree,
)


def create_preview(u, workspace, source):
    """Create only native Blueprint/UMG assets; no editor helper classes enter the delivery."""
    _, content, _, plan = context(u, workspace, source)
    contract = load_preview_plan(source, plan)
    if contract is None:
        raise RuntimeError("Interactive intent is required before generating preview assets.")
    root = content["packRoot"] + "/Preview"
    if any(u.EditorAssetLibrary.does_asset_exist(root + "/" + n) for n in PREVIEW_ASSETS):
        raise RuntimeError("Preview destination already exists.")
    world = u.EditorLoadingAndSavingUtils.load_map(map_name(plan))
    editor = actor_editor(u)
    actors = {a.get_actor_label(): a for a in editor.get_all_level_actors()}
    bp = widget_tree(u, root, contract)
    variables(u, bp, contract)
    low, high = bounds_of(actors["PB_Product"])
    centre = u.Vector(*((a + b) / 2 for a, b in zip(low, high, strict=True)))
    defaults = u.get_default_object(bp.generated_class())
    defaults.set_editor_property("Centre", centre)
    offset = actors["PB_Camera"].get_actor_location() - centre
    defaults.set_editor_property("BaseDistance", math.sqrt(offset.x**2 + offset.y**2 + offset.z**2))
    defaults.set_editor_property("BaseYaw", math.degrees(math.atan2(offset.y, offset.x)))
    defaults.set_editor_property(
        "BasePitch", math.degrees(math.atan2(offset.z, math.hypot(offset.x, offset.y)))
    )
    apply_functions(u, bp, contract)
    focus_functions(u, bp)
    bind_controls(u, bp, contract)
    keyboard(u, bp, contract)
    pointer(u, bp, contract)
    g = Graph(u, bp, "InitializePreview")
    for name, kind in (("Camera", u.Actor), ("KeyLight", u.Actor), ("SelfWidget", u.UserWidget)):
        pin = g.editor.add_graph_input_parameter(
            name + "Arg", u.BlueprintEditorLibrary.get_object_reference_type(kind)
        )
        g.set(name, pin)
    g.local("ResetView")
    g.local("ResetLight")
    g.call("/Script/UMG.Widget.SetKeyboardFocus", self=g.self_pin())
    compile_widget(u, bp)
    actor_bp = u.BlueprintEditorLibrary.create_blueprint_asset_with_parent(
        root + "/BP_Preview", u.Actor
    )
    events = u.BlueprintGraphEditor.get_graph_editor_by_name(actor_bp, "EventGraph")
    for name in ("Camera", "KeyLight"):
        events.add_member_variable(
            name, u.BlueprintEditorLibrary.get_object_reference_type(u.Actor)
        )
        u.BlueprintEditorLibrary.set_blueprint_variable_instance_editable(actor_bp, name, True)
    compile_widget(u, actor_bp)
    event = u.BlueprintEditorLibrary.add_event_override(
        actor_bp, "ReceiveBeginPlay", u.IntPoint(0, 0)
    )
    g = Graph(u, actor_bp, "", event=event)
    player = g.pure("/Script/Engine.GameplayStatics.GetPlayerController", PlayerIndex=0)
    node = u.PackageBuilderPreviewEditorLibrary.add_create_widget(
        g.editor.get_graph(), bp.generated_class()
    )
    g.connect(player, node.find_input_pin("OwningPlayer"))
    g.connect(g.tail, node.find_execute_pin())
    g.tail = node.find_then_pin()
    widget = node.find_output_pin("ReturnValue")
    g.call("/Script/UMG.UserWidget.AddToViewport", self=widget, ZOrder=0)
    g.call(
        bp.generated_class().get_path_name() + ".InitializePreview",
        self=widget,
        CameraArg=g.get("Camera"),
        KeyLightArg=g.get("KeyLight"),
        SelfWidgetArg=widget,
    )
    g.call(
        "/Script/Engine.PlayerController.SetViewTargetWithBlend",
        self=player,
        NewViewTarget=g.get("Camera"),
        BlendTime=0,
    )
    cursor = g.editor.add_set_member_variable_node(
        "bShowMouseCursor", "/Script/Engine.PlayerController"
    )
    g.connect(player, cursor.find_self_pin())
    g.connect(True, cursor.find_input_pin("bShowMouseCursor"))
    g.connect(g.tail, cursor.find_execute_pin())
    g.tail = cursor.find_then_pin()
    g.call(
        "/Script/UMG.WidgetBlueprintLibrary.SetInputMode_GameAndUIEx",
        PlayerController=player,
        InWidgetToFocus=widget,
        bHideCursorDuringCapture=False,
    )
    compile_widget(u, actor_bp)
    actor = editor.spawn_actor_from_class(actor_bp.generated_class(), u.Vector())
    actor.set_actor_label("PB_Preview")
    for name in ("Camera", "KeyLight"):
        actor.set_editor_property(name, actors["PB_Camera" if name == "Camera" else "PB_Key"])
    mode = u.BlueprintEditorLibrary.create_blueprint_asset_with_parent(
        root + "/BP_PreviewGameMode", u.GameModeBase
    )
    compile_widget(u, mode)
    defaults = u.get_default_object(mode.generated_class())
    defaults.set_editor_property("default_pawn_class", None)
    # The controller unconditionally spawns HUDClass; an empty native HUD avoids a null-class warning.
    defaults.set_editor_property("hud_class", u.HUD)
    world.get_world_settings().set_editor_property("default_game_mode", mode.generated_class())
    for asset in (bp, actor_bp, mode):
        if not u.EditorAssetLibrary.save_loaded_asset(asset, only_if_is_dirty=False):
            raise RuntimeError("Preview asset save failed.")
    if not u.EditorLoadingAndSavingUtils.save_map(world, map_name(plan)):
        raise RuntimeError("Interactive map save failed.")
    return [artifact(workspace, plan, "Preview/" + name + ".uasset") for name in PREVIEW_ASSETS]
