"""Acceptance-only fault injection into an owned disposable overview project."""

import os
from pathlib import Path

import unreal as u

repo = Path(__file__).resolve().parents[1]
job = Path(os.environ["PACKAGEBUILDER_UNREAL_REQUEST"]).parent
if not job.resolve().is_relative_to(repo / "artifacts/ue"):
    raise RuntimeError("Not an owned fixture job.")
root = "/Game/PBOverviewFixture"
mode = os.environ["PB_OVERVIEW_FAULT"]
library = u.EditorAssetLibrary
if mode == "unused":
    asset = u.AssetToolsHelpers.get_asset_tools().create_asset(
        "M_Unused", root + "/Materials", u.Material, u.MaterialFactoryNew()
    )
    if not asset or not library.save_loaded_asset(asset, only_if_is_dirty=False):
        raise RuntimeError("Fault creation failed.")
    # An unloaded map reference forces Unreal's normal rename path to leave a redirector.
    world = u.EditorLoadingAndSavingUtils.new_blank_map(False)
    editor = u.get_editor_subsystem(u.EditorActorSubsystem) or u.get_default_object(
        u.EditorActorSubsystem
    )
    actor = editor.spawn_actor_from_class(u.StaticMeshActor, u.Vector(0, 0, 0))
    actor.static_mesh_component.set_static_mesh(library.load_asset("/Engine/BasicShapes/Cube"))
    actor.static_mesh_component.set_material(0, asset)
    if not u.EditorLoadingAndSavingUtils.save_map(world, root + "/Maps/L_FaultReference"):
        raise RuntimeError("Fault reference save failed.")
elif mode == "redirector":
    u.AssetRegistryHelpers.get_asset_registry().scan_paths_synchronous([root], True)
    if not library.rename_asset(root + "/Materials/M_Unused", root + "/Materials/M_UnusedMoved"):
        raise RuntimeError("Fault rename failed.")
elif mode == "cleanup":
    for name in ("M_Unused", "M_UnusedMoved"):
        path = root + "/Materials/" + name
        if library.does_asset_exist(path) and not library.delete_asset(path):
            raise RuntimeError("Fault cleanup failed.")
    # Deleting a referenced material resaves loaded referencers. Delete its fixture map last.
    if not library.delete_asset(root + "/Maps/L_FaultReference"):
        raise RuntimeError("Fault map cleanup failed.")
elif mode in ("bad-light", "restore-light"):
    world = u.EditorLoadingAndSavingUtils.load_map(root + "/Maps/L_Overview")
    editor = u.get_editor_subsystem(u.EditorActorSubsystem) or u.get_default_object(
        u.EditorActorSubsystem
    )
    light = next(a for a in editor.get_all_level_actors() if a.get_actor_label() == "PB_Key")
    light.light_component.set_intensity(0 if mode == "bad-light" else 13.8)
    if not u.EditorLoadingAndSavingUtils.save_map(world, root + "/Maps/L_Overview"):
        raise RuntimeError("Fault map save failed.")
else:
    raise RuntimeError("Unknown fault.")
