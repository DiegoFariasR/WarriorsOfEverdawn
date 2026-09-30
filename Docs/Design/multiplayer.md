# Multiplayer

Solo and multiplayer are one code path. Up to 8 players, hosted by one of them (listen server). Solo is a host nobody joins: Godot's default offline peer acts as its own server, so the same spawn and authority rules run.

## Who owns what

| State | Owner |
|---|---|
| A player's movement and aim | That player's machine |
| Whether a player's own swings hit | That player's machine |
| Enemy AI and movement, HP, damage (computed by `Core`), deaths, spawns | Host |
| Player HP, going down, getting back up | Host |
| A player's mana and skill use | That player's machine |
| Whether a player's swing hits another player (PvP) | The attacker's machine |
| Enemy attacks on players | Host for now; move to the player's machine if dodges feel unfair under lag |
| Loot | Host (not built yet) |

Co-op among friends, so cheating is not a design concern. Players owning their own movement avoids client-side prediction and correction, the hardest part of netcode.

No lockstep: nothing needs to be deterministic across machines. State is sent, not inputs.

## What goes over the network

- **Spawning.** The host's `MultiplayerSpawner` (`PlayerSpawner` in `Arena.tscn`) spawns one `PlayerCharacter` per peer, named by peer id and owned by that peer. It also replicates existing players to anyone who joins later.
- **Per player, 20 times a second, unreliable:** `NetPosition`, `NetVelocity`, `AimYaw`, through a `MultiplayerSynchronizer`.
- **Attacks:** reliable RPC `StartAttack(index)` from the owner to every peer; a held Spin ends with reliable `EndChannel`. Revolutions in between need no messages: every peer loops the clip itself.
- **Dodges:** the owner moves its own character through the dodge and sends one reliable `StartDodge(direction)`; every peer plays the clip and leaves the ghosts.
- **Player HP:** a host-owned `Vitals` child on each player syncs `Hp` on change. Reliable RPCs from the host: `ShowHit`, `Downed`, `Revived(position)`.
- **Enemies:** a second host `MultiplayerSpawner` (`EnemySpawner`). Per enemy, 10 times a second: `NetPosition`, `NetYaw`, `NetMoving`, `Hp`. Reliable RPCs from the host: `PlayAttack`, `ShowHit`, `Die`.
- **A player's hit on an enemy:** reliable RPC `RequestDamage(skillIndex)` from the attacker to the host only. The host applies `Core` damage and broadcasts the result.
- **Never skeleton poses.** Every machine derives the leg clip, body turn and torso twist from velocity and aim ([locomotion.md](locomotion.md)).
- **Remote players** smooth their position and aim toward the latest update. No buffered interpolation yet; add it if remote players look jittery over a real connection.
- Clients reach each other through the host (Godot's server relay).

## PvP

The host chooses co-op or PvP for the session (`./dev.sh host --pvp`) and sends the choice to each client as it joins; a client's own `--pvp` is ignored. In PvP:

- Every player's swings also test the other players, as the attacker's machine sees them, and report hits to the host (`PlayerVitals.RequestDamage`). The host applies the damage with the attacker's STR through the victim's host-owned vitals, so going down and getting back up work as in co-op.
- Skeletons still come unless the host passes `--no-enemies`.
- Other players' overhead bars have a red border instead of blue, and the player frame shows "PvP".

## Connecting

- Direct IP over ENet, default port 7777. Works on a local network; over the internet the host has to forward the port.
- Release route undecided. Options: Steam networking (lobbies, invites, relay around routers; needs a Steam app, dev can use test app 480; Godot 4.7 C# support to be checked) or Epic Online Services (free, not tied to Steam). Switching is a `MultiplayerPeer` swap and does not touch gameplay code.

## Testing

- `./dev.sh net-test`: a headless host and two headless bot clients on one machine, about 25 s. Each peer must see both others move at least 3 units and attack; each must land its own hits and see a skeleton die; damage from all three must reach the host; skeleton attacks must land on someone; the torso twist must keep the chest near the aim; no character may turn faster than its limit, which must actually come into play; heads (including the warrior's bone-attached helmet) must be at their scale; every peer must spin, land spin hits and keep to half speed while spinning; and dodges must keep their distance and charges and show on the other machines. Logs land in `_staging/net-test/`.
- `./dev.sh pvp-test`: a PvP host and one bot client, no skeletons, about 20 s. Each player must hit the other, take damage, and see the other's bar; the host must count damage from both.
- `./dev.sh host` and `./dev.sh join <address>`: two real windows, for looking at it.

## Known engine issue

When a client leaves, the host sometimes logs `Unable to send packet on channel 0, max channels: 0`: its `MultiplayerSynchronizer` sends once more to the departing peer. Open upstream as [godot#86814](https://github.com/godotengine/godot/issues/86814). Harmless: the host keeps running and handles later departures. `net-test` reports it as a note instead of failing.

## Open questions

- Joining a game in progress: players and enemies already replicate to late joiners, but a late joiner sees every existing skeleton play its rise-from-the-ground clip.
- Enemy count and health scaling with player count.
- Loot: shared or per player.
- PvP scoring, teams, and whether PvP respawns should differ from co-op.
- Where characters are saved.
- Release connection route (see Connecting).
