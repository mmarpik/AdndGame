using Adnd.Core.Combat.Sessions;

namespace Adnd.Core.Spells.Casting.Handlers;

public sealed class HoldPersonHandler : ISpellEffectHandler
{
    public bool CanHandle(string spellId) => string.Equals(spellId, "hold_person", StringComparison.OrdinalIgnoreCase);

    public SpellCastResult Resolve(SpellCastRequest request)
    {
        var spell = request.Spell;
        if (spell == null)
            return SpellCastResult.Failure("Missing spell definition.");

        if (request.Context != SpellUseContext.Combat)
            return SpellCastResult.Failure("Hold Person can only be cast in combat.");

        var session = request.CombatSession;
        var rng = request.Rng ?? Random.Shared;

        // Determine target - pick a random humanoid from the specified group
        MonsterInstance? target = null;

        var firstTarget = request.Targets.FirstOrDefault();
        if (firstTarget?.TargetGroupId != null && session != null)
        {
            var groupTargets = session.GetAliveMonstersByGroup(firstTarget.TargetGroupId)
                .Where(m => IsHumanoid(m.Name))
                .ToList();

            if (groupTargets.Count > 0)
            {
                target = groupTargets[rng.Next(groupTargets.Count)];
            }
        }

        // Fallback to old behavior
        if (target == null)
        {
            var humanoidTargets = request.MonsterTargets
                .Where(m => m.IsAlive && IsHumanoid(m.Name))
                .ToList();

            if (humanoidTargets.Count > 0)
            {
                target = humanoidTargets[rng.Next(humanoidTargets.Count)];
            }
        }

        if (target == null)
            return SpellCastResult.Failure("No valid humanoid targets for Hold Person.");

        var result = new SpellCastResult { Success = true };
        result.Events.Add($"{request.Caster.Name} casts {spell.Name}!");

        var saveTarget = target.Template.SavingThrows?.Spell ?? 20;
        var saveRoll = rng.Next(1, 21);

        if (saveRoll >= saveTarget)
        {
            result.Events.Add($"{target.DisplayName} resists the spell (save {saveRoll} vs {saveTarget}).");
            return result;
        }

        var rounds = rng.Next(2, 7); // 2-6 rounds
        target.SetStatus(MonsterStatus.Paralyzed, rounds);
        result.Events.Add($"{target.DisplayName} fails save ({saveRoll} vs {saveTarget}) and is paralyzed for {rounds} round(s)!");

        return result;
    }

    private static bool IsHumanoid(string monsterName)
    {
        var humanoids = new[] { "human", "elf", "dwarf", "halfling", "gnome", "orc", "goblin", "hobgoblin", "kobold", "bugbear", "gnoll" };
        var nameLower = monsterName.ToLowerInvariant();
        return humanoids.Any(h => nameLower.Contains(h));
    }
}
