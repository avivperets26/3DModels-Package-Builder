"""Install only a successfully compiled, source-matching helper into an owned development clone."""

import shutil

from package_builder_protocol import atomic_write_result, load_bounded_json

from package_builder_unreal.import_plan import file_identity
from package_builder_unreal.project import checked_tree


def install_helper(repository, clone):
    """Never pick a stale helper merely by modification time; compare every source/binary identity."""
    source = repository / "workers/unreal/editor/PackageBuilderPreviewEditor"
    identities = {
        p.relative_to(source).as_posix(): list(file_identity(p))
        for p in checked_tree(source)
        if p.is_file()
    }
    for receipt_path in sorted(
        (repository / "artifacts/PB-1116").glob("*/receipt.json"),
        key=lambda p: p.stat().st_mtime,
        reverse=True,
    ):
        receipt = load_bounded_json(receipt_path)
        if not receipt.get("passed") or receipt.get("sourceFiles") != identities:
            continue
        cached = repository / receipt["helper"]
        if any(
            list(file_identity(cached / name)) != identity
            for name, identity in receipt["binaries"].items()
        ):
            raise RuntimeError("Compiled helper binary identity changed.")
        destination = clone.project / "Plugins/PackageBuilderPreviewEditor"
        shutil.copytree(cached, destination, ignore=shutil.ignore_patterns("*.pdb"))
        path = clone.project / (clone.project_name + ".uproject")
        descriptor = load_bounded_json(path)
        descriptor["Plugins"].append({"Name": "PackageBuilderPreviewEditor", "Enabled": True})
        atomic_write_result(path, descriptor)
        return receipt_path
    raise RuntimeError(
        "Build the editor helper after its source changes before preview validation."
    )
