using UnityEngine;

namespace WizardryViewer.Unity
{
    /// <summary>
    /// Puts the dungeon in the dark and gives the party a torch to carry.
    ///
    /// This is not only atmosphere. The game will not tell you a monster is on a square until you
    /// step onto it, which looks absurd on a lit table where you can plainly see the whole level.
    /// Lit by a torch instead, the same behaviour reads correctly: the party genuinely cannot see
    /// what is waiting two squares ahead, because nothing out there is lit.
    ///
    /// The room lights are dimmed rather than switched off, so the table itself stays legible as an
    /// object even when the dungeon on it is dark.
    /// </summary>
    public sealed class TorchLight : MonoBehaviour
    {
        [SerializeField] private TableRenderer table;

        [Tooltip("The warm lamp over the table.")]
        [SerializeField] private Light roomLamp;

        [Tooltip("The cool fill light for the room.")]
        [SerializeField] private Light roomFill;

        [Tooltip("How much of the room lighting survives underground.")]
        [SerializeField] private float undergroundRoomScale = 0.14f;

        [Tooltip("How much of the ambient survives underground.")]
        [SerializeField] private float undergroundAmbientScale = 0.18f;

        // Small numbers, because the scale is small. A point light falls off with the square of the
        // distance, and the floor is roughly a centimetre away: intensity 2 blew the tiles out to
        // pure white and reduced the figures to silhouettes.
        [SerializeField] private float torchIntensity = 0.09f;

        [Tooltip("Torch reach, in dungeon cells. Beyond this a monster is simply not visible.")]
        [SerializeField] private float torchRangeCells = 4.5f;

        [Tooltip("How high above the table the flame sits, in cells. Higher spreads the pool of light.")]
        [SerializeField] private float torchHeightCells = 1.3f;

        [SerializeField] private float cellSize = 0.0254f;

        [Tooltip("Seconds to fade between lit room and dark dungeon.")]
        [SerializeField] private float fadeSeconds = 0.8f;

        private Light _torch;
        private float _lampBase = -1f;
        private float _fillBase = -1f;
        private Color _ambientBase;
        private float _blend;                 // 0 = room lit, 1 = underground

        private void Awake()
        {
            if (roomLamp != null) _lampBase = roomLamp.intensity;
            if (roomFill != null) _fillBase = roomFill.intensity;
            _ambientBase = RenderSettings.ambientLight;

            var go = new GameObject("PartyTorch");
            go.transform.SetParent(transform, false);

            _torch = go.AddComponent<Light>();
            _torch.type = LightType.Point;
            _torch.color = new Color(1f, 0.72f, 0.38f);      // flame, not daylight
            _torch.range = cellSize * torchRangeCells;
            _torch.intensity = 0f;
            _torch.shadows = LightShadows.Soft;              // walls should block it
            _torch.enabled = false;
        }

        private void OnDestroy()
        {
            // Ambient is global state. Leaving the scene dark after this component goes away would
            // be someone else's mysterious bug later.
            RenderSettings.ambientLight = _ambientBase;
            if (roomLamp != null && _lampBase >= 0f) roomLamp.intensity = _lampBase;
            if (roomFill != null && _fillBase >= 0f) roomFill.intensity = _fillBase;
        }

        private void Update()
        {
            if (table == null) return;

            // Underground means a dungeon level is laid. Before anything is laid at all we are not
            // underground either, so an idle table stays lit rather than sitting in the dark.
            var underground = !table.IsTownBoard && table.LaidBounds.HasValue;

            var step = fadeSeconds <= 0f ? 1f : Time.deltaTime / fadeSeconds;
            _blend = Mathf.MoveTowards(_blend, underground ? 1f : 0f, step);

            if (roomLamp != null && _lampBase >= 0f)
                roomLamp.intensity = Mathf.Lerp(_lampBase, _lampBase * undergroundRoomScale, _blend);

            if (roomFill != null && _fillBase >= 0f)
                roomFill.intensity = Mathf.Lerp(_fillBase, _fillBase * undergroundRoomScale, _blend);

            RenderSettings.ambientLight = Color.Lerp(_ambientBase, _ambientBase * undergroundAmbientScale, _blend);

            if (_torch == null) return;

            _torch.enabled = _blend > 0.01f;
            if (!_torch.enabled) return;

            // Carried at about chest height on whoever is leading. With no party on the table the
            // torch simply stays where it was rather than snapping to the origin.
            var centre = table.PartyCentre;
            if (centre.HasValue)
                _torch.transform.position = centre.Value + Vector3.up * (cellSize * torchHeightCells);

            // Flicker: two noise samples at different rates, so it wavers rather than pulsing on a
            // recognisable cycle. Kept modest -- a strobing table is unpleasant to watch.
            var t = Time.time;
            var flicker = 0.88f
                        + Mathf.PerlinNoise(t * 5.5f, 0.3f) * 0.18f
                        + Mathf.PerlinNoise(t * 13f, 7.1f) * 0.06f;

            _torch.intensity = torchIntensity * _blend * flicker;
            _torch.range = cellSize * torchRangeCells * (0.97f + flicker * 0.03f);
        }
    }
}
