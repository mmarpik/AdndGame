using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Adnd.Core.Characters;
using Adnd.Core.Combat.Actions;
using Adnd.Core.Combat.Sessions;
using Adnd.Core.Config;
using Adnd.Core.Monsters;
using Adnd.Core.Spells;
using Adnd.Core.Spells.Casting;
using Adnd.Data.Spells;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using ImageSharpRgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;
using ImageSharpPngEncoder = SixLabors.ImageSharp.Formats.Png.PngEncoder;

namespace Adnd.Game;

public sealed class EncounterForm : Form
{
    private readonly string _monsterName;
    private readonly int _monsterCount;
    private readonly int _asleepMonsterCount;
    private readonly int _roundNumber;
    private readonly List<Character> _party;
    private readonly CombatSession? _session;
    private readonly bool _multipleGroups;
    private readonly Dictionary<string, CombatAction> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Image? _monsterImage;
    private readonly List<(string Name, Image? Image)> _monsterImages = new();
    private readonly SpellRepository _spellRepo = new("Data/Spells");

    public IReadOnlyDictionary<string, CombatAction> SelectedActions => _actions;

    private int _currentIndex;

    private readonly Label _headerLabel;
    private readonly Label _optionsTitleLabel;
    private readonly Label _optionsLegendLabel;
    private readonly Panel _monsterPanel;
    private readonly ListView _partyList;

    // Constructor for single group encounters (backward compatibility)
    public EncounterForm(string monsterName, int monsterCount, int asleepMonsterCount, List<Character> party, int roundNumber, int? dungeonLevel = null, Monster? monsterTemplate = null)
    {
        _monsterName = monsterName;
        _monsterCount = monsterCount;
        _asleepMonsterCount = asleepMonsterCount;
        _roundNumber = roundNumber;
        _party = party;

        // Check if we should use Wizardry suffix
        bool useWizSuffix = monsterTemplate != null && ShouldUseWizardrySuffix(monsterTemplate);
        _monsterImage = TryLoadMonsterImage(monsterName, dungeonLevel, useWizSuffix);

        Text = "Encounter";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(980, 620);
        BackColor = Color.Black;
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;

        _headerLabel = new Label
        {
            Left = 16,
            Top = 12,
            Width = 940,
            Height = 40,
            ForeColor = Color.White,
            BackColor = Color.Black,
            Font = new Font("Consolas", 18f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _optionsTitleLabel = new Label
        {
            Left = 16,
            Top = 64,
            Width = 940,
            Height = 40,
            ForeColor = Color.White,
            BackColor = Color.Black,
            Font = new Font("Consolas", 18f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _optionsLegendLabel = new Label
        {
            Left = 16,
            Top = 108,
            Width = 940,
            Height = 56,
            ForeColor = Color.White,
            BackColor = Color.Black,
            Font = new Font("Consolas", 16f, FontStyle.Bold),
            Text = "F)IGHT   U)SE ITEM   R)UN\nS)PELL   P)ARRY      T)AKE BACK",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _monsterPanel = new Panel
        {
            Left = 16,
            Top = 172,
            Width = 940,
            Height = 180,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle
        };
        _monsterPanel.Paint += MonsterPanel_Paint;

        _partyList = new ListView
        {
            Left = 16,
            Top = 362,
            Width = 940,
            Height = 236,
            BackColor = Color.Black,
            ForeColor = Color.White,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Consolas", 14f, FontStyle.Bold)
        };

        _partyList.Columns.Add("#", 50);
        _partyList.Columns.Add("Character Name", 220);
        _partyList.Columns.Add("Class", 140);
        _partyList.Columns.Add("AC", 80);
        _partyList.Columns.Add("Hits", 100);
        _partyList.Columns.Add("Status", 280);

        Controls.Add(_headerLabel);
        Controls.Add(_optionsTitleLabel);
        Controls.Add(_optionsLegendLabel);
        Controls.Add(_monsterPanel);
        Controls.Add(_partyList);

        KeyDown += EncounterForm_KeyDown;

        _currentIndex = FindNextActionableIndex(-1);
        UpdateHeader();
        UpdatePartyList();
    }

    // Constructor for multiple group encounters
    public EncounterForm(CombatSession session, int? dungeonLevel = null)
    {
        _session = session;
        _multipleGroups = true;
        _party = session.Party;
        _roundNumber = session.RoundNumber;

        var groups = session.GetDistinctGroupIds().ToList();
        var groupDescriptions = groups.Select(groupId =>
        {
            var monstersInGroup = session.GetAliveMonstersByGroup(groupId).ToList();
            if (monstersInGroup.Count == 0)
                return null;
            var name = monstersInGroup.First().Name;
            var count = monstersInGroup.Count;
            var asleepCount = monstersInGroup.Count(m => m.HasStatus(MonsterStatus.Asleep));

            // Check if we should use Wizardry suffix
            bool useWizSuffix = ShouldUseWizardrySuffix(monstersInGroup.First().Template);

            // Load image for this monster type
            var image = TryLoadMonsterImage(name, dungeonLevel, useWizSuffix);
            _monsterImages.Add((name, image));

            return $"{count} {name}" + (asleepCount > 0 ? $" ({asleepCount} asleep)" : "");
        }).Where(d => d != null).ToList();

        _monsterName = string.Join(" and ", groupDescriptions);
        _monsterCount = session.AliveMonsters.Count();
        _asleepMonsterCount = session.AliveMonsters.Count(m => m.HasStatus(MonsterStatus.Asleep));

        Text = "Encounter";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(980, 620);
        BackColor = Color.Black;
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;

        _headerLabel = new Label
        {
            Left = 16,
            Top = 12,
            Width = 940,
            Height = 40,
            ForeColor = Color.White,
            BackColor = Color.Black,
            Font = new Font("Consolas", 18f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _optionsTitleLabel = new Label
        {
            Left = 16,
            Top = 64,
            Width = 940,
            Height = 40,
            ForeColor = Color.White,
            BackColor = Color.Black,
            Font = new Font("Consolas", 18f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _optionsLegendLabel = new Label
        {
            Left = 16,
            Top = 108,
            Width = 940,
            Height = 56,
            ForeColor = Color.White,
            BackColor = Color.Black,
            Font = new Font("Consolas", 16f, FontStyle.Bold),
            Text = "F)IGHT   U)SE ITEM   R)UN\nS)PELL   P)ARRY      T)AKE BACK\nG)ROUP   (Select Target Group)",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _monsterPanel = new Panel
        {
            Left = 16,
            Top = 172,
            Width = 940,
            Height = 180,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle
        };
        _monsterPanel.Paint += MonsterPanel_Paint;

        _partyList = new ListView
        {
            Left = 16,
            Top = 362,
            Width = 940,
            Height = 236,
            BackColor = Color.Black,
            ForeColor = Color.White,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Consolas", 14f, FontStyle.Bold)
        };

        _partyList.Columns.Add("#", 50);
        _partyList.Columns.Add("Character Name", 220);
        _partyList.Columns.Add("Class", 140);
        _partyList.Columns.Add("AC", 80);
        _partyList.Columns.Add("Hits", 100);
        _partyList.Columns.Add("Status", 280);

        Controls.Add(_headerLabel);
        Controls.Add(_optionsTitleLabel);
        Controls.Add(_optionsLegendLabel);
        Controls.Add(_monsterPanel);
        Controls.Add(_partyList);

        KeyDown += EncounterForm_KeyDown;

        _currentIndex = FindNextActionableIndex(-1);
        UpdateHeader();
        UpdatePartyList();
    }

    private void EncounterForm_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Escape:
                DialogResult = DialogResult.Cancel;
                Close();
                break;
            case Keys.Enter:
            {
                // Enter means Fight for chars 1-3, Parry for chars 4-6
                var rank = GetActionableRank(_currentIndex);
                if (rank is >= 1 and <= 3)
                {
                    ChooseAction(CombatActionType.Fight);
                }
                else if (rank is >= 4 and <= 6)
                {
                    ChooseAction(CombatActionType.Parry);
                }
                break;
            }
            case Keys.F:
            {
                var rank = GetActionableRank(_currentIndex);
                if (rank is >= 1 and <= 3)
                    ChooseAction(CombatActionType.Fight);
                else
                    MessageBox.Show(this, "Only the first three living characters may choose Fight.", "Action not allowed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
            }
            case Keys.P:
                ChooseAction(CombatActionType.Parry);
                break;
            case Keys.T:
                StepBack();
                break;
            case Keys.U:
                ChooseAction(CombatActionType.UseItem);
                break;
            case Keys.R:
                ChooseAction(CombatActionType.Run);
                break;
            case Keys.S:
                ChooseSpellAction();
                break;
            case Keys.G:
                if (_multipleGroups && _session != null)
                    ChooseTargetGroup();
                break;
        }
    }

    private void ChooseAction(CombatActionType action)
    {
        if (_currentIndex < 0 || _currentIndex >= _party.Count)
            return;

        if (!IsActionable(_party[_currentIndex]))
        {
            _currentIndex = FindNextActionableIndex(_currentIndex);
            if (_currentIndex < 0)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            UpdateHeader();
            UpdatePartyList();
            return;
        }

        var character = _party[_currentIndex];
        var combatAction = CombatAction.OfType(action);

        // If Fight action and multiple groups exist, prompt for target group
        if (action == CombatActionType.Fight && _multipleGroups && _session != null)
        {
            var groups = _session.GetDistinctGroupIds()
                .Where(g => _session.GetAliveCountByGroup(g) > 0)
                .ToList();

            if (groups.Count > 1)
            {
                var targetGroupId = PromptGroupSelection(character);
                if (targetGroupId == null)
                    return; // User cancelled

                combatAction.TargetGroupId = targetGroupId;
            }
            else if (groups.Count == 1)
            {
                // Only one group, automatically target it
                combatAction.TargetGroupId = groups[0];
            }
        }

        _actions[character.Name] = combatAction;
        AdvanceActor();
    }

    private void ChooseSpellAction()
    {
        if (_currentIndex < 0 || _currentIndex >= _party.Count)
            return;

        var caster = _party[_currentIndex];
        if (!IsActionable(caster))
            return;

        var castable = GetCastableCombatSpells(caster);
        if (castable.Count == 0)
        {
            MessageBox.Show(this, $"{caster.Name} has no castable spells.", "Spell", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var spell = castable.Count == 1 ? castable[0] : PromptSpellSelection(caster, castable);
        if (spell == null)
            return;

        SpellCastTarget? target;
        if (spell.RangeType == SpellRangeType.Self)
        {
            target = SpellCastTarget.Ally(caster);
        }
        else if (spell.RangeType == SpellRangeType.Ally)
        {
            var ally = PromptAllyTarget();
            if (ally == null)
                return;
            target = SpellCastTarget.Ally(ally);
        }
        else // Enemy spells
        {
            // Determine targeting based on TargetingScope
            if (spell.TargetingScope == SpellTargetingScope.SingleTarget)
            {
                // SingleTarget: pick a random target within a group
                // If multiple groups exist, ask which group to target
                if (_multipleGroups && _session != null)
                {
                    var targetGroupId = PromptGroupSelection(caster);
                    if (targetGroupId == null)
                        return;
                    target = SpellCastTarget.EnemyGroup(targetGroupId);
                }
                else
                {
                    // Single group or no session - use default targeting (random will be picked by handler)
                    target = SpellCastTarget.EnemyGroup("default");
                }
            }
            else if (spell.TargetingScope == SpellTargetingScope.SingleGroup)
            {
                // SingleGroup: affects all monsters in one group
                // If multiple groups exist, ask which group to target
                if (_multipleGroups && _session != null)
                {
                    var groups = _session.GetDistinctGroupIds()
                        .Where(g => _session.GetAliveCountByGroup(g) > 0)
                        .ToList();

                    if (groups.Count > 1)
                    {
                        var targetGroupId = PromptGroupSelection(caster);
                        if (targetGroupId == null)
                            return;
                        target = SpellCastTarget.EnemyGroup(targetGroupId);
                    }
                    else
                    {
                        // Only one group left
                        target = SpellCastTarget.EnemyGroup(groups.FirstOrDefault() ?? "default");
                    }
                }
                else
                {
                    // Single group encounter
                    target = SpellCastTarget.EnemyGroup("default");
                }
            }
            else // AllGroups
            {
                // AllGroups: affects all monsters in all groups, no targeting needed
                target = null;
            }
        }

        _actions[caster.Name] = new CombatAction
        {
            Type = CombatActionType.CastSpell,
            SpellId = spell.Id,
            Target = target
        };

        AdvanceActor();
    }

    private Spell? PromptSpellSelection(Character caster, List<Spell> castable)
    {
        var spellLines = string.Join(Environment.NewLine, castable.Select((s, i) => $"{i + 1}. L{s.Level} {s.Name}"));
        var selected = PromptForNumber(
            "Choose Spell",
            $"{caster.Name} - choose spell to cast:{Environment.NewLine}{Environment.NewLine}{spellLines}",
            1,
            castable.Count);

        if (!selected.HasValue)
            return null;

        return castable[selected.Value - 1];
    }

    private Character? PromptAllyTarget()
    {
        var allies = _party.Where(IsActionable).ToList();
        if (allies.Count == 0)
            return null;

        var allyLines = string.Join(Environment.NewLine, allies.Select((a, i) => $"{i + 1}. {a.Name} (HP {a.CurrentHitPoints}/{a.MaxHitPoints})"));
        var selected = PromptForNumber("Choose Ally Target", allyLines, 1, allies.Count);
        return selected.HasValue ? allies[selected.Value - 1] : null;
    }

    private int? PromptEnemyTarget()
    {
        var enemyLines = string.Join(Environment.NewLine, Enumerable.Range(1, _monsterCount).Select(i => $"{i}. {_monsterName} #{i}"));
        return PromptForNumber("Choose Enemy Target", enemyLines, 1, _monsterCount);
    }

    private string? PromptGroupSelection(Character caster)
    {
        if (_session == null)
            return "default";

        var groups = _session.GetDistinctGroupIds()
            .Where(groupId => _session.GetAliveCountByGroup(groupId) > 0)
            .ToList();

        if (groups.Count <= 1)
            return groups.FirstOrDefault() ?? "default";

        var groupDescriptions = new List<string>();
        for (int i = 0; i < groups.Count; i++)
        {
            var groupId = groups[i];
            var monstersInGroup = _session.GetAliveMonstersByGroup(groupId).ToList();
            var name = monstersInGroup.First().Name;
            var count = monstersInGroup.Count;
            var asleepCount = monstersInGroup.Count(m => m.HasStatus(MonsterStatus.Asleep));
            groupDescriptions.Add($"{i + 1}. {groupId}: {count} {name}" + (asleepCount > 0 ? $" ({asleepCount} asleep)" : ""));
        }

        var groupLines = string.Join(Environment.NewLine, groupDescriptions);
        var selected = PromptForNumber("Select Target Group", $"Which group should {caster.Name} target?\n\n{groupLines}", 1, groups.Count);

        if (!selected.HasValue)
            return null;

        return groups[selected.Value - 1];
    }

    private void ChooseTargetGroup()
    {
        if (_session == null || _currentIndex < 0 || _currentIndex >= _party.Count)
            return;

        var character = _party[_currentIndex];
        if (!IsActionable(character))
            return;

        var groups = _session.GetDistinctGroupIds()
            .Where(groupId => _session.GetAliveCountByGroup(groupId) > 0)
            .ToList();

        if (groups.Count <= 1)
        {
            MessageBox.Show(this, "Only one group remains.", "Group Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var groupDescriptions = new List<string>();
        for (int i = 0; i < groups.Count; i++)
        {
            var groupId = groups[i];
            var monstersInGroup = _session.GetAliveMonstersByGroup(groupId).ToList();
            var name = monstersInGroup.First().Name;
            var count = monstersInGroup.Count;
            var asleepCount = monstersInGroup.Count(m => m.HasStatus(MonsterStatus.Asleep));
            groupDescriptions.Add($"{i + 1}. {groupId}: {count} {name}" + (asleepCount > 0 ? $" ({asleepCount} asleep)" : ""));
        }

        var groupLines = string.Join(Environment.NewLine, groupDescriptions);
        var selected = PromptForNumber("Select Target Group", $"Which group should {character.Name} target?\n\n{groupLines}", 1, groups.Count);

        if (selected.HasValue)
        {
            var selectedGroupId = groups[selected.Value - 1];
            MessageBox.Show(this, $"{character.Name} will target {selectedGroupId}", "Group Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // Store group preference (for now, just show message - actual targeting handled in combat resolver)
        }
    }

    private List<Spell> GetCastableCombatSpells(Character caster)
    {
        var allSpells = _spellRepo.LoadAll();
        var result = new List<Spell>();

        foreach (var state in caster.Spellcasting)
        {
            var byClass = allSpells
                .Where(s => s.SpellClass == state.SpellClass && (s.CastContext == SpellCastContext.Both || s.CastContext == SpellCastContext.Combat))
                .Where(s => _roundNumber == 1 || !string.Equals(s.Id, "bless", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var spell in byClass)
            {
                var levelIndex = spell.Level - 1;
                if (levelIndex < 0 || levelIndex >= state.SlotsPerDay.Count)
                    continue;

                if (state.SlotsPerDay[levelIndex] <= 0)
                    continue;

                var used = levelIndex < state.SlotsUsed.Count ? state.SlotsUsed[levelIndex] : 0;
                if (used >= state.SlotsPerDay[levelIndex])
                    continue;

                var isDivine = state.SpellClass is SpellClass.Cleric or SpellClass.Druid;
                if (!isDivine)
                {
                    if (!state.KnownSpellIds.Contains(spell.Id, StringComparer.OrdinalIgnoreCase))
                        continue;
                }

                result.Add(spell);
            }
        }

        return result
            .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(s => s.Level)
            .ThenBy(s => s.Name)
            .ToList();
    }

    private int? PromptForNumber(string title, string prompt, int min, int max)
    {
        using var form = new Form();
        form.Text = title;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterParent;
        form.ClientSize = new Size(520, 340);
        form.MinimizeBox = false;
        form.MaximizeBox = false;

        var label = new Label
        {
            Left = 12,
            Top = 12,
            Width = 496,
            Height = 250,
            AutoSize = false,
            Text = prompt
        };

        var input = new TextBox
        {
            Left = 12,
            Top = 270,
            Width = 320
        };

        var ok = new Button { Text = "OK", Left = 352, Width = 75, Top = 268, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 433, Width = 75, Top = 268, DialogResult = DialogResult.Cancel };

        form.Controls.Add(label);
        form.Controls.Add(input);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        if (form.ShowDialog(this) != DialogResult.OK)
            return null;

        if (!int.TryParse(input.Text.Trim(), out var selected))
            return null;

        if (selected < min || selected > max)
            return null;

        return selected;
    }

    private void StepBack()
    {
        var previous = FindPreviousActionableIndex(_currentIndex < 0 ? _party.Count : _currentIndex);
        if (previous < 0)
            return;

        _currentIndex = previous;
        _actions.Remove(_party[_currentIndex].Name);
        UpdateHeader();
        UpdatePartyList();
    }

    private void AdvanceActor()
    {
        _currentIndex = FindNextActionableIndex(_currentIndex);

        if (_currentIndex < 0)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        UpdateHeader();
        UpdatePartyList();
    }

    private void UpdateHeader()
    {
        var asleepText = _asleepMonsterCount > 0 ? $"  ({_asleepMonsterCount} ASLEEP)" : string.Empty;
        _headerLabel.Text = $"1)  {_monsterCount}  {_monsterName.ToUpperInvariant()}{asleepText}";

        if (_currentIndex >= 0 && _currentIndex < _party.Count)
        {
            _optionsTitleLabel.Text = $"{_party[_currentIndex].Name.ToUpperInvariant()}'S OPTIONS";
            UpdateOptionsLegend();
        }
        else
        {
            _optionsTitleLabel.Text = "NO ACTIONS (ALL CHARACTERS DOWN)";
        }
    }

    private void UpdateOptionsLegend()
    {
        var rank = GetActionableRank(_currentIndex);

        // Determine which action is mapped to Enter
        string fightText, parryText;
        if (rank is >= 1 and <= 3)
        {
            fightText = "F<-IGHT";  // Enter for Fight
            parryText = "P)ARRY";
        }
        else if (rank is >= 4 and <= 6)
        {
            fightText = "F)IGHT";
            parryText = "P<-ARRY";  // Enter for Parry
        }
        else
        {
            fightText = "F)IGHT";
            parryText = "P)ARRY";
        }

        if (_multipleGroups)
        {
            _optionsLegendLabel.Text = $"{fightText}   U)SE ITEM   R)UN\nS)PELL   {parryText}      T)AKE BACK\nG)ROUP   (Select Target Group)";
        }
        else
        {
            _optionsLegendLabel.Text = $"{fightText}   U)SE ITEM   R)UN\nS)PELL   {parryText}      T)AKE BACK";
        }
    }

    private void UpdatePartyList()
    {
        _partyList.Items.Clear();

        for (int i = 0; i < _party.Count; i++)
        {
            var c = _party[i];
            string action;

            if (c.CurrentHitPoints <= 0 || c.HasStatus(CharacterStatus.Dead))
            {
                action = "DEAD";
            }
            else if (_actions.TryGetValue(c.Name, out var a))
            {
                action = GetDetailedActionStatus(a);
            }
            else
            {
                action = "??????";
            }

            var item = new ListViewItem((i + 1).ToString());
            item.SubItems.Add(c.Name);
            item.SubItems.Add(GetClassCode(c));
            item.SubItems.Add(c.ArmorClass.ToString());
            item.SubItems.Add(c.CurrentHitPoints.ToString()+ "/"+c.MaxHitPoints.ToString()); ;// Add max hit points as a separate subitem
            item.SubItems.Add(action);

            _partyList.Items.Add(item);
        }

        if (_currentIndex >= 0 && _currentIndex < _partyList.Items.Count)
            _partyList.Items[_currentIndex].Selected = true;
    }

    private string GetDetailedActionStatus(CombatAction action)
    {
        var baseAction = action.Type.ToString();

        // If casting a spell, show the spell name
        if ((action.Type == CombatActionType.Spell || action.Type == CombatActionType.CastSpell) && !string.IsNullOrEmpty(action.SpellId))
        {
            var allSpells = _spellRepo.LoadAll();
            var spell = allSpells.FirstOrDefault(s => s.Id == action.SpellId);
            if (spell != null)
            {
                baseAction = $"Spell: {spell.Name}";
            }
        }

        // If fighting or targeting a specific group, show the group
        if (!string.IsNullOrEmpty(action.TargetGroupId) && _multipleGroups)
        {
            // Get the monster name from the target group
            if (_session != null)
            {
                var groupMonsters = _session.GetMonstersByGroup(action.TargetGroupId).ToList();
                if (groupMonsters.Any())
                {
                    var monsterName = groupMonsters.First().Name;
                    baseAction += $" -> {monsterName}";
                }
            }
        }

        return baseAction;
    }

    private int FindNextActionableIndex(int fromIndex)
    {
        for (int i = fromIndex + 1; i < _party.Count; i++)
        {
            if (IsActionable(_party[i]))
                return i;
        }

        return -1;
    }

    private int FindPreviousActionableIndex(int fromIndex)
    {
        for (int i = fromIndex - 1; i >= 0; i--)
        {
            if (IsActionable(_party[i]))
                return i;
        }

        return -1;
    }

    private static bool IsActionable(Character c) => c.CurrentHitPoints > 0 && !c.HasStatus(CharacterStatus.Dead);

    private int GetActionableRank(int partyIndex)
    {
        if (partyIndex < 0 || partyIndex >= _party.Count)
            return -1;

        int rank = 0;
        for (int i = 0; i < _party.Count; i++)
        {
            if (!IsActionable(_party[i]))
                continue;

            rank++;
            if (i == partyIndex)
                return rank;
        }

        return -1;
    }

    private static string GetClassCode(Character c)
    {
        var cls = c.Classes.Count > 0 ? c.Classes[0].ToDisplayString() : c.Class.ToDisplayString();
        var clsCode = cls.Length >= 3 ? cls[..3].ToUpperInvariant() : cls.ToUpperInvariant();
        var raceCode = c.Race.ToDisplayString();
        raceCode = raceCode.Length > 0 ? raceCode[..1].ToUpperInvariant() : "?";
        return $"{raceCode}-{clsCode}";
    }

    private void MonsterPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

        var rect = _monsterPanel.ClientRectangle;

        // Handle multiple group images
        if (_monsterImages.Count > 0)
        {
            DrawMultipleMonsterImages(g, rect, _monsterImages);
            return;
        }

        // Handle single monster image
        if (_monsterImage is not null)
        {
            DrawMonsterImage(g, rect, _monsterImage);
            return;
        }

        using var pen = new Pen(Color.White, 2f);
        var cx = rect.Width / 2f;
        var cy = rect.Height / 2f + 8f;

        switch (_monsterName.ToLowerInvariant())
        {
            case "skeleton":
                DrawSkeleton(g, pen, cx, cy);
                break;
            case "goblin":
                DrawHumanoid(g, pen, cx, cy);
                break;
            default:
                DrawRat(g, pen, cx, cy);
                break;
        }
    }

    private static void DrawMultipleMonsterImages(Graphics g, Rectangle bounds, List<(string Name, Image? Image)> monsterImages)
    {
        const int padding = 10;
        const int spacing = 15;

        if (monsterImages.Count == 0)
            return;

        // Calculate available space for each image/slot
        var totalSpacing = spacing * (monsterImages.Count - 1);
        var availableWidth = bounds.Width - (2 * padding) - totalSpacing;
        var slotWidth = availableWidth / monsterImages.Count;

        // Draw each monster in its slot
        for (int i = 0; i < monsterImages.Count; i++)
        {
            var image = monsterImages[i].Image;
            var name = monsterImages[i].Name;

            // Calculate the slot bounds
            var slotX = bounds.X + padding + (i * (slotWidth + spacing));
            var slotBounds = new Rectangle(slotX, bounds.Y + padding, slotWidth, bounds.Height - (2 * padding));

            if (image != null)
            {
                // Draw image if available
                var scale = Math.Min(slotBounds.Width / (float)image.Width, (slotBounds.Height - 20) / (float)image.Height);
                var drawWidth = (int)(image.Width * scale);
                var drawHeight = (int)(image.Height * scale);
                var drawX = slotBounds.X + ((slotBounds.Width - drawWidth) / 2);
                var drawY = slotBounds.Y + ((slotBounds.Height - 20 - drawHeight) / 2);

                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(image, new Rectangle(drawX, drawY, drawWidth, drawHeight));
            }
            else
            {
                // Draw simple placeholder if no image
                using var pen = new Pen(Color.White, 2f);
                var cx = slotBounds.X + (slotBounds.Width / 2f);
                var cy = slotBounds.Y + (slotBounds.Height / 2f);

                // Draw a simple creature shape based on name
                if (name.ToLowerInvariant().Contains("skeleton"))
                    DrawSkeleton(g, pen, cx, cy);
                else if (name.ToLowerInvariant().Contains("goblin"))
                    DrawHumanoid(g, pen, cx, cy);
                else
                    DrawRat(g, pen, cx, cy);
            }

            // Draw monster name label below
            using var font = new Font("Consolas", 10f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            var textSize = g.MeasureString(name, font);
            var textX = slotBounds.X + ((slotBounds.Width - textSize.Width) / 2);
            var textY = slotBounds.Bottom - textSize.Height;
            g.DrawString(name, font, brush, textX, textY);
        }
    }

    private static void DrawMonsterImage(Graphics g, Rectangle bounds, Image image)
    {
        const int padding = 10;
        var target = Rectangle.Inflate(bounds, -padding, -padding);

        var scale = Math.Min(target.Width / (float)image.Width, target.Height / (float)image.Height);
        var drawWidth = (int)(image.Width * scale);
        var drawHeight = (int)(image.Height * scale);
        var drawX = target.X + ((target.Width - drawWidth) / 2);
        var drawY = target.Y + ((target.Height - drawHeight) / 2);

        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        g.DrawImage(image, new Rectangle(drawX, drawY, drawWidth, drawHeight));
    }

    private static System.Drawing.Image? TryLoadMonsterImage(string monsterName, int? dungeonLevel = null, bool useWizardrySuffix = false)
    {
        var slug = monsterName.Trim().ToLowerInvariant().Replace(" ", "_");
        var camelCase = monsterName.Trim().Replace(" ", "");
        var exts = new[] { ".webp", ".png", ".bmp", ".gif", ".jpg", ".jpeg" };

        var baseDir = AppContext.BaseDirectory;
        var candidates = new List<string>();

        // Helper function to add candidates with optional _Wiz suffix
        void AddCandidates(string folder, string baseName, bool tryWizFirst)
        {
            foreach (var ext in exts)
            {
                // If Wizardry suffix should be used, try _Wiz version first
                if (tryWizFirst)
                {
                    candidates.Add(Path.Combine(folder, baseName + "_Wiz" + ext));
                }
                candidates.Add(Path.Combine(folder, baseName + ext));
            }
        }

        // If dungeonLevel is provided, search in level-specific folder first
        if (dungeonLevel.HasValue)
        {
            var levelFolder = $"Level{dungeonLevel.Value}";
            var baseFolder = Path.Combine(baseDir, "Assets", "Monsters", levelFolder);

            AddCandidates(baseFolder, slug, useWizardrySuffix);
            AddCandidates(baseFolder, camelCase, useWizardrySuffix);
            AddCandidates(baseFolder, monsterName, useWizardrySuffix);

            // Source paths
            var sourceFolder1 = Path.Combine("Adnd.Game", "Assets", "Monsters", levelFolder);
            var sourceFolder2 = Path.Combine("Assets", "Monsters", levelFolder);

            AddCandidates(sourceFolder1, slug, useWizardrySuffix);
            AddCandidates(sourceFolder1, camelCase, useWizardrySuffix);
            AddCandidates(sourceFolder2, slug, useWizardrySuffix);
            AddCandidates(sourceFolder2, camelCase, useWizardrySuffix);
        }

        // Also search in all level folders (Level1-Level10) if not found yet
        for (int level = 1; level <= 10; level++)
        {
            var levelFolder = $"Level{level}";
            var baseFolder = Path.Combine(baseDir, "Assets", "Monsters", levelFolder);

            AddCandidates(baseFolder, slug, useWizardrySuffix);
            AddCandidates(baseFolder, camelCase, useWizardrySuffix);
            AddCandidates(baseFolder, monsterName, useWizardrySuffix);

            // Source paths
            var sourceFolder1 = Path.Combine("Adnd.Game", "Assets", "Monsters", levelFolder);
            var sourceFolder2 = Path.Combine("Assets", "Monsters", levelFolder);

            AddCandidates(sourceFolder1, slug, useWizardrySuffix);
            AddCandidates(sourceFolder1, camelCase, useWizardrySuffix);
            AddCandidates(sourceFolder2, slug, useWizardrySuffix);
            AddCandidates(sourceFolder2, camelCase, useWizardrySuffix);
        }

        // Fallback: search in root Monsters folder
        var rootFolder = Path.Combine(baseDir, "Assets", "Monsters");
        AddCandidates(rootFolder, slug, useWizardrySuffix);
        AddCandidates(rootFolder, camelCase, useWizardrySuffix);
        AddCandidates(rootFolder, monsterName, useWizardrySuffix);

        // Source root paths
        AddCandidates(Path.Combine("Adnd.Game", "Assets", "Monsters"), slug, useWizardrySuffix);
        AddCandidates(Path.Combine("Adnd.Game", "Assets", "Monsters"), camelCase, useWizardrySuffix);
        AddCandidates(Path.Combine("Assets", "Monsters"), slug, useWizardrySuffix);
        AddCandidates(Path.Combine("Assets", "Monsters"), camelCase, useWizardrySuffix);

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
            return null;

        try
        {
            // Use ImageSharp to load the image (supports WebP)
            using var imageSharp = ImageSharpImage.Load<ImageSharpRgba32>(path);

            // Convert ImageSharp image to System.Drawing.Bitmap
            using var ms = new MemoryStream();
            imageSharp.Save(ms, new ImageSharpPngEncoder());
            ms.Position = 0;
            return new System.Drawing.Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    private static void DrawSkeleton(Graphics g, Pen pen, float cx, float cy)
    {
        g.DrawEllipse(pen, cx - 18, cy - 82, 36, 36);
        g.DrawLine(pen, cx, cy - 46, cx, cy + 16);
        g.DrawLine(pen, cx - 24, cy - 20, cx + 24, cy - 20);
        g.DrawLine(pen, cx - 18, cy + 0, cx + 18, cy + 0);
        g.DrawLine(pen, cx, cy + 16, cx - 16, cy + 52);
        g.DrawLine(pen, cx, cy + 16, cx + 16, cy + 52);
    }

    private static void DrawHumanoid(Graphics g, Pen pen, float cx, float cy)
    {
        g.DrawEllipse(pen, cx - 16, cy - 76, 32, 32);
        g.DrawLine(pen, cx, cy - 44, cx, cy + 18);
        g.DrawLine(pen, cx - 22, cy - 18, cx + 22, cy - 18);
        g.DrawLine(pen, cx, cy + 18, cx - 14, cy + 54);
        g.DrawLine(pen, cx, cy + 18, cx + 14, cy + 54);
    }

    private static void DrawRat(Graphics g, Pen pen, float cx, float cy)
    {
        g.DrawEllipse(pen, cx - 34, cy - 26, 68, 36);
        g.DrawEllipse(pen, cx + 24, cy - 22, 22, 18);
        g.DrawLine(pen, cx - 36, cy - 8, cx - 72, cy - 24);
    }

    private static bool ShouldUseWizardrySuffix(Monster monster)
    {
        // Use Wizardry suffix when:
        // 1. The monster source is WizardryAndAdnd
        // 2. AND the game is set to OnlyWizardry mode
        var sourceOption = GameRulesProvider.Current.MonsterSourceOptions;
        return monster.Source == Sources.WizardryAndAdnd && sourceOption == SourceOptions.OnlyWizardry;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monsterImage?.Dispose();
        }

        base.Dispose(disposing);
    }
}
