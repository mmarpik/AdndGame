using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Adnd.Core.Characters;
using Adnd.Core.Items;

namespace Adnd.Data.Items;

public class ItemRepository
{
    private readonly string _folder;

    public ItemRepository(string folder = "Data/Items")
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
    }

    public IEnumerable<Item> LoadAll()
    {
        var list = new List<Item>();

        foreach (var file in Directory.GetFiles(_folder, "*.json"))
        {
            var json = File.ReadAllText(file);

            var grouped = JsonSerializer.Deserialize<ItemCategoryJsonModel>(json);
            if (grouped?.Items != null && grouped.Items.Count > 0)
            {
                foreach (var model in grouped.Items)
                {
                    var item = ToItem(model);
                    if (item != null)
                        list.Add(item);
                }

                continue;
            }

            var single = JsonSerializer.Deserialize<ItemJsonModel>(json);
            if (single != null)
            {
                var item = ToItem(single);
                if (item != null)
                    list.Add(item);
            }
        }

        return list
            .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    public bool TryAdjustStock(string itemName, int delta)
    {
        var file = FindItemFile(itemName);
        if (file == null)
            return false;

        var json = File.ReadAllText(file);

        var grouped = JsonSerializer.Deserialize<ItemCategoryJsonModel>(json);
        if (grouped?.Items != null && grouped.Items.Count > 0)
        {
            var target = grouped.Items.FirstOrDefault(i => string.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase));
            if (target == null)
                return false;

            if (!target.IsShopBuyable)
                return false;

            if (target.StockQuantity.HasValue)
            {
                var next = target.StockQuantity.Value + delta;
                if (next < 0)
                    return false;

                target.StockQuantity = next;

                var updatedGrouped = JsonSerializer.Serialize(grouped, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, updatedGrouped);
                return true;
            }

            return true;
        }

        var single = JsonSerializer.Deserialize<ItemJsonModel>(json);
        if (single == null)
            return false;

        if (!single.IsShopBuyable)
            return false;

        if (single.StockQuantity.HasValue)
        {
            var next = single.StockQuantity.Value + delta;
            if (next < 0)
                return false;

            single.StockQuantity = next;

            var updated = JsonSerializer.Serialize(single, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, updated);
            return true;
        }

        return true;
    }

    private string? FindItemFile(string itemName)
    {
        foreach (var file in Directory.GetFiles(_folder, "*.json"))
        {
            var json = File.ReadAllText(file);

            var grouped = JsonSerializer.Deserialize<ItemCategoryJsonModel>(json);
            if (grouped?.Items != null && grouped.Items.Any(i => string.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase)))
                return file;

            var model = JsonSerializer.Deserialize<ItemJsonModel>(json);
            if (model != null && string.Equals(model.Name, itemName, StringComparison.OrdinalIgnoreCase))
                return file;
        }

        return null;
    }

    private Item? ToItem(ItemJsonModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Type))
            return null;

        return new Item
        {
            Name = model.Name,
            Type = Enum.Parse<ItemType>(model.Type),
            Slot = string.IsNullOrEmpty(model.Slot) ? null : Enum.Parse<EquipmentSlot>(model.Slot),
            Cost = model.Cost,
            Weight = model.Weight,
            ToHitBonus = model.ToHitBonus,
            IsShopBuyable = model.IsShopBuyable,
            StockQuantity = model.StockQuantity,
            ArmorClassBonus = model.ArmorClassBonus,
            Damage = model.Damage,
            AllowedClasses = ResolveAllowedClasses(model.AllowedClasses)
        };
    }

    private static List<CharacterClass> ResolveAllowedClasses(List<string>? rawAllowedClasses)
    {
        if (rawAllowedClasses == null || rawAllowedClasses.Count == 0)
            return new List<CharacterClass>();

        var hasAll = rawAllowedClasses.Any(c => string.Equals(c?.Trim(), "All", StringComparison.OrdinalIgnoreCase));
        if (hasAll)
            return Enum.GetValues<CharacterClass>().ToList();

        var list = new List<CharacterClass>();
        foreach (var name in rawAllowedClasses)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (Enum.TryParse<CharacterClass>(name.Trim(), ignoreCase: true, out var parsed))
                list.Add(parsed);
        }

        return list;
    }
}
