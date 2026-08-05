"""Shared immutable validation primitives for direct-data Blender inspectors."""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True, slots=True)
class InspectionFinding:
    """Stable sanitized failure compatible with the PB-0109 finding contract."""

    code: str
    explanation: str
    suggested_action: str
    source: str

    def as_protocol_value(self) -> dict[str, Any]:
        """Return the public blocking-error representation."""

        return {
            "code": self.code,
            "severity": "error",
            "explanation": self.explanation,
            "source": self.source,
            "suggestedAction": self.suggested_action,
            "blocksRelease": True,
        }


def required_name(value: Any) -> str:
    """Copy one non-empty control-free Blender identity without normalization."""

    if not isinstance(value, str) or not value or any(ord(character) < 32 for character in value):
        raise ValueError
    return value


def finite_number(value: Any) -> float:
    """Copy one finite non-boolean numeric value."""

    if type(value) not in {float, int} or not math.isfinite(value):
        raise ValueError
    return float(value)


def finite_components(value: Any, length: int) -> tuple[float, ...]:
    """Copy an exact-size finite numeric sequence."""

    components = tuple(value)
    if len(components) != length:
        raise ValueError
    return tuple(finite_number(component) for component in components)


def matrix_components(value: Any) -> tuple[float, ...]:
    """Copy a finite Blender 4x4 matrix in deterministic row-major order."""

    rows = tuple(value)
    if len(rows) != 4:
        raise ValueError
    return tuple(component for row in rows for component in finite_components(row, 4))


def safe_filename(value: Any) -> str | None:
    """Return only a safe final filename, never an external physical directory."""

    if value in {None, ""}:
        return None
    path = required_name(value).replace("\\", "/").rstrip("/")
    name = path.rsplit("/", 1)[-1]
    if name in {"", ".", ".."}:
        raise ValueError
    return name
