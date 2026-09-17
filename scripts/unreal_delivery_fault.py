"""Trusted negative fixture: restore a machine-local reimport filename in a disposable extraction."""

from pathlib import Path

import unreal as u

project = Path(u.Paths.project_dir()).resolve(strict=True)
repository = Path(__file__).resolve().parents[1]
if not project.is_relative_to(repository / "artifacts/ue") or project.name != "project":
    raise RuntimeError("Unexpected delivery fault target.")
asset = u.EditorAssetLibrary.load_asset("/Game/PBOverviewFixture/Textures/T_albedo")
asset.get_editor_property("asset_import_data").scripted_add_filename(
    str(project.parent / "input/albedo.png"), 0, "source-path-fault"
)
if not u.EditorAssetLibrary.save_loaded_asset(asset, only_if_is_dirty=False):
    raise RuntimeError("Fault asset was not saved.")
