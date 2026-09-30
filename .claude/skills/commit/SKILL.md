---
name: commit
description: Commit all uncommitted changes to main via a subagent so the diff doesn't bloat main context. Local only -- this repo has no remote, so nothing is pushed.
argument-hint: "[optional why/context to seed the commit message]"
disable-model-invocation: true
user-invocable: true
---

# /commit

Delegates the commit to a `general-purpose` subagent so the big `git diff` reads stay out of main context. The pre-commit hook (`.git/hooks/pre-commit`) formats staged C# and runs `Core.Tests` when Core changed. Ported from Everdawn's `/commit`, minus the push: this repository is local only.

## What to do when invoked

1. Acknowledge in one short sentence ("Delegating the commit to a subagent.").
2. Launch ONE `general-purpose` subagent with `model: "sonnet"` and the prompt below. Pass `$ARGUMENTS` verbatim under "User context" (empty when none provided), and paste the `Co-Authored-By` trailer from this session's commit attribution guidance under "Trailer" — the subagent can't see it otherwise.
3. Do NOT run `git status` / `git diff` / `git log` in the main session -- that defeats the skill.
4. Relay the subagent's final report verbatim.

## Subagent prompt

> Commit all uncommitted changes to `main` in `C:\Users\Diego\Projects\WarriorsOfEverdawn`. The repository has no remote: do not push, do not add a remote.
>
> **User context (verbatim, may be empty):** $ARGUMENTS
>
> **Trailer:** <the Co-Authored-By line from the main session>
>
> **Procedure:**
>
> 1. `git rev-parse --abbrev-ref HEAD` must return `main` (on a repository with no commits yet, `git symbolic-ref --short HEAD`). If not, abort with "Not on main; aborting." Never switch branches.
> 2. Run `git status` and `git diff --stat`. If both report a clean tree, return "Working tree clean -- nothing to commit" and exit.
> 3. `git log --oneline -10` (skip on a repository with no commits) to match the recent commit-message style.
> 4. Read enough of `git diff` to draft an accurate message. Sample the most informative files first; do NOT exhaust a huge diff once the theme is clear from `--stat` plus a few file diffs.
> 5. Draft the commit message:
>    - First line under 70 chars; capture the dominant theme, in the shape recent commits use.
>    - Blank line, then a bulleted body grouping changes by theme. Cite pending-refactor entry numbers (`#3`) when the diff lands one.
>    - End with the trailer.
>    - When `User context` is non-empty, fold its "why" into the body -- don't paste it verbatim.
>    - No emojis.
> 6. `git add -A`. If any candidate file looks sensitive (`.env`, `*.key`, `credentials.json`, etc.), abort and name the file -- don't commit. Binary assets go through Git LFS per `.gitattributes`; if `git lfs` is not installed, abort and say so rather than committing binaries as plain blobs.
> 7. Commit with a HEREDOC message. If the pre-commit hook fails, report the failure and STOP. Never `--no-verify`, never `--amend`.
> 8. Return in <= 100 words: new commit hash, the first line of the message, the hook result (e.g. "format clean, 72 tests passed"), and any warnings worth surfacing.
>
> **Forbidden:** push, adding a remote, branch creation, branch switching, `--amend`, `--force`, `--no-verify`, `--no-gpg-sign`, editing files the user didn't already modify.
