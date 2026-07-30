using System;
using System.IO;

namespace Adnd.Game.Installers;

public static class ItemInstaller
{
    public static void Install()
    {
        string source = Path.GetFullPath("../../../../Adnd.Data/Items");
        string target = Path.GetFullPath("Data/Items");

        Console.WriteLine($"DEBUG: Copying items from:\n{source}\n\nto:\n{target}\n");

        if (!Directory.Exists(source))
        {
            Console.WriteLine("WARNING: Source item folder not found.");
            return;
        }
        Console.WriteLine("DEBUG: target."+ target);
        Directory.CreateDirectory(target);

        foreach (var existing in Directory.GetFiles(target, "*.json"))
            File.Delete(existing);

        foreach (var file in Directory.GetFiles(source, "*.json"))
        {
            var fileName = Path.GetFileName(file);
            var dest = Path.Combine(target, fileName);

            File.Copy(file, dest, overwrite: true);
        }

        Console.WriteLine("DEBUG: Items installed.");
    }
}
