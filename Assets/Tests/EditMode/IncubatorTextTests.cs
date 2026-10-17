using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;

namespace TerrariumDays.Tests
{
    public sealed class IncubatorTextTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly CareTuning care = new CareTuning();

        private static DateTimeOffset AtDay(double gameDays) => Epoch + GameCalendar.RealTimeFor(gameDays);

        [Test]
        public void IncubatorName_HasTheNumberAndTheModel()
        {
            Assert.AreEqual("孵卵器1（簡易）", IncubatorText.IncubatorName(new Incubator { Id = 1, Model = IncubatorModel.Simple }));
            Assert.AreEqual("孵卵器2（標準）", IncubatorText.IncubatorName(new Incubator { Id = 2, Model = IncubatorModel.Standard }));
            Assert.AreEqual("孵卵器3（高級）", IncubatorText.IncubatorName(new Incubator { Id = 3, Model = IncubatorModel.Luxury }));
        }

        [Test]
        public void Occupancy_TemperatureAndHumidity()
        {
            Assert.AreEqual("3／6個", IncubatorText.Occupancy(3, 6));
            Assert.AreEqual("設定 28℃", IncubatorText.SetTemperature(28));
            Assert.AreEqual("実温 27.8℃", IncubatorText.RealTemperature(27.84d));
            Assert.AreEqual("実温 26.0℃", IncubatorText.RealTemperature(26d));
            Assert.AreEqual("湿度 72%", IncubatorText.Humidity(71.6d));
        }

        [Test]
        public void HumidityWarning_BelowSixtyOrAboveNinetyFive()
        {
            Assert.IsTrue(IncubatorText.IsHumidityWarning(59.9d, care));
            Assert.IsFalse(IncubatorText.IsHumidityWarning(60d, care));
            Assert.IsFalse(IncubatorText.IsHumidityWarning(95d, care));
            Assert.IsTrue(IncubatorText.IsHumidityWarning(95.1d, care));
        }

        [TestCase(CandlingStage.Unknown, "まだ分からない")]
        [TestCase(CandlingStage.Infertile, "無精卵")]
        [TestCase(CandlingStage.Early, "初期")]
        [TestCase(CandlingStage.Middle, "中期")]
        [TestCase(CandlingStage.Late, "後期")]
        [TestCase(CandlingStage.AboutToHatch, "孵化直前")]
        [TestCase(CandlingStage.Dead, "駄目になった卵")]
        public void CandlingLabel_MatchesTheSpec(CandlingStage stage, string expected)
        {
            Assert.AreEqual(expected, IncubatorText.CandlingLabel(stage));
        }

        [TestCase(EggFailure.Dried, "乾燥")]
        [TestCase(EggFailure.Mold, "カビ")]
        [TestCase(EggFailure.Heat, "高温")]
        [TestCase(EggFailure.Cold, "低温")]
        public void FailureLabel_NamesTheCause(EggFailure failure, string expected)
        {
            Assert.AreEqual(expected, IncubatorText.FailureLabel(failure));
        }

        [Test]
        public void EggDetail_ShowsStageDevelopmentAndAge()
        {
            var egg = new Egg { Id = 12, LaidAtUtc = Epoch, Fertile = true, DevelopmentPercent = 48.7d };
            Assert.AreEqual("卵12：中期（発生48%）・産卵から21日", IncubatorText.EggDetail(egg, CandlingStage.Middle, AtDay(21.5d)));

            var young = new Egg { Id = 3, LaidAtUtc = Epoch, Fertile = true, DevelopmentPercent = 5d };
            Assert.AreEqual("卵3：まだ分からない・産卵から3日", IncubatorText.EggDetail(young, CandlingStage.Unknown, AtDay(3d)));

            var dead = new Egg { Id = 4, LaidAtUtc = Epoch, Fertile = true, Failure = EggFailure.Mold };
            Assert.AreEqual("卵4：駄目になった卵（カビ）・産卵から9日", IncubatorText.EggDetail(dead, CandlingStage.Dead, AtDay(9d)));
        }

        [Test]
        public void MoveResult_Messages()
        {
            Assert.AreEqual("孵卵器へ3個移しました", IncubatorText.MoveResult(3, 0));
            Assert.AreEqual("孵卵器へ3個移しました（1個は空きがなく残りました）", IncubatorText.MoveResult(3, 1));
            Assert.AreEqual("孵卵器に空きがありません", IncubatorText.MoveResult(0, 2));
            Assert.AreEqual("移す卵がありません", IncubatorText.MoveResult(0, 0));
        }

        [Test]
        public void HomeButton_ShowsCountsOnlyWhenThereAreAny()
        {
            Assert.AreEqual("孵卵器", IncubatorText.HomeButton(0, 0));
            Assert.AreEqual("孵卵器（卵5・トレイ1）", IncubatorText.HomeButton(5, 1));
            Assert.AreEqual("孵卵器（卵0・トレイ2）", IncubatorText.HomeButton(0, 2));
        }

        [Test]
        public void ParentsLine_UsesNamesOrUnknown()
        {
            var hana = new FamilyMember { Name = "ハナ" };
            var taro = new FamilyMember { Name = "タロウ", Gone = true };
            var unknown = FamilyMember.UnknownMember();

            Assert.AreEqual("両親：ハナ×タロウ", IncubatorText.ParentsLine(hana, taro));
            Assert.AreEqual("両親：ハナ×不明", IncubatorText.ParentsLine(hana, unknown));
            Assert.AreEqual("両親：不明", IncubatorText.ParentsLine(unknown, unknown));
        }

        [Test]
        public void TreeBox_ShowsNameMorphAndSex()
        {
            Assert.AreEqual("不明", IncubatorText.TreeBox(FamilyMember.UnknownMember()));
            Assert.AreEqual("ハナ\nノーマル\n♀",
                IncubatorText.TreeBox(new FamilyMember { Name = "ハナ", MorphName = "ノーマル", Sex = Sex.Female, SexKnown = true }));
            Assert.AreEqual("タロウ（手放した）\nタンジェリン\n♂",
                IncubatorText.TreeBox(new FamilyMember { Name = "タロウ", MorphName = "タンジェリン", Sex = Sex.Male, SexKnown = true, Gone = true }));
            Assert.AreEqual("レオパ9\nノーマル\n?",
                IncubatorText.TreeBox(new FamilyMember { Name = "レオパ9", MorphName = "ノーマル", Sex = Sex.Male, SexKnown = false }));
        }

        [Test]
        public void Messages_ListEveryEvent()
        {
            var report = new IncubationReport();
            report.Hatched.Add(new PetState { Name = "レオパ7" });
            report.InfertileRevealed.Add(new Egg { IncubatorId = 1 });
            report.EggsDied.Add(new Egg { IncubatorId = 2, Fertile = true, Failure = EggFailure.Mold });
            report.HumidityLowIncubatorIds.Add(1);
            report.NestBoxEggsRemoved.Add(new Egg { CageId = 2, Fertile = false });
            report.NestBoxEggsRemoved.Add(new Egg { CageId = 3, Fertile = true, Failure = EggFailure.Cold });

            CollectionAssert.AreEqual(new[]
            {
                "レオパ7が孵化しました（孵化トレイ）",
                "孵卵器1の卵が無精卵と分かりました",
                "孵卵器2の卵が駄目になりました（カビ）",
                "孵卵器1の湿度が60%を下回りました",
                "ケージ2の産卵床の卵を1個取り除きました（無精卵）",
                "ケージ3の産卵床の卵を1個取り除きました（低温）"
            }, IncubatorText.Messages(report, care));
        }

        [Test]
        public void TrayRow_AndGraceLine()
        {
            var pet = new PetState { Name = "レオパ7", Stage = GrowthStage.Baby, SexRevealed = false };
            Assert.AreEqual($"レオパ7（{GrowthModel.StageLabel(GrowthStage.Baby)}・性別不明）", IncubatorText.TrayRow(pet));

            pet.CareGraceUntilUtc = AtDay(30d);
            Assert.AreEqual("お世話不要（あと12日）", IncubatorText.GraceLine(pet, AtDay(18.5d)));
            Assert.AreEqual("世話が必要です。ケージへ移してください", IncubatorText.GraceLine(pet, AtDay(30d)));

            pet.CareGraceUntilUtc = null;
            Assert.AreEqual("世話が必要です。ケージへ移してください", IncubatorText.GraceLine(pet, AtDay(1d)));
        }

        [Test]
        public void FixedMessages()
        {
            Assert.AreEqual("湿度が95%を超えました。カビに注意してください", IncubatorText.MoldWarning(care));
            Assert.AreEqual("空いているケージがありません", IncubatorText.NoEmptyCage);
            Assert.AreEqual("孵化トレイ", IncubatorText.TrayLocation);
        }
    }
}
