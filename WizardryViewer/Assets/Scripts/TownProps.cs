using UnityEngine;

namespace WizardryViewer.Unity
{
    /// <summary>
    /// Builds a little piece of terrain over each town pad — a colonnaded temple, a tavern, a
    /// market stall, a practice yard, and the gate at the edge of town.
    ///
    /// Everything here is deliberately ROOFLESS, and every enclosure has a doorway facing the
    /// camera. The table is viewed from above, so a solid roof would hide the very thing the
    /// buildings are meant to frame. Tabletop terrain is built the same way and for the same
    /// reason: you need to see the figures standing inside.
    ///
    /// Scale is taken from the minis rather than guessed. A figure is about 13mm on a 25.4mm cell
    /// and the dungeon's own walls are 17mm, so walls here sit near that and columns a little
    /// taller, which lets a temple read as grander without dwarfing anyone.
    /// </summary>
    public sealed class TownProps : MonoBehaviour
    {
        [SerializeField] private Material stone;
        [SerializeField] private Material slate;
        [SerializeField] private Material wood;
        [SerializeField] private Material darkWood;
        [SerializeField] private Material steel;
        [SerializeField] private Material gold;
        [SerializeField] private Material paleStone;
        [SerializeField] private Material cloth;

        /// <summary>
        /// Build the props for a place. Returns null for an id with nothing built for it, so an
        /// unrecognised location still shows its bare pad rather than nothing at all.
        /// </summary>
        public GameObject Build(string locationId, Vector3 padCentre, float cell)
        {
            if (string.IsNullOrEmpty(locationId)) return null;

            var root = new GameObject("Props:" + locationId);
            root.transform.SetParent(transform, false);
            root.transform.position = padCentre;

            switch (locationId)
            {
                case "Temple":          BuildTemple(root.transform, cell); break;
                case "Tavern":          BuildTavern(root.transform, cell); break;
                case "Shop":            BuildShop(root.transform, cell); break;
                case "TrainingGrounds": BuildTrainingYard(root.transform, cell); break;
                case "EdgeOfTown":      BuildGate(root.transform, cell); break;
                default:
                    Destroy(root);
                    return null;
            }

            return root;
        }

        // ---------------------------------------------------------------- places

        /// <summary>
        /// A colonnade on a stepped plinth, with an altar at the back. No walls at all: the party
        /// is visible between every pair of columns, which is what makes this the clearest of the
        /// five to read from above.
        /// </summary>
        private void BuildTemple(Transform root, float c)
        {
            var colH = c * 0.85f;      // ~21mm, comfortably above a 13mm figure
            var colD = c * 0.16f;
            var half = c * 0.78f;      // columns just inside the 2x2 pad

            // Two shallow steps, so it sits on something rather than floating on the tiles.
            Box(root, new Vector3(0, c * 0.02f, 0), new Vector3(c * 1.80f, c * 0.04f, c * 1.80f), paleStone, "Plinth");
            Box(root, new Vector3(0, c * 0.05f, 0), new Vector3(c * 1.66f, c * 0.03f, c * 1.66f), paleStone, "Step");

            // Columns around the edge, with the middle of the front row left out for the entrance.
            for (int ix = -1; ix <= 1; ix++)
            {
                for (int iz = -1; iz <= 1; iz++)
                {
                    if (ix == 0 && iz == 0) continue;                 // nave stays clear
                    if (ix == 0 && iz == -1) continue;                // doorway, facing the camera
                    var pos = new Vector3(ix * half, c * 0.07f + colH * 0.5f, iz * half);
                    Cylinder(root, pos, colD, colH, paleStone, $"Column_{ix}_{iz}");
                }
            }

            // Architrave: four beams resting on the columns, centre left open to the sky.
            var beamY = c * 0.07f + colH + c * 0.04f;
            var beamT = c * 0.08f;
            Box(root, new Vector3(0, beamY, -half), new Vector3(c * 1.72f, beamT, colD), paleStone, "Architrave_S");
            Box(root, new Vector3(0, beamY,  half), new Vector3(c * 1.72f, beamT, colD), paleStone, "Architrave_N");
            Box(root, new Vector3(-half, beamY, 0), new Vector3(colD, beamT, c * 1.72f), paleStone, "Architrave_W");
            Box(root, new Vector3( half, beamY, 0), new Vector3(colD, beamT, c * 1.72f), paleStone, "Architrave_E");

            // A pediment over the entrance, so the front reads as the front.
            Box(root, new Vector3(0, beamY + beamT * 0.9f, -half), new Vector3(c * 0.9f, beamT * 0.9f, colD * 0.9f), slate, "Pediment");

            // Altar at the back, with a gold top: the one bright thing on the board.
            Box(root, new Vector3(0, c * 0.07f + c * 0.13f, half * 0.55f), new Vector3(c * 0.5f, c * 0.26f, c * 0.3f), stone, "Altar");
            Box(root, new Vector3(0, c * 0.07f + c * 0.27f, half * 0.55f), new Vector3(c * 0.58f, c * 0.03f, c * 0.36f), gold, "AltarTop");
        }

        /// <summary>
        /// Three walls and a wide doorway, plus a window band on one side. Open topped, so the
        /// party inside is visible from above and through the door from the front.
        /// </summary>
        private void BuildTavern(Transform root, float c)
        {
            var h = c * 0.62f;         // ~16mm, near the dungeon wall height
            var t = c * 0.08f;
            var half = c * 0.85f;

            // Back and one side solid.
            Box(root, new Vector3(0, h * 0.5f, half), new Vector3(c * 1.78f, h, t), darkWood, "Wall_Back");
            Box(root, new Vector3(-half, h * 0.5f, 0), new Vector3(t, h, c * 1.78f), darkWood, "Wall_West");

            // The other side gets a window: a low sill and a lintel with a gap between them.
            Box(root, new Vector3(half, h * 0.22f, 0), new Vector3(t, h * 0.44f, c * 1.78f), darkWood, "Wall_East_Sill");
            Box(root, new Vector3(half, h * 0.92f, 0), new Vector3(t, h * 0.16f, c * 1.78f), darkWood, "Wall_East_Lintel");
            Box(root, new Vector3(half, h * 0.66f, -c * 0.62f), new Vector3(t, h * 0.36f, c * 0.5f), darkWood, "Wall_East_Mullion");

            // Front wall in two pieces, leaving a doorway in the middle facing the camera.
            Box(root, new Vector3(-c * 0.62f, h * 0.5f, -half), new Vector3(c * 0.56f, h, t), darkWood, "Wall_Front_L");
            Box(root, new Vector3( c * 0.62f, h * 0.5f, -half), new Vector3(c * 0.56f, h, t), darkWood, "Wall_Front_R");
            Box(root, new Vector3(0, h * 0.90f, -half), new Vector3(c * 0.70f, h * 0.20f, t), darkWood, "Door_Lintel");

            // A bench and a table inside, tucked to the back so they do not crowd the figures.
            Cylinder(root, new Vector3(-c * 0.45f, c * 0.12f, c * 0.45f), c * 0.34f, c * 0.24f, wood, "Table");
            Box(root, new Vector3(c * 0.5f, c * 0.09f, c * 0.5f), new Vector3(c * 0.5f, c * 0.06f, c * 0.16f), wood, "Bench");

            // Hanging sign out front, on a bracket.
            Cylinder(root, new Vector3(-c * 1.02f, h * 0.62f, -half - c * 0.1f), c * 0.06f, h * 1.24f, wood, "SignPost");
            Box(root, new Vector3(-c * 0.86f, h * 1.16f, -half - c * 0.1f), new Vector3(c * 0.34f, c * 0.04f, c * 0.04f), wood, "SignArm");
            Box(root, new Vector3(-c * 0.80f, h * 0.96f, -half - c * 0.1f), new Vector3(c * 0.30f, c * 0.28f, c * 0.03f), cloth, "SignBoard");
        }

        /// <summary>
        /// A market stall: counter at the front, awning over it, stock behind. Open on every side,
        /// so nothing is hidden at all.
        /// </summary>
        private void BuildShop(Transform root, float c)
        {
            var postH = c * 0.78f;
            var half = c * 0.8f;

            // Counter across the front, with a gap at one end to step through.
            Box(root, new Vector3(-c * 0.28f, c * 0.16f, -half), new Vector3(c * 1.1f, c * 0.32f, c * 0.16f), wood, "Counter");
            Box(root, new Vector3(-c * 0.28f, c * 0.34f, -half), new Vector3(c * 1.18f, c * 0.04f, c * 0.22f), darkWood, "CounterTop");

            // Four posts and a canted awning above them.
            foreach (var sx in new[] { -1f, 1f })
            {
                Cylinder(root, new Vector3(sx * half, postH * 0.5f, -half), c * 0.07f, postH, wood, $"Post_F{sx}");
                Cylinder(root, new Vector3(sx * half, postH * 0.5f, half * 0.2f), c * 0.07f, postH, wood, $"Post_B{sx}");
            }

            // The awning only oversails the counter. A wider one looked better in isolation but
            // covered the middle of the pad, and the party standing there vanished under it.
            var awning = Box(root, new Vector3(0, postH + c * 0.05f, -half * 0.78f),
                             new Vector3(c * 1.8f, c * 0.035f, c * 0.55f), cloth, "Awning");
            awning.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);

            // Stock at the back: crates and a barrel.
            Box(root, new Vector3(-c * 0.55f, c * 0.16f, half * 0.75f), new Vector3(c * 0.32f, c * 0.32f, c * 0.32f), wood, "Crate_A");
            Box(root, new Vector3(-c * 0.2f, c * 0.12f, half * 0.8f), new Vector3(c * 0.26f, c * 0.24f, c * 0.26f), darkWood, "Crate_B");
            Cylinder(root, new Vector3(c * 0.55f, c * 0.19f, half * 0.75f), c * 0.3f, c * 0.38f, darkWood, "Barrel");

            // A rack of blades, since this is where the armour comes from.
            Box(root, new Vector3(c * 0.75f, c * 0.4f, half * 0.1f), new Vector3(c * 0.05f, c * 0.8f, c * 0.05f), wood, "Rack");
            for (int i = 0; i < 3; i++)
                Box(root, new Vector3(c * (0.62f + i * 0.12f), c * 0.42f, half * 0.1f),
                    new Vector3(c * 0.03f, c * 0.62f, c * 0.02f), steel, "Blade_" + i);
        }

        /// <summary>
        /// A fenced practice yard with pells and a weapon rack. The fence is deliberately low so it
        /// never hides anyone; this is the place the party is most exposed to view.
        /// </summary>
        private void BuildTrainingYard(Transform root, float c)
        {
            var postH = c * 0.4f;
            var half = c * 0.85f;

            // Fence posts around the edge, with the front middle left open as a gateway.
            for (int i = -2; i <= 2; i++)
            {
                var f = i * (half / 2f);
                Cylinder(root, new Vector3(f, postH * 0.5f, half), c * 0.06f, postH, darkWood, $"PostN_{i}");
                if (i != 0) Cylinder(root, new Vector3(f, postH * 0.5f, -half), c * 0.06f, postH, darkWood, $"PostS_{i}");
                Cylinder(root, new Vector3(-half, postH * 0.5f, f), c * 0.06f, postH, darkWood, $"PostW_{i}");
                Cylinder(root, new Vector3( half, postH * 0.5f, f), c * 0.06f, postH, darkWood, $"PostE_{i}");
            }

            // Two rails per side, skipping the gateway span at the front.
            foreach (var y in new[] { postH * 0.45f, postH * 0.8f })
            {
                Box(root, new Vector3(0, y, half), new Vector3(half * 2f, c * 0.03f, c * 0.03f), darkWood, "RailN");
                Box(root, new Vector3(-half, y, 0), new Vector3(c * 0.03f, c * 0.03f, half * 2f), darkWood, "RailW");
                Box(root, new Vector3( half, y, 0), new Vector3(c * 0.03f, c * 0.03f, half * 2f), darkWood, "RailE");
                Box(root, new Vector3(-half * 0.62f, y, -half), new Vector3(half * 0.7f, c * 0.03f, c * 0.03f), darkWood, "RailS_L");
                Box(root, new Vector3( half * 0.62f, y, -half), new Vector3(half * 0.7f, c * 0.03f, c * 0.03f), darkWood, "RailS_R");
            }

            // Two pells to hack at: post, crossbar, straw head.
            foreach (var sx in new[] { -0.5f, 0.5f })
            {
                var x = sx * c;
                Cylinder(root, new Vector3(x, c * 0.34f, c * 0.5f), c * 0.09f, c * 0.68f, wood, "Pell");
                Box(root, new Vector3(x, c * 0.56f, c * 0.5f), new Vector3(c * 0.44f, c * 0.04f, c * 0.04f), wood, "PellArms");
                Sphere(root, new Vector3(x, c * 0.70f, c * 0.5f), c * 0.12f, wood, "PellHead");
            }

            // Weapon rack by the gate.
            Box(root, new Vector3(-half * 0.8f, c * 0.2f, -c * 0.35f), new Vector3(c * 0.06f, c * 0.4f, c * 0.5f), darkWood, "Rack");
            for (int i = 0; i < 3; i++)
                Box(root, new Vector3(-half * 0.8f, c * 0.34f, -c * (0.5f + i * 0.14f)),
                    new Vector3(c * 0.02f, c * 0.5f, c * 0.02f), steel, "Spear_" + i);
        }

        /// <summary>
        /// The way out: a stone arch, a signpost, and the dark stair the party descends. Nothing
        /// encloses the pad, so the whole party stays in plain sight.
        /// </summary>
        private void BuildGate(Transform root, float c)
        {
            var postH = c * 0.95f;
            var half = c * 0.8f;

            foreach (var sx in new[] { -1f, 1f })
                Box(root, new Vector3(sx * half, postH * 0.5f, -half), new Vector3(c * 0.2f, postH, c * 0.2f), stone, $"GatePost{sx}");

            Box(root, new Vector3(0, postH + c * 0.07f, -half), new Vector3(half * 2f + c * 0.2f, c * 0.14f, c * 0.22f), stone, "Lintel");
            Box(root, new Vector3(0, postH + c * 0.2f, -half), new Vector3(c * 0.7f, c * 0.12f, c * 0.26f), slate, "Keystone");

            // The stair down, at the back: a dark recess rather than a hole, since the pad is solid.
            Box(root, new Vector3(0, c * 0.012f, half * 0.7f), new Vector3(c * 0.9f, c * 0.02f, c * 0.6f), slate, "StairMouth");
            for (int i = 0; i < 3; i++)
                Box(root, new Vector3(0, c * (0.03f + i * 0.02f), half * (0.52f + i * 0.11f)),
                    new Vector3(c * 0.82f, c * 0.02f, c * 0.12f), stone, "Step_" + i);

            // Signpost leaning out toward the viewer.
            Cylinder(root, new Vector3(-half * 1.05f, c * 0.42f, -half * 0.1f), c * 0.07f, c * 0.84f, darkWood, "SignPost");
            var board = Box(root, new Vector3(-half * 1.05f + c * 0.22f, c * 0.72f, -half * 0.1f),
                            new Vector3(c * 0.46f, c * 0.2f, c * 0.03f), wood, "SignBoard");
            board.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
        }

        // ---------------------------------------------------------------- primitives

        private GameObject Box(Transform parent, Vector3 localPos, Vector3 size, Material material, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Prepare(go, parent, localPos, material, name);
            go.transform.localScale = size;
            return go;
        }

        private GameObject Cylinder(Transform parent, Vector3 localPos, float diameter, float height, Material material, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Prepare(go, parent, localPos, material, name);
            // Unity's cylinder is 2 units tall, so half the height gives the scale.
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            return go;
        }

        private GameObject Sphere(Transform parent, Vector3 localPos, float diameter, Material material, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Prepare(go, parent, localPos, material, name);
            go.transform.localScale = Vector3.one * diameter;
            return go;
        }

        private void Prepare(GameObject go, Transform parent, Vector3 localPos, Material material, string name)
        {
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            // Colliders come free with the primitives and are pure overhead here: nothing on this
            // table is ever picked, raycast or simulated.
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            if (material != null)
                go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
