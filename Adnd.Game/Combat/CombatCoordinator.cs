using System.Text;
using System.Windows.Forms;
using Adnd.Core.Characters;
using Adnd.Core.Characters.Progression;
using Adnd.Core.Combat.Resolution;
using Adnd.Core.Combat.Sessions;
using Adnd.Core.Config;
using Adnd.Core.Items;
using Adnd.Core.Spells.Casting;
using Adnd.Core.Spells.Casting.Handlers;
using Adnd.Core.Treasure;
using Adnd.Data.Characters;
using Adnd.Data.Encounters.Factories;
using Adnd.Data.Items;
using Adnd.Data.Party;
using Adnd.Data.Spells;
using Adnd.Data.Treasure;

namespace Adnd.Game.Combat;

public sealed class CombatCoordinator
{
    private readonly EncounterMonsterFactory _monsterFactory = new();
    private readonly CombatResolver _combatResolver;
    private readonly PartyRepository _partyRepository = new();
    private readonly LevelUpService _levelUpService = new();
    private readonly TreasureService _treasureService;
    private readonly ItemRepository _itemRepository = new("Data/Items");
    private readonly Random _random = new();

    public CombatCoordinator()
    {
        var spellRepo = new SpellRepository("Data/Spells");
        var resolver = new SpellResolver(new ISpellEffectHandler[]
        {
            new CureLightWoundsHandler(),
            new MagicMissileHandler(),
            new BlessHandler(),
            new SleepHandler()
        });

        var spellCastingService = new SpellCastingService(resolver, spellRepo.LoadAll());
        _combatResolver = new CombatResolver(spellCastingService: spellCastingService);

        var treasureRepo = new TreasureTableRepository("Data/Treasure");
        _treasureService = new TreasureService(treasureRepo, _random);
    }

    public CombatOutcome StartEncounter(IWin32Window owner, string monsterName, int monsterCount, List<Character> party, CharacterRepository characterRepository, int? dungeonLevel = null)
    {
        var monsters = _monsterFactory.CreateGroup(monsterName, monsterCount);
        var session = new CombatSession(party, monsters);

        while (session.Outcome == CombatOutcome.InProgress)
        {
            if (!session.AliveParty.Any())
            {
                session.Outcome = CombatOutcome.Defeat;
                break;
            }

            using var encounterForm = new EncounterForm(monsterName, session.AliveMonsters.Count(), session.Party, session.RoundNumber, dungeonLevel);
            var dialogResult = encounterForm.ShowDialog(owner);
            if (dialogResult != DialogResult.OK)
            {
                // If player closes dialog, treat as escape to avoid dead-end.
                session.Outcome = CombatOutcome.Escaped;
                break;
            }

            var roundEvents = _combatResolver.ResolveRound(session, encounterForm.SelectedActions);
            ShowRoundEvents(owner, roundEvents);

            MoveDeadPartyMembersToEnd(session.Party);
        }

        if (session.Outcome == CombatOutcome.Victory)
        {
            ApplyVictoryRewards(owner, session);
        }

        RemoveTemporaryCombatEffects(session);

        foreach (var character in party)
            characterRepository.Save(character);

        MoveDeadPartyMembersToEnd(session.Party);
        ShowFinalOutcome(owner, session.Outcome);
        return session.Outcome;
    }

    private static void RemoveTemporaryCombatEffects(CombatSession session)
    {
        if (session.BlessedPartyMembers.Count == 0)
            return;

        foreach (var name in session.BlessedPartyMembers)
        {
            var c = session.Party.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (c != null)
                c.ArmorClass += 1;
        }

        session.BlessedPartyMembers.Clear();
    }

    private void ApplyVictoryRewards(IWin32Window owner, CombatSession session)
    {
        var survivors = session.Party
            .Where(c => c.CurrentHitPoints > 0 && !c.HasStatus(CharacterStatus.Dead))
            .ToList();

        if (survivors.Count == 0)
            return;

        int totalMonsterXp = session.Monsters.Sum(m => Math.Max(0, m.Template.XPValue));
        int xpEach = totalMonsterXp / survivors.Count;
        int xpRemainder = totalMonsterXp % survivors.Count;
        var xpMultiplier = GameRulesProvider.Current.XpMultiplier;

        var levelUpResults = new List<LevelUpResult>();
        for (int i = 0; i < survivors.Count; i++)
        {
            var baseGain = xpEach + (i < xpRemainder ? 1 : 0);
            var gain = (int)Math.Round(baseGain * xpMultiplier, MidpointRounding.AwayFromZero);
            if (gain < 0)
                gain = 0;

            levelUpResults.Add(_levelUpService.ApplyExperienceAndAutoLevel(survivors[i], gain));
        }

        var totalAwardedXp = levelUpResults.Sum(r => r.ExperienceAfter - r.ExperienceBefore);

        var treasure = _treasureService.RollTreasureForEncounter(session.Monsters);

        DistributeCoin(survivors, treasure.CopperPieces, (c, amount) => c.CopperPieces += amount);
        DistributeCoin(survivors, treasure.SilverPieces, (c, amount) => c.SilverPieces += amount);
        DistributeCoin(survivors, treasure.ElectrumPieces, (c, amount) => c.ElectrumPieces += amount);
        DistributeCoin(survivors, treasure.GoldPieces, (c, amount) => c.GoldPieces += amount);
        DistributeCoin(survivors, treasure.PlatinumPieces, (c, amount) => c.PlatinumPieces += amount);

        var valuablesValueGp = treasure.TotalGemValueGp + treasure.TotalJewelryValueGp + treasure.TotalArtValueGp;
        DistributeCoin(survivors, valuablesValueGp, (c, amount) => c.GoldPieces += amount);

        var magicAward = AwardMagicItemsFromPlaceholders(survivors, treasure.MagicPlaceholders);

        var sb = new StringBuilder();
        sb.AppendLine("Victory Rewards");
        sb.AppendLine();
        sb.AppendLine($"Monsters defeated: {session.Monsters.Count}");
        sb.AppendLine($"Base monster XP: {totalMonsterXp}");
        sb.AppendLine($"XP multiplier: x{xpMultiplier:0.##}");
        sb.AppendLine($"Total awarded XP: {totalAwardedXp}");
        sb.AppendLine($"Survivors: {survivors.Count}");
        sb.AppendLine();
        sb.AppendLine("XP awards:");

        foreach (var r in levelUpResults)
        {
            var gain = r.ExperienceAfter - r.ExperienceBefore;
            sb.AppendLine($"- {r.CharacterName}: +{gain} XP (total {r.ExperienceAfter})");
        }

        sb.AppendLine();
        sb.AppendLine("Treasure found:");
        sb.AppendLine($"- Coins: {treasure.CopperPieces} cp, {treasure.SilverPieces} sp, {treasure.ElectrumPieces} ep, {treasure.GoldPieces} gp, {treasure.PlatinumPieces} pp");

        if (treasure.Gems.Count > 0)
            sb.AppendLine($"- Gems: {treasure.Gems.Count} (total {treasure.TotalGemValueGp} gp)");
        if (treasure.Jewelry.Count > 0)
            sb.AppendLine($"- Jewelry: {treasure.Jewelry.Count} (total {treasure.TotalJewelryValueGp} gp)");
        if (treasure.Art.Count > 0)
            sb.AppendLine($"- Art: {treasure.Art.Count} (total {treasure.TotalArtValueGp} gp)");
        if (valuablesValueGp > 0)
            sb.AppendLine($"- Valuables value distributed as gp: {valuablesValueGp} gp");

        if (magicAward.AssignedItems.Count > 0)
        {
            sb.AppendLine("- Magic items awarded:");
            foreach (var assigned in magicAward.AssignedItems)
                sb.AppendLine($"    {assigned.ReceiverName}: {assigned.ItemName}");
        }

        if (magicAward.UnassignedItems.Count > 0)
        {
            sb.AppendLine("- Unclaimed magic items:");
            foreach (var unassigned in magicAward.UnassignedItems)
                sb.AppendLine($"    {unassigned}");
        }

        var leveled = levelUpResults.Where(x => x.LeveledUp).ToList();
        if (leveled.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Level ups:");

            foreach (var r in leveled)
            {
                sb.AppendLine($"- {r.CharacterName}: L{r.OldLevel} -> L{r.NewLevel} (HP +{r.HitPointsGained})");

                foreach (var change in r.SpellSlotChanges)
                {
                    var oldSlots = change.OldSlots.Count == 0 ? "none" : string.Join(",", change.OldSlots);
                    var newSlots = change.NewSlots.Count == 0 ? "none" : string.Join(",", change.NewSlots);
                    sb.AppendLine($"    {change.SpellClass} slots: [{oldSlots}] -> [{newSlots}]");
                }
            }
        }

        MessageBox.Show(owner, sb.ToString(), "Combat Rewards", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private MagicAwardResult AwardMagicItemsFromPlaceholders(List<Character> survivors, List<TreasureMagicPlaceholderResult> placeholders)
    {
        var result = new MagicAwardResult();
        if (survivors.Count == 0 || placeholders == null || placeholders.Count == 0)
            return result;

        var allItems = _itemRepository.LoadAll().ToList();
        if (allItems.Count == 0)
            return result;

        var nextReceiverIndex = 0;

        foreach (var placeholder in placeholders)
        {
            var pool = GetItemPoolForMagicTable(allItems, placeholder.Table);
            if (pool.Count == 0)
            {
                for (int i = 0; i < Math.Max(1, placeholder.Count); i++)
                    result.UnassignedItems.Add($"{placeholder.Table} (no matching item defined)");
                continue;
            }

            var rolls = Math.Max(0, placeholder.Count);
            for (int i = 0; i < rolls; i++)
            {
                var rolled = pool[_random.Next(pool.Count)];
                var item = CloneItem(rolled);

                var assigned = false;
                for (int attempt = 0; attempt < survivors.Count; attempt++)
                {
                    var idx = (nextReceiverIndex + attempt) % survivors.Count;
                    var receiver = survivors[idx];
                    if (!receiver.CanCarry(item))
                        continue;

                    receiver.Inventory.Add(item);
                    result.AssignedItems.Add(new AssignedMagicItem
                    {
                        ReceiverName = receiver.Name,
                        ItemName = item.Name
                    });

                    nextReceiverIndex = (idx + 1) % survivors.Count;
                    assigned = true;
                    break;
                }

                if (!assigned)
                    result.UnassignedItems.Add(item.Name + " (no one can carry)");
            }
        }

        return result;
    }

    private static List<Item> GetItemPoolForMagicTable(List<Item> allItems, string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            return new List<Item>();

        var key = table.Trim().ToLowerInvariant();
        return key switch
        {
            "potion" => allItems.Where(i => i.Type == ItemType.Potion).ToList(),
            "scroll" => allItems.Where(i => i.Type == ItemType.Scroll).ToList(),
            "weapon" => allItems.Where(i => i.Type == ItemType.Weapon).ToList(),
            "armor" => allItems.Where(i => i.Type == ItemType.Armor || i.Type == ItemType.Shield).ToList(),
            "magicitem" => allItems.Where(i => i.Type == ItemType.MagicItem).ToList(),
            _ => allItems.Where(i => i.Type == ItemType.MagicItem && i.Name.Contains(table, StringComparison.OrdinalIgnoreCase)).ToList()
        };
    }

    private static Item CloneItem(Item source)
    {
        return new Item
        {
            Name = source.Name,
            Type = source.Type,
            Slot = source.Slot,
            Cost = source.Cost,
            Weight = source.Weight,
            ToHitBonus = source.ToHitBonus,
            IsShopBuyable = source.IsShopBuyable,
            StockQuantity = source.StockQuantity,
            ArmorClassBonus = source.ArmorClassBonus,
            Damage = source.Damage,
            AllowedClasses = new List<CharacterClass>(source.AllowedClasses)
        };
    }

    private sealed class MagicAwardResult
    {
        public List<AssignedMagicItem> AssignedItems { get; } = new();
        public List<string> UnassignedItems { get; } = new();
    }

    private sealed class AssignedMagicItem
    {
        public string ReceiverName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
    }

    private void MoveDeadPartyMembersToEnd(List<Character> combatParty)
    {
        // Reorder in-memory combat turn order immediately.
        var alive = combatParty
            .Where(c => c.CurrentHitPoints > 0 && !c.HasStatus(CharacterStatus.Dead))
            .ToList();
        var dead = combatParty
            .Where(c => c.CurrentHitPoints <= 0 || c.HasStatus(CharacterStatus.Dead))
            .ToList();

        combatParty.Clear();
        combatParty.AddRange(alive);
        combatParty.AddRange(dead);

        var partyData = _partyRepository.Load();
        if (partyData.Members.Count == 0)
            return;

        var deadLookup = combatParty
            .ToDictionary(
                c => c.Name,
                c => c.CurrentHitPoints <= 0 || c.HasStatus(CharacterStatus.Dead),
                StringComparer.OrdinalIgnoreCase);

        var aliveNames = new List<string>();
        var unknownNames = new List<string>();
        var deadNames = new List<string>();

        foreach (var memberName in partyData.Members)
        {
            if (!deadLookup.TryGetValue(memberName, out var isDead))
            {
                unknownNames.Add(memberName);
                continue;
            }

            if (isDead)
                deadNames.Add(memberName);
            else
                aliveNames.Add(memberName);
        }

        var reordered = aliveNames
            .Concat(unknownNames)
            .Concat(deadNames)
            .ToList();

        if (!partyData.Members.SequenceEqual(reordered, StringComparer.OrdinalIgnoreCase))
        {
            partyData.Members = reordered;
            _partyRepository.Save(partyData);
        }
    }

    private static void ShowRoundEvents(IWin32Window owner, IEnumerable<Adnd.Core.Combat.Events.CombatEvent> events)
    {
        var sb = new StringBuilder();
        foreach (var e in events)
            sb.AppendLine(e.Message);

        MessageBox.Show(owner, sb.ToString(), "Combat Round", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void ShowFinalOutcome(IWin32Window owner, CombatOutcome outcome)
    {
        var text = outcome switch
        {
            CombatOutcome.Victory => "Victory!",
            CombatOutcome.Defeat => "Defeat...",
            CombatOutcome.Escaped => "The party escaped.",
            _ => "Combat ended."
        };

        MessageBox.Show(owner, text, "Combat Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void DistributeCoin(List<Character> survivors, int totalAmount, Action<Character, int> add)
    {
        if (totalAmount <= 0 || survivors.Count == 0)
            return;

        int each = totalAmount / survivors.Count;
        int remainder = totalAmount % survivors.Count;

        for (int i = 0; i < survivors.Count; i++)
        {
            add(survivors[i], each + (i < remainder ? 1 : 0));
        }
    }
}
