using TMPro;
using UnityEngine;

namespace WizardryViewer.Unity
{
    /// <summary>
    /// What the DM is saying, printed on a card lying next to the table.
    ///
    /// Deliberately a WORLD-space object, not a screen overlay: screen-space canvases don't
    /// render in VR, and this way the text is a physical thing on the table that a headset
    /// user could lean over and read. Costs nothing today, saves a rewrite later.
    ///
    /// It is also the only place words reach the player, so swapping to TTS later means
    /// changing this one class.
    /// </summary>
    public sealed class DmSubtitle : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float holdSeconds = 2.5f;
        [SerializeField] private float fadeSeconds = 0.4f;

        private float _remaining;

        public void Say(string line)
        {
            if (label == null) return;
            label.text = line;
            SetAlpha(1f);
            _remaining = holdSeconds + fadeSeconds;
        }

        private void Update()
        {
            if (label == null || _remaining <= 0f) return;

            _remaining -= Time.deltaTime;

            if (_remaining <= 0f)
            {
                SetAlpha(0f);
                label.text = string.Empty;
            }
            else if (_remaining < fadeSeconds)
            {
                SetAlpha(_remaining / fadeSeconds);
            }
        }

        private void SetAlpha(float a)
        {
            var c = label.color;
            c.a = a;
            label.color = c;
        }
    }
}
