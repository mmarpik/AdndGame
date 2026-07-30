using Adnd.Core.Characters;

namespace Adnd.Core.Spells.Casting.Handlers;

public sealed class BlessHandler : ISpellEffectHandler
{
    public bool CanHandle(string spellId) => string.Equals(spellId, "bless", StringComparison.OrdinalIgnoreCase);

    public SpellCastResult Resolve(SpellCastRequest request)
    {
        var spell = request.Spell;
        if (spell == null)
            return SpellCastResult.Failure("Missing spell definition.");

        if (request.Context != SpellUseContext.Combat)
            return SpellCastResult.Failure("Bless can only be cast in combat.");

        if (request.RoundNumber != 1)
            return SpellCastResult.Failure("Bless can only be cast in round 1.");

        if (request.CombatSession == null)
            return SpellCastResult.Failure("Missing combat session for Bless.");

        if (request.CombatSession.BlessedPartyMembers.Count > 0)
            return SpellCastResult.Failure("Bless is already active for this battle.");

        var aliveAllies = request.PartyTargets
            .Where(c => c.CurrentHitPoints > 0
                        && !c.HasStatus(CharacterStatus.Dead)
                        && !c.HasStatus(CharacterStatus.Ashes)
                        && !c.HasStatus(CharacterStatus.Lost))
            .ToList();

        if (aliveAllies.Count == 0)
            return SpellCastResult.Failure("No valid allies to bless.");

        foreach (var ally in aliveAllies)
        {
            ally.ArmorClass -= 1;
            request.CombatSession.BlessedPartyMembers.Add(ally.Name);
        }

        return new SpellCastResult
        {
            Success = true,
            Events =
            {
                $"{request.Caster.Name} casts {spell.Name}. {spell.EffectDescription}",
                "Blessed allies gain -1 AC and -1 THAC0 for this battle."
            }
        };
    }
}
