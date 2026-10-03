# Magic: staffs, wands and enchantments

Two magic weapons, each in eight elements, all Everdawn's: its four (fire, water, wind, earth), the weaponised forms of two of them (ice of water, lightning of wind), and its two astral types (divine, void). A **staff** throws a bolt or a volley, throws a ball that bursts and raises a barrier. A **wand and book** throw the same bolt or volley and raise the same barrier, and in place of the ball hold a spell on an area. Either is a weapon like any other ([combat.md](combat.md)): it takes one slot, is bought, dropped, picked up, improved and sold as the others are, and has the same four things (primary, secondary, lunge, guard). All numbers are first-pass and live in `Core/Combat` (`Skills.StaffOf`, `Skills.WandFor`, `Projectiles`, `Weapons.Barrier`). The sections down to the lunge describe the staff; the wand's differences follow them.

## The eight

| Element | Bolt or volley (staff and wand) | Held spell (wand) | Area | Damage a cycle | Mana a cycle | Looks like |
|---|---|---|---|---|---|---|
| Fire | Fire Bolt | Inferno | 2.5 round a spot 4 ahead | 10 | 6 | Pillars of fire standing up out of the ground |
| Water | Water Volley | Geyser | 3 round a spot 4 ahead | 6 | 4 | Columns of water standing up out of the ground |
| Ice | Ice Volley | Blizzard | 3.5 round a spot 4 ahead | 6 | 5 | Shards of ice falling out of the sky |
| Wind | Wind Volley | Cyclone | 4 round a spot 4 ahead | 6 | 4 | A ring of wind spreading from the middle of the area |
| Lightning | Lightning Volley | Lightning Storm | 3 round a spot 4 ahead | 8 | 5 | Bolts from the sky, there and gone |
| Earth | Earth Bolt | Earthquake | 3 round a spot 4 ahead | 8 | 5 | Spikes of rock thrust up from below |
| Divine | Divine Bolt | Divine Nova | 3.5 round the caster | 8 | 5 | A ring of light spreading from the middle |
| Void | Void Volley | Void Corrosion | 3 round a spot 4 ahead | 7 | 4 | Dark swellings that linger |

Which element throws a bolt and which a volley are Everdawn's (`fire-bolt`, `earth-bolt` and `divine-bolt`; a volley for the other five), and so are six of the held spells: its Inferno, Blizzard (`ice-blizzard`), Lightning Storm (`lightning-storm`), Earthquake, Divine Nova and Void Corrosion. **The Geyser and the Cyclone are this game's own**: Everdawn has no signature spell for water or for wind. Water and ice are resisted alike, and wind and lightning ([damage-types.md](damage-types.md)), so with no effects yet the two of a pair differ in their held spell's numbers and looks alone: the natural element's is the cheaper, and the Cyclone the widest and weakest.

## Bolt and volley (primary, staff and wand)

- **A bolt** is one ball: 30 damage, flying 17.5 a second for up to 12, touching anything within 0.35 of its path. **A volley** is three darts of 10, loosed 0.12 s of clip time apart, flying 24 a second for up to 12 and touching within 0.2. Thrown together they deal what a bolt does; spread over a moving target, some miss.
- Thrown with `Ranged_Magic_Shoot`; the first leaves the staff 0.3 s into the clip, where the staff is furthest forward (`swing-survey`). No mana. **One second between throws** (the skill's cooldown): without it a bolt out-dealt every blade from twelve times its reach.
- It flies level at chest height, along the aim as it was when it left, and ends at the first body it touches, at a wall, or at the end of its distance. It does not go through one body to the next.
- **Walls stop it**, and so does anything else of the level that is solid (`Util/Walls`): every machine looks along each step of the flight for the world and ends the bolt there. The first step is looked at from the caster's chest, not from the hand 0.6 in front of it: a caster standing against a wall has its hand inside the wall, and from there a bolt used to fly straight through.
- A cast cut short by a dash throws what it had thrown.

## The ball that bursts (the staff's secondary)

- **Thrown, not held.** One press throws one ball: 25 damage, flying 14.5 a second for up to 12. Where it ends it bursts, and everything within 2.5 of that spot takes the damage, the body it struck once like the rest. It ends at the first body it touches, at a wall, or at the end of its flight, and bursts at all three. 12 mana a cast, and 2.5 s before the next. Thrown with the bolt's clip.
- **A wall shelters what stands behind it** from the burst: a ball that ends at a wall bursts on its own side of it, and catches only what the spot it burst at can see.
- Fire's is the **Fireball**; the others are each element's **Burst** (Water Burst, Ice Burst and so on). The Fireball leaves burning ground where it bursts and the Ice Burst ice ("Burning ground and ice", below); the others differ in look alone.

## The held spell (the wand's secondary)

- Held like a Spin: a cycle is one loop of the casting clip (`Ranged_Magic_Spellcasting`, 0.67 s; about 0.3 s at the Knight's attack speed), paid for in mana as it starts. Everything in the area takes the spell's damage once a cycle; what walks in mid-cycle takes it as it enters. The caster moves at half speed.
- **It lands on what the caster can see.** A body in the area with a wall between it and the caster takes nothing (`Walls.Between`, read 1.6 over the feet: above the barrels and benches a spell is cast across, below the top of every wall). The ring and the strikes are still drawn over the whole area.
- **The area** is a circle on the ground: for all but one, round the spot 4 ahead of where the caster faces, so it is steered by aiming and by walking; for the Divine Nova, round the caster. A body touching the circle is in it.
- An area ahead does not cover the caster's own feet: what stands closer than the near edge (1.5 for Inferno, 1 for the 3-wide, 0.5 for Blizzard, none for the Cyclone, which reaches the caster's feet) is not hit. The Nova is the answer to a crowd at arm's length.
- A ring on the ground shows the area for as long as the spell is held, and a round of strikes falls inside it each cycle.
- The Inferno leaves the ground under its area burning, and the Blizzard leaves ice, each cycle ("Burning ground and ice", below).

## Burning ground and ice

Fire and ice leave their ground where they land, first pass (`Core/Combat/Surfaces`, `Main/GroundSurfaces`): the Fireball and the Ice Burst a patch 2 wide (a little narrower than what the burst catches) where the ball bursts, and the Inferno and the Blizzard a patch as wide as their area under it, each cycle they are held. The other six elements leave nothing yet.

| | Lasts | Every second, on what stands on it | And |
|---|---|---|---|
| **Burning ground** | 4 s | A hit of 6 fire, by the caster's WIS (8 in the Knight's hands), building its burn as fire does | |
| **Ice** | 6 s | 30 cold: more than a second takes off, so what stays is chilled in two and frozen in about seven | It slides: its speed changes by no more than 4 a second, so it goes on past where it meant to stop and is slow to turn about |

- **What it acts on** is whatever stands on it: skeletons and players alike, the caster and its friends too, in co-op as in PvP. Magic is dangerous. A body touching a patch is on it for the hit and the cold; its middle must be on the ice for it to slide. A hit from burning ground never staggers, or a skeleton in the flames would be held in its stagger.
- **On a player** burning ground bites past any guard or barrier, as a burn does, the armour taking its share, and builds its burn; ice chills it and in the end freezes it. Nothing hurts or chills a player on the town's safe ground; ice is slippery there as everywhere. A player slides on its own machine, which hears where every patch is; the host burns and chills it where it last reported standing.
- **The level's own fire:** the town's camp fire (its layout's `campfire` marker) is burning ground 0.6 wide that never goes out, laid alike on every machine. It burns whoever walks into it, even on the town's safe ground, at the hit's own strength (no one's WIS), and nothing puts it out: ice laid over it lies beside it.
- **Fire and ice undo each other:** a patch laid over one of the other kind destroys it, whoever laid either, and stays: fire melts ice, ice puts fire out. Touching is enough. A Blizzard held over burning ground puts it out cycle by cycle and leaves its ice; a Fireball thrown onto ice melts it.
- **A held spell renews its patch** rather than laying another when its area has moved by less than half the patch's radius, and a caster keeps eight patches at most, the next taking the place of the one nearest its end: a held spell walked across the courtyard leaves a trail of fire or ice that ends, not a field of it.
- **How it looks** (`SurfacePatchView`): burning ground is the ground scorched brown, glowing orange from within, with tongues of fire flickering up out of it, embers rising and an orange light thrown about; ice is a pale blue, glossy sheet with the ice shader's frost over it and shards standing up out of it. Each spreads over a quarter second as it is laid and fades over the last 0.6 s.
- **Who decides:** the caster's machine says where its spell left its ground, as it decides what the spell hit; the host keeps the patches, tells every machine to draw them, acts on the skeletons and players on them once a second, and slides the skeletons it moves; each player's own machine slides it. A machine that joins later does not see the patches of the moment.
- `./dev.sh floors-test` lays the player's own fire at its feet out in the field and its own ice a little further out: the fire burns it, on the ice it sets off slowly and is chilled, and its own fire laid over that ice melts it; and before all that, in the town, walking into the camp fire burns it.

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
- **The secondary is held, not thrown:** its element's spell on an area, as above.
- **Lunge:** a jab with the wand, 16 damage, reach 1.65: the shortest of all.
- **Against the staff:** the wand's spell deals more to what stays in its area and costs mana for as long as it is held; the staff's ball lands all at once, anywhere a bolt can reach, and then has to wait.
- **It looks like** `wand` and `spellbook_open`, with a spark of the element at the wand's tip and the element's shader over both.

## What a spell grows with

A spell's damage grows with WIS (5% a point) where a blow's grows with STR (2.5% a point), so a staff's lunge goes by STR and its spells by WIS (`StatRules.Damage`). Which it is goes by the damage type: a spell is of its element's type and the poke is a blunt blow ([damage-types.md](damage-types.md)). The Knight's WIS of 5 makes a bolt of 30 hit for 38. The blacksmith improves a staff or a wand like any weapon: each level adds a tenth to the bolt (each dart of a volley), to the ball or the held spell, and to the poke.

## How it looks

- **The staff** is one model (`staff`) for all eight, with a ball of the element at its head, big enough to take in the model's own green gem. On the back it is carried like any pole.
- **The elements and their effects are Everdawn's**, from the character kit both games share (`EverdawnKit.Magic`, at `GodotClient/kit`): each element's two colours and its surface shader in layers (`MagicLooks`, the shaders at `res://kit/shaders/magic/`), the honeycomb of the barrier (`barrier_hex`), and the effects a spell is drawn with. Wind, divine and ice lead with the second of the two colours, pale blue, gold and a full blue: the firsts of the three are all near white, and here they have to be told apart at a glance. Lightning's shader flickers, so a lightning enchantment flashes over a blade and is gone for half a second. Which kit element each of ours is, the colour it leads with, how strongly it shows on an enchanted weapon and the shape of its area spell's strikes are in `Character/ElementLooks`.
- **A spell is drawn as Everdawn draws one:** see-through in its colour and lit from within, with every layer of its element's shader over it (`ElementLooks.Made`). A bolt is a ball and a dart a six-sided bipyramid, point first, each leaving a trail of fading spheres behind it (`SpellTrail`). Where one ends it lands with an impact (`SpellImpact`): a bolt's explosive puff, a dart's shatter, and for a ball that bursts an explosion as wide as what it catches.
- **An area spell's strikes land as the element's area spell does in Everdawn:** an earthquake's spikes and a blizzard's shards shatter, a nova's or cyclone's ring pulses, corrosion lingers on the ground, an inferno's or geyser's pillars stand as they are, and a lightning storm's are jagged bolts from the sky that land in an electric arc-flash and sparks (`LightningFlash`).
- **Magic circles**, Everdawn's turning rings of white: one stands in front of a caster, facing the way it throws, from the start of a throw until a moment after its last bolt leaves; one lies under a caster for as long as it holds a spell on an area (`Character/CastingCircles`). Arrows, enchanted or not, have none.
- **Casting** throws with `Ranged_Magic_Shoot` and holds with `Ranged_Magic_Spellcasting`, from the stance of what is in hand between spells: a staff's two-handed, a wand's one-handed, its book held out as it holds a spell. The barrier plays the guard of what is held ([combat.md](combat.md), "Guard"): a staff is raised across the body, a wand's forearm raised.
- `./dev.sh magic-lineup [fire,void,...]` shows the wands casting side by side (with none named, all eight in two pictures of four), each with its area spell running and what it throws beside it; `./dev.sh magic-lineup barriers` shows each staff inside its barrier. `./dev.sh weapon-lineup fire-staff` and `fire-wand` show the poses.

## Where to get one

The **weaponsmith** sells the fire staff and the fire wand, plain, for 50 gold each like any weapon. The **enchanter**, in its own room off the back of the town's courtyard, makes a staff in the player's hand the staff of any other element, or a wand the wand, for 30 gold: other spells, the same level ([trade.md](trade.md)). So every staff and wand is had by buying the fire one and having it attuned, and a player can change its mind.

## Enchanted weapons

The enchanter lays an element on a weapon of steel or wood, for 60 gold and an orb:

- **40% of every blow's damage becomes that element's magic** (`Weapons.EnchantedShare`): the swing, the Spin and the lunge alike. The magic part grows with WIS and the rest with STR, where a plain weapon's whole blow grows with STR. In the Knight's hands, with STR 12 and WIS 5, that changes almost nothing: a Slice of 26 is still 26.
- **Its element's effect, in its share.** The magic part of a blow builds the element's bar on what it hits, with 40% of the skill's buildup, and the weapon's own type builds its bar with the rest: an ice-enchanted greatsword chills a little and bleeds a little less ([damage-types.md](damage-types.md), "Statuses"). Swinging a divine or void enchantment also builds on the wielder, 8 a swing.
- The weapon keeps its level, and the blacksmith goes on improving it. A weapon has one enchantment: another element takes the place of the first, at the same price.
- It is the same weapon in every other way: the same skills, reach, guard, clips and stance. Its id and name carry the element: `greatsword~fire+3`, "Greatsword of Fire +3".
- **It shows:** the element's shader plays over the whole weapon (and the shield of a sword and shield), in the hand, on the back and on the ground, thinner than over a thing made of the element so the weapon is still seen under it (`ElementLooks.Enchantment`). Its swings leave a trail of the element too: in the colour the element leads with, with the same shader over it (`WeaponTrail.Element`), where a plain weapon's trail is warm white.
- The merchant pays half the gold put into it, the enchantment's 60 included; the orb is not paid for.
- A staff is not enchanted: it is of its element already.

## Who decides

- **Burning ground and ice**: the caster's machine says where; the host keeps them and acts on what stands on them ("Burning ground and ice", above).
- **A ball that bursts** is a bolt until it ends. The caster's machine decides what the burst catches (`PlayerCharacter.BlastAt`), where its own copy ended; every machine draws the burst where its copy did.
- **Bolts** (`Main/Bolts`): the caster's machine looses each one (`LooseBolt`, to every machine) and decides what it hits, as it does for its swings; every machine flies its own copy along the same line and ends it at a wall on its own. A hit is reported to the host by skill id like any other, and `EndBolt` ends the copies. The host works out the damage from the weapon it sees the caster hold.
- **The held spell**: the caster's machine tests its area each cycle and reports the hits, as for a Spin; every machine draws the ring and the strikes from the skill it was told started and where it sees the caster aim.
- **The barrier**: raising it is the player's machine's, like any guard. What is left of it is the host's (`PlayerVitals.Barrier`), which takes blows off it and brings it back, and reaches every machine with the player's HP.

## Verified by `./dev.sh magic-test`

A host and five bot clients, about 40 s against tripled waves: four with the fire, water, earth and ice wands in hand and wind, void, divine and lightning on their backs, one with the fire staff and the lightning staff, and one with a bow enchanted with ice in hand and a greatsword enchanted with wind on its back. The bow's arrows fly as bolts do, so they are checked here: the host counts their hits apart from the bolts' (`arrow_hits`). Wand bots keep a hostile at their spell's distance, throw at it from as far as a bolt flies (leading a moving one), hold their spell on it while they can pay, and throw for half of every five seconds whatever else is due. On each machine: its player throws bolts, as many to a cast as the skill has (one, or three), some land, the others' bolts are seen, each ends in a burst and none outlives its flight; its spell is drawn in rounds of strikes and the others' are seen; barriers show here and on the others, and its own takes blows and is seen coming back; burning ground and ice are seen laid, never more patches at once than the casters may keep; the staff's ball bursts, and a burst catches a body, and the others see it burst; the thrust of a staff or a wand reaches as far as its lunge's range; and every weapon of an element shows it on every machine, a staff at its head and an enchanted weapon all over, whatever has flashed over it since, while no plain weapon does. On the host: every wand's and the staff's bolts landed hits, every wand's spell and the staff's bursts, the enchanted weapons' blows arrived as part magic, turns on burning ground burned skeletons and turns on ice chilled them and stood them on it, and every caster's barrier was dealt a blow as it went up (`--barrier-drill`: 6, through the same path as a skeleton's) and took it, at no cost in HP. Part of `./dev.sh smoke`.

The rules themselves are `Core.Tests/Combat/StaffsTests` (a staff per element, bolt or volley by element, a volley dealing what a bolt does, a ball thrown for mana that bursts, what a burst catches, the barrier's pool and its return, WIS and STR, improved staffs), `Core.Tests/Combat/WandsTests` (a wand per element, the staff's shot and barrier, a spell held on an area, areas ahead and round the caster, wands attuned to wands, improved wands), `Core.Tests/Combat/EnchantmentsTests` (any element on any weapon of steel or wood, the magic part by WIS and the rest by STR, level and enchantment kept through each other, ids and names, staffs attuned), `Core.Tests/Combat/SurfacesTests` (what fire and ice leave and where, a patch renewed or laid beside, the caster's eight, a patch's end, the slide on ice, the cold that freezes what stays) and `Core.Tests/Trade`.

## Not here yet

- An enchantment's worth: it moves 40% of a blow, damage and buildup alike, to its element ([damage-types.md](damage-types.md), "Statuses"), so the blade also burns, chills or corrupts a little. Since skeletons are weak to every physical type, it still deals no more to them, and less for six of the eight.
- Arcane, Everdawn's third variant, and its Chain Lightning.
- PvP with staffs and wands runs through the same code as swings (bolts and areas hit players, barriers take the blows) but no self-test covers it.
- All eight staffs are one model, and all eight wands one wand and one book. Whether each should have its own.
- A cast or a casting stance of its own for the barrier.
- Balance: bolt damage and its second between throws, the areas' sizes, the barrier's 40. Skeletons do not threaten a caster that keeps its distance.
