using System.Drawing;
using System.Windows.Forms;
using Adnd.Core.Characters;
using Adnd.Core.Config;
using Adnd.Core.Items;
using Adnd.Core.Spells;
using Adnd.Core.Spells.Casting;
using Adnd.Core.Spells.Casting.Handlers;
using Adnd.Data.Characters;
using Adnd.Data.Party;
using Adnd.Data.Spells;

namespace Adnd.Game;

public sealed class CampCharacterInspectForm : Form
{
    private readonly string _characterName;
    private readonly List<string> _partyMembers;
    private readonly CharacterRepository _characterRepository = new("Data/Characters");
    private readonly PartyRepository _partyRepository = new("Data/Party");
    private readonly SpellRepository _spellRepository = new("Data/Spells");
    private readonly SpellCastingService _spellCastingService;

    private readonly TextBox _detailsBox;

    public CampCharacterInspectForm(string characterName, List<string> partyMembers)
    {
        _characterName = characterName;
        _partyMembers = partyMembers;

        var resolver = new SpellResolver(new ISpellEffectHandler[]
        {
            new CureLightWoundsHandler(),
            new MagicMissileHandler(),
            new BlessHandler(),
            new SleepHandler()
        });
        _spellCastingService = new SpellCastingService(resolver, _spellRepository.LoadAll());

        Text = $"Inspect - {characterName}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1020, 700);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _detailsBox = new TextBox
        {
            Left = 12,
            Top = 12,
            Width = 996,
            Height = 560,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10f),
            BackColor = Color.Black,
            ForeColor = Color.White
        };

        var buttons = new FlowLayoutPanel
        {
            Left = 12,
            Top = 584,
            Width = 996,
            Height = 100,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        buttons.Controls.Add(MakeButton("Read", (_, _) => NotImplemented("Read")));
        buttons.Controls.Add(MakeButton("Equip", (_, _) => EquipAction()));
        buttons.Controls.Add(MakeButton("Unequip", (_, _) => UnequipAction()));
        buttons.Controls.Add(MakeButton("Trade", (_, _) => TradeAction()));
        buttons.Controls.Add(MakeButton("Drop", (_, _) => DropAction()));
        buttons.Controls.Add(MakeButton("Pool Gold", (_, _) => PoolGoldAction()));
        buttons.Controls.Add(MakeButton("Identify", (_, _) => NotImplemented("Identify")));
        buttons.Controls.Add(MakeButton("Memorize", (_, _) => MemorizeSpellAction()));
        buttons.Controls.Add(MakeButton("Cast Spell", (_, _) => CastSpellAction()));
        buttons.Controls.Add(MakeButton("Refresh", (_, _) => RefreshView()));
        buttons.Controls.Add(MakeButton("Close", (_, _) => Close()));

        Controls.Add(_detailsBox);
        Controls.Add(buttons);

        RefreshView();
    }

    private Button MakeButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, Width = 92, Height = 32 };
        button.Click += onClick;
        return button;
    }

    private Character? GetCharacter() => _characterRepository.GetAll().FirstOrDefault(c => string.Equals(c.Name, _characterName, StringComparison.OrdinalIgnoreCase));

    private List<Character> GetPartyCharacters()
    {
        var roster = _characterRepository.GetAll().ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        return _partyMembers.Where(roster.ContainsKey).Select(name => roster[name]).ToList();
    }

    private void RefreshView()
    {
        var c = GetCharacter();
        if (c == null)
        {
            _detailsBox.Text = "Character no longer exists.";
            return;
        }

        var lines = new List<string>
        {
            c.ToString(),
            string.Empty,
            "=== EQUIPPED ITEMS ==="
        };

        foreach (var kv in c.Equipment)
            lines.Add(kv.Value == null ? $" - {kv.Key}: (empty)" : $" - {kv.Key}: {kv.Value.Name}");

        lines.Add(string.Empty);
        lines.Add("=== INVENTORY ===");
        if (c.Inventory.Count == 0)
            lines.Add(" (empty)");
        else
            for (int i = 0; i < c.Inventory.Count; i++)
                lines.Add($"{i + 1}. {c.Inventory[i].Name}");

        lines.Add(string.Empty);
        lines.Add("=== SPELLCASTING ===");

        if (c.Spellcasting == null || c.Spellcasting.Count == 0)
        {
            lines.Add(" (no spellcastings)");
        }
        else
        {
            foreach (var state in c.Spellcasting)
            {
                SyncAutoKnownSpells(c, state);
                var all = _spellRepository.LoadByClass(state.SpellClass);
                var known = all.Where(s => state.KnownSpellIds.Contains(s.Id)).ToList();
                lines.Add($" - {state.SpellClass}: known {known.Count}, prepared {state.PreparedSpells.Sum(ps => ps.Count)}");

                for (int lvl = 0; lvl < state.SlotsPerDay.Count; lvl++)
                {
                    var max = state.SlotsPerDay[lvl];
                    if (max <= 0)
                        continue;
                    var used = lvl < state.SlotsUsed.Count ? state.SlotsUsed[lvl] : 0;
                    lines.Add($"   L{lvl + 1} slots: {Math.Max(0, max - used)}/{max}");
                }

                if (known.Count > 0)
                    lines.Add("   Known: " + string.Join(", ", known.Select(s => $"L{s.Level} {s.Name}")));
            }
        }

        _detailsBox.Text = string.Join(Environment.NewLine, lines);
    }

    private void EquipAction()
    {
        var c = GetCharacter();
        if (c == null)
            return;

        var equipable = c.Inventory
            .Where(it => it.Type == ItemType.Weapon || it.Type == ItemType.Shield || it.Slot.HasValue)
            .Where(it => it.AllowedClasses.Count == 0 || c.Classes.Any(cls => it.AllowedClasses.Contains(cls)))
            .ToList();

        if (equipable.Count == 0)
        {
            MessageBox.Show(this, "No equipable items in inventory.", "Equip", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var idx = PromptChoice("Equip Item", equipable.Select(i => $"{i.Name} [{GetDisplaySlot(i)}]").ToList());
        if (!idx.HasValue)
            return;

        var item = equipable[idx.Value];

        EquipmentSlot? targetSlot = null;
        if (item.Type == ItemType.Weapon)
        {
            var slotChoice = PromptChoice("Equip Weapon To", new List<string> { "Main Hand", "Off Hand" });
            if (!slotChoice.HasValue)
                return;

            targetSlot = slotChoice.Value == 0 ? EquipmentSlot.MainHand : EquipmentSlot.OffHand;
        }
        else if (item.Type == ItemType.Shield)
        {
            targetSlot = EquipmentSlot.OffHand;
        }
        else if (item.Slot.HasValue)
        {
            targetSlot = item.Slot.Value;
        }

        if (!targetSlot.HasValue)
            return;

        var originalSlot = item.Slot;
        item.Slot = targetSlot.Value;

        var ok = EquipmentManager.Equip(c, item);

        item.Slot = originalSlot;

        if (ok)
        {
            _characterRepository.Save(c);
            RefreshView();
        }
        else
        {
            MessageBox.Show(this, $"{c.Name} cannot equip {item.Name}.", "Equip", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static string GetDisplaySlot(Item item)
    {
        if (item.Type == ItemType.Weapon)
            return "MainHand/OffHand";

        if (item.Type == ItemType.Shield)
            return "OffHand";

        return item.Slot?.ToString() ?? "-";
    }

    private void UnequipAction()
    {
        var c = GetCharacter();
        if (c == null)
            return;

        var equipped = c.Equipment.Where(kv => kv.Value != null).Select(kv => kv.Key).ToList();
        if (equipped.Count == 0)
        {
            MessageBox.Show(this, "No equipped items.", "Unequip", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var idx = PromptChoice("Unequip", equipped.Select(s => s.ToString()).ToList());
        if (!idx.HasValue)
            return;

        if (EquipmentManager.Unequip(c, equipped[idx.Value]))
        {
            _characterRepository.Save(c);
            RefreshView();
        }
    }

    private void DropAction()
    {
        var c = GetCharacter();
        if (c == null)
            return;

        if (c.Inventory.Count == 0)
        {
            MessageBox.Show(this, "Inventory empty.", "Drop", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var idx = PromptChoice("Drop Item", c.Inventory.Select(i => i.Name).ToList());
        if (!idx.HasValue)
            return;

        c.Inventory.RemoveAt(idx.Value);
        _characterRepository.Save(c);
        RefreshView();
    }

    private void PoolGoldAction()
    {
        var members = GetPartyCharacters();
        var receiver = members.FirstOrDefault(m => string.Equals(m.Name, _characterName, StringComparison.OrdinalIgnoreCase));
        if (receiver == null)
            return;

        var pooled = 0;
        foreach (var member in members)
        {
            if (string.Equals(member.Name, receiver.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            pooled += member.GoldPieces;
            member.GoldPieces = 0;
            _characterRepository.Save(member);
        }

        receiver.GoldPieces += pooled;
        _characterRepository.Save(receiver);
        RefreshView();
        MessageBox.Show(this, $"Pooled {pooled} gp to {receiver.Name}.", "Pool Gold", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void MemorizeSpellAction()
    {
        var c = GetCharacter();
        if (c == null)
            return;

        if (!CanUseMemorizeAction(c))
        {
            MessageBox.Show(this, "This character cannot memorize spells.", "Memorize", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var states = c.Spellcasting;
        if (states == null || states.Count == 0)
            return;

        var stateIdx = states.Count == 1 ? 0 : PromptChoice("Spellcasting Type", states.Select(s => s.SpellClass.ToString()).ToList());
        if (!stateIdx.HasValue)
            return;

        var state = states[stateIdx.Value];
        SyncAutoKnownSpells(c, state);

        var classSpells = _spellRepository.LoadByClass(state.SpellClass);
        var knownSpells = classSpells
            .Where(s => state.KnownSpellIds.Contains(s.Id))
            .OrderBy(s => s.Level)
            .ThenBy(s => s.Name)
            .ToList();

        if (knownSpells.Count == 0)
            return;

        var spellIdx = PromptChoice("Memorize Spell", knownSpells.Select(s => $"L{s.Level} {s.Name}").ToList());
        if (!spellIdx.HasValue)
            return;

        var chosen = knownSpells[spellIdx.Value];
        var levelIndex = chosen.Level - 1;
        if (levelIndex < 0 || levelIndex >= state.SlotsPerDay.Count || state.SlotsPerDay[levelIndex] <= 0)
            return;

        var preparedForLevel = state.PreparedSpells
            .Join(classSpells, p => p.SpellId, sp => sp.Id, (p, sp) => new { p.Count, sp.Level })
            .Where(x => x.Level == chosen.Level)
            .Sum(x => x.Count);

        if (preparedForLevel >= state.SlotsPerDay[levelIndex])
            return;

        var prepared = state.PreparedSpells.FirstOrDefault(ps => ps.SpellId == chosen.Id);
        if (prepared == null)
            state.PreparedSpells.Add(new PreparedSpell { SpellId = chosen.Id, Count = 1 });
        else
            prepared.Count += 1;

        _characterRepository.Save(c);
        RefreshView();
    }

    private void CastSpellAction()
    {
        var c = GetCharacter();
        if (c == null)
            return;

        var states = c.Spellcasting;
        if (states == null || states.Count == 0)
            return;

        var stateIdx = states.Count == 1 ? 0 : PromptChoice("Spellcasting Type", states.Select(s => s.SpellClass.ToString()).ToList());
        if (!stateIdx.HasValue)
            return;

        var state = states[stateIdx.Value];
        SyncAutoKnownSpells(c, state);

        var allForClass = _spellRepository.LoadByClass(state.SpellClass)
            .Where(s => s.CastContext is SpellCastContext.Both or SpellCastContext.Exploration)
            .ToList();

        bool isAutoMemorizedClass = IsAutoMemorizedClass(state.SpellClass);
        var castable = allForClass
            .Where(spell =>
            {
                var levelIdx = spell.Level - 1;
                if (levelIdx < 0 || levelIdx >= state.SlotsPerDay.Count)
                    return false;
                if (state.SlotsPerDay[levelIdx] <= 0)
                    return false;
                var used = levelIdx < state.SlotsUsed.Count ? state.SlotsUsed[levelIdx] : 0;
                if (used >= state.SlotsPerDay[levelIdx])
                    return false;
                if (isAutoMemorizedClass)
                    return true;
                var knows = state.KnownSpellIds.Contains(spell.Id);
                var prepared = state.PreparedSpells.Any(ps => ps.SpellId == spell.Id && ps.Count > 0);
                return knows && prepared;
            })
            .OrderBy(s => s.Level)
            .ThenBy(s => s.Name)
            .ToList();

        if (castable.Count == 0)
            return;

        var spellIdx = PromptChoice("Cast Spell", castable.Select(s => $"L{s.Level} {s.Name}").ToList());
        if (!spellIdx.HasValue)
            return;

        var spell = castable[spellIdx.Value];
        var partyMembers = GetPartyCharacters();
        if (partyMembers.Count == 0)
            return;

        var caster = partyMembers.FirstOrDefault(x => string.Equals(x.Name, c.Name, StringComparison.OrdinalIgnoreCase)) ?? c;
        var targets = new List<SpellCastTarget>();

        if (spell.RangeType == SpellRangeType.Self)
        {
            targets.Add(SpellCastTarget.Ally(caster));
        }
        else if (spell.RangeType == SpellRangeType.Ally)
        {
            var targetIdx = PromptChoice("Choose Ally Target", partyMembers.Select(p => $"{p.Name} (HP {p.CurrentHitPoints}/{p.MaxHitPoints})").ToList());
            if (!targetIdx.HasValue)
                return;

            targets.Add(SpellCastTarget.Ally(partyMembers[targetIdx.Value]));
        }
        else
        {
            MessageBox.Show(this, "Enemy-target spells require combat.", "Cast Spell", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = _spellCastingService.Cast(new SpellCastRequest
        {
            Caster = caster,
            SpellId = spell.Id,
            Context = SpellUseContext.Exploration,
            Targets = targets,
            PartyTargets = partyMembers,
            MonsterTargets = new List<Adnd.Core.Combat.Sessions.MonsterInstance>()
        });

        MessageBox.Show(this, string.Join(Environment.NewLine, result.Events), "Cast Spell", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (result.Success)
        {
            foreach (var member in partyMembers)
                _characterRepository.Save(member);
            _characterRepository.Save(caster);
            RefreshView();
        }
    }

    private void SyncAutoKnownSpells(Character c, SpellcastingState state)
    {
        if (!IsAutoMemorizedClass(state.SpellClass))
            return;

        var classSpells = _spellRepository.LoadByClass(state.SpellClass);
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

        if (knownChanged || preparedChanged)
        {
            state.KnownSpellIds = shouldKnow;
            state.PreparedSpells = shouldPrepared;
            _characterRepository.Save(c);
        }
    }

    private static bool IsAutoMemorizedClass(SpellClass spellClass)
    {
        if (spellClass is SpellClass.Cleric or SpellClass.Druid)
            return true;

        return GameRulesProvider.Current.AutoMemorizeArcaneSpellsDaily
               && spellClass is SpellClass.MagicUser or SpellClass.Illusionist;
    }

    private static bool CanUseMemorizeAction(Character c)
    {
        return c.Classes.Any(cls => cls == CharacterClass.MagicUser
                                    || cls == CharacterClass.Illusionist
                                    || cls == CharacterClass.Ranger);
    }

    private static void NotImplemented(string actionName)
    {
        MessageBox.Show($"[{actionName} action not yet implemented]", actionName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private int? PromptChoice(string title, List<string> options)
    {
        using var form = new Form();
        form.Text = title;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterParent;
        form.ClientSize = new Size(560, 420);
        form.MinimizeBox = false;
        form.MaximizeBox = false;

        var list = new ListBox
        {
            Left = 12,
            Top = 12,
            Width = 536,
            Height = 330,
            Font = new Font("Consolas", 10f)
        };

        foreach (var option in options)
            list.Items.Add(option);

        var ok = new Button { Text = "OK", Left = 392, Top = 354, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 473, Top = 354, Width = 75, DialogResult = DialogResult.Cancel };

        form.Controls.Add(list);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        if (form.ShowDialog(this) != DialogResult.OK)
            return null;

        return list.SelectedIndex >= 0 ? list.SelectedIndex : null;
    }

    private void TradeAction()
    {
        var giver = GetCharacter();
        if (giver == null)
            return;

        if (giver.Inventory.Count == 0)
        {
            MessageBox.Show(this, "No items to trade.", "Trade", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var members = GetPartyCharacters();
        var recipients = members
            .Where(m => !string.Equals(m.Name, giver.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (recipients.Count == 0)
        {
            MessageBox.Show(this, "No other party member to trade with.", "Trade", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var recipientIdx = PromptChoice("Trade With", recipients.Select(r => $"{r.Name} ({r.Class})").ToList());
        if (!recipientIdx.HasValue)
            return;

        var receiver = recipients[recipientIdx.Value];

        var itemIdx = PromptChoice("Choose Item", giver.Inventory.Select(i => $"{i.Name} (Wt {i.Weight})").ToList());
        if (!itemIdx.HasValue)
            return;

        var item = giver.Inventory[itemIdx.Value];

        if (!receiver.CanCarry(item))
        {
            MessageBox.Show(this,
                $"{receiver.Name} cannot carry more weight ({receiver.CurrentCarryWeight}/{receiver.MaxCarryWeight}).",
                "Trade",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        giver.Inventory.Remove(item);
        receiver.Inventory.Add(item);

        _characterRepository.Save(giver);
        _characterRepository.Save(receiver);

        RefreshView();

        MessageBox.Show(this,
            $"Traded {item.Name} from {giver.Name} to {receiver.Name}.",
            "Trade",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
