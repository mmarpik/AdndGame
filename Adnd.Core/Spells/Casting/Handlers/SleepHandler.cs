using Adnd.Core.Combat.Sessions;

namespace Adnd.Core.Spells.Casting.Handlers;

public sealed class SleepHandler : ISpellEffectHandler
{
    public bool CanHandle(string spellId) => string.Equals(spellId, "sleep", StringComparison.OrdinalIgnoreCase);

    public SpellCastResult Resolve(SpellCastRequest request)
    {
        var spell = request.Spell;
        if (spell == null)
            return SpellCastResult.Failure("Missing spell definition.");

        if (request.Context != SpellUseContext.Combat)
            return SpellCastResult.Failure("Sleep can only be cast in combat.");

        var targets = request.Targets
            .Where(t => t.Type == SpellCastTargetType.Enemy && t.MonsterIndex.HasValue)
            .Select(t => request.MonsterTargets.FirstOrDefault(m => m.Index == t.MonsterIndex!.Value && m.IsAlive))
            .Where(m => m != null)
            .Cast<MonsterInstance>()
            .DistinctBy(m => m.Index)
            .ToList();

        if (targets.Count == 0)
            targets = request.MonsterTargets.Where(m => m.IsAlive).ToList();

        if (targets.Count == 0)
            return SpellCastResult.Failure("No valid enemy targets for Sleep.");

        var rng = request.Rng ?? Random.Shared;
        var result = new SpellCastResult { Success = true };
        result.Events.Add($"{request.Caster.Name} casts {spell.Name}. {spell.EffectDescription}");

        foreach (var monster in targets)
        {
            var saveTarget = monster.Template.SavingThrows?.Spell ?? 20;
            var saveRoll = rng.Next(1, 21);

            if (saveRoll >= saveTarget)
            {
                result.Events.Add($"{monster.DisplayName} resists Sleep (save {saveRoll} vs {saveTarget}).");
                continue;
            }

            var rounds = rng.Next(1, 5);
            monster.SetStatus(MonsterStatus.Asleep, rounds);
            result.Events.Add($"{monster.DisplayName} fails save ({saveRoll} vs {saveTarget}) and falls asleep for {rounds} round(s).");
        }

        return result;
    }
}
