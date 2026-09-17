"""Hostile-plan coverage for interactive native generation; runtime evidence requires real Unreal."""

import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]
from package_builder_protocol import WorkerInputError  # noqa: E402

from package_builder_unreal.preview_plan import load_preview_plan  # noqa: E402
from package_builder_unreal.project_validation import preview_diagnostics  # noqa: E402


class PreviewPlanTests(unittest.TestCase):
    def setUp(self):
        root = REPO / "artifacts/test-results/unreal-preview"
        root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=root)
        self.addCleanup(self.temp.cleanup)
        self.source = Path(self.temp.name)
        self.experience = json.loads(
            (REPO / "tests/fixtures/preview/preview-experience-v1.json").read_text("utf-8")
        )
        self.plan = {
            "schemaVersion": 1,
            "profile": "unreal-interactive-preview-v1",
            "projectName": "Product",
            "experience": self.experience,
        }
        presentation = self.experience["presentation"]
        bg = copy.deepcopy(presentation["background"])
        for source, target in (("outerColour", "outer"), ("centreColour", "centre")):
            colour = bg.pop(source)
            bg[target] = [colour[c] for c in ("red", "green", "blue")]
        lights = []
        for name in ("keyLight", "fillLight"):
            light = copy.deepcopy(presentation["lighting"][name])
            light["colour"] = [light["colour"][c] for c in ("red", "green", "blue")]
            lights.append(light)
        self.overview = {"projectName": "Product", "background": bg, "lights": lights}

    def load(self):
        (self.source / "unreal-preview-plan.json").write_text(
            json.dumps(self.plan), encoding="utf-8"
        )
        return load_preview_plan(self.source, self.overview)

    def test_explicit_intent_and_canonical_values(self):
        self.assertIsNone(load_preview_plan(self.source, self.overview))
        self.assertEqual(self.experience, self.load())

    def test_rejects_foreign_identity_unknown_fields_and_unsupported_version(self):
        for key, value in (
            ("projectName", "Other"),
            ("profile", "other"),
            ("schemaVersion", 2),
            ("schemaVersion", True),
            ("extra", True),
        ):
            original = copy.deepcopy(self.plan)
            self.plan[key] = value
            with self.subTest(key=key), self.assertRaises(WorkerInputError):
                self.load()
            self.plan = original

    def test_invalid_navigation_never_reaches_engine(self):
        for field, number in (
            ("pointerZoomStep", 1),
            ("keyboardZoomStep", 0),
            ("minimumDistanceMultiplier", -1),
            ("minimumPitchDegrees", 81),
            ("maximumPitchDegrees", float("nan")),
            ("pointerOrbitDegreesPerUnit", True),
        ):
            original = copy.deepcopy(self.plan)
            self.plan["experience"]["navigation"][field] = number
            with self.subTest(field=field), self.assertRaises((WorkerInputError, ValueError)):
                self.load()
            self.plan = original

    def test_rejects_missing_controls_recovery_or_unimplemented_bindings(self):
        for change in (
            lambda p: p["accessibility"].update(controls=[]),
            lambda p: p["accessibility"].update(visibleFocusRequired=False),
            lambda p: p["overlay"].update(restoreControlId="missing"),
            lambda p: p["bindings"][1].update(input="unsupported-key"),
            lambda p: p["accessibility"]["controls"][0].update(focusOrder=True),
            lambda p: p["accessibility"]["controls"][0].update(focusOrder=50),
        ):
            original = copy.deepcopy(self.plan)
            change(self.plan["experience"])
            with self.assertRaises(WorkerInputError):
                self.load()
            self.plan = original

    def test_rejects_presentation_drift(self):
        self.overview["lights"][0]["intensity"] += 1
        with self.assertRaises(WorkerInputError):
            self.load()

    def test_engine_notice_requires_exact_independent_baseline(self):
        notice = "LogConsoleManager: Warning: Console variable 'r.MotionVectorSimulation' used in the render thread. Rendering artifacts could happen. Use ECVF_RenderThreadSafe or don't use in render thread."
        self.assertFalse(preview_diagnostics(notice, notice)["findings"])
        for text, baseline in (
            (notice, ""),
            (notice + "\nLogBlueprint: Warning: Broken asset", notice),
            (notice * 2, notice),
            ("LogScript: Error: Runtime failure", notice),
        ):
            with self.subTest(text=text):
                self.assertTrue(preview_diagnostics(text, baseline)["findings"])
