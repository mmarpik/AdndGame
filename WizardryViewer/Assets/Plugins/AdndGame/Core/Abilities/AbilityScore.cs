// Abilities/AbilityScore.cs
namespace Adnd.Core.Abilities;

public enum AbilityType { Strength, Intelligence, Wisdom, Dexterity, Constitution, Charisma }

public sealed class AbilityScore
{
    public AbilityType Type { get; }
    public int Value { get; }

    public AbilityScore(AbilityType type, int value)
    {
        Type = type;
        Value = value;
    }
}
