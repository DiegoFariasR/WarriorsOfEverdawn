# Magic: staffs, wands and enchantments

Two magic weapons, each in eight elements, all Everdawn's: its four (fire, water, wind, earth), the weaponised forms of two of them (ice of water, lightning of wind), and its two astral types (divine, void). A **staff** throws a bolt or a volley, holds a spell on an area and raises a barrier. A **wand and book** throw the same bolt or volley and raise the same barrier, and in place of the held spell throw a ball that bursts. Either is a weapon like any other ([combat.md](combat.md)): it takes one slot, is bought, dropped, picked up, improved and sold as the others are, and has the same four things (primary, secondary, lunge, guard). All numbers are first-pass and live in `Core/Combat` (`Skills.StaffOf`, `Skills.WandFor`, `Projectiles`, `Weapons.Barrier`). The sections down to the lunge describe the staff; the wand's differences follow them.

## The eight

| Staff | Primary | Held spell | Area | Damage a cycle | Mana a cycle | Looks like |
|---|---|---|---|---|---|---|
| Fire | Fire Bolt | Inferno | 2.5 round a spot 4 ahead | 10 | 6 | Pillars of fire standing up out of the ground |
| Water | Water Volley | Geyser | 3 round a spot 4 ahead | 6 | 4 | Columns of water standing up out of the ground |
| Ice | Ice Volley | Blizzard | 3.5 round a spot 4 ahead | 6 | 5 | Shards of ice falling out of the sky |
| Wind | Wind Volley | Cyclone | 4 round a spot 4 ahead | 6 | 4 | A ring of wind spreading from the middle of the area |
| Lightning | Lightning Volley | Lightning Storm | 3 round a spot 4 ahead | 8 | 5 | Bolts from the sky, there and gone |
| Earth | Earth Bolt | Earthquake | 3 round a spot 4 ahead | 8 | 5 | Spikes of rock thrust up from below |
| Divine | Divine Bolt | Divine Nova | 3.5 round the caster | 8 | 5 | A ring of light spreading from the middle |
| Void | Void Volley | Void Corrosion | 3 round a spot 4 ahead | 7 | 4 | Dark swellings that linger |

Which staff throws a bolt and which a volley are Everdawn's (`fire-bolt`, `earth-bolt` and `divine-bolt`; a volley for the other five), and so are six of the held spells: its Inferno, Blizzard (`ice-blizzard`), Lightning Storm (`lightning-storm`), Earthquake, Divine Nova and Void Corrosion. **The Geyser and the Cyclone are this game's own**: Everdawn has no signature spell for water or for wind. Water and ice are resisted alike, and wind and lightning ([damage-types.md](damage-types.md)), so with no effects yet the two of a pair differ in their held spell's numbers and looks alone: the natural element's is the cheaper, and the Cyclone the widest and weakest.

## Bolt and volley (primary)

- **A bolt** is one ball: 30 damage, flying 16 a second for up to 12, touching anything within 0.35 of its path. **A volley** is three darts of 10, loosed 0.12 s of clip time apart, flying 22 a second for up to 12 and touching within 0.2. Thrown together they deal what a bolt does; spread over a moving target, some miss.
- Thrown with `Ranged_Magic_Shoot`; the first leaves the staff 0.3 s into the clip, where the staff is furthest forward (`swing-survey`). No mana. **One second between throws** (the skill's cooldown): without it a bolt out-dealt every blade from twelve times its reach.
- It flies level at chest height, along the aim as it was when it left, and ends at the first body it touches, at a wall, or at the end of its distance. It does not go through one body to the next.
- A cast cut short by a dash throws what it had thrown.

## The held spell (secondary)

- Held like a Spin: a cycle is one loop of the casting clip (`Ranged_Magic_Spellcasting`, 0.67 s; about 0.3 s at the Knight's attack speed), paid for in mana as it starts. Everything in the area takes the spell's damage once a cycle; what walks in mid-cycle takes it as it enters. The caster moves at half speed.
- **The area** is a circle on the ground: for all but one, round the spot 4 ahead of where the caster faces, so it is steered by aiming and by walking; for the Divine Nova, round the caster. A body touching the circle is in it.
- An area ahead does not cover the caster's own feet: what stands closer than the near edge (1.5 for Inferno, 1 for the 3-wide, 0.5 for Blizzard, none for the Cyclone, which reaches the caster's feet) is not hit. The Nova is the answer to a crowd at arm's length.
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

## Wand and book

One weapon, as the sword and shield are: the wand in the right hand, the open book in the left, both on the back and both on the ground when it is put down. It is held in the one-handed stance, wand up and book before the chest.

- **Primary and barrier are the staff's.** A wand throws the very bolt or volley the staff of its element does (the same skill, so the same numbers and the same second between throws), and raises the same barrier.
- **The secondary is thrown, not held.** One press throws one ball: 25 damage, flying 13 a second for up to 12. Where it ends it bursts, and everything within 2.5 of that spot takes the damage, the body it struck once like the rest. It ends at the first body it touches, at a wall, or at the end of its flight, and bursts at all three. 12 mana a cast, and 2.5 s before the next.
- Fire's is the **Fireball**; the others are each element's **Burst** (Water Burst, Ice Burst and so on). They differ in look alone: what an element's burst does of its own waits on what the elements do.
- **Lunge:** a jab with the wand, 16 damage, reach 1.65: the shortest of all.
- **Against the staff:** the staff's spell deals more to what stays in its area and costs mana for as long as it is held; the wand's ball lands all at once, anywhere a bolt can reach, and then has to wait.
- **It looks like** `wand` and `spellbook_open`, with a spark of the element at the wand's tip and the element's shader over both.

## What a spell grows with

A spell's damage grows with WIS (5% a point) where a blow's grows with STR (2.5% a point), so a staff's lunge goes by STR and its spells by WIS (`StatRules.Damage`). Which it is goes by the damage type: a spell is of its element's type and the poke is a blunt blow ([damage-types.md](damage-types.md)). The Knight's WIS of 5 makes a bolt of 30 hit for 38. The blacksmith improves a staff like any weapon: each level adds a tenth to the bolt (each dart of a volley), the spell and the poke.

## How it looks

- **The staff** is one model (`staff`) for all eight, with a ball of the element at its head, big enough to take in the model's own green gem. On the back it is carried like any pole.
- **The elements** take Everdawn's colours (`ElementPalettes`), its surface shaders (`elemental_<element>.gdshader`, the brightest layer of each), its ball for a bolt and its bipyramid for a dart, and its honeycomb shader for the barrier (`barrier_hex`). Wind, divine and ice use the second of Everdawn's two colours first, pale blue, gold and a full blue: its firsts for the three are all near white. Ice and lightning brought their own shaders from Everdawn (`elemental_ice`, `elemental_lightning`); lightning's flickers, so a lightning enchantment flashes over a blade and is gone for half a second. All of it is in `Character/ElementLooks`.
- **Casting** uses the two-handed stance between spells, `Ranged_Magic_Shoot` to throw and `Ranged_Magic_Spellcasting` to hold. The barrier plays the same `Melee_Blocking` every guard does.
- `./dev.sh magic-lineup [fire,void,...]` shows the staffs casting side by side (with none named, all eight in two pictures of four), each with its area spell running and what it throws beside it; `./dev.sh magic-lineup barriers` shows each inside its barrier. `./dev.sh weapon-lineup fire-staff` shows the poses.

## Where to get one

The **weaponsmith** sells the fire staff and the fire wand, plain, for 50 gold each like any weapon. The **enchanter**, the fourth seller, on a rug by a shrine of candles at the front of the town's courtyard, makes a staff in the player's hand the staff of any other element, or a wand the wand, for 30 gold: other spells, the same level ([trade.md](trade.md)). So every staff and wand is had by buying the fire one and having it attuned, and a player can change its mind.

## Enchanted weapons

The enchanter lays an element on a weapon of steel or wood, for 60 gold and an orb:

- **40% of every blow's damage becomes that element's magic** (`Weapons.EnchantedShare`): the swing, the Spin and the lunge alike. The magic part grows with WIS and the rest with STR, where a plain weapon's whole blow grows with STR. In the Knight's hands, with STR 12 and WIS 5, that changes almost nothing: a Slice of 26 is still 26.
- **Its element's effect, in its share.** The magic part of a blow builds the element's bar on what it hits, with 40% of the skill's buildup, and the weapon's own type builds its bar with the rest: an ice-enchanted greatsword chills a little and bleeds a little less ([damage-types.md](damage-types.md), "Statuses"). Swinging a divine or void enchantment also builds on the wielder, 8 a swing.
- The weapon keeps its level, and the blacksmith goes on improving it. A weapon has one enchantment: another element takes the place of the first, at the same price.
- It is the same weapon in every other way: the same skills, reach, guard, clips and stance. Its id and name carry the element: `greatsword~fire+3`, "Greatsword of Fire +3".
- **It shows:** the element's shader plays over the whole weapon (and the shield of a sword and shield), in the hand, on the back and on the ground, thinner than over a thing made of the element so the weapon is still seen under it (`ElementLooks.Enchantment`).
- The merchant pays half the gold put into it, the enchantment's 60 included; the orb is not paid for.
- A staff is not enchanted: it is of its element already.

## Who decides

- **A ball that bursts** is a bolt until it ends. The caster's machine decides what the burst catches (`PlayerCharacter.BlastAt`), where its own copy ended; every machine draws the burst where its copy did.
- **Bolts** (`Main/Bolts`): the caster's machine looses each one (`LooseBolt`, to every machine) and decides what it hits, as it does for its swings; every machine flies its own copy along the same line and ends it at a wall on its own. A hit is reported to the host by skill id like any other, and `EndBolt` ends the copies. The host works out the damage from the weapon it sees the caster hold.
- **The held spell**: the caster's machine tests its area each cycle and reports the hits, as for a Spin; every machine draws the ring and the strikes from the skill it was told started and where it sees the caster aim.
- **The barrier**: raising it is the player's machine's, like any guard. What is left of it is the host's (`PlayerVitals.Barrier`), which takes blows off it and brings it back, and reaches every machine with the player's HP.

## Verified by `./dev.sh magic-test`

A host and five bot clients, about 40 s against tripled waves: four with the fire, water, earth and ice staffs in hand and wind, void, divine and lightning on their backs, one with the fire wand and the lightning wand, and one with a bow enchanted with ice in hand and a greatsword enchanted with wind on its back. The bow's arrows fly as bolts do, so they are checked here: the host counts their hits apart from the bolts' (`arrow_hits`). Staff bots keep a hostile at their spell's distance, throw at it from as far as a bolt flies (leading a moving one), hold their spell on it while they can pay, and throw for half of every five seconds whatever else is due. On each machine: its player throws bolts, as many to a cast as the skill has (one, or three), some land, the others' bolts are seen, each ends in a burst and none outlives its flight; its spell is drawn in rounds of strikes and the others' are seen; barriers show here and on the others, and its own takes blows and is seen coming back; the wand's ball bursts, and a burst catches a body, and the others see it burst; the thrust of a staff or a wand reaches as far as its lunge's range; and every weapon of an element shows it on every machine, a staff at its head and an enchanted weapon all over, whatever has flashed over it since, while no plain weapon does. On the host: every staff's and the wand's bolts landed hits, every staff's spell and the wand's bursts, the enchanted weapons' blows arrived as part magic, and every caster's barrier was dealt a blow as it went up (`--barrier-drill`: 6, through the same path as a skeleton's) and took it, at no cost in HP. Part of `./dev.sh smoke`.

The rules themselves are `Core.Tests/Combat/StaffsTests` (a staff per element, bolt or volley by element, a volley dealing what a bolt does, areas ahead and round the caster, the barrier's pool and its return, WIS and STR, improved staffs), `Core.Tests/Combat/WandsTests` (a wand per element, the staff's shot and barrier, a ball thrown for mana that bursts, what a burst catches, wands attuned to wands), `Core.Tests/Combat/EnchantmentsTests` (any element on any weapon of steel or wood, the magic part by WIS and the rest by STR, level and enchantment kept through each other, ids and names, staffs attuned) and `Core.Tests/Trade`.

## Not here yet

- An enchantment's worth: it moves 40% of a blow, damage and buildup alike, to its element ([damage-types.md](damage-types.md), "Statuses"), so the blade also burns, chills or corrupts a little. Since skeletons are weak to every physical type, it still deals no more to them, and less for six of the eight.
- Arcane, Everdawn's third variant, and its Chain Lightning.
- PvP with staffs runs through the same code as swings (bolts and areas hit players, barriers take the blows) but no self-test covers it.
- All eight staffs are one model, and all eight wands one wand and one book. Whether each should have its own.
- A cast or a casting stance of its own for the barrier.
- Balance: bolt damage and its second between throws, the areas' sizes, the barrier's 40. Skeletons do not threaten a caster that keeps its distance.
