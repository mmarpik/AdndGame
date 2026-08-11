#nullable enable
using System.Collections.Generic;

namespace WizardryViewer.Protocol
{

    /// <summary>
    /// Wire format v1. Plain DTOs with no serializer attributes so the same source files can be
    /// dropped into Unity (Newtonsoft) or consumed here (System.Text.Json).
    /// See docs/viewer-protocol.md.
    /// </summary>
    public sealed class Snapshot
    {
        public int SchemaVersion { get; set; } = 1;
        public long Seq { get; set; }
        public string Phase { get; set; } = Phases.Town;

        public int? Level { get; set; }
        public Grid? Grid { get; set; }
        public List<string> Explored { get; set; } = new();

        /// <summary>
        /// Where the party is when not in the maze — a stable id such as "Tavern", never a display
        /// name. Which places exist and where they sit on the table is the viewer's business; the
        /// game only says which one the party is standing in. Null underground.
        /// </summary>
        public string? Location { get; set; }

        public List<PartyMember> Party { get; set; } = new();
        public Encounter? Encounter { get; set; }
        public List<LogEntry> Log { get; set; } = new();
    }

    public static class Phases
    {
        public const string Town = "town";
        public const string Maze = "maze";
        public const string Combat = "combat";
    }

    public sealed class Grid
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<string> Rows { get; set; } = new();

        /// <summary>Glyph at (x,y), or '#' outside the grid. Unknown glyphs are caller's problem.</summary>
        public char At(int x, int y)
        {
            if (y < 0 || y >= Rows.Count) return '#';
            var row = Rows[y];
            if (x < 0 || x >= row.Length) return '#';
            return row[x];
        }

        /// <summary>Anything that is not a wall is walkable, including reserved glyphs.</summary>
        public bool IsFloor(int x, int y) => At(x, y) != '#';
    }

    public sealed class PartyMember
    {
        public string Id { get; set; } = "";

        /// <summary>Player-entered proper noun. The one string that is content, not an id.</summary>
        public string Name { get; set; } = "";

        public string ClassId { get; set; } = "";
        public string RaceId { get; set; } = "";
        public int Slot { get; set; }
        public int[]? Cell { get; set; }
        public string Facing { get; set; } = "North";
        public int[]? Hp { get; set; }
        public int Ac { get; set; }
        public List<string> Status { get; set; } = new();
    }

    public sealed class Encounter
    {
        public int Round { get; set; }
        public List<MonsterGroup> Groups { get; set; } = new();
    }

    public sealed class MonsterGroup
    {
        public string GroupId { get; set; } = "";
        public string MonsterId { get; set; } = "";
        public int Alive { get; set; }
        public int Asleep { get; set; }
        public List<MonsterMember> Members { get; set; } = new();
    }

    public sealed class MonsterMember
    {
        public string Id { get; set; } = "";
        public int Index { get; set; }
        public bool Alive { get; set; } = true;
        public List<string> Status { get; set; } = new();
    }

    /// <summary>
    /// One thing that happened, expressed as meaning rather than wording.
    ///
    /// There is deliberately NO text field: all narration, localisation and TTS belong to the
    /// viewer. Every string here is a stable identifier from the game's enums or data files,
    /// never a display string. Consumers MUST ignore unrecognised <see cref="T"/> values and
    /// tolerate unknown fields.
    /// </summary>
    public sealed class LogEntry
    {
        public string T { get; set; } = "";
        public string? By { get; set; }
        public string? At { get; set; }
        public bool? Hit { get; set; }
        public int? Amount { get; set; }
        public int? Damage { get; set; }
        public int[]? Hp { get; set; }
        public string? SpellId { get; set; }
        public string? MonsterId { get; set; }
        public string? Cause { get; set; }
        public string? Vs { get; set; }
        public string? Result { get; set; }
        public List<string>? Add { get; set; }
        public List<string>? Remove { get; set; }
        public int[]? From { get; set; }
        public int[]? To { get; set; }
        public int? Gold { get; set; }
        public List<string>? ItemIds { get; set; }
    }

    public static class LogTypes
    {
        public const string Attack = "attack";
        public const string Damage = "damage";
        public const string Heal = "heal";
        public const string Cast = "cast";
        public const string Save = "save";
        public const string Status = "status";
        public const string Death = "death";
        public const string Spawn = "spawn";
        public const string Move = "move";
        public const string Treasure = "treasure";
        public const string Experience = "experience";
    }

    public static class SaveResults
    {
        public const string Success = "success";
        public const string Failure = "failure";
    }

    public static class Ids
    {
        public static string Character(string name) => "char:" + name;
        public static string Monster(string groupId, int index) => $"mon:{groupId}#{index}";
        public static string Group(string groupId) => "group:" + groupId;
    }

}
