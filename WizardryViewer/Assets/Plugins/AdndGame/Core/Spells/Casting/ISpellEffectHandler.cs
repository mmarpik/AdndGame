namespace Adnd.Core.Spells.Casting;

public interface ISpellEffectHandler
{
    bool CanHandle(string spellId);
    SpellCastResult Resolve(SpellCastRequest request);
}
