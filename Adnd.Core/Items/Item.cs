using Adnd.Core.Characters;

namespace Adnd.Core.Items;

public class Item
{
    public string Name { get; set; } = "";
    public ItemType Type { get; set; }
    public EquipmentSlot? Slot { get; set; } // null = cannot equip (potions etc.)
    public int Cost { get; set; } = 0;
    public int Weight { get; set; } = 0;
    public int ToHitBonus { get; set; } = 0;
    public bool IsShopBuyable { get; set; } = true;
    public int? StockQuantity { get; set; } = null; // null = unlimited

    // Combat stats
    public int ArmorClassBonus { get; set; } = 0;
    public string Damage { get; set; } = ""; // e.g. "1d6", "2d4"
    public List<CharacterClass> AllowedClasses { get; set; } = new();

    public override string ToString()
    {
        return $"{Name} ({Type})";
    }
}
