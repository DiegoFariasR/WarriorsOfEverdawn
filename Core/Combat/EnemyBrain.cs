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

    // Walk back to its post, every player lost: the target's position is the post.
    Return,
}

public readonly record struct EnemyTarget(long Id, Vector2 Position);

public readonly record struct EnemyDecision(EnemyAction Action, EnemyTarget Target);

public static class EnemyBrain
{
    // Steps in to this share of its reach before swinging, so a target drifting away mid-swing is still caught.
    public const float EngageFraction = 0.8f;

    // Once after a player, it gives up only when every player is further than its aggro range times this, so one
    // stepping just out of sight does not shake it off.
    public const float GiveUpFactor = 1.5f;

    // Back at its post once this near it.
    public const float AtPost = 1f;

    // `post` is where it rose, which it goes back to; `engaged` whether it was after a player the last time it decided.
    public static EnemyDecision Decide(EnemyDefinition enemy, Vector2 position, Vector2 post, IReadOnlyList<EnemyTarget> livingPlayers, bool attackReady, bool engaged)
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

        if (nearest is not { } target || nearestDistance > enemy.AggroRange * (engaged ? GiveUpFactor : 1f))
        {
            return Vector2.Distance(position, post) > AtPost
                ? new EnemyDecision(EnemyAction.Return, new EnemyTarget(default, post))
                : new EnemyDecision(EnemyAction.Idle, default);
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
