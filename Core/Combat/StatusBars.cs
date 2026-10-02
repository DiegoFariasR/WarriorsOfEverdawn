using System;
using System.Collections.Generic;

namespace WarriorsOfEverdawn.Core.Combat;

// What a body's bars make of it at this moment, Everdawn's statuses. Of a pair of tiers only the higher shows.
[Flags]
public enum Statuses
{
    None = 0,
    Burning = 1 << 0,
    Chilled = 1 << 1,
    Frozen = 1 << 2,
    Dizzy = 1 << 3,
    Stunned = 1 << 4,
    Bleeding = 1 << 5,
    Illuminated = 1 << 6,
    Blessed = 1 << 7,
    Exalted = 1 << 8,
    Tainted = 1 << 9,
    Defiled = 1 << 10,
    Forsaken = 1 << 11,
}

// Everdawn's numbers for what the damage types do beyond damage (its ThermalSystem, StunSystem, BleedSystem and
// AstralSystem), unchanged but for time: Everdawn counts in turns and this game in seconds.
public static class StatusRules
{
    // A turn of Everdawn's, here: bars decay and burns and wounds bite once in this long.
    public const float Turn = 1f;

    // How long a body frozen or stunned loses, where Everdawn's loses its turn. About what a skeleton takes between
    // two blows.
    public const float LostTurn = 2f;

    public const int BarMost = 100;

    public const int ChilledAt = 30;
    public const int FrozenAt = 100;
    public const int FrozenKeeps = 40;
    public const int ThawedKeepsAtMost = 10;
    public const int ColdDecay = 15;
    public const float ChilledSpeed = 0.8f;

    // A burn loses half of itself a turn, and never less than this.
    public const int BurnDecayLeast = 15;

    // Fire builds its burn by the damage it did: its power for every this much damage. Everdawn's is 100, with
    // powers of 40 to 50, so a burn starts at near half the hit; with the lower powers here this keeps that share.
    public const int BurnPerDamage = 60;

    public const int DizzyAt = 30;
    public const int StunnedAt = 100;
    public const int StunnedKeeps = 45;
    public const int RecoveredKeepsAtMost = 5;
    public const int StunDecay = 20;
    public const float DizzyDamage = 0.8f;

    public const int BleedDecay = 10;

    // A wound takes this share of what HP the body has left, every turn.
    public const float BleedOfHp = 0.05f;

    // A blow from a foe lights a body no further than this; its own divine casting, to the full bar.
    public const int LitByFoeAtMost = 30;
    public const int BlessedAt = 40;
    public const int ExaltedAt = 80;
    public const int DefiledAt = 40;
    public const int ForsakenAt = 80;
    public const int AstralDecay = 10;

    // What casting divine or void magic builds on the caster itself, a cast.
    public const int CasterAstral = 20;

    public const float BlessedDivineDealt = 1.25f;
    public const float ExaltedDivineDealt = 1.5f;
    public const int BlessedVoidResistance = 20;
    public const int ExaltedVoidResistance = 40;
    public const float DefiledVoidDealt = 1.25f;
    public const float ForsakenVoidDealt = 1.5f;
    public const int CorruptedVoidResistance = -20;
    public const int DefiledDivineResistance = 10;
    public const int ForsakenDivineResistance = 20;
}

// A bite of a burn or a wound, for whoever keeps the body's HP to deal.
public readonly record struct StatusTick(DamageType Type, int Damage);

// One body's six bars, as Everdawn keeps them: burn against cold, a stun bar, a bleed bar, illumination against
// corruption. Hits build them, by type; time wears them down; what they have reached is the body's statuses.
public sealed class StatusBars
{
    private float _sinceTurn;
    private float _lostLeft;
    private Statuses _lostTo;

    public int Burn { get; private set; }

    public int Cold { get; private set; }

    public int Stun { get; private set; }

    public int Bleed { get; private set; }

    public int Illumination { get; private set; }

    public int Corruption { get; private set; }

    // Frozen or stunned: it neither moves nor acts.
    public bool Lost => _lostLeft > 0f;

    public Statuses Active
    {
        get
        {
            var active = Lost ? _lostTo : Statuses.None;
            active |= Burn > 0 ? Statuses.Burning : Statuses.None;
            active |= Bleed > 0 ? Statuses.Bleeding : Statuses.None;
            active |= Cold >= StatusRules.ChilledAt && !active.HasFlag(Statuses.Frozen) ? Statuses.Chilled : Statuses.None;
            active |= Stun >= StatusRules.DizzyAt && !active.HasFlag(Statuses.Stunned) ? Statuses.Dizzy : Statuses.None;
            active |= Illumination >= StatusRules.ExaltedAt ? Statuses.Exalted
                : Illumination >= StatusRules.BlessedAt ? Statuses.Blessed
                : Illumination > 0 ? Statuses.Illuminated : Statuses.None;
            active |= Corruption >= StatusRules.ForsakenAt ? Statuses.Forsaken
                : Corruption >= StatusRules.DefiledAt ? Statuses.Defiled
                : Corruption > 0 ? Statuses.Tainted : Statuses.None;
            return active;
        }
    }

    // The share of its run and attack speed the body keeps.
    public float Speed => Active.HasFlag(Statuses.Chilled) ? StatusRules.ChilledSpeed : 1f;

    // What the body's own damage of this type is multiplied by.
    public float Dealt(DamageType type)
    {
        var active = Active;
        float dealt = active.HasFlag(Statuses.Dizzy) ? StatusRules.DizzyDamage : 1f;
        if (type == DamageType.Divine)
        {
            dealt *= active.HasFlag(Statuses.Exalted) ? StatusRules.ExaltedDivineDealt : active.HasFlag(Statuses.Blessed) ? StatusRules.BlessedDivineDealt : 1f;
        }
        else if (type == DamageType.Void)
        {
            dealt *= active.HasFlag(Statuses.Forsaken) ? StatusRules.ForsakenVoidDealt : active.HasFlag(Statuses.Defiled) ? StatusRules.DefiledVoidDealt : 1f;
        }

        return dealt;
    }

    // The body's resistances as its astral bars leave them: the lit resist the void, the corrupted give way to it
    // and resist the divine.
    public Resistances Adjusted(Resistances own)
    {
        var active = Active;
        if (active.HasFlag(Statuses.Exalted))
        {
            own = own.Raised(DamageType.Void, StatusRules.ExaltedVoidResistance);
        }
        else if (active.HasFlag(Statuses.Blessed))
        {
            own = own.Raised(DamageType.Void, StatusRules.BlessedVoidResistance);
        }

        if (active.HasFlag(Statuses.Forsaken))
        {
            own = own.Raised(DamageType.Void, StatusRules.CorruptedVoidResistance).Raised(DamageType.Divine, StatusRules.ForsakenDivineResistance);
        }
        else if (active.HasFlag(Statuses.Defiled))
        {
            own = own.Raised(DamageType.Void, StatusRules.CorruptedVoidResistance).Raised(DamageType.Divine, StatusRules.DefiledDivineResistance);
        }

        return own;
    }

    // A hit lands on the body, or the part of one that is of this type: `power` is what the skill builds, `damage`
    // what the part dealt before the body's resistances, which a burn grows by. An element takes the bar that
    // opposes it down first and builds its own with what is left, less what the body resists.
    public void Build(DamageType type, int power, int damage, Resistances resistances)
    {
        if (power <= 0)
        {
            return;
        }

        float taken = resistances.Taken(type);
        switch (type)
        {
            case DamageType.Fire:
                Burn = Opposed(power * damage / StatusRules.BurnPerDamage, taken, Cold, Burn, int.MaxValue, out int cold);
                Cold = cold;
                break;
            case DamageType.Water:
            case DamageType.Ice:
                BuildCold(power, taken, type == DamageType.Water ? StatusRules.ChilledAt : StatusRules.BarMost);
                break;
            case DamageType.Blunt:
            case DamageType.Earth:
            case DamageType.Lightning:
            case DamageType.Wind:
                BuildStun(power, type == DamageType.Wind ? StatusRules.DizzyAt : StatusRules.BarMost);
                break;
            case DamageType.Slash:
                Bleed = Math.Min(StatusRules.BarMost, Bleed + (int)(power * taken));
                break;
            case DamageType.Divine:
                Illumination = Opposed(power, taken, Corruption, Illumination, StatusRules.LitByFoeAtMost, out int corruption);
                Corruption = corruption;
                break;
            case DamageType.Void:
                Corruption = Opposed(power, taken, Illumination, Corruption, StatusRules.BarMost, out int illumination);
                Illumination = illumination;
                break;
            case DamageType.Pierce:
            case DamageType.Arcane:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "A damage type that builds nothing, and is not said to");
        }
    }

    // The body casts magic of this type itself: divine lights it and void corrupts it, each taking the other down
    // first, as a hit would. Its own resistances are no matter here.
    public void BuildFromCasting(DamageType type, int power)
    {
        if (type == DamageType.Divine)
        {
            Illumination = Opposed(power, 1f, Corruption, Illumination, StatusRules.BarMost, out int corruption);
            Corruption = corruption;
        }
        else if (type == DamageType.Void)
        {
            Corruption = Opposed(power, 1f, Illumination, Corruption, StatusRules.BarMost, out int illumination);
            Illumination = illumination;
        }
    }

    // The body starts a skill: what of it is divine or void builds on the body itself, by its share.
    public void BuildFromCasting(SkillDefinition skill)
    {
        foreach (var (type, share) in skill.Parts)
        {
            BuildFromCasting(type, (int)MathF.Round(StatusRules.CasterAstral * share));
        }
    }

    // Time passes. A lost turn runs out; and once a turn, burns and wounds bite and every bar wears down. What
    // bit is returned for whoever keeps the body's HP, which is `hp` now.
    public IReadOnlyList<StatusTick> Advance(float delta, int hp, Resistances resistances)
    {
        if (_lostLeft > 0f)
        {
            _lostLeft -= delta;
            if (_lostLeft <= 0f)
            {
                // Out of it with little left on the bar, so it is not lost again at once.
                Cold = _lostTo.HasFlag(Statuses.Frozen) ? Math.Min(Cold, StatusRules.ThawedKeepsAtMost) : Cold;
                Stun = _lostTo.HasFlag(Statuses.Stunned) ? Math.Min(Stun, StatusRules.RecoveredKeepsAtMost) : Stun;
                _lostLeft = 0f;
                _lostTo = Statuses.None;
            }
        }

        _sinceTurn += delta;
        if (_sinceTurn < StatusRules.Turn)
        {
            return Array.Empty<StatusTick>();
        }

        _sinceTurn -= StatusRules.Turn;
        var ticks = new List<StatusTick>(2);
        if (Burn > 0)
        {
            ticks.Add(new StatusTick(DamageType.Fire, Math.Max(1, (int)MathF.Round(Burn * resistances.Taken(DamageType.Fire)))));
        }

        if (Bleed > 0)
        {
            ticks.Add(new StatusTick(DamageType.Pierce, Math.Max(1, (int)MathF.Round(hp * StatusRules.BleedOfHp * resistances.Taken(DamageType.Pierce)))));
        }

        Burn = Math.Max(0, Burn - Math.Max(Burn / 2, StatusRules.BurnDecayLeast));
        Cold = Math.Max(0, Cold - StatusRules.ColdDecay);
        Stun = Math.Max(0, Stun - StatusRules.StunDecay);
        Bleed = Math.Max(0, Bleed - StatusRules.BleedDecay);
        Illumination = Math.Max(0, Illumination - StatusRules.AstralDecay);
        Corruption = Math.Max(0, Corruption - StatusRules.AstralDecay);
        return ticks;
    }

    // The body dies, or gets back up: nothing is left on it.
    public void Clear()
    {
        Burn = Cold = Stun = Bleed = Illumination = Corruption = 0;
        _lostLeft = 0f;
        _lostTo = Statuses.None;
        _sinceTurn = 0f;
    }

    // Water cools no further than a chill: only ice freezes. Either puts the burn out first. A frozen body takes no
    // more cold until it thaws.
    private void BuildCold(int power, float taken, int noFurtherThan)
    {
        if (_lostTo.HasFlag(Statuses.Frozen))
        {
            return;
        }

        int before = Cold;
        int cold = Opposed(power, taken, Burn, Cold, StatusRules.BarMost, out int burn);
        Burn = burn;
        Cold = Math.Min(cold, Math.Max(before, noFurtherThan));
        if (Cold >= StatusRules.FrozenAt)
        {
            Cold = StatusRules.FrozenKeeps;
            Lose(Statuses.Frozen);
        }
    }

    // Wind shakes no further than dizzy: a blunt blow, earth or lightning stuns. Nothing resists a stun yet.
    private void BuildStun(int power, int noFurtherThan)
    {
        if (_lostTo.HasFlag(Statuses.Stunned))
        {
            return;
        }

        Stun = Math.Min(Math.Min(StatusRules.BarMost, Stun + power), Math.Max(Stun, noFurtherThan));
        if (Stun >= StatusRules.StunnedAt)
        {
            Stun = StatusRules.StunnedKeeps;
            Lose(Statuses.Stunned);
        }
    }

    private void Lose(Statuses to)
    {
        _lostTo |= to;
        _lostLeft = StatusRules.LostTurn;
    }

    // Everdawn's opposing buildup: the opposing bar comes down first, whatever the body resists; what is left of
    // the power builds the bar it is for, less what the body resists, and no higher than the bar goes for this
    // source. A bar already higher than that stays where it is.
    private static int Opposed(int power, float taken, int opposing, int bar, int noHigherThan, out int opposingLeft)
    {
        int removed = Math.Min(opposing, power);
        opposingLeft = opposing - removed;
        int built = (int)((power - removed) * taken);
        return Math.Max(bar, Math.Min(noHigherThan, bar + built));
    }
}
