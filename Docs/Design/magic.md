# Magic staffs

Six staffs, one for each of Everdawn's four elements (fire, water, wind, earth) and two astral types (divine, void). A staff is a weapon like any other ([combat.md](combat.md)): it takes one slot, is bought, dropped, picked up, improved and sold as the others are, and has the same four things. What differs is what those four are: the primary throws a bolt or a volley, the secondary holds a spell on an area, the guard is a barrier, and the lunge is a poke with the staff. All numbers are first-pass and live in `Core/Combat` (`Skills.StaffOf`, `Projectiles`, `Weapons.Barrier`).

## The six

| Staff | Primary | Held spell | Area | Damage a cycle | Mana a cycle | Looks like |
|---|---|---|---|---|---|---|
| Fire | Fire Bolt | Inferno | 2.5 round a spot 4 ahead | 10 | 6 | Pillars of fire standing up out of the ground |
| Water | Water Volley | Blizzard | 3.5 round a spot 4 ahead | 6 | 5 | Shards falling out of the sky |
| Wind | Wind Volley | Lightning Storm | 3 round a spot 4 ahead | 8 | 5 | Bolts from the sky, there and gone |
| Earth | Earth Bolt | Earthquake | 3 round a spot 4 ahead | 8 | 5 | Spikes of rock thrust up from below |
| Divine | Divine Bolt | Divine Nova | 3.5 round the caster | 8 | 5 | A ring of light spreading from the middle |
| Void | Void Volley | Void Corrosion | 3 round a spot 4 ahead | 7 | 4 | Dark swellings that linger |

Which staff throws a bolt and which a volley, and each one's area spell, are Everdawn's: its `fire-bolt`, `earth-bolt` and `divine-bolt`, its volleys, and its Inferno, Earthquake, Blizzard, Lightning Storm, Divine Nova and Void Corrosion. Everdawn's Blizzard and Lightning Storm belong to ice and lightning, the weaponised forms of water and wind, which have no staff of their own here.

## Bolt and volley (primary)

- **A bolt** is one ball: 30 damage, flying 16 a second for up to 12, touching anything within 0.35 of its path. **A volley** is three darts of 10, loosed 0.12 s of clip time apart, flying 22 a second for up to 12 and touching within 0.2. Thrown together they deal what a bolt does; spread over a moving target, some miss.
- Thrown with `Ranged_Magic_Shoot`; the first leaves the staff 0.3 s into the clip, where the staff is furthest forward (`swing-survey`). No mana. **One second between throws** (the skill's cooldown): without it a bolt out-dealt every blade from twelve times its reach.
- It flies level at chest height, along the aim as it was when it left, and ends at the first body it touches, at a wall, or at the end of its distance. It does not go through one body to the next.
- A cast cut short by a dash throws what it had thrown.

## The held spell (secondary)

- Held like a Spin: a cycle is one loop of the casting clip (`Ranged_Magic_Spellcasting`, 0.67 s; about 0.3 s at the Knight's attack speed), paid for in mana as it starts. Everything in the area takes the spell's damage once a cycle; what walks in mid-cycle takes it as it enters. The caster moves at half speed.
- **The area** is a circle on the ground: for five of the six, round the spot 4 ahead of where the caster faces, so it is steered by aiming and by walking; for the Divine Nova, round the caster. A body touching the circle is in it.
- An area ahead does not cover the caster's own feet: what stands closer than the near edge (1.5 for Inferno, 1 for the 3-wide, 0.5 for Blizzard) is not hit. The Nova is the answer to a crowd at arm's length.
- A ring on the ground shows the area for as long as the spell is held, and a round of strikes falls inside it each cycle.

## Barrier (guard)

Every staff has the same one. Holding Shift raises a shell all round the caster:

- **It takes 40** before it gives. While it holds, a blow from any side is stopped whole and comes off the barrier; a blow bigger than what is left spends it, and the rest gets through. Spent, it stops nothing, and no shell shows.
- **It comes back** at 8 a second once it has been down for 2 s, to full; never while it is up.
- It parries nothing, and the caster moves at half speed inside it.
- The skill bar's guard slot shows what is left ("Barrier 28"). The shell is drawn in the staff's element's colour and thins as it is spent.
- The barrier belongs to the player, not the staff: swapping to another staff does not bring a fresh one.

## Lunge

A dash with the primary held throws a thrust with the staff, as with any weapon: 24 damage, reach 2.1, no spell. The least of the lunges.

## What a spell grows with

A spell's damage grows with WIS (5% a point) where a blow's grows with STR (2.5% a point), so a staff's lunge goes by STR and its spells by WIS (`StatRules.Damage`). The Knight's WIS of 5 makes a bolt of 30 hit for 38. The blacksmith improves a staff like any weapon: each level adds a tenth to the bolt (each dart of a volley), the spell and the poke.

## How it looks

- **The staff** is one model (`staff`) for all six, with a ball of the element at its head, big enough to take in the model's own green gem. On the back it is carried like any pole.
- **The elements** take Everdawn's colours (`ElementPalettes`), its surface shaders (`elemental_<element>.gdshader`, the brightest layer of each), its ball for a bolt and its bipyramid for a dart, and its honeycomb shader for the barrier (`barrier_hex`). Wind and divine use the second of Everdawn's two colours first, pale blue and gold: its firsts are both near white. All of it is in `Character/ElementLooks`.
- **Casting** uses the two-handed stance between spells, `Ranged_Magic_Shoot` to throw and `Ranged_Magic_Spellcasting` to hold. The barrier plays the same `Melee_Blocking` every guard does.
- `./dev.sh magic-lineup [fire,void,...]` shows the staffs casting side by side, each with its area spell running and what it throws beside it; `./dev.sh magic-lineup barriers` shows each inside its barrier. `./dev.sh weapon-lineup fire-staff` shows the poses.

## Where to get one

The **Arcanist**, the fourth seller, stands on a rug by a shrine of candles at the front of the town's courtyard and sells the six staffs plain, 50 gold each, through the same window as the others ([trade.md](trade.md)). The weaponsmith keeps to steel and wood: eleven weapons would not fit its nine slots.

## Who decides

- **Bolts** (`Main/Bolts`): the caster's machine looses each one (`LooseBolt`, to every machine) and decides what it hits, as it does for its swings; every machine flies its own copy along the same line and ends it at a wall on its own. A hit is reported to the host by skill id like any other, and `EndBolt` ends the copies. The host works out the damage from the weapon it sees the caster hold.
- **The held spell**: the caster's machine tests its area each cycle and reports the hits, as for a Spin; every machine draws the ring and the strikes from the skill it was told started and where it sees the caster aim.
- **The barrier**: raising it is the player's machine's, like any guard. What is left of it is the host's (`PlayerVitals.Barrier`), which takes blows off it and brings it back, and reaches every machine with the player's HP.

## Verified by `./dev.sh magic-test`

A host and two bot clients with the fire, water and earth staffs in hand and wind, void and divine on their backs, about 40 s against doubled waves. Staff bots keep a hostile at their spell's distance, throw at it from as far as a bolt flies (leading a moving one), hold their spell on it while they can pay, and throw for half of every five seconds whatever else is due. On each machine: its player throws bolts, as many to a cast as the skill has (one, or three), some land, the others' bolts are seen, each ends in a burst and none outlives its flight; its spell is drawn in rounds of strikes and the others' are seen; barriers show here and on the others, and its own takes blows and is seen coming back; the staff's thrust reaches as far as its lunge's range. On the host: every player's bolts and spells landed hits, and every player's barrier was dealt a blow as it went up (`--barrier-drill`: 6, through the same path as a skeleton's) and took it, at no cost in HP. Part of `./dev.sh smoke`.

The rules themselves are `Core.Tests/Combat/StaffsTests` (a staff per element, bolt or volley by element, a volley dealing what a bolt does, areas ahead and round the caster, the barrier's pool and its return, WIS and STR, improved staffs) and `Core.Tests/Trade`.

## Not here yet

- **What the elements do beyond damage.** In Everdawn fire burns, water chills, wind and earth stun, divine and void have their own bars, and each type has its resistance. Here an element is a look and a shape; skeletons resist nothing.
- Ice, lightning and arcane, Everdawn's variants.
- PvP with staffs runs through the same code as swings (bolts and areas hit players, barriers take the blows) but no self-test covers it.
- All six staffs are one model. Whether each should have its own.
- A cast or a casting stance of its own for the barrier.
- Balance: bolt damage and its second between throws, the areas' sizes, the barrier's 40. Skeletons do not threaten a caster that keeps its distance.
