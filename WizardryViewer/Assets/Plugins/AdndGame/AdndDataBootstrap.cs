using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

// Unity-only plumbing: one-time async extraction of Adnd.Data's StreamingAssets JSON into
// Application.persistentDataPath, needed on Android/Quest and iOS - see AdndDataPaths.cs for why
// (StreamingAssets isn't a normal listable/writable filesystem folder there, unlike Windows
// Standalone). Not synced from either repo.
//
// Why this exists instead of rewriting Adnd.Data's repositories to be async: they're plain
// synchronous File/Directory code, forked from Robert's shared repo, and staying that way matters
// (see project-adndgame-multiplatform-embed memory - most of Adnd.Data is auto-synced, not hand-
// maintained). Doing ALL the async I/O once, up front, before any repository is touched, means
// every repository - including the write-back ones (ItemRepository.TryAdjustStock,
// GameRulesProvider.Save, PartyRepository.Save, character saves) - keeps working completely
// unmodified afterward, reading/writing a normal folder under persistentDataPath. Game code MUST
// await EnsureReadyAsync() before constructing any Adnd.Data repository on platforms where
// NeedsExtraction is true.
//
// Android has no API to list an APK's contents at runtime (Directory.GetFiles doesn't work
// inside it), so this reads Assets/StreamingAssets/Data/manifest.json (written by
// sync-adnd-unity.ps1) via UnityWebRequest first to learn what files exist, then fetches each one
// the same way. iOS doesn't strictly need UnityWebRequest for reads (its StreamingAssets path is
// a normal readable file:// path, just read-only) but is folded into the same extraction path for
// one uniform code path rather than two - UnityWebRequest.Get also works fine against a local
// file:// URL, so this costs a bit of redundant copying on iOS, not correctness.
//
// NEVER VERIFIED ON AN ACTUAL DEVICE OR BUILD. Everything here has only run inside the Windows
// Editor via ForceExtractionForTesting (see AdndDataBootstrapTests in execute_code history) -
// that proves the manifest-parse + UnityWebRequest-fetch + persistentDataPath-write logic works,
// it does NOT prove the Android jar:// URI resolution or iOS app-bundle behavior Unity's own docs
// describe, since neither can be exercised without a real device/build.
namespace Adnd.Unity.Compat
{
    [Serializable]
    internal class AdndDataManifest
    {
        public string dataVersion = "";
        public List<string> files = new();
    }

    public static class AdndDataBootstrap
    {
        private const string VersionMarkerFileName = "AdndDataVersion.txt";

        /// <summary>Set true only from test code to exercise the extraction path outside Android/iOS.</summary>
        public static bool ForceExtractionForTesting = false;

        public static bool IsReady { get; private set; }

        public static bool NeedsExtraction =>
            ForceExtractionForTesting
            || Application.platform == RuntimePlatform.Android
            || Application.platform == RuntimePlatform.IPhonePlayer;

        public static async Task EnsureReadyAsync()
        {
            if (IsReady)
                return;

            if (!NeedsExtraction)
            {
                IsReady = true;
                return;
            }

            await ExtractAsync();
            IsReady = true;
        }

        /// <summary>Test-only: clears the in-memory ready flag so EnsureReadyAsync runs again.</summary>
        internal static void ResetForTesting() => IsReady = false;

        private static async Task ExtractAsync()
        {
            var manifestUrl = CombineUrl(Application.streamingAssetsPath, "Data/manifest.json");
            var manifestJson = await GetTextAsync(manifestUrl);
            var manifest = JsonConvert.DeserializeObject<AdndDataManifest>(manifestJson);
            if (manifest?.files == null)
                throw new InvalidOperationException("AdndDataBootstrap: manifest.json missing or malformed at " + manifestUrl);

            var destRoot = ExtractedDataRoot;
            var versionMarkerPath = Path.Combine(Application.persistentDataPath, VersionMarkerFileName);

            if (File.Exists(versionMarkerPath) && File.ReadAllText(versionMarkerPath) == manifest.dataVersion)
                return; // Already extracted this exact data version.

            foreach (var relPath in manifest.files)
            {
                var srcUrl = CombineUrl(Application.streamingAssetsPath, "Data/" + relPath);
                var text = await GetTextAsync(srcUrl);

                var destPath = Path.Combine(destRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                File.WriteAllText(destPath, text);
            }

            File.WriteAllText(versionMarkerPath, manifest.dataVersion);
        }

        /// <summary>Root of the extracted "Data" tree once EnsureReadyAsync has completed.</summary>
        public static string ExtractedDataRoot => Path.Combine(Application.persistentDataPath, "Data");

        private static async Task<string> GetTextAsync(string url)
        {
            using var request = UnityWebRequest.Get(url);
            var op = request.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException($"AdndDataBootstrap: failed to fetch '{url}': {request.error}");

            return request.downloadHandler.text;
        }

        private static string CombineUrl(string basePath, string relative)
        {
            if (basePath.Contains("://"))
                return basePath.TrimEnd('/') + "/" + relative.TrimStart('/');

            return Path.Combine(basePath, relative.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
