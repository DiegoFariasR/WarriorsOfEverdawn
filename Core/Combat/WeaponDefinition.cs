using System;
using System.Collections.Generic;
using System.Linq;

namespace WarriorsOfEverdawn.Core.Combat;

// What a player holds: one skill on the primary button, one on the secondary, the thrust a dash carries when the
// primary is thrown with it, and its guard on the guard button.
public sealed record WeaponDefinition(string Id, string Name, SkillDefinition Primary, SkillDefinition Secondary, SkillDefinition Lunge, GuardDefinition Guard)
{
    public const int PrimaryButton = 0;
    public const int SecondaryButton = 1;

    // How far a weapon can be improved.
    public const int MaxLevel = 10;

    // 0 for the plain make. Each level above it adds to the damage of every skill (Weapons.AtLevel), and shows in the
    // id and the name: "greatsword+3", "Greatsword +3".
    public int Level { get; init; }

    // The id of the plain weapon this is a make of; its own, for a plain one. How it looks and is held go by this.
    public string Kind { get; init; } = Id;

    // The magic a staff is made for; none for a weapon of steel or wood.
    public Element? Element { get; init; }

    // The element a weapon of steel or wood has been enchanted with, if any: part of every blow's damage is that
    // magic (Weapons.EnchantedShare). It shows in the id and the name: "greatsword~fire", "Greatsword of Fire".
    public Element? Enchantment { get; init; }

    public IEnumerable<SkillDefinition> Skills => new[] { Primary, Secondary, Lunge };

    public SkillDefinition Skill(int button) => button switch
    {
        PrimaryButton => Primary,
        SecondaryButton => Secondary,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, $"{Name} has skills on buttons 0 and 1 only"),
    };
}

public static class Weapons
{
    // Each level adds this share of a plain skill's damage, and never less than 1, so every level hits harder than
    // the one before whatever the skill.
    public const float DamagePerLevel = 0.1f;

    // An enchanted weapon deals this share of its damage as its element's magic. First pass.
    public const float EnchantedShare = 0.4f;

    // Before a weapon's level in ids and names, and before its enchantment in ids: "greatsword~fire+3".
    private const char LevelMark = '+';
    private const char EnchantMark = '~';

    // Guards, first pass: the greatsword stops everything in front and slows most; the staff covers the widest arc and
    // parries most easily; the spear guards a narrow front; the scythe lets the most through and parries hardest but
    // moves best.
    public static readonly WeaponDefinition Greatsword = new("greatsword", "Greatsword", Skills.Slice, Skills.Spin, Skills.GreatswordLunge,
        new GuardDefinition(HalfArc: 75f * Angles.DegToRad, DamageTaken: 0f, MoveSpeedFactor: 0.4f, ParryWindow: 0.2f));

    public static readonly WeaponDefinition Quarterstaff = new("quarterstaff", "Quarterstaff", Skills.StaffHit, Skills.StaffSpin, Skills.StaffLunge,
        new GuardDefinition(HalfArc: 100f * Angles.DegToRad, DamageTaken: 0.2f, MoveSpeedFactor: 0.5f, ParryWindow: 0.25f));

    public static readonly WeaponDefinition Spear = new("spear", "Spear", Skills.SpearThrust, Skills.SpearSpin, Skills.SpearLunge,
        new GuardDefinition(HalfArc: 60f * Angles.DegToRad, DamageTaken: 0.25f, MoveSpeedFactor: 0.5f, ParryWindow: 0.2f));

    public static readonly WeaponDefinition Scythe = new("scythe", "Scythe", Skills.ScytheSwing, Skills.ScytheSpin, Skills.ScytheLunge,
        new GuardDefinition(HalfArc: 70f * Angles.DegToRad, DamageTaken: 0.4f, MoveSpeedFactor: 0.6f, ParryWindow: 0.15f));

    // A sword in one hand and a shield in the other, carried, bought, improved and sold as the one weapon it is here.
    // The shield stops everything across the widest front, slows least and parries most easily; the sword pays for
    // it in damage and reach.
    public static readonly WeaponDefinition SwordAndShield = new("sword-and-shield", "Sword and shield", Skills.SwordSlash, Skills.SwordSpin, Skills.SwordLunge,
        new GuardDefinition(HalfArc: 110f * Angles.DegToRad, DamageTaken: 0f, MoveSpeedFactor: 0.7f, ParryWindow: 0.3f));

    // A hammer for both hands: slow, heavy blows that stun. Its guard stops nearly everything and all but roots
    // its wielder.
    public static readonly WeaponDefinition Warhammer = new("warhammer", "Warhammer", Skills.HammerSmash, Skills.HammerSpin, Skills.HammerLunge,
        new GuardDefinition(HalfArc: 70f * Angles.DegToRad, DamageTaken: 0.1f, MoveSpeedFactor: 0.35f, ParryWindow: 0.15f));

    // A claw on each hand, one weapon as a sword and shield are. Light, quick blows at arm's length; a guard of
    // crossed claws that lets a third through and is quick to parry.
    public static readonly WeaponDefinition Claws = new("claws", "Claws", Skills.ClawRake, Skills.ClawSpin, Skills.ClawLunge,
        new GuardDefinition(HalfArc: 65f * Angles.DegToRad, DamageTaken: 0.35f, MoveSpeedFactor: 0.65f, ParryWindow: 0.25f));

    // A bow and the arrows for it, which never run out. It deals from further off than anything of steel and less
    // than any of it, and it is the poorest guard there is: a stave of wood held across the body.
    public static readonly WeaponDefinition Bow = new("bow", "Bow", Skills.BowShot, Skills.BowVolley, Skills.BowLunge,
        new GuardDefinition(HalfArc: 50f * Angles.DegToRad, DamageTaken: 0.5f, MoveSpeedFactor: 0.6f, ParryWindow: 0.15f));

    // Every staff's guard is the same barrier, first pass: a shell all round that takes 40 before it gives and comes
    // back at 8 a second once it has been down for 2 s. Nothing gets past it while it holds, and it parries nothing.
    public static readonly GuardDefinition Barrier = new(HalfArc: MathF.PI, DamageTaken: 0f, MoveSpeedFactor: 0.5f, ParryWindow: 0f)
    {
        Name = "Barrier",
        Barrier = new BarrierDefinition(Strength: 40, RechargePerSecond: 8f, RechargeDelay: 2f),
    };

    private static readonly Dictionary<(string Kind, Element? Enchantment, int Level), WeaponDefinition> Made = new();

    // Weapons of steel and wood: what the weaponsmith sells.
    public static IReadOnlyList<WeaponDefinition> Arms { get; } = new[] { Greatsword, Quarterstaff, Spear, Scythe, SwordAndShield, Bow, Claws, Warhammer };

    // A magic staff for each element: a bolt or a volley, a ball that bursts, and the barrier.
    public static IReadOnlyList<WeaponDefinition> Staffs { get; } = Elements.All.Select(StaffFor).ToList();

    // A wand and book for each element, one weapon as a sword and shield are: the staff's bolt or volley and its
    // barrier, and in place of the staff's ball a spell held on an area.
    public static IReadOnlyList<WeaponDefinition> Wands { get; } = Elements.All.Select(WandFor).ToList();

    // The plain makes. Also the order the weapon key cycles through.
    public static IReadOnlyList<WeaponDefinition> All { get; } = Arms.Concat(Staffs).Concat(Wands).ToList();

    public static WeaponDefinition Default => Greatsword;

    public static WeaponDefinition Staff(Element element) => Staffs[(int)element];

    public static WeaponDefinition Wand(Element element) => Wands[(int)element];

    public static bool IsWand(WeaponDefinition weapon) => Wands.Contains(Plain(weapon));

    private static WeaponDefinition WandFor(Element element)
    {
        var skills = Skills.WandFor(element);
        return new WeaponDefinition($"{Elements.IdOf(element)}-wand", $"{element} wand", skills.Primary, skills.Channel, skills.Lunge, Barrier)
        {
            Element = element,
        };
    }

    private static WeaponDefinition StaffFor(Element element)
    {
        var skills = Skills.StaffOf(element);
        return new WeaponDefinition($"{Elements.IdOf(element)}-staff", $"{element} staff", skills.Primary, skills.Burst, skills.Lunge, Barrier)
        {
            Element = element,
        };
    }

    // A weapon by its id, enchanted and improved ones included: "spear", "spear+4", "spear~void", "spear~void+4".
    public static WeaponDefinition ById(string id)
    {
        int levelAt = id.IndexOf(LevelMark);
        string made = levelAt < 0 ? id : id[..levelAt];
        int enchantAt = made.IndexOf(EnchantMark);
        string kind = enchantAt < 0 ? made : made[..enchantAt];
        var plain = All.FirstOrDefault(w => w.Id == kind) ?? throw new KeyNotFoundException($"Unknown weapon '{id}'");

        Element? enchantment = null;
        if (enchantAt >= 0)
        {
            string named = made[(enchantAt + 1)..];
            enchantment = plain.Element == null && Elements.All.Where(e => Elements.IdOf(e) == named).Select(e => (Element?)e).FirstOrDefault() is { } element
                ? element
                : throw new KeyNotFoundException($"Unknown weapon '{id}': '{named}' is no element, or the {plain.Name} takes no enchantment");
        }

        int level = 0;
        if (levelAt >= 0 && !(int.TryParse(id[(levelAt + 1)..], out level) && level is >= 1 and <= WeaponDefinition.MaxLevel))
        {
            throw new KeyNotFoundException($"Unknown weapon '{id}': a level is from 1 to {WeaponDefinition.MaxLevel}");
        }

        return Make(plain, enchantment, level);
    }

    // The plain weapon an enchanted or improved one is a make of; a plain one is its own.
    public static WeaponDefinition Plain(WeaponDefinition weapon) => ById(weapon.Kind);

    // The weapon at this level, whatever level it is at now, its enchantment kept: every skill hits harder.
    public static WeaponDefinition AtLevel(WeaponDefinition weapon, int level) => Make(Plain(weapon), weapon.Enchantment, level);

    // The weapon enchanted with this element in place of any it had, its level kept. A staff is not enchanted: it
    // is of its element already (Attuned).
    public static WeaponDefinition Enchanted(WeaponDefinition weapon, Element element) => Make(Plain(weapon), element, weapon.Level);

    // The staff of this element in place of the staff of another, or the wand in place of the wand, its level kept.
    public static WeaponDefinition Attuned(WeaponDefinition magic, Element element) =>
        magic.Element != null
            ? AtLevel(IsWand(magic) ? Wand(element) : Staff(element), magic.Level)
            : throw new ArgumentException($"The {magic.Name} is of no element: it is enchanted, not attuned", nameof(magic));

    private static WeaponDefinition Make(WeaponDefinition plain, Element? enchantment, int level)
    {
        if (level is < 0 or > WeaponDefinition.MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"A weapon's level is from 0 to {WeaponDefinition.MaxLevel}");
        }

        if (enchantment != null && plain.Element != null)
        {
            throw new ArgumentException($"The {plain.Name} is of its own element and takes no enchantment", nameof(enchantment));
        }

        if (level == 0 && enchantment == null)
        {
            return plain;
        }

        lock (Made)
        {
            if (!Made.TryGetValue((plain.Id, enchantment, level), out var made))
            {
                string enchantedId = enchantment is { } id ? $"{EnchantMark}{Elements.IdOf(id)}" : "";
                string enchantedName = enchantment is { } name ? $" of {name}" : "";
                made = plain with
                {
                    Id = $"{plain.Id}{enchantedId}{(level > 0 ? $"{LevelMark}{level}" : "")}",
                    Name = $"{plain.Name}{enchantedName}{(level > 0 ? $" {LevelMark}{level}" : "")}",
                    Level = level,
                    Enchantment = enchantment,
                    Primary = Shaped(plain.Primary, enchantment, level),
                    Secondary = Shaped(plain.Secondary, enchantment, level),
                    Lunge = Shaped(plain.Lunge, enchantment, level),
                };
                Made[(plain.Id, enchantment, level)] = made;
            }

            return made;
        }
    }

    // What a skill dealing `plain` deals on a weapon of this level.
    public static int DamageAtLevel(int plain, int level) =>
        plain <= 0 ? plain : plain + level * Math.Max(1, (int)MathF.Round(plain * DamagePerLevel));

    // The plain skill of that id. Swings and hits travel between machines as skill ids; whoever needs what the skill
    // does on the weapon a player is holding asks that player's WeaponSets.
    public static SkillDefinition SkillById(string id) =>
        All.SelectMany(w => w.Skills).FirstOrDefault(s => s.Id == id)
        ?? throw new KeyNotFoundException($"No weapon has a skill '{id}'");

    // The plain weapon after this one's kind, in the order the weapon key cycles through.
    public static WeaponDefinition Next(WeaponDefinition weapon)
    {
        int index = All.ToList().IndexOf(Plain(weapon));
        return All[(index + 1) % All.Count];
    }

    // A plain skill as a weapon of this level and enchantment has it.
    private static SkillDefinition Shaped(SkillDefinition skill, Element? enchantment, int level)
    {
        var harder = skill with { Damage = DamageAtLevel(skill.Damage, level) };
        return enchantment is { } element ? harder with { Element = element, MagicShare = EnchantedShare } : harder;
    }
}
