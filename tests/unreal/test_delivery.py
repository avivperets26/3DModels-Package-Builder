"""Sanitized metadata and shared-template checks; native acceptance is a separate required gate."""

import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

REPO = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]
from package_builder_unreal.delivery import delivery_config, empty_import_paths  # noqa: E402


class DeliveryTests(unittest.TestCase):
    def test_empty_native_metadata_may_resolve_to_base_but_never_to_a_file(self):
        unreal = SimpleNamespace(
            Paths=SimpleNamespace(convert_relative_path_to_full=lambda _: "X:/Engine/Bin/")
        )
        for paths, expected in (
            ([], True),
            (["X:/Engine/Bin/"], True),
            (["X:/Engine/Bin/Source.fbx"], False),
            (["Y:/Private/source.png"], False),
        ):
            with self.subTest(paths=paths):
                self.assertEqual(
                    expected,
                    empty_import_paths(
                        unreal, SimpleNamespace(extract_filenames=lambda paths=paths: paths)
                    ),
                )

    def test_delivery_preserves_every_reviewed_template_setting(self):
        config = delivery_config({"projectName": "Product"})
        template = (REPO / "engine-templates/unreal/5.8/Config/DefaultEngine.ini").read_text(
            "utf-8"
        )
        self.assertEqual(template, config.replace("=/Game/Product/Maps/L_Overview\n", "=\n"))
        self.assertIn("BoneCompressionSettings=/Engine/", config)
