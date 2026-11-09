"""Pick Claude model IDs that actually exist for your API key.

Defaults are the current models. If a default isn't available to your
account (models get renamed/retired), we ask the Models API which models you
can use and pick the newest one of the same tier (sonnet / haiku), so a stale
name never stops a run. Any ATOMIO_MODEL* value in .env always wins.
"""
from __future__ import annotations

import os
from functools import lru_cache

DEFAULTS = {"sonnet": "claude-sonnet-5-5", "haiku": "claude-haiku-5-5"}


@lru_cache(maxsize=1)
def _available() -> tuple[str, ...] | None:
    try:
        import anthropic
        return tuple(m.id for m in anthropic.Anthropic().models.list(limit=100))  # newest first
    except Exception:
        return None  # offline / no key / old SDK: just use the defaults


def _prefixed(model: str) -> str:
    return model if "/" in model else f"anthropic/{model}"


def pick(tier: str, *env_names: str) -> str:
    for name in (*env_names, "ATOMIO_MODEL"):
        if os.getenv(name):
            return _prefixed(os.environ[name].strip())
    model = DEFAULTS[tier]
    avail = _available()
    if avail and model not in avail:
        same_tier = [m for m in avail if tier in m]
        if same_tier:
            print(f"[models] {model} isn't available to this key, using {same_tier[0]}")
            model = same_tier[0]
    return _prefixed(model)
