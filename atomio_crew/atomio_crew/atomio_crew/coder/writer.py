"""Code-writing step: ask Claude to implement one missing GDD feature.

The Writer gets the requirement (with its evidence contract), the GDD sections
it comes from, a map of the codebase, the full text of the files it may need
to read or change, and samples of the data files it should load. It returns
complete C# files as JSON. Static checks run on every reply before compiling.
"""
from __future__ import annotations

import json
import re
from pathlib import PurePosixPath

from ..content.context import gdd_excerpt, gdd_sections
from ..validators import FORBIDDEN_USINGS, _strip_cs
from .scanner import CodeIndex

SCRIPTS_ROOT = "Assets/Scripts/AtomIO/"
DATA_LIMIT = 12000  # chars of each data file shown to the model

SYSTEM = """You are the Atom.IO Coding Agent, a senior Unity 6.3 (C#) engineer adding ONE missing feature \
to an existing Android game. Output ONLY a JSON object:
{"files": [{"path": "Assets/Scripts/AtomIO/<Folder>/<TypeName>.cs", "content": "<complete C# file>"}],
 "summary": "what you built, 2-3 sentences", "manual_steps": ["anything the developer must do in the editor"]}

Rules:
- Satisfy the EVIDENCE CONTRACT exactly: create every listed type (file name == type name) and member,
  and load every listed Resources path. The agent re-scans the code to verify this.
- Namespace AtomIO (sub-namespaces OK). Unity 6 APIs: Rigidbody2D.linearVelocity,
  Object.FindFirstObjectByType / FindObjectsByType(FindObjectsSortMode.None). Only UnityEngine, UnityEngine.UI,
  UnityEngine.EventSystems, UnityEngine.SceneManagement, TMPro, System, System.Collections(.Generic).
  No networking, no new packages, no editor-only APIs in runtime scripts, no new Input APIs (the project uses
  the new Input System only, so never call UnityEngine.Input).
- Prefer NEW self-contained MonoBehaviours that set themselves up at runtime
  ([RuntimeInitializeOnLoadMethod] + SceneManager.sceneLoaded, finding existing objects such as
  ArenaSession.Instance, the scene Canvas, or LobbyController) so no scene or prefab wiring is needed.
  Build any UI in code (TextMeshProUGUI uses the default TMP font; anchor it so it doesn't cover the HUD or sticks).
- Only modify an existing file if the feature truly needs a hook there, and only files listed under
  MAY MODIFY. Return the COMPLETE modified file, keeping every existing behaviour; additive changes only.
- Data files are loaded with Resources.Load<TextAsset>("path without .json") and JsonUtility (wrap arrays
  in a [Serializable] class whose field names match the JSON keys).
- Respect GDD rules: any electron can be thrown, even the last one (no minimum); no permanent immunity; the game
  ends at Neon (Z=10); zero network calls."""


def _gdd_ids(ref: str) -> list[str]:
    ids = [i for i in re.findall(r"\d+(?:\.\d+)+", ref) if i in gdd_sections()]
    return ids or ["1.3.1"]


def build_messages(req: dict, ix: CodeIndex, all_reqs: dict, feedback: list[str] | None = None,
                   previous: str | None = None) -> list[dict]:
    touch_files = [ix.type_file(t) for t in req.get("touches", []) if ix.type_file(t)]
    dep_types = []
    for d in req.get("depends_on", []):
        ev = all_reqs[d]["evidence"]
        dep_types += ev.get("types", []) + [m.split(".")[0] for m in ev.get("members", [])]
    read_files = [ix.type_file(t) for t in dep_types if ix.type_file(t)]
    seen, context_files = set(), []
    for f in touch_files + read_files:
        if f.path not in seen and len(context_files) < 8:
            seen.add(f.path)
            context_files.append(f)
    files_txt = "\n\n".join(f"// ===== {f.path} =====\n{f.text}" for f in context_files)
    data_txt = ""
    for rel in req.get("data_files", []):
        p = ix.root / "Assets" / "Resources" / rel
        if p.exists():
            # Send the whole data file (these are small). A 1,500-char preview once hid two of the six
            # group abilities from the model, so it never implemented them.
            text = p.read_text(encoding="utf-8")
            cut = len(text) > DATA_LIMIT
            data_txt += (f"\n// Resources/{rel}{' (truncated: ' + str(len(text)) + ' chars total)' if cut else ''}\n"
                         f"{text[:DATA_LIMIT]}\n")
    may_modify = [f.path for f in touch_files]
    user = (f"FEATURE {req['id']}: {req['title']} (GDD {req['gdd_ref']})\n{req['description']}\n"
            f"ACCEPTANCE CRITERIA: {json.dumps(req['acceptance'])}\n"
            f"EVIDENCE CONTRACT: {json.dumps(req['evidence'])}\n"
            f"MAY MODIFY: {may_modify or 'nothing, create new files only'}\n\n"
            f"GDD EXCERPT:\n{gdd_excerpt(_gdd_ids(req['gdd_ref']))}\n\n"
            f"CODEBASE MAP:\n{ix.summary()}\n\nDATA FILES:{data_txt or ' (none)'}\n\n"
            f"RELEVANT FILES (full text):\n{files_txt or '(none)'}\n")
    if feedback:
        user += ("\nYOUR PREVIOUS ATTEMPT FAILED THESE CHECKS. Fix every one and return ALL files again:\n- "
                 + "\n- ".join(feedback[:25]) + (f"\n\nPREVIOUS ATTEMPT:\n{previous[:20000]}" if previous else ""))
    return [{"role": "system", "content": SYSTEM}, {"role": "user", "content": user}]


def parse_json_object(text: str) -> dict:
    if not isinstance(text, str):
        text = str(text)
    m = re.search(r"```(?:json)?\s*(\{.*\})\s*```", text, re.S)
    if m:
        text = m.group(1)
    start, end = text.find("{"), text.rfind("}")
    if start == -1 or end <= start:
        raise ValueError("no JSON object in reply")
    return json.loads(text[start:end + 1])


def parse_reply(text: str) -> dict:
    if not isinstance(text, str):
        text = str(text)
    m = re.search(r"```(?:json)?\s*(\{.*\})\s*```", text, re.S)
    if m:
        text = m.group(1)
    start, end = text.find("{"), text.rfind("}")
    if start == -1 or end <= start:
        raise ValueError("no JSON object in reply")
    data = json.loads(text[start:end + 1])
    if not isinstance(data.get("files"), list) or not data["files"]:
        raise ValueError("reply has no files")
    return data


def static_checks(files: list[dict], req: dict, ix: CodeIndex) -> list[str]:
    """Cheap checks before compiling: paths, file/type names, namespace, banned APIs, allowed edits."""
    errs = []
    allowed_existing = {ix.type_file(t).path for t in req.get("touches", []) if ix.type_file(t)}
    for f in files:
        path, content = f.get("path", ""), f.get("content", "")
        p = PurePosixPath(path)
        if not path.startswith(SCRIPTS_ROOT) or ".." in p.parts or p.suffix != ".cs":
            errs.append(f"{path}: must be a .cs file under {SCRIPTS_ROOT}")
            continue
        if path in ix.files and path not in allowed_existing:
            errs.append(f"{path}: existing file not in MAY MODIFY ({sorted(allowed_existing)})")
        code = _strip_cs(content)
        for a, b in ("{}", "()"):
            if code.count(a) != code.count(b):
                errs.append(f"{path}: unbalanced {a}{b}")
        if not re.search(r"\bnamespace\s+AtomIO\b", code):
            errs.append(f"{path}: must be inside namespace AtomIO")
        if not re.search(rf"\b(class|struct|interface|enum)\s+{re.escape(p.stem)}\b", code):
            errs.append(f"{path}: must declare a type named {p.stem} (Unity needs file name == type name)")
        for u in (*FORBIDDEN_USINGS, "UnityEditor"):
            if re.search(rf"\busing\s+{re.escape(u)}\b", code) or f"{u}." in code:
                errs.append(f"{path}: forbidden API '{u}'")
        if re.search(r"\bInput\.(GetAxis|GetKey|GetMouse|GetTouch|touch)", code):
            errs.append(f"{path}: legacy UnityEngine.Input is disabled in this project (new Input System only)")
        if path in ix.files and len(content) < 0.6 * len(ix.files[path].text):
            errs.append(f"{path}: modified file is much shorter than the original; keep all existing behaviour")
    return errs
