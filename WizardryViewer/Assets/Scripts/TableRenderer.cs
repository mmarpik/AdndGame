// Unity-only. The cardboard end of the pipe.
//
// Everything above this class is game-agnostic plumbing. Everything in it is presentation:
// which prefab, where on the table, how it animates, what the DM says. Nothing here may
// derive game facts — if you need one, add it to the snapshot.

using System.Collections.Generic;
using UnityEngine;
using WizardryViewer.Protocol;

namespace WizardryViewer.Unity
{
    public sealed class TableRenderer : MonoBehaviour
    {
        [Header("Scale (keep honest: real table, real 28mm minis)")]
        [Tooltip("Metres per dungeon cell. A 1-inch dungeon tile is 0.0254.")]
        [SerializeField] private float cellSize = 0.0254f;
        [SerializeField] private Transform tableOrigin;

        [Header("Prefabs")]
        [SerializeField] private GameObject floorTilePrefab;
        [SerializeField] private GameObject wallPiecePrefab;
        [SerializeField] private GameObject blankStandeePrefab;   // fallback for unknown ids
        [SerializeField] private StandeeEntry[] standees;         // monsterId/classId -> prefab

        [Tooltip("Stand in one id for another when no figure is registered for it. Lets a game " +
                 "with its own vocabulary (an AD&D Cleric, a Halfling) reach a figure carved for " +
                 "the nearest equivalent, without duplicating prefabs.")]
        [SerializeField] private AliasEntry[] standeeAliases;

        [Header("Level")]
        [Tooltip("Lay the entire level rather than only what the party has mapped. The snapshot " +
                 "carries the whole grid either way, so this reveals nothing the viewer was not " +
                 "already told — it just skips the fog of war.")]
        [SerializeField] private bool revealEntireLevel;

        [Header("Marching order")]
        [Tooltip("Sideways gap between files, as a fraction of a cell. Keep file spacing plus half " +
                 "a figure's width under 0.5 or the outer two clip the corridor walls.")]
        [SerializeField] private float fileSpacing = 0.27f;
        [Tooltip("Front-to-back gap between the two ranks, as a fraction of a cell.")]
        [SerializeField] private float rankSpacing = 0.28f;

        [Tooltip("Yaw correction for the sculpts' own forward direction, in degrees. These figures " +
                 "are not modelled facing their local +Z, so without this the party marches with " +
                 "its back to the way it is going.")]
        [SerializeField] private float standeeYaw = 180f;

        [Header("Motion")]
        [Tooltip("Cells per second when a figure slides. A step must land inside one beat.")]
        [SerializeField] private float slideCellsPerSecond = 2.0f;
        [SerializeField] private float turnDegreesPerSecond = 540f;
        [Tooltip("Further than this and we snap instead of sliding: that is a catch-up jump, " +
                 "not a move, and gliding across the map would be a lie.")]
        [SerializeField] private float snapBeyondCells = 2.5f;

        [System.Serializable]
        public struct StandeeEntry
        {
            public string id;
            public GameObject prefab;
        }

        [System.Serializable]
        public struct AliasEntry
        {
            public string from;
            public string to;
        }

        private struct Placement
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private readonly Dictionary<string, GameObject> _figurines = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _tiles = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _walls = new Dictionary<string, GameObject>();
        private readonly HashSet<string> _seenThisReconcile = new HashSet<string>();

        // Where each figure is headed. _anchor is the authoritative pose from the snapshot;
        // _target is what Update chases, which a beat may lean away from for effect.
        private readonly Dictionary<string, Placement> _anchor = new Dictionary<string, Placement>();
        private readonly Dictionary<string, Placement> _target = new Dictionary<string, Placement>();

        /// <summary>Centre of the party on the table, or null while nobody is placed.</summary>
        public Vector3? PartyCentre { get; private set; }

        /// <summary>Extent of everything laid on the table, for a camera that wants to frame it.</summary>
        public Bounds? LaidBounds { get { return _hasLaid ? _laid : (Bounds?)null; } }

        private Bounds _laid;
        private bool _hasLaid;

        // Which level's geometry is on the table. Tiles and walls are keyed by cell, which is only
        // unique within a level, so laying a second level over a first leaves the old one standing —
        // and LayCell's ContainsKey guard silently skips every cell the two happen to share.
        private int? _laidLevel;
        private int _laidWidth;
        private int _laidHeight;

        // Town and dungeon share the tile dictionaries, so swapping between them has to clear as
        // surely as a level change does.
        private bool _laidTown;
        private string _saidLocation;
        private DmSubtitle _subtitle;
        private readonly Dictionary<string, GameObject> _townLabels = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _townProps = new Dictionary<string, GameObject>();
        private TownProps _props;

        /// <summary>
        /// Where each place sits on the town board, in cells. This is presentation and belongs
        /// here: the snapshot only names the place the party is standing in. Ids the table does not
        /// know still get a pad, appended after the ones it does.
        /// </summary>
        private static readonly (string Id, int X, int Y)[] TownPlaces =
        {
            ("TrainingGrounds", 0,  0),
            ("Tavern",          4,  0),
            ("Temple",          8,  0),
            ("Shop",           12,  0),
            ("EdgeOfTown",      6,  4),
        };

        public Vector3 CellToWorld(int x, int y)
        {
            var o = tableOrigin != null ? tableOrigin.position : Vector3.zero;
            return o + new Vector3(x * cellSize, 0f, -y * cellSize);
        }

        /// <summary>
        /// Make the table match the snapshot. Always safe to call, at any time, from any
        /// state — this is the operation that makes a late-joining or lagging viewer correct.
        /// </summary>
        public void Reconcile(Snapshot s)
        {
            ClearIfNewLevel(s);

            if (IsTown(s)) LayTown(s);
            else LayExploredTiles(s);

            _seenThisReconcile.Clear();

            // Marching order: three abreast in front, three behind, oriented by facing.
            // Six 28mm figures do not fit in one 25mm square, so they overflow it slightly —
            // which is exactly what happens on a real table.
            var facing = FacingVector(s.Party.Count > 0 ? s.Party[0].Facing : "North");

            // Underground they face the way they are marching. In town they are not marching
            // anywhere — the game reports "North" regardless — so they turn to face whoever is
            // looking at the board instead of standing with their backs to them.
            var look = IsTown(s) ? Face(Vector3.back) : Face(facing);

            // In town nobody has a dungeon cell, so the whole party would otherwise be skipped and
            // the board would stand empty. They gather on the pad for wherever they are.
            var townCell = IsTown(s) ? (TownCell(s.Location) ?? new[] { TownPlaces.Length * 4, 8 }) : null;

            foreach (var p in s.Party)
            {
                var cell = p.Cell != null && p.Cell.Length == 2 ? p.Cell : townCell;
                if (cell == null) continue;
                _seenThisReconcile.Add(p.Id);

                bool created;
                // A "Dwarf_Fighter" figure is used when one is registered, otherwise any Fighter.
                var go = Ensure(p.Id, p.RaceId + "_" + p.ClassId, p.ClassId, out created);

                var pose = PartyPose(p.Slot, cell, facing, look);
                if (p.Status != null && p.Status.Contains("Dead")) pose.Rotation = Toppled(pose.Rotation);
                Place(p.Id, go, pose, created);
            }

            if (s.Encounter != null && s.Party.Count > 0 && s.Party[0].Cell != null)
            {
                // Monsters stand two squares ahead of the party, facing back at them,
                // each group in its own row so multi-group encounters stay legible.
                var partyCell = s.Party[0].Cell;
                var anchor = CellToWorld(partyCell[0], partyCell[1]) + facing * (cellSize * 2f);
                var right = RightOf(facing);
                var faceParty = Face(-facing);

                for (int gi = 0; gi < s.Encounter.Groups.Count; gi++)
                {
                    var g = s.Encounter.Groups[gi];
                    for (int i = 0; i < g.Members.Count; i++)
                    {
                        var m = g.Members[i];
                        _seenThisReconcile.Add(m.Id);

                        bool created;
                        var go = Ensure(m.Id, g.MonsterId, g.MonsterId, out created);

                        var file = i - (g.Members.Count - 1) * 0.5f;
                        var pose = new Placement
                        {
                            Position = anchor
                                     + right * (file * cellSize * 0.5f)
                                     + facing * (gi * cellSize * 0.6f),
                            Rotation = m.Alive ? faceParty : Toppled(faceParty),
                        };
                        Place(m.Id, go, pose, created);
                    }
                }
            }

            // Anything the snapshot no longer mentions has left the table.
            var stale = new List<string>();
            foreach (var kv in _figurines)
                if (!_seenThisReconcile.Contains(kv.Key)) stale.Add(kv.Key);

            foreach (var id in stale)
            {
                Destroy(_figurines[id]);
                _figurines.Remove(id);
                _anchor.Remove(id);
                _target.Remove(id);
            }

            RecomputePartyCentre(s);
        }

        /// <summary>
        /// Play one thing that happened. Dropping any of these must leave the table correct,
        /// because Reconcile follows. So: animation and sound only, never bookkeeping.
        /// </summary>
        public void PlayBeat(LogEntry e, Snapshot context, string spokenLine)
        {
            if (!string.IsNullOrEmpty(spokenLine))
                Debug.Log($"[DM] {spokenLine}");   // TODO TTS

            switch (e.T)
            {
                case LogTypes.Move:
                    SlideParty(e, context);
                    break;
                case LogTypes.Attack:
                    LeanInto(e);
                    break;
                case LogTypes.Death:
                    Topple(e);
                    break;
                case LogTypes.Spawn:
                    // TODO the DM's hand reaches in and sets the standee down.
                    break;
                case LogTypes.Treasure:
                    // TODO scatter coins.
                    break;
            }
        }

        /// <summary>
        /// Figures walk to where they are told rather than teleporting. Constant speed, so a
        /// step reads as a step; MoveTowards converges exactly and needs no easing state.
        /// </summary>
        private void Update()
        {
            if (_target.Count == 0) return;

            var step = cellSize * slideCellsPerSecond * Time.deltaTime;
            var turn = turnDegreesPerSecond * Time.deltaTime;

            foreach (var kv in _figurines)
            {
                Placement t;
                if (!_target.TryGetValue(kv.Key, out t)) continue;
                if (kv.Value == null) continue;

                var tr = kv.Value.transform;
                tr.position = Vector3.MoveTowards(tr.position, t.Position, step);
                tr.rotation = Quaternion.RotateTowards(tr.rotation, t.Rotation, turn);
            }
        }

        /// <summary>
        /// Drop the previous level's geometry when the party changes level.
        ///
        /// Accumulating cells *within* a level is the fog-of-war reveal and must be kept, so this
        /// deliberately does not clear on every snapshot — only when the level underneath us has
        /// actually changed.
        /// </summary>
        private void ClearIfNewLevel(Snapshot s)
        {
            // Coming up into town, or heading back down: the other place's geometry has to go.
            var town = IsTown(s);
            if (town != _laidTown)
            {
                ClearLevelGeometry();
                _laidTown = town;

                // Force a fresh lay of whatever we return to, since its cells were just destroyed.
                _laidLevel = null;
                _laidWidth = 0;
                _laidHeight = 0;
            }

            if (town) return;             // the town board lays itself
            if (s.Grid == null) return;   // nothing to lay: leave the table alone

            // Only a level we can compare counts. Level is optional on the wire, and reading a
            // missing one as "a different level" would sweep the table mid-crawl — far worse than
            // failing to clear, so an absent Level is never on its own a reason to wipe.
            var changed = s.Level.HasValue && _laidLevel.HasValue && s.Level.Value != _laidLevel.Value;

            // A level's grid never resizes, so different dimensions mean a different level even
            // when the Level field failed to say so. Guarded on having laid something, or the very
            // first snapshot would compare against 0x0 and count as a change.
            if (_tiles.Count > 0 && (s.Grid.Width != _laidWidth || s.Grid.Height != _laidHeight))
                changed = true;

            if (changed) ClearLevelGeometry();

            if (s.Level.HasValue) _laidLevel = s.Level;
            _laidWidth = s.Grid.Width;
            _laidHeight = s.Grid.Height;
        }

        /// <summary>
        /// Take the floor and walls off the table. Figures are left alone — Reconcile's own stale
        /// sweep owns those, and it runs off the snapshot's ids rather than the level.
        /// </summary>
        private void ClearLevelGeometry()
        {
            foreach (var kv in _tiles)
                if (kv.Value != null) Destroy(kv.Value);
            foreach (var kv in _walls)
                if (kv.Value != null) Destroy(kv.Value);

            foreach (var kv in _townLabels)
                if (kv.Value != null) Destroy(kv.Value);

            foreach (var kv in _townProps)
                if (kv.Value != null) Destroy(kv.Value);

            _tiles.Clear();
            _walls.Clear();
            _townLabels.Clear();
            _townProps.Clear();

            // The whole-level camera frames LaidBounds; keeping the old level's extent would leave
            // it framing bare table next to the new one.
            _hasLaid = false;
            _laid = default(Bounds);
        }

        /// <summary>
        /// True while the town board is laid rather than a dungeon level. Lighting keys off this:
        /// the town is out in daylight, the dungeon is lit by whatever the party carries.
        /// </summary>
        public bool IsTownBoard => _laidTown;

        /// <summary>
        /// Above ground: the snapshot carries a place name and no grid.
        /// </summary>
        private static bool IsTown(Snapshot s)
        {
            return !string.IsNullOrEmpty(s.Location) || (s.Grid == null && s.Phase == Phases.Town);
        }

        /// <summary>
        /// Lay the town out as pads on the table, one per place, and name the one the party is
        /// standing in. A 2x2 pad reads as somewhere you go rather than a square you step on.
        /// </summary>
        private void LayTown(Snapshot s)
        {
            if (floorTilePrefab == null) return;

            if (_props == null) _props = FindAnyObjectByType<TownProps>();

            foreach (var place in TownPlaces)
            {
                LayPad(place.X, place.Y);
                LabelPad(place.Id, place.X, place.Y);

                // Terrain over the pad. Built once and kept: the whole town is standing whether or
                // not the party is there, which is what makes it read as a place they walk between.
                if (_props != null && !_townProps.ContainsKey(place.Id))
                {
                    var centre = CellToWorld(place.X, place.Y) + new Vector3(cellSize * 0.5f, 0f, -cellSize * 0.5f);
                    var built = _props.Build(place.Id, centre, cellSize);
                    if (built != null) _townProps[place.Id] = built;
                }
            }

            // A place the table has no spot for still gets a pad, parked clear of the known ones,
            // so an unrecognised id is visible rather than silently missing.
            if (!string.IsNullOrEmpty(s.Location) && TownCell(s.Location) == null)
                LayPad(TownPlaces.Length * 4, 8);

            // Found lazily rather than wired through setup: the pads alone cannot say which place
            // is which, and this is the one line of genuine display text the viewer owns.
            if (_subtitle == null) _subtitle = FindAnyObjectByType<DmSubtitle>();

            if (_subtitle != null && s.Location != _saidLocation)
            {
                _subtitle.Say(Prettify(s.Location));
                _saidLocation = s.Location;
            }
        }

        /// <summary>
        /// A name card lying on the table beside each pad. Permanent, unlike the DM subtitle, which
        /// holds for a couple of seconds and clears — fine for a line of narration, useless for
        /// telling one place from another once it has faded.
        /// </summary>
        private void LabelPad(string id, int x, int y)
        {
            var key = "label:" + id;
            if (_townLabels.ContainsKey(key)) return;

            var go = new GameObject("Label:" + id);
            go.transform.SetParent(transform, false);

            var text = go.AddComponent<TMPro.TextMeshPro>();
            text.text = Prettify(id);
            text.fontSize = 8f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = new Color(0.92f, 0.88f, 0.78f);
            text.rectTransform.sizeDelta = new Vector2(20f, 6f);

            // Flat on the table, reading from the player's side, clear of the pad's south edge. The
            // pad covers cells y and y+1, so anything less than 1.9 cells out lands on top of it.
            // Scaled so the longest name is about a pad wide: TMP sizes are in points and a cell
            // here is a real 25mm, so the default would be microscopic.
            go.transform.localScale = Vector3.one * (cellSize * 0.22f);
            go.transform.position = CellToWorld(x, y)
                                  + new Vector3(cellSize * 0.5f, 0.0015f, -cellSize * 1.9f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            _townLabels[key] = go;
        }

        private void LayPad(int x, int y)
        {
            for (int dx = 0; dx < 2; dx++)
                for (int dy = 0; dy < 2; dy++)
                {
                    var key = "town:" + (x + dx) + "," + (y + dy);
                    if (_tiles.ContainsKey(key)) continue;

                    var centre = CellToWorld(x + dx, y + dy);
                    _tiles[key] = Instantiate(floorTilePrefab, centre, Quaternion.identity, transform);

                    if (_hasLaid) _laid.Encapsulate(centre);
                    else { _laid = new Bounds(centre, Vector3.zero); _hasLaid = true; }
                }
        }

        /// <summary>Cell for a place id, or null when the table has no spot for it.</summary>
        private static int[] TownCell(string location)
        {
            if (string.IsNullOrEmpty(location)) return null;

            foreach (var place in TownPlaces)
                if (place.Id == location)
                    return new[] { place.X, place.Y };

            return null;
        }

        /// <summary>"TrainingGrounds" -> "Training Grounds", for the one line that is display text.</summary>
        private static string Prettify(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";

            var sb = new System.Text.StringBuilder(id.Length + 4);
            for (int i = 0; i < id.Length; i++)
            {
                if (i > 0 && char.IsUpper(id[i]) && !char.IsUpper(id[i - 1])) sb.Append(' ');
                sb.Append(id[i]);
            }

            return sb.ToString();
        }

        private void LayExploredTiles(Snapshot s)
        {
            if (s.Grid == null) return;

            if (revealEntireLevel)
            {
                for (int y = 0; y < s.Grid.Height; y++)
                    for (int x = 0; x < s.Grid.Width; x++)
                        if (s.Grid.IsFloor(x, y)) LayCell(s.Grid, x, y);
                return;
            }

            if (s.Explored == null) return;

            foreach (var key in s.Explored)
            {
                var parts = key.Split(',');
                if (parts.Length != 2) continue;
                int x, y;
                if (!int.TryParse(parts[0], out x) || !int.TryParse(parts[1], out y)) continue;
                LayCell(s.Grid, x, y);
            }
        }

        private void LayCell(Protocol.Grid grid, int x, int y)
        {
            var centre = CellToWorld(x, y);
            var key = x + "," + y;

            if (!_tiles.ContainsKey(key))
                _tiles[key] = Instantiate(floorTilePrefab, centre, Quaternion.identity, transform);

            if (_hasLaid) _laid.Encapsulate(centre);
            else { _laid = new Bounds(centre, Vector3.zero); _hasLaid = true; }

            // A wall piece goes on every edge where the neighbouring cell is solid. Because a
            // solid cell is never laid, no edge is ever built twice.
            RaiseWall(grid, x, y, 0, -1, "N");
            RaiseWall(grid, x, y, 0, 1, "S");
            RaiseWall(grid, x, y, 1, 0, "E");
            RaiseWall(grid, x, y, -1, 0, "W");
        }

        // Qualified: UnityEngine has a Grid of its own and it is not this one.
        private void RaiseWall(Protocol.Grid grid, int x, int y, int dx, int dy, string side)
        {
            if (wallPiecePrefab == null) return;
            if (grid.IsFloor(x + dx, y + dy)) return;

            var key = $"{x},{y},{side}";
            if (_walls.ContainsKey(key)) return;

            // The piece is a thin slab: local X spans the edge, Y is its height, Z is thickness.
            // Taken from the prefab rather than restated here, so the two cannot drift apart.
            var scale = wallPiecePrefab.transform.localScale;
            var height = scale.y;
            var thickness = scale.z;

            // Pushed out by half its thickness so the slab's inner face lands exactly on the cell
            // boundary. Centred on the boundary instead, a wall eats 1.75mm into the corridor and
            // the outer files of a marching party clip it — the space it moves into is solid rock.
            var step = (cellSize + thickness) * 0.5f;

            // Grid y grows south, so a north neighbour sits at +z in world space.
            var offset = new Vector3(dx * step, height * 0.5f, -dy * step);
            var rotation = dx != 0 ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

            _walls[key] = Instantiate(wallPiecePrefab, CellToWorld(x, y) + offset, rotation, transform);
        }

        private GameObject Ensure(string id, string preferredKey, string fallbackKey, out bool created)
        {
            GameObject existing;
            if (_figurines.TryGetValue(id, out existing))
            {
                created = false;
                return existing;
            }

            var prefab = Resolve(preferredKey) ?? Resolve(fallbackKey) ?? blankStandeePrefab;

            // Unknown id -> blank standee. Cheaper than a placeholder and funnier in context.
            var go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            go.name = id;
            _figurines[id] = go;
            created = true;
            return go;
        }

        /// <summary>
        /// Find a figure for an id, following aliases when nothing is registered under it directly.
        /// Chains are followed a few hops so "Human_Cleric -> Human_Priest" works even if the
        /// target is itself an alias, with a visited set because a typo could otherwise loop.
        /// </summary>
        private GameObject Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            var direct = Lookup(key);
            if (direct != null) return direct;

            if (standeeAliases == null || standeeAliases.Length == 0) return null;

            var seen = new HashSet<string>();
            var current = key;

            for (int hop = 0; hop < 4; hop++)
            {
                if (!seen.Add(current)) return null;   // alias points back at itself

                string next = null;
                foreach (var alias in standeeAliases)
                {
                    if (alias.from != current || string.IsNullOrEmpty(alias.to)) continue;
                    next = alias.to;
                    break;
                }

                if (next == null) return null;

                var found = Lookup(next);
                if (found != null) return found;

                current = next;
            }

            return null;
        }

        private GameObject Lookup(string key)
        {
            if (string.IsNullOrEmpty(key) || standees == null) return null;

            foreach (var entry in standees)
                if (entry.id == key && entry.prefab != null) return entry.prefab;

            return null;
        }

        /// <summary>
        /// Records where a figure belongs. Snaps rather than slides when it has just been set
        /// down, or when the distance means we are catching up rather than moving.
        /// </summary>
        private void Place(string id, GameObject go, Placement pose, bool created)
        {
            _anchor[id] = pose;
            _target[id] = pose;

            var far = Vector3.Distance(go.transform.position, pose.Position) > cellSize * snapBeyondCells;
            if (created || far)
            {
                go.transform.position = pose.Position;
                go.transform.rotation = pose.Rotation;
            }
        }

        private Placement PartyPose(int slot, int[] cell, Vector3 facing, Quaternion look)
        {
            var rank = slot / 3;                  // 0 = front, 1 = back
            var file = (slot % 3) - 1;            // -1, 0, +1

            // Ranks straddle the cell centre (-0.5, +0.5) rather than starting on it, so the
            // formation sits in the middle of its square instead of hanging out the back.
            var offset = RightOf(facing) * (file * cellSize * fileSpacing)
                       - facing * ((rank - 0.5f) * cellSize * rankSpacing);

            return new Placement
            {
                Position = CellToWorld(cell[0], cell[1]) + offset,
                Rotation = look,
            };
        }

        /// <summary>The party walks to the new cell, keeping marching order on the way.</summary>
        private void SlideParty(LogEntry e, Snapshot s)
        {
            if (e.To == null || e.To.Length != 2) return;

            var facing = FacingVector(s.Party.Count > 0 ? s.Party[0].Facing : "North");
            var look = Face(facing);

            foreach (var p in s.Party)
            {
                if (!_figurines.ContainsKey(p.Id)) continue;
                var pose = PartyPose(p.Slot, e.To, facing, look);
                _anchor[p.Id] = pose;
                _target[p.Id] = pose;
            }
        }

        /// <summary>
        /// The attacker steps into the blow. Reconcile at the end of the step puts them back,
        /// so this needs no timer of its own — and losing the beat costs nothing.
        /// </summary>
        private void LeanInto(LogEntry e)
        {
            if (e.By == null || e.At == null) return;

            GameObject victim;
            Placement home;
            if (!_figurines.TryGetValue(e.At, out victim)) return;
            if (!_anchor.TryGetValue(e.By, out home)) return;

            var toward = victim.transform.position - home.Position;
            toward.y = 0f;
            if (toward.sqrMagnitude < 1e-8f) return;

            home.Position += toward.normalized * (cellSize * 0.3f);
            _target[e.By] = home;
        }

        /// <summary>Tip the standee over where it stands, so the kill reads before Reconcile.</summary>
        private void Topple(LogEntry e)
        {
            if (e.At == null) return;

            Placement pose;
            if (!_anchor.TryGetValue(e.At, out pose)) return;

            pose.Rotation = Toppled(pose.Rotation);
            _anchor[e.At] = pose;
            _target[e.At] = pose;
        }

        private void RecomputePartyCentre(Snapshot s)
        {
            var sum = Vector3.zero;
            var n = 0;

            foreach (var p in s.Party)
            {
                Placement pose;
                if (!_anchor.TryGetValue(p.Id, out pose)) continue;
                sum += pose.Position;
                n++;
            }

            PartyCentre = n > 0 ? sum / n : (Vector3?)null;
        }

        /// <summary>Grid directions. +x is east, +z is north; the grid runs south as y grows.</summary>
        private static Vector3 FacingVector(string facing)
        {
            switch (facing)
            {
                case "East":  return Vector3.right;
                case "West":  return Vector3.left;
                case "North": return Vector3.forward;
                default:      return Vector3.back;   // South, and anything unrecognised
            }
        }

        private static Vector3 RightOf(Vector3 facing)
        {
            return new Vector3(facing.z, 0f, -facing.x);
        }

        /// <summary>
        /// Turn a direction into a figure's rotation.
        ///
        /// The sculpts do not face down their own +Z, so pointing them with a plain LookRotation
        /// stood the whole party with its back to the way it was marching. <see cref="standeeYaw"/>
        /// is the correction, kept as a field because a different set of figures may well be
        /// modelled facing the other way.
        /// </summary>
        private Quaternion Face(Vector3 direction)
        {
            return Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, standeeYaw, 0f);
        }

        /// <summary>
        /// Topple around the figure's own base rather than resetting rotation, so the facing
        /// set during placement survives.
        /// </summary>
        private static Quaternion Toppled(Quaternion upright)
        {
            return Quaternion.Euler(90f, upright.eulerAngles.y, 0f);
        }
    }
}
