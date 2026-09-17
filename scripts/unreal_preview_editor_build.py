"""Compile the owned editor helper, verify it in Unreal, and remove its temporary project."""

import argparse
import json
import shutil
import struct
import sys
import uuid
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]
from package_builder_protocol import load_bounded_json  # noqa: E402

from package_builder_unreal.import_plan import file_identity  # noqa: E402
from package_builder_unreal.project import (  # noqa: E402
    UnrealProjectClone,
    checked_tree,
    remove_owned_tree,
)
from package_builder_unreal.project_validation import log_findings  # noqa: E402
from unreal_candidate_process import candidate_editor, commandlet  # noqa: E402


def main(editor, timeout):
    """Use the signed candidate preflight and existing process lease/cleanup boundary."""
    profile = load_bounded_json(REPO / "profiles/engines/unreal-5.8.2-candidate.json")
    if editor != candidate_editor(profile):
        raise ValueError("Unexpected candidate editor.")
    engine = editor.parents[3]
    run = uuid.uuid4().hex
    job, evidence = REPO / "artifacts/ue" / run, REPO / "artifacts/PB-1116" / run
    (job / "temp").mkdir(parents=True)
    evidence.mkdir(parents=True)
    receipt = {"passed": False, "cleanupSucceeded": False}
    try:
        with UnrealProjectClone(REPO, REPO / profile["template"], job, "PBEditorBuild") as clone:
            source = REPO / "workers/unreal/editor/PackageBuilderPreviewEditor"
            source_files = {
                p.relative_to(source).as_posix(): file_identity(p)
                for p in checked_tree(source)
                if p.is_file()
            }
            plugin = clone.project / "Plugins/PackageBuilderPreviewEditor"
            shutil.copytree(source, plugin)
            descriptor = clone.project / "PBEditorBuild.uproject"
            data = load_bounded_json(descriptor)
            data["Plugins"].append({"Name": "PackageBuilderPreviewEditor", "Enabled": True})
            descriptor.write_text(json.dumps(data), encoding="utf-8")
            # UE 5.8 XmlConfigData serialization v2: no input files and no value overrides.
            # Supplying this explicit empty cache avoids importing/creating global user config.
            cache = job / "BuildConfiguration.bin"
            cache.write_bytes(struct.pack("<iii", 2, 0, 0))
            args, env = commandlet(clone, REPO, evidence, "smoke")
            env.update(
                DOTNET_CLI_TELEMETRY_OPTOUT="1",
                DOTNET_NOLOGO="1",
                DOTNET_CLI_HOME=str(job / "dotnet"),
                PB_HELPER_EVIDENCE=str(evidence),
            )
            runtimes = list(
                (engine / "Engine/Binaries/ThirdParty/DotNet").glob("*/win-x64/dotnet.exe")
            )
            if len(runtimes) != 1:
                raise RuntimeError("Candidate bundled .NET runtime is ambiguous.")
            build = [
                str(engine / "Engine/Binaries/DotNET/UnrealBuildTool/UnrealBuildTool.dll"),
                "UnrealEditor",
                "Win64",
                "Development",
                "-Project=" + str(descriptor),
                "-Plugin=" + str(plugin / "PackageBuilderPreviewEditor.uplugin"),
                "-NoUBTMakefiles",
                "-NoHotReload",
                "-NoEngineChanges",
                "-NoUBA",
                "-UBADisableRemote",
                "-UBARootDir=" + str(job / "uba"),
                "-UBATraceOutputFile=" + str(evidence / "build.uba"),
                "-NoXGE",
                "-NoSNDBS",
                "-XmlConfigCache=" + str(cache),
                "-Log=" + str(evidence / "build.log"),
            ]
            code = clone.execute(runtimes[0], build, env, timeout)
            receipt["buildExitCode"] = code
            if code:
                raise RuntimeError("Native helper compilation failed.")
            args[2] = "-script=" + str(REPO / "scripts/unreal_preview_editor_smoke.py")
            code = clone.execute(editor, args, env, timeout)
            receipt["smokeExitCode"] = code
            if code or log_findings((evidence / "smoke.log").read_text("utf-8")):
                raise RuntimeError("Native widget smoke failed or emitted diagnostics.")
            receipt["widget"] = load_bounded_json(evidence / "widget.json")
            if any(
                file_identity(source / name) != identity for name, identity in source_files.items()
            ):
                raise RuntimeError("Build modified helper source.")
            output = REPO / "tools/unreal-preview-helper/5.8.2" / run
            output.mkdir(parents=True)
            shutil.copy2(plugin / "PackageBuilderPreviewEditor.uplugin", output)
            shutil.copytree(plugin / "Binaries", output / "Binaries")
            receipt.update(
                passed=True,
                helper=output.relative_to(REPO).as_posix(),
                sourceUnchanged=True,
                sourceFiles=source_files,
                binaries={
                    p.relative_to(output).as_posix(): file_identity(p)
                    for p in checked_tree(output)
                    if p.is_file()
                },
            )
    finally:
        remove_owned_tree(job, REPO / "artifacts/ue")
        receipt["cleanupSucceeded"] = not job.exists()
        (evidence / "receipt.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
        print(json.dumps(receipt))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--editor", type=Path, required=True)
    parser.add_argument("--timeout", type=int, default=900, choices=range(30, 3601))
    options = parser.parse_args()
    main(options.editor, options.timeout)
