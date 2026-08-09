using Adnd.Core.Characters;

namespace Adnd.Core.Combat.Sessions;

public sealed class CombatSession
{
    public CombatSession(List<Character> party, List<MonsterInstance> monsters)
    {
        Party = party;
        Monsters = monsters;
    }

    public List<Character> Party { get; }
    public List<MonsterInstance> Monsters { get; }
    public int RoundNumber { get; set; } = 1;
    public CombatOutcome Outcome { get; set; } = CombatOutcome.InProgress;

    // Temporary round-combat effects only (not persisted).
    public HashSet<string> BlessedPartyMembers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> InvisiblyBuffedPartyMembers { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsBlessed(string characterName) => BlessedPartyMembers.Contains(characterName);

    public IEnumerable<Character> AliveParty => Party.Where(p => p.CurrentHitPoints > 0 && !p.HasStatus(CharacterStatus.Dead));
    public IEnumerable<MonsterInstance> AliveMonsters => Monsters.Where(m => m.IsAlive);
}
