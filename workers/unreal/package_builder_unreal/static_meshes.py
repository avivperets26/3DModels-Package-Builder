"""Pinned normalized-FBX geometry policy; source material slots are explicitly replaced."""

from package_builder_protocol import resolve_logical_reference


def mesh_editor(u):
    """Commandlets do not instantiate this editor subsystem; its mesh operations are stateless."""
    return u.get_editor_subsystem(u.StaticMeshEditorSubsystem) or u.get_default_object(
        u.StaticMeshEditorSubsystem
    )


def import_settings(u):
    """Preserve imported normals/tangents and source LOD0; convert FBX units/axes exactly once."""
    return {
        "combine_meshes": True,
        "convert_scene": True,
        "convert_scene_unit": True,
        "force_front_x_axis": False,
        "import_uniform_scale": 1.0,
        "import_mesh_lo_ds": False,
        "auto_generate_collision": False,
        "generate_lightmap_u_vs": True,
        "normal_import_method": u.FBXNormalImportMethod.FBXNIM_IMPORT_NORMALS_AND_TANGENTS,
        "normal_generation_method": u.FBXNormalGenerationMethod.MIKK_T_SPACE,
    }


def collision_trace(u, policy):
    # Empty simple geometry + SimpleAsComplex excludes triangle queries too, even when
    # a consuming component overrides the asset's default NoCollision profile.
    return getattr(
        u.CollisionTraceFlag,
        {
            "none": "CTF_USE_SIMPLE_AS_COMPLEX",
            "box": "CTF_USE_SIMPLE_AND_COMPLEX",
            "complex-as-simple": "CTF_USE_COMPLEX_AS_SIMPLE",
        }[policy],
    )


def import_mesh(u, root, source, entry):
    """Import a bounded FBX into one SM asset with no imported materials or automatic collision."""
    options = u.FbxImportUI()
    for key, value in {
        "automated_import_should_detect_type": False,
        "mesh_type_to_import": u.FBXImportType.FBXIT_STATIC_MESH,
        "import_mesh": True,
        "import_as_skeletal": False,
        "import_animations": False,
        "import_materials": False,
        "import_textures": False,
        "create_physics_asset": False,
    }.items():
        options.set_editor_property(key, value)
    data = options.get_editor_property("static_mesh_import_data")
    for key, value in import_settings(u).items():
        data.set_editor_property(key, value)
    task = u.AssetImportTask()
    for key, value in {
        "filename": str(resolve_logical_reference(source, entry["sourceReference"])),
        "destination_path": root + "/Meshes",
        "destination_name": "SM_" + entry["id"],
        "automated": True,
        "replace_existing": False,
        "save": False,
        "factory": u.FbxFactory(),
        "options": options,
    }.items():
        task.set_editor_property(key, value)
    u.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
    objects = list(task.get_objects())
    if len(objects) != 1 or not isinstance(objects[0], u.StaticMesh):
        raise RuntimeError("Expected exactly one static mesh.")
    mesh = objects[0]
    if mesh.get_path_name() != root + "/Meshes/SM_" + entry["id"] + ".SM_" + entry["id"]:
        raise RuntimeError("Mesh destination mismatch.")
    if len(mesh.get_editor_property("static_materials")) != len(entry["materials"]):
        raise RuntimeError("FBX material-slot inventory differs from plan.")
    if [
        str(slot.get_editor_property("material_slot_name"))
        for slot in mesh.get_editor_property("static_materials")
    ] != entry["sourceSlots"]:
        raise RuntimeError("FBX material-slot identities differ from plan.")
    mesh.set_editor_property("lod_group", "None")
    nanite = mesh.get_editor_property("nanite_settings")
    nanite.set_editor_property("enabled", False)
    mesh.set_editor_property("nanite_settings", nanite)
    for index, material_id in enumerate(entry["materials"]):
        material = u.EditorAssetLibrary.load_asset(root + "/Materials/MI_" + material_id)
        if not isinstance(material, u.MaterialInstanceConstant):
            raise RuntimeError("Mesh material is missing.")
        mesh.set_material(index, material)
    subsystem = mesh_editor(u)
    settings = subsystem.get_lod_build_settings(mesh, 0)
    settings.set_editor_property("recompute_normals", False)
    settings.set_editor_property("recompute_tangents", False)
    settings.set_editor_property("use_mikk_t_space", True)
    subsystem.set_lod_build_settings(mesh, 0, settings)
    subsystem.remove_collisions(mesh)
    if (
        entry["collision"] == "box"
        and subsystem.add_simple_collisions(mesh, u.ScriptCollisionShapeType.BOX) < 0
    ):
        raise RuntimeError("Box collision generation failed.")
    body = mesh.get_editor_property("body_setup")
    body.set_editor_property("collision_trace_flag", collision_trace(u, entry["collision"]))
    instance = body.get_editor_property("default_instance")
    instance.set_editor_property(
        "collision_profile_name", "NoCollision" if entry["collision"] == "none" else "BlockAll"
    )
    body.set_editor_property("default_instance", instance)
    if not u.EditorAssetLibrary.save_loaded_asset(mesh, only_if_is_dirty=False):
        raise RuntimeError("Static mesh save failed.")


def verify_mesh(u, root, entry):
    """Check native bounds, LOD0, material bindings, normal settings and collision after reload."""
    mesh = u.EditorAssetLibrary.load_asset(root + "/Meshes/SM_" + entry["id"])
    if not isinstance(mesh, u.StaticMesh):
        raise RuntimeError("Static mesh is missing.")
    subsystem = mesh_editor(u)
    if subsystem.get_lod_count(mesh) != 1 or subsystem.get_number_verts(mesh, 0) <= 0:
        raise RuntimeError("Static mesh LOD inventory failed.")
    if str(mesh.get_editor_property("lod_group")) != "None" or mesh.get_editor_property(
        "nanite_settings"
    ).get_editor_property("enabled"):
        raise RuntimeError("Static mesh LOD/Nanite policy differs.")
    settings = subsystem.get_lod_build_settings(mesh, 0)
    if (
        settings.get_editor_property("recompute_normals")
        or settings.get_editor_property("recompute_tangents")
        or not settings.get_editor_property("use_mikk_t_space")
    ):
        raise RuntimeError("Static mesh normal/tangent policy differs.")
    data = mesh.get_editor_property("asset_import_data")
    for key, expected in import_settings(u).items():
        if data.get_editor_property(key) != expected:
            raise RuntimeError("Static mesh import settings differ: " + key)
    bounds = mesh.get_bounding_box()
    size = bounds.max - bounds.min
    if any(
        abs(actual - expected) > max(0.01, expected * 0.0001)
        for actual, expected in zip((size.x, size.y, size.z), entry["expectedSizeCm"], strict=True)
    ):
        raise RuntimeError("Static mesh scale/orientation bounds differ.")
    if len(mesh.get_editor_property("static_materials")) != len(entry["materials"]):
        raise RuntimeError("Static mesh slots differ.")
    if [
        str(slot.get_editor_property("material_slot_name"))
        for slot in mesh.get_editor_property("static_materials")
    ] != entry["sourceSlots"]:
        raise RuntimeError("Static mesh slot names differ.")
    for index, name in enumerate(entry["materials"]):
        material = mesh.get_material(index)
        if (
            material is None
            or material.get_path_name() != root + "/Materials/MI_" + name + ".MI_" + name
        ):
            raise RuntimeError("Static mesh material reference differs.")
    body = mesh.get_editor_property("body_setup")
    if body.get_editor_property("collision_trace_flag") != collision_trace(
        u, entry["collision"]
    ) or subsystem.get_simple_collision_count(mesh) != (1 if entry["collision"] == "box" else 0):
        raise RuntimeError("Static mesh collision geometry differs.")
    expected_profile = "NoCollision" if entry["collision"] == "none" else "BlockAll"
    if (
        str(
            body.get_editor_property("default_instance").get_editor_property(
                "collision_profile_name"
            )
        )
        != expected_profile
    ):
        raise RuntimeError("Static mesh collision profile differs.")
