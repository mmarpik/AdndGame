namespace Adnd.Core.Spells;

public enum SpellRangeType
{
    Self,
    Ally,
    Enemy
}

public enum SpellTargeting
{
    Single,
    Multiple
}

public enum SpellCastContext
{
    Combat,
    Exploration,
    Both
}

public enum SpellEffectType
{
    Damage,
    Heal
}

public class Spell
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public SpellClass SpellClass { get; set; }
    public int Level { get; set; }
    public string Description { get; set; } = "";
    public SpellRangeType RangeType { get; set; } = SpellRangeType.Enemy;
    public SpellTargeting Targeting { get; set; } = SpellTargeting.Single;
    public SpellCastContext CastContext { get; set; } = SpellCastContext.Both;
    public SpellEffectType EffectType { get; set; } = SpellEffectType.Damage;
    public string EffectDescription { get; set; } = "";
}
