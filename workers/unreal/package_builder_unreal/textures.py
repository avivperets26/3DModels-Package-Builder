"""Apply explicit host texture policies and independently verify persisted Unreal assets."""

from pathlib import Path
from typing import Any

from package_builder_protocol import WorkerInputError, resolve_logical_reference

from package_builder_unreal.import_plan import file_identity, load_plan


class TexturePolicyError(RuntimeError):
    """Carries a fixed engine-property diagnostic without paths or vendor exception text."""


def _properties(unreal, entry):
    return {
        "compression_settings": getattr(unreal.TextureCompressionSettings, entry["compression"]),
        "srgb": entry["srgb"],
        "compression_no_alpha": entry["noAlpha"],
        "flip_green_channel": entry["flipGreenChannel"],
    }


def import_textures(unreal: Any, workspace: Path, source: Path, verify: bool) -> list[dict]:
    """Preflight the complete plan, create folders, import without overwrite, save and read back.

    Verification never fixes a setting: a fresh process must observe the saved policy exactly.
    Any failure leaves this disposable clone unpromotable; its host owns cleanup.
    """
    project = resolve_logical_reference(workspace, "project")
    if Path(unreal.Paths.project_dir()).resolve(strict=True) != project:
        raise WorkerInputError("Unexpected running project.")
    plan = load_plan(resolve_logical_reference(source, "unreal-import-plan.json"), source)
    if not (project / (plan["projectName"] + ".uproject")).is_file():
        raise WorkerInputError("Project identity does not match plan.")
    library = unreal.EditorAssetLibrary
    # Resolve every physical target before creating any folders or assets.
    targets = []
    for entry in plan["textures"]:
        virtual = plan["packRoot"] + "/Textures/" + entry["assetName"]
        physical = resolve_logical_reference(
            project, "Content/" + virtual.removeprefix("/Game/") + ".uasset"
        )
        if not verify and (physical.exists() or library.does_asset_exist(virtual)):
            raise WorkerInputError("Texture destination already exists.")
        targets.append((entry, virtual, physical))
    for folder in plan["folders"]:
        virtual = plan["packRoot"] + "/" + folder
        resolve_logical_reference(project, "Content/" + virtual.removeprefix("/Game/"))
    for folder in plan["folders"]:
        virtual = plan["packRoot"] + "/" + folder
        if verify:
            if not library.does_directory_exist(virtual):
                raise RuntimeError("Expected product folder is missing.")
        elif not library.make_directory(virtual):
            raise RuntimeError("Product folder creation failed.")
    artifacts = []
    for entry, virtual, physical in targets:
        if not verify:
            task = unreal.AssetImportTask()
            factory = unreal.TextureFactory()
            factory.set_editor_property("create_material", False)
            task.set_editor_property("factory", factory)
            for key, value in {
                "filename": str(resolve_logical_reference(source, entry["sourceReference"])),
                "destination_path": plan["packRoot"] + "/Textures",
                "destination_name": entry["assetName"],
                "automated": True,
                "replace_existing": False,
                "save": False,
            }.items():
                task.set_editor_property(key, value)
            unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
            objects = list(task.get_objects())
            if len(objects) != 1 or not isinstance(objects[0], unreal.Texture2D):
                raise RuntimeError("Import did not create exactly one texture.")
            texture = objects[0]
            if texture.get_path_name() != virtual + "." + entry["assetName"]:
                raise RuntimeError("Importer changed the planned asset reference.")
            for key, value in _properties(unreal, entry).items():
                texture.set_editor_property(key, value)
            if not library.save_loaded_asset(texture, only_if_is_dirty=False):
                raise RuntimeError("Texture save failed.")
        else:
            texture = library.load_asset(virtual)
        if not isinstance(texture, unreal.Texture2D):
            raise RuntimeError("Expected texture is unavailable.")
        for key, value in _properties(unreal, entry).items():
            if texture.get_editor_property(key) != value:
                raise TexturePolicyError("UNREAL_TEXTURE_" + key.upper() + "_MISMATCH")
        physical = resolve_logical_reference(project, physical.relative_to(project).as_posix())
        digest, size = file_identity(physical)
        if size == 0:
            raise RuntimeError("Saved texture is empty.")
        artifacts.append(
            {
                "artifactId": entry["assetName"],
                "role": "engine-asset",
                "logicalReference": physical.relative_to(workspace).as_posix(),
                "target": "unreal",
                "sha256": digest,
                "byteCount": size,
            }
        )
    return artifacts
