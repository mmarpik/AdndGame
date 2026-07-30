namespace Adnd.Core.Characters;

public class AbilityScores
{
    public int Strength { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Charisma { get; set; }

    public override string ToString()
    {
        return $"STR {Strength}, INT {Intelligence}, WIS {Wisdom}, " +
               $"DEX {Dexterity}, CON {Constitution}, CHA {Charisma}";
    }
}
