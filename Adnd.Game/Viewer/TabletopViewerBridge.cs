// Optional bridge to the Wizardry tabletop viewer, which draws the crawl as physical miniatures
// on a table (1 inch per dungeon cell, print-scale figures) instead of a first-person view.
//
// Two rules hold this together:
//   1. The game never depends on the viewer. If nothing is listening, every call here is a no-op
//      and the player sees no difference.
//   2. Nothing here may throw into the game loop, and nothing may block it. All I/O is
//      fire-and-forget on a background task with a short timeout.
//
// The wire format is the viewer's snapshot protocol v1: a full statement of what is true right
// now, not a diff. Publishing the whole picture every turn means a dropped or late snapshot can
// never leave the table wrong.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Adnd.Core.Characters;

namespace Adnd.Game.Viewer;

public sealed class TabletopViewerBridge
{
    // The viewer's HttpListener binds 127.0.0.1 specifically, so the literal address is required:
    // a "localhost" Host header does not match its prefix and comes back 400 with nothing logged.
    private const string DefaultEndpoint = "http://127.0.0.1:8787/state";

    /// <summary>Set ADND_VIEWER=0 to stop even trying.</summary>
    private const string DisableVariable = "ADND_VIEWER";

    /// <summary>Set ADND_VIEWER_URL to point at a viewer somewhere other than this machine.</summary>
    private const string EndpointVariable = "ADND_VIEWER_URL";

    // One client for the process. The timeout is the whole point: a turn must never wait on this.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMilliseconds(750) };

    private readonly string _endpoint;
    private readonly bool _enabled;

    // The viewer drops any snapshot not newer than the highest sequence it has seen, so this must
    // increase on every publish or later ones are silently ignored.
    private long _seq;

    // Cells the party has stood in, per level. The game keeps no fog of war of its own and the
    // viewer lays only what has been mapped, so that memory lives here.
    private readonly Dictionary<int, HashSet<(int X, int Y)>> _visited = new();

    public TabletopViewerBridge()
    {
        var disable = Environment.GetEnvironmentVariable(DisableVariable);
        _enabled = !string.Equals(disable, "0", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(disable, "false", StringComparison.OrdinalIgnoreCase);

        var configured = Environment.GetEnvironmentVariable(EndpointVariable);
        _endpoint = string.IsNullOrWhiteSpace(configured) ? DefaultEndpoint : configured!;
    }

    /// <summary>One monster group as the viewer wants to hear about it.</summary>
    public sealed record MonsterGroupView(string MonsterId, int Total, int Alive, int Asleep);

    /// <summary>
    /// Tell the viewer where the party is standing and what it can see.
    /// </summary>
    /// <param name="level">Dungeon level. The viewer clears the table when this changes.</param>
    /// <param name="isFloor">Walkable test for the level's grid, in game coordinates.</param>
    /// <param name="width">Grid width in cells.</param>
    /// <param name="height">Grid height in cells.</param>
    /// <param name="cellX">Party cell, x.</param>
    /// <param name="cellY">Party cell, y.</param>
    /// <param name="facing">"North", "East", "South" or "West".</param>
    /// <param name="party">Party in marching order; the first three are the front rank.</param>
    /// <param name="groups">Monsters currently faced, or null outside combat.</param>
    public void Publish(
        int level,
        Func<int, int, bool> isFloor,
        int width,
        int height,
        int cellX,
        int cellY,
        string facing,
        IReadOnlyList<Character> party,
        IReadOnlyList<MonsterGroupView>? groups = null)
    {
        if (!_enabled) return;

        try
        {
            var payload = BuildPayload(level, isFloor, width, height, cellX, cellY, facing, party, groups);
            var json = JsonSerializer.Serialize(payload);
            Post(json);
        }
        catch
        {
            // A viewer that cannot be drawn must never spoil a turn.
        }
    }

    private object BuildPayload(
        int level,
        Func<int, int, bool> isFloor,
        int width,
        int height,
        int cellX,
        int cellY,
        string facing,
        IReadOnlyList<Character> party,
        IReadOnlyList<MonsterGroupView>? groups)
    {
        // '#' is solid, anything else is walkable. The viewer only ever asks "is this floor".
        var rows = new string[height];
        for (int y = 0; y < height; y++)
        {
            var row = new StringBuilder(width);
            for (int x = 0; x < width; x++)
                row.Append(isFloor(x, y) ? '.' : '#');
            rows[y] = row.ToString();
        }

        if (!_visited.TryGetValue(level, out var seen))
        {
            seen = new HashSet<(int X, int Y)>();
            _visited[level] = seen;
        }
        seen.Add((cellX, cellY));

        // Standing in a cell maps its neighbours too, otherwise the party walks a corridor with no
        // walls beside it and the table looks like a bare floor plan.
        foreach (var (dx, dy) in new[] { (0, -1), (0, 1), (1, 0), (-1, 0) })
        {
            var nx = cellX + dx;
            var ny = cellY + dy;
            if (nx >= 0 && nx < width && ny >= 0 && ny < height && isFloor(nx, ny))
                seen.Add((nx, ny));
        }

        var members = new List<object>(party.Count);
        for (int i = 0; i < party.Count; i++)
        {
            var c = party[i];
            members.Add(new
            {
                Id = "char:" + c.Name,
                c.Name,
                ClassId = c.Class.ToString(),
                RaceId = c.Race.ToString(),
                Slot = i,
                Cell = new[] { cellX, cellY },
                Facing = facing,
                Hp = new[] { c.CurrentHitPoints, c.MaxHitPoints },
                Ac = c.ArmorClass,
                Status = StatusNames(c.Status),
            });
        }

        object? encounter = null;
        if (groups is { Count: > 0 })
        {
            var groupViews = new List<object>(groups.Count);
            for (int gi = 0; gi < groups.Count; gi++)
            {
                var g = groups[gi];
                var groupId = "g" + gi;
                var monsterMembers = new List<object>(Math.Max(0, g.Total));
                for (int i = 0; i < g.Total; i++)
                {
                    monsterMembers.Add(new
                    {
                        Id = $"mon:{groupId}#{i}",
                        Index = i,
                        Alive = i < g.Alive,
                        Status = i >= g.Alive - g.Asleep && i < g.Alive
                            ? new List<string> { "Asleep" }
                            : new List<string>(),
                    });
                }

                groupViews.Add(new
                {
                    GroupId = groupId,
                    g.MonsterId,
                    g.Alive,
                    g.Asleep,
                    Members = monsterMembers,
                });
            }

            encounter = new { Round = 1, Groups = groupViews };
        }

        return new
        {
            SchemaVersion = 1,
            Seq = ++_seq,
            Phase = groups is { Count: > 0 } ? "combat" : "maze",
            Level = level,
            Grid = new { Width = width, Height = height, Rows = rows },
            Explored = seen.Select(c => c.X + "," + c.Y).ToArray(),
            Party = members,
            Encounter = encounter,
            Log = Array.Empty<object>(),
        };
    }

    /// <summary>Flag names the viewer can read, e.g. Dead so a standee is laid on its side.</summary>
    private static List<string> StatusNames(CharacterStatus status)
    {
        var names = new List<string>();
        if (status == CharacterStatus.None) return names;

        foreach (CharacterStatus flag in Enum.GetValues<CharacterStatus>())
        {
            if (flag == CharacterStatus.None) continue;
            if (status.HasFlag(flag)) names.Add(flag.ToString());
        }

        return names;
    }

    /// <summary>
    /// Fire and forget. The viewer answers 204 before it does any work, so this is quick when it
    /// is there and harmless when it is not: a refused connection on loopback fails immediately,
    /// off the UI thread, and is swallowed.
    /// </summary>
    private void Post(string json)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await Http.PostAsync(_endpoint, content).ConfigureAwait(false);
            }
            catch
            {
                // No viewer running, or it went away. Nothing to do and nobody to tell.
            }
        });
    }
}
