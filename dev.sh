#!/usr/bin/env bash
set -uo pipefail

GODOT="${GODOT:-C:/Program Files/Godot_v4.7.2/Godot_v4.7.2-stable_mono_win64.exe}"
ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$ROOT/GodotClient"

usage() {
    cat <<EOF
Usage: ./dev.sh <command>

Build and test:
  build             Build the solution (Core, Core.Tests, GodotClient)
  test              Run the Core tests, then the character kit's (GodotClient/kit)
  format            dotnet format the solution (Core, Core.Tests, GodotClient)
  import            Headless asset import (run after copying assets in)

Godot self-tests (headless):
  net-test          Co-op host + $COOP_CLIENTS bot clients, one per weapon; asserts movement, combat, visuals and HUD reach every peer
  pvp-test          PvP host + 1 bot client, no skeletons; asserts players hit and damage each other
  magic-test        Host + $MAGIC_CLIENTS bot clients, $MAGIC_WANDS with wands, one with staffs and one with enchanted weapons;
                    asserts bolts and volleys are thrown, land and are seen by the others, area spells are drawn, a
                    staff's ball bursts, barriers take blows and come back, and every weapon of an element shows it
  trade-test        For each seller, a host + 1 bot client beside it with gold and orbs; asserts each opens the
                    shop window, gets what it can pay for (or sells what it carries), is refused after, and ends
                    with the right weapons in hand and the cost taken or the pay given
  town-test         Host + 1 bot client in the town; each breaks the nearest crate, sits on a bench and lies on a bed;
                    asserts crates break and splinter on both machines, both players are seen sitting, lying and
                    getting up where the seats put them, and no seat is left taken
  camera-test       In every camera mode, W must move up the screen and D right; HUD sits on screen
  wall-test         Bolts, balls and arrows loosed at a wall, from afar and from against it, must end at it
  parts-test        Every part and alias of the catalogue put on a figure, and 200 figures drawn at random from each pool
  floors-test       The player walks up into each of the town's storeys and down into the crypt; asserts every way is
                    found and walked, what is above is hidden and nothing else, and the crypt's guards keep to it, come
                    for the player, fall and leave a treasure the player takes
  smoke             camera-test, wall-test, parts-test, floors-test, net-test, pvp-test, trade-test and magic-test in
                    turn; fails if any fails
  playtest          net-test made 2.5 minutes long with a player downed on cue: every net-test gate plus waves,
                    removal of the dead and of damage numbers, a flat node count, and going down and back up
                    --screenshots N   the host runs in an off-screen, minimized window and captures N frames

Screenshots (real renderer; off-screen, minimized, unfocused window):
  screenshot        Bot plays solo; saves _staging/screenshot.png after --at seconds (default 6)
                    --frames N --interval S   a series: _staging/screenshot_1.png .. _N.png
                    --camera 1-4   --zoom 0.5-2 (below 1 is nearer)   --no-ui   --no-enemies   --weapon <id>   --back-weapon <id>   (greatsword, quarterstaff, spear, scythe, sword-and-shield, <element>-staff, <element>-wand; spear~fire enchanted, spear+3 improved)
                    --start-at x,z   the bot starts on that spot of the ground instead of in the town; x,y,z
                                     at that height (4 a storey up, -4 the crypt); or a marker: crypt, upstairs:1
                    --orb-chance P   every monster leaves a magic orb P of the time (0 to 1) instead of rarely
                    --potion-chance P   every monster leaves a potion's charge P of the time instead of rarely
                    --start-gold N   --start-orbs N   --start-armour TIER   the bot starts with that much
                    --start-spent   the bot starts at a third of its HP, its potion empty and no mana
                    --trade-drill   the bot trades with the seller it starts beside
                    --town-drill    the bot breaks a crate, sits on a bench and lies on a bed
                    --status-drill  every few seconds a skeleton is frozen and the next stunned

  armour-lineup     The knight in a row in each tier of armour, seen from the front; saves _staging/armour-lineup.png
                    [Outfit,Outfit,...] shows those outfits instead (characters of the kit's parts catalogue)
  weapon-lineup <id>  The knight holding that weapon in its stance, its guard and each skill as it lands, and
                    carrying it on the back; saves _staging/weapon-lineup.png
  magic-lineup      The wands casting: each holds its spell on an area, what it throws beside it. With nothing
                    named, all eight in two pictures: _staging/magic-lineup-1.png and -2.png. [fire,water,...]
                    shows those in _staging/magic-lineup.png; [barriers] each inside its barrier

Measurements:
  swing-survey      Each weapon skill's clip: when the striking point moves fastest (hit time) and how far it reaches

Assets:
  look-lineup       Figures in a row, seen from the front; saves _staging/look-lineup.png. [cast] is the player,
                    the enemies and the sellers; [townsfolk] or [skeletons] eight figures drawn at random from that
                    pool ([townsfolk@40] from seed 40 on); [Druid,Witch] those characters as they were made
                    ([Druid:bare] with nothing on)
  ring-lineup       The rings a thrust lays and arrows leave, side on, three moments apart; saves
                    _staging/ring-lineup-1.png to -3.png
  gold-lineup       Every pile gold falls in, one coin to ten, in a row on the ground; saves
                    _staging/gold-lineup-low.png (seen as from behind a player) and -high.png (from above)
  gold-piles        Rebuild the gold piles (two to ten coins) from the one coin, with Blender in
                    the background; then ./dev.sh import

Levels:
  level-fortresses  Generate the two fortress layouts (GodotClient/config/levels/*.layout.json); --seed N, --out-dir D
  level-audit       Audit level layouts without Godot: missing files, solids run together, markers or a gate blocked,
                    stairs with no floor to step off at either end
  level-list <town|fortress> [x0,z0,x1,z1] [--room -1|1]
                    What a layout has in an area (or all of it): placements with their boxes, markers and areas, in
                    world coordinates, or in a room's (u, v) with --room
  ways-dump [x0,z0,x1,z1]  The arena's navigation mesh: its polygons in that area, or with none only those that go
                    through the air between floors; exit 1 when any does

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

# The character kit shared with Everdawn, a git submodule: a clone made without it has an empty folder there.
KIT="$ROOT/GodotClient/kit"

need_kit() {
    if [ ! -f "$KIT/core/EverdawnKit.Core.csproj" ]; then
        echo "The character kit is missing at GodotClient/kit: run 'git submodule update --init'"
        return 1
    fi
}

build() {
    need_kit || return 1
    dotnet build "$ROOT/WarriorsOfEverdawn.slnx" -nologo -v q
}

# Open engine bug: the host's MultiplayerSynchronizer sends once more to a client that is leaving. Harmless.
KNOWN_DISCONNECT_ERROR='Unable to send packet on channel [0-9]+, max channels: 0'

# On frames where the torso twist turns 20+ deg: chest measured 4-13 deg off the aim with the twist (more the more a
# bot strafes while its aim jumps between skeletons, as it does waiting to parry), 35-44 deg without. Sway authored
# into the strafe clips is excluded by only counting those frames.
MAX_TWISTED_CHEST_ERROR_DEG=15
MIN_TWIST_SAMPLES=50

# Playtest: session length, when the host takes a player down, and the leak tolerances. In a run of 16-17 waves the
# node count the leak check compares stayed at 2848-2849 from the second wave on and orphan nodes at 0.
PLAYTEST_HOST_SECONDS=150
PLAYTEST_CLIENT_SECONDS=142
PLAYTEST_DOWN_AT=40
MAX_NODE_GROWTH=10
MAX_ORPHAN_GROWTH=2

# Session players' weapons in order: the host, then client1, client2, ... Every weapon is in each co-op session, in a
# hand so every skill's gates run, and on a back.
SESSION_WEAPONS=(greatsword quarterstaff spear scythe sword-and-shield claws warhammer)
SESSION_BACK_WEAPONS=(spear scythe sword-and-shield quarterstaff claws warhammer greatsword)
COOP_CLIENTS=6

# Waves are not scaled by player count yet, and one wave shared by seven bots is gone before the slower weapons get
# a turn (thrusts and lunges hit twice as hard as swings), so the test sessions make each wave four times its size.
# Tripled was enough for five bots; with seven, about one run in four left a bot that never reached a fight.
COOP_WAVE_SCALE=4

# Orbs are rare (a few monsters in a hundred leave one), so the test sessions raise every monster's chance to where
# each machine is sure to see some fall and be picked up.
COOP_ORB_CHANCE=0.25
COOP_POTION_CHANCE=0.25

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
    local pids=($!) names=(host)
    # Gives the host time to boot .NET and open the port; ENet keeps retrying the handshake for a few seconds beyond this.
    sleep 3
    local i
    for i in $(seq 1 "$clients"); do
        timeout "$limit" "${game[@]}" --join 127.0.0.1 --port "$port" --bot --weapon "${SESSION_WEAPONS[i]}" --back-weapon "${SESSION_BACK_WEAPONS[i]}" --quit-after "$client_quit" "$@" > "$logs/client$i.log" 2>&1 &
        pids+=($!)
        names+=("client$i")
    done

    local failed=0 status
    for i in "${!pids[@]}"; do
        wait "${pids[i]}"
        status=$?
        if [ "$status" -ne 0 ]; then
            echo "FAIL ${names[i]}: $(ended "$status" "$limit") (log: $logs/${names[i]}.log)"
            failed=1
        fi
    done
    return "$failed"
}

# How a process run under `timeout <limit>` ended: 124 is timeout's own, for one it killed at the limit.
ended() {
    local status=$1 limit=$2
    if [ "$status" -eq 124 ]; then
        echo "still running after $limit s, killed by timeout"
    else
        echo "exited with $status"
    fi
}

# Passes when the log's [tag] line satisfies an awk condition over its key=value fields, read as v["key"].
gate() {
    local label=$1 file=$2 tag=$3 condition=$4 message=$5
    local line keys missing
    line=$(grep -E "^\[$tag\]" "$file")
    # A field that is not there, or NaN, would read as 0 (gawk takes "NaN" for 0), which satisfies conditions like
    # "difference is about zero": the line must be there with every field the condition names, and NaN is stored as
    # a real NaN, which fails every ordered comparison.
    if [ -z "$line" ]; then
        echo "FAIL $label: $message (no $tag line in the log)"
        return 1
    fi
    keys=$(grep -oE 'v\["[^"]+"\]' <<< "$condition" | sed -E 's/^v\["(.*)"\]$/\1/' | tr '\n' ' ')
    missing=$(echo "$line" | awk -v keys="$keys" "{
        for (i = 2; i <= NF; i++) {
            split(\$i, kv, \"=\")
            if (tolower(kv[2]) == \"nan\") v[kv[1]] = \"+nan\" + 0; else v[kv[1]] = kv[2]
            seen[kv[1]] = 1
        }
        n = split(keys, named, \" \")
        for (j = 1; j <= n; j++) if (!(named[j] in seen)) { print named[j]; exit 3 }
        exit !($condition)
    }")
    case $? in
        0) return 0 ;;
        3) echo "FAIL $label: $message ($missing not printed in the $tag line)" ;;
        *) echo "FAIL $label: $message ($line)" ;;
    esac
    return 1
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
    run_session "$logs" "$COOP_CLIENTS" "$host_quit" "$client_quit" --wave-scale "$COOP_WAVE_SCALE" --orb-chance "$COOP_ORB_CHANCE" --potion-chance "$COOP_POTION_CHANCE" "$@" || failed=1

    local damage_taken_total=0 lunge_hits_total=0 hangings_faded_total=0 spin_dashes_kept=0 spin_dash_tests=0 spin_dashes_seen=0 one_handed_runners=0 channelled="" log
    for log in $(session_logs); do
        local file="$logs/$log.log"
        grep -E '^\[(net-check|combat-check|combat-host|anim-check|turn-check|head-check|carry-check|speed-check|skill-check|trail-check|reach-check|flash-check|ui-check|dash-check|spin-dash-check|lunge-check|pickup-check|loot-check|map-check|bot-check|ranged-check|guard-check|status-check|status-host)\]' "$file" | sed "s/^/$log: /"

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
        if awk '/^\[net-check\]/ { if (!match($0, /weapon_changes=[0-9]+/) || substr($0, RSTART + 15, RLENGTH - 15) + 0 < 2) bad = 1 } END { exit !bad }' "$file"; then
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
        gate "$log" "$file" head-check '(v["player_head"] - v["head_expected"]) ^ 2 < 0.0001 && (v["enemy_head"] - v["head_expected"]) ^ 2 < 0.0001 && (v["enemy_headgear"] - v["headgear_expected"]) ^ 2 < 0.0001' \
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
        # Thrusts lay their rows of rings (every bot lunges, and the spear thrusts), and arrows leave rings behind them
        # beyond those (skeleton archers shoot in every session).
        gate "$log" "$file" trail-check 'v["ring_rows"] + 0 >= 1 && v["rings_laid"] + 0 > v["ring_rows"] * v["rings_per_row"]' \
            "no thrust laid its rings, or no arrow left any" || failed=1
        gate "$log" "$file" flash-check 'v["enemy_flashes"] + 0 >= 1 && v["player_flashes"] + 0 >= 1 && v["lingering_frames"] + 0 == 0' \
            "hit flashes missing or lingering" || failed=1
        gate "$log" "$file" ui-check 'v["hp_mismatch_frames"] + 0 == 0 && v["bar_mismatch_frames"] + 0 == 0 && v["mana_mismatch_frames"] + 0 == 0 && v["max_enemy_bars"] + 0 >= 1 && v["max_player_bars"] + 0 >= 1 && v["skeleton_bar_frames"] + 0 >= 1 && v["far_skeleton_bar_frames"] + 0 == 0' \
            "HUD or overhead bars not showing the real values, no bar drawn over a skeleton near the player, or one drawn over a skeleton too far off" || failed=1

        # Archers' arrows reach every machine, each drawn where it flies (the arrow model once carried an offset that drew
        # it 9.85 away), and every machine clears them once they have flown their distance.
        gate "$log" "$file" ranged-check 'v["arrows_seen"] + 0 >= 1 && v["lingering_frames"] + 0 == 0 && v["drawn_off_max"] + 0 <= 0.1' \
            "no archer arrows seen, arrows drawn away from their flight, or arrows outliving their flight" || failed=1

        # Each machine measures its own player. Running, both hands stay on a weapon for two (0.69 apart in the
        # two-handed stance; 1.14 with the run clip's arms), and a sword and shield stay up in their stance (0.87)
        # instead of swinging with the run; a player runs with whichever it holds, and the one with the sword and
        # shield must be among them. Standing with an empty hand the hands come apart (0.87 in the unarmed idle; 0.69
        # would be the two-handed idle).
        gate "$log" "$file" carry-check 'v["samples"] + v["one_handed_samples"] >= 1 && (v["samples"] + 0 == 0 || v["hands_apart_running"] + 0 <= 0.85) && (v["one_handed_samples"] + 0 == 0 || v["hands_apart_running_one_handed"] + 0 <= 1.0) && v["unarmed_samples"] + 0 >= 1 && v["hands_apart_standing_unarmed"] + 0 >= 0.8' \
            "a weapon for two carried in one hand while running, a sword and shield swinging with the run, or empty hands standing as if holding a weapon" || failed=1
        if gate "$log" "$file" carry-check 'v["one_handed_samples"] + 0 >= 1' "" > /dev/null; then
            one_handed_runners=$((one_handed_runners + 1))
        fi

        # Every bot lets go of its weapon and takes it back. Every machine sees weapons put down and taken up (its own and
        # at least one other's) and what it shows on the ground matches what it was told; an unarmed player starts no
        # attack; labels show for exactly the weapons in range, offering the weapon while a slot is free.
        gate "$log" "$file" pickup-check 'v["placed_seen"] + 0 >= 2 && v["taken_seen"] + 0 >= 2 && v["on_ground"] + 0 == v["placed_seen"] - v["taken_seen"] && v["drops_here"] + 0 >= 1 && v["pickups_here"] + 0 >= 1 && v["attacks_unarmed"] + 0 == 0 && v["label_frames"] + 0 >= 1 && v["offer_frames"] + 0 >= 1 && v["label_mismatch_frames"] + 0 == 0' \
            "weapons not dropped or taken up, unseen by others, attacks while unarmed, or ground labels wrong" || failed=1

        # The fortresses: the player starts inside the allied town, gets out of it and as far as the enemy fortress, and
        # takes no hit while in the town; no skeleton is ever in the town; every skeleton rises inside the enemy fortress
        # and some get out of it; walls between the camera and the player fade, and banners and torches are found hanging
        # on walls to fade with them; the ways lead from where players start into each of the five rooms; nobody had to
        # walk straight for want of a way round the walls.
        gate "$log" "$file" map-check 'v["started_safe"] + 0 == 1 && v["left_town"] + 0 == 1 && v["nearest_to_fortress_gate"] + 0 <= 20 && v["hits_while_safe"] + 0 == 0 && v["enemies_in_town_frames"] + 0 == 0 && v["rose_outside_fortress"] + 0 == 0 && v["enemies_seen"] + 0 >= 1 && v["enemies_left_fortress"] + 0 >= 1 && v["walls_faded_max"] + 0 >= 1 && v["hangings"] + 0 >= 1 && v["rooms"] + 0 == 5 && v["rooms_with_a_way_in"] + 0 == v["rooms"] + 0 && v["straight_steps_after_start"] + 0 == 0' \
            "players not starting safe or never leaving town, skeletons in the town or rising outside their fortress or never leaving it, walls never fading, nothing hung on them, a room with no way in, or no way found round the walls" || failed=1

        # Monsters leave gold and, at the sessions' raised chance, magic orbs: every machine sees both fall and be picked
        # up, and what it shows on the ground matches what it was told, a model for each thing lying and no other (the
        # playtest's leak check leaves them out for that); every player has earned gold, orbs and souls, and the HUD
        # shows what it has. Potion charges fall and are picked up as orbs are, and potions are drunk (bots drink at half
        # HP), none healing more than a charge does.
        gate "$log" "$file" loot-check 'v["piles_seen"] + 0 >= 1 && v["piles_taken_seen"] + 0 >= 1 && v["piles_on_ground"] + 0 == v["piles_seen"] - v["piles_taken_seen"] && v["orbs_seen"] + 0 >= 1 && v["orbs_taken_seen"] + 0 >= 1 && v["orbs_on_ground"] + 0 == v["orbs_seen"] - v["orbs_taken_seen"] && v["gold_here"] + 0 > v["gold_start"] + 0 && v["orbs_here"] + 0 >= 1 && v["souls_here"] + 0 >= 1 && v["purse_mismatch_frames"] + 0 == 0 && v["loot_model_mismatch_frames"] + 0 == 0 && v["potions_seen"] + 0 >= 1 && v["potions_taken_seen"] + 0 >= 1 && v["potions_on_ground"] + 0 == v["potions_seen"] - v["potions_taken_seen"] && v["drinks_seen"] + 0 >= 1 && v["drink_healed_most"] + 0 <= v["heal_per_charge"] + 0' \
            "no gold, orb or potion charge dropped or picked up, models not matching what lies on the ground, none earned, the HUD showing other amounts, no potion drunk, or one healing more than a charge" || failed=1
        # Souls, seen: a wisp rises for every soul of every monster this machine saw die while its player was in the
        # game, and every one has reached the player or is still on its way.
        gate "$log" "$file" loot-check 'v["souls_risen"] + 0 >= 1 && v["souls_taken_in"] + 0 >= 1 && v["souls_risen"] + 0 == v["souls_died_here"] + 0 && v["souls_risen"] + 0 == v["souls_taken_in"] + v["souls_flying"]' \
            "no soul seen rising for a monster that died, none reaching the player, or one lost on the way" || failed=1

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

        # Each of the weapon's skills, both forms of its primary and its secondary: the drawn weapon's live reach at its hit tests (median) must match the skill's
        # range, so the hit area ends where the weapon does. Skill fields read skill=measured/range/samples.
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
                exit !(ok && skills >= 2)
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

    # The host decides loot, and its player is there from the first monster: it has exactly the gold it started with
    # and the gold collected (never more than fell), and one soul for every monster it saw die.
    gate host "$logs/host.log" loot-check 'v["gold_here"] + 0 == v["gold_start"] + v["gold_collected"] && v["gold_collected"] + 0 <= v["gold_dropped"] + 0 && v["orbs_here"] + 0 == v["orbs_collected"] + 0 && v["souls_here"] + 0 == v["deaths_since_here"] + 0' \
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

    # Every weapon here is of steel or wood, and skeletons are weak to all three physical types: each type landed,
    # and no hit was taken as a plain one.
    local physical_types
    physical_types=$(count_positive_peers "$logs/host.log" combat-host hits_by_type)
    if [ "$physical_types" -ne 3 ]; then
        echo "FAIL host: expected hits of the 3 physical damage types, got $physical_types ($(grep -E '^\[combat-host\]' "$logs/host.log"))"
        failed=1
    fi
    gate host "$logs/host.log" combat-host 'v["weak_hits"] + 0 >= 1 && v["plain_hits"] + 0 == 0 && v["resisted_hits"] + 0 == 0' \
        "a blow of steel or wood did not meet the skeletons' weakness" || failed=1

    # Slashes open wounds: every machine sees skeletons bleeding and names it over their bars, and on the host the
    # wounds bite.
    for log in $(session_logs); do
        gate "$log" "$logs/$log.log" status-check 'index(v["enemy_statuses"], "bleeding") > 0 && v["named_most"] + 0 >= 1' \
            "no skeleton seen bleeding, or no status named over a bar" || failed=1
    done
    if [ "$(count_positive_peers "$logs/host.log" status-host enemy_bites)" -lt 1 ]; then
        echo "FAIL host: no wound or burn bit a skeleton ($(grep -E '^\[status-host\]' "$logs/host.log"))"
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
    if [ "$one_handed_runners" -lt 1 ]; then
        echo "FAIL: no player was seen running with a sword and shield"
        failed=1
    fi

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
# A spot to stand on inside one of the town's rooms, as x,z, from its layout.
town_room_spot() {
    python -c 'import json, sys
spot = next(m["position"] for m in json.load(open(sys.argv[1]))["markers"] if m["name"] == "room")
print(f"{spot[0]},{spot[2]}")' "$PROJECT/config/levels/allied-town.layout.json"
}

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
    # Players start inside a room of the town, so each of them has to walk out through its doorway for the map
    # check's left_town; the net-test, starting them in the courtyard, only asks the ways for a path into each room.
    local room
    room=$(town_room_spot) || { echo "FAIL: no room marker in the town's layout"; return 1; }
    SESSION_HOST_SHOTS=$shots co_op_session "$logs" "$PLAYTEST_HOST_SECONDS" "$PLAYTEST_CLIENT_SECONDS" --down-at "$PLAYTEST_DOWN_AT" --start-at "$room" || failed=1

    local downed_on="" log
    for log in $(session_logs); do
        local file="$logs/$log.log"
        grep -E '^\[(wave-check|leak-check|down-check|down-drift)\]' "$file" | sed "s/^/$log: /"
        gate "$log" "$file" wave-check 'v["waves"] + 0 >= 3' "fewer than 3 waves in the session" || failed=1
        # Node growth compares each later wave with the second: the fewest nodes in each while every player carries
        # both weapons, leaving out what comes and goes with the fight and has its own check: skeletons and damage
        # numbers (the two lingering counts), gold on the ground and souls on their way (loot-check), burning ground,
        # ice and splinters (each over in seconds), HP bars (ui-check), arrows (ranged-check).
        gate "$log" "$file" leak-check "v[\"corpse_lingering_frames\"] + 0 == 0 && v[\"text_lingering_frames\"] + 0 == 0 && v[\"node_growth\"] + 0 <= $MAX_NODE_GROWTH && v[\"orphan_growth\"] + 0 <= $MAX_ORPHAN_GROWTH" \
            "dead skeletons or damage numbers outstaying their time, or nodes piling up from wave to wave" || failed=1
        gate "$log" "$file" down-check 'v["downs"] + 0 >= 1 && v["revives"] + 0 >= 1 && (v["down_seconds_min"] - v["expected_seconds"]) ^ 2 < 0.25 && (v["down_seconds_max"] - v["expected_seconds"]) ^ 2 < 0.25 && v["hp_after_revive_min"] + 0 == v["hp_max"] + 0 && v["hits_while_down"] + 0 == 0' \
            "no one seen going down and back up after the respawn delay at full HP, or a downed player was hit" || failed=1
        # Only the machine that owns the downed player can see it hold still and land back at the centre. A body may
        # settle by less than its own radius in the steps after it falls (pushed clear of a wall it was pressed into).
        gate "$log" "$file" down-check 'v["local_downs"] + 0 == 0 || (v["down_drift_max"] + 0 <= 0.01 && v["down_settle_max"] + 0 <= v["settle_allowed"] + 0 && v["attacks_while_down"] + 0 == 0 && v["revive_distance_max"] + 0 <= 0.5)' \
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
    run_session "$logs" 1 20 16 --pvp --no-enemies --status-drill || failed=1

    local log
    for log in host client1; do
        local file="$logs/$log.log"
        grep -E '^\[(pvp-check|pvp-host|combat-check|ui-check|status-check|status-host)\]' "$file" | sed "s/^/$log: /"
        gate "$log" "$file" pvp-check 'v["pvp"] == "True" && v["hits_on_players"] + 0 >= 1' \
            "PvP not on for this peer, or it never hit the other player" || failed=1
        gate "$log" "$file" combat-check 'v["damage_taken"] + 0 >= 1' \
            "never took damage from the other player" || failed=1
        gate "$log" "$file" ui-check 'v["bar_mismatch_frames"] + 0 == 0 && v["max_player_bars"] + 0 >= 1' \
            "the other player's bar missing or wrong" || failed=1
        # Statuses on players: blades leave them bleeding, and the host freezes and stuns each in turn
        # (--status-drill). Every machine sees it, and the one held stays where it is and starts no swing.
        gate "$log" "$file" status-check 'index(v["player_statuses"], "bleeding") > 0 && index(v["player_statuses"], "frozen") > 0 && index(v["player_statuses"], "stunned") > 0 && v["local_held_frames"] + 0 >= 1 && v["local_held_moved_most"] + 0 < 0.05 && v["swings_while_held"] + 0 == 0' \
            "no player seen bleeding, frozen or stunned, or this one moving or swinging while held" || failed=1
        check_log_errors "$log" "$file" || failed=1
    done
    gate host "$logs/host.log" status-host 'v["player_bites"] + 0 >= 1 && v["drills"] + 0 >= 2' \
        "no wound bit a player, or the host froze and stunned no one on cue" || failed=1

    local attackers
    attackers=$(count_positive_peers "$logs/host.log" pvp-host damage_by_attacker)
    if [ "$attackers" -ne 2 ]; then
        echo "FAIL host: expected both players to have damaged the other, got $attackers"
        failed=1
    fi

    [ "$failed" -eq 0 ] && echo "pvp-test passed" || echo "pvp-test FAILED (logs: $logs)"
    return "$failed"
}

# The drill gets a seller's first three offers and is refused a fourth. At the weaponsmith, exactly the gold for
# three plain weapons.
TRADE_TEST_GOLD=150

# At the blacksmith the players start in the last tier of armour had for gold alone, with exactly what the weapon in
# hand made +1, the one on the back made +1 and the next tier of armour, the first paid in gold and orbs, come to.
TRADE_TEST_SMITH_ARMOUR=2
TRADE_TEST_SMITH_GOLD=280
TRADE_TEST_SMITH_ORBS=3

# At the merchant the players sell the weapon in hand, the one on the back and the one orb they start with; the
# drill waits for a little gold to arrive before it opens the window.
TRADE_TEST_MERCHANT_GOLD=10
TRADE_TEST_MERCHANT_ORBS=1

# At the enchanter, exactly what three enchantments of the weapon in hand come to: it ends with the third element
# on it.
TRADE_TEST_ENCHANT_GOLD=180
TRADE_TEST_ENCHANT_ORBS=3

# At the innkeeper the players start spent (--start-spent) with exactly the gold for three rests, its one offer.
TRADE_TEST_INN_GOLD=30

# The sellers the town's layout stands, by id, on one line: Windows Python ends each line it prints with a carriage
# return, which would stay on every id but the last.
town_sellers() {
    python -c 'import json, sys
markers = json.load(open(sys.argv[1]))["markers"]
print(" ".join(m["name"][len("seller-"):] for m in markers if m["name"].startswith("seller-")))' "$PROJECT/config/levels/allied-town.layout.json"
}

# A spot a step and a half in front of that seller, where a customer stands, as x,z, from the town's layout: well
# inside the seller's reach, so a bot's first step does not take it out.
town_spot_beside_seller() {
    python -c 'import json, math, sys
seller = next(m for m in json.load(open(sys.argv[1]))["markers"] if m["name"] == "seller-" + sys.argv[2])
(x, _, z), yaw = seller["position"], seller["yaw"]
print(f"{x + 1.5 * math.sin(yaw):.2f},{z + 1.5 * math.cos(yaw):.2f}")' "$PROJECT/config/levels/allied-town.layout.json" "$1"
}

# One session of the trade test: a host and a client beside one seller.
trade_session() {
    local seller=$1 logs=$2 failed=0 spot
    spot=$(town_spot_beside_seller "$seller") || { echo "FAIL $seller: no marker for it in the town's layout"; return 1; }
    local means=(--start-gold "$TRADE_TEST_GOLD")
    if [ "$seller" = blacksmith ]; then
        means=(--start-gold "$TRADE_TEST_SMITH_GOLD" --start-orbs "$TRADE_TEST_SMITH_ORBS" --start-armour "$TRADE_TEST_SMITH_ARMOUR")
    elif [ "$seller" = merchant ]; then
        means=(--start-gold "$TRADE_TEST_MERCHANT_GOLD" --start-orbs "$TRADE_TEST_MERCHANT_ORBS")
    elif [ "$seller" = enchanter ]; then
        means=(--start-gold "$TRADE_TEST_ENCHANT_GOLD" --start-orbs "$TRADE_TEST_ENCHANT_ORBS")
    elif [ "$seller" = innkeeper ]; then
        means=(--start-gold "$TRADE_TEST_INN_GOLD" --start-spent)
    fi
    run_session "$logs" 1 14 11 --no-enemies --start-at "$spot" "${means[@]}" --trade-drill || failed=1

    local log
    for log in host client1; do
        local file="$logs/$log.log"
        grep -E '^\[(trade-check|trade-host)\]' "$file" | sed "s/^/$seller $log: /"
        # The window: the prompt shows beside the seller, the window opens once, for this seller, and closes, has all
        # its slots (TradeRules.Slots) with the seller's offers in them and is no bigger than the game's window (a
        # headless run has no screen to fit it on), and the player stands still while it is open.
        gate "$seller $log" "$file" trade-check 'v["sellers"] + 0 >= 1 && v["prompt_frames"] + 0 >= 1 && v["opened"] + 0 == 1 && v["closed"] + 0 == 1 && v["seller"] == "'"$seller"'" && v["slots"] + 0 == v["slots_wanted"] + 0 && v["items"] + 0 >= 1 && v["window_fits"] + 0 == 1 && v["moved_while_trading"] + 0 <= 0.05 && v["drill_done"] + 0 == 1' \
            "no seller or prompt, the shop window not opening for this seller or closing once, its slots or offers missing, it being bigger than the game's window, or the player moving while it is open" || failed=1
        # The trade: it gets the seller's first three offers, the window then says it cannot pay for another, the host
        # refuses it too, the gold and orbs left are what it started with less what it paid and plus what it was paid,
        # the weapons in its hand and on its back are what those trades leave (none, at the merchant), the merchant
        # alone pays, and the armour got is worn and shown on the HUD.
        gate "$seller $log" "$file" trade-check 'v["bought"] + 0 >= 1 && v["gold_here"] + 0 == v["gold_start"] - v["spent"] + v["earned"] && (v["earned"] + 0 > 0) == ("'"$seller"'" == "merchant") && v["orbs_here"] + 0 == v["orbs_start"] - v["orbs_spent"] && v["bought"] + 0 == 3 && v["refused_by_window"] + 0 >= 1 && v["refused_by_host"] + 0 >= 1 && v["in_hand"] == v["expected_in_hand"] && v["on_back"] == v["expected_on_back"] && v["armour_here"] + 0 == v["expected_armour"] + 0 && v["armour_shown"] + 0 == v["armour_here"] + 0' \
            "not three things got, the gold or orbs not matching what was paid and earned, a seller paying that should not or the merchant not paying, something it could not pay for not refused by the window or by the host, the weapons got not in hand and on the back, or the armour got not worn or not shown" || failed=1
        # The rest, at the innkeeper: it came spent, and leaves with its HP and potion full, its mana filled by the first
        # rest, before it could have filled of itself.
        if [ "$seller" = innkeeper ]; then
            gate "$seller $log" "$file" trade-check 'v["hp_start"] + 0 < v["hp_max"] + 0 && v["hp_here"] + 0 == v["hp_max"] + 0 && v["potion_start"] + 0 == 0 && v["potion_here"] + 0 == v["potion_max"] + 0 && v["mana_before_rest"] + 0 < v["mana_max"] + 0 && v["mana_on_rest"] + 0 == v["mana_max"] + 0' \
                "the rest not filling the HP, the potion or the mana of a player who came spent" || failed=1
        fi
        # The look: both players' figures on this machine wear the parts of the armour each has, but for the frames in
        # which it changes (two measured: the host arms a player as it joins, a frame before the figure's first of
        # its own), and this one's is the look of its tier, which is another than it came to the seller in when the
        # seller is the one who betters armour.
        gate "$seller $log" "$file" trade-check 'v["figures"] + 0 == 2 && v["undressed_frames_on_end"] + 0 <= 5 && v["outfit_here"] == v["outfit_wanted"] && (v["outfit_here"] != v["outfit_start"]) == ("'"$seller"'" == "blacksmith")' \
            "a player's figure not dressed in the look of the armour it wears, or the look changing without the armour (or not with it)" || failed=1
        check_log_errors "$seller $log" "$file" || failed=1

        # What the host holds for this player, and sees in its hand, is what the player's own machine has. Whole
        # entries are compared as plain text: an improved weapon's id has a + in it.
        local me hand gold orbs armour
        me=$(grep -E '^\[trade-check\]' "$file" | grep -oE 'me=[0-9]+' | cut -d= -f2)
        hand=$(grep -E '^\[trade-check\]' "$file" | grep -oE ' in_hand=[^ ]+' | cut -d= -f2)
        gold=$(grep -E '^\[trade-check\]' "$file" | grep -oE 'gold_here=[0-9]+' | cut -d= -f2)
        orbs=$(grep -E '^\[trade-check\]' "$file" | grep -oE 'orbs_here=[0-9]+' | cut -d= -f2)
        armour=$(grep -E '^\[trade-check\]' "$file" | grep -oE 'armour_here=[0-9]+' | cut -d= -f2)
        local field want
        for field in "hands $hand" "gold $gold" "orbs $orbs" "armour $armour"; do
            set -- $field
            want="$me:$2"
            if ! grep -E '^\[trade-host\]' "$logs/host.log" | grep -oE " $1=[^ ]*" | cut -d= -f2 | tr ',' '\n' | grep -Fxq "$want"; then
                echo "FAIL $seller $log: the host's $1 do not have $want ($(grep -E '^\[trade-host\]' "$logs/host.log"))"
                failed=1
            fi
        done
    done

    local buyers
    buyers=$(count_positive_peers "$logs/host.log" trade-host sold)
    if [ "$buyers" -ne 2 ]; then
        echo "FAIL $seller host: expected sales to both players, got $buyers"
        failed=1
    fi

    return "$failed"
}

# Four wands in hand and the other four on the backs, so every element's bolt or volley, area spell and barrier is
# cast in the session; a staff, whose ball bursts; and a sixth player with an enchanted, improved bow in hand, whose
# arrows fly as the bolts do, and an enchanted greatsword on the back. Waves tripled: with five casters, doubled ones
# left the ball nothing to burst on about one run in seven. In order: the wands, the staff, the rest.
MAGIC_WEAPONS=(fire-wand water-wand earth-wand ice-wand fire-staff bow~ice+2)
MAGIC_BACK_WEAPONS=(wind-wand void-wand divine-wand lightning-wand lightning-staff greatsword~wind)
MAGIC_WANDS=4
MAGIC_CASTERS=5
MAGIC_CLIENTS=5
MAGIC_HOST_SECONDS=44
MAGIC_CLIENT_SECONDS=38

magic_test() {
    build || return 1
    local logs="$ROOT/_staging/magic-test" failed=0 log player=0
    local SESSION_WEAPONS=("${MAGIC_WEAPONS[@]}") SESSION_BACK_WEAPONS=("${MAGIC_BACK_WEAPONS[@]}") COOP_CLIENTS=$MAGIC_CLIENTS
    run_session "$logs" "$MAGIC_CLIENTS" "$MAGIC_HOST_SECONDS" "$MAGIC_CLIENT_SECONDS" --wave-scale 3 --barrier-drill --status-drill || failed=1

    for log in $(session_logs); do
        local file="$logs/$log.log"
        grep -E '^\[(magic-check|magic-host|combat-check|combat-host|lunge-check|guard-check|carry-check|bot-check|status-check|status-host)\]' "$file" | sed "s/^/$log: /"

        if [ "$player" -lt "$MAGIC_CASTERS" ]; then
            # Bolts: this player throws them, as many to a cast as its skill has (one bolt, or a volley's three; a
            # cast cut short by a dash throws fewer), and some land.
            gate "$log" "$file" magic-check 'v["casts"] + 0 >= 1 && v["bolts_here"] + 0 >= 1 && v["bolts_per_cast_most"] + 0 == v["bolts_per_cast_wanted"] + 0 && v["casts_over_thrown"] + 0 == 0 && v["bolts_landed"] + 0 >= 1' \
                "no bolts thrown or landed, or a cast throwing more than its skill has or never all of them" || failed=1
            # The barrier: shown here; this player's took the host's drill blows (it fell below full) and was seen
            # coming back while it was down.
            gate "$log" "$file" magic-check 'v["barrier_frames_here"] + 0 >= 1 && v["barrier_least"] + 0 < v["barrier_full"] + 0 && v["barrier_rises"] + 0 >= 1' \
                "no barrier shown here, this player's never taking a blow, or never coming back" || failed=1
        fi

        if [ "$player" -lt "$MAGIC_WANDS" ]; then
            # The wand's spell held on an area: drawn here in rounds of strikes.
            gate "$log" "$file" magic-check 'v["area_rounds_here"] + 0 >= 2 && v["area_frames_here"] + 0 >= 1 && v["strikes_most"] + 0 >= 1' \
                "no spell held on an area here, or no strikes drawn" || failed=1
        elif [ "$player" -lt "$MAGIC_CASTERS" ]; then
            # The staff's ball: thrown, burst, and some burst caught a body.
            gate "$log" "$file" magic-check 'v["blasts_here"] + 0 >= 1 && v["blast_caught_most"] + 0 >= 1' \
                "no ball burst here, or no burst caught a body" || failed=1
        else
            # No staff here: the staff's bursts are seen from afar.
            gate "$log" "$file" magic-check 'v["blasts_seen_remote"] + 0 >= 1' "the staff's bursts unseen here" || failed=1
        fi

        # What the others cast is seen here: their bolts, each ending in a burst and none outliving its flight, their
        # area spells and their barriers.
        gate "$log" "$file" magic-check 'v["bolts_seen_remote"] + 0 >= 1 && v["bolt_lingering_frames"] + 0 == 0 && v["bursts_most"] + 0 >= 1 && v["area_frames_remote"] + 0 >= 1 && v["barrier_frames_remote"] + 0 >= 1' \
            "the others' bolts, area spells or barriers unseen, or bolts outliving their flight" || failed=1
        # Every weapon of an element shows it on every machine, a staff or wand at its head and an enchanted weapon
        # all over, whatever has flashed over it since; and no plain weapon does.
        gate "$log" "$file" magic-check 'v["alight_frames"] + 0 >= 1 && v["dark_frames"] + 0 == 0 && v["plain_alight_frames"] + 0 == 0' \
            "a staff, a wand or an enchanted weapon not showing its element, or a plain weapon showing one" || failed=1
        # The thrust reaches as far as the lunge's range, with a staff or a wand as with any weapon.
        gate "$log" "$file" lunge-check 'v["lunges_here"] + 0 >= 1 && v["reach_off_range"] ^ 2 < 0.0625' \
            "no lunge, or its thrust reaching off its range" || failed=1
        # Fire and ice leave their ground, seen laid on every machine, and never more patches at once than the casters
        # may keep between them.
        gate "$log" "$file" magic-check 'v["burning_laid"] + 0 >= 1 && v["icy_laid"] + 0 >= 1 && v["surfaces_shown_most"] + 0 >= 1 && v["surfaces_shown_most"] + 0 <= v["surfaces_kept_most"] + 0' \
            "no burning ground or ice seen laid, or more patches at once than their casters keep" || failed=1
        check_log_errors "$log" "$file" || failed=1
        player=$((player + 1))
    done

    # On the host, where hits are decided: every caster's bolts landed, every wand's area spell, the staff's bursts,
    # and the enchanted weapons' blows as part magic.
    local host="$logs/host.log" field peers wanted
    for field in bolt_hits:$MAGIC_CASTERS area_hits:$MAGIC_WANDS blast_hits:$((MAGIC_CASTERS - MAGIC_WANDS)) barrier_drills:$MAGIC_CASTERS barrier_took:$MAGIC_CASTERS; do
        wanted=${field#*:}
        field=${field%:*}
        peers=$(count_positive_peers "$host" magic-host "$field")
        if [ "$peers" -ne "$wanted" ]; then
            echo "FAIL host: expected $field from $wanted players, got $peers ($(grep -E '^\[magic-host\]' "$host"))"
            failed=1
        fi
    done
    peers=$(count_positive_peers "$host" magic-host arrow_hits)
    if [ "$peers" -lt 1 ]; then
        echo "FAIL host: no arrow from the bow hit anything ($(grep -E '^\[magic-host\]' "$host"))"
        failed=1
    fi
    peers=$(count_positive_peers "$host" magic-host enchanted_hits)
    if [ "$peers" -lt 1 ]; then
        echo "FAIL host: no blow of an enchanted weapon reached the host as part magic ($(grep -E '^\[magic-host\]' "$host"))"
        failed=1
    fi
    # Skeletons are weak to fire and not to water or earth: the fire staff's hits met the weakness, and the water
    # and earth staffs' were taken as they are.
    gate host "$host" combat-host 'v["weak_hits"] + 0 >= 1 && v["plain_hits"] + 0 >= 1' \
        "the skeletons' weakness to fire, or their taking water and earth as they are, did not show in the hits" || failed=1
    # Statuses: the host freezes a skeleton and stuns the next on cue (--status-drill), since they die too soon to be
    # frozen by play alone. Every machine sees both, the frozen in their ice (but for the frame it takes to go up),
    # and neither moving while it is held; fire leaves skeletons burning.
    for log in $(session_logs); do
        gate "$log" "$logs/$log.log" status-check 'index(v["enemy_statuses"], "frozen") > 0 && index(v["enemy_statuses"], "stunned") > 0 && index(v["enemy_statuses"], "burning") > 0 && v["held_frames"] + 0 >= 1 && v["ice_block_frames"] + 0 >= 1 && v["frozen_without_ice_frames"] * 20 <= v["ice_block_frames"] && v["held_moved_most"] + 0 < 0.05' \
            "no skeleton seen frozen, stunned or burning, a frozen one without its ice, or a held one moving" || failed=1
    done
    gate host "$host" status-host 'v["drills"] + 0 >= 2' "the host froze and stunned nothing on cue" || failed=1
    # Turns on burning ground burned skeletons and turns on ice chilled them, and skeletons stood on ice.
    gate host "$host" magic-host 'v["burning_acted"] + 0 >= 1 && v["icy_acted"] + 0 >= 1 && v["on_ice_frames"] + 0 >= 1' \
        "no skeleton burned by burning ground, chilled by ice or seen on it" || failed=1
    # The host deals every barrier a blow as it goes up (counted above): while a barrier holds the blow costs no HP.
    gate host "$host" magic-host 'v["drill_hp_lost"] + 0 == 0' "a blow on a barrier that held cost its player HP" || failed=1

    [ "$failed" -eq 0 ] && echo "magic-test passed" || echo "magic-test FAILED (logs: $logs)"
    return "$failed"
}

trade_test() {
    build || return 1
    local logs="$ROOT/_staging/trade-test" failed=0 sellers seller
    sellers=$(town_sellers) || { echo "FAIL: could not read the sellers from the town's layout"; return 1; }
    [ -n "$sellers" ] || { echo "FAIL: the town's layout stands no seller"; return 1; }
    for seller in $sellers; do
        trade_session "$seller" "$logs/$seller" || failed=1
    done

    [ "$failed" -eq 0 ] && echo "trade-test passed ($(echo $sellers | tr ' ' ','))" || echo "trade-test FAILED (logs: $logs)"
    return "$failed"
}

# The town test starts its players between a crate and the bench by the camp fire, a walk from the inn's beds.
TOWN_TEST_START="4.0,18.5"
TOWN_TEST_HOST_SECONDS=50
TOWN_TEST_CLIENT_SECONDS=45

town_test() {
    build || return 1
    local logs="$ROOT/_staging/town-test" failed=0
    run_session "$logs" 1 "$TOWN_TEST_HOST_SECONDS" "$TOWN_TEST_CLIENT_SECONDS" --no-enemies --start-at "$TOWN_TEST_START" --town-drill || failed=1

    local log
    for log in host client1; do
        local file="$logs/$log.log"
        grep -E '^\[town-(check|host)\]' "$file" | sed "s/^/$log: /"
        # Crates: the one it went for broke after showing a hit that did not break it, every one broken here burst into splinters as it broke (or broke before this
        # machine joined and was told of it), and no splinters are left.
        gate "$log" "$file" town-check 'v["drill_done"] + 0 == 1 && v["broke_mine"] + 0 == 1 && v["crate_hits"] + 0 >= 1 && v["crates_broken_shown"] + 0 >= 1 && v["crates_broken_here"] + 0 == v["crates_broken_shown"] + v["crates_broken_heard"] && v["splinters_most"] + 0 >= 1 && v["splinters_left"] + 0 == 0' \
            "the drill not done, its crate not broken, a crate breaking without splinters, or splinters left behind" || failed=1
        # Seats: every player seen sitting, lying down and getting up, its body where the seat puts it once down, nobody
        # left resting, and the prompt shown while a seat was in reach.
        gate "$log" "$file" town-check 'v["seen_sitting"] + 0 == v["players"] + 0 && v["seen_lying"] + 0 == v["players"] + 0 && v["seen_getting_up"] + 0 == v["players"] + 0 && v["off_seat_most"] + 0 <= 0.25 && v["resting_at_end"] + 0 == 0 && v["prompt_frames"] + 0 >= 1' \
            "a player not seen sitting, lying or getting up, a body away from where its seat puts it, someone left resting, or no prompt" || failed=1
        check_log_errors "$log" "$file" || failed=1
    done
    gate host "$logs/host.log" town-host 'v["seats"] + 0 >= 1 && v["seats_taken"] + 0 == 0' "no seats, or a seat left taken on the host" || failed=1

    [ "$failed" -eq 0 ] && echo "town-test passed" || echo "town-test FAILED (logs: $logs)"
    return "$failed"
}

camera_test() {
    build || return 1
    local log="$ROOT/_staging/camera-test.log" limit=60
    mkdir -p "$ROOT/_staging"
    timeout "$limit" "$GODOT" --headless --path "$PROJECT" -- --camera-check > "$log" 2>&1
    local status=$?
    grep -E '^\[(camera|layout)-check\]' "$log"
    local modes layout
    modes=$(grep -cE '^\[camera-check\].* ok$' "$log")
    layout=$(grep -cE '^\[layout-check\].* ok$' "$log")
    local failed=0
    [ "$status" -eq 0 ] && [ "$modes" -eq 4 ] && [ "$layout" -eq 1 ] || failed=1
    check_log_errors camera-test "$log" || failed=1
    if [ "$failed" -ne 0 ]; then
        echo "camera-test FAILED ($(ended "$status" "$limit"), $modes/4 modes ok, layout ok: $layout, log: $log)"
        return 1
    fi
    echo "camera-test passed"
}

wall_test() {
    build || return 1
    local log="$ROOT/_staging/wall-test.log" limit=60
    mkdir -p "$ROOT/_staging"
    timeout "$limit" "$GODOT" --headless --path "$PROJECT" -- --wall-check --no-enemies > "$log" 2>&1
    local status=$?
    grep -E '^\[wall-check\]' "$log"
    local failed=0
    [ "$status" -eq 0 ] && grep -qE '^\[wall-check\] passed$' "$log" || failed=1
    check_log_errors wall-test "$log" || failed=1
    if [ "$failed" -ne 0 ]; then
        echo "wall-test FAILED ($(ended "$status" "$limit"), log: $log)"
        return 1
    fi
    echo "wall-test passed"
}

parts_test() {
    build || return 1
    local log="$ROOT/_staging/parts-test.log" limit=120
    mkdir -p "$ROOT/_staging"
    timeout "$limit" "$GODOT" --headless --path "$PROJECT" -- --parts-check --no-enemies > "$log" 2>&1
    local status=$?
    grep -E '^\[parts-check\]' "$log"
    local failed=0
    [ "$status" -eq 0 ] && grep -qE '^\[parts-check\] passed$' "$log" || failed=1
    check_log_errors parts-test "$log" || failed=1
    if [ "$failed" -ne 0 ]; then
        echo "parts-test FAILED ($(ended "$status" "$limit"), log: $log)"
        return 1
    fi
    echo "parts-test passed"
}

# The navigation mesh baked headless, its polygons printed: in the area, or (with none) only the faulty ones.
ways_dump() {
    build || return 1
    local log="$ROOT/_staging/ways-dump.log" limit=120
    mkdir -p "$ROOT/_staging"
    timeout "$limit" "$GODOT" --headless --path "$PROJECT" -- --ways-dump "${1:-all}" --no-enemies > "$log" 2>&1
    local status=$?
    grep -E '^\[ways\]' "$log"
    [ "$status" -eq 0 ] || echo "ways-dump: $(ended "$status" "$limit") (log: $log)"
    return "$status"
}

floors_test() {
    build || return 1
    local log="$ROOT/_staging/floors-test.log" limit=240
    mkdir -p "$ROOT/_staging"
    timeout "$limit" "$GODOT" --headless --path "$PROJECT" -- --floors-check > "$log" 2>&1
    local status=$?
    grep -E '^\[floors-check\]' "$log"
    local failed=0
    [ "$status" -eq 0 ] && grep -qE '^\[floors-check\] passed$' "$log" || failed=1
    check_log_errors floors-test "$log" || failed=1
    if [ "$failed" -ne 0 ]; then
        echo "floors-test FAILED ($(ended "$status" "$limit"), log: $log)"
        return 1
    fi
    echo "floors-test passed"
}

# Every pile gold falls in, one coin to ten, in a row on the ground: once from low, as from behind a player, and
# once from high, as the cameras that look down see them.
gold_lineup() {
    local view height
    for view in low:1.6 high:5; do
        height="${view#*:}"
        screenshot --no-enemies --no-ui --at 1 --gold-lineup "$height" > /dev/null || return 1
        cp "$ROOT/_staging/screenshot.png" "$ROOT/_staging/gold-lineup-${view%%:*}.png"
        echo "gold-lineup: $ROOT/_staging/gold-lineup-${view%%:*}.png"
    done
}

# The rings thrusts and arrows leave, side on, a moment apart: _staging/ring-lineup-1.png to -3.png.
ring_lineup() {
    screenshot --no-enemies --no-ui --at 1.85 --frames 3 --interval 0.1 --ring-lineup > /dev/null || return 1
    local n
    for n in 1 2 3; do
        cp "$ROOT/_staging/screenshot_$n.png" "$ROOT/_staging/ring-lineup-$n.png"
        echo "ring-lineup: $ROOT/_staging/ring-lineup-$n.png"
    done
}

# Figures in a row, for looking at what the parts make: the game's cast, a pool's random figures, a character.
look_lineup() {
    screenshot --no-enemies --no-ui --at 1 --look-lineup "${1:-cast}" || return 1
    cp "$ROOT/_staging/screenshot.png" "$ROOT/_staging/look-lineup.png"
    echo "look-lineup: $ROOT/_staging/look-lineup.png"
}

# The armour tiers side by side, or the outfits named (comma-separated), for choosing and checking how armour looks.
armour_lineup() {
    local which=(--armour-lineup)
    [ $# -gt 0 ] && which=(--outfits "$1")
    screenshot --no-enemies --no-ui --at 1 "${which[@]}" || return 1
    cp "$ROOT/_staging/screenshot.png" "$ROOT/_staging/armour-lineup.png"
    echo "armour-lineup: $ROOT/_staging/armour-lineup.png"
}

# One weapon held in its stance, its guard and each skill at the moment it lands, and carried on the back, for
# checking how a weapon looks in the hands.
weapon_lineup() {
    [ $# -gt 0 ] || { echo "usage: ./dev.sh weapon-lineup <weapon id>"; return 1; }
    screenshot --no-enemies --no-ui --at 1 --weapon-lineup "$1" || return 1
    cp "$ROOT/_staging/screenshot.png" "$ROOT/_staging/weapon-lineup.png"
    echo "weapon-lineup: $ROOT/_staging/weapon-lineup.png"
}

# The wands casting: each of the elements named ("fire,void") holds its spell on an area with what it throws beside
# it. With "barriers", every staff's figure inside its barrier instead. All eight casting are wider than the field
# between the fortresses can frame, so with none named they are taken four at a time.
magic_lineup() {
    if [ $# -eq 0 ]; then
        local half=0 elements
        for elements in fire,water,ice,wind lightning,earth,divine,void; do
            half=$((half + 1))
            magic_lineup "$elements" > /dev/null || return 1
            cp "$ROOT/_staging/magic-lineup.png" "$ROOT/_staging/magic-lineup-$half.png"
            echo "magic-lineup: $ROOT/_staging/magic-lineup-$half.png ($elements)"
        done
        return 0
    fi
    local which=(--magic-lineup "$1")
    [ "$1" = barriers ] && which=(--magic-barriers)
    screenshot --no-enemies --no-ui --at 1.2 "${which[@]}" || return 1
    cp "$ROOT/_staging/screenshot.png" "$ROOT/_staging/magic-lineup.png"
    echo "magic-lineup: $ROOT/_staging/magic-lineup.png"
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
            --zoom) args+=(--zoom "$2"); shift 2 ;;
            --armour-lineup) args+=(--armour-lineup); shift ;;
            --outfits) args+=(--outfits "$2"); shift 2 ;;
            --weapon-lineup) args+=(--weapon-lineup "$2"); shift 2 ;;
            --magic-lineup) args+=(--magic-lineup "$2"); shift 2 ;;
            --magic-barriers) args+=(--magic-barriers); shift ;;
            --look-lineup) args+=(--look-lineup "$2"); shift 2 ;;
            --gold-lineup) args+=(--gold-lineup "$2"); shift 2 ;;
            --ring-lineup) args+=(--ring-lineup); shift ;;
            --start-at) args+=(--start-at "$2"); shift 2 ;;
            --orb-chance) args+=(--orb-chance "$2"); shift 2 ;;
            --potion-chance) args+=(--potion-chance "$2"); shift 2 ;;
            --start-gold) args+=(--start-gold "$2"); shift 2 ;;
            --start-orbs) args+=(--start-orbs "$2"); shift 2 ;;
            --start-armour) args+=(--start-armour "$2"); shift 2 ;;
            --start-spent) args+=(--start-spent); shift ;;
            --trade-drill) args+=(--trade-drill); shift ;;
            --town-drill) args+=(--town-drill); shift ;;
            --status-drill) args+=(--status-drill); shift ;;
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
    local log="$ROOT/_staging/screenshot.log" limit=${SCREENSHOT_LIMIT:-120}
    timeout "$limit" "$GODOT" --position -10000,-10000 --path "$PROJECT" -- --ai-playtest --screenshot "${args[@]}" > "$log" 2>&1
    local status=$?
    grep -E '^\[screenshot\]' "$log"
    if [ "$status" -ne 0 ] || ! ls "$ROOT"/_staging/screenshot*.png > /dev/null 2>&1; then
        echo "screenshot FAILED ($(ended "$status" "$limit"), log: $log)"
        grep -E -A2 'ERROR|Unhandled exception' "$log" | head -10
        return 1
    fi
}

# Blender, for the tools that build models. No window: it runs in the background.
BLENDER="${BLENDER:-C:/Program Files/Blender Foundation/Blender 5.2/blender.exe}"

gold_piles() {
    if [ ! -f "$BLENDER" ]; then
        echo "gold-piles: no Blender at '$BLENDER' (set BLENDER to its executable)"
        return 1
    fi
    local log="$ROOT/_staging/gold-piles.log" limit=300
    mkdir -p "$ROOT/_staging"
    timeout "$limit" "$BLENDER" --background --factory-startup --python-exit-code 1 --python "$ROOT/Tools/gold_piles.py" < /dev/null > "$log" 2>&1
    local status=$?
    grep -E '^\[gold-piles\]' "$log"
    if [ "$status" -ne 0 ]; then
        echo "gold-piles FAILED ($(ended "$status" "$limit"), log: $log)"
        return 1
    fi
    echo "gold-piles: run ./dev.sh import for the game to see them"
}

# Where each weapon skill's hit time and range come from (Dev/SwingSurvey.cs); read it before setting them in Core.
swing_survey() {
    build || return 1
    local log="$ROOT/_staging/swing-survey.log" limit=60
    mkdir -p "$ROOT/_staging"
    timeout "$limit" "$GODOT" --headless --path "$PROJECT" -- --swing-survey --no-enemies > "$log" 2>&1
    local status=$?
    grep -E '^\[swing-survey\]' "$log"
    if [ "$status" -ne 0 ] || ! grep -q '^\[swing-survey\]' "$log"; then
        echo "swing-survey FAILED ($(ended "$status" "$limit"), log: $log)"
        grep -E -A2 'ERROR|Unhandled exception' "$log" | head -10
        return 1
    fi
}

# Runs every self-test even after a failure, so one report covers them all.
smoke() {
    local failed=() name
    for name in camera-test wall-test parts-test floors-test net-test pvp-test trade-test town-test magic-test; do
        echo "== $name"
        "${name//-/_}" || failed+=("$name")
    done
    if [ "${#failed[@]}" -gt 0 ]; then
        echo "smoke FAILED: ${failed[*]}"
        return 1
    fi
    echo "smoke passed (camera-test, wall-test, parts-test, floors-test, net-test, pvp-test, trade-test, town-test, magic-test)"
}

case "${1:-help}" in
    build) build ;;
    test) need_kit && dotnet test "$ROOT/Core.Tests/Core.Tests.csproj" && dotnet test "$KIT/tests/EverdawnKit.Core.Tests.csproj" ;;
    format) shift; dotnet format "$ROOT/WarriorsOfEverdawn.slnx" "$@" ;;
    import) timeout 300 "$GODOT" --headless --path "$PROJECT" --import --quit ;;
    net-test) net_test ;;
    pvp-test) pvp_test ;;
    trade-test) trade_test ;;
    town-test) town_test ;;
    magic-test) magic_test ;;
    camera-test) camera_test ;;
    wall-test) wall_test ;;
    parts-test) parts_test ;;
    floors-test) floors_test ;;
    ways-dump) shift; ways_dump "$@" ;;
    level-list) shift; python "$ROOT/Tools/level_list.py" "$@" ;;
    smoke) smoke ;;
    playtest) shift; playtest "$@" ;;
    swing-survey) swing_survey ;;
    armour-lineup) shift; armour_lineup "$@" ;;
    weapon-lineup) shift; weapon_lineup "$@" ;;
    magic-lineup) shift; magic_lineup "$@" ;;
    look-lineup) shift; look_lineup "$@" ;;
    gold-lineup) gold_lineup ;;
    ring-lineup) ring_lineup ;;
    gold-piles) gold_piles ;;
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
