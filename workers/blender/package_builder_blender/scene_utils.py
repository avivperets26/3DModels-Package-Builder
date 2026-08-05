"""Context-independent Blender scene reset and temporary data disposal utilities."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True, slots=True)
class SceneResetReport:
    """Counts produced by a direct-data scene reset."""

    objects_removed: int
    orphaned_data_blocks_removed: int


def _unique_by_identity(values: tuple[Any, ...]) -> tuple[Any, ...]:
    unique: list[Any] = []
    identities: set[int] = set()
    for value in values:
        identity = id(value)
        if identity not in identities:
            identities.add(identity)
            unique.append(value)
    return tuple(unique)


def _batch_remove(data: Any, data_blocks: tuple[Any, ...]) -> int:
    """Remove exact data-block identities without consulting Blender UI context."""

    unique = _unique_by_identity(data_blocks)
    if unique:
        data.batch_remove(ids=unique)
    return len(unique)


def reset_scene(data: Any) -> SceneResetReport:
    """Remove every object and recursively purge local orphaned data-blocks.

    ``data`` is deliberately ``bpy.data`` rather than the full ``bpy`` module. This keeps the
    boundary incapable of observing or mutating selection, active-object, area, or mode state.
    Linked orphaned data is retained because Package Builder must not mutate external libraries.
    """

    objects_removed = _batch_remove(data, tuple(data.objects))
    orphaned_data_blocks_removed = data.orphans_purge(
        do_local_ids=True,
        do_linked_ids=False,
        do_recursive=True,
    )
    if not isinstance(orphaned_data_blocks_removed, int) or orphaned_data_blocks_removed < 0:
        raise RuntimeError("Blender returned an invalid orphan-purge count.")
    return SceneResetReport(objects_removed, orphaned_data_blocks_removed)


class TemporaryDataBlocks:
    """Own data-blocks created for one operation and dispose them as one direct-data batch."""

    def __init__(self, data: Any) -> None:
        self._data = data
        self._data_blocks: list[Any] = []
        self._closed = False

    def register(self, data_block: Any) -> Any:
        """Register a temporary data-block and return it for fluent construction."""

        if self._closed:
            raise RuntimeError("Temporary data ownership is already closed.")
        if data_block is None:
            raise ValueError("A temporary data-block cannot be None.")
        self._data_blocks.append(data_block)
        return data_block

    def close(self) -> int:
        """Dispose registered data-blocks once, retaining ownership if Blender rejects cleanup."""

        if self._closed:
            return 0
        removed = _batch_remove(self._data, tuple(reversed(self._data_blocks)))
        self._data_blocks.clear()
        self._closed = True
        return removed

    def __enter__(self) -> TemporaryDataBlocks:
        return self

    def __exit__(self, _exception_type: object, _exception: object, _traceback: object) -> bool:
        self.close()
        return False
