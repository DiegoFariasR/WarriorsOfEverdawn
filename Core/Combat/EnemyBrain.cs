using System.Collections.Generic;
using System.Numerics;

namespace WarriorsOfEverdawn.Core.Combat;

public enum EnemyAction
{
    Idle,
    Chase,
    Attack,
    Hold,

    // Move away from the target (ranged enemies, while they cannot shoot).
    Retreat,
}

public readonly record struct EnemyTarget(long Id, Vector2 Position);

public readonly record struct EnemyDecision(EnemyAction Action, EnemyTarget Target);

public static class EnemyBrain
{
    // Steps in to this share of its reach before swinging, so a target drifting away mid-swing is still caught.
    public const float EngageFraction = 0.8f;

    public static EnemyDecision Decide(EnemyDefinition enemy, Vector2 position, IReadOnlyList<EnemyTarget> livingPlayers, bool attackReady)
    {
        EnemyTarget? nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (var player in livingPlayers)
        {
            float distance = Vector2.Distance(player.Position, position);
            if (distance < nearestDistance)
            {
                nearest = player;
                nearestDistance = distance;
            }
        }

        if (nearest is not { } target || nearestDistance > enemy.AggroRange)
        {
            return new EnemyDecision(EnemyAction.Idle, default);
        }

        if (nearestDistance - BodySize.Radius > enemy.Attack.Range * EngageFraction)
        {
            return new EnemyDecision(EnemyAction.Chase, target);
        }

        if (attackReady)
        {
            return new EnemyDecision(EnemyAction.Attack, target);
        }

        return new EnemyDecision(nearestDistance < enemy.KeepAway ? EnemyAction.Retreat : EnemyAction.Hold, target);
    }
}
