namespace Adnd.Core.Characters;

public static class Thac0Calculator
{
    // Return THAC0 (To Hit Armor Class 0) based on class and level per AD&D 1e rules.
    // THAC0 decreases as level increases (lower is better).
    public static int GetThac0(CharacterClass cls, int level)
    {
        // AD&D 1e THAC0 progression by class group:
        // Fighters (Fighter, Paladin, Ranger): start at 20, improve by 1 every level
        // Clerics/Druids: start at 20, improve by 2 every 3 levels
        // Thieves/Assassins: start at 20, improve by 1 every 2 levels
        // Magic-Users/Illusionists: start at 20, improve by 1 every 3 levels
        // Monks: start at 20, improve by 1 every 2 levels (similar to thieves)
        // Bards: start at 20, improve by 1 every 2 levels (thief-like)

        return cls switch
        {
            CharacterClass.Fighter or CharacterClass.Paladin or CharacterClass.Ranger
                => 20 - (level - 1),
            CharacterClass.Cleric or CharacterClass.Druid
                => 20 - ((level - 1) / 3) * 2,
            CharacterClass.Thief or CharacterClass.Assassin or CharacterClass.Monk or CharacterClass.Bard
                => 20 - (level - 1) / 2,
            CharacterClass.MagicUser or CharacterClass.Illusionist
                => 20 - (level - 1) / 3,
            _ => 20
        };
    }
}

