// Turns a printable STL into a table-ready figure in one step: scaled to figure height, stood
// on a base, saved as a prefab, and registered against the id the snapshot will ask for.
//
// The id comes from the filename, which is the whole trick — drop Fighter.stl in and the party's
// fighters use it, with nothing to wire by hand. Names match ClassId for the party and MonsterId
// for foes: Fighter, Thief, Priest, MagicUser, Bishop, Goblin.

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WizardryViewer.Unity;

namespace WizardryViewer.EditorTools
{
    public static class MiniFromStl
    {
        private const string PrefabFolder = "Assets/Generated/Prefabs";
        private const string MaterialFolder = "Assets/Generated/Materials";
        private const string MeshFolder = "Assets/Generated/Meshes";
        private const string FigurineFolder = "Assets/Figurines";

        /// <summary>
        /// Widest a figure may be, in cells. Fitting a model by height is not enough: a paladin
        /// with a shield out is nearly as wide as it is tall, and three of those abreast put the
        /// outer two through the corridor wall. Must satisfy fileSpacing + half of this &lt; 0.5.
        /// </summary>
        private const float FootprintCells = 0.38f;

        /// <summary>
        /// Which model plays which party member. Keyed Race_ClassId so the table picks the right
        /// one per character; the renderer falls back to a bare ClassId when no race matches.
        /// The pack has Paladin/Mage/Rogue only, so Priest and Bishop borrow the nearest fit.
        /// </summary>
        private static readonly (string Id, string File)[] PartyMapping =
        {
            ("Dwarf_Fighter",   "4__Male_Dwarf_Paladin"),      // Grond
            ("Human_Fighter",   "0__Male_Human_Paladin"),       // Brann
            ("Hobbit_Thief",    "22__Male_Halfling_Rogue"),     // Pip
            ("Human_Priest",    "40__Female_Human_Paladin"),    // Sister
            ("Elf_MagicUser",   "50__Female_Human_Mage"),       // Mila — no elf in the pack
            ("Gnome_Bishop",    "12__Male_Halfling_Mage"),      // Odo
        };

        [MenuItem("Wizardry Viewer/Build Party From Figurines")]
        private static void BuildParty()
        {
            var done = 0;
            for (int i = 0; i < PartyMapping.Length; i++)
            {
                var map = PartyMapping[i];
                var path = $"{FigurineFolder}/{map.File}.stl";
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[mini] {path} not found — skipped");
                    continue;
                }

                EditorUtility.DisplayProgressBar("Building party",
                    $"{map.Id} from {map.File}", (float)i / PartyMapping.Length);

                if (Build(path, map.Id)) done++;
            }

            EditorUtility.ClearProgressBar();
            Debug.Log($"[mini] party built: {done} of {PartyMapping.Length} figures");
        }

        /// <summary>
        /// Decimate and bake every figurine in the pack, so the 40-80MB sources can be thrown away
        /// while the miniaturised versions stay usable (monsters, NPCs, whatever comes up later).
        ///
        /// Skips anything already baked, which makes it resumable: the full-resolution sources are
        /// what took Unity down twice, and a crash at file 50 must not mean starting from scratch.
        /// </summary>
        [MenuItem("Wizardry Viewer/Bake All Figurines")]
        private static void BakeAllFigurines()
        {
            var files = Directory.GetFiles(FigurineFolder, "*.stl");
            System.Array.Sort(files);

            int baked = 0, already = 0, failed = 0, cancelled = 0;
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    var path = files[i].Replace('\\', '/');
                    var id = Path.GetFileNameWithoutExtension(path);

                    if (AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshFolder}/Mini{id}.asset") != null)
                    {
                        already++;
                        continue;
                    }

                    if (EditorUtility.DisplayCancelableProgressBar("Baking figurines",
                            $"{i + 1}/{files.Length}  {id}", (float)i / files.Length))
                    {
                        cancelled = files.Length - i;
                        break;
                    }

                    if (BakeOnly(path, id)) baked++; else failed++;

                    // 865k tris and 2.6M verts apiece: without unloading between files, sixty of
                    // them in one loop is exactly the memory curve that killed the editor before.
                    EditorUtility.UnloadUnusedAssetsImmediate();
                    System.GC.Collect();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[mini] bake all: {baked} baked, {already} already present, " +
                      $"{failed} failed, {cancelled} not reached, of {files.Length} STLs");
        }

        /// <summary>Import + decimate + bake a mesh asset, without building a prefab for it.</summary>
        private static bool BakeOnly(string path, string id)
        {
            var importer = AssetImporter.GetAtPath(path) as StlImporter;
            if (importer != null)
            {
                importer.fitHeight = WizardryViewerSetup.FigureHeightMetres;
                importer.originAtBase = true;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                Debug.LogError($"[mini] {path} did not import as a mesh — skipped");
                return false;
            }

            var saved = Bake(id, mesh);
            Debug.Log($"[mini] baked {id}: {saved.triangles.Length / 3:N0} tris, " +
                      $"{saved.bounds.size.y * 1000f:F1}mm tall at fit scale");
            return true;
        }

        /// <summary>
        /// Re-register the party's prefabs in the open scene without touching the STLs.
        ///
        /// Needed because rebuilding the scene resets the standee table, and the sources the party
        /// was built from no longer exist — "Build Party From Figurines" would skip all six and
        /// leave the table on primitives with no way back. The prefabs and their baked meshes are
        /// still there, so re-pointing the scene at them is all that is required.
        /// </summary>
        [MenuItem("Wizardry Viewer/Re-register Party Prefabs")]
        private static void ReregisterParty()
        {
            var wired = 0;
            var missing = 0;

            foreach (var map in PartyMapping)
            {
                var prefab = Load<GameObject>($"{PrefabFolder}/Mini{map.Id}.prefab");
                if (prefab == null)
                {
                    Debug.LogWarning($"[mini] {PrefabFolder}/Mini{map.Id}.prefab missing — not wired");
                    missing++;
                    continue;
                }

                if (Register(map.Id, prefab)) wired++;
            }

            Debug.Log($"[mini] re-registered {wired} of {PartyMapping.Length} party prefabs" +
                      (missing > 0 ? $" ({missing} missing)" : "") +
                      (wired == 0 ? " — no TableRenderer in the open scene?" : ""));
        }

        [MenuItem("Wizardry Viewer/Make Mini From Selected STL")]
        private static void MakeMini()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".stl", System.StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Make Mini",
                    "Select a .stl file in the Project window first.", "OK");
                return;
            }

            Build(path, Path.GetFileNameWithoutExtension(path));
        }

        private static bool Build(string path, string id)
        {

            // Re-import at table scale before reading the mesh back.
            var importer = AssetImporter.GetAtPath(path) as StlImporter;
            if (importer != null)
            {
                importer.fitHeight = WizardryViewerSetup.FigureHeightMetres;
                importer.originAtBase = true;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                Debug.LogError($"[mini] {path} did not import as a mesh — check the console for import errors.");
                return false;
            }

            var prefab = BuildPrefab(id, mesh);
            var wired = Register(id, prefab);

            var scale = prefab.transform.Find("Body").localScale.x;
            Debug.Log($"[mini] {id}: {mesh.triangles.Length / 3:N0} tris, " +
                      $"{mesh.bounds.size.y * 1000f * scale:F1}mm tall, " +
                      $"{Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z) * scale / WizardryViewerSetup.CellSizeMetres:F2} cells wide " +
                      $"-> Mini{id}.prefab" +
                      (wired ? " (wired into the open scene)" : " (no TableRenderer in scene; not wired)"));
            return true;
        }

        private static GameObject BuildPrefab(string id, Mesh mesh)
        {
            var root = new GameObject("Mini" + id);

            var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plinth.name = "Base";
            plinth.transform.SetParent(root.transform, false);
            var plinthWidth = WizardryViewerSetup.CellSizeMetres * WizardryViewerSetup.PlinthCells;
            plinth.transform.localScale = new Vector3(plinthWidth, 0.0012f, plinthWidth);
            plinth.transform.localPosition = new Vector3(0f, 0.0012f, 0f);
            plinth.GetComponent<Renderer>().sharedMaterial = Load<Material>($"{MaterialFolder}/Slate.mat");
            Object.DestroyImmediate(plinth.GetComponent<Collider>());

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.0024f, 0f);   // stand on top of the base
            body.AddComponent<MeshFilter>().sharedMesh = Bake(id, mesh);

            // Shrink further if the model is wide, so the formation clears the walls. Height is
            // whatever falls out of that — a fat figure is a short figure.
            var footprint = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            var budget = WizardryViewerSetup.CellSizeMetres * FootprintCells;
            var shrink = footprint > budget ? budget / footprint : 1f;
            body.transform.localScale = Vector3.one * shrink;

            // Reuse the generated class colour so an imported fighter is still the red one. Ids are
            // Race_Class, and the materials are per class, so the race prefix has to come off before
            // the second attempt — without it every figure silently fell back to the same blue.
            var classOnly = id.Contains("_") ? id.Substring(id.IndexOf('_') + 1) : id;
            var material = Load<Material>($"{MaterialFolder}/Mini{id}.mat")
                        ?? Load<Material>($"{MaterialFolder}/Mini{classOnly}.mat")
                        ?? Load<Material>($"{MaterialFolder}/PlasticHero.mat");
            body.AddComponent<MeshRenderer>().sharedMaterial = material;

            Directory.CreateDirectory(PrefabFolder);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Mini{id}.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>
        /// Copy the imported mesh into a standalone asset. Without this a prefab points at a
        /// sub-asset inside the .stl, so the multi-gigabyte source library can never leave Assets/
        /// without breaking every figure. The decimated copy is a few hundred KB.
        /// </summary>
        private static Mesh Bake(string id, Mesh source)
        {
            Directory.CreateDirectory(MeshFolder);
            var path = $"{MeshFolder}/Mini{id}.asset";

            var copy = Object.Instantiate(source);
            copy.name = "Mini" + id;

            // Overwrite in place when rebuilding, so existing prefabs keep their reference.
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
                Object.DestroyImmediate(copy);
                AssetDatabase.SaveAssets();
                return existing;
            }

            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            return copy;
        }

        /// <summary>Point the open scene's TableRenderer at this prefab for the matching id.</summary>
        private static bool Register(string id, GameObject prefab)
        {
            var renderer = Object.FindObjectOfType<TableRenderer>();
            if (renderer == null) return false;

            var so = new SerializedObject(renderer);
            var list = so.FindProperty("standees");

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("id").stringValue != id) continue;

                element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                so.ApplyModifiedProperties();
                MarkSceneDirty();
                return true;
            }

            list.arraySize++;
            var added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("id").stringValue = id;
            added.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            MarkSceneDirty();
            return true;
        }

        private static void MarkSceneDirty()
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static T Load<T>(string path) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
