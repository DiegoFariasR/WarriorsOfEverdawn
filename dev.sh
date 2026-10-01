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
  net-test          Co-op host + 3 bot clients, one per weapon; asserts movement, combat, visuals and HUD reach every peer
  pvp-test          PvP host + 1 bot client, no skeletons; asserts players hit and damage each other
  camera-test       In every camera mode, W must move up the screen and D right; HUD sits on screen
  smoke             camera-test, net-test and pvp-test in turn; fails if any fails
  playtest          net-test made 2.5 minutes long with a player downed on cue: every net-test gate plus waves,
                    removal of the dead and of damage numbers, a flat node count, and going down and back up
                    --screenshots N   the host runs in an off-screen, minimized window and captures N frames

Screenshots (real renderer; off-screen, minimized, unfocused window):
  screenshot        Bot plays solo; saves _staging/screenshot.png after --at seconds (default 6)
                    --frames N --interval S   a series: _staging/screenshot_1.png .. _N.png
                    --camera 1-4   --no-ui   --no-enemies   --weapon <id>   --back-weapon <id>   (greatsword, quarterstaff, spear, scythe)

Measurements:
  swing-survey      Each weapon skill's clip: when the striking point moves fastest (hit time) and how far it reaches

Levels:
  level-fortresses  Generate the two fortress layouts (GodotClient/config/levels/*.layout.json); --seed N, --out-dir D
  level-audit       Audit level layouts without Godot: missing files, solids run together, markers or a gate blocked

Drift checks:
  health            Dashboard: formatting, docs, pending refactors, agent/skill docs, headless timeouts, level layouts
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

# On frames where the torso twist turns 20+ deg: chest measured 4-13 deg off the aim with the twist (more the more a
# bot strafes while its aim jumps between skeletons, as it does waiting to parry), 35-44 deg without. Sway authored
# into the strafe clips is excluded by only counting those frames.
MAX_TWISTED_CHEST_ERROR_DEG=15
MIN_TWIST_SAMPLES=50

# Playtest: session length, when the host takes a player down, and the leak tolerances. Over runs of 8-11 waves the
# node count the leak check compares stayed at 1331-1332 from the second wave on and orphan nodes at 0.
PLAYTEST_HOST_SECONDS=150
PLAYTEST_CLIENT_SECONDS=142
PLAYTEST_DOWN_AT=40
MAX_NODE_GROWTH=10
MAX_ORPHAN_GROWTH=2

# Session players' weapons in order: the host, then client1, client2, ... Every weapon is in each co-op session, in a
# hand so every skill's gates run, and on a back.
SESSION_WEAPONS=(greatsword quarterstaff spear scythe)
SESSION_BACK_WEAPONS=(spear scythe greatsword quarterstaff)
COOP_CLIENTS=3

# Waves are not scaled by player count yet, and one wave shared by four bots is gone before the slower weapons get
# a turn (thrusts and lunges hit twice as hard as swings), so the test sessions triple each wave.
COOP_WAVE_SCALE=3

# Long enough for every bot to show each thing the gates read (spins, swings, a lunge, a braced guard, a parry, a
# swap of sets) with samples to spare; at 32 and 26 one or another came up short about one run in three.
NET_TEST_HOST_SECONDS=44
NET_TEST_CLIENT_SECONDS=38

# Frames the host captures through the session (playtest --screenshots N). When above 0 the host runs in an
# off-screen, minimized window instead of headless, since captures need the real renderer.
SESSION_HOST_SHOTS=0

# Starts a headless host and <clients> bot clients on a random port, all with the extra flags, and waits for them.
# Each takes its weapon from SESSION_WEAPONS. Logs land in <logs>/host.log and <logs>/clientN.log. Fails if any
# process exits non-zero.
run_session() {
    local logs=$1 clients=$2 host_quit=$3 client_quit=$4
    shift 4
    mkdir -p "$logs"
    rm -f "$logs"/*.log
    local port=$((17000 + RANDOM % 1000))
    local game=("$GODOT" --headless --path "$PROJECT" --)
    # Well past the session's own length, so only a hang reaches it.
    local limit=$((host_quit + 60))

    if [ "$SESSION_HOST_SHOTS" -gt 0 ]; then
        rm -f "$ROOT"/_staging/screenshot*.png
        local interval=$(((host_quit - 20) / SESSION_HOST_SHOTS))
        timeout "$limit" "$GODOT" --position -10000,-10000 --path "$PROJECT" -- --ai-playtest --host --port "$port" --bot \
            --weapon "${SESSION_WEAPONS[0]}" --back-weapon "${SESSION_BACK_WEAPONS[0]}" --quit-after "$host_quit" --shots "$SESSION_HOST_SHOTS" --shot-at 15 --shot-interval "$interval" "$@" > "$logs/host.log" 2>&1 &
    else
        timeout "$limit" "${game[@]}" --host --port "$port" --bot --weapon "${SESSION_WEAPONS[0]}" --back-weapon "${SESSION_BACK_WEAPONS[0]}" --quit-after "$host_quit" "$@" > "$logs/host.log" 2>&1 &
    fi
    local pids=($!)
    # Gives the host time to boot .NET and open the port; ENet keeps retrying the handshake for a few seconds beyond this.
    sleep 3
    local i
    for i in $(seq 1 "$clients"); do
        timeout "$limit" "${game[@]}" --join 127.0.0.1 --port "$port" --bot --weapon "${SESSION_WEAPONS[i]}" --back-weapon "${SESSION_BACK_WEAPONS[i]}" --quit-after "$client_quit" "$@" > "$logs/client$i.log" 2>&1 &
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

# host client1 client2 ... for a co-op session.
session_logs() {
    echo host
    seq -f 'client%g' 1 "$COOP_CLIENTS"
}

# Peers on the host's [<tag>] line (field=peer:amount,...) with a positive amount.
count_positive_peers() {
    local file=$1 tag=$2 field=$3
    grep -E "^\[$tag\]" "$file" | grep -oE "$field=[^ ]*" | cut -d= -f2 | tr ',' '\n' | awk -F: '$2 > 0' | wc -l
}

# A co-op session of a host and COOP_CLIENTS bot clients with every net-test gate, shared by net-test and playtest.
# Every peer sees all the others as remote players, so each log needs that many passing [net-check] lines.
co_op_session() {
    local logs=$1 host_quit=$2 client_quit=$3
    shift 3
    local failed=0
    run_session "$logs" "$COOP_CLIENTS" "$host_quit" "$client_quit" --wave-scale "$COOP_WAVE_SCALE" "$@" || failed=1

    local damage_taken_total=0 lunge_hits_total=0 hangings_faded_total=0 spin_dashes_kept=0 spin_dash_tests=0 spin_dashes_seen=0 channelled="" log
    for log in $(session_logs); do
        local file="$logs/$log.log"
        grep -E '^\[(net-check|combat-check|combat-host|anim-check|turn-check|head-check|carry-check|speed-check|skill-check|trail-check|reach-check|flash-check|ui-check|dash-check|spin-dash-check|lunge-check|pickup-check|loot-check|map-check|bot-check|ranged-check|guard-check)\]' "$file" | sed "s/^/$log: /"

        local passing
        passing=$(awk '/^\[net-check\]/ {
                delete v
                for (i = 2; i <= NF; i++) { split($i, kv, "="); v[kv[1]] = kv[2] }
                if (v["me"] != v["player"] && v["travel"] + 0 >= 3 && v["attacks"] + 0 >= 1) ok++
            } END { print ok + 0 }' "$file")
        if [ "$passing" -ne "$COOP_CLIENTS" ]; then
            echo "FAIL $log: expected $COOP_CLIENTS remote players seen moving and attacking, got $passing ($file)"
            failed=1
        fi

        # Each player takes up its weapons after spawning, and every machine must show them, in hand and on the back.
        # Bots swap sets for a moment and back (BotControls), so every machine must also have seen two changes.
        local sets_seen expected_sets="" i
        sets_seen=$(grep -E '^\[net-check\]' "$file" | grep -oE 'weapon=[a-z-]+ back=[a-z-]+' | sed 's/weapon=//; s/ back=/+/' | sort | tr '\n' ' ')
        for i in $(seq 0 "$COOP_CLIENTS"); do
            expected_sets="$expected_sets${SESSION_WEAPONS[i]}+${SESSION_BACK_WEAPONS[i]}"$'\n'
        done
        expected_sets=$(printf '%s' "$expected_sets" | sort | tr '\n' ' ')
        if [ "$sets_seen" != "$expected_sets" ]; then
            echo "FAIL $log: players' weapons (in hand+on back) seen as '$sets_seen', expected '$expected_sets'"
            failed=1
        fi
        if awk '/^\[net-check\]/ { if (match($0, /weapon_changes=[0-9]+/) && substr($0, RSTART + 15, RLENGTH - 15) + 0 < 2) bad = 1 } END { exit !bad }' "$file"; then
            echo "FAIL $log: some player's swap to the back weapon and back was not seen here ($(grep -oE 'player=[0-9]+|weapon_changes=[0-9]+' "$file" | paste -d' ' - - | tr '\n' ';'))"
            failed=1
        fi

        gate "$log" "$file" combat-check 'v["hits_sent"] + 0 >= 1 && v["enemy_deaths_seen"] + 0 >= 1' \
            "expected own hits on skeletons and at least one skeleton death seen" || failed=1
        local taken
        taken=$(grep -E '^\[combat-check\]' "$file" | grep -oE 'damage_taken=[0-9]+' | cut -d= -f2)
        damage_taken_total=$((damage_taken_total + ${taken:-0}))
        local lunged
        lunged=$(grep -E '^\[lunge-check\]' "$file" | grep -oE 'lunge_hits=[0-9]+' | cut -d= -f2)
        lunge_hits_total=$((lunge_hits_total + ${lunged:-0}))
        local hangings_faded
        hangings_faded=$(grep -E '^\[map-check\]' "$file" | grep -oE 'hangings_faded_max=[0-9]+' | cut -d= -f2)
        hangings_faded_total=$((hangings_faded_total + ${hangings_faded:-0}))
        local spin_dash
        spin_dash=$(grep -E '^\[spin-dash-check\]' "$file")
        spin_dashes_kept=$((spin_dashes_kept + $(echo "$spin_dash" | grep -oE ' kept=[0-9]+' | cut -d= -f2 || echo 0)))
        spin_dash_tests=$((spin_dash_tests + $(echo "$spin_dash" | grep -oE 'tests_while_dashing=[0-9]+' | cut -d= -f2 || echo 0)))
        spin_dashes_seen=$((spin_dashes_seen + $(echo "$spin_dash" | grep -oE 'remote_seen=[0-9]+' | cut -d= -f2 || echo 0)))

        # A Spin held through a dash is never cut short by it.
        gate "$log" "$file" spin-dash-check 'v["cut_while_held"] + 0 == 0' "a dash cut short a Spin that was still held" || failed=1

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

        # Archers' arrows reach every machine, each drawn where it flies (the arrow model once carried an offset that drew
        # it 9.85 away), and every machine clears them once they have flown their distance.
        gate "$log" "$file" ranged-check 'v["arrows_seen"] + 0 >= 1 && v["lingering_frames"] + 0 == 0 && v["drawn_off_max"] + 0 <= 0.1' \
            "no archer arrows seen, arrows drawn away from their flight, or arrows outliving their flight" || failed=1

        # Running, both hands stay on the weapon (0.69 apart in the two-handed stance; 1.14 with the run clip's arms).
        # Standing with an empty hand they come apart (0.87 in the unarmed idle; 0.69 would be the two-handed idle).
        gate "$log" "$file" carry-check 'v["samples"] + 0 >= 1 && v["hands_apart_running"] + 0 <= 0.85 && v["unarmed_samples"] + 0 >= 1 && v["hands_apart_standing_unarmed"] + 0 >= 0.8' \
            "the weapon is carried in one hand while running, or empty hands stand as if holding one" || failed=1

        # Every bot lets go of its weapon and takes it back. Every machine sees weapons put down and taken up (its own and
        # at least one other's) and what it shows on the ground matches what it was told; an unarmed player starts no
        # attack; labels show for exactly the weapons in range, offering the weapon while a slot is free.
        gate "$log" "$file" pickup-check 'v["placed_seen"] + 0 >= 2 && v["taken_seen"] + 0 >= 2 && v["on_ground"] + 0 == v["placed_seen"] - v["taken_seen"] && v["drops_here"] + 0 >= 1 && v["pickups_here"] + 0 >= 1 && v["attacks_unarmed"] + 0 == 0 && v["label_frames"] + 0 >= 1 && v["offer_frames"] + 0 >= 1 && v["label_mismatch_frames"] + 0 == 0' \
            "weapons not dropped or taken up, unseen by others, attacks while unarmed, or ground labels wrong" || failed=1

        # The fortresses: the player starts inside the allied town, gets out of it and as far as the enemy fortress, and
        # takes no hit while in the town; no skeleton is ever in the town; every skeleton rises inside the enemy fortress
        # and some get out of it; walls between the camera and the player fade, and banners and torches are found hanging
        # on walls to fade with them; nobody had to walk straight for want of a way round the walls.
        gate "$log" "$file" map-check 'v["started_safe"] + 0 == 1 && v["left_town"] + 0 == 1 && v["nearest_to_fortress_gate"] + 0 <= 20 && v["hits_while_safe"] + 0 == 0 && v["enemies_in_town_frames"] + 0 == 0 && v["rose_outside_fortress"] + 0 == 0 && v["enemies_seen"] + 0 >= 1 && v["enemies_left_fortress"] + 0 >= 1 && v["walls_faded_max"] + 0 >= 1 && v["hangings"] + 0 >= 1 && v["straight_steps_after_start"] + 0 == 0' \
            "players not starting safe or never leaving town, skeletons in the town or rising outside their fortress or never leaving it, walls never fading, nothing hung on them, or no way found round the walls" || failed=1

        # Monsters leave gold: every machine sees piles fall and be picked up, and what it shows on the ground matches what
        # it was told, a coin stack for each pile lying and no other (the playtest's leak check leaves gold out for that);
        # every player has earned gold and souls, and the HUD shows what it has.
        gate "$log" "$file" loot-check 'v["piles_seen"] + 0 >= 1 && v["piles_taken_seen"] + 0 >= 1 && v["piles_on_ground"] + 0 == v["piles_seen"] - v["piles_taken_seen"] && v["gold_here"] + 0 >= 1 && v["souls_here"] + 0 >= 1 && v["purse_mismatch_frames"] + 0 == 0 && v["pile_model_mismatch_frames"] + 0 == 0' \
            "no gold dropped or picked up, coin stacks not matching the piles on the ground, none earned, or the HUD showing other amounts" || failed=1

        # A dash thrown with the attack button carries a thrust: every bot lunges, every machine sees it, the thrust's hit
        # window closes within two physics frames of the dash ending, and the weapon then reaches as far as the lunge's range.
        gate "$log" "$file" lunge-check 'v["lunges_here"] + 0 >= 1 && v["seen_remote"] + 0 >= 1 && v["land_offset_frames_max"] + 0 <= 2 && v["forward_lunges"] + 0 >= 1 && v["reach_off_range"] ^ 2 < 0.0625' \
            "no lunges, lunges unseen by others, the thrust landing off the end of its dash, or reaching off its range" || failed=1

        # Every bot raises its guard and every machine sees the others do it; a held guard stays up until a dash or going
        # down drops it; a guarded player moves no faster than its guard allows (the share is of that fastest speed, the
        # weapon's guard factor times the forward run, frame by frame, since weapon swaps change it).
        gate "$log" "$file" guard-check 'v["raised_here"] + 0 >= 1 && v["seen_remote"] + 0 >= 1 && v["guard_frames"] + 0 >= 1 && v["dropped_while_held"] + 0 == 0 && v["guard_speed_share_max"] + 0 <= 1.01' \
            "guards not raised, not seen by others, dropped while held, or guarded players moving too fast" || failed=1

        # The longest dash covers the fixed distance and none goes further (a wall can cut one short, so the median may
        # fall below it); the bot's bursts of three reach the
        # charge limit and get refused beyond it; dashes show on other machines; ghosts appear and clear.
        gate "$log" "$file" dash-check 'v["dashes"] + 0 >= 1 && (v["distance_max"] - v["expected"]) ^ 2 < 0.1225 && v["distance_median"] + 0 <= v["expected"] + 0.35 && v["max_in_recharge_window"] + 0 == v["charges"] + 0 && v["refused"] + 0 >= 1 && v["remote_dashes_seen"] + 0 >= 1 && v["ghosts_emitted"] + 0 >= 1 && v["ghost_lingering_frames"] + 0 == 0' \
            "dashes missing, off distance, beyond their charges, unseen by others, or ghosts wrong" || failed=1

        # Each of the weapon's two skills: the drawn weapon's live reach at its hit tests (median) must match the skill's
        # range, so the hit area ends where the weapon does. Skill fields read skill=measured/range.
        local reach
        reach=$(grep -E '^\[reach-check\]' "$file")
        if ! echo "$reach" | awk '{
                ok = 1; skills = 0
                for (i = 3; i <= NF; i++) {
                    if (index($i, "/") == 0) continue
                    skills++
                    split($i, kv, "="); split(kv[2], mr, "/")
                    if (mr[1] == "NaN") { ok = 0; continue }
                    d = mr[1] - mr[2]; if (d * d > 0.0625) ok = 0
                }
                exit !(ok && skills == 2)
            }'; then
            echo "FAIL $log: weapon reach at hit time does not match skill range (${reach:-no reach-check line})"
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

    # The host decides blocks and parries: both happen, and a parried skeleton reels.
    gate host "$logs/host.log" guard-check 'v["blocks"] + 0 >= 1 && v["parries"] + 0 >= 1 && v["enemies_parried"] + 0 >= 1' \
        "guards never blocked or parried, or a parry left the skeleton swinging" || failed=1

    # The host decides loot, and its player is there from the first monster: it has exactly the gold collected (never
    # more than fell) and one soul for every monster it saw die.
    gate host "$logs/host.log" loot-check 'v["gold_here"] + 0 == v["gold_collected"] + 0 && v["gold_collected"] + 0 <= v["gold_dropped"] + 0 && v["souls_here"] + 0 == v["deaths_since_here"] + 0' \
        "the host's player has other gold than was collected, or other souls than monsters died" || failed=1

    # The host decides arrow hits; bots keep moving, but arrows still find them (8 of 11 in the first run).
    gate host "$logs/host.log" ranged-check 'v["arrow_hits"] + 0 >= 1' "no arrow hit a player" || failed=1

    local peers_dealing_damage
    peers_dealing_damage=$(count_positive_peers "$logs/host.log" combat-host damage_by_peer)
    if [ "$peers_dealing_damage" -ne $((COOP_CLIENTS + 1)) ]; then
        echo "FAIL host: expected damage from all $((COOP_CLIENTS + 1)) peers to reach the host, got $peers_dealing_damage"
        failed=1
    fi
    if [ "$damage_taken_total" -lt 1 ]; then
        echo "FAIL: no player took damage; skeleton attacks never landed"
        failed=1
    fi
    if [ "$lunge_hits_total" -lt 1 ]; then
        echo "FAIL: no lunge hit a skeleton on any machine"
        failed=1
    fi
    # Which wall a camera looks through depends on where its bot fights, so this is asked of the session, not each machine.
    if [ "$hangings_faded_total" -lt 1 ]; then
        echo "FAIL: no banner or torch faded with its wall on any machine"
        failed=1
    fi
    # Across the session: Spins carried through dashes, still testing for hits on the way, and seen by other machines.
    if [ "$spin_dashes_kept" -lt 1 ] || [ "$spin_dash_tests" -lt 1 ] || [ "$spin_dashes_seen" -lt 1 ]; then
        echo "FAIL: no Spin carried on through a dash (kept $spin_dashes_kept, hit tests while dashing $spin_dash_tests, seen by others $spin_dashes_seen)"
        failed=1
    fi
    # Holding Spin must carry it past its first revolution, on the host and on at least one client.
    if [[ " $channelled " != *" host "* || " $channelled " != *" client"* ]]; then
        echo "FAIL: no held Spin went past two revolutions on the host and on a client (channelled on:${channelled:- none})"
        failed=1
    fi

    return "$failed"
}

net_test() {
    build || return 1
    local logs="$ROOT/_staging/net-test" failed=0
    co_op_session "$logs" "$NET_TEST_HOST_SECONDS" "$NET_TEST_CLIENT_SECONDS" || failed=1
    [ "$failed" -eq 0 ] && echo "net-test passed" || echo "net-test FAILED (logs: $logs)"
    return "$failed"
}

# net-test's session made long (several waves) with one player taken down on cue, plus the checks only a long
# session can make: waves keep coming, the dead and the damage numbers are removed on time, the node count stays
# flat from wave to wave, and a downed player stays put and gets back up at full HP.
playtest() {
    local shots=0
    while [ $# -gt 0 ]; do
        case "$1" in
            --screenshots) shots=$2; shift 2 ;;
            *) echo "playtest: unknown option '$1' (see ./dev.sh help)" >&2; return 2 ;;
        esac
    done
    build || return 1
    local logs="$ROOT/_staging/playtest" failed=0
    SESSION_HOST_SHOTS=$shots co_op_session "$logs" "$PLAYTEST_HOST_SECONDS" "$PLAYTEST_CLIENT_SECONDS" --down-at "$PLAYTEST_DOWN_AT" || failed=1

    local downed_on="" log
    for log in $(session_logs); do
        local file="$logs/$log.log"
        grep -E '^\[(wave-check|leak-check|down-check|down-drift)\]' "$file" | sed "s/^/$log: /"
        gate "$log" "$file" wave-check 'v["waves"] + 0 >= 3' "fewer than 3 waves in the session" || failed=1
        # Node growth compares each later wave with the second: the fewest nodes in each while every player carries
        # both weapons, leaving out what comes and goes with the fight and has its own check: skeletons and damage
        # numbers (the two lingering counts), gold on the ground (loot-check), HP bars (ui-check), arrows (ranged-check).
        gate "$log" "$file" leak-check "v[\"corpse_lingering_frames\"] + 0 == 0 && v[\"text_lingering_frames\"] + 0 == 0 && v[\"node_growth\"] != \"NaN\" && v[\"node_growth\"] + 0 <= $MAX_NODE_GROWTH && v[\"orphan_growth\"] + 0 <= $MAX_ORPHAN_GROWTH" \
            "dead skeletons or damage numbers outstaying their time, or nodes piling up from wave to wave" || failed=1
        gate "$log" "$file" down-check 'v["downs"] + 0 >= 1 && v["revives"] + 0 >= 1 && (v["down_seconds_min"] - v["expected_seconds"]) ^ 2 < 0.25 && (v["down_seconds_max"] - v["expected_seconds"]) ^ 2 < 0.25 && v["hp_after_revive_min"] + 0 == v["hp_max"] + 0 && v["hits_while_down"] + 0 == 0' \
            "no one seen going down and back up after the respawn delay at full HP, or a downed player was hit" || failed=1
        # Only the machine that owns the downed player can see it hold still and land back at the centre.
        gate "$log" "$file" down-check 'v["local_downs"] + 0 == 0 || (v["down_drift_max"] + 0 <= 0.01 && v["attacks_while_down"] + 0 == 0 && v["revive_distance_max"] + 0 <= 0.5)' \
            "the downed player moved or attacked while down, or did not get back up at the arena centre" || failed=1
        if gate "$log" "$file" down-check 'v["local_downs"] + 0 >= 1' "" > /dev/null; then
            downed_on="$downed_on $log"
        fi
    done
    if [ -z "$downed_on" ]; then
        echo "FAIL: no machine had its own player go down (--down-at $PLAYTEST_DOWN_AT)"
        failed=1
    fi
    if [ "$shots" -gt 0 ]; then
        ls "$ROOT"/_staging/screenshot_*.png 2> /dev/null || { echo "FAIL: --screenshots $shots but no captures"; failed=1; }
    fi

    [ "$failed" -eq 0 ] && echo "playtest passed" || echo "playtest FAILED (logs: $logs)"
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

# Captures need a real renderer (headless draws nothing), so this is the one AI-run launch with a window: placed
# off-screen, and the game minimizes it without taking focus (--ai-playtest). A bot plays solo so there is combat.
screenshot() {
    local args=(--bot)
    while [ $# -gt 0 ]; do
        case "$1" in
            --at) args+=(--shot-at "$2"); shift 2 ;;
            --frames) args+=(--shots "$2"); shift 2 ;;
            --interval) args+=(--shot-interval "$2"); shift 2 ;;
            --camera) args+=(--camera "$2"); shift 2 ;;
            --no-ui) args+=(--no-ui); shift ;;
            --no-enemies) args+=(--no-enemies); shift ;;
            --weapon) args+=(--weapon "$2"); shift 2 ;;
            --back-weapon) args+=(--back-weapon "$2"); shift 2 ;;
            *) echo "screenshot: unknown option '$1' (see ./dev.sh help)" >&2; return 2 ;;
        esac
    done
    build || return 1
    mkdir -p "$ROOT/_staging"
    rm -f "$ROOT"/_staging/screenshot*.png
    local log="$ROOT/_staging/screenshot.log"
    timeout 120 "$GODOT" --position -10000,-10000 --path "$PROJECT" -- --ai-playtest --screenshot "${args[@]}" > "$log" 2>&1
    local status=$?
    grep -E '^\[screenshot\]' "$log"
    if [ "$status" -ne 0 ] || ! ls "$ROOT"/_staging/screenshot*.png > /dev/null 2>&1; then
        echo "screenshot FAILED (exit $status, log: $log)"
        grep -E -A2 'ERROR|Unhandled exception' "$log" | head -10
        return 1
    fi
}

# Where each weapon skill's hit time and range come from (Dev/SwingSurvey.cs); read it before setting them in Core.
swing_survey() {
    build || return 1
    local log="$ROOT/_staging/swing-survey.log"
    mkdir -p "$ROOT/_staging"
    timeout 60 "$GODOT" --headless --path "$PROJECT" -- --swing-survey --no-enemies > "$log" 2>&1
    local status=$?
    grep -E '^\[swing-survey\]' "$log"
    if [ "$status" -ne 0 ] || ! grep -q '^\[swing-survey\]' "$log"; then
        echo "swing-survey FAILED (exit $status, log: $log)"
        grep -E -A2 'ERROR|Unhandled exception' "$log" | head -10
        return 1
    fi
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
    playtest) shift; playtest "$@" ;;
    swing-survey) swing_survey ;;
    level-fortresses) shift; python "$ROOT/Tools/level_fortresses.py" "$@" ;;
    level-audit) shift; python "$ROOT/Tools/level_audit.py" "$@" ;;
    screenshot) shift; screenshot "$@" ;;
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
