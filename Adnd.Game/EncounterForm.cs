using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Adnd.Core.Characters;
using Adnd.Core.Combat.Actions;
using Adnd.Core.Spells;
using Adnd.Core.Spells.Casting;
using Adnd.Data.Spells;

namespace Adnd.Game;

public sealed class EncounterForm : Form
{
    private readonly string _monsterName;
    private readonly int _monsterCount;
    private readonly int _roundNumber;
    private readonly List<Character> _party;
    private readonly Dictionary<string, CombatAction> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Image? _monsterImage;
    private readonly SpellRepository _spellRepo = new("Data/Spells");

    public IReadOnlyDictionary<string, CombatAction> SelectedActions => _actions;

    private int _currentIndex;

    private readonly Label _headerLabel;
    private readonly Label _optionsTitleLabel;
    private readonly Label _optionsLegendLabel;
    private readonly Panel _monsterPanel;
    private readonly ListView _partyList;

    public EncounterForm(string monsterName, int monsterCount, List<Character> party, int roundNumber)
    {
        _monsterName = monsterName;
        _monsterCount = monsterCount;
        _roundNumber = roundNumber;
        _party = party;
        _monsterImage = TryLoadMonsterImage(monsterName);

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
        _partyList.Columns.Add("Character Name", 280);
        _partyList.Columns.Add("Class", 140);
        _partyList.Columns.Add("AC", 80);
        _partyList.Columns.Add("Hits", 100);
        _partyList.Columns.Add("Status", 220);

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

        _actions[_party[_currentIndex].Name] = CombatAction.OfType(action);
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

        SpellCastTarget target;
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
        else
        {
            var enemyIndex = PromptEnemyTarget();
            if (!enemyIndex.HasValue)
                return;
            target = SpellCastTarget.Enemy(enemyIndex.Value);
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

                    var prepared = state.PreparedSpells.Any(ps => string.Equals(ps.SpellId, spell.Id, StringComparison.OrdinalIgnoreCase) && ps.Count > 0);
                    if (!prepared)
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
        _headerLabel.Text = $"1)  {_monsterCount}  {_monsterName.ToUpperInvariant()}";

        if (_currentIndex >= 0 && _currentIndex < _party.Count)
            _optionsTitleLabel.Text = $"{_party[_currentIndex].Name.ToUpperInvariant()}'S OPTIONS";
        else
            _optionsTitleLabel.Text = "NO ACTIONS (ALL CHARACTERS DOWN)";
    }

    private void UpdatePartyList()
    {
        _partyList.Items.Clear();

        for (int i = 0; i < _party.Count; i++)
        {
            var c = _party[i];
            var action = c.CurrentHitPoints <= 0 || c.HasStatus(CharacterStatus.Dead)
                ? "DEAD"
                : (_actions.TryGetValue(c.Name, out var a) ? a.Type.ToString() : "??????");

            var item = new ListViewItem((i + 1).ToString());
            item.SubItems.Add(c.Name);
            item.SubItems.Add(GetClassCode(c));
            item.SubItems.Add(c.ArmorClass.ToString());
            item.SubItems.Add(c.CurrentHitPoints.ToString());
            item.SubItems.Add(action);

            _partyList.Items.Add(item);
        }

        if (_currentIndex >= 0 && _currentIndex < _partyList.Items.Count)
            _partyList.Items[_currentIndex].Selected = true;
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

    private static Image? TryLoadMonsterImage(string monsterName)
    {
        var slug = monsterName.Trim().ToLowerInvariant().Replace(" ", "_");
        var exts = new[] { ".png", ".bmp", ".gif", ".jpg", ".jpeg" };

        var baseDir = AppContext.BaseDirectory;
        var candidates = new List<string>();

        foreach (var ext in exts)
        {
            candidates.Add(Path.Combine(baseDir, "Assets", "Monsters", slug + ext));
            candidates.Add(Path.Combine(baseDir, "Assets", "Monsters", monsterName + ext));
        }

        foreach (var ext in exts)
        {
            candidates.Add(Path.Combine("Adnd.Game", "Assets", "Monsters", slug + ext));
            candidates.Add(Path.Combine("Assets", "Monsters", slug + ext));
        }

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
            return null;

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var loaded = Image.FromStream(fs);
        return new Bitmap(loaded);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monsterImage?.Dispose();
        }

        base.Dispose(disposing);
    }
}
