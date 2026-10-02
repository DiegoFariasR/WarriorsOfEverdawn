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

    // Between a weapon's kind and its level, in ids and names.
    private const char LevelMark = '+';

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

    // Every staff's guard is the same barrier, first pass: a shell all round that takes 40 before it gives and comes
    // back at 8 a second once it has been down for 2 s. Nothing gets past it while it holds, and it parries nothing.
    public static readonly GuardDefinition Barrier = new(HalfArc: MathF.PI, DamageTaken: 0f, MoveSpeedFactor: 0.5f, ParryWindow: 0f)
    {
        Name = "Barrier",
        Barrier = new BarrierDefinition(Strength: 40, RechargePerSecond: 8f, RechargeDelay: 2f),
    };

    private static readonly Dictionary<(string Kind, int Level), WeaponDefinition> Improved = new();

    // Weapons of steel and wood: what the weaponsmith sells.
    public static IReadOnlyList<WeaponDefinition> Arms { get; } = new[] { Greatsword, Quarterstaff, Spear, Scythe, SwordAndShield };

    // A magic staff for each element: a bolt or a volley, a spell held on an area, and the barrier.
    public static IReadOnlyList<WeaponDefinition> Staffs { get; } = Elements.All.Select(StaffFor).ToList();

    // The plain makes. Also the order the weapon key cycles through.
    public static IReadOnlyList<WeaponDefinition> All { get; } = Arms.Concat(Staffs).ToList();

    public static WeaponDefinition Default => Greatsword;

    public static WeaponDefinition Staff(Element element) => Staffs[(int)element];

    private static WeaponDefinition StaffFor(Element element)
    {
        var skills = Skills.StaffOf(element);
        return new WeaponDefinition($"{Elements.IdOf(element)}-staff", $"{element} staff", skills.Primary, skills.Channel, skills.Lunge, Barrier)
        {
            Element = element,
        };
    }

    // A weapon by its id, improved ones included: "spear", "spear+4".
    public static WeaponDefinition ById(string id)
    {
        int mark = id.IndexOf(LevelMark);
        string kind = mark < 0 ? id : id[..mark];
        var plain = All.FirstOrDefault(w => w.Id == kind) ?? throw new KeyNotFoundException($"Unknown weapon '{id}'");
        if (mark < 0)
        {
            return plain;
        }

        return int.TryParse(id[(mark + 1)..], out int level) && level is >= 1 and <= WeaponDefinition.MaxLevel
            ? AtLevel(plain, level)
            : throw new KeyNotFoundException($"Unknown weapon '{id}': a level is from 1 to {WeaponDefinition.MaxLevel}");
    }

    // The plain weapon an improved one is a make of; a plain one is its own.
    public static WeaponDefinition Plain(WeaponDefinition weapon) => ById(weapon.Kind);

    // The weapon's kind at this level, whatever level it is at now: the same weapon with every skill hitting harder.
    public static WeaponDefinition AtLevel(WeaponDefinition weapon, int level)
    {
        if (level is < 0 or > WeaponDefinition.MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"A weapon's level is from 0 to {WeaponDefinition.MaxLevel}");
        }

        var plain = Plain(weapon);
        if (level == 0)
        {
            return plain;
        }

        lock (Improved)
        {
            if (!Improved.TryGetValue((plain.Id, level), out var improved))
            {
                improved = plain with
                {
                    Id = $"{plain.Id}{LevelMark}{level}",
                    Name = $"{plain.Name} {LevelMark}{level}",
                    Level = level,
                    Primary = Harder(plain.Primary, level),
                    Secondary = Harder(plain.Secondary, level),
                    Lunge = Harder(plain.Lunge, level),
                };
                Improved[(plain.Id, level)] = improved;
            }

            return improved;
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

    private static SkillDefinition Harder(SkillDefinition skill, int level) => skill with { Damage = DamageAtLevel(skill.Damage, level) };
}
