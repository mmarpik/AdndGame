using Adnd.Core.Characters;

namespace Adnd.Core.Spells.Casting;

public enum SpellCastTargetType
{
    Ally,
    Enemy
}

public sealed class SpellCastTarget
{
    public SpellCastTargetType Type { get; set; }
    public string? CharacterName { get; set; }
    public int? MonsterIndex { get; set; }

    public static SpellCastTarget Ally(Character character) => new()
    {
        Type = SpellCastTargetType.Ally,
        CharacterName = character.Name
    };

    public static SpellCastTarget Enemy(int monsterIndex) => new()
    {
        Type = SpellCastTargetType.Enemy,
        MonsterIndex = monsterIndex
    };
}
