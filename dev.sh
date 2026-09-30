#!/usr/bin/env bash
set -uo pipefail

GODOT="${GODOT:-C:/Program Files/Godot_v4.7.2/Godot_v4.7.2-stable_mono_win64.exe}"
ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$ROOT/GodotClient"

usage() {
    cat <<'EOF'
Usage: ./dev.sh <command>

Build and test:
  build             Build the solution (Core, Core.Tests, GodotClient)
  test              Run Core.Tests
  format            dotnet format the solution (Core, Core.Tests, GodotClient)
  import            Headless asset import (run after copying assets in)

Godot self-tests (headless):
  net-test          Co-op host + 2 bot clients; asserts movement, combat, visuals and HUD reach every peer
  pvp-test          PvP host + 1 bot client, no skeletons; asserts players hit and damage each other
  camera-test       In every camera mode, W must move up the screen and D right; HUD sits on screen
  smoke             camera-test, net-test and pvp-test in turn; fails if any fails

Drift checks:
  health            Dashboard: formatting, docs, pending refactors, agent/skill docs, headless timeouts
  check-docs        Doc links, file:line refs and ./dev.sh commands in Docs/ and the AGENTS/CLAUDE files resolve
  audit-refactors   Refs in Docs/Design/Refactor/pending-refactors.md still resolve (--id N, --by-status)
  lint-agents       .claude/agents, skills and tools-index reference real commands, files, agents and skills
  check-godot-timeouts  Every headless Godot launch in dev.sh is wrapped in a timeout

User only (open a window; blocked for AI sessions):
  editor            Open the Godot editor
  run               Play solo                        (user only)
  host [flags]      Host a game (--port N, --pvp)    (user only)
  join [address]    Join a game, default 127.0.0.1   (user only)
EOF
}

build() {
    dotnet build "$ROOT/WarriorsOfEverdawn.slnx" -nologo -v q
}

# Open engine bug: the host's MultiplayerSynchronizer sends once more to a client that is leaving. Harmless.
KNOWN_DISCONNECT_ERROR='Unable to send packet on channel [0-9]+, max channels: 0'

# On frames where the torso twist turns 20+ deg: chest measured about 3-4 deg off the aim with the twist,
# about 44 deg without. Sway authored into the strafe clips is excluded by only counting those frames.
MAX_TWISTED_CHEST_ERROR_DEG=12
MIN_TWIST_SAMPLES=50

# Starts a headless host and <clients> bot clients on a random port, all with the extra flags, and waits for them.
# Logs land in <logs>/host.log and <logs>/clientN.log. Fails if any process exits non-zero.
run_session() {
    local logs=$1 clients=$2 host_quit=$3 client_quit=$4
    shift 4
    mkdir -p "$logs"
    rm -f "$logs"/*.log
    local port=$((17000 + RANDOM % 1000))
    local game=("$GODOT" --headless --path "$PROJECT" --)

    timeout 90 "${game[@]}" --host --port "$port" --bot --quit-after "$host_quit" "$@" > "$logs/host.log" 2>&1 &
    local pids=($!)
    # Gives the host time to boot .NET and open the port; ENet keeps retrying the handshake for a few seconds beyond this.
    sleep 3
    local i
    for i in $(seq 1 "$clients"); do
        timeout 90 "${game[@]}" --join 127.0.0.1 --port "$port" --bot --quit-after "$client_quit" "$@" > "$logs/client$i.log" 2>&1 &
        pids+=($!)
    done

    local failed=0 pid
    for pid in "${pids[@]}"; do
        wait "$pid" || { echo "a session process exited with $?"; failed=1; }
    done
    return "$failed"
}

# Passes when the log's [tag] line satisfies an awk condition over its key=value fields, read as v["key"].
gate() {
    local label=$1 file=$2 tag=$3 condition=$4 message=$5
    local line
    line=$(grep -E "^\[$tag\]" "$file")
    # An empty line would read every field as 0, which satisfies conditions like "difference is about zero".
    if [ -z "$line" ]; then
        echo "FAIL $label: $message (no $tag line in the log)"
        return 1
    fi
    if ! echo "$line" | awk "{ for (i = 2; i <= NF; i++) { split(\$i, kv, \"=\"); v[kv[1]] = kv[2] } exit !($condition) }"; then
        echo "FAIL $label: $message (${line:-no $tag line})"
        return 1
    fi
}

# Any error fails, except the known engine disconnect error, which is noted instead.
check_log_errors() {
    local label=$1 file=$2
    local known
    known=$(grep -cE "$KNOWN_DISCONNECT_ERROR" "$file")
    if [ "$known" -gt 0 ]; then
        echo "NOTE $label: $known known Godot disconnect error(s), not failing: https://github.com/godotengine/godot/issues/86814"
    fi
    if grep -E 'ERROR|Unhandled exception' "$file" | grep -qvE "$KNOWN_DISCONNECT_ERROR"; then
        echo "FAIL $label: errors in log:"
        grep -E -A2 'ERROR|Unhandled exception' "$file" | grep -vE "$KNOWN_DISCONNECT_ERROR" | head -20
        return 1
    fi
}

# Peers on the host's [<tag>] line (field=peer:amount,...) with a positive amount.
count_positive_peers() {
    local file=$1 tag=$2 field=$3
    grep -E "^\[$tag\]" "$file" | grep -oE "$field=[^ ]*" | cut -d= -f2 | tr ',' '\n' | awk -F: '$2 > 0' | wc -l
}

# Every peer sees the two others as remote players, so each log needs two passing [net-check] lines.
net_test() {
    build || return 1
    local logs="$ROOT/_staging/net-test" failed=0
    run_session "$logs" 2 24 18 || failed=1

    local damage_taken_total=0 channelled="" log
    for log in host client1 client2; do
        local file="$logs/$log.log"
        grep -E '^\[(net-check|combat-check|combat-host|anim-check|turn-check|head-check|speed-check|skill-check|trail-check|reach-check|flash-check|ui-check|dodge-check)\]' "$file" | sed "s/^/$log: /"

        local passing
        passing=$(awk '/^\[net-check\]/ {
                delete v
                for (i = 2; i <= NF; i++) { split($i, kv, "="); v[kv[1]] = kv[2] }
                if (v["me"] != v["player"] && v["travel"] + 0 >= 3 && v["attacks"] + 0 >= 1) ok++
            } END { print ok + 0 }' "$file")
        if [ "$passing" -ne 2 ]; then
            echo "FAIL $log: expected 2 remote players seen moving and attacking, got $passing ($file)"
            failed=1
        fi

        gate "$log" "$file" combat-check 'v["hits_sent"] + 0 >= 1 && v["enemy_deaths_seen"] + 0 >= 1' \
            "expected own hits on skeletons and at least one skeleton death seen" || failed=1
        local taken
        taken=$(grep -E '^\[combat-check\]' "$file" | grep -oE 'damage_taken=[0-9]+' | cut -d= -f2)
        damage_taken_total=$((damage_taken_total + ${taken:-0}))

        # The game prints its own limits and expectations, so these checks never go stale when Core is retuned.
        gate "$log" "$file" turn-check 'v["max_turn_deg_s"] + 0 <= v["limit_deg_s"] * 1.01 && v["frames_at_limit"] + 0 >= 1' \
            "character turned faster than its limit, or the limit never came into play" || failed=1
        gate "$log" "$file" head-check '(v["player_head"] - v["head_expected"]) ^ 2 < 0.0001 && (v["player_headgear"] - v["headgear_expected"]) ^ 2 < 0.0001 && (v["enemy_head"] - v["head_expected"]) ^ 2 < 0.0001 && (v["enemy_headgear"] - v["headgear_expected"]) ^ 2 < 0.0001' \
            "head or headgear meshes not at Everdawn's scales on players and skeletons" || failed=1
        # Clip seconds per real second while swings play, against the player's attack speed.
        gate "$log" "$file" speed-check 'v["samples"] + 0 >= 20 && (v["swing_playback_rate"] - v["expected"]) ^ 2 < 0.01' \
            "swings not playing at the attack speed" || failed=1
        # Spin: used and landing; the player moves while spinning but never above Spin's share of run speed; and the
        # body turns one revolution per cycle in game while the spin plays at full strength (measured 1.00;
        # upper-body-only would read near 0). How long a peer keeps spinning depends on who reaches the skeletons
        # first, so channel length is checked per session.
        gate "$log" "$file" skill-check 'v["spins"] + 0 >= 1 && v["spin_hits"] + 0 >= 1 && v["spin_moving_frames"] + 0 >= 1 && v["spin_max_speed"] + 0 <= v["spin_speed_limit"] * 1.01 && v["spin_turn_ratio"] + 0 >= 0.9 && v["spin_turn_ratio"] + 0 <= 1.1' \
            "Spin not used, not landing, not moving at its speed, or not turning the body" || failed=1
        if gate "$log" "$file" skill-check 'v["spin_revolutions"] + 0 >= 2' "" > /dev/null; then
            channelled="$channelled $log"
        fi
        gate "$log" "$file" trail-check 'v["max_edges"] + 0 >= 4 && v["lingering_frames"] + 0 == 0 && v["enemy_trail_seen"] + 0 == 1' \
            "weapon trails missing or lingering" || failed=1
        gate "$log" "$file" flash-check 'v["enemy_flashes"] + 0 >= 1 && v["player_flashes"] + 0 >= 1 && v["lingering_frames"] + 0 == 0' \
            "hit flashes missing or lingering" || failed=1
        gate "$log" "$file" ui-check 'v["hp_mismatch_frames"] + 0 == 0 && v["bar_mismatch_frames"] + 0 == 0 && v["mana_mismatch_frames"] + 0 == 0 && v["max_enemy_bars"] + 0 >= 1 && v["max_player_bars"] + 0 >= 1' \
            "HUD or overhead bars not showing the real values" || failed=1

        # Dodges cover their fixed distance (open arena, so nothing cuts one short); the bot's bursts of three reach the
        # charge limit and get refused beyond it; dodges show on other machines; ghosts appear and clear.
        gate "$log" "$file" dodge-check 'v["dodges"] + 0 >= 1 && (v["distance_median"] - v["expected"]) ^ 2 < 0.1225 && v["max_in_recharge_window"] + 0 == v["charges"] + 0 && v["refused"] + 0 >= 1 && v["remote_dodges_seen"] + 0 >= 1 && v["ghosts_emitted"] + 0 >= 1 && v["ghost_lingering_frames"] + 0 == 0' \
            "dodges missing, off distance, beyond their charges, unseen by others, or ghosts wrong" || failed=1

        # Each skill's live blade-tip reach at its hit tests (median) must match the skill's range, so the hit
        # area ends where the drawn blade does. Fields read skill=measured/range.
        local reach
        reach=$(grep -E '^\[reach-check\]' "$file")
        if ! echo "$reach" | awk '{
                ok = NF > 2
                for (i = 3; i <= NF; i++) {
                    split($i, kv, "="); split(kv[2], mr, "/")
                    if (mr[1] == "NaN") { ok = 0; continue }
                    d = mr[1] - mr[2]; if (d * d > 0.0625) ok = 0
                }
                exit !ok
            }'; then
            echo "FAIL $log: blade tip reach at hit time does not match skill range (${reach:-no reach-check line})"
            failed=1
        fi

        local twisted samples
        twisted=$(grep -oE 'with_twist_deg=[0-9.]+' "$file" | cut -d= -f2)
        samples=$(grep -oE 'twist_samples=[0-9]+' "$file" | cut -d= -f2)
        if [ "${samples:-0}" -lt "$MIN_TWIST_SAMPLES" ] || [ -z "$twisted" ] \
            || awk -v t="$twisted" -v max="$MAX_TWISTED_CHEST_ERROR_DEG" 'BEGIN { exit !(t > max) }'; then
            echo "FAIL $log: chest ${twisted:-missing} deg off the aim over ${samples:-0} twisted frames (max $MAX_TWISTED_CHEST_ERROR_DEG deg, min $MIN_TWIST_SAMPLES frames)"
            failed=1
        fi

        check_log_errors "$log" "$file" || failed=1
    done

    local peers_dealing_damage
    peers_dealing_damage=$(count_positive_peers "$logs/host.log" combat-host damage_by_peer)
    if [ "$peers_dealing_damage" -ne 3 ]; then
        echo "FAIL host: expected damage from all 3 peers to reach the host, got $peers_dealing_damage"
        failed=1
    fi
    if [ "$damage_taken_total" -lt 1 ]; then
        echo "FAIL: no player took damage; skeleton attacks never landed"
        failed=1
    fi
    # Holding Spin must carry it past its first revolution, on the host and on at least one client.
    if [[ " $channelled " != *" host "* || " $channelled " != *" client"* ]]; then
        echo "FAIL: no held Spin went past two revolutions on the host and on a client (channelled on:${channelled:- none})"
        failed=1
    fi

    [ "$failed" -eq 0 ] && echo "net-test passed" || echo "net-test FAILED (logs: $logs)"
    return "$failed"
}

# Two players and no skeletons, so every hit and every point of damage is player on player.
pvp_test() {
    build || return 1
    local logs="$ROOT/_staging/pvp-test" failed=0
    run_session "$logs" 1 20 16 --pvp --no-enemies || failed=1

    local log
    for log in host client1; do
        local file="$logs/$log.log"
        grep -E '^\[(pvp-check|pvp-host|combat-check|ui-check)\]' "$file" | sed "s/^/$log: /"
        gate "$log" "$file" pvp-check 'v["pvp"] == "True" && v["hits_on_players"] + 0 >= 1' \
            "PvP not on for this peer, or it never hit the other player" || failed=1
        gate "$log" "$file" combat-check 'v["damage_taken"] + 0 >= 1' \
            "never took damage from the other player" || failed=1
        gate "$log" "$file" ui-check 'v["bar_mismatch_frames"] + 0 == 0 && v["max_player_bars"] + 0 >= 1' \
            "the other player's bar missing or wrong" || failed=1
        check_log_errors "$log" "$file" || failed=1
    done

    local attackers
    attackers=$(count_positive_peers "$logs/host.log" pvp-host damage_by_attacker)
    if [ "$attackers" -ne 2 ]; then
        echo "FAIL host: expected both players to have damaged the other, got $attackers"
        failed=1
    fi

    [ "$failed" -eq 0 ] && echo "pvp-test passed" || echo "pvp-test FAILED (logs: $logs)"
    return "$failed"
}

camera_test() {
    build || return 1
    local log="$ROOT/_staging/camera-test.log"
    mkdir -p "$ROOT/_staging"
    timeout 60 "$GODOT" --headless --path "$PROJECT" -- --camera-check > "$log" 2>&1
    local status=$?
    grep -E '^\[(camera|layout)-check\]' "$log"
    local modes layout
    modes=$(grep -cE '^\[camera-check\].* ok$' "$log")
    layout=$(grep -cE '^\[layout-check\].* ok$' "$log")
    if [ "$status" -ne 0 ] || [ "$modes" -ne 4 ] || [ "$layout" -ne 1 ]; then
        echo "camera-test FAILED (exit $status, $modes/4 modes ok, layout ok: $layout, log: $log)"
        return 1
    fi
    echo "camera-test passed"
}

# Runs every self-test even after a failure, so one report covers them all.
smoke() {
    local failed=() name
    for name in camera-test net-test pvp-test; do
        echo "== $name"
        "${name//-/_}" || failed+=("$name")
    done
    if [ "${#failed[@]}" -gt 0 ]; then
        echo "smoke FAILED: ${failed[*]}"
        return 1
    fi
    echo "smoke passed (camera-test, net-test, pvp-test)"
}

case "${1:-help}" in
    build) build ;;
    test) dotnet test "$ROOT/Core.Tests/Core.Tests.csproj" ;;
    format) shift; dotnet format "$ROOT/WarriorsOfEverdawn.slnx" "$@" ;;
    import) timeout 300 "$GODOT" --headless --path "$PROJECT" --import --quit ;;
    net-test) net_test ;;
    pvp-test) pvp_test ;;
    camera-test) camera_test ;;
    smoke) smoke ;;
    health) shift; python "$ROOT/Tools/health.py" "$@" ;;
    check-docs) shift; python "$ROOT/Tools/check_docs.py" "$@" ;;
    audit-refactors) shift; python "$ROOT/Tools/audit_refactors.py" "$@" ;;
    lint-agents) shift; python "$ROOT/Tools/lint_agents.py" "$@" ;;
    check-godot-timeouts) shift; python "$ROOT/Tools/check_godot_timeouts.py" "$@" ;;
    editor) "$GODOT" --editor --path "$PROJECT" ;;
    run) "$GODOT" --path "$PROJECT" ;;
    host) shift; "$GODOT" --path "$PROJECT" -- --host "$@" ;;
    join) shift; "$GODOT" --path "$PROJECT" -- --join "${1:-127.0.0.1}" "${@:2}" ;;
    help | -h | --help) usage ;;
    *) usage; exit 1 ;;
esac
