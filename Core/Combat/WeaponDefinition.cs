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

    public SkillDefinition Skill(int button) => button switch
    {
        PrimaryButton => Primary,
        SecondaryButton => Secondary,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, $"{Name} has skills on buttons 0 and 1 only"),
    };
}

public static class Weapons
{
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

    // Also the order the weapon key cycles through.
    public static IReadOnlyList<WeaponDefinition> All { get; } = new[] { Greatsword, Quarterstaff, Spear, Scythe };

    public static WeaponDefinition Default => Greatsword;

    public static WeaponDefinition ById(string id) =>
        All.FirstOrDefault(w => w.Id == id) ?? throw new KeyNotFoundException($"Unknown weapon '{id}'");

    // Swings and hits travel between machines as skill ids, so they resolve even while a weapon change is in flight.
    public static SkillDefinition SkillById(string id) =>
        All.SelectMany(w => new[] { w.Primary, w.Secondary, w.Lunge }).FirstOrDefault(s => s.Id == id)
        ?? throw new KeyNotFoundException($"No weapon has a skill '{id}'");

    public static WeaponDefinition Next(WeaponDefinition weapon)
    {
        int index = All.ToList().IndexOf(weapon);
        return index >= 0 ? All[(index + 1) % All.Count] : throw new KeyNotFoundException($"Unknown weapon '{weapon.Id}'");
    }
}
