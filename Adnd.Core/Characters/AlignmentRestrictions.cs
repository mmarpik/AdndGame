using System.Collections.Generic;

namespace Adnd.Core.Characters;

public static class AlignmentRestrictions
{
    // Return allowed alignments for the chosen classes and race, applying AD&D 1e rules.
    public static List<Alignment> GetAllowedAlignments(List<CharacterClass> classes, Race race)
    {
        // Start with all alignments
        var all = new List<Alignment>((Alignment[])System.Enum.GetValues(typeof(Alignment)));

        // If any class is Paladin, only Lawful Good allowed
        if (classes.Contains(CharacterClass.Paladin))
            return new List<Alignment> { Alignment.LawfulGood };

        // If any class is Druid, must be True Neutral
        if (classes.Contains(CharacterClass.Druid))
            return new List<Alignment> { Alignment.TrueNeutral };

        // Ranger: must be Good (Lawful/Neutral/Chaotic Good)
        if (classes.Contains(CharacterClass.Ranger))
            return new List<Alignment> { Alignment.LawfulGood, Alignment.NeutralGood, Alignment.ChaoticGood };

        // Assassin: must be Evil (LE, NE, CE) and cannot be Good; Neutral only NE allowed
        if (classes.Contains(CharacterClass.Assassin))
            return new List<Alignment> { Alignment.LawfulEvil, Alignment.NeutralEvil, Alignment.ChaoticEvil };

        // Monk: must be Lawful (LG, LN, LE) and cannot be Chaotic, NG, NE
        if (classes.Contains(CharacterClass.Monk))
            return new List<Alignment> { Alignment.LawfulGood, Alignment.LawfulNeutral, Alignment.LawfulEvil };

        // Bard: must be Neutral (NG, LN, CN, TN, NE) and exclude LG, LE, CG, CE
        if (classes.Contains(CharacterClass.Bard))
            return new List<Alignment> { Alignment.NeutralGood, Alignment.LawfulNeutral, Alignment.ChaoticNeutral, Alignment.TrueNeutral, Alignment.NeutralEvil };

        // For Rangers, Paladins, Assassins, Monks, Druids, and Bards we've returned early.
        // Otherwise start with all and then apply race-specific constraints.

        // Race rule: Half-Orcs must be Chaotic, Neutral, or Lawful Evil per user rule.
        if (race == Race.HalfOrc)
        {
            var allowed = new List<Alignment> {  Alignment.ChaoticNeutral, Alignment.TrueNeutral, Alignment.LawfulEvil, Alignment.NeutralEvil, Alignment.ChaoticEvil };
            // include LawfulNeutral? user asked "Chaotic, Neutral, or Lawful Evil" -> interpret Neutral as TrueNeutral + NeutralGood/NeutralEvil?
            // To match instruction precisely, allow TN, CN, LE, NE, CE
            return allowed;
        }

        // Cleric: alignment must match the deity, but mechanically allow all except True Neutral unless druid
        // Since deity system not implemented, allow all but disallow TrueNeutral for clerics (unless also Druid which was handled earlier)
        if (classes.Contains(CharacterClass.Cleric))
        {
            var list = new List<Alignment>(all);
            list.Remove(Alignment.TrueNeutral);
            return list;
        }

        // Default: all alignments allowed
        return all;
    }
}
