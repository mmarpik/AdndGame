namespace Adnd.Core.Monsters;

public class Monster
{
    public string Name { get; set; }
    public MonsterType Type { get; set; }
    public string ClimateTerain { get; set; } = string.Empty;//TBD string should be an enum
    public string Frequency { get; set; } = string.Empty;//TBD string should be an enum
    public string ActivityCycle { get; set; } = string.Empty;//TBD string should be an enum
    public string Intelligence { get; set; } = string.Empty;//TBD string should be an enum
    public string Alignment { get; set; } = string.Empty;//TBD string should be an enum
    public int NumberOfAppearancesMin { get; set; }
    public int NumberOfAppearancesMax { get; set; }
    public int ArmorClass { get; set; }
    public string MovementRate { get; set; } = string.Empty;//TBD string should be an enum
    public int HitDice { get; set; }
    public int THAC0 { get; set; }
    public int NumberOfAttacks { get; set; }
    public string MagicResistance { get; set; } = string.Empty;//TBD string should be an enum
    public string Size { get; set; } = string.Empty;//TBD string should be an enum
    public int HitPoints { get; set; }
    /*Example of a monster in JSON format:
            "ClimateTerain": "Temperate",
        "Frequency": "Common",
        "ActivityCycle": "Nocturnal",
        "Intelligence": "Low",
        "Alignment": "Chaotic Evil",
        "NumberOfAppearancesMin": 1,
        "NumberOfAppearancesMax": 4,
        "ArmorClass": 6,
        "MovementRate": "Slow",
        "HitDice": 1,
        "THAC0": 19,
        "NumberOfAttacks": 1,
        "MagicResistance": "None",
        "Size": "Small",
    */


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
