using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public enum ShopItemKind
    {
        Cage,
        Rack,
        Decor,
        NestBox,
        Incubator
    }

    /// <summary>One supply for sale (§9).</summary>
    public sealed class ShopItem
    {
        public ShopItem(string id, string label, long price, ShopItemKind kind, CageSize cageSize = CageSize.Standard,
            IncubatorModel incubator = IncubatorModel.Simple, int usableFromPhase = 0)
        {
            Id = id;
            Label = label;
            Price = price;
            Kind = kind;
            CageSize = cageSize;
            Incubator = incubator;
            UsableFromPhase = usableFromPhase;
        }

        public string Id { get; }
        public string Label { get; }
        public long Price { get; }
        public ShopItemKind Kind { get; }
        public CageSize CageSize { get; }
        public IncubatorModel Incubator { get; }

        /// <summary>0 = usable now; otherwise the development phase whose feature uses it (shown in the shop).</summary>
        public int UsableFromPhase { get; }
    }

    public static class ShopCatalog
    {
        public const string NestBoxId = "nest_box";

        public static List<ShopItem> Items(EconomyTuning economy)
        {
            var items = new List<ShopItem>
            {
                new ShopItem("cage_small", "小型ケージ", economy.SmallCagePrice, ShopItemKind.Cage, CageSize.Small),
                new ShopItem("cage_standard", "標準ケージ", economy.StandardCagePrice, ShopItemKind.Cage, CageSize.Standard),
                new ShopItem("cage_large", "大型ケージ", economy.LargeCagePrice, ShopItemKind.Cage, CageSize.Large),
                new ShopItem("rack", "ラック", economy.RackPrice, ShopItemKind.Rack),
            };
            foreach (var decor in DecorItems.All)
            {
                if (economy.DecorPrices.TryGetValue(decor.Id, out var price))
                {
                    items.Add(new ShopItem(decor.Id, decor.Label, price, ShopItemKind.Decor));
                }
            }

            items.Add(new ShopItem(NestBoxId, "産卵床", economy.NestBoxPrice, ShopItemKind.NestBox, usableFromPhase: 4));
            items.Add(new ShopItem("incubator_standard", "標準孵卵器", economy.StandardIncubatorPrice, ShopItemKind.Incubator,
                incubator: IncubatorModel.Standard, usableFromPhase: 5));
            items.Add(new ShopItem("incubator_luxury", "高級孵卵器", economy.LuxuryIncubatorPrice, ShopItemKind.Incubator,
                incubator: IncubatorModel.Luxury, usableFromPhase: 5));
            return items;
        }

        public static ShopItem Find(string id, EconomyTuning economy) => Items(economy).Find(i => i.Id == id);
    }
}
