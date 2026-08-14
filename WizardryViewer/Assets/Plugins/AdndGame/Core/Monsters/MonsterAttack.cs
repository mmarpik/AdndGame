namespace Adnd.Core.Monsters;

public class MonsterAttack
{
    public string Name { get; set; } = "";
    public int NumberOfAttacks { get; set; }
    public string Damage { get; set; } = ""; // e.g. "1d6", "2d4", "1d8+1"
}
