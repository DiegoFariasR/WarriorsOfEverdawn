using System;

namespace WarriorsOfEverdawn.Core.Combat;

// Cooldowns for one character's skill set, against a clock the caller advances.
public sealed class SkillCooldowns
{
    private readonly float[] _readyAt;

    public SkillCooldowns(int skillCount)
    {
        _readyAt = new float[skillCount];
    }

    public bool IsReady(int skill, float now) => now >= _readyAt[skill];

    public float Remaining(int skill, float now) => Math.Max(0f, _readyAt[skill] - now);

    public void Start(int skill, SkillDefinition definition, float now) => _readyAt[skill] = now + definition.Cooldown;
}
