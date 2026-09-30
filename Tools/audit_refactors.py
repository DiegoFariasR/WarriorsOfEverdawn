#!/usr/bin/env python
"""
Pending-refactors staleness gate.

Parses `Docs/Design/Refactor/pending-refactors.md`, extracts each numbered
refactor item, and re-verifies the references it claims. Flags items whose
file paths no longer resolve, whose symbol references no longer grep anywhere,
or whose optional `**Verify:**` command now exits 0 (a zero exit is a signal
that the issue the refactor described may already be resolved).

Scope, as with check_docs: high-confidence only. A "maybe resolved" signal is
just that -- a prompt for a human to re-read the entry, not an auto-delete.

Usage:
    python Tools/audit_refactors.py [--verbose]

Exit codes:
    0 -- every item's refs still resolve and no Verify command passed
    1 -- at least one item has stale refs or a passing Verify command
    2 -- tool error (file missing, unreadable)
"""
from __future__ import annotations

import os
import pathlib
import re
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _check_harness import make_argparser  # noqa: E402
from _file_scanner import INDEX_SKIP_DIRS, dev_sh_subcommands, filename_index  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
REFACTORS = ROOT / "Docs" / "Design" / "Refactor" / "pending-refactors.md"

CODE_EXTS = {".cs", ".py", ".sh", ".yml", ".yaml", ".json", ".md", ".tscn", ".gd"}

# Matches a new refactor item header: `## 12. Title here`
ITEM_HEADER = re.compile(r"^##\s+(\d+)\.\s+(.+?)\s*$")

# Matches a section heading inside an item: `## Not included (...)`, `## Landed ...`
NON_ITEM_HEADER = re.compile(r"^##\s+(?!\d+\.)")

# Inline backticked tokens: path/file, symbol, method(...).
BACKTICK_REF = re.compile(r"`([^`\n]{1,120})`")

# Optional metadata lines inside an item.
VERIFY_LINE = re.compile(r"^\*\*Verify:\*\*\s+(.+?)\s*$")
# Skip marker on the preceding line applies to the next non-blank line's refs.
# Same convention as Tools/check_docs.py.
SKIP_MARKER = re.compile(r"<!--\s*docs-lint-skip\s*-->")

# Grepable-symbol predicate: C# identifier or dotted chain, optionally ending (...).
SYMBOL = re.compile(r"^[A-Z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*(?:\(\))?$")

# Command-shape refs that should resolve against the corresponding tool registry
# instead of the filesystem (`./dev.sh foo` ≠ a file path; `python Tools/x.py 3` is
# a script invocation with args). check_docs.py validates dev.sh subcommands the
# same way via DEV_SH_CMD; mirror its shape here.
DEV_SH_CMD = re.compile(r"^\./dev\.sh\s+([a-z][a-z0-9-]*)")
PYTHON_SCRIPT = re.compile(r"^python\s+([A-Za-z0-9_./\\-]+\.py)\b")
DOTNET_CMD = re.compile(r"^dotnet\s+")


def resolve_path(ref: str) -> pathlib.Path | None:
    p = pathlib.Path(ref)
    candidate = p if p.is_absolute() else ROOT / p
    if candidate.exists():
        return candidate
    if "/" not in ref and "\\" not in ref:
        return filename_index().get(ref)
    return None


def _bare_identifier(symbol: str) -> str:
    return symbol.split(".")[-1].removesuffix("()")


_symbol_hits_cache: dict[str, bool] | None = None


def _python_walk_symbol_hits(bares: set[str]) -> dict[str, bool]:
    """Fallback when rg is unavailable: walk the tree once, test all patterns per file."""
    patterns = {b: re.compile(rf"\b{re.escape(b)}\b") for b in bares}
    hits: dict[str, bool] = {b: False for b in bares}
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if d not in INDEX_SKIP_DIRS and d not in {"Docs"}]
        for fn in filenames:
            if not fn.endswith((".cs", ".py", ".sh")):
                continue
            try:
                with open(pathlib.Path(dirpath) / fn, encoding="utf-8", errors="ignore") as f:
                    content = f.read()
            except OSError:
                continue
            for b, pat in patterns.items():
                if hits[b]:
                    continue
                if pat.search(content):
                    hits[b] = True
            if all(hits.values()):
                return hits
    return hits


def _build_symbol_hit_cache(all_symbols: set[str]) -> dict[str, bool]:
    """Run one batched ripgrep for the union of bare identifiers.

    Returns a map bare-identifier -> True/False. Identifiers shorter than 3 chars
    map to True (matches the legacy "too short to usefully search" branch). Falls
    back to a single tree walk if rg is unavailable.
    """
    bares = {_bare_identifier(s) for s in all_symbols}
    bares = {b for b in bares if b}
    short = {b for b in bares if len(b) < 3}
    long_bares = bares - short
    hits: dict[str, bool] = {b: True for b in short}

    if not long_bares:
        return hits

    pattern_lines = "\n".join(rf"\b{re.escape(b)}\b" for b in sorted(long_bares))
    with tempfile.NamedTemporaryFile("w", suffix=".rg", delete=False, encoding="utf-8") as tf:
        tf.write(pattern_lines)
        pattern_file = tf.name

    try:
        try:
            # One rg pass: print every match as "<bare>" by capturing it in the
            # pattern via -o. We don't actually need the file -- just whether
            # any line in the corpus matched a given bare identifier.
            result = subprocess.run(
                ["rg", "--type-add", "src:*.{cs,py,sh}", "-t", "src",
                 "--glob", "!Docs", "--glob", "!build",
                 "-o", "--no-filename", "-N", "-f", pattern_file, str(ROOT)],
                capture_output=True, text=True, timeout=60,
            )
        except (FileNotFoundError, subprocess.TimeoutExpired):
            hits.update(_python_walk_symbol_hits(long_bares))
            return hits
    finally:
        try:
            os.unlink(pattern_file)
        except OSError:
            pass

    matched: set[str] = set()
    if result.returncode in (0, 1) and result.stdout:
        for line in result.stdout.splitlines():
            stripped = line.strip()
            if stripped in long_bares:
                matched.add(stripped)
    for b in long_bares:
        hits[b] = b in matched
    return hits


def grep_symbol(symbol: str) -> bool:
    """Return True if the symbol (bare identifier, no parens/dots) appears anywhere
    outside `Docs/` and `build/`. Conservative: we just need any one hit.

    Relies on the per-run cache primed by `prime_symbol_cache`. If unprimed
    (e.g. external callers), falls back to a single ad-hoc rg invocation for
    the symbol on its own.
    """
    bare = _bare_identifier(symbol)
    if not bare or len(bare) < 3:
        return True
    if _symbol_hits_cache is not None and bare in _symbol_hits_cache:
        return _symbol_hits_cache[bare]
    # Unprimed fallback: build a single-symbol cache so the result is still
    # correct (but not batched). Keeps the helper safe for external callers.
    return _build_symbol_hit_cache({symbol}).get(bare, False)


def prime_symbol_cache(symbols: set[str]) -> None:
    """Batch-resolve every symbol the audit will look up. Idempotent."""
    global _symbol_hits_cache
    _symbol_hits_cache = _build_symbol_hit_cache(symbols)


_PATH_SEGMENT = re.compile(r"^[A-Za-z0-9_.\-]+$")


def _looks_like_directory_root(segment: str) -> bool:
    """Heuristic: does this segment plausibly start a real path?

    Real paths in this repo start with one of:
      - An uppercase letter (CamelCase root dir like `Tools`, `Core`).
      - A dot or underscore prefix (`.github`, `.claude`, `_staging`).
      - A lowercase root dir we recognise (`build`, `reports`, `bin`, `obj`).
    Prose word lists like `fire/water/ice` start with a lowercase common word
    and are filtered out.
    """
    if not segment:
        return False
    if segment[0].isupper():
        return True
    if segment[0] in (".", "_"):
        return True
    if segment in {"build", "reports", "bin", "obj", "dev.sh"}:
        return True
    return False


def classify(ref: str) -> str:
    """Return 'path' | 'symbol' | 'skip'.

    Path: ends with a known code-file extension, OR contains a path separator
    AND every segment matches `[A-Za-z0-9_.-]+` AND the leading segment looks
    like a directory root (CamelCase, dot/underscore prefix, or recognised
    lowercase root). The segment check excludes prose code-shape descriptions
    like `pathlib.rglob("*.cs") + per-file regex` (has parens/quotes/+); the
    directory-root check excludes prose enumerations like
    `fire/water/ice/wind/lightning/earth/divine/void/arcane`.
    Symbol: matches a C# identifier chain (`Foo`, `Foo.Bar`, `Foo.Bar()`). The
    chain-with-dots case must be distinguished from a path like `foo.json`; we
    key off the extension whitelist, not just "has a dot".
    """
    if not ref.strip():
        return "skip"
    if "*" in ref:
        return "skip"
    ext = pathlib.Path(ref).suffix.lower()
    if ext in CODE_EXTS:
        return "path"
    if "/" in ref or "\\" in ref:
        segments = [s for s in re.split(r"[/\\]", ref) if s]
        if not segments:
            return "skip"
        if not all(_PATH_SEGMENT.match(seg) for seg in segments):
            return "skip"
        if not _looks_like_directory_root(segments[0]):
            return "skip"
        return "path"
    if SYMBOL.match(ref):
        return "symbol"
    return "skip"


STATUS_LINE = re.compile(r"^\*\*Status:\*\*\s+(.+?)\s*$")
PHASE_READY_LINE = re.compile(r"^\s*\d+[a-z]?\.\s+\*\*ready\*\*", re.IGNORECASE)
PROPOSAL_MARKER = re.compile(r"^\*\*(Proposal|Decision|Approach|Phases)\b")


class Item:
    def __init__(self, num: int, title: str, start_line: int):
        self.num = num
        self.title = title
        self.start_line = start_line
        self.body_lines: list[str] = []
        self.verify_cmd: str | None = None
        self.status: str | None = None

    def status_class(self) -> str:
        """Classify `**Status:**` line into one of: ready / needs-clarification / blocked-by /
        scheduled-human-session / landed / unknown."""
        if not self.status:
            return "ready"
        s = self.status.lower()
        if s.startswith("blocked-by"):
            return "blocked-by"
        if s.startswith("needs-clarification"):
            return "needs-clarification"
        if s.startswith("scheduled-human-session"):
            return "scheduled-human-session"
        if s.startswith("ready"):
            return "ready"
        if s.startswith("landed"):
            return "landed"
        return "unknown"

    def has_phase_ready(self) -> bool:
        """True if any line in the body matches a phase-list 'N. **ready** ...' marker."""
        for line in self.body_lines:
            if PHASE_READY_LINE.match(line):
                return True
        return False

    def refs(self) -> list[tuple[int, str, bool]]:
        """Return list of (line-within-body, ref-text, in_proposal_block) for each backticked ref.
        Lines marked with <!-- docs-lint-skip --> on the preceding line are skipped.
        `in_proposal_block` is True when the ref appears inside a paragraph led by
        `**Proposal.**` / `**Decision.**` / `**Approach.**` / `**Phases**` — the
        caller may suppress symbol checks in those paragraphs because the names
        are deliberately future/proposed state. Path checks still apply."""
        out = []
        prev_skip = False
        in_proposal = False
        for i, line in enumerate(self.body_lines):
            stripped = line.strip()
            if PROPOSAL_MARKER.match(stripped):
                in_proposal = True
            elif in_proposal and not stripped:
                in_proposal = False
            if prev_skip:
                prev_skip = SKIP_MARKER.search(line) is not None
                continue
            if SKIP_MARKER.search(line):
                prev_skip = True
                continue
            for m in BACKTICK_REF.finditer(line):
                out.append((i, m.group(1), in_proposal))
        return out


def parse_items(path: pathlib.Path) -> list[Item]:
    items: list[Item] = []
    current: Item | None = None
    try:
        with open(path, encoding="utf-8") as f:
            lines = f.read().splitlines()
    except OSError as e:
        print(f"[audit-refactors] could not read {path}: {e}", file=sys.stderr)
        return []

    for lineno, line in enumerate(lines, start=1):
        m = ITEM_HEADER.match(line)
        if m:
            if current:
                items.append(current)
            current = Item(num=int(m.group(1)), title=m.group(2), start_line=lineno)
            continue
        if NON_ITEM_HEADER.match(line):
            # A "## Not included" or similar terminates an in-progress item.
            if current:
                items.append(current)
                current = None
            continue
        if current is None:
            continue
        current.body_lines.append(line)
        vm = VERIFY_LINE.match(line)
        if vm:
            current.verify_cmd = vm.group(1).strip("`")
        if current.status is None:
            sm = STATUS_LINE.match(line)
            if sm:
                current.status = sm.group(1)
    if current:
        items.append(current)
    return items


def run_verify(cmd: str) -> tuple[int, str]:
    """Run the verify command via bash -c. Returns (exit_code, tail)."""
    try:
        result = subprocess.run(
            ["bash", "-c", cmd],
            cwd=ROOT, capture_output=True, text=True, timeout=60,
        )
        tail = (result.stdout + result.stderr).strip().splitlines()[-5:]
        return result.returncode, "\n".join(tail)
    except (FileNotFoundError, subprocess.TimeoutExpired) as e:
        return 127, f"verify failed to run: {e}"


def audit_item(item: Item, verbose: bool,
               verify_results: dict[str, tuple[int, str]] | None = None) -> list[str]:
    issues: list[str] = []
    seen: set[str] = set()
    suppressed_symbols: list[str] = []
    for _, ref, in_proposal in item.refs():
        if ref in seen:
            continue
        seen.add(ref)
        # Command-shape refs are not filesystem paths: `./dev.sh foo` is a subcommand,
        # `python Tools/x.py 3` is a script invocation with args, `dotnet build` is a
        # CLI command. Route each through its registry instead of resolve_path.
        dev_m = DEV_SH_CMD.match(ref)
        if dev_m:
            subcmd = dev_m.group(1)
            if subcmd not in dev_sh_subcommands():
                issues.append(f"unknown dev.sh subcommand: `{ref}`")
            continue
        py_m = PYTHON_SCRIPT.match(ref)
        if py_m:
            if resolve_path(py_m.group(1)) is None:
                issues.append(f"python script no longer resolves: `{ref}`")
            continue
        if DOTNET_CMD.match(ref):
            continue
        kind = classify(ref)
        if kind == "path":
            if resolve_path(ref) is None:
                issues.append(f"path no longer resolves: `{ref}`")
        elif kind == "symbol":
            if not grep_symbol(ref):
                if in_proposal:
                    suppressed_symbols.append(ref)
                else:
                    issues.append(f"symbol not found anywhere: `{ref}`")
    if verbose and suppressed_symbols:
        for ref in suppressed_symbols:
            issues.append(f"symbol not found (suppressed: proposal block): `{ref}`")

    if item.verify_cmd:
        if verify_results is not None and item.verify_cmd in verify_results:
            exit_code, tail = verify_results[item.verify_cmd]
        else:
            exit_code, tail = run_verify(item.verify_cmd)
        if exit_code == 0:
            issues.append(
                f"Verify command exited 0 (issue may be resolved): `{item.verify_cmd}`"
            )
        elif verbose:
            issues.append(
                f"Verify command exited {exit_code} (issue persists): `{item.verify_cmd}`"
            )
    return issues


def run_verify_commands_parallel(commands: list[str], max_workers: int = 4) -> dict[str, tuple[int, str]]:
    """Run verify commands concurrently. Returns {cmd: (exit_code, tail)} keyed by
    command text. Deduplicates identical command strings before dispatch."""
    unique = list(dict.fromkeys(commands))
    if not unique:
        return {}
    results: dict[str, tuple[int, str]] = {}
    with ThreadPoolExecutor(max_workers=max_workers) as pool:
        future_to_cmd = {pool.submit(run_verify, cmd): cmd for cmd in unique}
        for future in future_to_cmd:
            cmd = future_to_cmd[future]
            try:
                results[cmd] = future.result()
            except Exception as e:
                results[cmd] = (127, f"verify raised: {e}")
    return results


def count_by_status(items) -> dict[str, int]:
    counts = {
        "ready": 0,
        "needs_clarification": 0,
        "needs_clarification_with_phase_ready": 0,
        "blocked_by": 0,
        "scheduled_human_session": 0,
        "landed": 0,
        "unknown": 0,
    }
    for item in items:
        cls = item.status_class()
        if cls == "needs-clarification" and item.has_phase_ready():
            counts["needs_clarification_with_phase_ready"] += 1
        key = cls.replace("-", "_")
        counts[key if key in counts else "unknown"] += 1
    return counts


def main(argv: list[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    parser = make_argparser("audit-refactors", description=__doc__)
    parser.add_argument("--by-status", action="store_true",
                        help="append a status breakdown line after the canonical footer")
    parser.add_argument("--id", dest="ids", action="append", type=int, default=[],
                        help="restrict audit to a specific entry number; repeatable")
    parser.add_argument("--match", dest="match", default=None,
                        help="restrict audit to entries whose title contains the given substring (case-insensitive)")
    args = parser.parse_args(argv)
    verbose = args.verbose
    summary = args.summary
    by_status = args.by_status
    id_filter = set(args.ids or [])
    match_filter = args.match.lower() if args.match else None
    if not REFACTORS.exists():
        print(f"[audit-refactors] {REFACTORS} does not exist", file=sys.stderr)
        return 2
    all_items = parse_items(REFACTORS)
    if not all_items:
        if args.json:
            import json as _json
            print(_json.dumps({
                "tool": "audit-refactors", "total": 0, "files_with_hits": 0, "files_scanned": 0,
                "severity": "blocking", "extra": {"scanned_items": 0, "total_items": 0},
            }))
        print("[audit-refactors] no items parsed (empty queue)")
        return 0

    if id_filter or match_filter:
        def _keep(item):
            if id_filter and item.num in id_filter:
                return True
            if match_filter and match_filter in item.title.lower():
                return True
            return False
        items = [i for i in all_items if _keep(i)]
        if not items:
            print(f"[audit-refactors] no items matched filter (--id {sorted(id_filter)} --match {args.match!r})")
            return 0
        print(f"[audit-refactors] scanning {len(items)} of {len(all_items)} item(s) (filtered)")
    else:
        items = all_items
        print(f"[audit-refactors] scanning {len(items)} item(s)")

    all_symbols: set[str] = set()
    for item in items:
        for _, ref, _ in item.refs():
            if DEV_SH_CMD.match(ref) or PYTHON_SCRIPT.match(ref) or DOTNET_CMD.match(ref):
                continue
            if classify(ref) == "symbol":
                all_symbols.add(ref)
    prime_symbol_cache(all_symbols)

    verify_cmds = [item.verify_cmd for item in items if item.verify_cmd]
    verify_results = run_verify_commands_parallel(verify_cmds, max_workers=4)

    any_issue = False
    items_with_issues = 0
    items_audited = 0
    for item in items:
        # Landed entries are kept for traceability; their refs may have intentionally
        # drifted post-landing (renamed files, deleted dead symbols). Skip the
        # ref-resolution audit for landed items but keep them in the by-status counts.
        if item.status_class() == "landed":
            continue
        items_audited += 1
        issues = audit_item(item, verbose, verify_results=verify_results)
        if not issues and not verbose:
            continue
        if not summary:
            header = f"#{item.num}. {item.title}  (line {item.start_line})"
            print(f"\n{header}")
            print("-" * len(header))
            if not issues:
                print("  ok")
                continue
        if issues:
            any_issue = True
            items_with_issues += 1
            if not summary:
                for msg in issues:
                    print(f"  - {msg}")

    status_counts = count_by_status(items)
    if by_status:
        nc_part = f"needs-clarification={status_counts['needs_clarification']}"
        if status_counts["needs_clarification_with_phase_ready"]:
            nc_part += f" ({status_counts['needs_clarification_with_phase_ready']} with phase-level ready)"
        parts = [
            f"ready={status_counts['ready']}",
            nc_part,
            f"blocked-by={status_counts['blocked_by']}",
            f"scheduled-human-session={status_counts['scheduled_human_session']}",
            f"landed={status_counts['landed']}",
        ]
        if status_counts["unknown"]:
            parts.append(f"unknown={status_counts['unknown']}")
        status_line = "[audit-refactors] status: " + " ".join(parts)
    else:
        status_line = None

    if args.json:
        import json as _json
        print(_json.dumps({
            "tool": "audit-refactors",
            "total": items_with_issues,
            "files_with_hits": items_with_issues,
            "files_scanned": len(items),
            "severity": "blocking",
            "extra": {
                "scanned_items": len(items),
                "total_items": len(all_items),
                "filtered": bool(id_filter or match_filter),
                "by_status": status_counts,
            },
        }))

    if not any_issue:
        print("\n[audit-refactors] clean")
        if status_line:
            print(status_line)
        return 0
    print(f"\n[audit-refactors] {items_with_issues} item(s) with stale refs across {items_audited} scanned")
    if status_line:
        print(status_line)
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
