"""PB-0407 armature, skin, bind-data, and weight inspection tests."""

from __future__ import annotations

import math
import sys
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKER_ROOT = REPOSITORY_ROOT / "workers" / "blender"
sys.path.insert(0, str(WORKER_ROOT))

from package_builder_blender.rig_inspection import inspect_rigs  # noqa: E402, I001


_IDENTITY = (
    (1.0, 0.0, 0.0, 0.0),
    (0.0, 1.0, 0.0, 0.0),
    (0.0, 0.0, 1.0, 0.0),
    (0.0, 0.0, 0.0, 1.0),
)


class _Bone:
    def __init__(
        self,
        name: str,
        parent: _Bone | None = None,
        *,
        use_deform: bool = True,
        matrix: tuple[tuple[float, ...], ...] = _IDENTITY,
    ) -> None:
        self.name = name
        self.parent = parent
        self.use_deform = use_deform
        self.head_local = (0.0, 0.0, 0.0)
        self.tail_local = (0.0, 1.0, 0.0)
        self.matrix_local = matrix


class _ArmatureData:
    def __init__(self, *bones: _Bone) -> None:
        self.bones = bones


class _ArmatureObject:
    type = "ARMATURE"

    def __init__(self, name: str, *bones: _Bone) -> None:
        self.name = name
        self.data = _ArmatureData(*bones)


class _Modifier:
    type = "ARMATURE"

    def __init__(self, armature: _ArmatureObject | None) -> None:
        self.object = armature


class _Group:
    def __init__(self, index: int, name: str) -> None:
        self.index = index
        self.name = name


class _Membership:
    def __init__(self, group: int, weight: float) -> None:
        self.group = group
        self.weight = weight


class _Vertex:
    def __init__(self, index: int, *groups: _Membership) -> None:
        self.index = index
        self.groups = groups


class _MeshData:
    def __init__(self, *vertices: _Vertex) -> None:
        self.vertices = vertices


class _MeshObject:
    type = "MESH"

    def __init__(
        self,
        name: str,
        *,
        armature: _ArmatureObject | None = None,
        groups: tuple[_Group, ...] = (),
        vertices: tuple[_Vertex, ...] = (),
        modifiers: tuple[_Modifier, ...] | None = None,
        bind_matrix: tuple[tuple[float, ...], ...] = _IDENTITY,
    ) -> None:
        self.name = name
        self.modifiers = (
            ((_Modifier(armature),) if armature is not None else ())
            if modifiers is None
            else modifiers
        )
        self.vertex_groups = groups
        self.data = _MeshData(*vertices)
        self.matrix_parent_inverse = bind_matrix


class _Unreadable:
    def __iter__(self):
        raise RuntimeError("private rig detail")


class RigInspectionTests(unittest.TestCase):
    def test_reports_skeleton_hierarchy_skin_bind_data_and_weights(self) -> None:
        root = _Bone("Root", use_deform=False)
        upper = _Bone("Upper", root)
        lower = _Bone("Lower", upper)
        armature = _ArmatureObject("Rig", root, upper, lower)
        mesh = _MeshObject(
            "Body",
            armature=armature,
            groups=(_Group(0, "Upper"), _Group(1, "Lower")),
            vertices=(
                _Vertex(0, _Membership(0, 1.0)),
                _Vertex(1, _Membership(0, 0.25), _Membership(1, 0.75)),
            ),
        )

        result = inspect_rigs((mesh, armature))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(
            (1, 3, 2, 1, 0),
            (
                result.report.skeleton_count,
                result.report.bone_count,
                result.report.deform_bone_count,
                result.report.skinned_mesh_count,
                result.report.unweighted_vertex_count,
            ),
        )
        skeleton = result.report.armatures[0]
        self.assertEqual(("Root",), skeleton.root_bone_names)
        self.assertEqual(
            "Upper", next(item for item in skeleton.bones if item.name == "Lower").parent_name
        )
        skin = result.report.skinned_meshes[0]
        self.assertEqual("Rig", skin.armature_object_name)
        self.assertEqual(2, skin.maximum_deform_influences)
        self.assertEqual(16, len(skin.parent_inverse_matrix))

    def test_reports_missing_unmatched_groups_and_unweighted_vertices(self) -> None:
        root = _Bone("Root")
        child = _Bone("Child", root)
        armature = _ArmatureObject("Rig", root, child)
        mesh = _MeshObject(
            "Body",
            armature=armature,
            groups=(_Group(0, "Root"), _Group(1, "Helper")),
            vertices=(_Vertex(0, _Membership(1, 1.0)), _Vertex(1)),
        )

        result = inspect_rigs((armature, mesh))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        skin = result.report.skinned_meshes[0]
        self.assertEqual(("Child",), skin.missing_deform_group_names)
        self.assertEqual(("Helper",), skin.unmatched_vertex_group_names)
        self.assertEqual((0, 1), skin.unweighted_vertex_indices)

    def test_static_scene_without_armatures_is_valid(self) -> None:
        result = inspect_rigs((_MeshObject("Static"),))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual((0, 0), (result.report.skeleton_count, result.report.skinned_mesh_count))

    def test_multiple_root_bones_are_reported(self) -> None:
        armature = _ArmatureObject("Rig", _Bone("B"), _Bone("A"))

        result = inspect_rigs((armature,))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(("A", "B"), result.report.armatures[0].root_bone_names)

    def test_cycles_and_orphaned_parents_fail_closed(self) -> None:
        first = _Bone("First")
        second = _Bone("Second", first)
        first.parent = second
        cycle = inspect_rigs((_ArmatureObject("Cycle", first, second),))
        orphan = inspect_rigs((_ArmatureObject("Orphan", _Bone("Child", _Bone("Missing"))),))

        self.assertEqual("BLENDER_RIG_DATA_INVALID", cycle.findings[0].code)
        self.assertEqual("BLENDER_RIG_DATA_INVALID", orphan.findings[0].code)

    def test_duplicate_and_empty_armatures_fail_closed(self) -> None:
        duplicate = inspect_rigs((_ArmatureObject("Rig", _Bone("Same"), _Bone("Same")),))
        empty = inspect_rigs((_ArmatureObject("Empty"),))

        self.assertEqual("BLENDER_RIG_DATA_INVALID", duplicate.findings[0].code)
        self.assertEqual("BLENDER_RIG_DATA_INVALID", empty.findings[0].code)

    def test_missing_or_multiple_armature_modifiers_fail_closed(self) -> None:
        root = _Bone("Root")
        armature = _ArmatureObject("Rig", root)
        missing = inspect_rigs((_MeshObject("Missing", modifiers=(_Modifier(None),)), armature))
        multiple = inspect_rigs(
            (
                _MeshObject(
                    "Multiple",
                    modifiers=(_Modifier(armature), _Modifier(armature)),
                ),
                armature,
            )
        )

        self.assertEqual("BLENDER_RIG_DATA_INVALID", missing.findings[0].code)
        self.assertEqual("BLENDER_RIG_DATA_INVALID", multiple.findings[0].code)

    def test_invalid_weight_and_group_reference_fail_closed(self) -> None:
        root = _Bone("Root")
        armature = _ArmatureObject("Rig", root)
        invalid_weight = inspect_rigs(
            (
                armature,
                _MeshObject(
                    "Weight",
                    armature=armature,
                    groups=(_Group(0, "Root"),),
                    vertices=(_Vertex(0, _Membership(0, 1.5)),),
                ),
            )
        )
        missing_group = inspect_rigs(
            (
                armature,
                _MeshObject(
                    "Group",
                    armature=armature,
                    groups=(_Group(0, "Root"),),
                    vertices=(_Vertex(0, _Membership(8, 1.0)),),
                ),
            )
        )

        self.assertEqual("BLENDER_RIG_DATA_INVALID", invalid_weight.findings[0].code)
        self.assertEqual("BLENDER_RIG_DATA_INVALID", missing_group.findings[0].code)

    def test_empty_skinned_mesh_fails_closed(self) -> None:
        armature = _ArmatureObject("Rig", _Bone("Root"))

        result = inspect_rigs((_MeshObject("Body", armature=armature), armature))

        self.assertEqual("BLENDER_RIG_DATA_INVALID", result.findings[0].code)

    def test_unknown_armature_duplicate_group_and_vertex_index_fail_closed(self) -> None:
        root = _Bone("Root")
        registered = _ArmatureObject("Registered", root)
        unknown = _ArmatureObject("Unknown", root)
        missing_armature = inspect_rigs(
            (
                registered,
                _MeshObject(
                    "Body",
                    modifiers=(_Modifier(unknown),),
                    vertices=(_Vertex(0),),
                ),
            )
        )
        duplicate_group = inspect_rigs(
            (
                registered,
                _MeshObject(
                    "Groups",
                    armature=registered,
                    groups=(_Group(0, "Root"), _Group(1, "Root")),
                    vertices=(_Vertex(0),),
                ),
            )
        )
        vertex_index = inspect_rigs(
            (
                registered,
                _MeshObject(
                    "Vertices",
                    armature=registered,
                    groups=(_Group(0, "Root"),),
                    vertices=(_Vertex(4, _Membership(0, 1.0)),),
                ),
            )
        )

        for result in (missing_armature, duplicate_group, vertex_index):
            self.assertEqual("BLENDER_RIG_DATA_INVALID", result.findings[0].code)

    def test_duplicate_identity_and_nonfinite_bind_data_fail_closed(self) -> None:
        duplicate = inspect_rigs((_MeshObject("Same"), _MeshObject("Same")))
        armature = _ArmatureObject("Rig", _Bone("Root"))
        invalid_matrix = tuple(
            tuple(math.inf if row == column == 0 else value for column, value in enumerate(values))
            for row, values in enumerate(_IDENTITY)
        )
        bind = inspect_rigs(
            (
                armature,
                _MeshObject("Body", armature=armature, bind_matrix=invalid_matrix),
            )
        )

        self.assertEqual("BLENDER_RIG_DATA_INVALID", duplicate.findings[0].code)
        self.assertEqual("BLENDER_RIG_DATA_INVALID", bind.findings[0].code)

    def test_unreadable_input_is_sanitized(self) -> None:
        result = inspect_rigs(_Unreadable())

        self.assertEqual("BLENDER_RIG_INPUT_INVALID", result.findings[0].code)
        self.assertNotIn("private", result.findings[0].explanation)


if __name__ == "__main__":
    unittest.main()
