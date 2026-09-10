# Equipment set fixture

Original procedural helmet and torso-armour test meshes, with one shared 2×2 steel-colour texture.
Created for this repository and dedicated to the public domain under CC0-1.0.
No customer or marketplace assets are included.

`tests/blender/engine/pb0810_equipment_fixture.py generate <source-directory>` regenerates
the sources using pinned Blender 5.0.0. Committed source bytes provide a stable test input;
Blender FBX metadata is not claimed to regenerate byte-identically.

The reviewed manifest feeds the production item/reuse, assembly, preview and portable
plans. The integration harness reimports the produced ZIP into fresh Blender scenes
and the produced Unity package into a new project. The primitive shapes test packaging
and reference preservation; they are not a character-fitting or artistic-quality test.
