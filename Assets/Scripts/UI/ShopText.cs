using System;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>Texts on the shop tab (§9).</summary>
    public static class ShopText
    {
        public static string Yen(long yen) => $"¥{yen:N0}";

        public static string OfferDetail(PetState animal) =>
            $"{GrowthModel.StageLabel(animal.Stage)}・{animal.WeightGrams:0.0}g・{CageStatusText.SexLabel(animal)}・性格は購入後に判明";

        public static string ItemLabel(ShopItem item)
        {
            switch (item.Kind)
            {
                case ShopItemKind.Cage:
                    return $"{item.Label}（装飾{DecorSlots.SlotsFor(item.CageSize)}）";
                case ShopItemKind.Rack:
                    return $"{item.Label}（ケージ{Colony.CagesPerRack}つ分）";
                default:
                    return item.UsableFromPhase > 0 ? $"{item.Label}（段階{item.UsableFromPhase}で使えます）" : item.Label;
            }
        }

        public static string FailureMessage(ShopResult result)
        {
            switch (result)
            {
                case ShopResult.NotEnoughMoney:
                    return "所持金が足りません";
                case ShopResult.NoEmptyCage:
                    return "空きケージがありません（先にケージを買ってください）";
                case ShopResult.NoRackSpace:
                    return "ラックに空きがありません（先にラックを買ってください）";
                case ShopResult.RackLimit:
                    return "ラックはこれ以上置けません";
                case ShopResult.NotFound:
                    return "もう売り切れました";
                case ShopResult.InPairing:
                    return "ペアリング中の個体は売れません";
                default:
                    return string.Empty;
            }
        }

        public static string ConfirmBuy(string what, long yen) => $"{what}を{Yen(yen)}で買いますか？";

        public static string ConfirmWholesale(PetState pet, long yen, EconomyTuning economy) =>
            $"{pet.Name}を{Yen(yen)}で卸しますか？\n（相場の{WholesaleRatePercent(economy)}%。取り消せません）";

        private static string WholesaleRatePercent(EconomyTuning economy) => (economy.WholesaleRate * 100d).ToString("0.#");

        /// <summary>Shown when the last animal is being sold, appended to the confirmation.</summary>
        public const string LastAnimalWarning = "\nこれで個体がいなくなります（ショップで迎え直せます）";

        /// <summary>Shown when a confirmation dialog's "はい" no longer matches the current offer/animal or its price (e.g. the shop restocked while the dialog was open).</summary>
        public const string OfferChangedMessage = "内容が変わりました。もう一度お確かめください";

        public static string BoughtAnimalMessage(PetState pet, Cage cage) => $"{pet.Name}を迎えました（ケージ{cage.Id}）";

        public static string BoughtItemMessage(ShopItem item) => $"{item.Label}を買いました";

        public static string WholesaleMessage(string name, long yen) => $"{name}を{Yen(yen)}で卸しました";

        public static string WholesaleLine(long market, long pay) => $"相場 {Yen(market)} → 卸値 {Yen(pay)}";

        public static string NextRestock(GameCalendar calendar, DateTimeOffset nowUtc)
        {
            var nextMonthStart = calendar.EpochUtc + GameCalendar.RealTimeFor((calendar.MonthIndexAt(nowUtc) + 1) * (double)GameCalendar.DaysPerMonth);
            var date = calendar.DateAt(nextMonthStart);
            return $"次の入荷 {date.Month}月{date.Day}日";
        }
    }
}
