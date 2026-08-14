using System;

namespace Adnd.Core.Characters;

public static class DiceRoller
{
    private static readonly Random _rng = new();

    public static int Roll(int count, int sides)
    {
        int sum = 0;
        for (int i = 0; i < count; i++)
            sum += _rng.Next(1, sides + 1);
        return sum;
    }

    public static int Roll3d6() => Roll(3, 6);
}
