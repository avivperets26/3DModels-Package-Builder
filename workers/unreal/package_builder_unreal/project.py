"""Owned project clones and process-wide leases for the Unreal host adapter."""

from __future__ import annotations

import os
import shutil
import subprocess
import threading
from contextlib import contextmanager
from pathlib import Path

from package_builder_protocol import WorkerInputError, reject_linked_path, resolve_logical_reference

TEMPLATE_FILES = (
    "PackageBuilder.uproject",
    "Config/DefaultEngine.ini",
    "Config/DefaultEditor.ini",
    "Content/Pack/.gitkeep",
    "Plugins/PackageBuilderWorker/PackageBuilderWorker.uplugin",
    "Plugins/PackageBuilderWorker/Content/Python/pb_worker_entry.py",
)


def checked_tree(root: Path) -> list[Path]:
    """Inspect before descent so junctions never expand the owned tree."""
    reject_linked_path(root)
    entries = []
    pending = [root]
    while pending:
        for entry in pending.pop().iterdir():
            reject_linked_path(entry)
            entries.append(entry)
            if len(entries) > 100_000:
                raise WorkerInputError("Project inventory exceeds the safety bound.")
            if entry.is_dir():
                pending.append(entry)
    return entries


def remove_owned_tree(root: Path, owner: Path) -> None:
    """Delete only a verified strict descendant, never through engine-created links."""
    reject_linked_path(owner)
    reject_linked_path(root)
    if root == owner or not root.is_relative_to(owner):
        raise WorkerInputError("Cleanup target is outside its owner.")
    if root.exists():
        checked_tree(root)
        shutil.rmtree(root)


@contextmanager
def exclusive_file(path: Path):
    """Hold a nonblocking OS lock. Keep the inode until its owning job is disposed."""
    reject_linked_path(path)
    with path.open("a+b") as stream:
        stream.seek(0)
        if os.name == "nt":
            import msvcrt

            msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl

            fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            yield
        finally:
            stream.seek(0)
            if os.name == "nt":
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(stream.fileno(), fcntl.LOCK_UN)


class UnrealProjectClone:
    """Keep one job's lease through copying, all engine exits, and guarded cleanup.

    Only reviewed source-template files are copied. Existing projects are never adopted or
    removed. Callers own the job directory and must preserve diagnostics outside this clone.
    """

    def __init__(self, repository: Path, template: Path, job: Path, project_name: str):
        from package_builder_unreal.import_plan import validate_segment

        validate_segment(project_name)
        for path in (repository, template, job):
            reject_linked_path(path)
            if not path.is_absolute() or not path.is_dir():
                raise WorkerInputError("Clone roots must be existing absolute directories.")
        self.repository = repository.resolve()
        self.template = template.resolve()
        self.job = job.resolve()
        if (
            self.template == self.repository
            or self.job == self.repository
            or not self.template.is_relative_to(self.repository)
            or not self.job.is_relative_to(self.repository)
            or self.template.is_relative_to(self.job)
            or self.job.is_relative_to(self.template)
        ):
            raise WorkerInputError("Clone roots overlap or leave the repository.")
        self.project = resolve_logical_reference(self.job, "project")
        self.project_name = project_name
        self._lease = None
        self._owned = False
        self.cleanup_succeeded = False
        self._execution = threading.Lock()

    def __enter__(self):
        if self._lease is not None:
            raise RuntimeError("This clone already has an active lease.")
        self._lease = exclusive_file(resolve_logical_reference(self.job, ".unreal-clone.lock"))
        self._lease.__enter__()
        try:
            actual = sorted(
                p.relative_to(self.template).as_posix()
                for p in checked_tree(self.template)
                if p.is_file()
            )
            if actual != sorted(TEMPLATE_FILES):
                raise WorkerInputError("Template contains missing or unreviewed files.")
            self.project.mkdir()  # Atomic ownership; never delete another job's existing clone.
            self._owned = True
            for relative in TEMPLATE_FILES:
                source = resolve_logical_reference(self.template, relative)
                if source.stat().st_size > 1_048_576:
                    raise WorkerInputError("Template file exceeds the copy bound.")
                target = self.project / relative
                if relative == "PackageBuilder.uproject":
                    target = self.project / (self.project_name + ".uproject")
                if relative == "Content/Pack/.gitkeep":
                    target = self.project / "Content" / self.project_name / ".gitkeep"
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, target)
            return self
        except BaseException:
            self.__exit__(None, None, None)
            raise

    def execute(
        self, executable: Path, arguments: list[str], environment: dict[str, str], timeout: int
    ):
        """Run while leased; timeout kills the owned process tree before cleanup on Windows."""
        if not self._execution.acquire(blocking=False):
            raise RuntimeError("An engine is already executing against this clone.")
        try:
            if not self._owned or self._lease is None:
                raise RuntimeError("An active project lease is required.")
            return self._execute(executable, arguments, environment, timeout)
        finally:
            self._execution.release()

    def _execute(self, executable, arguments, environment, timeout):
        reject_linked_path(executable)
        if not executable.is_absolute() or not executable.is_file() or not 1 <= timeout <= 3600:
            raise WorkerInputError("Invalid process executable or timeout.")
        flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        # Trusted host supplies the preflight-verified executable and argument array; no shell.
        process = subprocess.Popen(  # noqa: S603
            [str(executable), *arguments], cwd=self.job, env=environment, creationflags=flags
        )
        try:
            return process.wait(timeout=timeout)
        finally:
            if process.poll() is None:
                if os.name == "nt":
                    taskkill = Path(os.environ["SYSTEMROOT"]) / "System32/taskkill.exe"
                    subprocess.run(  # noqa: S603 - OS tool, fixed flags, owned process ID only.
                        [str(taskkill), "/PID", str(process.pid), "/T", "/F"],
                        check=True,
                        creationflags=flags,
                        capture_output=True,
                    )
                else:
                    process.kill()
                process.wait(timeout=30)

    def __exit__(self, *_args):
        if not self._execution.acquire(blocking=False):
            raise RuntimeError("Cannot dispose a clone while its engine is running.")
        try:
            if self._owned:
                remove_owned_tree(self.project, self.job)
                self.cleanup_succeeded = not self.project.exists()
                self._owned = False
        finally:
            self._execution.release()
            if self._lease is not None:
                self._lease.__exit__(None, None, None)
                self._lease = None
