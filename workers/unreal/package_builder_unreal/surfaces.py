"""Own the material/mesh request lifecycle inside an already isolated, texture-imported clone."""

from pathlib import Path

from package_builder_protocol import WorkerInputError, resolve_logical_reference

from package_builder_unreal.import_plan import file_identity, load_plan
from package_builder_unreal.materials import compile_material, verify_material
from package_builder_unreal.static_meshes import import_mesh, verify_mesh
from package_builder_unreal.surface_plan import load_surface_plan
from package_builder_unreal.textures import import_textures


def import_surfaces(u, workspace, source, verify):
    """Preflight all destinations and inputs; failures invalidate the clone and never promote output."""
    project = resolve_logical_reference(workspace, "project")
    if Path(u.Paths.project_dir()).resolve(strict=True) != project:
        raise WorkerInputError("Unexpected running project.")
    content = load_plan(resolve_logical_reference(source, "unreal-import-plan.json"), source)
    plan = load_surface_plan(source, content)
    root = content["packRoot"]
    targets = [
        ("Materials/" + prefix + m["id"]) for m in plan["materials"] for prefix in ("M_", "MI_")
    ]
    targets += ["Meshes/SM_" + m["id"] for m in plan["meshes"]]
    for target in targets:
        path = resolve_logical_reference(
            project, "Content/" + plan["projectName"] + "/" + target + ".uasset"
        )
        if not verify and (
            path.exists() or u.EditorAssetLibrary.does_asset_exist(root + "/" + target)
        ):
            raise WorkerInputError("Surface destination already exists.")
    # Reuse persisted texture verification; never repair input textures during surface work.
    artifacts = import_textures(u, workspace, source, True)
    for material in plan["materials"]:
        if not verify:
            compile_material(u, root, material)
        verify_material(u, root, material)
    for mesh in plan["meshes"]:
        if not verify:
            import_mesh(u, root, source, mesh)
        verify_mesh(u, root, mesh)
    for target in targets:
        path = resolve_logical_reference(
            project, "Content/" + plan["projectName"] + "/" + target + ".uasset"
        )
        digest, size = file_identity(path)
        if size == 0:
            raise RuntimeError("Empty surface output.")
        artifacts.append(
            {
                "artifactId": target.split("/")[-1],
                "role": "engine-asset",
                "logicalReference": path.relative_to(workspace).as_posix(),
                "target": "unreal",
                "sha256": digest,
                "byteCount": size,
            }
        )
    return artifacts
