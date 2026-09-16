# Unreal texture policy fixture

`source/texture.png` is an original synthetic 16×16 RGBA gradient generated for PB-1106, with distinct
green values and non-opaque alpha. It contains no third-party or customer content.
It is dedicated to the public domain under CC0-1.0, following the repository's original fixture
convention. The exact 508-byte source is hash-pinned in `clean-reimport-evidence.json` and the
closed binary-fixture allowlist; no arbitrary binary directory is permitted.
`unreal-import-plan.json` covers all eight canonical texture roles plus both resolved normal
orientations. The .NET golden test compares the entire serialized plan against canonical
Domain assignments. Python unit tests and the real Editor harness consume the same fixture.

The test deliberately assigns one image to several roles with distinct asset IDs. It validates
explicit interpretation and references, not realistic art, image conversion, rendering quality
or material appearance. Source fixtures remain tracked; all generated copies/assets are disposable.
