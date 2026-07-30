namespace Adnd.Core.Characters.Progression;

public static class ExperienceTable
{
    private static readonly Dictionary<CharacterClass, int[]> Thresholds = new()
    {
        // Index 0 => level 1 threshold
        [CharacterClass.Fighter] = [0, 2000, 4000, 8000, 18000, 35000, 70000, 125000, 250000, 500000, 750000, 1000000, 1250000, 1500000, 1750000, 2000000],
        [CharacterClass.Paladin] = [0, 2250, 4500, 9000, 18000, 36000, 75000, 150000, 300000, 600000, 900000, 1200000, 1500000, 1800000, 2100000, 2400000],
        [CharacterClass.Ranger] = [0, 2250, 4500, 10000, 20000, 40000, 90000, 150000, 225000, 325000, 600000, 900000, 1200000, 1500000, 1800000, 2100000],
        [CharacterClass.Cleric] = [0, 1500, 3000, 6000, 13000, 27500, 55000, 110000, 225000, 450000, 675000, 900000, 1125000, 1350000, 1575000, 1800000],
        [CharacterClass.Druid] = [0, 2000, 4000, 7500, 12500, 20000, 35000, 60000, 90000, 125000, 200000, 300000, 750000, 1500000, 3000000, 3500000],
        [CharacterClass.MagicUser] = [0, 2500, 5000, 10000, 22500, 40000, 60000, 90000, 135000, 250000, 375000, 750000, 1125000, 1500000, 1875000, 2250000],
        [CharacterClass.Illusionist] = [0, 2250, 4500, 9000, 18000, 35000, 60000, 90000, 130000, 200000, 300000, 450000, 600000, 750000, 900000, 1050000],
        [CharacterClass.Thief] = [0, 1250, 2500, 5000, 10000, 20000, 42500, 70000, 110000, 160000, 220000, 440000, 660000, 880000, 1100000, 1320000],
        [CharacterClass.Assassin] = [0, 1500, 3000, 6000, 12000, 24750, 50000, 100000, 200000, 300000, 450000, 600000, 750000, 900000, 1050000, 1200000],
        [CharacterClass.Monk] = [0, 2250, 4500, 9000, 18000, 35000, 70000, 125000, 250000, 500000, 750000, 1000000, 1250000, 1500000, 1750000, 2000000],
        [CharacterClass.Bard] = [0, 2500, 5000, 10000, 20000, 40000, 70000, 110000, 160000, 220000, 440000, 660000, 880000, 1100000, 1320000, 1540000]
    };

    public static int GetLevelForExperience(Character character, int experience)
    {
        if (character.IsDualClassed && character.DualClass.HasValue)
            return GetLevelForClass(character.DualClass.Value, experience);

        if (character.Classes == null || character.Classes.Count == 0)
            return 1;

        if (character.Classes.Count == 1)
            return GetLevelForClass(character.Classes[0], experience);

        // Multiclass: split XP equally, level is constrained by the slowest class.
        var share = experience / character.Classes.Count;
        int minLevel = int.MaxValue;
        foreach (var cls in character.Classes)
        {
            var lvl = GetLevelForClass(cls, share);
            if (lvl < minLevel)
                minLevel = lvl;
        }

        return Math.Max(1, minLevel);
    }

    public static int GetLevelForClass(CharacterClass cls, int experience)
    {
        if (!Thresholds.TryGetValue(cls, out var table) || table.Length == 0)
            return 1;

        int level = 1;
        for (int i = 0; i < table.Length; i++)
        {
            if (experience >= table[i])
                level = i + 1;
            else
                break;
        }

        return level;
    }

    public static int GetThresholdForLevel(CharacterClass cls, int level)
    {
        if (!Thresholds.TryGetValue(cls, out var table) || table.Length == 0)
            return int.MaxValue;

        if (level <= 1)
            return table[0];

        if (level - 1 < table.Length)
            return table[level - 1];

        // Above table range, continue with linear progression by last increment.
        int last = table[^1];
        int prev = table.Length > 1 ? table[^2] : 0;
        int step = Math.Max(1, last - prev);
        int extraLevels = level - table.Length;
        return last + (step * extraLevels);
    }
}
