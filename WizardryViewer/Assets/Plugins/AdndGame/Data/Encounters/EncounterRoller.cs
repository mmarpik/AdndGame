using System;
using Adnd.Data.Encounters;

namespace Adnd.Core.Encounters;

public static class EncounterRoller
{
    private static readonly Random _rng = new();

    public static string Roll(EncounterJsonModel table)
    {
        int roll = _rng.Next(1, 101);
        int cumulative = 0;

        foreach (var entry in table.Entries)
        {
            cumulative += entry.Chance;
            if (roll <= cumulative)
                return entry.Monster;
        }

        return "No Encounter";
    }
}
