namespace Adnd.Data.Items;

public class ItemJsonModel
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Slot { get; set; } = "";
    public int Cost { get; set; }
    public int Weight { get; set; } = 0;
    public int ToHitBonus { get; set; } = 0;
    public bool IsShopBuyable { get; set; } = true;
    public int? StockQuantity { get; set; }
    public int ArmorClassBonus { get; set; }
    public string Damage { get; set; } = "";
    public List<string> AllowedClasses { get; set; } = new();
}

public class ItemCategoryJsonModel
{
    public string Category { get; set; } = "";
    public List<ItemJsonModel> Items { get; set; } = new();
}
