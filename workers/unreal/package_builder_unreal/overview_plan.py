"""Closed static-overview wire boundary; host Domain supplies presentation semantics."""

import unicodedata

from package_builder_protocol import WorkerInputError, load_bounded_json, resolve_logical_reference

from package_builder_unreal.import_plan import validate_segment
from package_builder_unreal.surface_plan import _fields, _number

KINDS = {
    "hero",
    "orthographic-front",
    "orthographic-back",
    "orthographic-left",
    "orthographic-right",
}


def load_overview_plan(source, surfaces):
    """Reject unknown fields, output paths, unsupported views and unbounded scene parameters."""
    plan = load_bounded_json(resolve_logical_reference(source, "unreal-overview-plan.json"))
    _fields(
        plan,
        "schemaVersion profile projectName meshId label width height fieldOfView padding views background lights",
    )
    if (
        type(plan["schemaVersion"]) is not int
        or plan["schemaVersion"] != 1
        or plan["profile"] != "unreal-overview-v1"
        or plan["projectName"] != surfaces["projectName"]
        or len(surfaces["meshes"]) != 1
        or plan["meshId"] != surfaces["meshes"][0]["id"]
        or type(plan["width"]) is not int
        or type(plan["height"]) is not int
        or (plan["width"], plan["height"]) != (1920, 1080)
    ):
        raise WorkerInputError("Unsupported overview profile.")
    label = plan["label"]
    if label is not None and (
        not isinstance(label, str)
        or not 1 <= len(label) <= 120
        or any(unicodedata.category(c) == "Cc" for c in label)
    ):
        raise WorkerInputError("Invalid label.")
    _number(plan["fieldOfView"], 10, 100)
    _number(plan["padding"], 1.01, 3)
    views = plan["views"]
    if not isinstance(views, list) or not 1 <= len(views) <= 32:
        raise WorkerInputError("Invalid view count.")
    ids = set()
    for view in views:
        _fields(view, "id kind")
        validate_segment(view["id"])
        if view["id"].casefold() in ids or view["kind"] not in KINDS:
            raise WorkerInputError("Invalid or duplicate view.")
        ids.add(view["id"].casefold())
    if not any(v["kind"] == "hero" for v in views):
        raise WorkerInputError("Hero view required.")
    background = plan["background"]
    _fields(background, "outer centre centreX centreY radius horizontalScale")
    for field in ("outer", "centre"):
        colour(background[field])
    for field in ("centreX", "centreY"):
        _number(background[field], 0, 1)
    for field in ("radius", "horizontalScale"):
        _number(background[field], 0.01, 10)
    if not isinstance(plan["lights"], list) or len(plan["lights"]) != 2:
        raise WorkerInputError("Key and fill required.")
    for light in plan["lights"]:
        _fields(light, "yawDegrees pitchDegrees intensity colour")
        _number(light["yawDegrees"], -180, 180)
        _number(light["pitchDegrees"], -90, 90)
        _number(light["intensity"], 0, 100)
        colour(light["colour"])
    return plan


def colour(value):
    """Bound linear RGB without accepting booleans as numbers."""
    if not isinstance(value, list) or len(value) != 3:
        raise WorkerInputError("RGB required.")
    for channel in value:
        _number(channel, 0, 1)
