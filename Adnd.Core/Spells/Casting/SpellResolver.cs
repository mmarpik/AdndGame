namespace Adnd.Core.Spells.Casting;

public sealed class SpellResolver
{
    private readonly List<ISpellEffectHandler> _handlers;

    public SpellResolver(IEnumerable<ISpellEffectHandler> handlers)
    {
        _handlers = handlers.ToList();
    }

    public SpellCastResult Resolve(SpellCastRequest request)
    {
        var handler = _handlers.FirstOrDefault(h => h.CanHandle(request.SpellId));
        if (handler == null)
            return SpellCastResult.Failure($"Spell effect not implemented: {request.SpellId}");

        return handler.Resolve(request);
    }
}
