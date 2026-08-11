// Unity has no STL importer, and the model pipeline only takes FBX/OBJ/glTF. STL is a trivial
// format though, so this is a ScriptedImporter: drop a .stl anywhere under Assets/ and it
// becomes a mesh asset like any other model, re-importing when the file changes.
//
// The defaults are aimed at miniatures, which is what STL is nearly always used for:
// millimetres, Z-up, and sitting on its own base rather than centred on the origin.

using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

namespace WizardryViewer.EditorTools
{
    [ScriptedImporter(2, "stl")]
    public sealed class StlImporter : ScriptedImporter
    {
        [Tooltip("Metres per STL unit. STL carries no units; printable models are almost always " +
                 "millimetres, so 0.001.")]
        public float unitScale = 0.001f;

        [Tooltip("STL is conventionally Z-up. Unity is Y-up, so axes are swapped and winding " +
                 "reversed to keep faces pointing outwards.")]
        public bool zUp = true;

        [Tooltip("Metres. Above zero, the mesh is scaled uniformly to exactly this tall — the " +
                 "quickest way to make a print-scale model usable at table scale.")]
        public float fitHeight;

        [Tooltip("Put the origin at the centre of the base, so the model stands on a tile " +
                 "instead of being buried halfway through it.")]
        public bool originAtBase = true;

        [Tooltip("Merge exactly-identical vertices. STL stores every triangle's corners separately, " +
                 "so this alone typically cuts vertex count by two thirds.")]
        public bool weldVertices = true;

        [Tooltip("Metres. Above zero, vertices are clustered onto a grid this fine and collapsed — " +
                 "a print-resolution model is around a million triangles, which is pointless for a " +
                 "figure 22mm tall. 0.0004 keeps the silhouette and loses the printer's detail.")]
        public float simplifyGrid = 0.0004f;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var bytes = File.ReadAllBytes(ctx.assetPath);
            var name = Path.GetFileNameWithoutExtension(ctx.assetPath);

            Vector3[] positions;
            try
            {
                positions = IsBinary(bytes) ? ReadBinary(bytes) : ReadAscii(bytes);
            }
            catch (Exception ex)
            {
                ctx.LogImportError($"[stl] could not read {name}: {ex.Message}");
                return;
            }

            if (positions.Length < 3)
            {
                ctx.LogImportError($"[stl] {name} contains no triangles");
                return;
            }

            var mesh = BuildMesh(name, positions);
            ctx.AddObjectToAsset("mesh", mesh);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            {
                name = name + " Material",
            };
            ctx.AddObjectToAsset("material", material);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;

            ctx.AddObjectToAsset("root", go);
            ctx.SetMainObject(go);
        }

        /// <summary>
        /// A binary STL is an 84-byte header plus exactly 50 bytes per triangle. Checking the
        /// arithmetic is far more reliable than looking for a leading "solid": plenty of binary
        /// exporters write that word into the header too.
        /// </summary>
        private static bool IsBinary(byte[] bytes)
        {
            if (bytes.Length < 84) return false;
            var triangles = BitConverter.ToUInt32(bytes, 80);
            return 84L + triangles * 50L == bytes.Length;
        }

        private Vector3[] ReadBinary(byte[] bytes)
        {
            var triangles = (int)BitConverter.ToUInt32(bytes, 80);
            var result = new Vector3[triangles * 3];

            var at = 84;
            for (int t = 0; t < triangles; t++)
            {
                at += 12;   // the facet normal, which we recompute rather than trust
                for (int v = 0; v < 3; v++)
                {
                    var x = BitConverter.ToSingle(bytes, at);
                    var y = BitConverter.ToSingle(bytes, at + 4);
                    var z = BitConverter.ToSingle(bytes, at + 8);
                    result[t * 3 + v] = Convert(x, y, z);
                    at += 12;
                }
                at += 2;    // attribute byte count
            }

            return result;
        }

        private Vector3[] ReadAscii(byte[] bytes)
        {
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var lines = text.Split('\n');
            var verts = new System.Collections.Generic.List<Vector3>(lines.Length / 7 * 3);

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (!line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase)) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;

                float x, y, z;
                if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) continue;
                if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) continue;
                if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) continue;

                verts.Add(Convert(x, y, z));
            }

            // Trailing partial triangle would throw off the winding; drop it.
            var whole = verts.Count - verts.Count % 3;
            var result = new Vector3[whole];
            verts.CopyTo(0, result, 0, whole);
            return result;
        }

        /// <summary>
        /// Vertex clustering: snap every vertex onto a grid, keep one averaged vertex per occupied
        /// cell, and drop triangles whose corners have collapsed together. Cheap, order-independent,
        /// and it preserves the outline — which is all that survives at 22mm anyway. A grid of zero
        /// degenerates to an exact weld.
        /// </summary>
        private static void Cluster(Mesh mesh, Vector3[] positions, float grid)
        {
            var cells = new System.Collections.Generic.Dictionary<Vector3Int, int>(positions.Length / 4);
            var sums = new System.Collections.Generic.List<Vector3>(positions.Length / 4);
            var counts = new System.Collections.Generic.List<int>(positions.Length / 4);
            var remap = new int[positions.Length];

            var inverse = grid > 0f ? 1f / grid : 0f;

            for (int i = 0; i < positions.Length; i++)
            {
                var p = positions[i];
                var key = grid > 0f
                    ? new Vector3Int(Mathf.RoundToInt(p.x * inverse), Mathf.RoundToInt(p.y * inverse), Mathf.RoundToInt(p.z * inverse))
                    // Exact weld: quantise at float precision so identical corners share a key.
                    : new Vector3Int(p.x.GetHashCode(), p.y.GetHashCode(), p.z.GetHashCode());

                int index;
                if (!cells.TryGetValue(key, out index))
                {
                    index = sums.Count;
                    cells[key] = index;
                    sums.Add(p);
                    counts.Add(1);
                }
                else
                {
                    sums[index] += p;
                    counts[index]++;
                }

                remap[i] = index;
            }

            var verts = new Vector3[sums.Count];
            for (int i = 0; i < verts.Length; i++) verts[i] = sums[i] / counts[i];

            var tris = new System.Collections.Generic.List<int>(positions.Length);
            for (int t = 0; t + 2 < positions.Length; t += 3)
            {
                int a = remap[t], b = remap[t + 1], c = remap[t + 2];
                if (a == b || b == c || a == c) continue;   // collapsed to a line or a point
                tris.Add(a); tris.Add(b); tris.Add(c);
            }

            mesh.SetVertices(new System.Collections.Generic.List<Vector3>(verts));
            mesh.SetTriangles(tris, 0);
        }

        private Vector3 Convert(float x, float y, float z)
        {
            return zUp ? new Vector3(x, z, y) * unitScale : new Vector3(x, y, z) * unitScale;
        }

        private Mesh BuildMesh(string name, Vector3[] positions)
        {
            // Swapping Y and Z mirrors handedness, so each triangle has to be reversed or every
            // face ends up inside out.
            if (zUp)
            {
                for (int t = 0; t + 2 < positions.Length; t += 3)
                {
                    var swap = positions[t];
                    positions[t] = positions[t + 2];
                    positions[t + 2] = swap;
                }
            }

            var bounds = new Bounds(positions[0], Vector3.zero);
            for (int i = 1; i < positions.Length; i++) bounds.Encapsulate(positions[i]);

            var scale = 1f;
            if (fitHeight > 0f && bounds.size.y > 1e-9f) scale = fitHeight / bounds.size.y;

            var shift = Vector3.zero;
            if (originAtBase) shift = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

            for (int i = 0; i < positions.Length; i++)
                positions[i] = (positions[i] + shift) * scale;

            var mesh = new Mesh { name = name };
            if (positions.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;

            if (simplifyGrid > 0f) Cluster(mesh, positions, simplifyGrid);
            else if (weldVertices) Cluster(mesh, positions, 0f);
            else
            {
                var indices = new int[positions.Length];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
                mesh.SetVertices(new System.Collections.Generic.List<Vector3>(positions));
                mesh.SetTriangles(indices, 0);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();
            return mesh;
        }
    }
}
