using Adnd.Core.Monsters;

namespace Adnd.Core.Combat.Sessions;

public sealed class MonsterInstance
{
    private readonly Dictionary<MonsterStatus, int> _statusDurations = new();

    public MonsterInstance(Monster template, int index)
    {
        Template = template;
        Index = index;
        Name = template.Name;
        CurrentHitPoints = template.HitPoints;
        ArmorClass = template.ArmorClass;
    }

    public Monster Template { get; }
    public int Index { get; }
    public string Name { get; }
    public int CurrentHitPoints { get; set; }
    public int ArmorClass { get; }
    public bool IsAlive => CurrentHitPoints > 0;

    public string DisplayName => $"{Name} #{Index}";

    public bool HasStatus(MonsterStatus status)
    {
        return _statusDurations.TryGetValue(status, out var rounds) && rounds > 0;
    }

    public int GetStatusRounds(MonsterStatus status)
    {
        return _statusDurations.TryGetValue(status, out var rounds) ? Math.Max(0, rounds) : 0;
    }

    public void SetStatus(MonsterStatus status, int rounds)
    {
        if (rounds <= 0)
        {
            _statusDurations.Remove(status);
            return;
        }

        _statusDurations[status] = rounds;
    }

    public int TickStatus(MonsterStatus status)
    {
        if (!_statusDurations.TryGetValue(status, out var rounds) || rounds <= 0)
            return 0;

        rounds -= 1;
        if (rounds <= 0)
        {
            _statusDurations.Remove(status);
            return 0;
        }

        _statusDurations[status] = rounds;
        return rounds;
    }
}
