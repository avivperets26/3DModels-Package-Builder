"""Unreal centimetre/Z-up camera adaptation; source product transforms never change."""

import itertools
import math


def camera_frame(minimum, maximum, kind, fov, padding, aspect=1920 / 1080):
    """Fit all eight measured bounds corners using the native horizontal field of view."""
    if (
        not all(math.isfinite(x) for x in (*minimum, *maximum))
        or any(a > b for a, b in zip(minimum, maximum, strict=True))
        or minimum == maximum
    ):
        raise ValueError("Invalid or degenerate measured product bounds.")
    centre = tuple((a + b) / 2 for a, b in zip(minimum, maximum, strict=True))
    directions = {
        "hero": (1, 1, 0.65),
        "orthographic-front": (1, 0, 0),
        "orthographic-back": (-1, 0, 0),
        "orthographic-left": (0, -1, 0),
        "orthographic-right": (0, 1, 0),
    }
    backward = normalize(directions[kind])
    right = normalize((-backward[1], backward[0], 0))
    up = (
        backward[1] * right[2] - backward[2] * right[1],
        backward[2] * right[0] - backward[0] * right[2],
        backward[0] * right[1] - backward[1] * right[0],
    )
    corners = list(itertools.product(*zip(minimum, maximum, strict=True)))
    local = [
        tuple(
            dot(tuple(v - c for v, c in zip(p, centre, strict=True)), axis)
            for axis in (right, up, backward)
        )
        for p in corners
    ]
    tangent = math.tan(math.radians(fov / 2))
    distance = max(z + max(abs(x), abs(y) * aspect) * padding / tangent for x, y, z in local) + 1
    width = max(max(abs(x), abs(y) * aspect) for x, y, _ in local) * 2 * padding
    position = tuple(c + b * distance for c, b in zip(centre, backward, strict=True))
    projected = [
        (
            x / (width / 2 if kind != "hero" else (distance - z) * tangent) / 2 + 0.5,
            y / (width / (2 * aspect) if kind != "hero" else (distance - z) * tangent / aspect) / 2
            + 0.5,
        )
        for x, y, z in local
    ]
    return {
        "position": position,
        "centre": centre,
        "orthoWidth": width,
        "bounds": [
            min(p[0] for p in projected),
            min(p[1] for p in projected),
            max(p[0] for p in projected),
            max(p[1] for p in projected),
        ],
        "depthClipped": any(distance - z <= 1 for _, _, z in local),
    }


def dot(a, b):
    return sum(x * y for x, y in zip(a, b, strict=True))


def normalize(value):
    length = math.sqrt(dot(value, value))
    return tuple(x / length for x in value)
