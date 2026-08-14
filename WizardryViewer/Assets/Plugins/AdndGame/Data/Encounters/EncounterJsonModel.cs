namespace Adnd.Data.Encounters;

public class EncounterJsonModel
{
    public string Name { get; set; } = "";
    public List<EncounterEntryJson> Entries { get; set; } = new();
}

public class EncounterEntryJson
{
    public string Monster { get; set; } = "";
    public int Chance { get; set; }
}
