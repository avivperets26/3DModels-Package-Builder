"""Prepare a validated content-only project; packaging itself belongs to the shared host writer."""

import json
from pathlib import Path

from package_builder_protocol import (
    atomic_write_result,
    load_bounded_json,
    resolve_logical_reference,
)

from package_builder_unreal.import_plan import file_identity
from package_builder_unreal.overview import context, map_name
from package_builder_unreal.preview_plan import PREVIEW_UI_CONFIG, load_preview_plan
from package_builder_unreal.project_validation import OverviewValidationError, validate_overview


def prepare_delivery(u, workspace, source, output):
    """Remove machine-specific reimport metadata through native objects, then revalidate.

    Native filename updates preserve approved mesh import settings while clearing source paths
    and labels. Never patch serialized uasset bytes. The caller must gate the native
    log independently and package only the returned post-save identities.
    """
    validate_overview(u, workspace, source)
    project, content, _, plan = context(u, workspace, source)
    preview = load_preview_plan(source, plan)
    registry = u.AssetRegistryHelpers.get_asset_registry()
    for entry in registry.get_assets_by_path(content["packRoot"], recursive=True):
        asset = entry.get_asset()
        if isinstance(asset, (u.StaticMesh, u.Texture)):
            previous = asset.get_editor_property("asset_import_data")
            if previous is not None:
                for index in range(len(previous.extract_filenames())):
                    previous.scripted_add_filename("", index, "")
                if not empty_import_paths(u, previous):
                    raise RuntimeError("Import metadata was not sanitized.")
                if not u.EditorAssetLibrary.save_loaded_asset(asset, only_if_is_dirty=False):
                    raise RuntimeError("Sanitized asset was not saved.")
    validate_overview(u, workspace, source)
    files = []
    for entry in registry.get_assets_by_path(content["packRoot"], recursive=True):
        name = str(entry.package_name)
        relative = "Content/" + name.removeprefix("/Game/")
        files.append(relative + (".umap" if name == map_name(plan) else ".uasset"))
    # Descriptor/config use trusted values and reviewed defaults, never local editor settings.
    # Keep the working descriptor intact so later verification can still run the editor worker.
    staging = resolve_logical_reference(output, "delivery")
    staging.mkdir(exist_ok=False)
    generated = {
        plan["projectName"] + ".uproject": json.dumps(delivery_descriptor(), indent=2) + "\n",
        "Config/DefaultEngine.ini": delivery_config(plan, preview is not None),
        "Content/" + plan["projectName"] + "/Documentation/README.md": (
            "# "
            + plan["projectName"]
            + "\n\n"
            + "Open the project with Unreal Engine 5.8.2. The overview map opens automatically.\n"
            + "The product assets are inside Content/"
            + plan["projectName"]
            + ".\n\n"
            + (
                "Press Play for the interactive studio. Drag with the left mouse button to orbit; scroll to zoom. "
                "Click the studio, then use arrows to orbit and Page Up/Page Down or +/- to zoom. "
                "R resets the camera; L resets lighting. Tab/Shift+Tab focus the labelled controls; "
                "Enter/Space activates them. Focus Light Direction and use arrows, or drag its yaw/pitch sliders. "
                "H or Hide Controls hides the panel; Show Controls restores it. Product transforms remain unchanged.\n\n"
                if preview
                else ""
            )
            + "Reimport source paths are intentionally removed. Choose a source explicitly before reimporting.\n"
        ),
    }
    items = []
    for relative in sorted(files + list(generated)):
        if relative in generated:
            path = resolve_logical_reference(staging, relative)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(generated[relative], encoding="utf-8", newline="\n")
        else:
            path = resolve_logical_reference(project, relative)
        sha, size = file_identity(path)
        items.append(
            {
                "path": relative,
                "logicalReference": path.relative_to(workspace).as_posix(),
                "sha256": sha,
                "byteCount": size,
            }
        )
    receipt = staging / "inventory.json"
    atomic_write_result(
        receipt,
        {
            "schemaVersion": 1,
            "profile": "unreal-clean-project-v1",
            "projectName": plan["projectName"],
            "engineVersion": "5.8.2",
            "files": items,
        },
    )
    digest, size = file_identity(receipt)
    return [
        {
            "artifactId": "unreal-delivery-inventory",
            "role": "engine-asset",
            "logicalReference": receipt.relative_to(workspace).as_posix(),
            "target": "unreal",
            "sha256": digest,
            "byteCount": size,
        }
    ]


def verify_delivery(u, workspace, source):
    """Independently inspect the clean extraction, with no worker plugin in the delivery."""
    project, content, _, plan = context(u, workspace, source)
    descriptor = load_bounded_json(
        resolve_logical_reference(project, content["projectName"] + ".uproject")
    )
    if descriptor != delivery_descriptor():
        raise OverviewValidationError("UNREAL_DELIVERY_DESCRIPTOR")
    config = resolve_logical_reference(project, "Config/DefaultEngine.ini")
    expected_config = delivery_config(plan, load_preview_plan(source, plan) is not None).encode(
        "utf-8"
    )
    if config.stat().st_size != len(expected_config) or config.read_bytes() != expected_config:
        raise OverviewValidationError("UNREAL_DELIVERY_CONFIG")
    artifacts, _, _, _ = validate_overview(u, workspace, source, delivery=True)
    for entry in u.AssetRegistryHelpers.get_asset_registry().get_assets_by_path(
        content["packRoot"], recursive=True
    ):
        asset = entry.get_asset()
        if isinstance(asset, (u.StaticMesh, u.Texture)):
            data = asset.get_editor_property("asset_import_data")
            if data is not None and not empty_import_paths(u, data):
                raise OverviewValidationError("UNREAL_DELIVERY_SOURCE_PATH")
    return artifacts


def delivery_descriptor():
    """Closed content-only descriptor shared by preparation and independent reopen validation."""
    return {
        "FileVersion": 3,
        "EngineAssociation": "5.8",
        "Category": "3D Assets",
        "Description": "Validated static product overview.",
        "DisableEnginePluginsByDefault": True,
        "Plugins": [],
    }


def empty_import_paths(u, data):
    """UE resolves an empty stored filename to BaseDir; no file reference may remain."""
    base = u.Paths.convert_relative_path_to_full("").replace("\\", "/").rstrip("/")
    return all(name.replace("\\", "/").rstrip("/") == base for name in data.extract_filenames())


def delivery_config(plan, preview=False):
    """Reuse the reviewed content-only template's engine defaults (including non-plugin codecs)."""
    repository = Path(__file__).resolve().parents[3]
    template = resolve_logical_reference(
        repository, "engine-templates/unreal/5.8/Config/DefaultEngine.ini"
    )
    config = template.read_text("utf-8")
    for key in ("EditorStartupMap", "GameDefaultMap"):
        if config.count(key + "=\n") != 1:
            raise RuntimeError("Template startup map changed unexpectedly.")
        config = config.replace(key + "=\n", key + "=" + map_name(plan) + "\n")
    return config + (PREVIEW_UI_CONFIG if preview else "")
