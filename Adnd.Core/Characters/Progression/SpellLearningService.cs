using Adnd.Core.Spells;

namespace Adnd.Core.Characters.Progression;

public sealed class SpellLearningService
{
    private readonly Random _random;

    public SpellLearningService(Random? random = null)
    {
        _random = random ?? new Random();
    }

    /// <summary>
    /// Attempts to learn new spells for Intelligence-based casters (Magic User, Illusionist) when they level up.
    /// </summary>
    public List<string> AttemptLearnSpellsOnLevelUp(Character character, int oldLevel, int newLevel, List<Spell> availableSpells)
    {
        var learnedSpells = new List<string>();

        // Only apply to Magic Users and Illusionists
        var intBasedClasses = character.Classes
            .Where(c => c is CharacterClass.MagicUser or CharacterClass.Illusionist)
            .ToList();

        if (intBasedClasses.Count == 0)
            return learnedSpells;

        // Process each level gained
        for (int level = oldLevel + 1; level <= newLevel; level++)
        {
            foreach (var characterClass in intBasedClasses)
            {
                var spellClass = characterClass == CharacterClass.MagicUser 
                    ? SpellClass.MagicUser 
                    : SpellClass.Illusionist;

                var state = character.Spellcasting?.FirstOrDefault(s => s.SpellClass == spellClass);
                if (state == null)
                    continue;

                // Determine which spell levels the character can now cast
                var availableSpellLevels = new List<int>();
                for (int i = 0; i < state.SlotsPerDay.Count; i++)
                {
                    if (state.SlotsPerDay[i] > 0)
                        availableSpellLevels.Add(i + 1); // Convert 0-based index to 1-based spell level
                }

                // For each available spell level, try to learn new spells
                foreach (var spellLevel in availableSpellLevels)
                {
                    var spellsOfLevel = availableSpells
                        .Where(s => s.SpellClass == spellClass && s.Level == spellLevel)
                        .ToList();

                    var unknownSpells = spellsOfLevel
                        .Where(s => !state.KnownSpellIds.Contains(s.Id))
                        .ToList();

                    // Try to learn 1-2 new spells per spell level when gaining access to it
                    int spellsToAttempt = level == spellLevel ? 2 : 1; // Learn 2 when first gaining the level

                    foreach (var spell in unknownSpells.Take(spellsToAttempt))
                    {
                        if (TryLearnSpell(character, spell.Id))
                        {
                            state.KnownSpellIds.Add(spell.Id);
                            learnedSpells.Add($"{spell.Name} (L{spell.Level})");
                        }
                    }
                }
            }
        }

        return learnedSpells;
    }

    /// <summary>
    /// Attempts to learn a specific spell based on character's Intelligence score.
    /// Returns true if the spell was learned.
    /// </summary>
    public bool TryLearnSpell(Character character, string spellId)
    {
        var intelligence = character.Abilities.Intelligence;
        var chanceToLearn = GetChanceToLearnSpell(intelligence);

        var roll = _random.Next(1, 101); // Roll 1-100
        return roll <= chanceToLearn;
    }

    /// <summary>
    /// Gets the percentage chance to learn a spell based on Intelligence score.
    /// </summary>
    public static int GetChanceToLearnSpell(int intelligence)
    {
        return intelligence switch
        {
            >= 18 => 95,
            17 => 85,
            16 => 75,
            15 => 70,
            14 => 65,
            13 => 60,
            12 => 55,
            11 => 50,
            10 => 45,
            9 => 40,
            _ => 35 // Below 9 or above 18
        };
    }
}
