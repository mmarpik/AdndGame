// Random/Dice.cs
namespace Adnd.Core.Dices;

public interface IDice
{
    int Roll(int sides);
    int RollMany(int sides, int count);
}

public sealed class SystemDice : IDice
{
    private readonly Random _rng = new();//Maybe this should be my private random number generator?

    public int Roll(int sides) => _rng.Next(1, sides + 1);

    public int RollMany(int sides, int count)
    {
        int sum = 0;
        for (int i = 0; i < count; i++)
            sum += Roll(sides);
        return sum;
    }
}
