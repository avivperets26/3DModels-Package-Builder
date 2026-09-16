"""Hostile overview plans, analytic framing, graph reachability and native diagnostic gates."""

import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]
from package_builder_protocol import WorkerInputError  # noqa: E402

from package_builder_unreal.capture_geometry import camera_frame  # noqa: E402
from package_builder_unreal.overview_plan import KINDS, load_overview_plan  # noqa: E402
from package_builder_unreal.project_validation import (  # noqa: E402
    log_findings,
    repair_diagnostics,
    unused_packages,
)


class OverviewTests(unittest.TestCase):
    def setUp(self):
        root = REPO / "artifacts/validation/PB-1110"
        root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=root)
        self.addCleanup(self.temp.cleanup)
        self.source = Path(self.temp.name)
        self.plan = {
            "schemaVersion": 1,
            "profile": "unreal-overview-v1",
            "projectName": "Example",
            "meshId": "Box",
            "label": "Example",
            "width": 1920,
            "height": 1080,
            "fieldOfView": 35,
            "padding": 1.25,
            "views": [{"id": "Hero", "kind": "hero"}],
            "background": {
                "outer": [0.012, 0.014, 0.018],
                "centre": [0.14, 0.16, 0.2],
                "centreX": 0.5,
                "centreY": 0.58,
                "radius": 0.7,
                "horizontalScale": 0.82,
            },
            "lights": [
                {
                    "yawDegrees": -32,
                    "pitchDegrees": 42,
                    "intensity": 1.15,
                    "colour": [1, 0.94, 0.84],
                },
                {
                    "yawDegrees": 145,
                    "pitchDegrees": 25,
                    "intensity": 0.55,
                    "colour": [0.62, 0.75, 1],
                },
            ],
        }

    def load(self, plan):
        (self.source / "unreal-overview-plan.json").write_text(json.dumps(plan), encoding="utf-8")
        return load_overview_plan(
            self.source, {"projectName": "Example", "meshes": [{"id": "Box"}]}
        )

    def test_exact_views_order_and_label(self):
        self.plan["views"] += [{"id": "Front", "kind": "orthographic-front"}]
        self.assertEqual(self.plan, self.load(self.plan))

    def test_hostile_closed_fields_sizes_and_identifiers(self):
        for key, value in (
            ("schemaVersion", True),
            ("projectName", "../Other"),
            ("meshId", "Missing"),
            ("width", 3840),
            ("padding", float("nan")),
            ("fieldOfView", 0),
            ("label", "bad\nlabel"),
            ("label", "a" * 121),
            ("output", "elsewhere"),
            ("views", []),
            ("views", [{"id": "../Hero", "kind": "hero"}]),
            ("views", [{"id": "Hero", "kind": "animation-pose"}]),
        ):
            with self.subTest(key=key, value=value):
                changed = copy.deepcopy(self.plan)
                changed[key] = value
                with self.assertRaises((WorkerInputError, ValueError)):
                    self.load(changed)

    def test_duplicate_views_and_invalid_lighting(self):
        for mutation in (
            lambda p: p["views"].append({"id": "hero", "kind": "hero"}),
            lambda p: p["lights"][0].update(intensity=float("inf")),
            lambda p: p["background"].update(radius=0),
            lambda p: p["background"].update(outer=[True, 0, 0]),
        ):
            plan = copy.deepcopy(self.plan)
            mutation(plan)
            with self.assertRaises((WorkerInputError, ValueError)):
                self.load(plan)

    def test_all_views_fit_asymmetric_bounds_without_changing_geometry(self):
        minimum, maximum = (-70, -50, -20), (230, 100, 110)
        for kind in KINDS:
            frame = camera_frame(minimum, maximum, kind, 35, 1.25)
            self.assertTrue(all(0 < x < 1 for x in frame["bounds"]))
            self.assertFalse(frame["depthClipped"])
            self.assertGreater(frame["orthoWidth"], 0)
        self.assertEqual((-70, -50, -20), minimum)

    def test_graph_cycles_do_not_hide_unused_packages(self):
        dependencies = {
            "map": ["mesh"],
            "mesh": ["material"],
            "material": ["texture"],
            "texture": ["material"],
            "old": [],
        }
        self.assertEqual({"old"}, unused_packages(set(dependencies), dependencies, "map"))
        self.assertEqual(set(), unused_packages(set(dependencies) - {"old"}, dependencies, "map"))

    def test_finite_planar_bounds_are_supported_but_invalid_bounds_fail(self):
        self.assertFalse(camera_frame((0, 0, 0), (0, 100, 100), "hero", 35, 1.25)["depthClipped"])
        for maximum in ((0, 0, 0), (-1, 10, 10), (float("nan"), 10, 10)):
            with self.assertRaises(ValueError):
                camera_frame((0, 0, 0), maximum, "hero", 35, 1.25)

    def test_logs_fail_closed_without_echoing_sensitive_lines(self):
        for line in (
            "LogMaterial: Warning: missing",
            "LogLinker: Error: missing",
            "Fatal error: crash",
        ):
            self.assertEqual(["UNREAL_LOG_DIAGNOSTIC"], log_findings(line))
        self.assertEqual([], log_findings("LogInit: Display: 0 errors, 0 warnings"))

    def test_local_redirector_notice_requires_owned_path_and_native_deletion_event(self):
        def notice(name):
            return (
                "LogContentCommandlet: Display: Deleting unreferenced redirector [" + name + "]\n"  # noqa: S608 -- Native log fixture, not SQL.
                "LogContentCommandlet: Warning: '" + name + "' is in an unknown revision control "
                "state, attempting to delete from disk...\n"
                "LogInit: Display: Warning/Error Summary (Unique only)"
            )

        owned = str(self.source / "Old.uasset")
        report = repair_diagnostics(notice(owned), REPO, self.source)
        self.assertEqual([], report["findings"])
        self.assertEqual(1, report["localRedirectorDeletionNoticeLines"])
        for text in (
            notice(str(REPO / "outside.uasset")),
            notice(owned).replace("Deleting unreferenced redirector", "Not deleting"),
            notice(owned) + "\nLogContentCommandlet: Warning: failed to delete from disk.",  # noqa: S608 -- Native log fixture, not SQL.
            notice(owned) + "\nLogMaterial: Warning: shader error",
            notice(owned) + "\nLogLinker: Error: missing asset",
        ):
            self.assertEqual(
                ["UNREAL_LOG_DIAGNOSTIC"], repair_diagnostics(text, REPO, self.source)["findings"]
            )


if __name__ == "__main__":
    unittest.main()
