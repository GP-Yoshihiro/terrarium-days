using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>The decorations the shop sells (§9). Ids match TerrariumArtLayout.Decor and the USS icon classes.</summary>
    public static class DecorItems
    {
        public const string StarterDecorId = "rock_01";

        public static readonly IReadOnlyList<(string Id, string Label)> All = new List<(string, string)>
        {
            ("rock_01", "岩"),
            ("plant_01", "観葉植物"),
            ("water_dish_01", "水入れ"),
            ("heat_lamp_01", "保温ランプ"),
            ("driftwood_01", "流木"),
        };

        public static bool IsDecor(string id)
        {
            foreach (var decor in All)
            {
                if (decor.Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        public static string LabelOf(string id)
        {
            foreach (var decor in All)
            {
                if (decor.Id == id)
                {
                    return decor.Label;
                }
            }

            return id;
        }
    }

    public enum DecorResult
    {
        Ok,
        NotOwned,
        AlreadyPlaced,
        SlotsFull,
        NotPlaced
    }

    /// <summary>Decor belongs to a cage (§6.1): small 1, standard 2, large 3 slots; one of each item per cage.</summary>
    public static class DecorSlots
    {
        public static int SlotsFor(CageSize size)
        {
            switch (size)
            {
                case CageSize.Small:
                    return 1;
                case CageSize.Large:
                    return 3;
                default:
                    return 2;
            }
        }

        public static int FreeSlots(Cage cage) => SlotsFor(cage.Size) - cage.DecorIds.Count;

        public static DecorResult Place(Colony colony, Cage cage, string decorId)
        {
            if (cage.DecorIds.Contains(decorId))
            {
                return DecorResult.AlreadyPlaced;
            }

            if (FreeSlots(cage) <= 0)
            {
                return DecorResult.SlotsFull;
            }

            if (!colony.Inventory.TryTake(decorId))
            {
                return DecorResult.NotOwned;
            }

            cage.DecorIds.Add(decorId);
            return DecorResult.Ok;
        }

        public static DecorResult Remove(Colony colony, Cage cage, string decorId)
        {
            if (!cage.DecorIds.Remove(decorId))
            {
                return DecorResult.NotPlaced;
            }

            colony.Inventory.Add(decorId, 1);
            return DecorResult.Ok;
        }

        /// <summary>Row status in the decor drawer: what a tap on that row will do.</summary>
        public static string StatusText(Colony colony, Cage cage, string decorId)
        {
            if (cage.DecorIds.Contains(decorId))
            {
                return "置いています（タップで外す）";
            }

            var owned = colony.Inventory.Count(decorId);
            if (owned == 0)
            {
                return "未所持（ショップで購入）";
            }

            return FreeSlots(cage) <= 0 ? $"所持{owned}・枠がいっぱい" : $"所持{owned}（タップで置く）";
        }

        public static string SlotSummary(Cage cage) => $"装飾の枠 {cage.DecorIds.Count}/{SlotsFor(cage.Size)}";
    }
}
