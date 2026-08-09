using System;
using System.Linq;
using Adnd.Core.Characters;
using Adnd.Core.Config;
using Adnd.Core.Spells;
using Adnd.Data.Characters;
using Adnd.Data.Party;
using Adnd.Data.Spells;

namespace Adnd.Game;

public class MainMenu
{
    private readonly CityMenu _cityMenu = new();
    private readonly PartyMenu _partyMenu = new();
    private readonly TempleMenu _templeMenu = new();
    private readonly ShopMenu _shopMenu = new();
    private readonly DungeonMenu _dungeonMenu = new();
    private readonly SettingsMenu _settingsMenu = new();
    private readonly PartyRepository _partyRepo = new("Data/Party");
    private readonly CharacterRepository _charRepo = new("Data/Characters");
    private readonly SpellRepository _spellRepo = new("Data/Spells");

    public void Show()
    {
        while (true)
        {
            RestoreDailySpellPointsInTown();

            Console.Clear();
            Console.WriteLine("=== WELCOME TO THE CITY OF MYTHGAR ===\n");
            Console.WriteLine("T)raining Ground");
            Console.WriteLine("G)ilgamash Tavern");
            Console.WriteLine("C)hurch of Chant");
            Console.WriteLine("B)oltac's Trading Post");
            Console.WriteLine("M)aze");
            Console.WriteLine("S)ettings");
            Console.WriteLine("L<-eave");

            var key = Console.ReadKey(true).Key;

            switch (key)
            {
                case ConsoleKey.T:
                    _cityMenu.Show();
                    break;

                case ConsoleKey.G:
                    _partyMenu.Show();
                    break;

                case ConsoleKey.C:
                    if (!HasPartyMembers())
                    {
                        ShowPartyRequiredMessage();
                        break;
                    }

                    _templeMenu.Show();
                    break;

                case ConsoleKey.B:
                    if (!HasPartyMembers())
                    {
                        ShowPartyRequiredMessage();
                        break;
                    }

                    _shopMenu.Show(0,10,true);
                    break;

                case ConsoleKey.M:
                    if (!HasPartyMembers())
                    {
                        ShowPartyRequiredMessage();
                        break;
                    }

                    _dungeonMenu.Show();
                    break;

                case ConsoleKey.S:
                    _settingsMenu.Show();
                    break;

                case ConsoleKey.L:
                    return;
                case ConsoleKey.Enter:
                    return;
            }
        }
    }

    private void RestoreDailySpellPointsInTown()
    {
        var roster = _charRepo.GetAll().ToList();

        foreach (var character in roster)
        {
            var changed = false;

            if (character.HasStatus(CharacterStatus.Invisible))
            {
                character.RemoveStatus(CharacterStatus.Invisible);
                character.ArmorClass += 4;
                changed = true;
            }

            if (character.Spellcasting == null || character.Spellcasting.Count == 0)
            {
                if (changed)
                    _charRepo.Save(character);
                continue;
            }

            foreach (var state in character.Spellcasting)
            {
                if (state.SlotsPerDay.Count == 0)
                    continue;

                while (state.SlotsUsed.Count < state.SlotsPerDay.Count)
                {
                    state.SlotsUsed.Add(0);
                    changed = true;
                }

                for (int i = 0; i < state.SlotsPerDay.Count; i++)
                {
                    if (state.SlotsUsed[i] != 0)
                    {
                        state.SlotsUsed[i] = 0;
                        changed = true;
                    }
                }

                if (EnsureMagicUserHasSleep(state))
                    changed = true;

                if (SyncAutoKnownAndPrepared(character, state))
                    changed = true;
            }

            if (changed)
                _charRepo.Save(character);
        }
    }

    private static bool EnsureMagicUserHasSleep(SpellcastingState state)
    {
        if (state.SpellClass != SpellClass.MagicUser)
            return false;

        if (state.KnownSpellIds.Any(id => string.Equals(id, "sleep", StringComparison.OrdinalIgnoreCase)))
            return false;

        state.KnownSpellIds.Insert(0, "sleep");
        return true;
    }

    private bool SyncAutoKnownAndPrepared(Adnd.Core.Characters.Character character, SpellcastingState state)
    {
        if (!IsAutoMemorizedClass(state.SpellClass))
            return false;

        var classSpells = _spellRepo.LoadByClass(state.SpellClass);
        var maxUnlockedLevel = 0;
        for (int i = state.SlotsPerDay.Count - 1; i >= 0; i--)
        {
            if (state.SlotsPerDay[i] > 0)
            {
                maxUnlockedLevel = i + 1;
                break;
            }
        }

        var shouldKnow = classSpells
            .Where(s => s.Level <= maxUnlockedLevel)
            .Select(s => s.Id)
            .Distinct()
            .ToList();

        var shouldPrepared = shouldKnow
            .Select(id => new PreparedSpell { SpellId = id, Count = 1 })
            .ToList();

        var knownChanged = state.KnownSpellIds.Count != shouldKnow.Count || state.KnownSpellIds.Except(shouldKnow).Any();
        var preparedChanged = state.PreparedSpells.Count != shouldPrepared.Count
                              || state.PreparedSpells.Any(ps => !shouldPrepared.Any(sp => sp.SpellId == ps.SpellId && sp.Count == ps.Count));

        if (!knownChanged && !preparedChanged)
            return false;

        state.KnownSpellIds = shouldKnow;
        state.PreparedSpells = shouldPrepared;
        return true;
    }

    private static bool IsAutoMemorizedClass(SpellClass spellClass)
    {
        if (spellClass is SpellClass.Cleric or SpellClass.Druid)
            return true;

        return GameRulesProvider.Current.AutoMemorizeArcaneSpellsDaily
               && spellClass is SpellClass.MagicUser or SpellClass.Illusionist;
    }

    private bool HasPartyMembers()
    {
        var party = _partyRepo.Load();
        if (party.Members.Count == 0)
            return false;

        var roster = _charRepo.GetAll().ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        return party.Members.Any(name => roster.ContainsKey(name));
    }

    private static void ShowPartyRequiredMessage()
    {
        Console.WriteLine("You need at least one party member first.");
        Console.ReadKey(true);
    }
}
