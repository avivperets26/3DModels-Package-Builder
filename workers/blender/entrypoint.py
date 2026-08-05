"""Bootstrap loaded by Blender's ``--python`` argument."""

import sys
from pathlib import Path

WORKER_ROOT = Path(__file__).resolve().parent
if str(WORKER_ROOT) not in sys.path:
    sys.path.insert(0, str(WORKER_ROOT))

from package_builder_blender.entrypoint import main_from_blender  # noqa: E402

raise SystemExit(main_from_blender())
