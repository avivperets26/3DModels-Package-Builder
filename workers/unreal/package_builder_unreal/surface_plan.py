"""Closed material/mesh trust boundary, checked in full before touching the project."""

import math

from package_builder_protocol import WorkerInputError, load_bounded_json, resolve_logical_reference

from package_builder_unreal.import_plan import file_identity, validate_segment


def _fields(value, fields):
    if not isinstance(value, dict) or set(value) != set(fields.split()):
        raise WorkerInputError("Unexpected surface fields.")


def _number(value, low=None, high=None):
    if (
        type(value) not in (int, float)
        or not math.isfinite(value)
        or (low is not None and value < low)
        or (high is not None and value > high)
    ):
        raise WorkerInputError("Invalid finite surface parameter.")


def _ordered(entries):
    if not isinstance(entries, list) or len(entries) > 128:
        raise WorkerInputError("Surface count exceeds bound.")
    names = []
    for entry in entries:
        if not isinstance(entry, dict):
            raise WorkerInputError("Invalid surface entry.")
        validate_segment(entry.get("id"))
        names.append(entry["id"])
    if names != sorted(names) or len({n.casefold() for n in names}) != len(names):
        raise WorkerInputError("Surface identities collide or are unordered.")


def load_surface_plan(source, content):
    """Resolve only same-pack textures and named material instances; source FBX bytes are pinned."""
    plan = load_bounded_json(resolve_logical_reference(source, "unreal-surface-plan.json"))
    _fields(plan, "schemaVersion profile projectName materials meshes")
    if (
        type(plan["schemaVersion"]) is not int
        or plan["schemaVersion"] != 1
        or plan["profile"] != "unreal-surfaces-v1"
        or plan["projectName"] != content["projectName"]
    ):
        raise WorkerInputError("Unsupported surface profile.")
    _ordered(plan["materials"])
    _ordered(plan["meshes"])
    textures = {t["assetName"]: t for t in content["textures"]}
    for entry in plan["materials"]:
        _fields(
            entry,
            "id surface twoSided alphaCutoff metallic roughness normalScale occlusionStrength opacity emission uv textures",
        )
        if (
            entry["surface"] not in ("opaque", "cutout", "transparent")
            or type(entry["twoSided"]) is not bool
        ):
            raise WorkerInputError("Invalid surface mode.")
        for field in ("metallic", "roughness", "occlusionStrength", "opacity", "alphaCutoff"):
            _number(entry[field], 0, 1)
        _number(entry["normalScale"], 0, 1000)
        if entry["surface"] == "opaque" and entry["opacity"] != 1:
            raise WorkerInputError("Opaque surface requires full opacity.")
        for field in ("emission", "uv"):
            if not isinstance(entry[field], list) or len(entry[field]) != 4:
                raise WorkerInputError("Expected four surface components.")
            for value in entry[field]:
                _number(value, 0 if field == "emission" else -1_000_000, 1_000_000)
        if not isinstance(entry["textures"], dict) or not set(entry["textures"]) <= {
            "albedo",
            "normal",
            "orm",
            "emission",
            "opacity",
        }:
            raise WorkerInputError("Unsupported surface texture role.")
        for role, name in entry["textures"].items():
            if not isinstance(name, str) or name not in textures:
                raise WorkerInputError("Missing material texture.")
            texture = textures[name]
            srgb = role in ("albedo", "emission")
            compression = (
                "TC_NORMALMAP" if role == "normal" else "TC_DEFAULT" if srgb else "TC_MASKS"
            )
            if texture["srgb"] != srgb or texture["compression"] != compression:
                raise WorkerInputError("Texture use conflicts with import policy.")
            if role == "albedo" and texture["noAlpha"]:
                raise WorkerInputError("Albedo alpha must be preserved.")
    ids = {m["id"] for m in plan["materials"]}
    for entry in plan["meshes"]:
        _fields(
            entry,
            "id sourceReference sha256 byteCount materials collision expectedSizeCm sourceSlots",
        )
        if entry["collision"] not in ("none", "box", "complex-as-simple"):
            raise WorkerInputError("Unknown collision policy.")
        if (
            not isinstance(entry["materials"], list)
            or not 1 <= len(entry["materials"]) <= 64
            or any(not isinstance(m, str) or m not in ids for m in entry["materials"])
        ):
            raise WorkerInputError("Unresolved mesh material slots.")
        if not isinstance(entry["expectedSizeCm"], list) or len(entry["expectedSizeCm"]) != 3:
            raise WorkerInputError("Expected three size components.")
        slots = entry["sourceSlots"]
        if (
            not isinstance(slots, list)
            or len(slots) != len(entry["materials"])
            or any(not isinstance(s, str) or not s.strip() or len(s) > 128 for s in slots)
            or len(set(slots)) != len(slots)
        ):
            raise WorkerInputError("Invalid source slot inventory.")
        for dimension in entry["expectedSizeCm"]:
            _number(dimension, 0.000001, 1_000_000)
        if type(entry["byteCount"]) is not int or not 0 < entry["byteCount"] <= 268_435_456:
            raise WorkerInputError("Mesh size exceeds bound.")
        mesh = resolve_logical_reference(source, entry["sourceReference"])
        if (
            mesh.suffix.lower() != ".fbx"
            or not mesh.is_file()
            or mesh.stat().st_size != entry["byteCount"]
            or file_identity(mesh)[0] != entry["sha256"]
        ):
            raise WorkerInputError("FBX snapshot mismatch.")
    return plan
