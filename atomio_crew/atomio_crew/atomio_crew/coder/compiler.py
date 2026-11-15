"""Compile-check generated C# before it is written into the Unity project.

Backends, best first:
  1. unity  : Unity's own Roslyn compiler (shipped with the editor) against the
              project's real references, read from Assembly-CSharp.csproj. This
              is the same compiler Unity uses, so a pass here means Unity compiles it.
  2. mcsref : Mono's mcs against the same real Unity DLLs (netstandard + UnityEngine
              modules + the project's package assemblies) when Mono is installed.
  3. mcs    : Mono's mcs against the API stubs in compile_check/UnityStubs.cs.
  4. none   : no compiler found; reported as a warning, never silently "passed".
A backend is only trusted if the UNCHANGED project compiles with it first
(baseline). Runtime scripts only (Assets/Scripts); Editor scripts are skipped.
"""
from __future__ import annotations

import os
import re
import shutil
import subprocess
import tempfile
from pathlib import Path

STUBS = Path(__file__).resolve().parents[1] / "compile_check" / "UnityStubs.cs"
ERR_RE = re.compile(r"(?:^|[\\/])([^\\/(]+\.cs)\((\d+),\d+\): (error|warning) (CS\d+): (.*)")


def _editor_data_dir(unity_root: Path, csproj_text: str) -> Path | None:
    env = os.getenv("ATOMIO_UNITY_EDITOR")  # e.g. C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor
    cands = [Path(env) / "Data"] if env else []
    m = re.search(r"<HintPath>(.*?)[\\/]Data[\\/]Managed[\\/]UnityEngine[\\/]UnityEngine\.dll</HintPath>", csproj_text)
    if m:
        cands.append(Path(m.group(1)) / "Data")
    ver = (unity_root / "ProjectSettings" / "ProjectVersion.txt")
    if ver.exists():
        v = re.search(r"m_EditorVersion: (\S+)", ver.read_text()).group(1)
        cands += [Path(rf"C:\Program Files\Unity\Hub\Editor\{v}\Editor\Data"),
                  Path(f"/Applications/Unity/Hub/Editor/{v}/Unity.app/Contents")]
    for c in cands:
        if (c / "DotNetSdkRoslyn" / "csc.dll").exists():
            return c
    return None


class Compiler:
    def __init__(self, unity_root: Path, refs_root: Path | None = None):
        self.root = Path(unity_root)
        refs_base = Path(refs_root) if refs_root else self.root  # sandbox: references live in the real project
        self.backend = "none"
        self.note = ""
        self._unity = None
        self._mcsref: list[str] = []
        csproj = self.root / "Assembly-CSharp.csproj"
        if csproj.exists():
            text = csproj.read_text(encoding="utf-8", errors="replace")
            data = _editor_data_dir(refs_base, text)
            if data:
                dotnet = data / "NetCoreRuntime" / ("dotnet.exe" if os.name == "nt" else "dotnet")
                refs = []
                for hp in re.findall(r"<HintPath>(.*?)</HintPath>", text):
                    p = Path(hp) if Path(hp).is_absolute() else refs_base / hp
                    if p.name.startswith("Assembly-CSharp") or not p.exists():
                        continue  # never reference the project's own output (duplicate types)
                    refs.append(str(p))
                defines = (re.search(r"<DefineConstants>(.*?)</DefineConstants>", text) or [None, ""])[1]
                lang = (re.search(r"<LangVersion>(.*?)</LangVersion>", text) or [None, "9.0"])[1]
                ns = data / "NetStandard" / "ref" / "2.1.0" / "netstandard.dll"
                if ns.exists():
                    keep = [r for r in refs if ("Managed" in r and "UnityEngine" in r) or
                            ("ScriptAssemblies" in r and "Editor" not in Path(r).name and "Test" not in Path(r).name)]
                    self._mcsref = [str(ns)] + keep
                if dotnet.exists() and refs:
                    self._unity = {"dotnet": str(dotnet), "csc": str(data / "DotNetSdkRoslyn" / "csc.dll"),
                                   "refs": refs, "defines": defines, "lang": lang}
        has_mcs = bool(shutil.which("mcs"))
        order = ((["unity"] if self._unity else []) + (["mcsref"] if has_mcs and self._mcsref else [])
                 + (["mcs"] if has_mcs else []))
        for b in order:  # trust a backend only if the unchanged project compiles with it
            errs, ok = self._run(b, self.project_sources({}))
            if ok and not errs:
                self.backend = b
                break
            self.note += (f"{b}: unchanged project gave {len(errs)} errors; " if errs else f"{b}: could not run; ")
        if self.backend == "none":
            self.note += "no usable C# compiler: install Mono (mcs) or set ATOMIO_UNITY_EDITOR"

    def project_sources(self, changes: dict[str, str]) -> dict[str, str]:
        """Runtime scripts of the project with the proposed changes applied."""
        srcs = {}
        base = self.root / "Assets" / "Scripts"
        for p in base.rglob("*.cs"):
            srcs[p.relative_to(self.root).as_posix()] = p.read_text(encoding="utf-8", errors="replace")
        for path, content in changes.items():
            if "/Editor/" not in path:
                srcs[path] = content
        return srcs

    def _run(self, backend: str, srcs: dict[str, str]) -> tuple[list[str], bool]:
        with tempfile.TemporaryDirectory() as tmp:
            names = {}
            files = []
            for i, (rel, content) in enumerate(srcs.items()):
                p = Path(tmp) / f"{i:03d}_{Path(rel).name}"
                p.write_text(content, encoding="utf-8")
                names[p.name] = rel
                files.append(str(p))
            out = str(Path(tmp) / "check.dll")
            if backend == "unity":
                u = self._unity
                rsp = Path(tmp) / "args.rsp"
                lines = ["-nologo", "-target:library", "-nostdlib+", "-nowarn:0169,0414,0649",
                         f"-langversion:{u['lang']}", f"-define:{u['defines']}", f"-out:{out}"]
                lines += [f'-r:"{r}"' for r in u["refs"]] + [f'"{f}"' for f in files]
                rsp.write_text("\n".join(lines), encoding="utf-8")
                cmd = [u["dotnet"], u["csc"], "-noconfig", f"@{rsp}"]
            elif backend == "mcsref":
                cmd = ["mcs", "-target:library", "-nologo", "-nostdlib", "-noconfig", "-langversion:latest",
                       "-nowarn:0618,0649,0414,0169", f"-out:{out}", *[f"-r:{r}" for r in self._mcsref], *files]
            else:
                cmd = ["mcs", "-target:library", "-nologo", "-nowarn:0618,0649,0414,0169", f"-out:{out}",
                       str(STUBS), *files]
            try:
                proc = subprocess.run(cmd, capture_output=True, text=True, timeout=240)
            except (OSError, subprocess.TimeoutExpired) as e:
                return [f"compiler could not run: {e}"], False
            errors = []
            for line in (proc.stdout + proc.stderr).splitlines():
                m = ERR_RE.search(line)
                if m and m.group(3) == "error":
                    errors.append(f"{names.get(m.group(1), m.group(1))}:{m.group(2)} {m.group(4)}: {m.group(5)}")
            ran = proc.returncode == 0 or bool(errors)
            return errors, ran

    def check(self, changes: dict[str, str]) -> dict:
        if self.backend == "none":
            return {"backend": "none", "ok": True, "errors": [], "warning": self.note}
        errors, ran = self._run(self.backend, self.project_sources(changes))
        warnings = []
        if self.backend == "mcs":  # stubs cover a subset of Unity: unknown Unity APIs are only warnings
            stub_gap = [e for e in errors if re.search(r"CS0246|CS0234", e) or
                        (re.search(r"CS0117|CS1061", e) and ("UnityEngine" in e or "TMPro" in e))]
            errors = [e for e in errors if e not in stub_gap]
            warnings = [f"{e} (API missing from stubs: confirm in Unity)" for e in stub_gap]
        return {"backend": self.backend, "ok": ran and not errors, "errors": errors[:25],
                "warning": "; ".join(warnings[:10]) if ran else "compiler failed to run"}
