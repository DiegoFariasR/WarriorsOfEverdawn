using System;

namespace WarriorsOfEverdawn.Core.Stats;

// First pass: each level asks XpGrowth times the XP of the one before, and gives a point to put into a stat.
public static class LevelRules
{
    public const int FirstLevel = 1;
    public const int FirstLevelXp = 50;
    public const float XpGrowth = 1.5f;
    public const int PointsPerLevel = 1;

    // From this level to the next. Checked: far enough up, the XP no longer fits in an int.
    public static int ToNext(int level)
    {
        if (level < FirstLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"Levels start at {FirstLevel}");
        }

        return checked((int)MathF.Round(FirstLevelXp * MathF.Pow(XpGrowth, level - FirstLevel)));
    }

    // All it takes to reach the level from the first.
    public static int Reaching(int level)
    {
        int total = 0;
        for (int below = FirstLevel; below < level; below++)
        {
            total = checked(total + ToNext(below));
        }

        return total;
    }
}

// A player's level, the XP it has toward the next, the points it has yet to spend, and the stats they have gone into.
public sealed class Progress
{
    public Progress(CharacterStats start) => Stats = start;

    public int Level { get; private set; } = LevelRules.FirstLevel;

    // Toward the next level.
    public int Xp { get; private set; }

    public int Points { get; private set; }

    public CharacterStats Stats { get; private set; }

    public int ToNext => LevelRules.ToNext(Level);

    // How many levels it gained.
    public int Earn(int xp)
    {
        if (xp < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(xp), xp, "XP is only ever earned");
        }

        Xp += xp;
        int gained = 0;
        while (Xp >= ToNext)
        {
            Xp -= ToNext;
            Level++;
            Points += LevelRules.PointsPerLevel;
            gained++;
        }

        return gained;
    }

    // A point into the stat, if it has one to spend. Whether it did.
    public bool Spend(Stat stat)
    {
        if (Points == 0)
        {
            return false;
        }

        Points--;
        Stats = Stats.Raised(stat);
        return true;
    }
}
