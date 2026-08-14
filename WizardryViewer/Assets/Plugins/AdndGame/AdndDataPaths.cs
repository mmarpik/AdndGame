using System;
using System.IO;
using UnityEngine;

// Unity-only plumbing: single place that resolves where Adnd.Data's repositories should read
// (and, for Items/Party/Config, write) their JSON from in this project. Not synced from either
// repo - it has no upstream counterpart to drift from.
//
// On Windows Standalone, Root points straight at Application.streamingAssetsPath - a normal,
// listable, writable filesystem folder there (verified end-to-end, including writes - see
// project-adndgame-multiplatform-embed memory). Everywhere AdndDataBootstrap.NeedsExtraction is
// true (Android/Quest, iOS), Root instead points at AdndDataBootstrap.ExtractedDataRoot, a plain
// writable folder under Application.persistentDataPath that AdndDataBootstrap copies the
// StreamingAssets JSON into once via UnityWebRequest, since StreamingAssets itself isn't a normal
// listable/writable folder there. Game code MUST await AdndDataBootstrap.EnsureReadyAsync() before
// touching any of these paths on those platforms - Root throws rather than silently returning a
// path to data that doesn't exist yet if that hasn't happened.
namespace Adnd.Unity.Compat
{
    public static class AdndDataPaths
    {
        public static string Root
        {
            get
            {
                if (!AdndDataBootstrap.NeedsExtraction)
                    return Path.Combine(Application.streamingAssetsPath, "Data");

                if (!AdndDataBootstrap.IsReady)
                    throw new InvalidOperationException(
                        "AdndDataPaths.Root accessed before AdndDataBootstrap.EnsureReadyAsync() completed on a platform that needs extraction.");

                return AdndDataBootstrap.ExtractedDataRoot;
            }
        }

        public static string Items => Path.Combine(Root, "Items");
        public static string Monsters => Path.Combine(Root, "Monsters");
        public static string Spells => Path.Combine(Root, "Spells");
        public static string Treasure => Path.Combine(Root, "Treasure");
        public static string Encounters => Path.Combine(Root, "Encounters");
        public static string Characters => Path.Combine(Root, "Characters");

        // PartyRepository accepts either a directory or a file path and figures out which -
        // pass the file path explicitly so it never falls back to PartyPaths.PartyFile
        // ("Data/Party/party.json" relative to whatever the process's working directory
        // happens to be, which is not this).
        public static string PartyFile => Path.Combine(Root, "Party", "party.json");

        public static string GameRulesFile => Path.Combine(Root, "Config", "game-rules.json");
    }
}
