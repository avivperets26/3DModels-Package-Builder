"""Native stills with independent product-only opacity coverage and hash-bound media receipts."""

import struct
import zlib

from package_builder_protocol import atomic_write_result, resolve_logical_reference

from package_builder_unreal.import_plan import file_identity
from package_builder_unreal.overview import actor_editor, configure_camera, manual_exposure
from package_builder_unreal.project_validation import validate_overview


def write_png(path, width, height, rgba):
    """Encode only exact native RGBA readback, with no metadata or third-party image dependency."""
    if len(rgba) != width * height * 4 or width != 1920 or height != 1080:
        raise ValueError("Invalid native capture size.")

    def chunk(kind, data):
        return (
            struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
        )

    rows = b"".join(b"\0" + rgba[y * width * 4 : (y + 1) * width * 4] for y in range(height))
    path.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(rows))
        + chunk(b"IEND", b"")
    )


def render_previews(u, workspace, source, output):
    """Render saved final materials; coverage excludes studio/labels and preserves native opacity."""
    _, world, actors, plan = validate_overview(u, workspace, source)
    destination = resolve_logical_reference(output, "previews")
    if destination.exists():
        raise ValueError("Preview destination already exists.")
    destination.mkdir()
    editor = actor_editor(u)
    camera = editor.spawn_actor_from_class(u.SceneCapture2D, u.Vector(0, 0, 0))
    capture = camera.get_component_by_class(u.SceneCaptureComponent2D)
    capture.set_editor_property("capture_every_frame", False)
    capture.set_editor_property("capture_on_movement", False)
    manual_exposure(u, capture)
    target = u.RenderingLibrary.create_render_target2d(
        world, 1920, 1080, u.TextureRenderTargetFormat.RTF_RGBA8_SRGB
    )
    coverage_target = u.RenderingLibrary.create_render_target2d(
        world, 1920, 1080, u.TextureRenderTargetFormat.RTF_RGBA16F
    )
    receipts, artifacts = [], []
    if "PB_Label" in actors:
        actors["PB_Label"].set_actor_hidden_in_game(True)

    def pixels():
        u.SystemLibrary.execute_console_command(world, "Editor.AsyncAssetCompilationFinishAll")
        for _ in range(3):
            capture.capture_scene()
        result = u.RenderingLibrary.read_render_target(world, capture.texture_target, False)
        if result is None or len(result) != 1920 * 1080:
            raise RuntimeError("Native readback size differs.")
        return result

    try:
        for view in plan["views"]:
            u.log("Rendering overview view: " + view["id"])
            frame = configure_camera(u, camera, capture, actors["PB_Product"], view, plan)
            capture.clear_show_only_components()
            capture.set_editor_property(
                "primitive_render_mode",
                u.SceneCapturePrimitiveRenderMode.PRM_RENDER_SCENE_PRIMITIVES,
            )
            capture.set_editor_property("texture_target", target)
            capture.set_editor_property("capture_source", u.SceneCaptureSource.SCS_FINAL_COLOR_LDR)
            image = bytearray()
            for pixel in pixels():
                image.extend((pixel.r, pixel.g, pixel.b, 255))
            image_path = destination / (view["id"] + ".png")
            write_png(image_path, 1920, 1080, image)
            capture.set_editor_property(
                "primitive_render_mode", u.SceneCapturePrimitiveRenderMode.PRM_USE_SHOW_ONLY_LIST
            )
            capture.clear_show_only_components()
            capture.show_only_actor_components(actors["PB_Product"])
            capture.set_editor_property("texture_target", coverage_target)
            capture.set_editor_property("capture_source", u.SceneCaptureSource.SCS_SCENE_COLOR_HDR)
            mask = bytearray()
            for pixel in pixels():
                # SceneColor HDR stores inverse opacity in A; this is independent of RGB/exposure.
                mask.extend((255, 255, 255, 255 - pixel.a))
            coverage_path = destination / (view["id"] + "-coverage.png")
            write_png(coverage_path, 1920, 1080, mask)
            image_hash, image_size = file_identity(image_path)
            coverage_hash, coverage_size = file_identity(coverage_path)
            receipts.append(
                {
                    "id": view["id"],
                    "kind": view["kind"],
                    "image": image_path.name,
                    "imageSha256": image_hash,
                    "coverage": coverage_path.name,
                    "coverageSha256": coverage_hash,
                    "bounds": frame["bounds"],
                    "depthClipped": frame["depthClipped"],
                    "missingMaterials": 0,
                    "visibleHelpers": 0,
                }
            )
            for path, digest, size, role in (
                (image_path, image_hash, image_size, "preview-image"),
                (coverage_path, coverage_hash, coverage_size, "preview-coverage"),
            ):
                artifacts.append(
                    {
                        "artifactId": path.stem,
                        "role": role,
                        "logicalReference": path.relative_to(workspace).as_posix(),
                        "target": "unreal",
                        "sha256": digest,
                        "byteCount": size,
                    }
                )
    finally:
        editor.destroy_actor(camera)
        if "PB_Label" in actors:
            actors["PB_Label"].set_actor_hidden_in_game(False)
    receipt = destination / "capture-receipt.json"
    atomic_write_result(
        receipt,
        {
            "schemaVersion": 1,
            "profile": plan["profile"],
            "captures": receipts,
            "mediaValidationRequired": True,
        },
    )
    digest, size = file_identity(receipt)
    artifacts.append(
        {
            "artifactId": "unreal-capture-receipt",
            "role": "validation-evidence",
            "logicalReference": receipt.relative_to(workspace).as_posix(),
            "target": "unreal",
            "sha256": digest,
            "byteCount": size,
        }
    )
    return artifacts
