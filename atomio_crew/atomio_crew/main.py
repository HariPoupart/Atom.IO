#!/usr/bin/env python3
"""Atom.IO agent crew: command-line entry point.

    python main.py run --mock          # offline, no API key needed
    python main.py run                 # real run with Claude (needs ANTHROPIC_API_KEY)
    python main.py run --mock --without level_designer   # prove each agent is required
    python main.py generate --mock     # Assignment 4: content pipeline (Writer + Critic loop)
    python main.py validate            # re-run the validation suite on output/latest
    python main.py promote --unity-project ~/Unity/AtomIO   # manual gate -> copy into Unity
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
os.environ.setdefault("CREWAI_TRACING_ENABLED", "false")
os.environ.setdefault("OTEL_SDK_DISABLED", "true")
os.environ.setdefault("CREWAI_DISABLE_TELEMETRY", "true")


def _use_project_venv() -> None:
    """If started with a Python that isn't a virtual environment (you forgot to
    activate .venv), re-run this command with the project's .venv Python so the
    installed packages are always found."""
    if sys.prefix != sys.base_prefix or os.environ.get("ATOMIO_VENV_RELAUNCHED"):
        return  # already inside a virtual environment
    venv_python = ROOT / ".venv" / ("Scripts/python.exe" if os.name == "nt" else "bin/python")
    if venv_python.exists():
        import subprocess
        os.environ["ATOMIO_VENV_RELAUNCHED"] = "1"
        sys.exit(subprocess.call([str(venv_python), str(Path(__file__).resolve()), *sys.argv[1:]]))


_use_project_venv()


def _load_dotenv() -> None:
    env = ROOT / ".env"
    if not env.exists():
        return
    for line in env.read_text().splitlines():
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            k, v = line.split("=", 1)
            os.environ.setdefault(k.strip(), v.strip().strip('"').strip("'"))


def cmd_run(args) -> int:
    from atomio_crew import budget
    from atomio_crew.crew import build_crew, crew_inputs, make_llms
    from atomio_crew.postprocess import write_run

    mock = args.mock or os.getenv("ATOMIO_MOCK", "").lower() in ("1", "true", "yes")
    if not mock and not os.getenv("ANTHROPIC_API_KEY"):
        print("ANTHROPIC_API_KEY is not set. Add it to .env, or run with --mock for an offline demo.")
        return 2

    mode = "mock" if mock else "live"
    print(f"== Atom.IO crew ({mode}) : Asset Scraper -> Lead Architect -> Level Designer -> Lead Architect ==")
    llms = make_llms(mock)
    if args.without:
        print(f"!! Removing agent '{args.without}' to show the pipeline depends on it.")
    crew, _ = build_crew(llms, verbose=args.verbose, without=args.without)

    status = "completed"
    try:
        crew.kickoff(inputs=crew_inputs())
    except budget.BudgetExceeded as e:
        status = f"aborted: budget exceeded ({e})"
    except Exception as e:  # keep partial results instead of crashing
        status = f"aborted: {type(e).__name__}: {e}"

    try:
        usage = budget.check(llms)
    except budget.BudgetExceeded as e:
        usage = {"warning": str(e)}
    run_dir, gate = write_run(Path(args.out), usage, mode)

    print(f"\nCrew {status}.")
    print(f"Output: {run_dir}")
    print(f"Validation: {'PASSED' if gate['passed'] else 'FAILED'} "
          f"({len(gate['errors'])} errors, {len(gate['warnings'])} warnings)")
    for e in gate["errors"][:10]:
        print("  ERROR  ", e)
    for w in gate["warnings"][:5]:
        print("  warning", w)
    if "total" in usage:
        t = usage["total"]
        print(f"Tokens: {t['total_tokens']:,} / {t['cap']:,} cap   cost ~${t['usd']:.3f} / ${t['usd_ceiling']:.0f} ceiling")
    print("Next: review CHANGES.md, then `python main.py promote --unity-project <path>`.")
    return 0 if status == "completed" and gate["passed"] else 1


def cmd_validate(args) -> int:
    from atomio_crew import validators
    from atomio_crew.schemas import ArchitectReview, AssetManifest, CodeBundle, CodeFile, TierDesignSet
    from atomio_crew.tiers import build_all

    run = Path(args.run)
    m = AssetManifest.model_validate_json((run / "01_asset_manifest.json").read_text())
    r = ArchitectReview.model_validate_json((run / "02_architect_review.json").read_text())
    d = TierDesignSet.model_validate_json((run / "03_tier_design_set.json").read_text())
    files, _ = build_all(d)
    code_root = run / "unity"
    code = CodeBundle(files=[CodeFile(path=p.relative_to(code_root).as_posix(), content=p.read_text())
                             for p in sorted(code_root.rglob("*.cs"))], changes_md="")
    res = validators.run_all(manifest=m, review=r, design=d, tier_files=files, code=code)
    print(json.dumps(res, indent=2))
    return 0 if res["passed"] else 1


def cmd_promote(args) -> int:
    from atomio_crew.postprocess import promote
    return promote(Path(args.run).resolve(), Path(args.unity_project).expanduser(), args.yes)


def cmd_generate(args) -> int:
    """Assignment 4: GDD-grounded content pipeline (Writer -> Critic -> revise loop)."""
    from atomio_crew.content.pipeline import run as run_content
    from atomio_crew.content.specs import SPECS

    mock = args.mock or os.getenv("ATOMIO_MOCK", "").lower() in ("1", "true", "yes")
    if not mock and not os.getenv("ANTHROPIC_API_KEY"):
        print("ANTHROPIC_API_KEY is not set. Add it to .env, or run with --mock for an offline demo.")
        return 2
    keys = args.only or list(SPECS)
    print(f"== Atom.IO content pipeline ({'mock' if mock else 'live'}): Writer -> Critic -> revise, "
          f"max {args.max_rounds} rounds ==")
    res = run_content(keys, mock, args.max_rounds, Path(args.out), Path(args.content_dir))

    print(f"\nPipeline {res['status']}.")
    for r in res["results"]:
        caught = sum(len(h["issues"]) for h in r["history"])
        print(f"  {SPECS[r['key']].title:<30} {r['status'].upper():<13} rounds={r['rounds']}  "
              f"critic caught {caught} issue(s)")
    u = res["usage"]
    print(f"Tokens: {u['total_tokens']:,}  cost ~${u['usd']:.3f}")
    print(f"Content: {Path(args.content_dir)}   (read CRITIC_REPORT.md first)")
    ok = res["status"] == "completed" and all(r["status"] == "approved" for r in res["results"])
    return 0 if ok else 1


def main() -> int:
    _load_dotenv()
    p = argparse.ArgumentParser(description="Atom.IO multi-agent content crew (CrewAI)")
    sub = p.add_subparsers(dest="cmd", required=True)

    r = sub.add_parser("run", help="run the crew")
    r.add_argument("--mock", action="store_true", help="offline mock LLM, no API key needed")
    r.add_argument("--out", default=str(ROOT / "output"))
    r.add_argument("--verbose", action="store_true", help="show CrewAI agent logs")
    r.add_argument("--without", choices=["asset_scraper", "lead_architect", "level_designer"],
                   help="drop one agent to demonstrate the pipeline breaks without it")
    r.set_defaults(func=cmd_run)

    v = sub.add_parser("validate", help="re-validate a run folder")
    v.add_argument("--run", default=str(ROOT / "output" / "latest"))
    v.set_defaults(func=cmd_validate)

    g = sub.add_parser("promote", help="manual gate: copy a validated run into a Unity project")
    g.add_argument("--unity-project", required=True)
    g.add_argument("--run", default=str(ROOT / "output" / "latest"))
    g.add_argument("--yes", action="store_true", help="skip the confirmation prompt")
    g.set_defaults(func=cmd_promote)

    c = sub.add_parser("generate", help="Assignment 4: generate GDD-grounded content with a critic loop")
    c.add_argument("--mock", action="store_true", help="offline mock LLM, no API key needed")
    c.add_argument("--only", nargs="+", choices=["element_cards", "ui_text", "group_abilities"],
                   help="generate only these content types")
    c.add_argument("--max-rounds", type=int, default=3, help="max Writer/Critic rounds per content type")
    c.add_argument("--out", default=str(ROOT / "output"))
    c.add_argument("--content-dir", default=str(ROOT / "content"))
    c.set_defaults(func=cmd_generate)

    args = p.parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
