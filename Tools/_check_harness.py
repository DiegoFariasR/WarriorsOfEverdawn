"""
Shared scaffolding for the Tools/check_*.py family.

Copied from Everdawn. Provides a uniform CLI surface
(`--help` / `--verbose` / `--json` / `--summary`) and a `report()` helper
that emits the footer line every check tool already prints, optionally as
machine-readable JSON for `Tools/health.py` consumption.

Migration is opt-in: existing tools keep working until they switch over.
A migrated check tool shrinks to roughly:

    from _check_harness import make_argparser, report, ToolReport

    def scan() -> ToolReport:
        findings: list[tuple[str, int, str]] = []
        files_scanned = 0
        for f in iterate_files():
            files_scanned += 1
            for hit in scan_file(f):
                findings.append(hit)
        return ToolReport(
            tool_name="check-foo",
            findings=findings,           # (file, lineno, message) tuples
            files_scanned=files_scanned,
            unit="finding",              # noun used in the footer
        )

    def main(argv: list[str]) -> int:
        args = make_argparser("check-foo").parse_args(argv)
        result = scan()
        return report(result, json_out=args.json, verbose=args.verbose)

The harness is intentionally NOT a black-box framework -- tools that need
specialized footers (per-category breakdowns, allowlist displays) keep
their hand-rolled report blocks and just borrow the argparser. The
JSON-out shape is the contract `health.py` consumes once it migrates off
regex parsing in stage 2.
"""
from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass, field
from typing import Sequence


def make_argparser(tool_name: str, *, description: str | None = None) -> argparse.ArgumentParser:
    """Build the standard argparser used by every check_*.py tool.

    Adds `--verbose`/`-v`, `--summary`, and `--json`. Each tool's own
    `__doc__` is the canonical help text -- pass it as `description`.
    """
    parser = argparse.ArgumentParser(
        prog=f"python Tools/{tool_name.replace('-', '_')}.py",
        description=description,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--verbose", "-v", action="store_true",
                        help="emit per-file scan counts after the footer")
    parser.add_argument("--summary", action="store_true",
                        help="counts only; suppress per-finding list")
    parser.add_argument("--json", action="store_true",
                        help="emit machine-readable JSON footer for health.py")
    return parser


@dataclass
class ToolReport:
    """Result bundle for `report()`.

    tool_name        — `check-foo` (matches dev.sh subcommand name).
    findings         — list of `(rel_path, lineno, message)` tuples. Empty -> clean.
    files_scanned    — number of files iterated; printed in verbose footer.
    files_with_hits  — distinct files in `findings`; auto-computed when absent.
    unit             — singular noun for the footer ("finding", "leak", "stub").
    severity         — "blocking" (exit 1 on findings) or "advisory" (exit 0).
                       Defaults to "blocking" -- match existing tool behavior.
    extra            — open dict surfaced verbatim under `extra` in JSON output.
    """
    tool_name: str
    findings: Sequence[tuple[str, int, str]]
    files_scanned: int = 0
    files_with_hits: int | None = None
    unit: str = "finding"
    severity: str = "blocking"
    extra: dict = field(default_factory=dict)


def report(result: ToolReport, *, json_out: bool = False, verbose: bool = False) -> int:
    """Print the standard footer and return the appropriate exit code.

    Footer shapes (matching the existing tool convention):
        `[<tool>] clean (<N> file(s) scanned)`        when findings is empty + verbose
        `[<tool>] clean`                              when findings is empty
        `[<tool>] <N> <unit>(s) across <M> file(s)`   when findings is non-empty

    Exit codes:
        0 — no findings, or `severity == "advisory"`.
        1 — findings present and `severity == "blocking"`.
    """
    total = len(result.findings)
    files_with_hits = result.files_with_hits
    if files_with_hits is None:
        files_with_hits = len({f[0] for f in result.findings})

    if json_out:
        payload = {
            "tool": result.tool_name,
            "total": total,
            "files_with_hits": files_with_hits,
            "files_scanned": result.files_scanned,
            "severity": result.severity,
            "extra": result.extra,
        }
        print(json.dumps(payload))

    if total == 0:
        suffix = f" ({result.files_scanned} file(s) scanned)" if verbose else ""
        print(f"[{result.tool_name}] clean{suffix}")
        return 0

    unit_plural = f"{result.unit}(s)"
    print(f"[{result.tool_name}] {total} {unit_plural} across {files_with_hits} file(s)")
    return 0 if result.severity == "advisory" else 1


def cli_error(tool_name: str, message: str) -> int:
    """Standard tool-error exit (code 2). Use for missing inputs / config drift."""
    print(f"[{tool_name}] {message}", file=sys.stderr)
    return 2
