using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using WizardryViewer.Playback;
using WizardryViewer.Presentation;
using WizardryViewer.Protocol;

namespace WizardryViewer.Unity
{
    /// <summary>
    /// Owns the transport and the playback clock. Snapshots arrive on a background thread,
    /// get queued, and are drained on the main thread where Unity objects can be touched.
    /// </summary>
    public sealed class ViewerReceiver : MonoBehaviour
    {
        [Header("Transport")]
        [SerializeField] private int port = 8787;
        [Tooltip("Loopback only. A Quest build would clear this, since the game is elsewhere.")]
        [SerializeField] private bool loopbackOnly = true;

        [Header("Pacing")]
        [SerializeField] private float beatSeconds = 0.80f;
        [SerializeField] private float compressedBeatSeconds = 0.25f;

        [Header("Presentation")]
        [SerializeField] private bool swedish;
        [SerializeField] private TableRenderer table;
        [SerializeField] private DmSubtitle subtitle;

        public PlaybackController Playback { get; private set; }
        public string Endpoint => _transport != null ? _transport.Endpoint : "(not started)";
        public bool Connected => _transport != null && _transport.IsListening;

        private ISnapshotTransport _transport;
        private Narrator _narrator;

        private readonly Queue<Snapshot> _inbox = new Queue<Snapshot>();
        private readonly object _inboxGate = new object();
        private readonly Dictionary<string, string> _displayNames = new Dictionary<string, string>();

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore, // tolerate fields we predate
        };

        private void Awake()
        {
            EnsureInitialised();
        }

        /// <summary>
        /// Recompiling while in play mode reloads the domain: serialised fields survive, but
        /// anything built in Awake comes back null and Awake is NOT called again. Update would
        /// then throw every frame and the transport would be gone, so initialisation has to be
        /// re-entrant and driven from Update rather than assumed to have happened once.
        /// </summary>
        private void EnsureInitialised()
        {
            if (Playback != null) return;

            Playback = new PlaybackController
            {
                BeatSeconds = beatSeconds,
                CompressedBeatSeconds = compressedBeatSeconds,
            };

            _narrator = new Narrator(
                swedish ? new SwedishVocabulary() : new Vocabulary(),
                id => _displayNames.TryGetValue(id, out var n) ? n : id);

            Playback.Beat += OnBeat;
            Playback.Reconcile += OnReconcile;
            Playback.Skipped += n => Debug.Log($"[viewer] caught up, dropped {n} beats");

            _transport = new HttpSnapshotTransport(port, loopbackOnly);
            _transport.Received += OnPayload;

            try
            {
                _transport.Start();
                Debug.Log($"[viewer] listening on {_transport.Endpoint}");
            }
            catch (Exception ex)
            {
                // Almost always an accept thread from a previous domain still holding the port.
                // It cannot be collected — the running thread roots it — so the only cure once
                // it exists is restarting Unity. beforeAssemblyReload below prevents it.
                Debug.LogError($"[viewer] could not start transport on port {port}: {ex.Message}. " +
                               "A listener from a previous domain is probably still bound; " +
                               "restart the editor or use another port.");
            }

#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ShutdownTransport;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ShutdownTransport;
#endif
        }

        /// <summary>Background thread. Parse and queue; never touch Unity objects here.</summary>
        private void OnPayload(string json)
        {
            try
            {
                var snapshot = JsonConvert.DeserializeObject<Snapshot>(json, JsonSettings);
                if (snapshot == null) return;
                lock (_inboxGate) _inbox.Enqueue(snapshot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[viewer] undecodable payload: {ex.Message}");
            }
        }

        private void Update()
        {
            EnsureInitialised();   // cheap null check; see the note on domain reloads

            while (true)
            {
                Snapshot next;
                lock (_inboxGate)
                {
                    if (_inbox.Count == 0) break;
                    next = _inbox.Dequeue();
                }

                CacheDisplayNames(next);
                Playback.Receive(next);
            }

            Playback.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Ids are stable but not readable. Mapping them to words is a viewer concern, so it
        /// lives here and not in the protocol.
        /// </summary>
        private void CacheDisplayNames(Snapshot s)
        {
            foreach (var p in s.Party)
                _displayNames[p.Id] = p.Name;

            if (s.Encounter == null) return;

            foreach (var g in s.Encounter.Groups)
            {
                _displayNames[Ids.Group(g.GroupId)] = g.MonsterId;
                foreach (var m in g.Members)
                    _displayNames[m.Id] = $"{g.MonsterId} #{m.Index}";
            }
        }

        private void OnBeat(LogEntry entry, Snapshot context)
        {
            var line = _narrator.Describe(entry);   // null is normal: not everything needs words
            if (line != null && subtitle != null) subtitle.Say(line);
            if (table != null) table.PlayBeat(entry, context, line);
        }

        private void OnReconcile(Snapshot snapshot)
        {
            if (table != null) table.Reconcile(snapshot);
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ShutdownTransport;
#endif
            ShutdownTransport();
        }

        /// <summary>
        /// Releases the port and stops the accept thread. Must run BEFORE an assembly reload:
        /// a domain reload does not stop background threads, so a listener left running would
        /// keep the port bound and go on answering 204 to POSTs it queues into an object the
        /// reload has already orphaned — the sender sees success while the table never updates.
        /// </summary>
        private void ShutdownTransport()
        {
            if (_transport == null) return;
            _transport.Received -= OnPayload;
            _transport.Dispose();
            _transport = null;
        }
    }
}
