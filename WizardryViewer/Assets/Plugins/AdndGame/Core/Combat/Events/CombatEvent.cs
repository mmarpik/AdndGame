namespace Adnd.Core.Combat.Events;

public sealed class CombatEvent
{
    public CombatEvent(string message)
    {
        Message = message;
    }

    public string Message { get; }
}
