"""Assemble the Atom.IO crew: 3 agents, 5 tasks, sequential process."""
from __future__ import annotations

import os
from pathlib import Path

import yaml
from crewai import LLM, Agent, Crew, Process, Task

from . import budget, gdd_rules
from .schemas import ArchitectReview, AssetManifest, CodeBundle, TierDesignSet, ValidationReport
from .tools import (STATE, element_groups, free_asset_catalog, gdd_formula_table, license_check,
                    optional_web_search_tools, run_validation_suite,
                    tier_json_contract)

ROOT = Path(__file__).resolve().parent
CONFIG = ROOT / "config"
GDD_PATH = ROOT.parent / "data" / "gdd_atomio.md"
UNITY_VERSION = "6.3 LTS"

# Model tier per agent; real IDs are resolved by models.pick() (see models.py).
DEFAULT_MODELS = {
    "asset_scraper": "haiku",
    "lead_architect": "sonnet",
    "level_designer": "sonnet",
}


# --------------------------------------------------------------------------- task callbacks
# Module-level functions (CrewAI can checkpoint these). Each stores the task's
# Pydantic output in the shared RunState and enforces the GDD 3.2 token caps.
_ACTIVE_LLMS: dict = {}


def _store(attr: str, output) -> None:
    setattr(STATE, attr, output.pydantic)
    budget.check(_ACTIVE_LLMS)  # abort early if an agent blew through its cap


def on_manifest(output) -> None:
    _store("manifest", output)


def on_review(output) -> None:
    _store("review", output)


def on_design(output) -> None:
    STATE.tier_files = None  # force re-expansion with the new design
    _store("design", output)


def on_code(output) -> None:
    _store("code", output)


def on_validation(output) -> None:
    _store("validation_report", output)


def make_llms(mock: bool) -> dict:
    """One LLM instance per agent so token usage (and caps) are tracked per agent."""
    llms = {}
    for key, default in DEFAULT_MODELS.items():
        if mock:
            from .mock_llm import MockLLM
            llms[key] = MockLLM(model=f"mock/{key}")
        else:
            from .models import pick
            model = pick(default, f"ATOMIO_MODEL_{key.upper()}")
            llms[key] = LLM(model=model, temperature=0.4 if key == "level_designer" else 0.2,
                            max_tokens=budget.MAX_OUTPUT_TOKENS[key])
    return llms


def build_crew(llms: dict, verbose: bool = False, without: str | None = None) -> tuple[Crew, dict]:
    """Build the crew. ``without`` drops one agent and its tasks, to demonstrate
    that every agent is load-bearing (the run then fails validation)."""
    agents_cfg = yaml.safe_load((CONFIG / "agents.yaml").read_text())
    tasks_cfg = yaml.safe_load((CONFIG / "tasks.yaml").read_text())

    tools_for = {
        "asset_scraper": [free_asset_catalog, license_check, *optional_web_search_tools()],
        "lead_architect": [gdd_formula_table],
        "level_designer": [element_groups, gdd_formula_table],
    }
    max_iter = {"asset_scraper": 8, "lead_architect": 5, "level_designer": 5}

    _ACTIVE_LLMS.clear()
    _ACTIVE_LLMS.update(llms)

    if without:
        agents_cfg = {k: v for k, v in agents_cfg.items() if k != without}
        tasks_cfg = {k: v for k, v in tasks_cfg.items() if v["agent"] != without}

    agents = {
        key: Agent(**agents_cfg[key], llm=llms[key], tools=tools_for[key], max_iter=max_iter[key],
                   max_rpm=20, allow_delegation=False, verbose=verbose)
        for key in agents_cfg
    }

    outputs = {
        "scout_assets": (AssetManifest, on_manifest, []),
        "review_assets_and_plan": (ArchitectReview, on_review, []),
        "design_tiers": (TierDesignSet, on_design, []),
        "implement_core_scripts": (CodeBundle, on_code, [tier_json_contract, gdd_formula_table]),
        "validate_outputs": (ValidationReport, on_validation, [run_validation_suite]),
    }

    tasks: dict[str, Task] = {}
    for name, cfg in tasks_cfg.items():
        model, callback, extra_tools = outputs[name]
        tasks[name] = Task(
            name=name,
            description=cfg["description"],
            expected_output=cfg["expected_output"],
            agent=agents[cfg["agent"]],
            context=[tasks[c] for c in cfg.get("context", []) if c in tasks],
            output_pydantic=model,
            callback=callback,
            **({"tools": extra_tools} if extra_tools else {}),
        )

    crew = Crew(agents=list(agents.values()), tasks=list(tasks.values()),
                process=Process.sequential, verbose=verbose)
    return crew, tasks


def crew_inputs() -> dict:
    return {
        "gdd": GDD_PATH.read_text().strip(),
        "unity_version": UNITY_VERSION,
        "group_count": len(gdd_rules.GROUPS),
    }
