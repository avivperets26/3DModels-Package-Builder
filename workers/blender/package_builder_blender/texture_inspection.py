"""Read-only Blender image, material-connection, and probable-role inspection."""

from __future__ import annotations

from collections.abc import Iterable
from dataclasses import dataclass
from typing import Any

from .inspection_common import InspectionFinding, required_name, safe_filename


@dataclass(frozen=True, slots=True)
class MaterialTextureConnectionReport:
    """One material image-node connection and its conservative probable role."""

    material_name: str
    node_name: str
    image_name: str
    destinations: tuple[str, ...]
    probable_role: str
    role_basis: str


@dataclass(frozen=True, slots=True)
class ImageTextureReport:
    """Immutable metadata for one packed, external, or generated Blender image."""

    name: str
    width: int
    height: int
    file_format: str
    color_space: str
    color_space_kind: str
    source_kind: str
    source_filename: str | None
    packed_byte_count: int
    material_connection_count: int


@dataclass(frozen=True, slots=True)
class TextureInspectionReport:
    """Deterministic image and material-connection inventory."""

    images: tuple[ImageTextureReport, ...]
    connections: tuple[MaterialTextureConnectionReport, ...]
    image_count: int
    packed_image_count: int
    external_image_count: int
    connected_image_count: int


@dataclass(frozen=True, slots=True)
class TextureInspectionResult:
    """Non-throwing result for expected Blender texture-data failures."""

    report: TextureInspectionReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether a complete report was produced."""

        return self.report is not None and not self.findings


_ROLE_TERMS: tuple[tuple[str, tuple[str, ...]], ...] = (
    ("normal", ("normal", "normalmap")),
    ("ambient_occlusion", ("ambientocclusion", "occlusion", "ao")),
    ("roughness", ("roughness", "rough")),
    ("metallic", ("metallic", "metalness", "metal")),
    ("emission", ("emission", "emissive")),
    ("opacity", ("opacity", "alpha", "transparency")),
    ("height", ("displacement", "height", "bump")),
    ("albedo", ("basecolor", "albedo", "diffuse")),
)


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-texture-inspector")


def _token(value: str) -> str:
    return "".join(character.lower() for character in value if character.isalnum())


def _probable_role(connection_text: str, fallback_text: str) -> tuple[str, str]:
    connected = _token(connection_text)
    matches = tuple(role for role, terms in _ROLE_TERMS if any(term in connected for term in terms))
    if len(matches) == 1:
        return matches[0], "material_connection"
    if len(matches) > 1:
        return "ambiguous", "material_connection"

    fallback = _token(fallback_text)
    matches = tuple(role for role, terms in _ROLE_TERMS if any(term in fallback for term in terms))
    if len(matches) == 1:
        return matches[0], "name_hint"
    if len(matches) > 1:
        return "ambiguous", "name_hint"
    return "unknown", "none"


def _reachable_destinations(node: Any) -> tuple[str, ...]:
    pending = list(node.outputs)
    visited_nodes: set[int] = set()
    destinations: set[str] = set()
    traversed_links = 0
    while pending:
        output = pending.pop()
        required_name(output.name)
        for link in tuple(output.links):
            traversed_links += 1
            if traversed_links > 512:
                raise ValueError
            target_node = link.to_node
            target_socket = link.to_socket
            node_name = required_name(target_node.name)
            node_type = required_name(target_node.type)
            socket_name = required_name(target_socket.name)
            label = getattr(target_node, "label", "")
            if label:
                label = required_name(label)
            destinations.add(f"{node_name}:{node_type}:{socket_name}:{label}")
            identity = id(target_node)
            if identity not in visited_nodes:
                visited_nodes.add(identity)
                pending.extend(tuple(getattr(target_node, "outputs", ())))
    return tuple(sorted(destinations))


def _connections(materials: tuple[Any, ...]) -> tuple[MaterialTextureConnectionReport, ...]:
    reports: list[MaterialTextureConnectionReport] = []
    material_names: set[str] = set()
    for material in materials:
        material_name = required_name(material.name)
        if material_name in material_names:
            raise ValueError
        material_names.add(material_name)
        node_tree = getattr(material, "node_tree", None)
        if node_tree is None or not bool(getattr(material, "use_nodes", True)):
            continue
        for node in tuple(node_tree.nodes):
            if getattr(node, "type", None) != "TEX_IMAGE":
                continue
            image = node.image
            if image is None:
                continue
            node_name = required_name(node.name)
            image_name = required_name(image.name)
            destinations = _reachable_destinations(node)
            role, basis = _probable_role(
                " ".join(destinations),
                f"{material_name} {node_name} {getattr(node, 'label', '')} {image_name}",
            )
            reports.append(
                MaterialTextureConnectionReport(
                    material_name,
                    node_name,
                    image_name,
                    destinations,
                    role,
                    basis,
                )
            )
    return tuple(
        sorted(reports, key=lambda item: (item.material_name, item.node_name, item.image_name))
    )


def _packed_bytes(image: Any) -> int:
    packed_files = tuple(getattr(image, "packed_files", ()))
    if not packed_files:
        packed_file = getattr(image, "packed_file", None)
        packed_files = () if packed_file is None else (packed_file,)
    total = 0
    for packed in packed_files:
        value = getattr(packed, "packed_file", packed)
        size = value.size
        if type(size) is not int or size <= 0:
            raise ValueError
        total += size
    return total


def _color_space_kind(value: str) -> str:
    token = _token(value)
    if token in {"srgb", "displayp3"} or token.startswith("agxbase"):
        return "srgb"
    if "linear" in token or token in {"noncolor", "raw"}:
        return "linear"
    return "other"


def inspect_textures(images: Iterable[Any], materials: Iterable[Any]) -> TextureInspectionResult:
    """Report Blender images and shader connections without saving, packing, or changing them."""

    try:
        source_images = tuple(images)
        source_materials = tuple(materials)
    except Exception:
        return TextureInspectionResult(
            None,
            (
                _finding(
                    "BLENDER_TEXTURE_INPUT_INVALID",
                    "Blender image or material data could not be enumerated safely.",
                    "Discard the workspace and retry inspection in a new Blender process.",
                ),
            ),
        )

    try:
        connections = _connections(source_materials)
        connected_names = {item.image_name for item in connections}
        image_names: set[str] = set()
        reports: list[ImageTextureReport] = []
        for image in source_images:
            name = required_name(image.name)
            if name in image_names:
                raise ValueError
            image_names.add(name)
            size = tuple(image.size)
            if len(size) != 2 or any(
                type(component) is not int or component <= 0 for component in size
            ):
                raise ValueError
            file_format = required_name(image.file_format)
            color_space = required_name(image.colorspace_settings.name)
            packed_bytes = _packed_bytes(image)
            source = required_name(image.source).upper()
            if packed_bytes > 0:
                source_kind = "packed"
            elif source in {"FILE", "SEQUENCE", "MOVIE", "TILED"}:
                source_kind = "external"
            else:
                source_kind = source.lower()
            filename = safe_filename(getattr(image, "filepath", None))
            reports.append(
                ImageTextureReport(
                    name,
                    size[0],
                    size[1],
                    file_format,
                    color_space,
                    _color_space_kind(color_space),
                    source_kind,
                    filename,
                    packed_bytes,
                    sum(item.image_name == name for item in connections),
                )
            )
        if any(item.image_name not in image_names for item in connections):
            raise ValueError
    except Exception:
        return TextureInspectionResult(
            None,
            (
                _finding(
                    "BLENDER_TEXTURE_DATA_INVALID",
                    "Blender returned incomplete or inconsistent image or material data.",
                    "Review the retained worker log, repair the material or image data, and retry.",
                ),
            ),
        )

    ordered = tuple(sorted(reports, key=lambda item: item.name))
    return TextureInspectionResult(
        TextureInspectionReport(
            ordered,
            connections,
            len(ordered),
            sum(item.source_kind == "packed" for item in ordered),
            sum(item.source_kind == "external" for item in ordered),
            len(connected_names),
        ),
        (),
    )
