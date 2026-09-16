"""Native Unreal graph construction; scalar intent comes from the canonical host material."""

import math


def equivalent_float(actual, expected):
    """Allow native float32 rounding across the bounded HDR/UV range, not semantic drift."""
    return math.isclose(actual, expected, rel_tol=1e-7, abs_tol=1e-5)


class MaterialGraph:
    """Checks every graph edge so a missing engine pin cannot silently produce a partial material."""

    def __init__(self, unreal, material):
        self.u = unreal
        self.material = material
        self.lib = unreal.MaterialEditingLibrary

    def node(self, kind, **properties):
        node = self.lib.create_material_expression(
            self.material, getattr(self.u, "MaterialExpression" + kind)
        )
        if node is None:
            raise RuntimeError("Material expression creation failed.")
        for key, value in properties.items():
            node.set_editor_property(key, value)
        return node

    def edge(self, source, target, pin, channel=""):
        if not self.lib.connect_material_expressions(source, channel, target, pin):
            raise RuntimeError("Material graph edge failed.")

    def product(self, left, right, channel=""):
        node = self.node("Multiply")
        self.edge(left, node, "A", channel)
        self.edge(right, node, "B")
        return node

    def scalar(self, name, value):
        return self.node("ScalarParameter", parameter_name=name, default_value=value)

    def output(self, node, name):
        if not self.lib.connect_material_property(
            node, "", getattr(self.u.MaterialProperty, "MP_" + name)
        ):
            raise RuntimeError("Material output connection failed.")


def material_properties(u, entry):
    """Map the reviewed mode directly; never infer transparency from texture filenames."""
    return {
        "blend_mode": getattr(
            u.BlendMode,
            {
                "opaque": "BLEND_OPAQUE",
                "cutout": "BLEND_MASKED",
                "transparent": "BLEND_TRANSLUCENT",
            }[entry["surface"]],
        ),
        "two_sided": entry["twoSided"],
        "opacity_mask_clip_value": entry["alphaCutoff"],
    }


def compile_material(u, root, entry):
    """Build one master and instance with saved explicit texture/scalar parameters."""
    tools = u.AssetToolsHelpers.get_asset_tools()
    material = tools.create_asset(
        "M_" + entry["id"], root + "/Materials", u.Material, u.MaterialFactoryNew()
    )
    if material is None:
        raise RuntimeError("Material creation failed.")
    for key, value in material_properties(u, entry).items():
        material.set_editor_property(key, value)
    g = MaterialGraph(u, material)
    uv = g.node("TextureCoordinate")
    scale = g.node("Constant2Vector", r=entry["uv"][0], g=entry["uv"][1])
    offset = g.node("Constant2Vector", r=entry["uv"][2], g=entry["uv"][3])
    uv_scaled = g.product(uv, scale)
    uv_final = g.node("Add")
    g.edge(uv_scaled, uv_final, "A")
    g.edge(offset, uv_final, "B")
    samples = {}
    for role, name in entry["textures"].items():
        texture = u.EditorAssetLibrary.load_asset(root + "/Textures/" + name)
        if not isinstance(texture, u.Texture2D):
            raise RuntimeError("Material texture is unavailable.")
        sample = g.node(
            "TextureSampleParameter2D",
            parameter_name=role,
            texture=texture,
            sampler_type=getattr(
                u.MaterialSamplerType,
                "SAMPLERTYPE_NORMAL"
                if role == "normal"
                else "SAMPLERTYPE_COLOR"
                if role in ("albedo", "emission")
                else "SAMPLERTYPE_MASKS",
            ),
        )
        g.edge(uv_final, sample, "UVs")
        samples[role] = sample
    white = g.node("Constant3Vector", constant=u.LinearColor(1, 1, 1, 1))
    g.output(samples.get("albedo", white), "BASE_COLOR")
    for role, channel in (("roughness", "G"), ("metallic", "B")):
        scalar = g.scalar(role, entry[role])
        g.output(
            g.product(samples["orm"], scalar, channel) if "orm" in samples else scalar, role.upper()
        )
    if "orm" in samples:
        ao = g.node("LinearInterpolate", const_a=1)
        g.edge(samples["orm"], ao, "B", "R")
        g.edge(g.scalar("occlusionStrength", entry["occlusionStrength"]), ao, "Alpha")
        g.output(ao, "AMBIENT_OCCLUSION")
    if "normal" in samples:
        # Scale tangent-space XY, preserve Z, then normalize (no colour-space conversion).
        normal_scale = g.node(
            "VectorParameter",
            parameter_name="normalScale",
            default_value=u.LinearColor(entry["normalScale"], entry["normalScale"], 1, 1),
        )
        normal = g.node("Normalize")
        g.edge(g.product(samples["normal"], normal_scale), normal, "VectorInput")
        g.output(normal, "NORMAL")
    emission = g.node(
        "VectorParameter",
        parameter_name="emissionColor",
        default_value=u.LinearColor(*entry["emission"][:3], 1),
    )
    if "emission" in samples:
        emission = g.product(samples["emission"], emission)
    g.output(
        g.product(emission, g.scalar("emissionIntensity", entry["emission"][3])), "EMISSIVE_COLOR"
    )
    if entry["surface"] != "opaque":
        alpha = g.scalar("opacity", entry["opacity"])
        if "albedo" in samples:
            alpha = g.product(samples["albedo"], alpha, "A")
        if "opacity" in samples:
            alpha = g.product(samples["opacity"], alpha, "R")
        g.output(alpha, "OPACITY_MASK" if entry["surface"] == "cutout" else "OPACITY")
    if g.lib.recompile_material(material):
        raise RuntimeError("Native material compilation failed.")
    instance = tools.create_asset(
        "MI_" + entry["id"],
        root + "/Materials",
        u.MaterialInstanceConstant,
        u.MaterialInstanceConstantFactoryNew(),
    )
    if instance is None:
        raise RuntimeError("Material instance creation failed.")
    g.lib.set_material_instance_parent(instance, material)
    # Explicit instance overrides prove that generated instances are editable and self-contained.
    for role in ("roughness", "metallic"):
        # UE 5.8.2's setter returns false even after success (vendor implementation).
        # Read back the actual value rather than treating that return flag as evidence.
        g.lib.set_material_instance_scalar_parameter_value(instance, role, entry[role])
        if (
            abs(g.lib.get_material_instance_scalar_parameter_value(instance, role) - entry[role])
            > 0.00001
        ):
            raise RuntimeError("Material instance parameter failed.")
    g.lib.update_material_instance(instance)
    for asset in (material, instance):
        if not u.EditorAssetLibrary.save_loaded_asset(asset, only_if_is_dirty=False):
            raise RuntimeError("Material save failed.")


def verify_material(u, root, entry):
    """Observe saved master settings, graph outputs, texture parameters and instance inheritance without repair."""
    lib = u.MaterialEditingLibrary
    material = u.EditorAssetLibrary.load_asset(root + "/Materials/M_" + entry["id"])
    instance = u.EditorAssetLibrary.load_asset(root + "/Materials/MI_" + entry["id"])
    if (
        not isinstance(material, u.Material)
        or not isinstance(instance, u.MaterialInstanceConstant)
        or instance.get_editor_property("parent") != material
    ):
        raise RuntimeError("Material instance parent mismatch.")
    for key, expected in material_properties(u, entry).items():
        actual = material.get_editor_property(key)
        matches = (
            equivalent_float(actual, expected)
            if isinstance(expected, float)
            else actual == expected
        )
        if not matches:
            raise RuntimeError("Persisted material mode mismatch.")
    outputs = ["BASE_COLOR", "ROUGHNESS", "METALLIC", "EMISSIVE_COLOR"]
    outputs += ["NORMAL"] if "normal" in entry["textures"] else []
    outputs += ["AMBIENT_OCCLUSION"] if "orm" in entry["textures"] else []
    if entry["surface"] != "opaque":
        outputs.append("OPACITY_MASK" if entry["surface"] == "cutout" else "OPACITY")
    for output in outputs:
        if (
            lib.get_material_property_input_node(
                material, getattr(u.MaterialProperty, "MP_" + output)
            )
            is None
        ):
            raise RuntimeError("Missing material graph output.")
    for role, name in entry["textures"].items():
        texture = lib.get_material_instance_texture_parameter_value(instance, role)
        if texture is None or texture.get_path_name() != root + "/Textures/" + name + "." + name:
            raise RuntimeError("Persisted texture parameter mismatch.")
    scalars = {
        "roughness": entry["roughness"],
        "metallic": entry["metallic"],
        "emissionIntensity": entry["emission"][3],
    }
    if "orm" in entry["textures"]:
        scalars["occlusionStrength"] = entry["occlusionStrength"]
    if entry["surface"] != "opaque":
        scalars["opacity"] = entry["opacity"]
    for role, expected in scalars.items():
        if not equivalent_float(
            lib.get_material_instance_scalar_parameter_value(instance, role), expected
        ):
            raise RuntimeError("Persisted scalar parameter mismatch.")
    vectors = {"emissionColor": (*entry["emission"][:3], 1)}
    if "normal" in entry["textures"]:
        vectors["normalScale"] = (entry["normalScale"], entry["normalScale"], 1, 1)
    for role, expected in vectors.items():
        actual = lib.get_material_instance_vector_parameter_value(instance, role)
        if any(
            not equivalent_float(a, b)
            for a, b in zip((actual.r, actual.g, actual.b, actual.a), expected, strict=True)
        ):
            raise RuntimeError("Persisted vector parameter mismatch.")
    uv_nodes = [
        node
        for node in lib.get_material_expressions(material)
        if isinstance(node, u.MaterialExpressionConstant2Vector)
    ]
    if len(uv_nodes) != 2 or any(
        not equivalent_float(node.get_editor_property(component), entry["uv"][index * 2 + offset])
        for index, node in enumerate(uv_nodes)
        for offset, component in enumerate(("r", "g"))
    ):
        raise RuntimeError("Persisted UV transform mismatch.")
