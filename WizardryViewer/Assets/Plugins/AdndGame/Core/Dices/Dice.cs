// Random/Dice.cs
namespace Adnd.Core.Dices;

public interface IDice
{
    int Roll(int sides);
    int RollMany(int sides, int count);
    int GetNumberOfSuccesses(int numberOfTries, float probabilityEachTry);
}

public sealed class SystemDice : IDice
{
    private readonly Random _rng = new();//Maybe this should be my private random number generator?

    public int Roll(int sides) => _rng.Next(1, sides + 1);
    public int GetNumberOfSuccesses(int numberOfTries, float probabilityEachTry)
    {
        int successes = 0;
        Random rnd = new Random();

        for (int i = 0; i < numberOfTries; i++)
        {
            float roll = (float)rnd.NextDouble(); // Slumpar tal mellan 0.0 och 1.0

            if (roll < probabilityEachTry)
            {
                successes++;
            }
        }

        return successes;
    }

    public int RollMany(int sides, int count)
    {
        int sum = 0;
        for (int i = 0; i < count; i++)
            sum += Roll(sides);
        return sum;
    }
}
