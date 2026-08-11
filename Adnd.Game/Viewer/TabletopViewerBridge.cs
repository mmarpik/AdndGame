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
using Adnd.Core.Combat.Actions;
using Adnd.Core.Combat.Sessions;

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

    // The viewer drops any snapshot whose Seq is not greater than the last one it accepted, so this
    // must increase on every publish. Process-wide, NOT per instance: two bridges counting
    // independently means whichever starts later is ignored until its count overtakes the other's.
    // That showed up as the maze refusing to appear until the party moved, because the town
    // publishes had already run the number up.
    private static long _seq;

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

    // Who was alive and how hurt, as of the last combat publish. Round-to-round changes are found
    // by comparing against this: CombatEvent carries only a display string, so nothing structured
    // can be recovered from the round's own output.
    private readonly Dictionary<string, int> _lastHp = new();
    private readonly HashSet<string> _lastDead = new();

    /// <summary>
    /// Publish a combat round: the monsters actually in the session, plus beats for what the party
    /// chose to do and who died. Beats are theatre — the viewer animates them but reconciles to the
    /// state either way, so a dropped one costs nothing.
    /// </summary>
    public void PublishCombat(
        int level,
        Func<int, int, bool> isFloor,
        int width,
        int height,
        int cellX,
        int cellY,
        string facing,
        IReadOnlyList<Character> party,
        IReadOnlyList<MonsterInstance> monsters,
        int roundNumber,
        IReadOnlyDictionary<string, CombatAction> chosenActions = null)
    {
        if (!_enabled) return;

        try
        {
            var beats = new List<object>();

            // What the party chose. These are ids and enum names, never display text.
            if (chosenActions != null)
            {
                foreach (var kv in chosenActions)
                {
                    var action = kv.Value;
                    if (action == null) continue;

                    // Only actions that are a visible gesture get a beat. Parry, Run and UseItem
                    // are choices, not motions, and inventing a lunge for them would misreport
                    // what the character did.
                    string type;
                    switch (action.Type)
                    {
                        case CombatActionType.Fight: type = "attack"; break;
                        case CombatActionType.Spell:
                        case CombatActionType.CastSpell: type = "cast"; break;
                        default: continue;
                    }

                    // The chosen target is a group, not an individual: which monster gets hit is
                    // decided during resolution. Lean toward the first living member of the group
                    // so the gesture points the right way without inventing a victim.
                    var target = FirstAliveIn(monsters, action.TargetGroupId);

                    beats.Add(new
                    {
                        T = type,
                        By = "char:" + kv.Key,
                        At = target,
                        SpellId = action.SpellId,
                    });
                }
            }

            // Deaths and damage, found by diffing rather than by reading round messages.
            foreach (var m in monsters)
            {
                var id = MonsterId(m);
                if (!m.IsAlive && _lastDead.Add(id))
                    beats.Add(new { T = "death", At = id });
            }

            foreach (var c in party)
            {
                var id = "char:" + c.Name;
                var dead = c.CurrentHitPoints <= 0 || c.Status.HasFlag(CharacterStatus.Dead);
                if (dead)
                {
                    if (_lastDead.Add(id)) beats.Add(new { T = "death", At = id });
                }
                else if (_lastHp.TryGetValue(id, out var was) && c.CurrentHitPoints < was)
                {
                    beats.Add(new
                    {
                        T = "damage",
                        At = id,
                        Amount = was - c.CurrentHitPoints,
                        Hp = new[] { c.CurrentHitPoints, c.MaxHitPoints },
                    });
                }
                _lastHp[id] = c.CurrentHitPoints;
            }

            var groups = GroupsOf(monsters);
            var payload = BuildPayload(level, isFloor, width, height, cellX, cellY, facing,
                                       party, groups, monsters, roundNumber, beats);
            Post(JsonSerializer.Serialize(payload));
        }
        catch
        {
            // Never spoil a fight for the sake of a picture.
        }
    }

    /// <summary>
    /// Tell the viewer the party is above ground and where. No grid and no cells: which places
    /// exist and where they sit on the table is the viewer's business, so this sends only the id.
    /// </summary>
    public void PublishTown(string locationId, IReadOnlyList<Character> party)
    {
        if (!_enabled) return;

        try
        {
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
                    Cell = (int[])null,
                    Facing = "North",
                    Hp = new[] { c.CurrentHitPoints, c.MaxHitPoints },
                    Ac = c.ArmorClass,
                    Status = StatusNames(c.Status),
                });
            }

            Post(JsonSerializer.Serialize(new
            {
                SchemaVersion = 1,
                Seq = System.Threading.Interlocked.Increment(ref _seq),
                Phase = "town",
                Level = (int?)null,
                Grid = (object)null,
                Explored = Array.Empty<string>(),
                Location = locationId,
                Party = members,
                Encounter = (object)null,
                Log = Array.Empty<object>(),
            }));
        }
        catch
        {
            // As everywhere here: the town is not worth a crash.
        }
    }

    /// <summary>Forget the previous fight, so the next one's first round is not read as deaths.</summary>
    public void ResetCombatMemory()
    {
        _lastHp.Clear();
        _lastDead.Clear();
    }

    private static string MonsterId(MonsterInstance m) => $"mon:{m.GroupId}#{m.Index}";

    private static string FirstAliveIn(IReadOnlyList<MonsterInstance> monsters, string groupId)
    {
        foreach (var m in monsters)
        {
            if (!m.IsAlive) continue;
            if (groupId != null && m.GroupId != groupId) continue;
            return MonsterId(m);
        }

        // No group named, or that group is wiped: any living monster will do for a direction.
        foreach (var m in monsters)
            if (m.IsAlive) return MonsterId(m);

        return null;
    }

    /// <summary>Real groups, real counts — taken from the session rather than re-rolled.</summary>
    private static List<MonsterGroupView> GroupsOf(IReadOnlyList<MonsterInstance> monsters)
    {
        var byGroup = new Dictionary<string, (string Name, int Total, int Alive, int Asleep)>();
        foreach (var m in monsters)
        {
            byGroup.TryGetValue(m.GroupId, out var acc);
            var name = acc.Name ?? m.Name;
            var asleep = acc.Asleep + (m.IsAlive && m.HasStatus(MonsterStatus.Asleep) ? 1 : 0);
            byGroup[m.GroupId] = (name, acc.Total + 1, acc.Alive + (m.IsAlive ? 1 : 0), asleep);
        }

        var result = new List<MonsterGroupView>(byGroup.Count);
        foreach (var kv in byGroup)
            result.Add(new MonsterGroupView(kv.Value.Name, kv.Value.Total, kv.Value.Alive, kv.Value.Asleep));

        return result;
    }

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
        IReadOnlyList<MonsterGroupView>? groups,
        IReadOnlyList<MonsterInstance>? monsters = null,
        int roundNumber = 1,
        List<object>? beats = null)
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
        if (monsters is { Count: > 0 })
        {
            // Straight from the session: every standee is a real MonsterInstance, so its id is
            // stable across rounds and the viewer can topple the one that actually died.
            var byGroup = new Dictionary<string, List<MonsterInstance>>();
            foreach (var m in monsters)
            {
                if (!byGroup.TryGetValue(m.GroupId, out var list))
                {
                    list = new List<MonsterInstance>();
                    byGroup[m.GroupId] = list;
                }
                list.Add(m);
            }

            var groupViews = new List<object>(byGroup.Count);
            foreach (var kv in byGroup)
            {
                var groupMembers = new List<object>(kv.Value.Count);
                var alive = 0;
                var asleep = 0;
                foreach (var m in kv.Value)
                {
                    if (m.IsAlive) alive++;
                    var isAsleep = m.IsAlive && m.HasStatus(MonsterStatus.Asleep);
                    if (isAsleep) asleep++;

                    groupMembers.Add(new
                    {
                        Id = MonsterId(m),
                        m.Index,
                        Alive = m.IsAlive,
                        Status = isAsleep ? new List<string> { "Asleep" } : new List<string>(),
                    });
                }

                groupViews.Add(new
                {
                    GroupId = kv.Key,
                    MonsterId = kv.Value.Count > 0 ? kv.Value[0].Name : kv.Key,
                    Alive = alive,
                    Asleep = asleep,
                    Members = groupMembers,
                });
            }

            encounter = new { Round = roundNumber, Groups = groupViews };
        }

        return new
        {
            SchemaVersion = 1,
            Seq = System.Threading.Interlocked.Increment(ref _seq),
            Phase = encounter != null ? "combat" : "maze",
            Level = level,
            Grid = new { Width = width, Height = height, Rows = rows },
            Explored = seen.Select(c => c.X + "," + c.Y).ToArray(),
            Party = members,
            Encounter = encounter,
            Log = beats ?? new List<object>(),
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
