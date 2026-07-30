namespace Adnd.Core.Treasure;

public interface ITreasureTableProvider
{
    bool TryGetTable(string treasureType, out TreasureTable table);
}
