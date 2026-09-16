"""Validate the typed host plan at the engine boundary; domain policies stay in .NET."""

from __future__ import annotations

import hashlib
import re
from pathlib import Path

from package_builder_protocol import WorkerInputError, load_bounded_json, resolve_logical_reference

SEGMENT = re.compile(r"[A-Za-z][A-Za-z0-9_]{0,39}\Z")
FOLDERS = {
    "Meshes",
    "Materials",
    "Textures",
    "Maps",
    "Documentation",
    "Skeletons",
    "Animations",
    "Blueprints",
}


def validate_segment(value):
    """Require Unreal-safe, bounded ASCII names; never silently sanitize identities."""
    if not isinstance(value, str) or not SEGMENT.fullmatch(value):
        raise WorkerInputError("Invalid Unreal name.")


def file_identity(path: Path) -> tuple[str, int]:
    """Stream a source or asset identity without loading image payloads into host memory."""
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest(), path.stat().st_size


def load_plan(path: Path, source: Path) -> dict:
    """Validate all destinations and source identities before any engine mutation."""
    plan = load_bounded_json(path)
    if not isinstance(plan, dict) or set(plan) != {
        "schemaVersion",
        "profile",
        "projectName",
        "packRoot",
        "folders",
        "textures",
    }:
        raise WorkerInputError("Unexpected plan fields.")
    if (
        type(plan["schemaVersion"]) is not int
        or plan["schemaVersion"] != 1
        or plan["profile"] != "unreal-content-v1"
    ):
        raise WorkerInputError("Unsupported import profile.")
    validate_segment(plan["projectName"])
    if plan["packRoot"] != "/Game/" + plan["projectName"]:
        raise WorkerInputError("Pack root differs from project identity.")
    folders = plan["folders"]
    if (
        not isinstance(folders, list)
        or not all(isinstance(f, str) for f in folders)
        or folders != sorted(set(folders))
        or not set(folders) <= FOLDERS
        or not {"Meshes", "Materials", "Textures", "Maps", "Documentation"} <= set(folders)
    ):
        raise WorkerInputError("Invalid content folders.")
    textures = plan["textures"]
    if not isinstance(textures, list) or len(textures) > 128:
        raise WorkerInputError("Texture count exceeds the bound.")
    names = set()
    for entry in textures:
        if not isinstance(entry, dict) or set(entry) != {
            "assetName",
            "sourceReference",
            "sha256",
            "byteCount",
            "srgb",
            "compression",
            "noAlpha",
            "flipGreenChannel",
        }:
            raise WorkerInputError("Unexpected texture fields.")
        name = entry["assetName"]
        if not isinstance(name, str) or not name.startswith("T_"):
            raise WorkerInputError("Invalid texture prefix.")
        validate_segment(name[2:])
        if name.casefold() in names:
            raise WorkerInputError("Case-insensitive texture name collision.")
        names.add(name.casefold())
        if any(type(entry[key]) is not bool for key in ("srgb", "noAlpha", "flipGreenChannel")):
            raise WorkerInputError("Import flags must be booleans.")
        if entry["compression"] not in ("TC_DEFAULT", "TC_NORMALMAP", "TC_MASKS"):
            raise WorkerInputError("Unsupported compression.")
        if (entry["compression"] != "TC_DEFAULT" and entry["srgb"]) or (
            entry["compression"] != "TC_NORMALMAP" and entry["flipGreenChannel"]
        ):
            raise WorkerInputError("Conflicting engine settings.")
        if type(entry["byteCount"]) is not int or not 0 < entry["byteCount"] <= 268_435_456:
            raise WorkerInputError("Texture byte count is invalid.")
        if not isinstance(entry["sourceReference"], str):
            raise WorkerInputError("Invalid source reference.")
        image = resolve_logical_reference(source, entry["sourceReference"])
        if image.suffix.lower() not in {".png", ".tga", ".jpg", ".jpeg", ".exr", ".tif", ".tiff"}:
            raise WorkerInputError("Unsupported texture source format.")
        if (
            not image.is_file()
            or image.stat().st_size != entry["byteCount"]
            or file_identity(image)[0] != entry["sha256"]
        ):
            raise WorkerInputError("Source texture differs from its snapshot identity.")
    if [e["assetName"] for e in textures] != sorted(e["assetName"] for e in textures):
        raise WorkerInputError("Texture plan must be in canonical order.")
    return plan
