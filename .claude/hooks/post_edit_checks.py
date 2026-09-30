"""PostToolUse(Edit|Write) hook: remind Claude which check an edited file needs before reporting done.

Only hookSpecificOutput.additionalContext reaches Claude from a PostToolUse hook; plain stdout/stderr on exit 0 does not.
"""
import json
import sys

# First match wins, so the self-test entry must precede the general GodotClient one.
CHECKS = (
    ("/Core/", ".cs", "Core C# edited -- run ./dev.sh test and ./dev.sh format before reporting done."),
    ("/Core.Tests/", ".cs", "Core.Tests edited -- run ./dev.sh test and ./dev.sh format before reporting done."),
    ("/GodotClient/scripts/Dev/", ".cs",
     "Self-test edited -- make sure every [*-check] line it prints has its gate in dev.sh, then run ./dev.sh smoke."),
    ("/GodotClient/", ".cs",
     "GodotClient C# edited -- run ./dev.sh build, ./dev.sh format and the self-test that covers it "
     "(./dev.sh smoke runs all three) before reporting done."),
    ("/dev.sh", "dev.sh", "dev.sh edited -- run ./dev.sh check-godot-timeouts, and the command you changed."),
    ("/.claude/", ".md", ".claude doc edited -- run ./dev.sh lint-agents before reporting done."),
)


def main() -> int:
    try:
        path = json.loads(sys.stdin.buffer.read().decode("utf-8"))["tool_input"].get("file_path") or ""
    except (ValueError, KeyError, TypeError, AttributeError) as e:
        print(f"post_edit_checks: unreadable hook input ({e!r}); no check reminder for this edit.", file=sys.stderr)
        return 1

    path = path.replace("\\", "/")
    for segment, suffix, hint in CHECKS:
        if segment in path and path.endswith(suffix):
            print(json.dumps({"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": hint}}))
            break
    return 0


if __name__ == "__main__":
    sys.exit(main())
