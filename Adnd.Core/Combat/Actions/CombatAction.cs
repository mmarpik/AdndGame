using Adnd.Core.Spells.Casting;

namespace Adnd.Core.Combat.Actions;

public sealed class CombatAction
{
    public CombatActionType Type { get; set; }
    public string? SpellId { get; set; }
    public SpellCastTarget? Target { get; set; }
    public string? TargetGroupId { get; set; }

    public static CombatAction OfType(CombatActionType type) => new() { Type = type };
}
