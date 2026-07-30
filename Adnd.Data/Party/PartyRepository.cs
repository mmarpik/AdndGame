using System.IO;
using System.Text.Json;

namespace Adnd.Data.Party;

public class PartyRepository
{
    private readonly string _path;
    private readonly string? _legacyFilePath;

    public PartyRepository(string path = PartyPaths.PartyFile)
    {
        // Accept both file paths (e.g. Data/Party/party.json)
        // and directory paths (e.g. Data/Party).
        var looksLikeDirectory = !Path.HasExtension(path) || path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar);
        var originalPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var resolvedPath = looksLikeDirectory
            ? Path.Combine(originalPath, "party.json")
            : originalPath;

        var folder = Path.GetDirectoryName(resolvedPath) ?? "Data";

        string? legacyFilePath = null;

        // Legacy case: older code may have saved party JSON directly to "Data/Party" (as a file).
        if (looksLikeDirectory && File.Exists(originalPath))
        {
            legacyFilePath = originalPath;
        }

        // If a file exists where the target folder should be, fall back to a safe folder.
        if (File.Exists(folder))
        {
            legacyFilePath ??= folder;

            var parent = Path.GetDirectoryName(folder);
            var safeFolder = Path.Combine(parent ?? "Data", "PartyData");
            resolvedPath = Path.Combine(safeFolder, Path.GetFileName(resolvedPath));
            folder = safeFolder;
        }

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        _path = resolvedPath;
        _legacyFilePath = legacyFilePath;
    }

    public Party Load()
    {
        if (File.Exists(_path))
        {
            var json = File.ReadAllText(_path);
            var party = JsonSerializer.Deserialize<Party>(json);
            return party ?? new Party();
        }

        // Try legacy file path and migrate if possible.
        if (!string.IsNullOrWhiteSpace(_legacyFilePath) && File.Exists(_legacyFilePath))
        {
            var json = File.ReadAllText(_legacyFilePath);
            var party = JsonSerializer.Deserialize<Party>(json) ?? new Party();
            Save(party);
            return party;
        }

        return new Party();
    }

    public void Save(Party party)
    {
        var json = JsonSerializer.Serialize(party, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_path, json);
    }
}
