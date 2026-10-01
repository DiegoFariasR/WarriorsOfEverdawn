#!/usr/bin/env python
"""
Check that every AI-run Godot launch in dev.sh has a wall-clock
`timeout N` wrapper, an explicit `--quit` flag, or an `# allow-hang:
<reason>` opt-out comment. Copied from Everdawn, where a hung headless
run once sat for 15+ minutes before anyone noticed.

A launch is `"$GODOT" --headless ...`, `"${game[@]}" ...` (the array
`run_session` builds with --headless in it), or a `"$GODOT" ...` line
carrying `--ai-playtest` (the minimized-window screenshot run, which
can hang the same way).

Out of scope (deliberately):
- User-only launches (./dev.sh editor, run, host, join): foreground
  windows the user closes, which the headless_guard hook blocks for AI.

Usage:
    python Tools/check_godot_timeouts.py [--summary]

Exit codes:
    0 -- every AI-run launch is wrapped
    1 -- at least one unwrapped AI-run launch
"""
from __future__ import annotations

import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from _check_harness import make_argparser

ROOT = pathlib.Path(__file__).resolve().parent.parent
DEV_SH = ROOT / "dev.sh"

HEADLESS_PATTERN = re.compile(r'"\$GODOT"\s+--headless\b|\$GODOT\s+--headless\b|"\$\{game\[@\]\}"|"\$GODOT".*\s--ai-playtest\b')
# `local game=("$GODOT" --headless ...)` defines the launch; it does not start a process.
ARRAY_DEFINITION = re.compile(r'\w+=\(\s*"\$GODOT"')
OK_PATTERNS = (
    re.compile(r"\btimeout\s+(\d+\b|\"?\$\{?\w+\}?\"?)"),  # `timeout 120 "$GODOT" ...`, or a limit held in a variable
    re.compile(r"\s--quit(?![\w-])"),    # explicit engine-side quit (import path); not the game's --quit-after
)
ALLOW_HANG = re.compile(r"#\s*allow-hang:\s*\S")


def collect_logical_lines(text: str) -> list[tuple[int, str]]:
    """Join backslash-continuation lines into single logical lines, tagged with the start line number."""
    out: list[tuple[int, str]] = []
    buf: list[str] = []
    start_lineno = -1
    for lineno, raw in enumerate(text.splitlines(), 1):
        if start_lineno == -1:
            start_lineno = lineno
        stripped = raw.rstrip()
        if stripped.endswith("\\"):
            buf.append(stripped[:-1])
        else:
            buf.append(stripped)
            out.append((start_lineno, " ".join(buf)))
            buf = []
            start_lineno = -1
    if buf:
        out.append((start_lineno, " ".join(buf)))
    return out


def main(argv: list[str]) -> int:
    args = make_argparser("check-godot-timeouts", description=__doc__).parse_args(argv)
    summary_mode = args.summary
    text = DEV_SH.read_text(encoding="utf-8")
    logical = collect_logical_lines(text)

    issues: list[tuple[int, str]] = []
    for i, (lineno, joined) in enumerate(logical):
        if not HEADLESS_PATTERN.search(joined) or ARRAY_DEFINITION.search(joined):
            continue
        if any(p.search(joined) for p in OK_PATTERNS):
            continue
        if ALLOW_HANG.search(joined):
            continue
        # Opt-out comment may sit on the immediately-preceding logical line.
        if i > 0 and ALLOW_HANG.search(logical[i - 1][1]):
            continue
        issues.append((lineno, joined.strip()))

    if not issues:
        print("[check-godot-timeouts] clean")
        return 0

    if not summary_mode:
        for lineno, snippet in issues:
            print(f"dev.sh:{lineno}: AI-run Godot launch without 'timeout N' / '--quit' / '# allow-hang:' opt-out")
            print(f"  {snippet[:240]}")
    print(f"[check-godot-timeouts] {len(issues)} unwrapped --headless launch(es)")
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
