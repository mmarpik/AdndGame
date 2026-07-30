using System.IO;

namespace Adnd.Game.Installers;

public static class SpellInstaller
{
    public static void Install()
    {
        string source = Path.GetFullPath("../../../../Adnd.Data/Spells");
        string target = Path.GetFullPath("Data/Spells");

        if (!Directory.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            var dest = Path.Combine(target, fileName);
            File.Copy(file, dest, overwrite: true);
        }
    }
}
