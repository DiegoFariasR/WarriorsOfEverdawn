# Headless Launch Rules (Rationale)

The hard rules live in the root [AGENTS.md](../../AGENTS.md) "CRITICAL EXECUTION RULES" section. This doc carries the *why* and the list of already-correct launchers, so the rationale doesn't burn always-loaded context. Carried over from Everdawn, where both rules were learned the hard way.

## Why `--headless` is mandatory

AI runs in the background while the user is doing real work in other windows. A spawned Godot window steals focus, minimizes whatever they were on, and pulls the cursor. In Everdawn this happened: one unattended maintenance run opened nine game windows over the user's work.

Defense in depth:

1. **Launchers pass `--headless` themselves**: `run_session` (net-test, pvp-test), `camera_test`, `import`.
2. **`.claude/hooks/headless_guard.py` blocks the rest**: `./dev.sh run`, `editor`, `host`, `join`, and direct Godot executable calls without `--headless` or `--ai-playtest`.
3. **`.claude/memory/feedback_godot_headless.md`** reinforces it in-session.

If you add a new AI-callable Godot launcher (a `dev.sh` command, a Python `subprocess` call, anything), build `--headless` into it from day one, or `--ai-playtest` plus an off-screen `--position` when it has to draw.

## Why `timeout N` wraps every AI-run launch

Godot 4's `--headless` uses the dummy renderer. Paths that need a real renderer (reading back a viewport image, for one) fail inside a callback before the game reaches its quit, and the engine then sits forever. Everdawn lost 15+ minutes to exactly this. A network test adds another way to hang: a client that never connects waits for a host that is gone.

Hence the rule: every AI-run launch in `dev.sh` (`--headless`, or `--ai-playtest` for screenshots, which can hang the same way) is wrapped with `timeout N`, or carries an explicit `--quit` flag, or a `# allow-hang: <reason>` opt-out on the same or preceding line. `./dev.sh check-godot-timeouts` (also part of `./dev.sh health`) lints for unwrapped launches. The game's own `--quit-after N` does not count: it runs inside the game, so a hang before it is reached is exactly what the wrapper is for.

## Already-correct launchers

- `dev.sh import` (timeout 300, and `--quit`)
- `dev.sh net-test` and `dev.sh pvp-test` (`run_session`: timeout 90 per process)
- `dev.sh camera-test` (timeout 60)
- `dev.sh smoke` (runs the three above)
- `dev.sh playtest` (`run_session`: each process limited to the session length plus 60 s; with `--screenshots` the host runs as `--ai-playtest` off-screen instead of headless)
- `dev.sh screenshot` (timeout 120; `--ai-playtest` window, off-screen at `--position -10000,-10000`)

## The one exception: screenshots

Headless runs draw nothing, so checking how something looks needs the real renderer and a window. `./dev.sh screenshot` uses Everdawn's way to keep that window out of the user's way: it opens off-screen (`--position -10000,-10000`), and `--ai-playtest` minimizes it without taking focus as soon as the game starts. Captures still come out as full frames at the window's size. Use it only when the question is what the game draws; everything measurable belongs in the headless self-tests.
