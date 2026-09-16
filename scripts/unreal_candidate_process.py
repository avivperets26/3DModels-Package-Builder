"""Trusted host-only candidate invocation shared by live texture and surface acceptance."""

import os
from pathlib import Path


def commandlet(clone, repository, evidence, key, render=False):
    """Keep writable state in the owned job and shared local DDC; no request can select scripts."""
    job = clone.job
    environment = dict(
        os.environ,
        PACKAGEBUILDER_WORKERS_ROOT=str(repository / "workers"),
        PACKAGEBUILDER_UNREAL_REQUEST=str(job / "request.json"),
        PYTHONDONTWRITEBYTECODE="1",
        TEMP=str(job / "temp"),
        TMP=str(job / "temp"),
    )
    environment["UE-LocalDataCachePath"] = str(repository / "runtime-data/unreal/5.8.2/ddc")
    environment["UE-SharedDataCachePath"] = "None"
    arguments = [
        str(clone.project / (clone.project_name + ".uproject")),
        "-run=pythonscript",
        "-script="
        + str(clone.project / "Plugins/PackageBuilderWorker/Content/Python/pb_worker_entry.py"),
        "-unattended",
        "-nosplash",
        "-nosound",
        "-ddc=(Local)",
        "-ini:EditorSettings:[/Script/UnrealEd.AnalyticsPrivacySettings]:bSendUsageData=False",
        "-UserDir=" + str(job / "user"),
        "-abslog=" + str(evidence / (key + ".log")),
    ]
    arguments += ["-AllowCommandletRendering", "-RenderOffscreen"] if render else ["-NullRHI"]
    return arguments, environment


def candidate_editor(profile):
    """Resolve only the user-approved installation checked by the PowerShell signature preflight."""
    return (
        Path(profile["userApprovedExternalInstallationRoot"])
        / "Engine/Binaries/Win64/UnrealEditor-Cmd.exe"
    )


def repair_redirectors(clone, repository, evidence, editor, timeout):
    """Resave references, then rescan/delete redirectors in a fresh project-only commandlet.

    UE 5.8's registry can retain the old map dependency during the resave process. A second
    bounded invocation reads the saved map; the caller still verifies no redirectors remain.
    """
    for key in ("fix-redirectors", "fix-redirectors-final"):
        arguments, environment = commandlet(clone, repository, evidence, key)
        arguments[1:3] = [
            "-run=ResavePackages",
            "-fixupredirects",
            "-projectonly",
            "-SCCProvider=None",
        ]
        code = clone.execute(editor, arguments, environment, timeout)
        if code:
            return code
    return 0
