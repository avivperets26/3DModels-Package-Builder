"""Validate the native adapter's supported contract surface before authoring any assets."""

from package_builder_protocol import WorkerInputError, load_bounded_json, resolve_logical_reference

from package_builder_unreal.surface_plan import _fields, _number

PREVIEW_ASSETS = ("WBP_Preview", "BP_Preview", "BP_PreviewGameMode")

# Keep the compact 13-point panel readable in small editor viewports as well as 1080p.
PREVIEW_UI_CONFIG = "\n[/Script/Engine.UserInterfaceSettings]\nUIScaleCurve=(EditorCurveData=(Keys=((Time=0.000000,Value=1.000000),(Time=2160.000000,Value=1.000000))),ExternalCurve=None)\n"


def load_preview_plan(source, overview):
    """Absence means static intent; an explicit plan requires the entire interactive asset closure."""
    path = resolve_logical_reference(source, "unreal-preview-plan.json")
    if not path.exists():
        return None
    plan = load_bounded_json(path)
    _fields(plan, "schemaVersion profile projectName experience")
    if (
        type(plan["schemaVersion"]) is not int
        or plan["schemaVersion"] != 1
        or plan["profile"] != "unreal-interactive-preview-v1"
        or plan["projectName"] != overview["projectName"]
    ):
        raise WorkerInputError("Unsupported interactive preview plan.")
    value = plan["experience"]
    _fields(
        value,
        "schemaVersion contractVersion presentation navigation lightControls overlay itemSelection animationTransport bindings accessibility",
    )
    if any(
        type(value[k]) is not int or value[k] != 1 for k in ("schemaVersion", "contractVersion")
    ):
        raise WorkerInputError("Unsupported interactive contract version.")
    required_bindings = {
        ("OrbitPointer", "pointer-primary-drag"),
        ("OrbitUp", "arrow-up"),
        ("OrbitDown", "arrow-down"),
        ("OrbitLeft", "arrow-left"),
        ("OrbitRight", "arrow-right"),
        ("ZoomPointer", "pointer-wheel"),
        ("ZoomIn", "page-up"),
        ("ZoomIn", "equals"),
        ("ZoomIn", "numpad-plus"),
        ("ZoomOut", "page-down"),
        ("ZoomOut", "minus"),
        ("ZoomOut", "numpad-minus"),
        ("ResetView", "r"),
        ("ResetLight", "l"),
        ("LightDirectionPointer", "focused-slider-pointer"),
        ("LightUp", "focused-light-arrow-up"),
        ("LightDown", "focused-light-arrow-down"),
        ("LightLeft", "focused-light-arrow-left"),
        ("LightRight", "focused-light-arrow-right"),
        ("ToggleOverlay", "h"),
        ("FocusNext", "tab"),
        ("FocusPrevious", "shift-tab"),
        ("ActivateFocusedControl", "enter"),
        ("ActivateFocusedControl", "space"),
    }
    bindings = value["bindings"]
    if not isinstance(bindings, list) or len(bindings) > 64:
        raise WorkerInputError("Invalid preview bindings.")
    for binding in bindings:
        _fields(binding, "action input")
        if not all(isinstance(v, str) and len(v) <= 80 for v in binding.values()):
            raise WorkerInputError("Invalid preview binding.")
    actual_bindings = {(b["action"], b["input"]) for b in bindings}
    actions = {action for action, _ in required_bindings}
    if {pair for pair in actual_bindings if pair[0] in actions} != required_bindings:
        raise WorkerInputError("Unsupported native input binding; update the adapter explicitly.")
    for name, fields in (
        (
            "navigation",
            "minimumPitchDegrees maximumPitchDegrees minimumDistanceMultiplier maximumDistanceMultiplier pointerOrbitDegreesPerUnit keyboardOrbitStepDegrees pointerZoomStep keyboardZoomStep",
        ),
        (
            "lightControls",
            "minimumPitchDegrees maximumPitchDegrees pointerStepDegrees keyboardStepDegrees",
        ),
    ):
        policy = value[name]
        _fields(policy, fields)
        for number in policy.values():
            _number(number, -90, 360)
        if not -90 <= policy["minimumPitchDegrees"] < policy["maximumPitchDegrees"] <= 90:
            raise WorkerInputError("Invalid preview pitch bounds.")
        for key, number in policy.items():
            if key not in ("minimumPitchDegrees", "maximumPitchDegrees") and number <= 0:
                raise WorkerInputError("Invalid preview interaction step.")
    nav = value["navigation"]
    if not (
        nav["minimumDistanceMultiplier"] <= 1 <= nav["maximumDistanceMultiplier"]
        and nav["minimumDistanceMultiplier"] < nav["maximumDistanceMultiplier"]
        and nav["pointerZoomStep"] < 1
        and nav["keyboardZoomStep"] < 1
    ):
        raise WorkerInputError("Invalid preview distance policy.")
    _fields(value["overlay"], "initiallyVisible restoreControlId")
    if value["overlay"] != {"initiallyVisible": True, "restoreControlId": "show-controls"}:
        raise WorkerInputError("Unsupported overlay recovery policy.")
    _fields(value["accessibility"], "visibleFocusRequired controls")
    if value["accessibility"]["visibleFocusRequired"] is not True:
        raise WorkerInputError("Visible keyboard focus is required.")
    controls = value["accessibility"]["controls"]
    if not isinstance(controls, list) or not 1 <= len(controls) <= 32:
        raise WorkerInputError("Invalid preview control list.")
    ids = set()
    orders = {}
    for control in controls:
        _fields(control, "id label accessibleName accessibleHelp focusOrder")
        for field in ("id", "label", "accessibleName", "accessibleHelp"):
            text = control[field]
            if (
                not isinstance(text, str)
                or not 1 <= len(text) <= 256
                or any(ord(c) < 32 for c in text)
            ):
                raise WorkerInputError("Invalid preview label.")
        if (
            control["id"] in ids
            or type(control["focusOrder"]) is not int
            or control["focusOrder"] < 0
            or control["focusOrder"] in orders.values()
        ):
            raise WorkerInputError("Invalid or duplicate preview control order.")
        ids.add(control["id"])
        orders[control["id"]] = control["focusOrder"]
    if (
        not {"reset-view", "light-direction", "reset-light", "hide-controls", "show-controls"}
        <= ids
    ):
        raise WorkerInputError("Missing preview controls.")
    supported = ("reset-view", "light-direction", "reset-light", "hide-controls", "show-controls")
    if sorted(supported, key=orders.get) != list(supported):
        raise WorkerInputError("Unsupported native focus order; update the adapter explicitly.")
    _fields(value["presentation"], "background lighting")
    background = value["presentation"]["background"]
    _fields(background, "outerColour centreColour centreX centreY radius horizontalScale")
    for field, native in (("outerColour", "outer"), ("centreColour", "centre")):
        _fields(background[field], "red green blue")
        if [background[field][c] for c in ("red", "green", "blue")] != overview["background"][
            native
        ]:
            raise WorkerInputError("Preview and overview background disagree.")
    if any(
        background[k] != overview["background"][k]
        for k in ("centreX", "centreY", "radius", "horizontalScale")
    ):
        raise WorkerInputError("Preview and overview background disagree.")
    _fields(value["presentation"]["lighting"], "keyLight fillLight")
    for name, intent in zip(("keyLight", "fillLight"), overview["lights"], strict=True):
        light = value["presentation"]["lighting"][name]
        _fields(light, "yawDegrees pitchDegrees intensity colour")
        _fields(light["colour"], "red green blue")
        if (
            any(light[k] != intent[k] for k in ("yawDegrees", "pitchDegrees", "intensity"))
            or [light["colour"][k] for k in ("red", "green", "blue")] != intent["colour"]
        ):
            raise WorkerInputError("Preview and overview lighting disagree.")
    return value
