using Adnd.Core.Characters;

namespace Adnd.Core.Spells.Casting.Handlers;

public sealed class InvisibilityHandler : ISpellEffectHandler
{
    public bool CanHandle(string spellId)
    {
        return string.Equals(spellId, "invisibility", StringComparison.OrdinalIgnoreCase)
               || string.Equals(spellId, "improved_invisibility", StringComparison.OrdinalIgnoreCase);
    }

    public SpellCastResult Resolve(SpellCastRequest request)
    {
        var spell = request.Spell;
        if (spell == null)
            return SpellCastResult.Failure("Missing spell definition.");

        var allyNames = request.Targets
            .Where(t => t.Type == SpellCastTargetType.Ally && !string.IsNullOrWhiteSpace(t.CharacterName))
            .Select(t => t.CharacterName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allyNames.Count == 0)
            return SpellCastResult.Failure("No valid ally target for Invisibility.");

        var allies = request.PartyTargets
            .Where(c => allyNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (allies.Count == 0)
            return SpellCastResult.Failure("No valid ally target for Invisibility.");

        var newlyInvisible = new List<string>();
        var alreadyInvisible = new List<string>();

        foreach (var ally in allies)
        {
            if (ally.HasStatus(CharacterStatus.Invisible))
            {
                alreadyInvisible.Add(ally.Name);
                continue;
            }

            ally.AddStatus(CharacterStatus.Invisible);
            ally.ArmorClass -= 4;
            newlyInvisible.Add(ally.Name);

            if (request.Context == SpellUseContext.Combat
                && request.CombatSession != null
                && string.Equals(request.SpellId, "invisibility", StringComparison.OrdinalIgnoreCase))
            {
                request.CombatSession.InvisiblyBuffedPartyMembers.Add(ally.Name);
            }
        }

        var targetNames = string.Join(", ", allies.Select(a => a.Name));

        var result = new SpellCastResult
        {
            Success = true,
            Events =
            {
                $"{request.Caster.Name} casts {spell.Name}. {spell.EffectDescription}",
                $"{targetNames} is targeted by invisibility magic."
            }
        };

        if (newlyInvisible.Count > 0)
            result.Events.Add($"{string.Join(", ", newlyInvisible)} gains -4 AC and becomes Invisible.");

        if (alreadyInvisible.Count > 0)
            result.Events.Add($"{string.Join(", ", alreadyInvisible)} is already Invisible (no additional AC bonus).");

        return result;
    }
}
