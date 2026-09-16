"""PB-1104 through PB-1106 isolated host and engine-boundary tests; not live engine evidence."""

import copy
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import threading
import types
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

REPO = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]

from package_builder_protocol import WorkerInputError  # noqa: E402

from package_builder_unreal.import_plan import load_plan  # noqa: E402
from package_builder_unreal.project import UnrealProjectClone  # noqa: E402
from package_builder_unreal.worker import run  # noqa: E402


class Workspace(unittest.TestCase):
    def setUp(self):
        root = REPO / "artifacts/validation/PB-1104"
        root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=root)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.template = self.root / "template"
        shutil.copytree(REPO / "engine-templates/unreal/5.8", self.template)
        self.job = self.root / "job"
        self.job.mkdir()

    def clone(self, job=None):
        return UnrealProjectClone(self.root, self.template, job or self.job, "PBTextureFixture")


class CloneTests(Workspace):
    def test_same_lease_rejects_concurrent_execution_and_early_disposal(self):
        started, finish = threading.Event(), threading.Event()
        process = Mock()
        process.poll.return_value = 0

        def wait(**_kwargs):
            started.set()
            finish.wait(timeout=10)
            return 0

        process.wait.side_effect = wait
        with (
            self.clone() as clone,
            patch("package_builder_unreal.project.subprocess.Popen", return_value=process),
        ):
            thread = threading.Thread(
                target=clone.execute, args=(Path(sys.executable), [], dict(os.environ), 20)
            )
            thread.start()
            try:
                self.assertTrue(started.wait(timeout=10))
                with self.assertRaises(RuntimeError):
                    clone.execute(Path(sys.executable), [], dict(os.environ), 20)
                with self.assertRaises(RuntimeError):
                    clone.__exit__()
                with self.assertRaises(RuntimeError):
                    clone.__enter__()
                self.assertTrue(clone.project.is_dir())
            finally:
                finish.set()
                thread.join(timeout=10)

    def test_cancelled_job_cleans_clone(self):
        clone = self.clone()
        with self.assertRaises(KeyboardInterrupt), clone:
            raise KeyboardInterrupt()
        self.assertTrue(clone.cleanup_succeeded)

    def test_exact_clone_renaming_and_independent_jobs_cleanup(self):
        other = self.root / "other"
        other.mkdir()
        first, second = self.clone(), self.clone(other)
        with first, second:
            self.assertNotEqual(first.project, second.project)
            self.assertTrue((first.project / "PBTextureFixture.uproject").is_file())
            self.assertFalse((first.project / "PackageBuilder.uproject").exists())
            self.assertTrue((first.project / "Content/PBTextureFixture/.gitkeep").is_file())
            self.assertFalse((first.project / "Content/Pack").exists())
            self.assertEqual(6, sum(p.is_file() for p in first.project.rglob("*")))
        self.assertTrue(first.cleanup_succeeded and second.cleanup_succeeded)
        self.assertTrue((self.template / "PackageBuilder.uproject").is_file())

    def test_separate_process_cannot_acquire_same_job_lease(self):
        script = """
import sys
from pathlib import Path
sys.path[:0] = sys.argv[1:3]
from package_builder_unreal.project import UnrealProjectClone
try:
    with UnrealProjectClone(*map(Path, sys.argv[3:6]), 'PBTextureFixture'):
        sys.exit(1)
except OSError:
    sys.exit(83)
"""
        with self.clone():
            result = subprocess.run(  # noqa: S603 - fixed child interpreter and test program.
                [
                    sys.executable,
                    "-B",
                    "-c",
                    script,
                    str(REPO / "workers/shared"),
                    str(REPO / "workers/unreal"),
                    str(self.root),
                    str(self.template),
                    str(self.job),
                ],
                capture_output=True,
                timeout=20,
            )
            self.assertEqual(83, result.returncode, result.stderr)
        # Persistent zero-byte lock is reusable after release; no stale lock prevents retry.
        with self.clone():
            pass

    def test_existing_clone_is_never_adopted_or_deleted(self):
        project = self.job / "project"
        project.mkdir()
        marker = project / "user.txt"
        marker.write_text("preserve")
        with self.assertRaises(FileExistsError), self.clone():
            pass
        self.assertEqual("preserve", marker.read_text())

    def test_unreviewed_template_files_fail_before_clone_creation(self):
        (self.template / "evil.py").write_text("raise Exception()")
        with self.assertRaises(WorkerInputError), self.clone():
            pass
        self.assertFalse((self.job / "project").exists())

    def test_failed_copy_cleans_partial_clone_and_releases_lease(self):
        with (
            patch("package_builder_unreal.project.shutil.copyfile", side_effect=OSError()),
            self.assertRaises(OSError),
            self.clone(),
        ):
            pass
        self.assertFalse((self.job / "project").exists())
        with self.clone():
            pass

    def test_timeout_stops_owned_process_then_cleans_project(self):
        clone = self.clone()
        with self.assertRaises(subprocess.TimeoutExpired), clone:
            clone.execute(
                Path(sys.executable),
                ["-B", "-c", "import time;time.sleep(60)"],
                dict(os.environ),
                1,
            )
        self.assertTrue(clone.cleanup_succeeded)

    def test_overlap_and_outside_roots_fail(self):
        for template, job in [
            (self.template, self.template),
            (self.template, self.root),
            (REPO, self.job),
        ]:
            with self.subTest(template=template, job=job), self.assertRaises(WorkerInputError):
                UnrealProjectClone(self.root, template, job, "PBTextureFixture")

    def test_engine_created_link_blocks_cleanup_without_touching_target(self):
        target = self.root / "protected"
        target.mkdir()
        (target / "keep.txt").write_text("keep")
        clone = self.clone().__enter__()
        link = clone.project / "escape"
        try:
            os.symlink(target, link, target_is_directory=True)
        except OSError:
            clone.__exit__()
            self.skipTest("Symlink creation unavailable on this host.")
        try:
            with self.assertRaises(WorkerInputError):
                clone.__exit__()
            self.assertTrue((target / "keep.txt").is_file())
        finally:
            link.unlink()
            shutil.rmtree(clone.project)


class FakeObject:
    def __init__(self):
        self.properties = {}

    def set_editor_property(self, key, value):
        self.properties[key] = value

    def get_editor_property(self, key):
        return self.properties[key]


class FakeTexture(FakeObject):
    def __init__(self, path):
        super().__init__()
        self.path = path

    def get_path_name(self):
        return self.path + "." + self.path.rsplit("/", 1)[1]

    def set_editor_property(self, key, value):
        super().set_editor_property(key, value)
        # Real 5.8.2 regression: changing compression can reset the colour-space setting.
        if key == "compression_settings":
            self.properties["srgb"] = False


class FakeTask(FakeObject):
    def get_objects(self):
        return self.objects


class TextureTests(Workspace):
    def setUp(self):
        super().setUp()
        self.lease = self.clone().__enter__()
        self.addCleanup(self.lease.__exit__)
        self.source = self.job / "input"
        self.source.mkdir()
        shutil.copyfile(
            REPO / "tests/fixtures/unreal/source/texture.png", self.source / "texture.png"
        )
        shutil.copyfile(
            REPO / "tests/fixtures/unreal/unreal-import-plan.json",
            self.source / "unreal-import-plan.json",
        )
        self.plan_path = self.source / "unreal-import-plan.json"
        self.plan = json.loads(self.plan_path.read_text("utf-8"))
        self.import_count = 0
        self.engine = types.SimpleNamespace(
            Paths=types.SimpleNamespace(project_dir=lambda: str(self.lease.project)),
            SystemLibrary=types.SimpleNamespace(get_engine_version=lambda: "5.8.2-test"),
            Texture2D=FakeTexture,
            TextureFactory=FakeObject,
            AssetImportTask=FakeTask,
            TextureCompressionSettings=types.SimpleNamespace(
                TC_DEFAULT="TC_DEFAULT", TC_MASKS="TC_MASKS", TC_NORMALMAP="TC_NORMALMAP"
            ),
            EditorAssetLibrary=self,
            AssetToolsHelpers=types.SimpleNamespace(get_asset_tools=lambda: self),
        )

    def path(self, virtual):
        return self.lease.project / "Content" / virtual.removeprefix("/Game/")

    def does_asset_exist(self, virtual):
        return self.path(virtual).with_suffix(".uasset").exists()

    def make_directory(self, virtual):
        self.path(virtual).mkdir(parents=True, exist_ok=True)
        return True

    def does_directory_exist(self, virtual):
        return self.path(virtual).is_dir()

    def import_asset_tasks(self, tasks):
        for task in tasks:
            self.import_count += 1
            task.objects = [
                FakeTexture(
                    task.properties["destination_path"] + "/" + task.properties["destination_name"]
                )
            ]

    def save_loaded_asset(self, texture, **_kwargs):
        self.path(texture.path).with_suffix(".uasset").write_text(json.dumps(texture.properties))
        return True

    def load_asset(self, virtual):
        texture = FakeTexture(virtual)
        texture.properties = json.loads(self.path(virtual).with_suffix(".uasset").read_text())
        return texture

    def execute(self, operation="import-unreal-textures"):
        request = {
            "protocolVersion": 1,
            "jobId": "Texture-Job",
            "operation": operation,
            "productManifestReference": "input/product.json",
            "inputDirectoryReference": "input",
            "outputDirectoryReference": "output",
            "resultFileReference": "output/result.json",
            "engineVersion": "5.8.2",
            "target": "unreal",
        }
        path = self.job / "request.json"
        path.write_text(json.dumps(request), encoding="utf-8")
        code = run(path, self.engine, io.StringIO(), io.StringIO())
        return code, json.loads((self.job / "output/result.json").read_text())

    def test_nine_imports_save_reopen_and_no_overwrite(self):
        code, imported = self.execute()
        self.assertEqual(0, code)
        self.assertEqual(9, len(imported["artifacts"]))
        self.assertEqual(9, self.import_count)
        code, verified = self.execute("verify-unreal-textures")
        self.assertEqual(0, code)
        self.assertEqual(imported["artifacts"], verified["artifacts"])
        code, failed = self.execute()
        self.assertEqual(5, code)
        self.assertEqual([], failed["artifacts"])
        self.assertEqual(9, self.import_count)

    def test_reopen_detects_saved_setting_corruption_without_repair(self):
        self.assertEqual(0, self.execute()[0])
        path = self.path("/Game/PBTextureFixture/Textures/T_Albedo").with_suffix(".uasset")
        properties = json.loads(path.read_text())
        properties["srgb"] = False
        path.write_text(json.dumps(properties))
        before = path.read_bytes()
        self.assertEqual(5, self.execute("verify-unreal-textures")[0])
        self.assertEqual(before, path.read_bytes())

    def test_mutated_source_fails_before_any_engine_import(self):
        (self.source / "texture.png").write_bytes(b"changed")
        self.assertEqual(5, self.execute()[0])
        self.assertEqual(0, self.import_count)

    def test_hostile_plan_fields_are_rejected(self):
        mutations = [
            lambda p: p.update(schemaVersion=True),
            lambda p: p.update(projectName="../escape"),
            lambda p: p.update(packRoot="/Game/Foreign"),
            lambda p: p.update(folders=["../escape"]),
            lambda p: p.update(extra="bad"),
            lambda p: p["textures"].append(p["textures"][0]),
            lambda p: p["textures"][0].update(sourceReference="../texture.png"),
            lambda p: p["textures"][0].update(byteCount=True),
            lambda p: p["textures"][0].update(srgb=1),
            lambda p: p["textures"][0].update(compression="TC_BAD"),
            lambda p: p["textures"][0].update(flipGreenChannel=True),
            lambda p: p["textures"][0].update(assetName="T_../oops"),
        ]
        for mutate in mutations:
            plan = copy.deepcopy(self.plan)
            mutate(plan)
            self.plan_path.write_text(json.dumps(plan), encoding="utf-8")
            with self.subTest(plan=plan), self.assertRaises((WorkerInputError, OSError)):
                load_plan(self.plan_path, self.source)

    def test_deep_duplicate_and_oversized_plans_fail_before_engine_calls(self):
        for text in ('{"profile":1,"profile":2}', "[" * 100 + "0" + "]" * 100, " " * 1_048_577):
            with self.subTest(size=len(text)):
                self.plan_path.write_text(text, encoding="utf-8")
                with self.assertRaises(WorkerInputError):
                    load_plan(self.plan_path, self.source)

    def test_texture_save_failure_never_claims_success(self):
        with patch.object(self, "save_loaded_asset", return_value=False):
            code, result = self.execute()
        self.assertEqual(5, code)
        self.assertEqual([], result["artifacts"])
        self.assertEqual("requires-cleanup", result["retrySafety"])


if __name__ == "__main__":
    unittest.main()
