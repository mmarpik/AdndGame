using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace WizardryViewer.Unity
{
    /// <summary>
    /// Desktop transport. Accepts POSTs and answers 204 immediately, before doing anything
    /// with the body, so a slow viewer can never stall the game.
    ///
    /// Not for Quest: System.Net.HttpListener is unreliable under IL2CPP on Android. When
    /// that day comes, add TcpSnapshotTransport implementing the same interface — and bind
    /// to 0.0.0.0 rather than loopback, since the game will then be on another machine.
    /// </summary>
    public sealed class HttpSnapshotTransport : ISnapshotTransport
    {
        private readonly string _prefix;
        private HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public event Action<string> Received;

        public string Endpoint => _prefix + "state";
        public bool IsListening => _running;

        public HttpSnapshotTransport(int port, bool loopbackOnly = true)
        {
            var host = loopbackOnly ? "127.0.0.1" : "+";
            _prefix = $"http://{host}:{port}/";
        }

        public void Start()
        {
            if (_running) return;

            _listener = new HttpListener();
            _listener.Prefixes.Add(_prefix);
            _listener.Start();

            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "WizardryViewer transport" };
            _thread.Start();
        }

        private void Loop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext();
                }
                catch
                {
                    return; // stopped
                }

                string body = null;
                try
                {
                    using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                        body = reader.ReadToEnd();
                }
                catch
                {
                    // fall through: still answer, still don't block the sender
                }

                try
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                }
                catch
                {
                    // sender already gave up on us; that is explicitly allowed
                }

                if (!string.IsNullOrEmpty(body))
                    Received?.Invoke(body);
            }
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
        }

        public void Dispose() => Stop();
    }
}
