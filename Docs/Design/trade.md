# Sellers and trade

Sellers stand in the allied town and trade what players have earned for goods. Four are planned; two are in: the weaponsmith, who sells weapons, and the blacksmith, who makes better what a player already has on: the weapon in its hand, the one on its back and the armour it wears. Every seller uses the same window, so a player who has used one has used them all.

## Sellers

| Seller | Stands | Trades | Offers |
|---|---|---|---|
| Weaponsmith | The town's armoury (the room on the right as one comes in by the gate) | Gold for weapons | The four weapons in their plain make, 50 gold each |
| Blacksmith | At the anvil in the courtyard | Gold and orbs for better weapons and armour | Three, always in the same slots: the weapon in the buyer's hand one level better, the weapon on its back one level better, and its armour one tier better |

- A seller is a figure on its spot with its name over its head, seen through walls so it can be found from across the town. It is solid, and walked round like any prop.
- Within 2.5 of one, "E - trade" shows over it. E (Start on a controller) opens its window. A player who is down, or already trading, gets no prompt.
- Where sellers stand is the town layout's business: a marker `seller-<id>` with the way they face ([level-layouts.md](level-layouts.md)). A marker for a seller nobody knows stops the arena from loading, and a seller with no marker fails a Core test.
- What a seller offers is `Core/Trade` (`Sellers`): a name, a line saying what for what, and up to nine offers, worked out from what the buyer carries and wears. An offer is a weapon or a tier of armour and a cost in gold, souls and orbs (most often one of them); a weapon has either nowhere in particular to go (one for sale) or the slot whose weapon it replaces (an improvement); and an offer has a reason when it cannot be had.

## Weapon levels

A weapon has a level from 0 (plain) to 10, shown in its name: "Greatsword +3". The level belongs to the weapon, not to whoever holds it: it stays with the weapon on the back, on the ground, and in another player's hands.

- **Damage:** each level adds a tenth of every skill's plain damage, and never less than 1, so every level hits harder than the last whatever the skill. A +10 weapon deals about twice what a plain one does. Nothing else about the weapon changes: reach, arc, timing, mana and guard are the plain weapon's.
- **The blacksmith's cost,** for the level reached (first-pass numbers):

| To | +1 | +2 | +3 | +4 | +5 | +6 | +7 | +8 | +9 | +10 | All ten |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Gold | 40 | 80 | 120 | 160 | 200 | 240 | 280 | 320 | 360 | 400 | 2200 |
| Orbs | 1 | 1 | 2 | 2 | 3 | 3 | 4 | 4 | 5 | 5 | 30 |

- The blacksmith's first two slots are the hand's weapon and the back's, in that order. An empty slot, or a weapon already at +10, shows greyed with the reason.
- Under the hood an improved weapon is its plain definition with harder-hitting skills and an id that carries the level (`greatsword+3`), so everything that moves weapons about by id (the hand and back a player's machine reports, weapons on the ground, `--weapon`) carries the level with no more said. How it looks and is held goes by its kind.
- Skills still travel between machines by their plain ids. Whoever needs what a skill does asks the weapons the player carries (`WeaponSets.SkillById`): the host works out a hit's damage from the weapon it sees in the attacker's hand, so a machine cannot claim a level it does not have.
- Q, which changes the weapon in hand to the next kind, gives a plain weapon.

## Armour

Every player wears one tier of a single line of armour (`Core/Combat/Armour`), starting in worn clothes, which stop nothing. The blacksmith's third slot is the next tier up from what is worn, so the line is climbed a tier at a time and never out of turn:

| Tier | 1 Leather | 2 Mail | 3 Scale | 4 Plate | 5 Tempered plate | All five |
|---|---|---|---|---|---|---|
| Stops of every blow | 10% | 20% | 30% | 40% | 50% | |
| Gold | 60 | 120 | 200 | 300 | 420 | 1100 |
| Orbs | - | - | 1 | 2 | 3 | 6 |
| Looks like (KayKit character) | Rogue | Cleric | Engineer | the Knight's own | Paladin, with its breastplate | |

- **Gold alone for the first two tiers, gold and orbs after.** First-pass numbers.
- **What it stops:** the guard takes its share of a blow first, then the armour takes its own of what is left. It works against skeletons' blows and arrows and, in PvP, other players'.
- **Small blows:** damage is whole numbers and a minion hits for 6, so rounding each blow alone would make one tier no better than the last against it. What a blow keeps past the armour is worked out to the fraction, the whole part dealt and the rest carried into the next blow (`ArmourWear`), so over a fight the share is exact: a hundred blows of 6 in Leather deal 540.
- Armour belongs to the player, not to a thing that can be dropped: it is bought once, worn at once, and kept for the session, through going down and getting back up.
- The slot shows the next tier by name with its cost, and the detail pane how much of a blow it stops against how much is stopped now, and the tier out of 5. Once it is bought the same slot offers the tier after. In tempered plate it shows greyed: as good as it can be made.
- Weapons and armour were first two sellers; they are one because both are the same thing to the player, the next step up for something it has on, and three offers read at a glance where a second window of five mostly greyed tiers did not.
- The player frame shows the tier worn as ARM beside STR, WIS and AGI ([ui.md](ui.md)).
- **It shows on the figure.** Each tier is the body, arms and legs of one KayKit character worn in place of the Knight's own, the head and helmet kept, so a player is still told by its helmet and cape (`Character/ArmourLook`). Worn clothes are the Hoarder's tunic; the Knight's own plate, which everyone used to start in, is now tier 4. This is Everdawn's way of showing armour (its armours' `appearance`) with Everdawn's part models: all of them are skinned to the same rig, so a part goes under the figure's skeleton as it is. Every machine dresses each player from the tier the host holds for it, and the ghosts a dash leaves wear it too. Which character stands for which tier is a first pick, made from `./dev.sh armour-lineup`, which shows the six side by side from the front (or any other outfits named, to try them).
- The figure looks the same in every tier.

## The window

The same for every seller (`Main/ShopPanel`), opened over the game, which dims behind it:

| Part | Shows |
|---|---|
| Heading | The seller's name and its line ("Plain weapons for gold"); on the right, the gold, souls and orbs the player carries |
| Slots | Nine, in three rows of three, numbered 1 to 9. A slot with an offer shows its name and cost ("80 gold + 1 orb" on two lines); the rest show a dash. A cost the player cannot pay is red; an offer that cannot be had is greyed |
| Chosen offer | To the right: the name; what it is (a weapon's three lines as a weapon on the ground shows them, or for an improvement what each skill deals now and would deal, and the level out of 10); the cost and how much the player is short; what having it does; the outcome of the last purchase; and the button, "Buy" or "Improve" |
| Foot | The keys |

- **Choosing:** click a slot, press its number, or walk to it with the arrows, WASD or the d-pad. The first slot is chosen as the window opens.
- **Buying:** E, Enter, A on a controller, or the button, which is greyed while the offer cannot be had or paid for. The window stays open and reads the offers afresh, so several things can be had in one visit: after an improvement the same slot offers the next level or tier.
- The window opens on the first offer that can be had, and the choice moves to one that can if the chosen offer stops being so.
- **Closing:** Esc, or B on a controller. It also closes if the player goes down.
- The player stands still while the window is open: its controls ask for nothing.
- Nine slots because that is what the number keys reach and what fits beside the detail pane at the game's window size (the window is 804 by 440 in a 1152 by 648 game). A seller with more than nine offers would need pages; none has.

## Buying a weapon

- The weapon goes into the hand if it is empty, else onto the back if that is empty. With both full it takes the place of the weapon in hand, which is put down at the player's feet, not lost: it can be picked up again like any weapon on the ground once a slot is free. The window says which of the three will happen before the purchase.
- A weapon already carried can be bought again.
- Prices are first-pass placeholders.

## Who decides

The host holds every purse, so it decides every purchase ([multiplayer.md](multiplayer.md)). The buyer's machine asks (`Market.RequestBuy(seller, offer)`); the host works out what that seller offers that player from the weapons it sees them carry, checks that the player is up, within reach of the seller by the position it last reported, that the offer can be had and that the player can pay, takes the cost and answers that machine alone with `Deliver(seller, offer, weapon, slot, armour tier)`, or with `Refuse` and the reason. On `Deliver` the buyer's machine puts that weapon in its player's hands, or in the slot it improves. Armour the host puts on the player itself as it takes the cost, since it is the host that reckons every blow; the tier worn reaches every machine with the player's HP and purse. The window does not send a request the player plainly cannot pay for; the host refuses one anyway.

## Verified by `./dev.sh trade-test`

For each seller the town's layout stands, a host and one bot client start a step and a half in front of it (`--start-at`, `--trade-drill`) with exactly what the seller's first three offers come to (`--start-gold`, `--start-orbs`): at the weaponsmith 150 gold for three plain weapons; at the blacksmith, starting in Mail, the last tier of armour had for gold alone (`--start-armour`), 280 gold and 3 orbs for the weapon in hand made +1, the one on the back made +1 and Scale, the first tier paid in gold and orbs. On each machine: the prompt shows; the window opens once, for that seller, has nine slots holding the seller's offers, and is no bigger than the game's window; the bot gets through the window the first three offers in slot order; is then told by the window that it cannot pay for another, asks the host anyway and is refused; its gold and orbs are what it started with less what it paid; the weapons in its hand and on its back are what those purchases leave (`greatsword+1` and `spear+1` on the host after the blacksmith) and the armour got is worn and shown on the HUD (tier 3); both players' figures on that machine wear the parts of the armour each has, never out of step for more than the frames in which it changes, and at the blacksmith the bot leaves in another look than it came in (Cleric to Engineer) while at the weaponsmith it does not; it did not move while the window was open; and the window closes. On the host: sales went to both players, and the weapon in hand, the gold, the orbs and the armour it holds for each player are what that player's own machine has. Part of `./dev.sh smoke`.

That an improved weapon hits harder where it counts was checked in a solo fight on the host: a greatsword's Spin lands 13 a hit plain and 26 at +10, with the plain spear on the same player's back unchanged (`[combat-host] biggest_hit_by_skill`). That armour stops what it says was checked the same way: the 100 damage that takes a player down came in 12 blows in worn clothes and in 25 in tempered plate (`[combat-check] damage_taken`, `hits_taken`, with `--start-armour`). The rules themselves (what a purse can pay, reach, being down, where a bought weapon goes, every level of every weapon hitting harder than the last, every level costing more, a weapon improved step by step to +10 and no further, every tier of armour stopping more and costing more, gold alone for the first two, armour made better tier by tier to the last and no further, the blacksmith's three offers in their order, the exact share over many small blows, that every seller's offers fit the window) are `Core.Tests/Trade`, `Core.Tests/Combat/WeaponsTests`, `WeaponSetsTests` and `ArmourTests`.

## Open questions

- The other two sellers: who they are, what they trade for which currency, and where they stand. Souls still buy nothing.
- Which character's parts stand for which tier of armour: a first pick. Everdawn tints parts by palette, which would give more looks from the same models (a darker, bluer plate for the tempered one); nothing is tinted here yet.
- Whether tempered plate stopping half of every blow, on top of a guard, is too much: armour's numbers are first-pass.
- Q still changes the weapon in hand to the next kind for nothing, which was how weapons were tried before there was a seller. It makes buying one pointless, and swapping to a kind and back loses its level; whether it goes, or stays as a development key only.
- Prices, the blacksmith's costs and how much a level adds: all first-pass. Thirty orbs for a full +10 is a long way at a few orbs in a hundred monsters.
- Whether an improved weapon should look it (a glow or a colour by level): today only its name tells.
- Whether the weaponsmith should sell better makes, or buy weapons back.
- Whether a weapon put down by a purchase should be sold back instead.
- Whether the window should close when another player attacks the trader in PvP.
