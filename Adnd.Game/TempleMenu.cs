using System;
using Adnd.Core.Characters;
using Adnd.Data.Party;
using Adnd.Data.Characters;

namespace Adnd.Game;

public class TempleMenu
{
    private readonly PartyRepository _partyRepo = new("Data/Party");
    private readonly CharacterRepository _charRepo = new("Data/Characters");

    public void Show()
    {
        while (true)
        {
            var party = _partyRepo.Load();

            Console.Clear();
            Console.WriteLine("=== Church of Chant ===");
            Console.WriteLine("H)eal Party");
            Console.WriteLine("R)aise Dead");
            Console.WriteLine("L>-eave");

            var key = Console.ReadKey(true).Key;

            if (key == ConsoleKey.H) HealParty(party);
            else if (key == ConsoleKey.R) RaiseDead(party);
            else if (key == ConsoleKey.L || key == ConsoleKey.Enter) break;
        }
    }

    private void HealParty(Party party)
    {
        const int healCostPerCharacter = 10;

        var roster = _charRepo.GetAll().ToDictionary(c => c.Name, c => c);
        var partyCharacters = party.Members
            .Where(name => roster.ContainsKey(name))
            .Select(name => roster[name])
            .ToList();

        if (partyCharacters.Count == 0)
        {
            Console.WriteLine("No party members found.");
            Console.ReadKey(true);
            return;
        }

        var needHealing = partyCharacters
            .Where(c => c.CurrentHitPoints < c.MaxHitPoints)
            .ToList();

        if (needHealing.Count == 0)
        {
            Console.WriteLine("No one needs healing.");
            Console.ReadKey(true);
            return;
        }

        Console.WriteLine($"Healing costs {healCostPerCharacter} gp per character who needs healing.");

        var healedCount = 0;
        var skipped = new List<string>();

        foreach (var c in needHealing)
        {
            if (c.GoldPieces < healCostPerCharacter)
            {
                skipped.Add(c.Name);
                continue;
            }

            c.GoldPieces -= healCostPerCharacter;
            c.CurrentHitPoints = c.MaxHitPoints;
            _charRepo.Save(c);
            healedCount++;
        }

        Console.WriteLine($"Healed {healedCount} character(s).");

        if (skipped.Count > 0)
            Console.WriteLine($"Could not afford healing: {string.Join(", ", skipped)}");

        Console.ReadKey(true);
    }

    private void RaiseDead(Party party)
    {
        const int raiseDeadCost = 100;
        const int raiseFromAshesCost = 500;

        var roster = _charRepo.GetAll().ToDictionary(c => c.Name, c => c);
        var partyCharacters = party.Members
            .Where(name => roster.ContainsKey(name))
            .Select(name => roster[name])
            .ToList();

        if (partyCharacters.Count == 0)
        {
            Console.WriteLine("No party members found.");
            Console.ReadKey(true);
            return;
        }

        var revivableMembers = partyCharacters
            .Where(c => (c.HasStatus(CharacterStatus.Dead) || c.HasStatus(CharacterStatus.Ashes) || c.CurrentHitPoints <= 0)
                        && !c.HasStatus(CharacterStatus.Lost))
            .ToList();

        if (revivableMembers.Count == 0)
        {
            Console.WriteLine("No party members can be revived.");
            Console.ReadKey(true);
            return;
        }

        Console.Clear();
        Console.WriteLine("=== RAISE DEAD ===\n");
        Console.WriteLine("Who wants to be raised?");
        for (int i = 0; i < revivableMembers.Count; i++)
        {
            var c = revivableMembers[i];
            var state = c.HasStatus(CharacterStatus.Ashes) ? "Ashes" : "Dead";
            var cost = c.HasStatus(CharacterStatus.Ashes) ? raiseFromAshesCost : raiseDeadCost;
            Console.WriteLine($"{i + 1}. {c.Name} ({state}, HP {c.CurrentHitPoints}/{c.MaxHitPoints}, Cost {cost} gp)");
        }

        Console.Write("Choose #: ");
        var targetSelection = InputHelper.ReadNumber(1, revivableMembers.Count);
        if (!targetSelection.HasValue)
        {
            Console.WriteLine("Invalid selection.");
            Console.ReadKey(true);
            return;
        }

        var target = revivableMembers[targetSelection.Value - 1];
        var isAshesTarget = target.HasStatus(CharacterStatus.Ashes);
        var revivalCost = isAshesTarget ? raiseFromAshesCost : raiseDeadCost;

        Console.WriteLine($"\nWho will pay {revivalCost} gp?");
        for (int i = 0; i < partyCharacters.Count; i++)
        {
            var payer = partyCharacters[i];
            var canAfford = payer.GoldPieces >= revivalCost ? "" : " (not enough)";
            Console.WriteLine($"{i + 1}. {payer.Name} ({payer.GoldPieces} gp){canAfford}");
        }

        Console.Write("Choose #: ");
        var payerSelection = InputHelper.ReadNumber(1, partyCharacters.Count);
        if (!payerSelection.HasValue)
        {
            Console.WriteLine("Invalid selection.");
            Console.ReadKey(true);
            return;
        }

        var payingCharacter = partyCharacters[payerSelection.Value - 1];
        if (payingCharacter.GoldPieces < revivalCost)
        {
            Console.WriteLine($"{payingCharacter.Name} does not have enough gold.");
            Console.ReadKey(true);
            return;
        }

        payingCharacter.GoldPieces -= revivalCost;

        if (target.Abilities.Constitution <= 0)
        {
            target.RemoveStatus(CharacterStatus.Dead);
            target.RemoveStatus(CharacterStatus.Ashes);
            target.AddStatus(CharacterStatus.Lost);
            target.CurrentHitPoints = 0;

            _charRepo.Save(target);
            if (!string.Equals(target.Name, payingCharacter.Name, StringComparison.OrdinalIgnoreCase))
                _charRepo.Save(payingCharacter);

            Console.WriteLine($"{target.Name} has Constitution 0 and is automatically Lost.");
            Console.WriteLine($"{payingCharacter.Name} paid {revivalCost} gp.");
            Console.ReadKey(true);
            return;
        }

        var systemShockChance = GetSystemShockSurvivalChance(target.Abilities.Constitution);
        var roll = Random.Shared.Next(1, 101);

        Console.WriteLine();
        Console.WriteLine($"System Shock roll for {target.Name}: {roll} (needs {systemShockChance} or less)");

        if (roll <= systemShockChance)
        {
            target.RemoveStatus(CharacterStatus.Dead);
            target.RemoveStatus(CharacterStatus.Ashes);
            target.RemoveStatus(CharacterStatus.Lost);

            if (target.CurrentHitPoints <= 0)
                target.CurrentHitPoints = 1;

            target.Abilities.Constitution = Math.Max(0, target.Abilities.Constitution - 1);

            Console.WriteLine($"{target.Name} has been raised.");
            Console.WriteLine($"{target.Name} loses 1 Constitution (now {target.Abilities.Constitution}).");
        }
        else
        {
            if (isAshesTarget)
            {
                target.RemoveStatus(CharacterStatus.Dead);
                target.RemoveStatus(CharacterStatus.Ashes);
                target.AddStatus(CharacterStatus.Lost);
                target.CurrentHitPoints = 0;

                Console.WriteLine($"Revival failed. {target.Name} is now Lost and can never be revived again.");
            }
            else
            {
                target.RemoveStatus(CharacterStatus.Dead);
                target.AddStatus(CharacterStatus.Ashes);
                target.CurrentHitPoints = 0;

                Console.WriteLine($"Raise Dead failed. {target.Name} is now ashes.");
            }
        }

        _charRepo.Save(target);
        if (!string.Equals(target.Name, payingCharacter.Name, StringComparison.OrdinalIgnoreCase))
            _charRepo.Save(payingCharacter);

        Console.WriteLine($"{payingCharacter.Name} paid {revivalCost} gp.");
        Console.ReadKey(true);
    }

    private static int GetSystemShockSurvivalChance(int constitution)
    {
        return constitution switch//not 1e adnd tabel but I think this is better.
        {
            <= 1 => 30,
            <= 3 => 35,
            <= 5 => 40,
            <= 7 => 45,
            <= 9 => 50,
            <= 11 => 55,
            <= 13 => 60,
            <= 15 => 65,
            16 => 70,
            17 => 75,
            18 => 80,
            19 => 85,
            20 => 90,
            21 => 95,
            22 => 97,
            23 => 98,
            24 => 99,
            _ => 100
        };
    }
}
