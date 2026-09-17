"""Map native UMG input events to the shared preview actions and generated state functions."""

from package_builder_unreal.preview_graphs import Graph
from package_builder_unreal.preview_widget import LIBRARY, WIDGET, compile_widget

INPUT = "/Script/Engine.KismetInputLibrary."


def reply(g, handled=True):
    return g.pure(LIBRARY + ("Handled" if handled else "Unhandled"))


def key_equal(g, key, name):
    # FKey has custom native text serialization: its token is the key name, not a struct literal.
    return g.pure(INPUT + "EqualEqual_KeyKey", A=key, B=name)


def keyboard(u, bp, contract):
    """Handle shortcuts before focused buttons consume them; non-preview keys remain unhandled."""
    g = Graph(u, bp, "OnPreviewKeyDown", override=True)
    event = g.parameter("InKeyEvent")
    key = g.pure(INPUT + "GetKey", Input=event)
    nav, light = contract["navigation"], contract["lightControls"]
    for names, action in (
        (("R",), "ResetView"),
        (("L",), "ResetLight"),
        (("H",), "ToggleOverlay"),
    ):
        other = g.branch(key_equal(g, key, names[0]))
        g.local(action)
        g.result(reply(g))
        g.tail = other
    other = g.branch(key_equal(g, key, "Tab"))
    visible = g.branch(g.get("OverlayVisible"))
    shift = g.pure(INPUT + "InputEvent_IsShiftDown", Input=event)
    step = g.math("SelectInt", A=-1, B=1, bPickA=shift)
    initial_reverse = g.math(
        "BooleanAND", A=shift, B=g.math("Less_IntInt", A=g.get("FocusIndex"), B=0)
    )
    current = g.math("SelectInt", A=0, B=g.get("FocusIndex"), bPickA=initial_reverse)
    index = g.math(
        "Percent_IntInt",
        A=g.math("Add_IntInt", A=g.math("Add_IntInt", A=current, B=step), B=4),
        B=4,
    )
    g.local("FocusControl", Index=index)
    g.result(reply(g))
    g.tail = visible
    g.local("FocusControl", Index=4)
    g.result(reply(g))
    g.tail = other
    activate = g.math("BooleanOR", A=key_equal(g, key, "Enter"), B=key_equal(g, key, "SpaceBar"))
    other = g.branch(activate)
    for index, action in (
        (0, "ResetView"),
        (2, "ResetLight"),
        (3, "ToggleOverlay"),
        (4, "ToggleOverlay"),
    ):
        next_index = g.branch(g.math("EqualEqual_IntInt", A=g.get("FocusIndex"), B=index))
        g.local(action)
        g.result(reply(g))
        g.tail = next_index
    g.result(reply(g))
    g.tail = other
    for name, dx, dy in (("Left", -1, 0), ("Right", 1, 0), ("Up", 0, 1), ("Down", 0, -1)):
        other = g.branch(key_equal(g, key, name))
        camera = g.branch(g.math("EqualEqual_IntInt", A=g.get("FocusIndex"), B=1))
        g.local(
            "AdjustLight",
            DeltaYaw=dx * light["keyboardStepDegrees"],
            DeltaPitch=dy * light["keyboardStepDegrees"],
        )
        g.result(reply(g))
        g.tail = camera
        # UI-focused arrows do not move the camera. Click the studio to return focus.
        no_camera = g.branch(g.math("EqualEqual_IntInt", A=g.get("FocusIndex"), B=-1))
        g.local(
            "Orbit",
            DeltaYaw=dx * nav["keyboardOrbitStepDegrees"],
            DeltaPitch=dy * nav["keyboardOrbitStepDegrees"],
        )
        g.result(reply(g))
        g.tail = no_camera
        g.result(reply(g))
        g.tail = other
    for name, step in (
        ("PageUp", 1),
        ("Equals", 1),
        ("Add", 1),
        ("PageDown", -1),
        ("Hyphen", -1),
        ("Subtract", -1),
    ):
        other = g.branch(key_equal(g, key, name))
        no_camera = g.branch(g.math("EqualEqual_IntInt", A=g.get("FocusIndex"), B=-1))
        g.local("Zoom", Steps=step, Fraction=nav["keyboardZoomStep"])
        g.result(reply(g))
        g.tail = no_camera
        g.result(reply(g))
        g.tail = other
    g.result(reply(g, False))
    compile_widget(u, bp)


def pointer(u, bp, contract):
    """Capture only primary drags started on the studio; UI-owned input never orbits the model."""
    nav = contract["navigation"]
    for name in ("OnMouseButtonDown", "OnMouseButtonUp", "OnMouseMove", "OnMouseWheel"):
        g = Graph(u, bp, name, override=True)
        event = g.parameter("MouseEvent")
        if name == "OnMouseButtonDown":
            # Empty panel space also owns the pointer; do not start a camera drag through it.
            clear = g.branch(
                g.math(
                    "BooleanOR",
                    A=g.pure(WIDGET + "IsHovered", self=g.get("ControlsPanel")),
                    B=g.pure(WIDGET + "IsHovered", self=g.get("ShowButton")),
                )
            )
            g.result(reply(g, False))
            g.tail = clear
            other = g.branch(
                key_equal(
                    g,
                    g.pure(INPUT + "PointerEvent_GetEffectingButton", Input=event),
                    "LeftMouseButton",
                )
            )
            g.set("Dragging", True)
            g.local("FocusControl", Index=-1)
            g.call(WIDGET + "SetKeyboardFocus", self=g.self_pin())
            value = g.pure(LIBRARY + "CaptureMouse", Reply=reply(g), CapturingWidget=g.self_pin())
            g.result(value)
            g.tail = other
        elif name == "OnMouseButtonUp":
            other = g.branch(g.get("Dragging"))
            g.set("Dragging", False)
            g.result(g.pure(LIBRARY + "ReleaseMouseCapture", Reply=reply(g)))
            g.tail = other
        elif name == "OnMouseMove":
            other = g.branch(g.get("Dragging"))
            held = g.pure(
                INPUT + "PointerEvent_IsMouseButtonDown", Input=event, MouseButton="LeftMouseButton"
            )
            released = g.branch(held)
            delta = g.pure(INPUT + "PointerEvent_GetCursorDelta", Input=event)
            split = g.node("/Script/Engine.KismetMathLibrary.BreakVector2D", InVec=delta)
            g.local(
                "Orbit",
                DeltaYaw=g.product(split.find_output_pin("X"), nav["pointerOrbitDegreesPerUnit"]),
                DeltaPitch=g.product(
                    split.find_output_pin("Y"), -nav["pointerOrbitDegreesPerUnit"]
                ),
            )
            g.result(reply(g))
            g.tail = released
            g.set("Dragging", False)
            g.result(g.pure(LIBRARY + "ReleaseMouseCapture", Reply=reply(g)))
            g.tail = other
        else:
            hovered = g.math(
                "BooleanOR",
                A=g.pure(WIDGET + "IsHovered", self=g.get("ControlsPanel")),
                B=g.pure(WIDGET + "IsHovered", self=g.get("ShowButton")),
            )
            other = g.branch(g.math("Not_PreBool", A=hovered))
            g.local(
                "Zoom",
                Steps=g.pure(INPUT + "PointerEvent_GetWheelDelta", Input=event),
                Fraction=nav["pointerZoomStep"],
            )
            g.result(reply(g))
            g.tail = other
        g.result(reply(g, False))
    event = u.BlueprintEditorLibrary.add_event_override(bp, "OnMouseCaptureLost", u.IntPoint(0, 0))
    g = Graph(u, bp, "", event=event)
    g.set("Dragging", False)
    compile_widget(u, bp)
