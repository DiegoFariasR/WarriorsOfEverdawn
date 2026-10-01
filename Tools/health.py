#!/usr/bin/env python
"""
Project-health dashboard, adapted from Everdawn's.

Runs every drift check at once (formatting, docs, pending refactors, subagent
and skill docs, Godot --headless timeouts) and prints a compact status table.
Each check stays callable on its own; this is the "look at all dashboards at
once" entry point. Tests and the Godot self-tests are not drift checks and
stay separate (./dev.sh test, ./dev.sh smoke).

Usage:
    python Tools/health.py [--verbose] [--only SUBSTR ...] [--skip SUBSTR ...] [--list]

Filters:
    --only SUBSTR   Keep only checks whose label or short-key contains SUBSTR.
                    Repeat the flag to OR multiple substrings.
    --skip SUBSTR   Drop checks whose label or short-key contains SUBSTR.
                    Comma-separated lists also accepted (e.g. --skip docs,format).
    --list          Print the available check labels + short-keys and exit.

Exit codes:
    0 -- every check clean
    1 -- at least one check reports drift (but the script always runs all checks)
"""
from __future__ import annotations

import concurrent.futures
import json
import pathlib
import re
import subprocess
import sys
import time
from dataclasses import dataclass
from typing import Callable

ROOT = pathlib.Path(__file__).resolve().parent.parent


@dataclass
class Check:
    label: str
    cmd: list[str]
    # Extracts a one-line status summary from the check's stdout+stderr combined.
    summarize: Callable[[str, int], tuple[str, int]]


def summarize_format(out: str, exit_code: int) -> tuple[str, int]:
    if exit_code == 0:
        return "clean", 0
    files = {m.group(1) for m in re.finditer(r"([\w./\\-]+\.cs)\(\d+,\d+\)", out)}
    if files:
        return f"{len(files)} file(s) need dotnet format", 1
    return f"dotnet format failed (exit {exit_code})", 1


def summarize_godot_timeouts(out: str, exit_code: int) -> tuple[str, int]:
    if "[check-godot-timeouts] clean" in out:
        return "clean", 0
    m = re.search(r"\[check-godot-timeouts\] (\d+) unwrapped --headless launch\(es\)", out)
    if m:
        return f"{m.group(1)} unwrapped --headless launch(es)", 1
    return "unknown state", 1


def _extract_harness_json(out: str) -> dict | None:
    """Find the ToolReport JSON line in tool output. Returns None if missing."""
    for line in out.splitlines():
        line = line.strip()
        if not (line.startswith("{") and line.endswith("}")):
            continue
        try:
            payload = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(payload, dict) and "tool" in payload and "total" in payload:
            return payload
    return None


def summarize_harness_json(out: str, tool_name: str, unit: str) -> tuple[str, int]:
    payload = _extract_harness_json(out)
    if payload is None:
        return "unknown state (no JSON footer)", 1
    total = int(payload.get("total", 0))

    if tool_name == "audit-refactors":
        scanned = int((payload.get("extra") or {}).get("scanned_items", 0))
        if scanned == 0:
            return "0 items (empty queue)", 0
        if total == 0:
            return f"{scanned} items, all clean", 0
        return f"{scanned} items, {total} with stale refs", 1

    if total == 0:
        return "clean", 0
    row = f"{total} {unit}(s) in {int(payload.get('files_with_hits', 0))} file(s)"
    return row, 0 if payload.get("severity") == "advisory" else 1


def summarize_level_audit(out: str, exit_code: int) -> tuple[str, int]:
    audited = len(re.findall(r"^\[level-audit\] \S+: ", out, flags=re.M))
    failing = len(re.findall(r"^\[level-audit\] \S+: \d+ problem", out, flags=re.M))
    if audited == 0:
        return "no layouts audited", 1
    return ("clean" if failing == 0 else f"{failing} of {audited} layout(s) with problems"), (1 if failing or exit_code else 0)


CHECKS = [
    Check(
        label="Formatting",
        cmd=["dotnet", "format", str(ROOT / "WarriorsOfEverdawn.slnx"), "--verify-no-changes"],
        summarize=summarize_format,
    ),
    Check(
        label="Documentation",
        cmd=[sys.executable, str(ROOT / "Tools" / "check_docs.py"), "--summary", "--json"],
        summarize=lambda out, _: summarize_harness_json(out, "check-docs", "issue"),
    ),
    Check(
        label="Pending refactors",
        cmd=[sys.executable, str(ROOT / "Tools" / "audit_refactors.py"), "--summary", "--json"],
        summarize=lambda out, _: summarize_harness_json(out, "audit-refactors", "item"),
    ),
    Check(
        label="Subagent and skill docs",
        cmd=[sys.executable, str(ROOT / "Tools" / "lint_agents.py"), "--summary", "--json"],
        summarize=lambda out, _: summarize_harness_json(out, "lint-agents", "issue"),
    ),
    Check(
        label="Godot launch timeouts",
        cmd=[sys.executable, str(ROOT / "Tools" / "check_godot_timeouts.py"), "--summary"],
        summarize=summarize_godot_timeouts,
    ),
    Check(
        label="Level layouts",
        cmd=[sys.executable, str(ROOT / "Tools" / "level_audit.py")],
        summarize=summarize_level_audit,
    ),
]


def short_key(label: str) -> str:
    """Derive a short slug for filtering. 'Pending refactors' -> 'pending'."""
    base = re.split(r"[\s(]", label, maxsplit=1)[0]
    return base.lower()


def parse_filter_values(argv: list[str], flag: str) -> list[str]:
    """Collect repeated --flag values; allow comma-separated within each value."""
    values: list[str] = []
    i = 0
    while i < len(argv):
        if argv[i] == flag and i + 1 < len(argv):
            for part in argv[i + 1].split(","):
                part = part.strip()
                if part:
                    values.append(part.lower())
            i += 2
        else:
            i += 1
    return values


def filter_checks(checks: list[Check], only: list[str], skip: list[str]) -> list[Check]:
    def matches(check: Check, needles: list[str]) -> bool:
        haystack = (check.label + " " + short_key(check.label)).lower()
        return any(n in haystack for n in needles)

    out = list(checks)
    if only:
        out = [c for c in out if matches(c, only)]
    if skip:
        out = [c for c in out if not matches(c, skip)]
    return out


def run_check(check: Check, verbose: bool) -> tuple[str, int, float]:
    start = time.monotonic()
    result = subprocess.run(check.cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    duration = time.monotonic() - start
    combined = result.stdout + "\n" + result.stderr
    try:
        summary, exit_bit = check.summarize(combined, result.returncode)
    except Exception as e:
        summary, exit_bit = f"summarizer error: {e}", 1
    if verbose:
        print(f"\n--- {check.label} stdout ---")
        print(result.stdout)
        if result.stderr.strip():
            print(f"--- {check.label} stderr ---")
            print(result.stderr)
    return summary, exit_bit, duration


def main(argv: list[str]) -> int:
    if "--help" in argv or "-h" in argv:
        print(__doc__.strip())
        return 0
    if "--list" in argv:
        width = max(len(c.label) for c in CHECKS)
        for c in CHECKS:
            print(f"{c.label.ljust(width)}  {short_key(c.label)}")
        return 0
    verbose = "--verbose" in argv or "-v" in argv
    only_drift = "--only-drift" in argv or "--quiet" in argv or "-q" in argv

    checks = filter_checks(CHECKS, parse_filter_values(argv, "--only"), parse_filter_values(argv, "--skip"))
    if not checks:
        print("[health] no checks selected after --only/--skip filters", file=sys.stderr)
        return 1

    label_width = max(len(c.label) for c in checks)
    print(f"Project health ({ROOT.name})")
    print("=" * 60)

    # The checks are independent read-only subprocesses; results are collected by index so the table keeps
    # declaration order whatever order they finish in.
    results: list[tuple[str, int, float] | None] = [None] * len(checks)
    with concurrent.futures.ThreadPoolExecutor(max_workers=len(checks)) as pool:
        futures = {pool.submit(run_check, check, verbose): i for i, check in enumerate(checks)}
        for fut in concurrent.futures.as_completed(futures):
            results[futures[fut]] = fut.result()

    any_drift = False
    for check, result in zip(checks, results):
        assert result is not None
        summary, bit, dur = result
        if bit != 0:
            any_drift = True
        if only_drift and bit == 0:
            continue
        marker = "OK " if bit == 0 else "!! "
        print(f"{marker}{check.label.ljust(label_width)}  {summary}  ({dur:.1f}s)")

    print("=" * 60)
    print("drift detected" if any_drift else "all clean")
    return 1 if any_drift else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
