namespace Adnd.Core.Spells.Casting.Handlers;

public sealed class MagicMissileHandler : ISpellEffectHandler
{
    public bool CanHandle(string spellId) => string.Equals(spellId, "magic_missile", StringComparison.OrdinalIgnoreCase);

    public SpellCastResult Resolve(SpellCastRequest request)
    {
        var spell = request.Spell;
        if (spell == null)
            return SpellCastResult.Failure("Missing spell definition.");

        var targetRef = request.Targets.FirstOrDefault(t => t.Type == SpellCastTargetType.Enemy);
        var target = targetRef?.MonsterIndex is int idx
            ? request.MonsterTargets.FirstOrDefault(m => m.Index == idx && m.IsAlive)
            : request.MonsterTargets.FirstOrDefault(m => m.IsAlive);

        if (target == null)
            return SpellCastResult.Failure("No valid enemy target selected.");

        var rng = request.Rng ?? Random.Shared;
        var damageRoll = rng.Next(1, 5);
        var damage = damageRoll + 1; // 1d4+1
        var before = target.CurrentHitPoints;
        target.CurrentHitPoints = Math.Max(0, target.CurrentHitPoints - damage);
        var actual = Math.Max(0, before - target.CurrentHitPoints);

        var result = new SpellCastResult { Success = true };
        result.Events.Add($"{request.Caster.Name} casts {spell.Name}. {spell.EffectDescription} Damage: {damage} (1d4+1).");
        result.Events.Add($"{target.DisplayName} takes {actual} damage.");
        if (!target.IsAlive)
            result.Events.Add($"{target.DisplayName} is destroyed.");

        result.HpChanges[target.DisplayName] = -actual;
        return result;
    }
}
