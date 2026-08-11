// Unity-only. A debug recorder: dumps the game view to a PNG sequence so a run can be turned
// into a video with ffmpeg afterwards.
//
// Deliberately wall-clock paced rather than using Time.captureFramerate. That setting gives
// perfectly even frames by slowing Unity's clock to match capture cost — but the scenario arrives
// over HTTP in real time, so a slowed clock would let snapshots pile up and the viewer's catch-up
// rule would compress the very beats we are trying to film.

using System.IO;
using UnityEngine;

namespace WizardryViewer.Unity
{
    public sealed class FrameGrabber : MonoBehaviour
    {
        [SerializeField] private string folder;
        [SerializeField] private float framesPerSecond = 20f;
        [SerializeField] private int maxFrames = 1200;

        private float _next;
        private int _frame;

        public int Captured { get { return _frame; } }
        public bool Finished { get; private set; }

        public void Begin(string outputFolder, float fps, int limit)
        {
            folder = outputFolder;
            framesPerSecond = fps;
            maxFrames = limit;
            _frame = 0;
            _next = 0f;
            Finished = false;
            Directory.CreateDirectory(folder);
            enabled = true;
        }

        private void Update()
        {
            if (Finished || string.IsNullOrEmpty(folder)) return;

            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f / Mathf.Max(1f, framesPerSecond);

            if (_frame >= maxFrames)
            {
                Finished = true;
                Debug.Log($"[grab] hit the {maxFrames}-frame limit");
                return;
            }

            _frame++;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"frame_{_frame:D4}.png"));
        }

        public void End()
        {
            Finished = true;
            Debug.Log($"[grab] {_frame} frames -> {folder}");
        }
    }
}
