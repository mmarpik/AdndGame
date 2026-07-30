using System.IO;

namespace Adnd.Game.Installers;

public static class TreasureInstaller
{
    public static void Install()
    {
        string source = Path.GetFullPath("../../../../Adnd.Data/Treasure");
        string target = Path.GetFullPath("Data/Treasure");

        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(target);

        foreach (var existing in Directory.GetFiles(target, "*.json"))
            File.Delete(existing);

        foreach (var file in Directory.GetFiles(source, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            var dest = Path.Combine(target, fileName);
            File.Copy(file, dest, overwrite: true);
        }
    }
}
