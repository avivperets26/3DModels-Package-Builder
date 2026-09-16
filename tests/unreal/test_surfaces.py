"""PB-1108/1109 hostile boundary and graph failure tests; native acceptance is a separate harness."""

import copy
import json
import struct
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import Mock

REPO = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]
from package_builder_protocol import WorkerInputError  # noqa: E402

from package_builder_unreal.import_plan import file_identity  # noqa: E402
from package_builder_unreal.materials import (  # noqa: E402
    MaterialGraph,
    equivalent_float,
    material_properties,
)
from package_builder_unreal.static_meshes import collision_trace, import_settings  # noqa: E402
from package_builder_unreal.surface_plan import load_surface_plan  # noqa: E402


class SurfaceBoundaryTests(unittest.TestCase):
    def setUp(self):
        root = REPO / "artifacts/validation/PB-1107"
        root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=root)
        self.addCleanup(self.temp.cleanup)
        self.source = Path(self.temp.name)
        (self.source / "mesh.fbx").write_bytes(b"synthetic parser fixture, not native FBX")
        digest, size = file_identity(self.source / "mesh.fbx")
        self.content = {
            "projectName": "Example",
            "textures": [
                {"assetName": "T_ORM", "srgb": False, "compression": "TC_MASKS", "noAlpha": True}
            ],
        }
        self.plan = {
            "schemaVersion": 1,
            "profile": "unreal-surfaces-v1",
            "projectName": "Example",
            "materials": [
                {
                    "id": "Surface",
                    "surface": "opaque",
                    "twoSided": False,
                    "alphaCutoff": 0.333,
                    "metallic": 0.8,
                    "roughness": 0.7,
                    "normalScale": 1,
                    "occlusionStrength": 1,
                    "opacity": 1,
                    "emission": [0, 0, 0, 0],
                    "uv": [1, 1, 0, 0],
                    "textures": {"orm": "T_ORM"},
                }
            ],
            "meshes": [
                {
                    "id": "Object",
                    "sourceReference": "mesh.fbx",
                    "sha256": digest,
                    "byteCount": size,
                    "materials": ["Surface"],
                    "collision": "none",
                    "expectedSizeCm": [100, 200, 300],
                    "sourceSlots": ["FixtureSurface"],
                }
            ],
        }

    def load(self, plan=None):
        (self.source / "unreal-surface-plan.json").write_text(
            json.dumps(self.plan if plan is None else plan), encoding="utf-8"
        )
        return load_surface_plan(self.source, self.content)

    def test_valid_modes_and_collision_choices(self):
        for surface in ("opaque", "cutout", "transparent"):
            for collision in ("none", "box", "complex-as-simple"):
                with self.subTest(surface=surface, collision=collision):
                    self.plan["materials"][0]["surface"] = surface
                    self.plan["meshes"][0]["collision"] = collision
                    self.assertEqual(self.plan, self.load())

    def test_unknown_and_duplicate_identities(self):
        for changes in (
            {"schemaVersion": True},
            {"schemaVersion": 2},
            {"profile": "future"},
            {"projectName": "../elsewhere"},
            {"script": "evil.py"},
        ):
            plan = copy.deepcopy(self.plan)
            plan.update(changes)
            with self.subTest(changes=changes), self.assertRaises(WorkerInputError):
                self.load(plan)
        for field in ("materials", "meshes"):
            plan = copy.deepcopy(self.plan)
            plan[field].append(copy.deepcopy(plan[field][0]))
            plan[field][-1]["id"] = plan[field][0]["id"].lower()
            with self.subTest(field=field), self.assertRaises(WorkerInputError):
                self.load(plan)

    def test_material_parameter_and_reference_failures(self):
        bad = [
            ("surface", "auto"),
            ("twoSided", 1),
            ("metallic", -0.01),
            ("roughness", 1.01),
            ("normalScale", float("inf")),
            ("occlusionStrength", True),
            ("opacity", 0.5),
            ("emission", [1, 2, 3]),
            ("uv", [1, 1, float("nan"), 0]),
            ("textures", {"orm": "T_Missing"}),
            ("textures", {"height": "T_ORM"}),
        ]
        for key, value in bad:
            plan = copy.deepcopy(self.plan)
            plan["materials"][0][key] = value
            with self.subTest(key=key), self.assertRaises((WorkerInputError, ValueError)):
                self.load(plan)
        self.content["textures"][0]["srgb"] = True
        with self.assertRaises(WorkerInputError):
            self.load()

    def test_mesh_snapshot_bounds_and_reference_failures(self):
        for key, value in [
            ("id", "../bad"),
            ("sourceReference", "../mesh.fbx"),
            ("sha256", "0" * 64),
            ("byteCount", 0),
            ("byteCount", 268435457),
            ("materials", []),
            ("materials", ["missing"]),
            ("collision", "auto"),
            ("expectedSizeCm", [100, -1, 300]),
            ("sourceSlots", []),
            ("sourceSlots", [" "]),
        ]:
            plan = copy.deepcopy(self.plan)
            plan["meshes"][0][key] = value
            with self.subTest(key=key), self.assertRaises(WorkerInputError):
                self.load(plan)
        (self.source / "mesh.fbx").write_bytes(b"mutated")
        with self.assertRaises(WorkerInputError):
            self.load()

    def test_limits_and_unknown_fields(self):
        for field in ("materials", "meshes"):
            plan = copy.deepcopy(self.plan)
            plan[field] *= 129
            with self.subTest(field=field), self.assertRaises(WorkerInputError):
                self.load(plan)
            plan = copy.deepcopy(self.plan)
            plan[field][0]["unreviewed"] = True
            with self.assertRaises(WorkerInputError):
                self.load(plan)


class EnginePolicyTests(unittest.TestCase):
    def test_native_float_rounding_accepts_hdr_values_but_rejects_drift(self):
        for expected in (0.4, 100000.01, -999999.99, 0):
            rounded = struct.unpack("<f", struct.pack("<f", expected))[0]
            self.assertTrue(equivalent_float(rounded, expected))
            self.assertFalse(equivalent_float(rounded + max(1, abs(expected) * 0.01), expected))
        self.assertFalse(equivalent_float(float("nan"), 1))

    def test_failed_graph_edges_and_missing_nodes_fail_closed(self):
        engine = types.SimpleNamespace(
            MaterialEditingLibrary=Mock(),
            MaterialExpressionMultiply=object,
            MaterialProperty=types.SimpleNamespace(MP_BASE_COLOR=1),
        )
        graph = MaterialGraph(engine, object())
        engine.MaterialEditingLibrary.create_material_expression.return_value = None
        with self.assertRaises(RuntimeError):
            graph.node("Multiply")
        engine.MaterialEditingLibrary.connect_material_expressions.return_value = False
        with self.assertRaises(RuntimeError):
            graph.edge(object(), object(), "A")
        engine.MaterialEditingLibrary.connect_material_property.return_value = False
        with self.assertRaises(RuntimeError):
            graph.output(object(), "BASE_COLOR")

    def test_modes_do_not_depend_on_texture_names(self):
        engine = types.SimpleNamespace(
            BlendMode=types.SimpleNamespace(BLEND_OPAQUE=1, BLEND_MASKED=2, BLEND_TRANSLUCENT=3)
        )
        for mode, expected in (("opaque", 1), ("cutout", 2), ("transparent", 3)):
            actual = material_properties(
                engine, {"surface": mode, "twoSided": True, "alphaCutoff": 0.4}
            )
            self.assertEqual(
                actual, {"blend_mode": expected, "two_sided": True, "opacity_mask_clip_value": 0.4}
            )

    def test_import_policy_preserves_normals_and_never_auto_generates_collision(self):
        engine = types.SimpleNamespace(
            FBXNormalImportMethod=types.SimpleNamespace(FBXNIM_IMPORT_NORMALS_AND_TANGENTS=3),
            FBXNormalGenerationMethod=types.SimpleNamespace(MIKK_T_SPACE=4),
            CollisionTraceFlag=types.SimpleNamespace(
                CTF_USE_COMPLEX_AS_SIMPLE=5,
                CTF_USE_SIMPLE_AND_COMPLEX=6,
                CTF_USE_SIMPLE_AS_COMPLEX=7,
            ),
        )
        settings = import_settings(engine)
        self.assertFalse(settings["auto_generate_collision"])
        self.assertFalse(settings["import_mesh_lo_ds"])
        self.assertTrue(settings["convert_scene_unit"])
        self.assertTrue(settings["convert_scene"])
        self.assertEqual(settings["normal_import_method"], 3)
        self.assertEqual(collision_trace(engine, "complex-as-simple"), 5)
        self.assertEqual(collision_trace(engine, "box"), 6)
        self.assertEqual(collision_trace(engine, "none"), 7)


if __name__ == "__main__":
    unittest.main()
