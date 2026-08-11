using System;

namespace WizardryViewer.Unity
{
    /// <summary>
    /// How snapshots arrive. Abstracted for one reason: <see cref="HttpListener"/> is not
    /// dependable on Android/IL2CPP, so a Quest build will need a plain TcpListener instead.
    /// Nothing above this interface knows or cares which is in use.
    ///
    /// Implementations must:
    ///   * never block the caller of <see cref="Start"/>
    ///   * raise <see cref="Received"/> from whatever thread they like — the host marshals
    ///   * respond to the sender immediately and unconditionally, so the game never waits
    /// </summary>
    public interface ISnapshotTransport : IDisposable
    {
        /// <summary>Raw JSON payload, one per snapshot. May fire on a background thread.</summary>
        event Action<string> Received;

        /// <summary>Human-readable endpoint for logging, e.g. "http://127.0.0.1:8787/state".</summary>
        string Endpoint { get; }

        bool IsListening { get; }

        void Start();
        void Stop();
    }
}
