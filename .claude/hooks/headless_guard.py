"""PreToolUse(Bash) hook: block AI-initiated Godot launches that would open a visible window.

Rule: AGENTS.md "Never launch Godot with a visible window from AI automation".
Exit 2 blocks the call and feeds stderr back to Claude.
"""
import json
import re
import sys

GUI_DEV_COMMANDS = (
    "./dev.sh run",
    "./dev.sh editor",
    "./dev.sh host",
    "./dev.sh join",
)

RULE = 'See AGENTS.md "Never launch Godot with a visible window from AI automation".'


def main() -> int:
    try:
        command = json.loads(sys.stdin.buffer.read().decode("utf-8"))["tool_input"].get("command") or ""
    except (ValueError, KeyError, TypeError, AttributeError) as e:
        print(f"headless_guard: unreadable hook input ({e!r}); Godot launch rule NOT enforced for this call.", file=sys.stderr)
        return 1

    if "--headless" in command:
        return 0

    # taskkill names the Godot exe only to stop it; stripping those segments keeps cleanup commands usable.
    launchable = re.sub(r"taskkill[^;&|\n]*", "", command)

    if "Godot_v4" in launchable or "$GODOT" in launchable or "${GODOT" in launchable:
        print(
            f"BLOCKED: AI-initiated Godot launches MUST include --headless. {RULE} If the command only reads or "
            "searches a path containing Godot_v4, use the Read/Grep/Glob tools instead.",
            file=sys.stderr,
        )
        return 2

    if any(gui in launchable for gui in GUI_DEV_COMMANDS):
        print(f"BLOCKED: ./dev.sh run/editor/host/join opens a GUI window when AI-initiated. {RULE}", file=sys.stderr)
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
