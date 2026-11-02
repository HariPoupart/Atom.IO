"""Write the crew's results to a staging folder that mirrors a Unity project.

Nothing touches the real Unity project until the developer runs
``python main.py promote`` (the manual gate from GDD 2.4 / 2.5).
"""
from __future__ import annotations

import json
import shutil
from datetime import datetime
from pathlib import Path

from . import validators
from .tiers import build_all
from .tools import STATE

TIERS_DIR = Path("unity/Assets/Resources/AtomIO/Tiers")


def _dump(path: Path, data) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if hasattr(data, "model_dump"):
        data = data.model_dump()
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n")


def write_run(out_root: Path, usage: dict, mode: str) -> tuple[Path, dict]:
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    run = out_root / f"run_{stamp}"
    run.mkdir(parents=True, exist_ok=True)

    s = STATE
    if s.manifest is not None:
        _dump(run / "01_asset_manifest.json", s.manifest)
    if s.review is not None:
        _dump(run / "02_architect_review.json", s.review)
    if s.design is not None:
        _dump(run / "03_tier_design_set.json", s.design)
        s.tier_files, s.tier_index = build_all(s.design)
        for fname, data in s.tier_files.items():
            _dump(run / TIERS_DIR / fname, data)
        _dump(run / TIERS_DIR / "index.json", s.tier_index)

    code_paths = []
    if s.code is not None:
        for f in s.code.files:
            if not f.path.startswith(validators.SCRIPTS_ROOT) or ".." in f.path:
                continue  # never write outside the scripts folder
            dest = run / "unity" / f.path
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_text(f.content)
            code_paths.append(f.path)

    # Independent re-run of the validation suite (does not trust the LLM's report).
    gate = validators.run_all(manifest=s.manifest, review=s.review, design=s.design,
                              tier_files=s.tier_files, code=s.code)
    _dump(run / "05_validation_report.json", {
        "deterministic_suite": gate,
        "lead_architect_report": s.validation_report.model_dump() if s.validation_report else None,
    })
    _dump(run / "usage.json", {"mode": mode, **usage})

    changes = [f"# CHANGES ({stamp}, {mode} run)", "",
               f"Gate status: **{'READY FOR REVIEW' if gate['passed'] else 'BLOCKED'}** "
               f"({len(gate['errors'])} errors, {len(gate['warnings'])} warnings)", ""]
    if s.code is not None:
        changes += [s.code.changes_md.strip(), ""]
    if s.tier_index:
        src = s.tier_index["design_sources"]
        changes += ["## Level Designer: tier configs", "",
                    f"- 118 tier files + `tier_fallback.json` + `index.json` in `Assets/Resources/AtomIO/Tiers/`",
                    f"- Design sources: {src['group']} by group, {src['override']} landmark overrides, "
                    f"{src['fallback']} fallback", ""]
    if s.review is not None:
        changes += ["## Lead Architect: asset decisions", "",
                    *[f"- Approved: {a}" for a in s.review.approved_assets],
                    *[f"- Rejected: {r.name} ({r.reason})" for r in s.review.rejected_assets], ""]
    (run / "CHANGES.md").write_text("\n".join(changes))
    (run / "GATE.json").write_text(json.dumps({"status": "pending_review" if gate["passed"] else "blocked",
                                               "code_files": code_paths}, indent=2))

    latest = out_root / "latest"
    if latest.is_symlink() or latest.exists():
        latest.unlink() if latest.is_symlink() else shutil.rmtree(latest)
    try:
        latest.symlink_to(run.name)
    except OSError:
        shutil.copytree(run, latest)
    return run, gate


def promote(run: Path, unity_project: Path, assume_yes: bool = False) -> int:
    """Manual gate: copy a validated run into a real Unity project."""
    gate_file = run / "GATE.json"
    if not gate_file.exists():
        print(f"No GATE.json in {run}.")
        return 2
    gate = json.loads(gate_file.read_text())
    if gate["status"] != "pending_review":
        print("This run is BLOCKED by validation errors. See 05_validation_report.json.")
        return 3
    if not (unity_project / "Assets").is_dir():
        print(f"{unity_project} does not look like a Unity project (no Assets/ folder).")
        return 2

    src = run / "unity" / "Assets"
    files = [p for p in src.rglob("*") if p.is_file()]
    print(f"About to copy {len(files)} files into {unity_project / 'Assets'}:")
    for p in files[:12]:
        print("   ", p.relative_to(run / "unity"))
    if len(files) > 12:
        print(f"    ... and {len(files) - 12} more")
    if not assume_yes and input("Have you reviewed CHANGES.md and the code? Type 'yes' to promote: ").strip() != "yes":
        print("Not promoted.")
        return 1

    for p in files:
        dest = unity_project / p.relative_to(run / "unity")
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(p, dest)
    log = unity_project / "CHANGES.md"
    with log.open("a") as fh:
        fh.write("\n\n" + (run / "CHANGES.md").read_text())
    gate["status"] = "promoted"
    gate_file.write_text(json.dumps(gate, indent=2))
    print(f"Promoted. Appended to {log}. Open Unity and let it compile.")
    return 0
