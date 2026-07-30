using Adnd.Core.Characters;

namespace Adnd.Core.Spells;

public static class SpellProgression
{
    public static List<(SpellClass SpellClass, List<int> SlotsPerDay)> GetSpellcastingTracks(CharacterClass characterClass, int level)
    {
        var tracks = new List<(SpellClass SpellClass, List<int> SlotsPerDay)>();

        switch (characterClass)
        {
            case CharacterClass.MagicUser:
                tracks.Add((SpellClass.MagicUser, GetSlotsFromTable(MagicUserSlots, SpellClass.MagicUser, level)));
                break;
            case CharacterClass.Illusionist:
                tracks.Add((SpellClass.Illusionist, GetSlotsFromTable(IllusionistSlots, SpellClass.Illusionist, level)));
                break;
            case CharacterClass.Cleric:
                tracks.Add((SpellClass.Cleric, GetSlotsFromTable(ClericSlots, SpellClass.Cleric, level)));
                break;
            case CharacterClass.Druid:
                tracks.Add((SpellClass.Druid, GetSlotsFromTable(DruidSlots, SpellClass.Druid, level)));
                break;
            case CharacterClass.Ranger:
                // AD&D 1E: Ranger gets MU spells from level 8 and Cleric spells from level 9.
                tracks.Add((SpellClass.MagicUser, GetSlotsFromTable(RangerMagicUserSlots, SpellClass.MagicUser, level)));
                tracks.Add((SpellClass.Cleric, GetSlotsFromTable(RangerClericSlots, SpellClass.Cleric, level)));
                break;
            case CharacterClass.Paladin:
                // AD&D 1E: Paladin first spells at level 9.
                tracks.Add((SpellClass.Cleric, GetSlotsFromTable(PaladinClericSlots, SpellClass.Cleric, level)));
                break;
            case CharacterClass.Bard:
                // AD&D 1E: Bard gets druid spells from level 8.
                tracks.Add((SpellClass.Druid, GetSlotsFromTable(BardDruidSlots, SpellClass.Druid, level)));
                break;
        }

        return tracks;
    }

    public static bool TryGetSpellClass(CharacterClass characterClass, out SpellClass spellClass)
    {
        switch (characterClass)
        {
            case CharacterClass.MagicUser:
                spellClass = SpellClass.MagicUser;
                return true;
            case CharacterClass.Illusionist:
                spellClass = SpellClass.Illusionist;
                return true;
            case CharacterClass.Cleric:
                spellClass = SpellClass.Cleric;
                return true;
            case CharacterClass.Druid:
                spellClass = SpellClass.Druid;
                return true;
            default:
                spellClass = default;
                return false;
        }
    }

    public static int GetMaxSpellLevel(SpellClass spellClass)
    {
        return spellClass == SpellClass.MagicUser ? 9 : 7;
    }

    public static List<int> GetSlotsPerDay(SpellClass spellClass, int level)
    {
        if (level < 1)
            level = 1;

        var table = spellClass switch
        {
            SpellClass.MagicUser => MagicUserSlots,
            SpellClass.Illusionist => IllusionistSlots,
            SpellClass.Cleric => ClericSlots,
            SpellClass.Druid => DruidSlots,
            _ => MagicUserSlots
        };

        var row = table.TryGetValue(level, out var exact)
            ? exact
            : table[table.Keys.Max()];

        return new List<int>(row);
    }

    private static List<int> GetSlotsFromTable(Dictionary<int, int[]> table, SpellClass spellClass, int level)
    {
        if (level < 1)
            level = 1;

        if (table.Count == 0)
            return Enumerable.Repeat(0, GetMaxSpellLevel(spellClass)).ToList();

        var minLevel = table.Keys.Min();
        if (level < minLevel)
            return Enumerable.Repeat(0, GetMaxSpellLevel(spellClass)).ToList();

        var row = table.TryGetValue(level, out var exact)
            ? exact
            : table[table.Keys.Max()];

        return new List<int>(row);
    }

    private static readonly Dictionary<int, int[]> MagicUserSlots = new()
    {
        { 1, [1, 0, 0, 0, 0, 0, 0, 0, 0] },
        { 2, [2, 0, 0, 0, 0, 0, 0, 0, 0] },
        { 3, [2, 1, 0, 0, 0, 0, 0, 0, 0] },
        { 4, [3, 2, 0, 0, 0, 0, 0, 0, 0] },
        { 5, [4, 2, 1, 0, 0, 0, 0, 0, 0] },
        { 6, [4, 2, 2, 0, 0, 0, 0, 0, 0] },
        { 7, [4, 3, 2, 1, 0, 0, 0, 0, 0] },
        { 8, [4, 3, 3, 2, 0, 0, 0, 0, 0] },
        { 9, [4, 3, 3, 2, 1, 0, 0, 0, 0] },
        { 10, [4, 4, 3, 2, 2, 0, 0, 0, 0] }
    };

    private static readonly Dictionary<int, int[]> IllusionistSlots = new()
    {
        { 1, [1, 0, 0, 0, 0, 0, 0] },
        { 2, [2, 0, 0, 0, 0, 0, 0] },
        { 3, [2, 1, 0, 0, 0, 0, 0] },
        { 4, [3, 2, 0, 0, 0, 0, 0] },
        { 5, [4, 2, 1, 0, 0, 0, 0] },
        { 6, [4, 2, 2, 0, 0, 0, 0] },
        { 7, [4, 3, 2, 1, 0, 0, 0] },
        { 8, [4, 3, 3, 2, 0, 0, 0] },
        { 9, [4, 3, 3, 2, 1, 0, 0] },
        { 10, [4, 4, 3, 2, 2, 0, 0] }
    };

    private static readonly Dictionary<int, int[]> ClericSlots = new()
    {
        { 1, [1, 0, 0, 0, 0, 0, 0] },
        { 2, [2, 0, 0, 0, 0, 0, 0] },
        { 3, [2, 1, 0, 0, 0, 0, 0] },
        { 4, [3, 2, 0, 0, 0, 0, 0] },
        { 5, [3, 3, 1, 0, 0, 0, 0] },
        { 6, [3, 3, 2, 0, 0, 0, 0] },
        { 7, [3, 3, 2, 1, 0, 0, 0] },
        { 8, [3, 3, 3, 2, 0, 0, 0] },
        { 9, [4, 3, 3, 2, 1, 0, 0] },
        { 10, [4, 4, 3, 2, 2, 0, 0] }
    };

    private static readonly Dictionary<int, int[]> DruidSlots = new()
    {
        { 1, [1, 0, 0, 0, 0, 0, 0] },
        { 2, [2, 0, 0, 0, 0, 0, 0] },
        { 3, [2, 1, 0, 0, 0, 0, 0] },
        { 4, [2, 2, 0, 0, 0, 0, 0] },
        { 5, [3, 2, 1, 0, 0, 0, 0] },
        { 6, [3, 3, 2, 0, 0, 0, 0] },
        { 7, [3, 3, 2, 1, 0, 0, 0] },
        { 8, [3, 3, 3, 2, 0, 0, 0] },
        { 9, [4, 3, 3, 2, 1, 0, 0] },
        { 10, [4, 4, 3, 2, 2, 0, 0] }
    };

    // Ranger gets Magic-User spells from level 8
    private static readonly Dictionary<int, int[]> RangerMagicUserSlots = new()
    {
        { 8,  [1, 0, 0, 0, 0, 0, 0, 0, 0] },
        { 9,  [2, 0, 0, 0, 0, 0, 0, 0, 0] },
        { 10, [2, 1, 0, 0, 0, 0, 0, 0, 0] }
    };

    // Ranger gets Cleric spells from level 9
    private static readonly Dictionary<int, int[]> RangerClericSlots = new()
    {
        { 9,  [1, 0, 0, 0, 0, 0, 0] },
        { 10, [2, 0, 0, 0, 0, 0, 0] }
    };

    // Paladin first spells at level 9
    private static readonly Dictionary<int, int[]> PaladinClericSlots = new()
    {
        { 9,  [1, 0, 0, 0, 0, 0, 0] },
        { 10, [1, 1, 0, 0, 0, 0, 0] }
    };

    // Bard gets druid spells from level 8
    private static readonly Dictionary<int, int[]> BardDruidSlots = new()
    {
        { 8,  [1, 0, 0, 0, 0, 0, 0] },
        { 9,  [2, 0, 0, 0, 0, 0, 0] },
        { 10, [2, 1, 0, 0, 0, 0, 0] }
    };
}
