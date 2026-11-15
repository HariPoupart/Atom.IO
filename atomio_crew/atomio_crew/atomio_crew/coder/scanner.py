"""Scan the Unity project's C# scripts and Resources into a searchable index.

Plain code, no LLM: fast, free and repeatable, so the agent can re-scan after
every write. The index records, per file, the types it declares, the members
(methods, properties, fields, events) declared in it, and every Resources.Load
path it uses. It also lists the data files under Assets/Resources.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

SCRIPT_DIRS = ("Assets/Scripts", "Assets/AtomIO")
TYPE_RE = re.compile(r"\b(?:class|struct|interface|enum)\s+([A-Z]\w*)")
# methods / constructors:  [mods] ReturnType Name(   — also matches expression-bodied members
METHOD_RE = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|virtual|override|"
                       r"abstract|sealed|async|new|extern|unsafe)\s+)+[\w<>\[\],.?\s]+?\s+([A-Za-z_]\w*)\s*\(", re.M)
# properties, fields and events:  [mods] Type Name [;={]
MEMBER_RE = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|readonly|const|"
                       r"event|virtual|override|new)\s+)+[\w<>\[\],.?\s]+?\s+([A-Za-z_]\w*)\s*(?:[;={]|=>)", re.M)
LOAD_RE = re.compile(r"Resources\.Load(?:<[^>]+>)?\(\s*(?:\$?\"([^\"]*)\"|([A-Za-z_]\w*))")
CONST_RE = re.compile(r"(?:const\s+string|string)\s+(\w+)\s*=\s*\"([^\"]+)\"")


@dataclass
class FileInfo:
    path: str                     # relative to the Unity project, forward slashes
    types: list[str]
    members: list[str]
    loads: list[str]
    lines: int
    text: str = field(repr=False)


@dataclass
class CodeIndex:
    root: Path
    files: dict[str, FileInfo]
    resources: list[str]          # e.g. "AtomIO/Content/group_abilities.json"

    # ---------------------------------------------------------------- queries
    def type_file(self, type_name: str) -> FileInfo | None:
        for f in self.files.values():
            if type_name in f.types:
                return f
        return None

    def has_type(self, type_name: str) -> bool:
        return self.type_file(type_name) is not None

    def has_member(self, dotted: str) -> bool:
        """'Type.Member': the member is declared in the file that declares Type."""
        type_name, _, member = dotted.partition(".")
        f = self.type_file(type_name)
        return f is not None and member in f.members

    def loads_path(self, prefix: str) -> bool:
        return any(p.startswith(prefix) or prefix in p for f in self.files.values() for p in f.loads)

    def has_resource(self, rel: str) -> bool:
        return rel in self.resources

    def summary(self, max_files: int = 60) -> str:
        """Compact text map of the codebase for prompts."""
        out = []
        for f in list(self.files.values())[:max_files]:
            pub = [m for m in f.members if not m.startswith("_")][:18]
            out.append(f"{f.path} ({f.lines} lines): types {f.types}; members {pub}"
                       + (f"; loads {f.loads}" if f.loads else ""))
        return "\n".join(out)


def _strip_comments(src: str) -> str:
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    return re.sub(r"//[^\n]*", "", src)


def scan(unity_root: Path) -> CodeIndex:
    unity_root = Path(unity_root)
    files: dict[str, FileInfo] = {}
    for d in SCRIPT_DIRS:
        base = unity_root / d
        if not base.exists():
            continue
        for p in sorted(base.rglob("*.cs")):
            text = p.read_text(encoding="utf-8", errors="replace")
            code = _strip_comments(text)
            consts = dict(CONST_RE.findall(code))
            loads = []
            for literal, ident in LOAD_RE.findall(code):
                if literal:
                    loads.append(literal.split("{")[0])
                elif ident in consts:
                    loads.append(consts[ident])
            # string constants that look like Resources paths also count (e.g. Folder + name)
            loads += [v for v in consts.values() if v.startswith("AtomIO/") and v not in loads]
            members = sorted(set(METHOD_RE.findall(code)) | set(MEMBER_RE.findall(code)))
            rel = p.relative_to(unity_root).as_posix()
            files[rel] = FileInfo(rel, TYPE_RE.findall(code), members, loads, text.count("\n") + 1, text)
    res_root = unity_root / "Assets" / "Resources"
    resources = sorted(p.relative_to(res_root).as_posix() for p in res_root.rglob("*")
                       if p.is_file() and p.suffix != ".meta") if res_root.exists() else []
    return CodeIndex(unity_root, files, resources)
