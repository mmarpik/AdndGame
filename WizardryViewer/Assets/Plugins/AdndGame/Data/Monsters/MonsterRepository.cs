// Monsters/MonsterRepository.cs
//L�ser alla JSON-filer i en mapp och returnerar riktiga Core-monster.
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Adnd.Core.Monsters;

namespace Adnd.Data.Monsters;

public class MonsterRepository
{
    private readonly string _folder;

    public MonsterRepository(string folder = MonsterDataPaths.MonsterJsonFolder)
    {
        _folder = folder;
    }

    public IEnumerable<Monster> GetAll()
    {
        var monsters = new List<Monster>();

        if (!Directory.Exists(_folder))
            return monsters;

        var files = Directory.GetFiles(_folder, "*.json");

        foreach (var file in files)
        {
            var jsonText = File.ReadAllText(file);

            var grouped = JsonConvert.DeserializeObject<MonsterLevelJsonModel>(jsonText);
            if (grouped?.Monsters != null && grouped.Monsters.Count > 0)
            {
                foreach (var m in grouped.Monsters)
                {
                    if (m != null)
                    {
                        var monster = MonsterImporter.Convert(m);
                        monster.DungeonLevel = grouped.Level; // Set the dungeon level
                        monsters.Add(monster);
                    }
                }

                continue;
            }

            var jsonModel = JsonConvert.DeserializeObject<MonsterJsonModel>(jsonText);
            if (jsonModel != null)
            {
                var monster = MonsterImporter.Convert(jsonModel);
                monster.DungeonLevel = 0; // Default to 0 for monsters without a level file
                monsters.Add(monster);
            }
        }

        return monsters;
    }
}
