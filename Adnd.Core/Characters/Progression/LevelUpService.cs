namespace Adnd.Core.Characters.Progression;

public sealed class LevelUpService
{
    private readonly SpellProgressionService _spellProgressionService = new();

    public LevelUpResult ApplyExperienceAndAutoLevel(Character character, int gainedXp)
    {
        if (gainedXp < 0)
            gainedXp = 0;

        character.EnsureClassProgressions();

        var primaryClass = character.Classes.Count > 0 ? character.Classes[0] : character.Class;
        var primaryBefore = character.GetClassLevel(primaryClass);

        var result = new LevelUpResult
        {
            CharacterName = character.Name,
            OldLevel = primaryBefore,
            ExperienceBefore = character.Experience
        };

        if (character.Classes.Count <= 1)
        {
            var entry = character.ClassProgressions.First();
            entry.Experience += gainedXp;
            LevelSingleClass(character, entry, ref result);
        }
        else
        {
            var perClass = gainedXp / character.Classes.Count;
            var remainder = gainedXp % character.Classes.Count;

            for (int i = 0; i < character.ClassProgressions.Count; i++)
            {
                var gain = perClass + (i < remainder ? 1 : 0);
                character.ClassProgressions[i].Experience += gain;
            }

            foreach (var entry in character.ClassProgressions)
            {
                var target = ExperienceTable.GetLevelForClass(entry.Class, entry.Experience);
                entry.Level = Math.Max(entry.Level, target);
            }

            var primaryEntry = character.ClassProgressions.First(e => e.Class == primaryClass);
            while (primaryEntry.Level > character.Level)
            {
                character.Level++;
                var gain = HitPointProgression.RollHitPointGain(character, character.Level);
                result.HitPointsGained += gain;
                character.MaxHitPoints += gain;
                character.CurrentHitPoints += gain;
            }

            ApplyBasicWarriorAttackProgression(character, primaryEntry.Level);
        }

        character.EnsureClassProgressions();
        result.NewLevel = character.GetClassLevel(primaryClass);
        result.ExperienceAfter = character.Experience;

        result.SpellSlotChanges = _spellProgressionService.RecalculateFromClassProgressions(character);

        return result;
    }

    private static void LevelSingleClass(Character character, ClassProgression entry, ref LevelUpResult result)
    {
        var targetLevel = ExperienceTable.GetLevelForClass(entry.Class, entry.Experience);

        while (entry.Level < targetLevel)
        {
            entry.Level++;
            character.Level = entry.Level;

            var gain = HitPointProgression.RollHitPointGain(character, character.Level);
            result.HitPointsGained += gain;
            character.MaxHitPoints += gain;
            character.CurrentHitPoints += gain;
        }

        ApplyBasicWarriorAttackProgression(character, entry.Level);
    }

    private static void ApplyBasicWarriorAttackProgression(Character character, int effectiveLevel)
    {
        if (character.Classes.Count == 0)
            return;

        var primary = character.Classes[0];
        if (primary is CharacterClass.Fighter or CharacterClass.Paladin or CharacterClass.Ranger)
        {
            character.NumberOfAttacks = effectiveLevel >= 13 ? 2 : 1;
        }
    }
}
