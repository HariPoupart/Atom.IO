"""Token budget from GDD 3.2: per-agent caps and a hard dollar ceiling."""
from __future__ import annotations

import os

# GDD 3.2 projections, used as hard caps per agent.
AGENT_TOKEN_CAPS = {
    "asset_scraper": 45_000,
    "lead_architect": 480_000,   # core (240k) + bonus features (240k)
    "level_designer": 885_000,
}
TOTAL_TOKEN_CAP = 1_410_000
USD_CEILING = float(os.getenv("ATOMIO_USD_CEILING", "10"))

# Per-million-token prices; override in .env to match the model you pick.
PRICE_IN = float(os.getenv("ATOMIO_PRICE_IN_PER_MTOK", "3.0"))
PRICE_OUT = float(os.getenv("ATOMIO_PRICE_OUT_PER_MTOK", "15.0"))

# Max output tokens per single call, per agent.
MAX_OUTPUT_TOKENS = {
    "asset_scraper": 4_000,
    "lead_architect": 32_000,    # a full code bundle in one response
    "level_designer": 12_000,
}


class BudgetExceeded(RuntimeError):
    pass


def usage_of(llm) -> dict:
    """Normalise token usage from a real CrewAI LLM or the MockLLM."""
    if hasattr(llm, "usage"):
        u = llm.usage()
        return {"prompt_tokens": u["prompt_tokens"], "completion_tokens": u["completion_tokens"],
                "calls": u["calls"]}
    s = llm.get_token_usage_summary()
    return {"prompt_tokens": getattr(s, "prompt_tokens", 0) or 0,
            "completion_tokens": getattr(s, "completion_tokens", 0) or 0,
            "calls": getattr(s, "successful_requests", 0) or 0}


def cost_usd(u: dict) -> float:
    return u["prompt_tokens"] / 1e6 * PRICE_IN + u["completion_tokens"] / 1e6 * PRICE_OUT


def check(llms: dict) -> dict:
    """Raise BudgetExceeded if any agent or the crew is over its cap. Returns the usage report."""
    report, total_tok, total_usd = {}, 0, 0.0
    for key, llm in llms.items():
        u = usage_of(llm)
        tok = u["prompt_tokens"] + u["completion_tokens"]
        report[key] = {**u, "total_tokens": tok, "cap": AGENT_TOKEN_CAPS[key], "usd": round(cost_usd(u), 4)}
        total_tok += tok
        total_usd += cost_usd(u)
        if tok > AGENT_TOKEN_CAPS[key]:
            raise BudgetExceeded(f"{key} used {tok:,} tokens, cap is {AGENT_TOKEN_CAPS[key]:,}")
    report["total"] = {"total_tokens": total_tok, "cap": TOTAL_TOKEN_CAP, "usd": round(total_usd, 4),
                       "usd_ceiling": USD_CEILING}
    if total_tok > TOTAL_TOKEN_CAP or total_usd > USD_CEILING:
        raise BudgetExceeded(f"crew used {total_tok:,} tokens / ${total_usd:.2f}; "
                             f"caps are {TOTAL_TOKEN_CAP:,} / ${USD_CEILING:.2f}")
    return report
