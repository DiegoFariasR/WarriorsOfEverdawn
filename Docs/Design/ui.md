# UI

First-pass HUD in Everdawn's style. The layout will change; the look and the data behind it should carry over.

## Style

Ported from Everdawn's `EverdawnTheme` into `GodotClient/scripts/Theme/UiTheme.cs`: dark wood panels (`Wood` #3a2418, `WoodDk` #1e120a) with gold borders (`Gold` #d4a842), cream text (`TextMain` #c8b898), gold highlights (`GoldHi` #ffd878), red HP bars (`BarHp` #c43838) and blue mana bars (`BarMp` #3f8fcf). Words use **Cinzel**, emboldened like Everdawn's `BoldFont`; numbers use **Impact**. Floating damage numbers use Impact too, each in the colour of its damage type (Everdawn's, `Theme/DamageTypeColours`; [damage-types.md](damage-types.md)).

**Font licences:** Cinzel is under the SIL Open Font License and can ship. **Impact is a Microsoft font that generally cannot be redistributed with a game.** Fine while prototyping; swap it for an open lookalike (for example Anton or Oswald) before any release. The swap is one path in `UiTheme.Numbers`.

## Elements

| Element | Where | Shows |
|---|---|---|
| Player frame | Top left | Name and weapon (with "PvP" when on), HP bar with value, mana bar with value, STR / WIS / AGI and ARM (the tier of armour worn, from 0), while there are points to spend their count and a "+ STR", "+ WIS" and "+ AGI" button, gold, souls and orbs earned (the orbs' count tinted with the colour the orbs are passing through; the souls' count swelling, lit white, as each soul's wisp reaches the player), "down" status |
| Skill bar | Bottom centre | The held weapon's two skills: primary (LMB) and Spin (RMB, held, with its mana per revolution, e.g. "4 MP / turn"); it changes with the weapon. Under each skill's name, its damage type in that type's colour ("Slash", or "Slash + Fire" on an enchanted weapon). A slot dims while it cannot be used (not enough mana, or a cooldown with seconds left). With an empty hand the slots and the guard read "-" and stay dimmed, and the player frame says "Unarmed" |
| Guard slot | Bottom centre, after the skills | "SHIFT", the guard, "hold"; the name turns gold while the guard is up and the slot dims for the 0.4 s it cannot go up again. A staff's barrier shows what is left of it: "Barrier 28" |
| Long names | Player frame, shop slots | The frame's title is cut with an ellipsis at the width of its bars ("Knight - Greatsword of..."), so a long weapon name does not stretch the frame under the notice; the notice is centred in what the frame leaves of the top of the screen. A shop slot can show a short label in place of the offer's name (the enchanter's show the element) |
| Dash slot | Bottom centre, after Guard | "SPACE Dash", a pip per charge (lit while available), the seconds until the next charge, and "+ LMB: lunge" |
| Potion slot | Bottom centre, after Dash | "R", "Potion", a red pip per charge of the health potion (lit while it is there), "+40 HP" and the seconds until the next drink; dimmed with none left ([combat.md](combat.md), "Health potion") |
| XP bar | Bottom centre, under the skill bar and as wide | Purple fill (this game's own colour: Everdawn has no XP), "Level 5   94 / 253 XP": the level, the XP toward the next and what the next asks ("Levels") |
| Back weapon slot | Bottom centre, after the potion | "TAB", the weapon on the back ("Empty" with none), "on your back": what Tab swaps to |
| Ground weapon labels | Over each weapon on the ground within 4 of the player | The weapon's name. The one F would take (the one the player faces) also shows three lines of what it does in this player's hands (damage with the player's STR) and, within reach, "F - pick up" in gold, or in red that hands and back are full and G drops |
| Seller signs | Over each seller | The seller's name, seen through walls; within 2.5 of the player, "E - trade" above it |
| Seat prompt | Over the free seat in reach | "E - sit" or "E - lie down", while E would take it: no seller in reach, the player not trading, down or already on a seat (`SeatPrompt`; [level-layouts.md](level-layouts.md), "Seats") |
| Seller names | Over each seller | Its name in gold, and above it "E - trade" while the player is in reach and free to trade; on the screen, one size in every camera (`SellerLabels`) |
| Shop window | Centre, over a dimmed game, while trading | The one window every seller uses: heading and purse, twelve slots, the chosen goods with price and what buying does, the Buy button, the keys ([trade.md](trade.md)) |
| Mode notice | Top centre | The camera mode on start and on each switch, the new weapon sets on each change, and each level reached with its point (fades after 2.5 s) |
| Overhead bars | Over every living skeleton within 12 m of the player (`ArenaMap.SkeletonOverheadReach`; its statuses and damage numbers too) and every other player who is up | A thin HP bar (48 x 5 px) with no frame and no number, small enough to leave the fight in view. Fill: red for skeletons and PvP opponents, blue for co-op allies (Everdawn's TeamPlayer). Removed when a skeleton dies, hidden while a player is down |

All values are live: HP from the host-owned vitals, skeleton HP from the replicated enemy state, stats from `Core`, mana from the player's own pool.

## Stats and mana

First pass, to be elaborated (`Core/Stats`):

| Stat | Knight | Effect |
|---|---|---|
| STR | 12 | +2.5% physical damage per point (x1.3: Slice 20 -> 26, Spin 10 -> 13 per revolution) |
| WIS | 5 | +5% magic damage per point (x1.25), and 10 max mana per point (Everdawn's `MpPerWis`): 50 |
| AGI | 8 | +1.25% attack speed per point (x1.1: swings at 2.2x instead of the 2x base), and +1.25% run speed (x1.1: runs at 5.5 instead of 5, [locomotion.md](locomotion.md)) |

A skill's damage grows with STR for the part of it that is physical and with WIS for the part that is magic, so a fire-enchanted sword grows with both ([damage-types.md](damage-types.md)).

- **Mana** regenerates 5 per second on the player's own machine, where skills start. Spin costs 4 per revolution, about 13 per second at the Knight's attack speed, so a full pool holds about 6 s of spinning.
- **Skeletons** have no stats: base damage and the 2x base attack speed.
- The host applies damage with the attacker's STR and WIS, so a hit is worth the same on every machine.

## Levels

First pass (`Core/Stats/Progress.cs`, `LevelRules`):

- **XP:** every player gets a monster's XP as it dies, as it gets its souls ([combat.md](combat.md)): 10 for a minion, 12 for an archer, 20 for a warrior. Kept by the host, for the session; going down loses none.
- **Each level asks 1.5 times the XP of the one before:** 50 from level 1 to 2, then 75, 112, 169, 253, 380, 570, 854, 1281, 1922 (3744 in all to reach level 11). No cap; far enough up the XP no longer fits in an int and the rule throws.
- **A level gives one point**, to put into STR, WIS or AGI with the buttons in the player frame; points wait until they are spent. "Level N!" floats over the player on every machine, and its own machine's notice says a point is there to spend.
- **What a point does** is what the stat does (the table above): more physical damage, more magic damage and mana, or faster swings and a faster run. A point into WIS grows the mana pool and what is in it by 10 at once.
- **A click on a button is no swing:** the left button pressed over one of the HUD's buttons does not attack until it is let go (`HumanControls`).
- `--start-xp N` starts every player with that much XP, for trying levels ([GodotClient/AGENTS.md](../../GodotClient/AGENTS.md)).

## Verified

- `./dev.sh net-test`: on every machine, every frame, the player frame shows the local player's real HP and mana; every living skeleton and every other player who is up has a bar showing their real HP; no bar outlives its owner. Swings are measured playing at the AGI attack speed of the moment. `[level-check]`: every player reaches level 2 or more and every machine sees levels reached; each bot puts its points into STR, WIS and AGI in turn by pressing the HUD's buttons; on every machine, every frame, each player's stats are its start and a point a level less those it has yet to spend; the mana pool is as big as WIS makes it (10 a point), catching up in the physics step after a point goes in; the HUD shows the level, XP and points; and on the host its own player's XP is exactly the XP of the monsters it saw die.
- `./dev.sh pvp-test`: each of two players sees the other's bar with the right HP.
- `./dev.sh camera-test`: at 1280x720 the player frame, skill bar and XP bar lie fully on screen with an 8 px margin and do not overlap.
- Not verified: how it looks. The headless tests draw nothing.

## Open questions

- Stat effects beyond the first pass (HP from stats as in Everdawn, move speed, crits...).
- Mana regeneration from WIS rather than a flat rate.
- Final layout.
