using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Adnd.Data.Encounters;

public class EncounterRepository
{
    private readonly string _folder;

    public EncounterRepository(string folder = "Data/Encounters")
    {
        _folder = folder;
    }

    public IEnumerable<EncounterJsonModel> GetAll()
    {
        var list = new List<EncounterJsonModel>();

        if (!Directory.Exists(_folder))
            return list;

        foreach (var file in Directory.GetFiles(_folder, "*.json"))
        {
            var json = File.ReadAllText(file);
            var model = JsonSerializer.Deserialize<EncounterJsonModel>(json);

            if (model != null)
                list.Add(model);
        }

        return list;
    }
}
