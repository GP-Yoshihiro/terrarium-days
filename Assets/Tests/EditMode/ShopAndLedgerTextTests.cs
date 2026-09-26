using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;

namespace TerrariumDays.Tests
{
    public sealed class ShopAndLedgerTextTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Yen_UsesThousandsSeparators()
        {
            Assert.That(ShopText.Yen(4800), Is.EqualTo("¥4,800"));
        }

        [Test]
        public void OfferDetail_ShowsStageWeightAndSexAndThatThePersonalityComesLater()
        {
            var baby = new PetState { Stage = GrowthStage.Baby, WeightGrams = 4.26d, SexRevealed = false };

            Assert.That(ShopText.OfferDetail(baby), Is.EqualTo("ベビー・4.3g・性別不明・性格は購入後に判明"));
        }

        [Test]
        public void ItemLabel_SaysWhenItBecomesUseful()
        {
            var economy = new EconomyTuning();

            Assert.That(ShopText.ItemLabel(ShopCatalog.Find("cage_small", economy)), Is.EqualTo("小型ケージ（装飾1）"));
            Assert.That(ShopText.ItemLabel(ShopCatalog.Find("cage_large", economy)), Is.EqualTo("大型ケージ（装飾3）"));
            Assert.That(ShopText.ItemLabel(ShopCatalog.Find("rack", economy)), Is.EqualTo("ラック（ケージ4つ分）"));
            Assert.That(ShopText.ItemLabel(ShopCatalog.Find("plant_01", economy)), Is.EqualTo("観葉植物"));
            Assert.That(ShopText.ItemLabel(ShopCatalog.Find(ShopCatalog.NestBoxId, economy)), Is.EqualTo("産卵床（段階4で使えます）"));
            Assert.That(ShopText.ItemLabel(ShopCatalog.Find("incubator_standard", economy)), Is.EqualTo("標準孵卵器（段階5で使えます）"));
        }

        [Test]
        public void FailureMessages()
        {
            Assert.That(ShopText.FailureMessage(ShopResult.NotEnoughMoney), Is.EqualTo("所持金が足りません"));
            Assert.That(ShopText.FailureMessage(ShopResult.NoEmptyCage), Is.EqualTo("空きケージがありません（先にケージを買ってください）"));
            Assert.That(ShopText.FailureMessage(ShopResult.NoRackSpace), Is.EqualTo("ラックに空きがありません（先にラックを買ってください）"));
            Assert.That(ShopText.FailureMessage(ShopResult.RackLimit), Is.EqualTo("ラックはこれ以上置けません"));
            Assert.That(ShopText.FailureMessage(ShopResult.NotFound), Is.EqualTo("もう売り切れました"));
            Assert.That(ShopText.FailureMessage(ShopResult.Ok), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ConfirmationsAndResults()
        {
            var pet = new PetState { Name = "レオパ2" };

            Assert.That(ShopText.ConfirmBuy("マックスノー", 12000), Is.EqualTo("マックスノーを¥12,000で買いますか？"));
            Assert.That(ShopText.ConfirmWholesale(pet, 1600), Is.EqualTo("レオパ2を¥1,600で卸しますか？\n（相場の40%。取り消せません）"));
            Assert.That(ShopText.BoughtAnimalMessage(pet, new Cage { Id = 3 }), Is.EqualTo("レオパ2を迎えました（ケージ3）"));
            Assert.That(ShopText.WholesaleMessage("レオパ2", 1600), Is.EqualTo("レオパ2を¥1,600で卸しました"));
            Assert.That(ShopText.WholesaleLine(4000, 1600), Is.EqualTo("相場 ¥4,000 → 卸値 ¥1,600"));
        }

        [Test]
        public void NextRestock_IsTheFirstOfNextGameMonth()
        {
            var calendar = new GameCalendar(Epoch);

            Assert.That(ShopText.NextRestock(calendar, Epoch), Is.EqualTo("次の入荷 5月1日"));
            Assert.That(ShopText.NextRestock(calendar, Epoch + GameCalendar.RealTimeFor(45d)), Is.EqualTo("次の入荷 6月1日"));
        }
    }
}
