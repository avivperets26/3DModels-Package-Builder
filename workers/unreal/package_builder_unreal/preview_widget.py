"""Author a product-local UMG overlay from canonical labels and engine-native widgets."""

from package_builder_unreal.preview_graphs import Graph, state_functions

WIDGET = "/Script/UMG.Widget."
LIBRARY = "/Script/UMG.WidgetBlueprintLibrary."
MATH = "/Script/Engine.KismetMathLibrary."
ACTOR = "/Script/Engine.Actor."


def compile_widget(u, blueprint):
    """Fail immediately on graph diagnostics; never save a partially compiled customer asset."""
    if not u.BlueprintEditorLibrary.compile_blueprint(blueprint):
        raise RuntimeError("Preview Blueprint compilation failed: " + blueprint.get_name())


def widget_tree(u, root_path, contract):
    """A compact panel and separate restore control keep pointer recovery available."""
    settings = u.get_default_object(
        u.load_class(None, "/Script/UMGEditor.WidgetEditingProjectSettings")
    )
    previous = settings.get_editor_property("DefaultRootWidget")
    try:
        settings.set_editor_property("DefaultRootWidget", u.CanvasPanel)
        bp = u.AssetToolsHelpers.get_asset_tools().create_asset(
            "WBP_Preview", root_path, u.WidgetBlueprint, u.WidgetBlueprintFactory()
        )
    finally:
        settings.set_editor_property("DefaultRootWidget", previous)
    tree = u.find_object(bp, "WidgetTree")
    root = u.find_object(tree, "CanvasPanel_0")
    root.set_visibility(u.SlateVisibility.VISIBLE)
    labels = {x["id"]: x for x in contract["accessibility"]["controls"]}

    def new(kind, name):
        value = u.new_object(kind, outer=tree, name=name)
        return value

    panel = new(u.Border, "ControlsPanel")
    panel.set_brush_color(u.LinearColor(0.015, 0.018, 0.025, 0.94))
    panel.set_padding(u.Margin(12))
    slot = root.add_child_to_canvas(panel)
    slot.set_position(u.Vector2D(20, 20))
    slot.set_size(u.Vector2D(280, 360))
    column = new(u.VerticalBox, "ControlsColumn")
    panel.add_child(column)

    def text(parent, name, label):
        value = new(u.TextBlock, name)
        value.set_text(label)
        font = value.get_editor_property("font")
        font.set_editor_property("size", 13)
        value.set_font(font)
        value.set_auto_wrap_text(True)
        value.set_color_and_opacity(u.SlateColor(specified_color=u.LinearColor(0.93, 0.95, 1, 1)))
        parent.add_child(value)
        return value

    text(column, "Title", "STUDIO PREVIEW")
    text(
        column,
        "Help",
        "Drag: orbit  |  Wheel: zoom\nArrows: orbit  |  +/-: zoom\nTab: focus  |  Enter: activate\nR: reset view  |  L: reset light  |  H: hide",
    )

    def button(parent, name, identity):
        value = new(u.Button, name)
        value.set_editor_property("is_focusable", True)
        spec = labels[identity]
        value.set_tool_tip_text(spec["accessibleHelp"])
        value.set_editor_property("override_accessible_defaults", True)
        value.set_editor_property("accessible_behavior", u.SlateAccessibleBehavior.CUSTOM)
        value.set_editor_property("accessible_text", spec["accessibleName"])
        value.set_editor_property("accessible_summary_behavior", u.SlateAccessibleBehavior.CUSTOM)
        value.set_editor_property("accessible_summary_text", spec["accessibleHelp"])
        parent.add_child(value)
        text(value, name + "Label", spec["label"]).slot.set_padding(u.Margin(6))
        return value

    button(column, "ResetViewButton", "reset-view")
    button(column, "LightButton", "light-direction")
    for name, label in (("YawSlider", "Key light yaw"), ("PitchSlider", "Key light pitch")):
        text(column, name + "Label", label)
        slider = new(u.Slider, name)
        slider.set_editor_property("is_focusable", False)
        slider.set_tool_tip_text(label + "; focus Light Direction for keyboard arrows.")
        column.add_child(slider)
    button(column, "ResetLightButton", "reset-light")
    button(column, "HideButton", "hide-controls")
    show = button(root, "ShowButton", "show-controls")
    show.slot.set_position(u.Vector2D(20, 20))
    show.slot.set_size(u.Vector2D(170, 36))
    show.set_visibility(u.SlateVisibility.COLLAPSED)
    if not u.PackageBuilderPreviewEditorLibrary.register_widget_variables(bp):
        raise RuntimeError("Preview widget registration failed.")
    compile_widget(u, bp)
    return bp


def variables(u, bp, contract):
    """Declare state independently of product transforms; only camera/light references are writable."""
    lib = u.BlueprintEditorLibrary
    editor = u.BlueprintGraphEditor.get_graph_editor_by_name(bp, "EventGraph")
    light = contract["presentation"]["lighting"]["keyLight"]
    for name, default in {
        "Yaw": 0,
        "Pitch": 0,
        "Distance": 1,
        "LightYaw": light["yawDegrees"],
        "LightPitch": light["pitchDegrees"],
        "BaseDistance": 1,
        "BaseYaw": 0,
        "BasePitch": 0,
    }.items():
        editor.add_member_variable(name, lib.get_basic_type_by_name("real"), str(default))
    for name, default in (("Dragging", False), ("OverlayVisible", True)):
        editor.add_member_variable(name, lib.get_basic_type_by_name("bool"), str(default).lower())
    editor.add_member_variable("FocusIndex", lib.get_basic_type_by_name("int"), "-1")
    for name, kind in (("Camera", u.Actor), ("KeyLight", u.Actor), ("SelfWidget", u.UserWidget)):
        editor.add_member_variable(name, lib.get_object_reference_type(kind))
    for name in ("Centre",):
        editor.add_member_variable(name, lib.get_struct_type(u.Vector.static_struct()))
    compile_widget(u, bp)
    u.get_default_object(bp.generated_class()).set_editor_property("is_focusable", True)
    u.get_default_object(bp.generated_class()).set_visibility(u.SlateVisibility.VISIBLE)


def apply_functions(u, bp, contract):
    """Convert shared state to Unreal coordinates without moving or scaling the product."""
    g = Graph(u, bp, "ApplyCamera")
    g.guard(g.pure("/Script/Engine.KismetSystemLibrary.IsValid", Object=g.get("Camera")))
    policy = contract["navigation"]
    pitch = g.clamp(
        g.sum(g.get("BasePitch"), g.get("Pitch")),
        policy["minimumPitchDegrees"],
        policy["maximumPitchDegrees"],
    )
    rotation = g.math("MakeRotator", Pitch=pitch, Yaw=g.sum(g.get("BaseYaw"), g.get("Yaw")), Roll=0)
    direction = g.math("GetForwardVector", InRot=rotation)
    offset = g.math(
        "Multiply_VectorFloat", A=direction, B=g.product(g.get("BaseDistance"), g.get("Distance"))
    )
    position = g.math("Add_VectorVector", A=g.get("Centre"), B=offset)
    look = g.math("FindLookAtRotation", Start=position, Target=g.get("Centre"))
    g.call(
        ACTOR + "K2_SetActorLocationAndRotation",
        self=g.get("Camera"),
        NewLocation=position,
        NewRotation=look,
        bSweep=False,
        bTeleport=True,
    )
    g = Graph(u, bp, "ApplyLight")
    g.guard(g.pure("/Script/Engine.KismetSystemLibrary.IsValid", Object=g.get("KeyLight")))
    rotation = g.math(
        "MakeRotator", Pitch=g.product(g.get("LightPitch"), -1), Yaw=g.get("LightYaw"), Roll=0
    )
    g.call(
        ACTOR + "K2_SetActorRotation",
        self=g.get("KeyLight"),
        NewRotation=rotation,
        bTeleportPhysics=True,
    )
    for member, slider, minimum, maximum in (
        ("LightYaw", "YawSlider", -180, 180),
        (
            "LightPitch",
            "PitchSlider",
            contract["lightControls"]["minimumPitchDegrees"],
            contract["lightControls"]["maximumPitchDegrees"],
        ),
    ):
        normalized = g.math(
            "Divide_DoubleDouble",
            A=g.math("Subtract_DoubleDouble", A=g.get(member), B=minimum),
            B=maximum - minimum,
        )
        g.call("/Script/UMG.Slider.SetValue", self=g.get(slider), InValue=normalized)
    compile_widget(u, bp)
    state_functions(u, bp, contract, apply=True)
    compile_widget(u, bp)
    for name, values, update in (
        ("ResetView", {"Yaw": 0, "Pitch": 0, "Distance": 1}, "ApplyCamera"),
        (
            "ResetLight",
            {
                "LightYaw": contract["presentation"]["lighting"]["keyLight"]["yawDegrees"],
                "LightPitch": contract["presentation"]["lighting"]["keyLight"]["pitchDegrees"],
            },
            "ApplyLight",
        ),
    ):
        g = Graph(u, bp, name)
        for field, value in values.items():
            g.set(field, value)
        g.local(update)
    compile_widget(u, bp)


def focus_functions(u, bp):
    """A deterministic focus cycle also supplies an explicit high-contrast focus highlight."""
    g = Graph(u, bp, "FocusControl")
    value = g.input("Index", "int")
    g.set("FocusIndex", value)
    for index, name in enumerate(
        ("ResetViewButton", "LightButton", "ResetLightButton", "HideButton", "ShowButton")
    ):
        selected = g.math("EqualEqual_IntInt", A=g.get("FocusIndex"), B=index)
        colour = g.math(
            "SelectColor",
            A="(R=0.12,G=0.4,B=0.7,A=1)",
            B="(R=0.06,G=0.075,B=0.1,A=1)",
            bPickA=selected,
        )
        g.call("/Script/UMG.Button.SetBackgroundColor", self=g.get(name), InBackgroundColor=colour)
    for index, name in enumerate(
        ("ResetViewButton", "LightButton", "ResetLightButton", "HideButton", "ShowButton")
    ):
        other = g.branch(g.math("EqualEqual_IntInt", A=g.get("FocusIndex"), B=index))
        g.call(WIDGET + "SetKeyboardFocus", self=g.get(name))
        g.tail = other
    compile_widget(u, bp)
    g = Graph(u, bp, "ToggleOverlay")
    other = g.branch(g.get("OverlayVisible"))
    g.set("OverlayVisible", False)
    g.call(WIDGET + "SetVisibility", self=g.get("ControlsPanel"), InVisibility="Collapsed")
    g.call(WIDGET + "SetVisibility", self=g.get("ShowButton"), InVisibility="Visible")
    g.local("FocusControl", Index=4)
    g.tail = other
    g.set("OverlayVisible", True)
    g.call(WIDGET + "SetVisibility", self=g.get("ControlsPanel"), InVisibility="Visible")
    g.call(WIDGET + "SetVisibility", self=g.get("ShowButton"), InVisibility="Collapsed")
    g.local("FocusControl", Index=0)
    compile_widget(u, bp)


def bind_controls(u, bp, contract):
    """UMG click/value delegates call the same functions used by pointer and keyboard actions."""
    for name, function, index in (
        ("ResetViewButton", "ResetView", 0),
        ("LightButton", None, 1),
        ("ResetLightButton", "ResetLight", 2),
        ("HideButton", "ToggleOverlay", None),
        ("ShowButton", "ToggleOverlay", None),
    ):
        event = u.PackageBuilderPreviewEditorLibrary.bind_widget_event(bp, name, "OnClicked")
        g = Graph(u, bp, "", event=event)
        if index is not None:
            g.local("FocusControl", Index=index)
        if function:
            g.local(function)
    for name, field, minimum, maximum in (
        ("YawSlider", "LightYaw", -180, 180),
        (
            "PitchSlider",
            "LightPitch",
            contract["lightControls"]["minimumPitchDegrees"],
            contract["lightControls"]["maximumPitchDegrees"],
        ),
    ):
        event = u.PackageBuilderPreviewEditorLibrary.bind_widget_event(bp, name, "OnValueChanged")
        g = Graph(u, bp, "", event=event)
        g.set(field, g.sum(minimum, g.product(event.find_output_pin("Value"), maximum - minimum)))
        g.local("ApplyLight")
    compile_widget(u, bp)
