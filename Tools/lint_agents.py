#!/usr/bin/env python3
"""
Lint .claude/agents/*.md, .claude/skills/*/SKILL.md and .claude/tools-index.md for stale references.
Copied from Everdawn; skills are linted here too, since most of them were adapted from Everdawn's and
a leftover Everdawn command or path is exactly the drift this catches.

Checks (per agent file; skills and the index get checks 2-6):
  1. Frontmatter has required keys (name, description, tools, model) on agent files.
  2. Every `./dev.sh <subcommand>` token resolves to a real subcommand (parsed from `./dev.sh help`).
  3. Every `Tools/<script>.py` reference exists on disk.
  4. Every repo-relative path in backticks (Docs/, Core/, Core.Tests/, GodotClient/, Tools/) exists.
  5. Every cross-reference to another agent (by name) resolves to an existing agent file.
  6. Every `/<skill-name>` reference resolves to .claude/skills/<name>/SKILL.md (only validates names
     that look like project skills, to avoid false positives on URL paths).

Cross-file checks (roster sync):
  7. Every agent .md (except README) appears in .claude/agents/README.md decision table.
  8. Every name-like entry in README.md's decision table refers to an existing agent .md.
  9. AGENTS.md's "Subagent routing" hardcoded roster list (the `specialized subagents (...)` parenthetical) matches the actual set of agent files.

Description-quality checks (per agent file):
  10. Description contains an explicit "Do NOT" / "do not use" / "not for" clause (negative scope).
  11. Description contains a proactive-trigger phrase ("Use proactively", "Use when", "MUST BE USED").

Memory index sync:
  12. Every .claude/memory/*.md file appears in both lists in MEMORY.md
      (summary link under "## Memory Files" AND @-import under "## Full content").

Exit code: 0 if clean, 1 if any issue found, 2 on configuration error.

Usage:
  python Tools/lint_agents.py
"""

from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _check_harness import ToolReport, make_argparser, report  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
AGENTS_DIR = ROOT / ".claude" / "agents"
AGENTS_README = AGENTS_DIR / "README.md"
MEMORY_DIR = ROOT / ".claude" / "memory"
MEMORY_INDEX = MEMORY_DIR / "MEMORY.md"
SKILLS_DIR = ROOT / ".claude" / "skills"
TOOLS_INDEX = ROOT / ".claude" / "tools-index.md"
PROJECT_AGENTS_MD = ROOT / "AGENTS.md"

REQUIRED_FRONTMATTER_KEYS = {"name", "description", "tools", "model"}

# Skill and agent names this repo has or Everdawn has. Only these are checked when referenced, so URL paths
# and prose like "/s" don't false-positive; including Everdawn's catches references copied over from there.
KNOWN_SKILL_NAMES = {
    "auto-maintain", "refactor", "polish", "close-chat", "smoke", "commit", "resolve-blockers",
    "audit-pending-refactors", "dead-code-sweep", "doc-drift", "spec-vs-impl", "test-coverage-gap",
    "polish-godot", "ui-iterate", "ui-review", "ui-tournament", "godot-iterate", "outside-view",
    "investigate-report", "translation-review", "ai-playtest", "balance-review", "render-portrait",
    "generate-icon", "name-it", "update-id", "opus-trigger", "godot",
}
KNOWN_AGENT_NAMES = {
    "core-engineer", "godot-engineer", "polish-engineer",
    "battle-balancer", "content-author", "scenario-writer", "gamecore-engineer", "godot-presenter",
    "locale-fixer", "report-investigator", "asset-polisher", "ui-iterator", "design-critic",
    "scene-critic", "uv-remapper",
}

DO_NOT_PHRASES = (
    "do not use",
    "do not delegate",
    "don't use",
    "not for",
    "do NOT use",
)
PROACTIVE_PHRASES = (
    "use proactively",
    "use when",
    "use for",
    "must be used",
)

PATH_PREFIX_RE = re.compile(
    r"`((?:Docs|Core|Core\.Tests|GodotClient|Tools)/[\w./()\-+]+?)`"
)
DEV_SH_RE = re.compile(r"\./dev\.sh\s+([a-z][a-z0-9-]*)")
TOOLS_PY_RE = re.compile(r"`?(Tools/[\w._\-]+\.py)`?")
SKILL_REF_RE = re.compile(r"`?/([a-z][\w-]+)\b")


def parse_dev_sh_commands() -> set[str] | None:
    dev_sh = ROOT / "dev.sh"
    if not dev_sh.exists():
        return None
    try:
        proc = subprocess.run(
            ["bash", str(dev_sh), "help"],
            capture_output=True,
            text=True,
            timeout=15,
            cwd=str(ROOT),
        )
    except Exception:
        return None
    cmds: set[str] = set()
    for line in (proc.stdout + proc.stderr).splitlines():
        m = re.match(r"\s{2,}([a-z][a-z0-9-]+)\b", line)
        if m:
            cmds.add(m.group(1))
    cmds.add("help")
    return cmds or None


def parse_frontmatter(text: str) -> dict[str, str] | None:
    if not text.startswith("---"):
        return None
    end = text.find("\n---", 3)
    if end < 0:
        return None
    block = text[3:end].strip()
    result: dict[str, str] = {}
    for line in block.splitlines():
        if ":" in line:
            k, _, v = line.partition(":")
            result[k.strip()] = v.strip()
    return result


def lint_file(
    path: Path,
    dev_sh_cmds: set[str] | None,
    agent_names: set[str],
    skill_names: set[str],
    require_frontmatter: bool,
) -> list[str]:
    text = path.read_text(encoding="utf-8")
    errors: list[str] = []

    if require_frontmatter:
        fm = parse_frontmatter(text)
        if fm is None:
            errors.append("missing or malformed YAML frontmatter")
        else:
            missing = REQUIRED_FRONTMATTER_KEYS - fm.keys()
            if missing:
                errors.append(f"frontmatter missing keys: {sorted(missing)}")
            declared_name = fm.get("name", "")
            if declared_name and declared_name != path.stem:
                errors.append(
                    f"frontmatter name '{declared_name}' does not match filename '{path.stem}'"
                )
            description = fm.get("description", "").lower()
            if description and not any(p in description for p in DO_NOT_PHRASES):
                errors.append(
                    "description missing negative scope clause "
                    "('Do NOT use', 'not for', etc.) -- routing risk"
                )
            if description and not any(p in description for p in PROACTIVE_PHRASES):
                errors.append(
                    "description missing proactive-trigger phrase "
                    "('Use proactively', 'Use when', 'MUST BE USED')"
                )

    if dev_sh_cmds is not None:
        for m in DEV_SH_RE.finditer(text):
            cmd = m.group(1)
            if cmd not in dev_sh_cmds:
                errors.append(f"unknown ./dev.sh subcommand: '{cmd}'")

    for m in TOOLS_PY_RE.finditer(text):
        rel = m.group(1)
        if not (ROOT / rel).exists():
            errors.append(f"missing Tools file: {rel}")

    for m in PATH_PREFIX_RE.finditer(text):
        rel = m.group(1).rstrip("/")
        full = ROOT / rel
        if not full.exists():
            errors.append(f"missing path: {rel}")

    for m in SKILL_REF_RE.finditer(text):
        candidate = m.group(1)
        if candidate in skill_names:
            continue
        if candidate.lower() in KNOWN_SKILL_NAMES:
            errors.append(
                f"unknown skill reference: '/{candidate}' "
                f"(no .claude/skills/{candidate}/SKILL.md)"
            )

    for m in re.finditer(r"`([a-z][a-z0-9-]{2,})`", text):
        name = m.group(1)
        if name in KNOWN_AGENT_NAMES and name not in agent_names:
            errors.append(f"references agent '{name}' but no .claude/agents/{name}.md exists")

    return list(dict.fromkeys(errors))


def lint_memory_index_sync() -> list[str]:
    """Every .claude/memory/*.md file must appear in both lists in MEMORY.md."""
    errors: list[str] = []
    if not MEMORY_INDEX.exists() or not MEMORY_DIR.is_dir():
        return errors
    index_text = MEMORY_INDEX.read_text(encoding="utf-8")
    memory_files = [p for p in MEMORY_DIR.glob("*.md") if p.name != "MEMORY.md"]
    for path in sorted(memory_files):
        link_pattern = f"({path.name})"
        import_pattern = f"@.claude/memory/{path.name}"
        if link_pattern not in index_text:
            errors.append(
                f"MEMORY.md missing summary link for {path.name} "
                f"(under '## Memory Files')"
            )
        if import_pattern not in index_text:
            errors.append(
                f"MEMORY.md missing @-import for {path.name} "
                f"(under '## Full content')"
            )
    return errors


def lint_roster_sync(agent_names: set[str]) -> list[str]:
    """Cross-file roster checks: agents <-> README decision table <-> AGENTS.md "Subagent routing" roster."""
    errors: list[str] = []

    if AGENTS_README.exists():
        readme_text = AGENTS_README.read_text(encoding="utf-8")
        for name in agent_names:
            tick_name = f"`{name}`"
            if tick_name not in readme_text and name not in readme_text:
                errors.append(
                    f"agent '{name}' has a .md file but no row in .claude/agents/README.md"
                )
        for m in re.finditer(r"`([a-z][a-z0-9-]{2,})`", readme_text):
            candidate = m.group(1)
            if candidate.endswith("er") or candidate.endswith("or") or "-" in candidate:
                looks_like_agent = candidate in KNOWN_AGENT_NAMES or candidate in agent_names
                if looks_like_agent and candidate not in agent_names:
                    errors.append(
                        f"README references agent '{candidate}' but no "
                        f".claude/agents/{candidate}.md exists"
                    )

    if PROJECT_AGENTS_MD.exists():
        text = PROJECT_AGENTS_MD.read_text(encoding="utf-8")
        m = re.search(r"specialized subagents \(([^)]+)\)", text)
        if m:
            listed = {n.strip() for n in m.group(1).split(",")}
            missing_from_roster = agent_names - listed
            if missing_from_roster:
                errors.append(
                    f"AGENTS.md 'Subagent routing' roster missing agents: {sorted(missing_from_roster)}"
                )
            extra_in_roster = listed - agent_names
            if extra_in_roster:
                errors.append(
                    f"AGENTS.md 'Subagent routing' roster references nonexistent agents: "
                    f"{sorted(extra_in_roster)}"
                )

    return errors


def main() -> int:
    args = make_argparser("lint-agents", description=__doc__).parse_args(sys.argv[1:])

    if not AGENTS_DIR.is_dir():
        print(f"error: {AGENTS_DIR} not found", file=sys.stderr)
        return 2

    dev_sh_cmds = parse_dev_sh_commands()
    if dev_sh_cmds is None:
        print(
            "warn: could not parse './dev.sh help' output; "
            "skipping subcommand validation",
            file=sys.stderr,
        )

    agent_files = sorted(p for p in AGENTS_DIR.glob("*.md") if p.stem != "README")
    agent_names = {p.stem for p in agent_files}
    skill_names = (
        {p.name for p in SKILLS_DIR.iterdir() if p.is_dir()}
        if SKILLS_DIR.is_dir()
        else set()
    )

    findings_tuples: list[tuple[str, int, str]] = []
    files_with_issues = 0
    files_scanned = 0

    for f in agent_files:
        files_scanned += 1
        errs = lint_file(f, dev_sh_cmds, agent_names, skill_names, require_frontmatter=True)
        if errs:
            files_with_issues += 1
            rel = str(f.relative_to(ROOT))
            print(f"\n{rel}:")
            for e in errs:
                print(f"  - {e}")
                findings_tuples.append((rel, 0, e))

    for skill_md in sorted(SKILLS_DIR.glob("*/SKILL.md")) if SKILLS_DIR.is_dir() else []:
        files_scanned += 1
        errs = lint_file(skill_md, dev_sh_cmds, agent_names, skill_names, require_frontmatter=False)
        if errs:
            files_with_issues += 1
            rel = str(skill_md.relative_to(ROOT))
            print(f"\n{rel}:")
            for e in errs:
                print(f"  - {e}")
                findings_tuples.append((rel, 0, e))

    readme = AGENTS_DIR / "README.md"
    if readme.exists():
        files_scanned += 1
        errs = lint_file(readme, dev_sh_cmds, agent_names, skill_names, require_frontmatter=False)
        if errs:
            files_with_issues += 1
            rel = str(readme.relative_to(ROOT))
            print(f"\n{rel}:")
            for e in errs:
                print(f"  - {e}")
                findings_tuples.append((rel, 0, e))

    if TOOLS_INDEX.exists():
        files_scanned += 1
        errs = lint_file(
            TOOLS_INDEX, dev_sh_cmds, agent_names, skill_names, require_frontmatter=False
        )
        if errs:
            files_with_issues += 1
            rel = str(TOOLS_INDEX.relative_to(ROOT))
            print(f"\n{rel}:")
            for e in errs:
                print(f"  - {e}")
                findings_tuples.append((rel, 0, e))

    roster_errs = lint_roster_sync(agent_names)
    if roster_errs:
        files_with_issues += 1
        print("\nroster sync:")
        for e in roster_errs:
            print(f"  - {e}")
            findings_tuples.append(("<roster-sync>", 0, e))

    memory_errs = lint_memory_index_sync()
    if memory_errs:
        files_with_issues += 1
        print("\nmemory index sync:")
        for e in memory_errs:
            print(f"  - {e}")
            findings_tuples.append(("<memory-index-sync>", 0, e))

    if findings_tuples:
        print()  # blank separator before harness footer

    tool_report = ToolReport(
        tool_name="lint-agents",
        findings=findings_tuples,
        files_scanned=files_scanned,
        files_with_hits=files_with_issues,
        unit="issue",
        severity="blocking",
    )
    return report(tool_report, json_out=args.json, verbose=args.verbose)


if __name__ == "__main__":
    sys.exit(main())
