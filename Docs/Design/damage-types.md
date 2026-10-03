# Damage types

Every hit is of a damage type. The types, their families, their colours and what each does beyond damage are Everdawn's (`../Everdawn/Docs/Design/damage-types.md` and, beside it, its battle systems reference). A type decides three things: the colour a hit is shown in, what the body hit makes of it (its resistances), and which of the body's bars it builds (its statuses). Rules live in `Core/Combat` (`DamageType`, `Resistances`, `StatusBars`) and `Core/Stats` (`StatRules.Damage`, `StatRules.Afflict`); colours in `GodotClient/scripts/Theme` (`DamageTypeColours`, `StatusLooks`).

## The twelve

| Family | Types |
|---|---|
| Physical | Pierce, Blunt, Slash |
| Elemental | Fire, Water, Ice, Wind, Lightning, Earth |
| Astral | Divine, Void, Arcane |

Ice is the weaponised form of water and lightning of wind, as in Everdawn: each is an element of its own, with its staff, wand and enchantment ([magic.md](magic.md)), and is resisted as the natural element is. Arcane is resisted as whichever of divine and void the body resists less. Nothing deals arcane yet; it is in the list so the list is Everdawn's.

## What deals what

A skill has one type of its own (`SkillDefinition.Type`, which every skill has to name).

| Weapon | Type |
|---|---|
| Greatsword, scythe, sword and shield, claws | Slash |
| Quarterstaff, warhammer | Blunt |
| Spear, bow | Pierce |
| A staff's or a wand's bolt or volley, a wand's held spell, a staff's burst | Its element's: one of the eight |
| A staff's or a wand's poke | Blunt |
| Skeleton Minion, Skeleton Warrior | Slash |
| Skeleton Archer | Pierce |

- A weapon of steel or wood deals its one type with every skill, its lunge included.
- **An enchanted weapon's blow is of two types** (`SkillDefinition.Parts`): 60% the weapon's own and 40% the enchantment's ([magic.md](magic.md), "Enchanted weapons"). The physical part grows with STR and the magic part with WIS, as before.

## Resistances

A body can resist or be weak to each type (`Resistances`), as a percentage: 25 stops a quarter of a hit of that type, -25 lets a quarter more through. No body stops more than 90% (Everdawn's cap). A resistance is named for one of nine types (the twelve less ice, lightning and arcane, which go by the rule above) or for a whole family at once.

Each part of a hit is taken by what the body makes of that part's type, so a skeleton weak to fire and not to slash takes more only from the fire in a blow with an enchanted blade.

- **Enemies** carry theirs in `EnemyDefinition.Resistances`; an enemy that names none resists nothing.
- **Skeletons are weak to what is physical, to fire and to the divine**: 25% more from each (`Enemies.Skeletal`, `Enemies.SkeletonWeakness`). All three skeletons share it. First pass, and the one use of resistances so far.
- **Players** have no resistances: their armour and their guard take a share of every hit whatever its type ([combat.md](combat.md), [trade.md](trade.md)).
- The host works it out where it applies the damage, as it already did the damage itself ([multiplayer.md](multiplayer.md)).

- **Piercing finds the gaps**: what resists piercing resists it half as well as it says (Everdawn's armour-cracker). A weakness to it is no smaller for that.

## Statuses

Every body, skeleton or player, carries six bars (`StatusBars`), as in Everdawn: burn against cold, stun, bleed, illumination against corruption. A hit builds the bar of its type by the skill's `Buildup`; time wears the bars down; what a bar has reached is a status. Everdawn counts in turns and this game in seconds: **a turn there is one second here** (`StatusRules.Turn`), and a turn lost to being frozen or stunned is two (`StatusRules.LostTurn`). Every other number is Everdawn's.

| Type | Builds | What it brings |
|---|---|---|
| Fire | Burn (no top) | **Burning** while the bar is above 0: every second it deals what is on the bar, as fire. The bar then loses half, never less than 15 |
| Water | Cold, no further than 30 | **Chilled** at 30: run and attack speed at 80% |
| Ice | Cold, to 100 | Chilled at 30; **Frozen** at 100: held for 2 s, then out with at most 10 on the bar |
| Wind | Stun, no further than 30 | **Dizzy** at 30: its own damage at 80% |
| Blunt, earth, lightning | Stun, to 100 | Dizzy at 30; **Stunned** at 100: held for 2 s, then out with at most 5 on the bar |
| Slash | Bleed, to 100 | **Bleeding** while above 0: every second, 5% of the HP it has left, as piercing |
| Divine | Illumination | Lit by a foe's blow to 30 at most (Illuminated, which does nothing of its own); by its own casting to 100: **Blessed** at 40 (its divine damage 125%, 20 resistance to void), **Exalted** at 80 (150%, 40) |
| Void | Corruption, to 100 | Tainted above 0; **Defiled** at 40 (its void damage 125%, void resistance -20, divine +10); **Forsaken** at 80 (150%, -20, +20) |
| Pierce, arcane | Nothing | |

- **Opposites cancel.** Fire takes the cold bar down before it builds any burn, and water or ice put the burn out before they build any cold; the divine takes corruption down first and the void illumination. What is cancelled goes whatever the body resists; what is left builds the bar, less what the body resists (a weakness builds it faster).
- **Fire builds by the damage it did**: its buildup for every 60 damage (`StatusRules.BurnPerDamage`; Everdawn's 100 with its higher buildups), so a bolt of 38 starts a burn of 19.
- **Water never freezes and wind never stuns**: each stops at the soft status, and leaves a bar that ice or lightning pushed higher where it is.
- **Held** (frozen or stunned), a skeleton stands in the pose it was caught in, its swing called off, and is not moved even by the crowd; a player takes no step, starts no dash or skill, drops its guard, and the swing it was in lands nothing more. A frozen body takes no more cold and a stunned one no more stun until it is out.
- **Decay**, a second: cold 15, stun 20, bleed 10, illumination and corruption 10, burn half (15 at least).
- **Casting builds on the caster**: starting a skill adds 20 illumination to the caster for what of it is divine, and 20 corruption for what is void (by the share, on an enchanted weapon: 8). A held spell counts once, as it starts. So a divine caster that keeps casting is blessed in about four casts and a void caster defiled; a foe's void blow takes the blessing off, and a divine one the corruption.
- **Burns and wounds bite past any guard or barrier.** A player's armour takes its share of each as of any blow.
- **Buildups** (`Skills`, first pass): a swing 20, each turn of a Spin 6, a lunge 30, a bolt 30, each dart of a volley 15, each cycle of a held spell 8, a staff's burst 30. Of an enchanted blow each part builds its share: 60% on the weapon's bar, 40% on the element's. That is what an enchantment adds.
- **A skeleton's own blows build nothing yet** (`Buildup` 0): a minion's slash that left players bleeding 5% of their HP a second would near double what it deals. So players take statuses from other players only (PvP), and from their own casting.
- The host keeps the bars, as it keeps HP, and sends each body's statuses as one number (`StatusMask`); a player's own machine obeys it ([multiplayer.md](multiplayer.md)).

## How it shows

- **Damage numbers** are written in the hit's type's colour, Everdawn's (`ElementPalettes.DamageNumberColor` there): pierce rust brown, blunt gold, slash rose, fire red-orange, water teal, ice pale blue, wind paler blue, lightning yellow, earth ochre, divine near white, void purple. A hit of two types is written in the mean of the two colours. A hit of no type (the host taking a player down for a test) keeps the old red.
- A hit on a weakness is written larger (1.2 times) and one that is resisted smaller (0.85). With every skeleton weak to every physical type, most numbers today are the larger ones.
- **The skill bar** names each skill's type under its name, in that type's colour (the text colour of Everdawn's `EffectTypeBadgeColors`): "Slash", or "Slash + Fire" for an enchanted blade.
- **Weapon labels and the shop** write the type after the damage: "Slice 26 slash".
- **Statuses** are named over a body's HP bar, each in the colour of the type that brings it, the ones that hold a body first; the player's own under its bars on its frame. A body frozen stands in a block of ice, and "Frozen!" or "Stunned!" rises over it as it is caught. A burn's or a wound's bite is a smaller number in fire's or piercing's colour.
- The spells themselves are drawn in their element's look ([magic.md](magic.md)), which is Everdawn's too but a table of its own: ice's numbers are pale blue and its shards a fuller blue.

## Verified by

- `Core.Tests` (`DamageTypesTests`, `ResistancesTests`): the families, what is resisted through what, the type of every weapon skill, spell and monster attack, the two parts of an enchanted blow, and what a resistance or a weakness does to each part.
- `./dev.sh net-test`: on the host, hits of all three physical types land and every one meets the skeletons' weakness (`[combat-host] hits_by_type`, `weak_hits`, `plain_hits`).
- `./dev.sh magic-test`: hits that meet the weakness (the fire staff's, the pokes) and hits taken as they are (the water, ice and earth staffs').
- `Core.Tests` (`StatusBarsTests`): every bar's buildup, cancellation, soft and hard limits, decay, bites and what each status changes.
- `./dev.sh net-test`: every machine sees skeletons bleeding and names it over their bars; on the host the wounds bite.
- `./dev.sh magic-test`, with the host freezing a skeleton and stunning the next every 2.5 s (`--status-drill`, since skeletons die too soon to be frozen by play): every machine sees them frozen in their ice and stunned, neither moving while held, and skeletons burning.
- `./dev.sh pvp-test`, with the same drill on the two players: both seen bleeding, frozen and stunned, and each stays where it is and starts no swing while its own machine is told it is held.
- The colours are checked by eye (`./dev.sh screenshot --weapon water-staff --frames 8`), not by a test.

## Not here yet

- Everdawn's lessening of buildup hit by hit within one cast (0.7 a hit): every dart of a volley builds its full 15.
- Resistance to stun (a number of its own in Everdawn), tenacity, thermal protection, penetration.
- What the blessed and the corrupted do to healing and to barriers received: there is no healing here, and barriers are untouched.
- What skeletons' blows build on players.
- A look for burning, bleeding and the astral statuses on the body itself: only the frozen have one. A slowed walk for the chilled: they move slower, at the same stride.
- Skeletons seldom live long enough to be frozen or stunned: they have 30 to 70 HP. The hard statuses matter against players and whatever tougher comes.
- An arcane weapon.
- Anything that tells a player what an enemy resists before hitting it.
- Whether 25% is the right weakness, and whether all skeletons should share one.
