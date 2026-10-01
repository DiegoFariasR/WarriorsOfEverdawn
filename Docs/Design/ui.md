# UI

First-pass HUD in Everdawn's style. The layout will change; the look and the data behind it should carry over.

## Style

Ported from Everdawn's `EverdawnTheme` into `GodotClient/scripts/Theme/UiTheme.cs`: dark wood panels (`Wood` #3a2418, `WoodDk` #1e120a) with gold borders (`Gold` #d4a842), cream text (`TextMain` #c8b898), gold highlights (`GoldHi` #ffd878), red HP bars (`BarHp` #c43838) and blue mana bars (`BarMp` #3f8fcf). Words use **Cinzel**, emboldened like Everdawn's `BoldFont`; numbers use **Impact**. Floating damage numbers use Impact too.

**Font licences:** Cinzel is under the SIL Open Font License and can ship. **Impact is a Microsoft font that generally cannot be redistributed with a game.** Fine while prototyping; swap it for an open lookalike (for example Anton or Oswald) before any release. The swap is one path in `UiTheme.Numbers`.

## Elements

| Element | Where | Shows |
|---|---|---|
| Player frame | Top left | Name and weapon (with "PvP" when on), HP bar with value, mana bar with value, STR / WIS / AGI, gold and souls earned, "down" status |
| Skill bar | Bottom centre | The held weapon's two skills: primary (LMB) and Spin (RMB, held, with its mana per revolution, e.g. "4 MP / turn"); it changes with the weapon. A slot dims while it cannot be used (not enough mana, or a cooldown with seconds left). With an empty hand the slots and the guard read "-" and stay dimmed, and the player frame says "Unarmed" |
| Guard slot | Bottom centre, after the skills | "SHIFT", the guard, "hold"; the name turns gold while the guard is up and the slot dims for the 0.4 s it cannot go up again |
| Dash slot | Bottom centre, after Guard | "SPACE Dash", a pip per charge (lit while available), the seconds until the next charge, and "+ LMB: lunge" |
| Back weapon slot | Bottom centre, after Dash | "X", the weapon on the back ("Empty" with none), "on your back": what X swaps to |
| Ground weapon labels | Over each weapon on the ground within 4 of the player | The weapon's name. The one F would take (the one the player faces) also shows three lines of what it does in this player's hands (damage with the player's STR) and, within reach, "F - pick up" in gold, or in red that hands and back are full and G drops |
| Mode notice | Top centre | The camera mode on start and on each switch, and the new weapon sets on each change (fades after 2.5 s) |
| Overhead bars | Over every living skeleton and every other player who is up | A thin HP bar (48 x 5 px) with no frame and no number, small enough to leave the fight in view. Fill: red for skeletons and PvP opponents, blue for co-op allies (Everdawn's TeamPlayer). Removed when a skeleton dies, hidden while a player is down |

All values are live: HP from the host-owned vitals, skeleton HP from the replicated enemy state, stats from `Core`, mana from the player's own pool.

## Stats and mana

First pass, to be elaborated (`Core/Stats`):

| Stat | Knight | Effect |
|---|---|---|
| STR | 12 | +2.5% damage per point (x1.3: Slice 20 -> 26, Spin 10 -> 13 per revolution) |
| WIS | 5 | 10 max mana per point (Everdawn's `MpPerWis`): 50 |
| AGI | 8 | +1.25% attack speed per point (x1.1: swings at 2.2x instead of the 2x base) |

- **Mana** regenerates 5 per second on the player's own machine, where skills start. Spin costs 4 per revolution, about 13 per second at the Knight's attack speed, so a full pool holds about 6 s of spinning.
- **Skeletons** have no stats: base damage and the 2x base attack speed.
- The host applies damage with the attacker's STR, so a hit is worth the same on every machine.

## Verified

- `./dev.sh net-test`: on every machine, every frame, the player frame shows the local player's real HP and mana; every living skeleton and every other player who is up has a bar showing their real HP; no bar outlives its owner. Swings are measured playing at the AGI attack speed.
- `./dev.sh pvp-test`: each of two players sees the other's bar with the right HP.
- `./dev.sh camera-test`: at 1280x720 the player frame and skill bar lie fully on screen with an 8 px margin and do not overlap.
- Not verified: how it looks. The headless tests draw nothing.

## Open questions

- Stat effects beyond the first pass (HP from stats as in Everdawn, move speed, crits...).
- Mana regeneration from WIS rather than a flat rate.
- Final layout.
