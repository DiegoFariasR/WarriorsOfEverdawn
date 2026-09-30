"""
Shared file-tree scanner skeleton for Tools/check_*.py family.

Copied from Everdawn. Consumers today: audit_refactors.py and check_docs.py,
which pull in `dev_sh_subcommands` and `filename_index`. `scan_files` and
`format_report` are there for the next `*.cs` / `*.md` check.

Public surface:
    Finding                                -- dataclass for a single hit.
    parse_common_args(argv, doc)           -- handle --help / -v.
    scan_files(scan_dir, scan_fn, ...)     -- walk + collect findings.
    format_report(tool_name, findings, ...) -- print results, return exit code.
    dev_sh_subcommands()                   -- parse dev.sh subcommand names.
    filename_index()                       -- bare filename -> repo path map.
    INDEX_SKIP_DIRS                        -- dir-name skip set for the index.
"""
from __future__ import annotations

import argparse
import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Iterable, Optional, Sequence

ROOT = Path(__file__).resolve().parent.parent


# Directories excluded from bare-filename resolution (build artefacts, third-party).
# Used by `filename_index()`; shared between audit_refactors.py and check_docs.py.
INDEX_SKIP_DIRS = {".git", ".godot", "bin", "obj", "build", "node_modules", "_staging"}


_filename_index_cache: Optional[dict[str, Path]] = None


def filename_index() -> dict[str, Path]:
    """Map of bare filename -> one resolved repo path, built once on first call.

    Walks ROOT once, skipping `INDEX_SKIP_DIRS`. Multiple files with the same
    name resolve to the first one encountered (depth-first lexical order).
    Cached for the lifetime of the process; survives across consumers when
    imported in the same Python process.
    """
    global _filename_index_cache
    if _filename_index_cache is not None:
        return _filename_index_cache
    idx: dict[str, Path] = {}
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in INDEX_SKIP_DIRS]
        for fn in filenames:
            idx.setdefault(fn, Path(dirpath) / fn)
    _filename_index_cache = idx
    return idx


_dev_sh_subcommands_cache: Optional[set[str]] = None


def dev_sh_subcommands() -> set[str]:
    """Parse `dev.sh` and return the set of declared subcommand names.

    Shared between check_docs.py and audit_refactors.py (which both verify
    that backticked `./dev.sh foo` references resolve to a real subcommand).
    Result is cached for the lifetime of the process.
    """
    global _dev_sh_subcommands_cache
    if _dev_sh_subcommands_cache is not None:
        return _dev_sh_subcommands_cache
    dev_sh = ROOT / "dev.sh"
    cmds: set[str] = set()
    if dev_sh.exists():
        with open(dev_sh, encoding="utf-8") as f:
            for raw in f:
                # Case arms may list `|`-separated alternatives (e.g. `help|-h|--help)`);
                # split and keep the subcommand-shaped tokens (flag aliases like -h are skipped).
                m = re.match(r"\s*([a-z][a-z0-9-]*(?:\s*\|\s*[A-Za-z0-9_.-]+)*)\)(?:\s|$)", raw)
                if m:
                    for part in m.group(1).split("|"):
                        part = part.strip()
                        if re.fullmatch(r"[a-z][a-z0-9-]*", part):
                            cmds.add(part)
    _dev_sh_subcommands_cache = cmds
    return cmds


@dataclass
class Finding:
    """One scanner hit. `extra` is opaque per-tool payload (kind, expected path, snippet)."""
    file: Path                     # absolute path to the source file
    lineno: int
    message: str                   # short, single-line; tool-specific
    extra: dict[str, Any] = field(default_factory=dict)

    @property
    def rel(self) -> str:
        try:
            return self.file.relative_to(ROOT).as_posix()
        except ValueError:
            return self.file.as_posix()


# A per-file callback. Takes the file Path; returns the list of findings (with
# `file` left unset -- the harness fills it in). Callbacks may also return a
# pre-populated Finding (with .file set); both shapes accepted.
ScanFn = Callable[[Path], list[Finding]]


def parse_common_args(argv: Sequence[str], doc: Optional[str]) -> argparse.Namespace:
    """Handle the common `--help` / `--verbose|-v` short-circuits.

    Returns an argparse.Namespace with at least `.verbose: bool`. Callers can
    extend this by re-parsing argv with their own argparse if they need more
    flags -- this is just the floor every check_*.py shares.
    """
    parser = argparse.ArgumentParser(
        description=(doc.strip() if doc else None),
        formatter_class=argparse.RawDescriptionHelpFormatter,
        add_help=True,
    )
    parser.add_argument("-v", "--verbose", action="store_true",
                        help="Print per-file scan counts in the summary.")
    parser.add_argument("extra", nargs=argparse.REMAINDER,
                        help=argparse.SUPPRESS)
    return parser.parse_args(list(argv))


def scan_files(scan_dir: Path, scan_fn: ScanFn, *,
               extensions: Iterable[str] = ("*.cs",),
               exclude_path_substrings: Optional[Iterable[str]] = None,
               exclude_suffixes: Optional[Iterable[str]] = None) -> tuple[list[Finding], int]:
    """Walk scan_dir for files matching any glob in `extensions`, apply scan_fn,
    collect findings.

    Args:
        scan_dir: directory to recurse (typically GodotClient/scripts or Core).
        scan_fn: per-file callback returning list of Findings. The harness sets
            `Finding.file` on any returned Finding whose file is missing.
        extensions: glob patterns passed to `rglob` (e.g. ("*.cs",), ("*.md",),
            ("*.yml", "*.yaml")). Defaults to ("*.cs",).
        exclude_path_substrings: skip any file whose POSIX relpath contains
            one of these (e.g. "/test/").
        exclude_suffixes: skip any file whose relpath ends with one of these
            (e.g. "Tests.cs").

    Returns: (findings, files_scanned).
    """
    findings: list[Finding] = []
    files_scanned = 0
    excl_subs = tuple(exclude_path_substrings or ())
    excl_suffixes = tuple(exclude_suffixes or ())

    matches: set[Path] = set()
    for ext in extensions:
        matches.update(scan_dir.rglob(ext))

    for src in sorted(matches):
        rel = src.relative_to(ROOT).as_posix() if src.is_relative_to(ROOT) else src.as_posix()
        if excl_suffixes and rel.endswith(excl_suffixes):
            continue
        if excl_subs and any(sub in rel for sub in excl_subs):
            continue
        files_scanned += 1
        try:
            hits = scan_fn(src)
        except OSError:
            continue
        for h in hits:
            if h.file is None or not str(h.file):
                h.file = src
            findings.append(h)
    return findings, files_scanned


def format_report(tool_name: str, findings: Sequence[Finding], files_scanned: int,
                  *, verbose: bool = False, clean_suffix: str = "",
                  failure_hint: Optional[str] = None,
                  render_finding: Optional[Callable[[Finding], list[str]]] = None) -> int:
    """Print findings + summary; return the exit code.

    Group findings by file, print a per-file header (`<rel>\\n<dashes>`) and one
    line per finding via `render_finding` (default `"<rel>:<lineno>  <message>"`).

    Returns 0 if no findings, 1 otherwise.
    """
    if not findings:
        suffix = f" ({files_scanned} file(s) scanned)" if verbose and not clean_suffix else clean_suffix
        print(f"[{tool_name}] clean{suffix}")
        return 0

    grouped: dict[str, list[Finding]] = {}
    for f in findings:
        grouped.setdefault(f.rel, []).append(f)

    def default_render(f: Finding) -> list[str]:
        return [f"  {f.rel}:{f.lineno}  {f.message}"]

    render = render_finding or default_render
    for rel, hits in grouped.items():
        print(f"\n{rel}")
        print("-" * len(rel))
        for h in hits:
            for line in render(h):
                print(line)

    print(f"\n[{tool_name}] {len(findings)} finding(s) across {len(grouped)} file(s)")
    if failure_hint:
        print(f"  {failure_hint}")
    return 1
