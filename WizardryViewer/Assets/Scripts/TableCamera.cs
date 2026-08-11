// Unity-only. Presentation.
//
// Frames the party at a seated player's distance. The dungeon is rendered at honest 1:1
// miniature scale — one inch per cell — so a whole corridor is only a few centimetres of
// table. Framing the entire tabletop therefore shows a postage stamp; the camera has to come
// in close and follow instead.
//
// This moves the RIG, never the camera, because that is what an XR rig does: the headset owns
// the camera's pose inside the rig, and the application owns where the rig stands.

using UnityEngine;

namespace WizardryViewer.Unity
{
    public sealed class TableCamera : MonoBehaviour
    {
        /// <summary>
        /// At 1:1 scale these two cannot be the same shot. Ten cells is close enough to tell a
        /// priest from a thief; a 22x22 level is 56cm of table and reduces a figure to a speck.
        /// So it is a toggle, not a compromise.
        /// </summary>
        public enum Framing { FollowParty, WholeLevel }

        [SerializeField] private TableRenderer table;

        [Header("Framing")]
        [SerializeField] private Framing framing = Framing.FollowParty;
        [Tooltip("Press to switch between following the party and seeing the whole level.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        [Tooltip("Fraction of the frame the level should fill in WholeLevel.")]
        [SerializeField] private float levelFill = 0.85f;

        [Header("Seat")]
        [Tooltip("True metres from the party to the camera — not a horizontal offset, so the visible " +
                 "area follows from it directly. At 40 degrees vertical FOV, 0.23 shows about 6.5 cells.")]
        [SerializeField] private float distance = 0.23f;
        [Tooltip("Degrees below horizontal. Shallow enough to see figures side-on rather than from above.")]
        [SerializeField] private float pitchDegrees = 38f;

        [Header("Follow")]
        [Tooltip("Seconds to settle. Long enough not to chase every step, short enough to keep up.")]
        [SerializeField] private float followSeconds = 0.5f;
        [Tooltip("Metres of party movement to ignore, so a single figure shuffling does not drift " +
                 "the whole view.")]
        [SerializeField] private float deadZone = 0.01f;
        [Tooltip("How far the view may hang past the edge of the level, as a fraction of a half-window. " +
                 "0 keeps every pixel on the dungeon but pushes an edge-hugging party to the side; " +
                 "1 ignores the level outline entirely and always centres the party.")]
        [Range(0f, 1f)]
        [SerializeField] private float edgeOverhang = 0.55f;

        private Vector3 _velocity;
        private Vector3 _focus;
        private bool _hasFocus;

        // The view stays on one side of the table rather than swinging round behind the party:
        // a camera that rotates with facing makes a dungeon crawl unreadable, and in VR it
        // would be worse than unreadable.
        private static readonly Vector3 ViewFrom = new Vector3(0f, 0f, -1f);

        private void LateUpdate()
        {
            if (table == null) return;

            if (Input.GetKeyDown(toggleKey))
            {
                framing = framing == Framing.FollowParty ? Framing.WholeLevel : Framing.FollowParty;
                _hasFocus = false;   // re-seat immediately rather than gliding across the table
            }

            var centre = framing == Framing.WholeLevel ? LevelFocus() : table.PartyCentre;
            if (centre == null) return;

            if (!_hasFocus)
            {
                _focus = centre.Value;
                _hasFocus = true;
                transform.position = Seat(_focus);
                transform.rotation = Aim(transform.position, _focus);
                return;
            }

            if ((centre.Value - _focus).magnitude > deadZone)
                _focus = centre.Value;

            if (framing == Framing.FollowParty) _focus = KeepOnLevel(_focus);

            transform.position = Vector3.SmoothDamp(transform.position, Seat(_focus), ref _velocity, followSeconds);
            transform.rotation = Aim(transform.position, _focus);
        }

        /// <summary>
        /// Pull the aim point back inside the level. The party starts against the west wall, and
        /// centring on them there spends half the frame on bare tabletop; this trades a perfectly
        /// centred party for a frame that is all dungeon. No-op once they walk inland.
        /// </summary>
        private Vector3 KeepOnLevel(Vector3 focus)
        {
            var bounds = table.LaidBounds;
            if (bounds == null) return focus;

            var cam = GetComponentInChildren<Camera>();
            var fov = cam != null ? cam.fieldOfView : 40f;
            var aspect = cam != null ? cam.aspect : 1.6f;

            var visibleHeight = 2f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var visibleWidth = visibleHeight * aspect;

            focus.x = ClampSpan(focus.x, bounds.Value.min.x, bounds.Value.max.x, visibleWidth);
            focus.z = ClampSpan(focus.z, bounds.Value.min.z, bounds.Value.max.z, visibleHeight);
            return focus;
        }

        /// <summary>
        /// Keep a window of the given width inside [min,max], centring if it will not fit. The
        /// window is allowed to hang over the edge by <see cref="edgeOverhang"/> of a half-window:
        /// clamping it fully inside shoves a party standing at the west wall right to the side of
        /// the frame, which is worse than showing a strip of bare table.
        /// </summary>
        private float ClampSpan(float value, float min, float max, float window)
        {
            var half = window * 0.5f * (1f - edgeOverhang);
            if (max - min <= half * 2f) return (min + max) * 0.5f;
            return Mathf.Clamp(value, min + half, max - half);
        }

        private Vector3? LevelFocus()
        {
            var bounds = table.LaidBounds;
            return bounds == null ? (Vector3?)null : bounds.Value.center;
        }

        /// <summary>Camera position for a given aim point: back along the view axis and up by the pitch.</summary>
        private Vector3 FollowSeat(Vector3 focus)
        {
            var pitch = pitchDegrees * Mathf.Deg2Rad;
            var back = ViewFrom * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
            return focus + back * distance;
        }

        private Vector3 Seat(Vector3 focus)
        {
            if (framing == Framing.FollowParty) return FollowSeat(focus);

            // Back off until the level fits. Vertical FOV is the binding constraint on a wide
            // window; on a tall one it is horizontal, so check both and take the further seat.
            var bounds = table.LaidBounds;
            if (bounds == null) return FollowSeat(focus);

            var cam = GetComponentInChildren<Camera>();
            var fov = cam != null ? cam.fieldOfView : 40f;
            var aspect = cam != null ? cam.aspect : 1.6f;

            var size = bounds.Value.size;
            var extent = Mathf.Max(size.x, size.z) / Mathf.Max(0.01f, levelFill);

            var vertical = extent * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var horizontalFov = 2f * Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * aspect);
            var horizontal = extent * 0.5f / Mathf.Tan(horizontalFov * 0.5f);
            var back = Mathf.Max(vertical, horizontal);

            // Steeper than the seated view: a map wants to be looked down on, not across.
            return focus + ViewFrom * (back * 0.55f) + Vector3.up * (back * 0.85f);
        }

        private static Quaternion Aim(Vector3 from, Vector3 focus)
        {
            var forward = focus - from;
            return forward.sqrMagnitude < 1e-8f
                ? Quaternion.identity
                : Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
