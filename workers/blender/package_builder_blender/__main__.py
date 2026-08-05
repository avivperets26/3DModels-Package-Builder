"""Allow protocol-shell execution with ``python -m package_builder_blender`` for tests."""

from package_builder_blender.entrypoint import main

raise SystemExit(main())
