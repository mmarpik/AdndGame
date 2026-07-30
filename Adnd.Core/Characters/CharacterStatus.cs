namespace Adnd.Core.Characters;

/// <summary>
/// Character status conditions (AD&D). Can be combined as flags.
/// Empty (None) = alive and well.
/// </summary>
[System.Flags]
public enum CharacterStatus
{
    None = 0,           // Alive and well (default)
    Dead = 1 << 0,      // Character is dead
    Poisoned = 1 << 1,  // Character is poisoned
    Paralyzed = 1 << 2, // Character is paralyzed
    Petrified = 1 << 3, // Character is turned to stone
    Asleep = 1 << 4,    // Character is asleep/unconscious
    Ashes = 1 << 5,     // Character has turned to ashes
    Lost = 1 << 6       // Character is permanently lost and cannot be revived
}
