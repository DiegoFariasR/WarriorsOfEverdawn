#!/usr/bin/env python
"""
Doc-to-code drift linter.

Scans markdown files for references to code, files, and shell commands. Flags
references that no longer resolve: missing files, out-of-bounds line numbers,
dev.sh subcommands that were renamed or removed.

Scope is deliberately tight: only high-confidence, low-false-positive checks.
Paraphrased references and semantic drift (doc claim disagrees with code
behaviour) are out of scope -- those need human review.

Escape valves:
    <!-- docs-lint-skip -->   on the line above an intentionally-stale ref
    .docs-lint-ignore         regex-per-line file at repo root, matched against issue text

Usage:
    python Tools/check_docs.py [--verbose] [--summary] [--fix]

    --summary : suppress per-finding lines; keep the trailing total only
    --fix     : self-heal broken refs in place when the basename resolves
                uniquely in the source tree (mechanical recovery after a
                file move). Skipped when basename is ambiguous. Restricted
                to whitelisted source dirs (GodotClient/, Core/, Core.Tests/,
                Tools/) to avoid resolving to _staging/ or build artifacts.

Exit codes:
    0 -- clean
    1 -- one or more issues found
    2 -- tool error (no docs found / dev.sh unreadable)
"""
from __future__ import annotations

import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _check_harness import ToolReport, make_argparser, report  # noqa: E402
from _file_scanner import INDEX_SKIP_DIRS, dev_sh_subcommands, filename_index  # noqa: E402, F401

ROOT = pathlib.Path(__file__).resolve().parent.parent

DOC_DIRS = [
    ROOT / "Docs",
]

DOC_FILES = [
    ROOT / "AGENTS.md",
    ROOT / "CLAUDE.md",
    ROOT / "GodotClient" / "AGENTS.md",
    ROOT / "GodotClient" / "CLAUDE.md",
    ROOT / "Core" / "AGENTS.md",
    ROOT / "Core" / "CLAUDE.md",
]

CHECKABLE_EXTS = {".cs", ".py", ".sh", ".yml", ".yaml", ".json", ".md", ".tscn", ".gd", ".webp", ".png", ".glb"}

MARKDOWN_LINK = re.compile(r"\[([^\]]*)\]\(([^)\s]+)\)")
FILE_COLON_LINE = re.compile(r"`?([A-Za-z0-9_./\\-]+\.[a-zA-Z]{2,5}):(\d+)(?:-(\d+))?`?")
BACKTICKED_PATH = re.compile(r"`([A-Za-z0-9_./\\-]+?\.(?:cs|py|sh|yml|yaml|json|md|tscn|gd))`")
DEV_SH_CMD = re.compile(r"\./dev\.sh\s+([a-z][a-z0-9-]*)")

SKIP_PREV_LINE = re.compile(r"<!--\s*docs-lint-skip\s*-->")


def collect_docs() -> list[pathlib.Path]:
    files: set[pathlib.Path] = set()
    for d in DOC_DIRS:
        if d.exists():
            for p in d.rglob("*.md"):
                files.add(p)
    for f in DOC_FILES:
        if f.exists():
            files.add(f)
    return sorted(files)


def load_ignore_patterns() -> list[re.Pattern]:
    ignore_file = ROOT / ".docs-lint-ignore"
    if not ignore_file.exists():
        return []
    patterns: list[re.Pattern] = []
    with open(ignore_file, encoding="utf-8") as f:
        for raw in f:
            raw = raw.strip()
            if not raw or raw.startswith("#"):
                continue
            try:
                patterns.append(re.compile(raw))
            except re.error:
                pass
    return patterns


def is_external(ref: str) -> bool:
    return ref.startswith(("http://", "https://", "mailto:", "ftp://"))


def is_pure_anchor(ref: str) -> bool:
    return ref.startswith("#")


def is_checkable_path(ref: str) -> bool:
    if is_external(ref) or is_pure_anchor(ref):
        return False
    path_only = ref.split("#", 1)[0].split("?", 1)[0]
    if not path_only:
        return False
    if any(path_only.lower().endswith(ext) for ext in CHECKABLE_EXTS):
        return True
    return ("/" in path_only or "\\" in path_only) and "." in pathlib.Path(path_only).name


def resolve(ref: str, doc_dir: pathlib.Path | None = None) -> pathlib.Path | None:
    path_only = ref.split("#", 1)[0].split("?", 1)[0]
    if not path_only:
        return None
    p = pathlib.Path(path_only)
    if p.is_absolute():
        return p if p.exists() else None
    # Try doc-relative first (links and refs in a subdir doc typically live near it).
    if doc_dir is not None:
        rel_candidate = (doc_dir / p).resolve()
        if rel_candidate.exists():
            return rel_candidate
    # Then repo-root-relative.
    root_candidate = ROOT / p
    if root_candidate.exists():
        return root_candidate
    # Bare filename (no path separator) -- search the repo for any file with this name.
    if "/" not in path_only and "\\" not in path_only:
        hit = filename_index().get(path_only)
        if hit is not None:
            return hit
    return None


def count_lines(p: pathlib.Path) -> int:
    try:
        with open(p, encoding="utf-8", errors="ignore") as f:
            return sum(1 for _ in f)
    except OSError:
        return 0


def parse_line_fragment(fragment: str) -> tuple[int, int] | None:
    # Accept #L42, #L42-L51, #L42-51
    if not fragment.startswith("L"):
        return None
    body = fragment[1:]
    try:
        if "-" in body:
            lo_s, hi_s = body.split("-", 1)
            lo = int(lo_s)
            hi = int(hi_s.lstrip("L"))
        else:
            lo = hi = int(body)
        return lo, hi
    except ValueError:
        return None


def check_line_range(resolved: pathlib.Path, lo: int, hi: int, ref_desc: str) -> str | None:
    total = count_lines(resolved)
    if total == 0:
        return None
    if lo < 1 or hi > total:
        return f"line range out of bounds: {ref_desc} (file has {total} lines)"
    return None


def scan(path: pathlib.Path, known_subcommands: set[str]) -> list[tuple[int, str]]:
    try:
        with open(path, encoding="utf-8") as f:
            lines = f.read().splitlines()
    except OSError as e:
        return [(0, f"could not read: {e}")]

    issues: list[tuple[int, str]] = []
    doc_dir = path.parent
    prev_line_skip = False

    for i, line in enumerate(lines, start=1):
        if prev_line_skip:
            prev_line_skip = SKIP_PREV_LINE.search(line) is not None
            continue
        if SKIP_PREV_LINE.search(line):
            prev_line_skip = True
            continue

        # 1. Markdown links.
        for m in MARKDOWN_LINK.finditer(line):
            target = m.group(2)
            if not is_checkable_path(target):
                continue
            resolved = resolve(target, doc_dir)
            if resolved is None:
                issues.append((i, f"broken link: {m.group(0)}"))
                continue
            frag = target.split("#", 1)[1] if "#" in target else None
            if frag:
                rng = parse_line_fragment(frag)
                if rng is not None:
                    err = check_line_range(resolved, rng[0], rng[1], m.group(0))
                    if err:
                        issues.append((i, err))

        # 2. file.ext:line references outside markdown links.
        for m in FILE_COLON_LINE.finditer(line):
            path_part = m.group(1)
            if is_external(path_part):
                continue
            if "/" not in path_part and "\\" not in path_part:
                # Skip bare filenames (too many false positives from error-text blocks).
                continue
            resolved = resolve(path_part, doc_dir)
            if resolved is None:
                issues.append((i, f"broken file:line ref: {m.group(0)}"))
                continue
            lo = int(m.group(2))
            hi = int(m.group(3)) if m.group(3) else lo
            err = check_line_range(resolved, lo, hi, m.group(0))
            if err:
                issues.append((i, err))

        # 3. Backticked paths.
        for m in BACKTICKED_PATH.finditer(line):
            ref = m.group(1)
            if is_external(ref):
                continue
            if resolve(ref, doc_dir) is None:
                issues.append((i, f"broken path ref: `{ref}`"))

        # 4. dev.sh subcommands.
        for m in DEV_SH_CMD.finditer(line):
            cmd = m.group(1)
            if cmd not in known_subcommands:
                issues.append((i, f"unknown dev.sh subcommand: `./dev.sh {cmd}`"))

    return issues


def filter_ignored(issues: list[tuple[int, str]], patterns: list[re.Pattern]) -> list[tuple[int, str]]:
    if not patterns:
        return issues
    return [(n, m) for (n, m) in issues if not any(p.search(m) for p in patterns)]


# Whitelist of source dirs the --fix flag is allowed to point fixed refs into.
# Keeps the basename lookup from resolving to build artifacts, _staging copies,
# or sibling tree clones.
_FIX_BASENAME_ROOTS = (
    "GodotClient", "Core", "Core.Tests", "Tools",
)


_basename_all_index_cache: dict[str, list[pathlib.Path]] | None = None


def _basename_all_index() -> dict[str, list[pathlib.Path]]:
    """Build a basename -> [paths] index restricted to the whitelisted source
    dirs. Multiple results per basename signal ambiguity; --fix skips those."""
    global _basename_all_index_cache
    if _basename_all_index_cache is not None:
        return _basename_all_index_cache
    import os
    idx: dict[str, list[pathlib.Path]] = {}
    for root_name in _FIX_BASENAME_ROOTS:
        root_dir = ROOT / root_name
        if not root_dir.exists():
            continue
        for dirpath, dirnames, filenames in os.walk(root_dir):
            dirnames[:] = [d for d in dirnames if d not in INDEX_SKIP_DIRS]
            for fn in filenames:
                idx.setdefault(fn, []).append(pathlib.Path(dirpath) / fn)
    _basename_all_index_cache = idx
    return idx


_BROKEN_LINK_RE = re.compile(r"broken link: (\[[^\]]*\]\(([^)]+)\))")
_BROKEN_PATH_RE = re.compile(r"broken (?:path ref|file:line ref): `?([^`]+)`?")


def _try_fix_ref(ref: str, doc_dir: pathlib.Path) -> tuple[str | None, str]:
    """Return (replacement_ref, status). status is one of: 'fixed'/'ambiguous'/'no-match'/'skip'.
    `replacement_ref` is the path string to substitute for `ref` (preserving the
    `../` prefix count) or None if not fixable.
    """
    path_part, _, anchor = ref.partition("#")
    if not path_part:
        return None, "skip"
    basename = pathlib.Path(path_part).name
    if not basename or "." not in basename:
        return None, "skip"
    candidates = _basename_all_index().get(basename, [])
    if len(candidates) == 0:
        return None, "no-match"
    if len(candidates) > 1:
        return None, "ambiguous"
    target_abs = candidates[0]
    # Re-derive a path string that preserves the `../` prefix shape of the
    # original ref. Compute the relative path from `doc_dir` to the resolved
    # target; if the original ref was repo-root-relative (no leading `..`),
    # keep it that way.
    try:
        rel_from_doc = pathlib.Path(target_abs).resolve().relative_to(doc_dir.resolve())
        replacement = str(rel_from_doc).replace("\\", "/")
    except ValueError:
        # Need ../ traversal; build manually.
        try:
            target_resolved = target_abs.resolve()
            doc_resolved = doc_dir.resolve()
            common = pathlib.Path(*[a for a, b in zip(target_resolved.parts, doc_resolved.parts) if a == b])
            up = len(doc_resolved.parts) - len(common.parts)
            down = target_resolved.parts[len(common.parts):]
            replacement = "/".join([".."] * up + list(down))
        except Exception:
            return None, "skip"
    if anchor:
        replacement = f"{replacement}#{anchor}"
    return replacement, "fixed"


def _apply_fixes(path: pathlib.Path, issues: list[tuple[int, str]]) -> tuple[int, int, int]:
    """Rewrite `path` in place, replacing broken refs whose basename resolves
    uniquely. Returns (fixed_count, ambiguous_count, no_match_count)."""
    try:
        with open(path, encoding="utf-8") as f:
            text = f.read()
    except OSError:
        return 0, 0, 0
    doc_dir = path.parent
    fixed = 0
    ambiguous = 0
    no_match = 0
    replacements: list[tuple[str, str]] = []
    for _, msg in issues:
        m = _BROKEN_LINK_RE.search(msg)
        if m:
            target = m.group(2)
        else:
            mp = _BROKEN_PATH_RE.search(msg)
            target = mp.group(1) if mp else None
        if target is None:
            continue
        path_part, _, _ = target.partition("#")
        if "/" not in path_part and "\\" not in path_part:
            # Bare-filename refs already get the filename_index lookup in resolve();
            # if they reached the issue list, they truly don't exist.
            no_match += 1
            continue
        replacement, status = _try_fix_ref(target, doc_dir)
        if status == "fixed" and replacement is not None:
            replacements.append((target, replacement))
            fixed += 1
        elif status == "ambiguous":
            ambiguous += 1
        elif status == "no-match":
            no_match += 1
    if not replacements:
        return 0, ambiguous, no_match
    new_text = text
    for old, new in replacements:
        new_text = new_text.replace(old, new)
    if new_text != text:
        with open(path, "w", encoding="utf-8") as f:
            f.write(new_text)
    return fixed, ambiguous, no_match


def main(argv: list[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    parser = make_argparser("check-docs", description=__doc__)
    parser.add_argument("--fix", action="store_true",
                        help="self-heal broken refs in place when basename resolves uniquely in the whitelisted source dirs")
    args = parser.parse_args(argv)
    verbose = args.verbose
    summary = args.summary
    do_fix = args.fix

    files = collect_docs()
    if not files:
        print("[check-docs] no doc files found", file=sys.stderr)
        return 2

    known_cmds = dev_sh_subcommands()
    if not known_cmds:
        print("[check-docs] warning: dev.sh not found or no subcommands parsed", file=sys.stderr)

    ignore_patterns = load_ignore_patterns()

    if verbose:
        print(f"[check-docs] scanning {len(files)} file(s), {len(known_cmds)} dev.sh subcommand(s)")

    files_with_issues = 0
    total_issues = 0
    findings_tuples: list[tuple[str, int, str]] = []
    fix_total = 0
    fix_ambiguous = 0
    fix_no_match = 0
    for path in files:
        issues = filter_ignored(scan(path, known_cmds), ignore_patterns)
        if not issues:
            continue
        if do_fix:
            fixed, ambig, nomatch = _apply_fixes(path, issues)
            if fixed:
                rel = str(path.relative_to(ROOT))
                print(f"[check-docs] --fix rewrote {fixed} ref(s) in {rel}")
            fix_total += fixed
            fix_ambiguous += ambig
            fix_no_match += nomatch
            # Re-scan after rewrite so the footer counts the residual issues.
            issues = filter_ignored(scan(path, known_cmds), ignore_patterns)
            if not issues:
                continue
        files_with_issues += 1
        total_issues += len(issues)
        rel = str(path.relative_to(ROOT))
        if not summary:
            print(f"{rel}:")
            for lineno, msg in issues:
                print(f"  line {lineno:>4}: {msg}")
        findings_tuples.extend((rel, lineno, msg) for lineno, msg in issues)

    if do_fix:
        print(f"[check-docs] --fix summary: fixed={fix_total} ambiguous={fix_ambiguous} no-match={fix_no_match}")

    tool_report = ToolReport(
        tool_name="check-docs",
        findings=findings_tuples,
        files_scanned=len(files),
        files_with_hits=files_with_issues,
        unit="issue",
        severity="blocking",
    )
    return report(tool_report, json_out=args.json, verbose=verbose)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
