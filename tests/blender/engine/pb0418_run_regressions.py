"""Exercise PB-0418 findings from real Blender imports and data blocks."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any


def _arguments() -> tuple[Path, Path, Path, Path]:
    """Read repository, catalog, materialized fixture, and report paths."""

    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 4:
        raise ValueError("Expected repository, catalog, fixture root, and report paths.")
    return tuple(Path(value).resolve() for value in values)  # type: ignore[return-value]


REPOSITORY_ROOT, CATALOG_FILE, FIXTURE_ROOT, REPORT_FILE = _arguments()
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

import bpy  # noqa: E402

from package_builder_blender.regression_validation import (  # noqa: E402
    RegressionObservation,
    validate_regression_observation,
)


def _reset() -> None:
    """Reset each regression case so prior errors cannot contaminate its facts."""

    bpy.ops.wm.read_factory_settings(use_empty=True)
    if tuple(bpy.data.objects):
        raise RuntimeError("Regression reset did not produce an empty scene.")


def _mesh(name: str, *, with_uv: bool) -> Any:
    """Create one minimal mesh for bounded regression observations."""

    mesh = bpy.data.meshes.new(f"MS_{name}")
    mesh.from_pydata(((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 0.0, 1.0)), (), ((0, 1, 2),))
    mesh.update()
    if with_uv:
        mesh.uv_layers.new(name="UVMap")
    scene_object = bpy.data.objects.new(f"P_{name}", mesh)
    bpy.context.scene.collection.objects.link(scene_object)
    return scene_object


def _corrupt(case: dict[str, Any]) -> RegressionObservation:
    """Invoke Blender's real importer on one intentionally corrupt materialized payload."""

    _reset()
    source = FIXTURE_ROOT / f"{case['id']}.{case['sourceFormat']}"
    try:
        if case["sourceFormat"] == "fbx":
            result = set(bpy.ops.import_scene.fbx(filepath=str(source)))
        else:
            result = set(bpy.ops.import_scene.gltf(filepath=str(source)))
        succeeded = result == {"FINISHED"} and any(item.type == "MESH" for item in bpy.data.objects)
    except Exception:
        succeeded = False
    return RegressionObservation(case["id"], case["sourceFormat"], succeeded, 0)


def _synthetic(case: dict[str, Any]) -> RegressionObservation:
    """Derive each non-corrupt failure fact from actual Blender data blocks."""

    _reset()
    _mesh(case["id"], with_uv=case["id"] != "no-uvs")
    missing_images = 0
    skeleton_count = 0
    invalid_animations = 0
    unsupported: tuple[str, ...] = ()

    if case["id"] == "missing-images":
        for index in range(2):
            image = bpy.data.images.new(f"Missing-{index}", width=1, height=1)
            image.source = "FILE"
            image.filepath = str(FIXTURE_ROOT / f"not-present-{index}.png")
        missing_images = sum(
            image.source == "FILE" and not Path(bpy.path.abspath(image.filepath)).is_file()
            for image in bpy.data.images
        )
    elif case["id"] == "multiple-rigs":
        for index in range(2):
            data = bpy.data.armatures.new(f"Rig-{index}")
            bpy.context.scene.collection.objects.link(bpy.data.objects.new(f"Rig-{index}", data))
        skeleton_count = sum(item.type == "ARMATURE" for item in bpy.data.objects)
    elif case["id"] == "unsupported-data":
        volume = bpy.data.volumes.new("UnsupportedVolume")
        bpy.context.scene.collection.objects.link(bpy.data.objects.new("UnsupportedVolume", volume))
        unsupported = tuple(
            sorted(
                {item.type for item in bpy.data.objects if item.type not in {"MESH", "ARMATURE"}}
            )
        )
    elif case["id"] == "invalid-animation":
        data = bpy.data.armatures.new("InvalidRig")
        bpy.context.scene.collection.objects.link(bpy.data.objects.new("InvalidRig", data))
        bpy.data.actions.new("InvalidEmptyAction")
        skeleton_count = sum(item.type == "ARMATURE" for item in bpy.data.objects)
        invalid_animations = sum(not tuple(action.slots) for action in bpy.data.actions)

    meshes = tuple(item for item in bpy.data.objects if item.type == "MESH")
    return RegressionObservation(
        case["id"],
        case["sourceFormat"],
        True,
        len(meshes),
        missing_images,
        skeleton_count,
        sum(not tuple(item.data.uv_layers) for item in meshes),
        unsupported,
        invalid_animations,
    )


def main() -> None:
    """Require every versioned regression fixture to emit its exact stable findings."""

    cases = json.loads(CATALOG_FILE.read_text(encoding="utf-8"))["fixtures"]
    records = []
    for case in cases:
        observation = _corrupt(case) if case["id"].startswith("corrupt-") else _synthetic(case)
        result = validate_regression_observation(observation)
        codes = [finding.code for finding in result.findings]
        if codes != case["expectedCodes"]:
            raise RuntimeError(f"{case['id']} emitted {codes}, expected {case['expectedCodes']}")
        records.append({"fixtureId": case["id"], "findingCodes": codes})
    REPORT_FILE.write_text(
        json.dumps({"succeeded": True, "fixtures": records}, indent=2), encoding="utf-8"
    )
    print(f"PB-0418 real Blender regressions passed: {len(records)}/{len(cases)}")


if __name__ == "__main__":
    main()
