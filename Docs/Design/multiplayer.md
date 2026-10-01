# Multiplayer

Solo and multiplayer are one code path. Up to 8 players, hosted by one of them (listen server). Solo is a host nobody joins: Godot's default offline peer acts as its own server, so the same spawn and authority rules run.

## Who owns what

| State | Owner |
|---|---|
| A player's movement and aim | That player's machine |
| Whether a player's own swings hit | That player's machine |
| Enemy AI and movement, HP, damage (computed by `Core`), deaths, spawns | Host |
| The map (walls, spawn spots, the safe town) | Nobody: every machine builds it from the same two layout files |
| Player HP, going down, getting back up | Host |
| Gold on the ground, and each player's gold and souls | Host |
| A player's mana and skill use | That player's machine |
| A player's weapons, in hand and on the back | That player's machine |
| Weapons lying on the ground, and who gets one that is reached for | Host |
| Raising and lowering a player's guard | That player's machine |
| Whether a guard blocks or parries | Host, by its own copy of the guard |
| Whether a player's swing hits another player (PvP) | The attacker's machine |
| Enemy attacks on players | Host for now; move to the player's machine if dashes feel unfair under lag |
| Loot | Host (not built yet) |

Co-op among friends, so cheating is not a design concern. Players owning their own movement avoids client-side prediction and correction, the hardest part of netcode.

No lockstep: nothing needs to be deterministic across machines. State is sent, not inputs.

## What goes over the network

- **Spawning.** The host's `MultiplayerSpawner` (`PlayerSpawner` in `Arena.tscn`) spawns one `PlayerCharacter` per peer, named by peer id and owned by that peer. It also replicates existing players to anyone who joins later.
- **Per player, 20 times a second, unreliable:** `NetPosition`, `NetVelocity`, `AimYaw`, through a `MultiplayerSynchronizer`. On the same synchronizer, sent when they change and to late joiners: `WeaponId` and `StowedWeaponId`, which every machine turns into the weapon in the player's hands (with its trail) and the one on the back, ghosts included.
- **Attacks:** reliable RPC `StartAttack(skillId)` from the owner to every peer; a held Spin ends with reliable `EndChannel`. Revolutions in between need no messages: every peer loops the clip itself.
- **Dashes:** the owner moves its own character through the dash and sends one reliable `StartDash(direction, spinsOn)`; every peer plays the clip and leaves the ghosts, or with `spinsOn` leaves the body to the Spin in progress. A lunge adds reliable `StartLunge(skillId, landsIn)`: every peer plays the stab so that it is fully extended `landsIn` seconds on, when its own copy of the dash ends.
- **Guard:** reliable `RaiseGuard` and `LowerGuard` from the owner to every peer. Every machine times the guard by its own clock (no clock sync), and the host's copy decides each attack against it: the parry window runs from when the raise reached the host, so a client parries a little later than it sees ([combat.md](combat.md), "Guard"). The host shows the outcome with `ShowGuarded`, and a parried skeleton with `ShowParried`.
- **Player HP, gold and souls:** a host-owned `Vitals` child on each player syncs `Hp`, `Gold` and `Souls` on change. Reliable RPCs from the host: `ShowHit`, `ShowGuarded(outcome)`, `Downed`, `Revived(position)`.
- **Enemies:** a second host `MultiplayerSpawner` (`EnemySpawner`). Per enemy, 10 times a second: `NetPosition`, `NetYaw`, `NetMoving`, `Hp`. Reliable RPCs from the host: `PlayAttack`, `ShowHit`, `ShowParried`, `Die`.
- **Weapons on the ground** (`GroundWeapons`, a node at the same path on every machine): a player's machine sends the host `RequestDrop(weaponId, at, yaw)` or `RequestPickUp(id)`. The host answers everyone with `Place(id, weaponId, at, yaw)` or `Take(id, byPeer)`, and the machine of `byPeer` puts the weapon in its player's free slot; a request for a weapon already gone gets `Refuse` back. The requester keeps its slots as they are until the answer comes. A joining machine is sent a `Place` for each weapon already down. An empty slot replicates as an empty weapon id.
- **Gold on the ground** (`Loot`, a node at the same path on every machine): the host sends everyone `Place(id, amount, at)` as a monster dies and `Take(id, byPeer)` when a player's reported position comes within reach of the pile; no machine asks for anything. A joining machine is sent a `Place` for each pile already down.
- **The map** needs no messages: every machine loads the same layouts and bakes the same navigation mesh ([level-layouts.md](level-layouts.md)). Whether a player is in the safe town is decided on the host, from the position the player last reported.
- **Arrows:** one reliable RPC from the host, `Arrows.Fly(id, attackId, from, direction)`, and every machine flies its own copy along the same straight line, ending it itself at its maximum distance. The host tests the hits and sends `Arrows.End(id)` only when one hits a player. `Arrows` is a node at the same path on every machine, which is what its RPCs need.
- **A player's hit on an enemy:** reliable RPC `RequestDamage(skillId)` from the attacker to the host only. The host applies `Core` damage and broadcasts the result.
- **Never skeleton poses.** Every machine derives the leg clip, body turn and torso twist from velocity and aim ([locomotion.md](locomotion.md)).
- **Skill ids, not button indexes,** travel in `StartAttack` and `RequestDamage`. The weapon travels separately on the synchronizer, so an index could be read against the weapon before a change; an id always names the skill that was swung.
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

- `./dev.sh net-test`: a headless host and three headless bot clients on one machine, one per weapon (greatsword, quarterstaff, spear, scythe) with a different one on each back, about 50 s, against tripled waves (`--wave-scale 3`). Each peer must see all the others move at least 3 units and attack, holding and carrying their weapons, and see each bot swap its sets and back; each must land its own hits and see a skeleton die; damage from all four must reach the host; skeleton attacks must land on someone; the torso twist must keep the chest near the aim; no character may turn faster than its limit, which must actually come into play; heads (including the warrior's bone-attached helmet) must be at their scale; every peer must spin, land spin hits and keep to half speed while spinning; and dashes must keep their distance and charges and show on the other machines. Logs land in `_staging/net-test/`.
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
