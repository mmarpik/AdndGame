namespace Adnd.Core.Monsters;

public class Monster
{
    public string Name { get; set; }
    public MonsterType Type { get; set; }

    public int ArmorClass { get; set; }
    public int HitDice { get; set; }
    public int HitPoints { get; set; }

    public MonsterMovement Movement { get; set; }
    public MonsterMorale Morale { get; set; }
    public MonsterSavingThrows SavingThrows { get; set; }

    public List<MonsterAttack> Attacks { get; set; } = new();
    public List<MonsterSpecialAbility> SpecialAbilities { get; set; } = new();

    public int XPValue { get; set; }

    // AD&D treasure type token(s), e.g. "A", "B", "A,B", or "None".
    public string TreasureType { get; set; } = "None";

    // Optional override for treasure chance (0.0-1.0) used by future treasure systems.
    public double? TreasureChanceOverride { get; set; }
}
