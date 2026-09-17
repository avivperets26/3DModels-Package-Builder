"""Host for fast native graph feedback, with contained evidence and unconditional project cleanup."""

import json
import sys
import uuid
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]
from package_builder_protocol import load_bounded_json, resolve_logical_reference  # noqa: E402

from package_builder_unreal.project import UnrealProjectClone, remove_owned_tree  # noqa: E402
from package_builder_unreal.project_validation import log_findings  # noqa: E402
from unreal_candidate_process import candidate_editor, commandlet  # noqa: E402
from unreal_preview_helper import install_helper  # noqa: E402


def main(contract):
    """Use the already signature-checked candidate installation; no user asset is imported."""
    contract = resolve_logical_reference(REPO, contract)
    profile = load_bounded_json(REPO / "profiles/engines/unreal-5.8.2-candidate.json")
    run = uuid.uuid4().hex
    job, evidence = REPO / "artifacts/ue" / run, REPO / "artifacts/PB-1116" / run
    (job / "temp").mkdir(parents=True)
    evidence.mkdir(parents=True)
    receipt = {"passed": False, "cleanupSucceeded": False}
    try:
        with UnrealProjectClone(REPO, REPO / profile["template"], job, "PBGraphProbe") as clone:
            install_helper(REPO, clone)
            args, env = commandlet(clone, REPO, evidence, "graphs")
            args[2] = "-script=" + str(REPO / "scripts/unreal_preview_graph_probe.py")
            env.update(PB_PREVIEW_CONTRACT=str(contract), PB_PREVIEW_EVIDENCE=str(evidence))
            code = clone.execute(candidate_editor(profile), args, env, 180)
            if (
                code
                or not (evidence / "graphs.json").exists()
                or log_findings((evidence / "graphs.log").read_text("utf-8"))
            ):
                raise RuntimeError("Native graph conformance failed: " + str(evidence))
            receipt.update(passed=True, result=load_bounded_json(evidence / "graphs.json"))
    finally:
        remove_owned_tree(job, REPO / "artifacts/ue")
        receipt["cleanupSucceeded"] = not job.exists()
        (evidence / "receipt.json").write_text(json.dumps(receipt), encoding="utf-8")
        print(str(evidence))


if __name__ == "__main__":
    main(sys.argv[1])
