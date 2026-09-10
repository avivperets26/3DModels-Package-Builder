# Twelve-column collection fixture

Original procedural geometry and 2x2 texture authored for Package Builder; dedicated to
the public domain under **CC0-1.0**. No downloaded or customer assets are included.

Twelve independent columns have different polygon counts and dimensions. Declaration
order intentionally differs from filename order. All reference one shared stone texture.
`manifest.json` is the reviewed source for both portable and Unity plans;
`expectations.json` is the independent geometry oracle (cylinder triangle count 4n-4).

Regenerate with the pinned Blender and `tests/blender/engine/pb0811_collection_fixture.py
-- generate <this-directory>`. FBX metadata may change; source bytes are pinned by the
clean-reimport evidence hashes. The verification command imports the actual portable ZIP
into isolated scenes and requires texture resolution inside its extracted directory.

These small golden source files are retained. Generated ZIPs, Unity packages, extractions
and temporary projects are disposed by the integration harness; compact reports remain.
