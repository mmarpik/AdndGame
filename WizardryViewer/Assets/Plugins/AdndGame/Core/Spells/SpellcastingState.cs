namespace Adnd.Core.Spells;

public class SpellcastingState
{
    public SpellClass SpellClass { get; set; }

    // IDs of known spells (for Magic-User/Illusionist primarily)
    public List<string> KnownSpellIds { get; set; } = new();

    // Prepared/memorized spells and their counts
    public List<PreparedSpell> PreparedSpells { get; set; } = new();

    // Spells per day by spell level index (0-based => level 1 spell)
    public List<int> SlotsPerDay { get; set; } = new();

    // Used slots per day by spell level index
    public List<int> SlotsUsed { get; set; } = new();
}
