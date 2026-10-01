---
name: close-chat
description: End-of-conversation sweep — extract memories, skill/tool candidates, and refactor opportunities spotted during the session and save each to the right place.
argument-hint: "(no args)"
disable-model-invocation: true
user-invocable: true
---

# Close Chat

Run this before ending a work session to capture everything worth keeping that wouldn't otherwise survive. Scan the full conversation for nine categories plus a session-meta delegation summary, present findings concisely, then save each to its canonical location.

## What to look for

Pass through the conversation once with each lens below. Only capture things that would be lost otherwise — information already in CLAUDE.md, git history, project docs, or existing memory is not worth re-saving.

### 1. Memory-worthy notes (behavioral only)

Memory is reserved for **cross-conversation behavioral preferences** — how to collaborate with this user, what to avoid, what to repeat. It is NOT for project-specific facts.

| Type | Use for | Save to memory? |
|---|---|---|
| `user` | User's role, expertise, collaboration preferences | Yes |
| `feedback` | Corrections ("don't do X"), validated non-obvious choices ("yes, that was right") | Yes |
| `project` | Decisions, design rules, system behaviors, authoring gotchas | **No — put in project docs** |
| `reference` | Dashboards, issues, external channels | **No — put in project docs** |

**Project-specific knowledge belongs in the project.** Before saving anything as a `project` or `reference` memory, ask: could this fit in `Docs/Design/*.md`, `AGENTS.md`, `Core/AGENTS.md`, `GodotClient/AGENTS.md`, `CLAUDE.md`, or an inline code comment? If yes, put it there — project docs are discoverable by repo-wide search, survive in PR review, stay visible to anyone not using auto-memory, and get updated alongside the code they describe. Memory is siloed per-assistant and rots silently when code changes.

Ignore: ephemeral task details, anything derivable from the current code, anything already in an existing memory file or project doc.

**Common drift to watch for** — patterns that have needed correction before, check specifically:

- **New CLI flag / workflow affordance landed without a doc update.** If the session added a launch flag, `./dev.sh` subcommand, or other developer-facing affordance, verify it was documented in the relevant places: `GodotClient/AGENTS.md` "Launch flags" (if Godot-facing), the root `AGENTS.md` "Commands" table, `.claude/tools-index.md`, `.claude/skills/<skill>/SKILL.md` (if any skill's procedure uses the same entry point), or an inline example in the nearest doc.
- **New self-test check without its gate or doc line.** A `[*-check]` line printed by `NetSelfTest` / `CameraSelfTest` needs a gate in `dev.sh`, a mention in the `AGENTS.md` command row, and the design doc's "Verified by" paragraph. The user has had to explicitly ask "note this in the skill" after shipping a feature — proactively bundling the doc update with the feature avoids the second round-trip.

### 2. Skill candidates

A multi-step process the user walked through that worked well and is likely to repeat. Look for sequences where the user issued a series of commands or instructions that could be scripted into a single named entry point.

Signal: "we did X, then Y, then Z" — and the sequence is reusable, not task-specific.

### 3. Tool candidates

A manual step that was tedious or error-prone and could be a script/CLI entry. Log parsing, batch file renames, asset conversion, custom diff, etc.

Signal: the user (or you) hand-rolled shell plumbing or copy-pasted output through multiple commands to produce a useful result.

### 4. Tool improvements

An *existing* tool (`Tools/*.py` or `./dev.sh` subcommand) that worked but was clumsy, missing a flag, slow on a real-world path, had a confusing default, or has a known edge case the session bumped into. Distinct from tool *candidates* (which propose a new script) — this is about tightening what we already have.

Signals:
- You (or the user) hand-rolled shell plumbing around an existing tool because it didn't quite output what was needed.
- A tool printed too much / too little / the wrong shape, forcing a second pass.
- A tool's failure mode was unhelpful (silent fallback, generic error, unclear what to fix).
- A `--flag` or `--format` would have saved an obvious back-and-forth.

Save policy: clear wins auto-save (edit the script, add the flag, fix the error message). Anything that changes existing output contract or default behaviour gets surfaced and asked first — downstream callers may depend on the current shape.

### 5. Refactor opportunities

Code patterns noticed during the session that were out of scope for the immediate task but deserve a dedicated pass later.

Signal: "I saw X but skipped it because Y." Parked observations, half-baked cleanup ideas, architectural smells (god-object files, duplicated tables, inconsistent patterns across sibling code).

### 6. Subagent candidates

A repeated workflow pattern that fits a specific domain not yet covered by an existing subagent (see [.claude/agents/README.md](../../agents/README.md) for the current roster). Unlike skills (which the user invokes) and tools (one-off scripts), subagents wrap a multi-step domain with its own context window, definition-of-done checklist, and tool grants -- they're worth the design effort when a workflow recurs and benefits from isolated context.

Signals:
- The session repeated a domain-specific sequence (survey -> edit -> verify) that doesn't match any existing agent's trigger description.
- The work was hand-routed because no agent owned it -- main context absorbed the survey output, context budget noticeably hurt.
- The domain has its own definition of done (a self-test, a measurement, a generated file) that should live next to the workflow, not in CLAUDE.md.

Before proposing, check the README decision table for an existing agent that could absorb the workflow with a description tweak -- modifying an existing agent is cheaper than adding one. Adding an agent has a routing-mutex cost: every new agent must have a trigger that doesn't overlap any other agent, or the main router can't pick correctly.

Subagent proposals always require explicit confirmation before saving (same flow as memory). Each proposal should include: kebab-case name, one-line trigger description, explicit Do-NOT scope, why an existing agent doesn't fit, and proposed model (Sonnet unless the work is high-stakes architecture).

### 7. Subagent improvements

An *existing* subagent definition that proved insufficient during the session — DoD checklist missed a step, workflow didn't account for a real edge case, scope was ambiguous and the main agent had to guess, or the description's Do-NOT clause wasn't strong enough to keep work out. Distinct from subagent *candidates* (propose a new agent) — this is about tightening what we already have so the next session doesn't repeat the gap.

Signals:
- The agent reported done but the user found a missed step that should have been a DoD item.
- The agent's workflow assumed a tool/file that doesn't exist or doesn't behave the way the workflow describes.
- The agent absorbed work that should have been routed elsewhere (or vice versa) because the description was ambiguous.
- The agent referenced a stale doc / skill / command (the linter catches *broken* references but not *outdated procedural advice*).

Save policy: clear wins to the agent's `.md` auto-save (add a DoD bullet, tighten a Do-NOT clause, correct a stale procedural step). Anything that changes the agent's *scope* (description trigger, what it owns vs. doesn't) surfaces and asks first — scope changes ripple through the README decision table and may overlap other agents. Run `./dev.sh lint-agents` after any edit.

### 8. Revert candidates (speculative fixes still in tree)

During a bug hunt, multiple speculative or pre-emptive fixes often land across different layers before the root cause is identified. Once the real fix is in place, some earlier fixes become **dead weight** — they no longer address a real problem but still pay ongoing cost (added complexity, behavioural drift, dead time, redundant code paths). They tend to stay in the tree silently unless someone asks "is this still needed?"

For each non-trivial fix that landed during the session, ask:
- **What hypothesis** did this fix address?
- **Did the final root-cause fix** make that hypothesis obsolete?
- **Does the fix still earn its keep** on its own merits, or is it now redundant?

Signals:
- The session touched **multiple layers** (e.g. Core + client script + self-test gate) for one bug — at most one of those layers is usually the real culprit; the others may have got speculative band-aids.
- A fix was added "to be safe", "as defensive timing", or "in case the real fix doesn't fully solve it." That phrasing is a tell.
- The investigation pivoted mid-session ("turns out the bug is actually in X, not Y") — fixes applied before the pivot are prime candidates.
- The fix has no test guarding it because it was speculative — only the post-pivot fix has the regression test.

Unlike refactors, these are **immediate revert candidates**, not parked future work. List them in the closing report; the user decides per item whether to keep, revert, or carry forward. **Do NOT auto-revert** — leave it explicit.

A canonical example: a bug presents as "a self-test gate fails now and then." Initial fix: loosen the gate's threshold. Continued investigation reveals the measurement itself counted the wrong frames. After the measurement fix, the loosened threshold is solving nothing — but it now lets a real regression through. Revert candidate (tighten the gate back).

### 9. Open topics

Pending offers, unanswered questions, and planned-but-not-started work that will silently drop when the session ends.

Signals:
- An assistant turn ended with "Want me to...", "Should I...", or a pick-your-option question that the user did not answer (they pivoted, replied with `/close-chat`, or moved on).
- A design doc landed in `Docs/Design/` but none of its phases were started.
- A multi-phase plan was agreed to but only some phases shipped.
- A bug was diagnosed but not fixed (or fixed in one place, parked elsewhere).
- WIP comments / `[pending]` todos still on the active todo list at session end.

Unlike the other lenses, **do not auto-save open topics anywhere.** List them under an "Open topics" section of the closing report and let the user decide per item: ignore, carry forward verbally next session, or convert into a refactor entry / design-doc update / memory. Only save once the user picks a destination.

## Procedure

1. **Scan** the full conversation through all nine lenses. List candidates as a short report to the user:
   - Category, one-line description, proposed save location (memories, subagent candidates, revert candidates, and open topics: no save location, just list as proposals).
   - Flag anything uncertain with a question rather than guessing.

2. **Session meta — delegation summary.** Before reporting, tally file-paths touched by domain (`Core/` + `Core.Tests/` C#, `GodotClient/` C# and scenes, `dev.sh` + `Tools/`, docs) and list which subagents the session actually spawned. If a code domain had three or more touches without its specialist being spawned (e.g. 5 edits under `GodotClient/scripts/` and `godot-engineer` was never invoked), surface that as a **delegation gap** in the closing report — one line per gap. Not a save category; the gap is feedback for the user's next session, not a writeable artifact. Look for the *opposite* too: an agent was spawned for a trivial one-line edit (delegation overhead), worth flagging so the user re-calibrates when to route.

3. **Memories and subagent proposals always require explicit confirmation before saving.** List each candidate with its proposed name, type, and one-line rationale, then wait for the user to approve (per item or all). Subagent proposals also need explicit Do-NOT scope and the existing-agent overlap check. Bias toward proposing less with higher quality — one sharp memory beats three fuzzy ones, one well-scoped agent beats three near-duplicates, and the user is the final arbiter of which patterns are worth carrying forward.

4. **For all other categories, save clear wins immediately and ask only on ambiguous ones.** Refactor entries, project-doc additions, skills, tools (new + improvements), and subagent improvements land without confirmation when the case is obvious. Subagent improvements that change *scope* (description trigger, what the agent owns) always ask first — scope changes ripple through the README decision table. Revert candidates, open topics, and delegation gaps are listed only — never auto-saved or auto-reverted.

4. **Save each to its canonical location:**

   | Kind | Location |
   |---|---|
   | Memory (behavioral only) | `.claude/memory/<file>.md` + **two** updates to `.claude/memory/MEMORY.md`: (a) a one-line summary entry under `## Memory Files`, and (b) an `@.claude/memory/<file>.md` line under `## Full content` so the new memory auto-loads on every session (including web). The auto-memory dir at `~/.claude/projects/<encoded-repo-path>/memory/` is junction'd here via `python Tools/setup_memory_junction.py`, so writes from both Claude Code and `/close-chat` land in the repo |
   | Project fact (system rules, architecture) | Append to the matching section in `AGENTS.md` (project) / `Core/AGENTS.md` / `GodotClient/AGENTS.md` (per-scope) / the area's `Docs/Design/*.md` |
   | Project fact (design decision, open question) | New or existing file under `Docs/Design/*.md` |
   | Project fact (AI-facing conventions) | `AGENTS.md` (root or `GodotClient/AGENTS.md`) / `CLAUDE.md` |
   | Code invariant / provisional mechanism | Inline comment at the implementation site |
   | Skill | `.claude/skills/<name>/SKILL.md` with project-standard frontmatter (name, description, argument-hint, disable-model-invocation, user-invocable) |
   | Tool | `Tools/<name>.py` (or a `dev.sh` function). If it deserves a shortcut, wire the command in `dev.sh` and follow `Docs/ops/registering-tools.md` |
   | Subagent | `.claude/agents/<name>.md` with frontmatter (name, description with trigger + Do-NOT scope, tools, model); plus a row in `.claude/agents/README.md` decision table and the `AGENTS.md` roster; run `./dev.sh lint-agents` after writing to verify references resolve |
   | Refactor | Append to `Docs/Design/Refactor/pending-refactors.md` — one numbered section per item with rationale, proposal, and risk. Create the file if missing |

5. **Report** the final list of saved files with paths. Link them as markdown so the user can click through to verify. List confirmed-but-pending memories separately so it's clear what's still waiting for approval.

## Rules

- **Do not commit** anything created by this skill unless the user explicitly asks. Save to disk, stop.
- **Do not invent findings.** If a category produced nothing, say so — "no memory-worthy items this session" is a valid outcome.
- **Never save memories without explicit confirmation.** Propose them with name + type + one-line rationale and wait. Other categories (refactor entries, project docs, skills, tools) save on clear wins without asking.
- **Project docs beat memory.** Default project-specific knowledge to a project file. Only fall back to memory when the information is purely behavioral (how to collaborate) and has no natural home in the repo.
- **Check existing memories AND project docs before writing.** Update an existing entry rather than creating a parallel one. If a memory duplicates a project doc, delete the memory.
- **Keep memory entries short.** Lead with the rule/fact, then `**Why:**` and `**How to apply:**` for feedback types (see `CLAUDE.md` auto-memory section for the full spec).
- **Keep project-doc additions in-voice.** Match the surrounding doc's tone and format (bullet style, heading level, terse vs. prose). Don't bolt on a new section header if a bullet in an existing section works.
- **Refactor entries must be independent.** Each numbered section should be actionable on its own — one PR per item.
- **Skill/tool frontmatter must match the project's conventions.** Look at an existing skill (`commit`, `smoke`) for the exact shape before writing a new one.
- **Subagents must pass the routing-mutex test.** Before writing, walk `.claude/agents/README.md` decision table and confirm the new trigger description does not overlap any existing row. After writing, run `./dev.sh lint-agents` -- it catches stale `./dev.sh` / `Tools/*.py` / repo-path references, wrong frontmatter, and filename-vs-name drift.

## Example closing report

```
Saved this session (clear wins, no confirmation needed):
- 1 system rule (skeletons test hits against the last reported player position) -> Docs/Design/combat.md
- 1 self-test convention (print the expectation next to the measurement) -> GodotClient/AGENTS.md
- 0 skills
- 0 tools
- 1 refactor item -> Docs/Design/Refactor/pending-refactors.md

Delegation summary:
- 6 edits under GodotClient/scripts/ this session; godot-engineer never spawned. Gap.
- core-engineer spawned once for a one-constant change. Overhead -- could have stayed in main agent.

Memory candidates (need your OK to save):
- feedback memory: "A green headless number is not what the user sees" -- the head scale read 0.75 in logs while heads looked unchanged. Save as memory/feedback_headless_numbers_vs_screen.md?

Tool improvements (saved):
- dev.sh gate: now fails when the check line is missing instead of reading every field as 0.

Revert candidates (not auto-reverted -- decide per item):
- The loosened spin-ratio threshold from before the fade-out frames were excluded; the measurement fix made it unnecessary.

Open topics (not saved -- decide per item):
- Asked whether a dash should make the player briefly invulnerable; no answer yet.

Nothing committed -- run /commit if you want to land what's saved.
```
