using System;
using System.Collections.Generic;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;

namespace TerrariumDays.Tests
{
    public sealed class BreedingTextTests
    {
        // Calendar day 0 is 1 April 2026.
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly GameCalendar calendar = new GameCalendar(Epoch);

        private static DateTimeOffset At(double gameDays) => Epoch + GameCalendar.RealTimeFor(gameDays);

        private static PetState Adult(Sex sex, string name, Personality personality = Personality.Calm) =>
            new PetState
            {
                Name = name,
                Sex = sex,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                HatchedAtUtc = Epoch.AddDays(-12),
                Personality = personality,
                PersonalityKnown = true,
            };

        [Test]
        public void ProblemLabels()
        {
            Assert.That(BreedingText.ProblemLabel(PairingProblem.None), Is.EqualTo(string.Empty));
            Assert.That(BreedingText.ProblemLabel(PairingProblem.OutOfSeason), Is.EqualTo("繁殖期ではありません"));
            Assert.That(BreedingText.ProblemLabel(PairingProblem.NotMaleAndFemale), Is.EqualTo("オスとメスを1匹ずつ選んでください"));
            Assert.That(BreedingText.ProblemLabel(PairingProblem.SexUnknown), Is.EqualTo("性別がまだ分かりません"));
            Assert.That(BreedingText.ProblemLabel(PairingProblem.Weak), Is.EqualTo("衰弱中です"));
            Assert.That(BreedingText.ProblemLabel(PairingProblem.TooYoung), Is.EqualTo("まだ若すぎます"));
            Assert.That(BreedingText.ProblemLabel(PairingProblem.TooLight), Is.EqualTo("体重が足りません"));
            Assert.That(BreedingText.ProblemLabel(PairingProblem.Gravid), Is.EqualTo("抱卵中です"));
            Assert.That(BreedingText.ProblemLabel(PairingProblem.AlreadyPairing), Is.EqualTo("ペアリング中です"));
            Assert.That(BreedingText.ProblemLabel(PairingProblem.NotFound), Is.EqualTo("個体が見つかりません"));
        }

        [Test]
        public void RequirementsAndSeason()
        {
            Assert.That(BreedingText.Requirement(Sex.Female, care), Is.EqualTo("メス：生後10か月以上・45g以上"));
            Assert.That(BreedingText.Requirement(Sex.Male, care), Is.EqualTo("オス：生後8か月以上・40g以上"));
            Assert.That(BreedingText.SeasonLine(new GameDate(2026, 4, 1), care), Is.EqualTo("いまは繁殖期です（3〜9月）"));
            Assert.That(BreedingText.SeasonLine(new GameDate(2026, 10, 3), care), Is.EqualTo("繁殖期は3〜9月です（いまは10月）"));
        }

        [Test]
        public void Percent_RoundsAndMarksTinyChances()
        {
            Assert.That(BreedingText.Percent(0d), Is.EqualTo("0%"));
            Assert.That(BreedingText.Percent(0.004d), Is.EqualTo("1%未満"));
            Assert.That(BreedingText.Percent(0.125d), Is.EqualTo("13%"));
            Assert.That(BreedingText.Percent(0.84d), Is.EqualTo("84%"));
            Assert.That(BreedingText.Percent(1d), Is.EqualTo("100%"));
        }

        [Test]
        public void ForecastLines_ShowCompatibilityOddsClutchesAndChildren()
        {
            var male = Adult(Sex.Male, "タロウ", Personality.Calm);
            male.Known = new KnownGenetics().SetHet(GeneId.Eclipse, 1d);
            var female = Adult(Sex.Female, "ハナ", Personality.Shy);
            female.Known = new KnownGenetics().SetHet(GeneId.Eclipse, 1d);

            Assert.That(BreedingText.ForecastLines(BreedingForecast.For(male, female, care)), Is.EqualTo(new[]
            {
                "相性：良い",
                "交尾の成功率：約84%",
                "産卵：5〜9回（1回に2個、ときどき1個）",
                "子の見込み（分かっている遺伝から）：",
                "・ノーマル 75%",
                "・エクリプス 25%",
            }));
        }

        [Test]
        public void ForecastLines_WithAnUnknownPersonality()
        {
            var male = Adult(Sex.Male, "タロウ");
            var female = Adult(Sex.Female, "ハナ");
            female.PersonalityKnown = false;

            var lines = BreedingText.ForecastLines(BreedingForecast.For(male, female, care));

            Assert.That(lines[0], Is.EqualTo("相性：不明（性格がまだ分かりません）"));
            Assert.That(lines[1], Is.EqualTo("交尾の成功率：約70%"));
        }

        [Test]
        public void PairingVisitorAndGravidLines()
        {
            var male = Adult(Sex.Male, "タロウ");
            var female = Adult(Sex.Female, "ハナ");
            var pairing = new Pairing { EndsAtUtc = At(3d) };

            Assert.That(BreedingText.DaysLeft(At(3d), Epoch), Is.EqualTo(3));
            Assert.That(BreedingText.DaysLeft(At(3d), At(2.5d)), Is.EqualTo(1));
            Assert.That(BreedingText.DaysLeft(At(3d), At(4d)), Is.EqualTo(0));
            Assert.That(BreedingText.PairingLine(pairing, male, female, Epoch), Is.EqualTo("ハナがタロウのケージを訪問中（あと3日）"));
            Assert.That(BreedingText.VisitorLabel(female, pairing, Epoch), Is.EqualTo("訪問中：ハナ（あと3日）"));

            female.Gravid = new GravidState { NextClutchAtUtc = At(30d), ClutchesLaid = 0 };
            Assert.That(BreedingText.GravidStatus(female.Gravid, calendar), Is.EqualTo("抱卵中（1回目の産卵は5月1日ごろ）"));
            Assert.That(BreedingText.GravidLine(female, calendar), Is.EqualTo("ハナ：抱卵中（1回目の産卵は5月1日ごろ）"));
            Assert.That(BreedingText.ConfirmPairing(male, female, care), Is.EqualTo("ハナをタロウのケージに入れて、ゲーム内3日間ペアリングしますか？"));
        }

        [Test]
        public void EggLines_ForNestBoxAndLooseEggs()
        {
            var room = new RoomClimate();
            var eggs = new List<Egg>
            {
                new Egg { Place = EggPlace.NestBox, LaidAtUtc = At(10d) },
                new Egg { Place = EggPlace.NestBox, LaidAtUtc = At(10d) },
                new Egg { Place = EggPlace.Loose, LaidAtUtc = At(10d) },
            };

            Assert.That(BreedingText.EggLines(eggs, room, At(10.5d), care), Is.EqualTo(new[]
            {
                "産卵床に卵が2個あります。室温24℃（天気を取得できないため）で発生中",
                "産卵床の外に卵が1個（あと約72分で乾いてしまいます）",
            }));

            room.Update(new WeatherReport(10d, 50d, 0));
            eggs[0].DevelopmentPercent = 100d;
            Assert.That(BreedingText.EggLines(eggs.GetRange(0, 2), room, At(10.5d), care), Is.EqualTo(new[]
            {
                "産卵床に卵が2個あります。室温18℃では寒くて発生が止まっています",
                "孵化は段階5（孵卵器）で追加されます",
            }));

            Assert.That(BreedingText.EggLines(new List<Egg>(), room, At(10.5d), care), Is.Empty);
        }

        [Test]
        public void Alerts_ForTheHomeThumbnails()
        {
            var colony = new Colony { CalendarEpochUtc = Epoch };
            var male = colony.AddAnimal(Adult(Sex.Male, "タロウ"), colony.AddCage(CageSize.Standard));
            var female = colony.AddAnimal(Adult(Sex.Female, "ハナ"), colony.AddCage(CageSize.Standard));
            var femaleCage = colony.CageOf(female);
            female.Weak = true;
            female.Gravid = new GravidState();
            colony.Eggs.Add(new Egg { CageId = femaleCage.Id });

            Assert.That(BreedingText.Alerts(colony, femaleCage, female), Is.EqualTo(new[] { "衰弱", "抱卵中", "産卵床なし", "卵あり" }));

            femaleCage.HasNestBox = true;
            Assert.That(BreedingText.Alerts(colony, femaleCage, female), Is.EqualTo(new[] { "衰弱", "抱卵中", "卵あり" }));

            colony.CageOf(male).VisitorAnimalId = female.Id;
            Assert.That(BreedingText.Alerts(colony, colony.CageOf(male), male), Is.EqualTo(new[] { "ペアリング中" }));
        }

        [Test]
        public void Messages_AndTheOneLineSummary()
        {
            var male = Adult(Sex.Male, "タロウ");
            var female = Adult(Sex.Female, "ハナ");
            var report = new BreedingReport();
            report.PairingsSucceeded.Add((female, male));
            report.PairingsFailed.Add((female, male));
            report.PairingsCancelled.Add((female, male, BreedingEnd.Weak));
            report.PairingsCancelled.Add((female, male, BreedingEnd.SeasonOver));
            report.Clutches.Add(new ClutchReport(female, 2, true));
            report.Clutches.Add(new ClutchReport(female, 1, false));
            report.GravidEnded.Add((female, BreedingEnd.AllClutchesLaid));
            report.GravidEnded.Add((female, BreedingEnd.TooThin));
            report.GravidEnded.Add((female, BreedingEnd.SeasonOver));
            report.GravidEnded.Add((female, BreedingEnd.Weak));
            report.EggsDried.Add(new Egg());
            report.EggsDried.Add(new Egg());

            var messages = BreedingText.Messages(report, care);

            Assert.That(messages, Is.EqualTo(new[]
            {
                "ペアリング成功！ハナが抱卵しました（産卵はゲーム内3〜4週間後から）",
                "ハナとタロウのペアリングはうまくいきませんでした",
                "衰弱したため、ハナとタロウのペアリングを中止しました",
                "繁殖期が終わったため、ハナとタロウのペアリングは実りませんでした",
                "ハナが産卵床に卵を2個産みました",
                "ハナが卵を1個産みました。産卵床がないので、ゲーム内2日で乾いてしまいます",
                "ハナの今シーズンの産卵が終わりました",
                "ハナは体重が40gを下回ったので、今シーズンの産卵を終えました",
                "繁殖期が終わり、ハナの抱卵は終わりました",
                "ハナは衰弱したので、抱卵が終わりました",
                "産卵床がなかったため、卵2個が乾いてだめになりました",
            }));
            Assert.That(BreedingText.Summary(messages), Is.EqualTo("ペアリング成功！ハナが抱卵しました（産卵はゲーム内3〜4週間後から）（ほか10件）"));
            Assert.That(BreedingText.Summary(new List<string> { "a" }), Is.EqualTo("a"));
            Assert.That(BreedingText.Summary(new List<string>()), Is.EqualTo(string.Empty));
        }

        [Test]
        public void WeakAndNestBoxTexts()
        {
            var pet = Adult(Sex.Female, "ハナ");
            var cage = new Cage();
            var inventory = new Inventory();
            inventory.Add(ShopCatalog.NestBoxId, 2);

            Assert.That(BreedingText.WeakStatus, Is.EqualTo("衰弱中（繁殖できません）"));
            Assert.That(BreedingText.WeakStartedMessage(pet, care), Is.EqualTo("ハナが衰弱しました（健康が30に戻るまで繁殖できません）"));
            Assert.That(BreedingText.WeakRecoveredMessage(pet), Is.EqualTo("ハナの衰弱が治りました"));
            Assert.That(BreedingText.NestBoxButtonLabel(cage, inventory), Is.EqualTo("産卵床を置く（所持2）"));
            cage.HasNestBox = true;
            Assert.That(BreedingText.NestBoxButtonLabel(cage, inventory), Is.EqualTo("産卵床を外す"));
            Assert.That(BreedingText.NestBoxMessage(NestBoxResult.NoneInInventory), Is.EqualTo("産卵床を持っていません（ショップの「用品」で買えます）"));
            Assert.That(BreedingText.NestBoxMessage(NestBoxResult.EggsInside), Is.EqualTo("卵が入っているので外せません"));
        }

        [Test]
        public void CageDetailLines_ListWeaknessVisitorGravidAndEggs()
        {
            var room = new RoomClimate();
            var colony = new Colony { CalendarEpochUtc = Epoch };
            var male = colony.AddAnimal(Adult(Sex.Male, "タロウ"), colony.AddCage(CageSize.Standard));
            var female = colony.AddAnimal(Adult(Sex.Female, "ハナ"), colony.AddCage(CageSize.Standard));
            var femaleCage = colony.CageOf(female);
            femaleCage.HasNestBox = true;
            female.Weak = true;
            female.Gravid = new GravidState { NextClutchAtUtc = At(30d) };
            colony.Eggs.Add(new Egg { CageId = femaleCage.Id, Place = EggPlace.NestBox, LaidAtUtc = At(1d) });

            Assert.That(BreedingText.CageDetailLines(colony, femaleCage, female, calendar, room, At(2d), care), Is.EqualTo(new[]
            {
                "衰弱中（繁殖できません）",
                "抱卵中（1回目の産卵は5月1日ごろ）",
                "産卵床に卵が1個あります。室温24℃（天気を取得できないため）で発生中",
            }));

            femaleCage.HasNestBox = false;
            colony.Eggs.Clear();
            female.Weak = false;
            Assert.That(BreedingText.CageDetailLines(colony, femaleCage, female, calendar, room, At(2d), care), Is.EqualTo(new[]
            {
                "抱卵中（1回目の産卵は5月1日ごろ）・産卵床がありません",
            }));

            female.Gravid = null;
            colony.Pairings.Add(new Pairing { Id = 1, MaleId = male.Id, FemaleId = female.Id, EndsAtUtc = At(5d) });
            colony.CageOf(male).VisitorAnimalId = female.Id;
            Assert.That(BreedingText.CageDetailLines(colony, colony.CageOf(male), male, calendar, room, At(2d), care), Is.EqualTo(new[]
            {
                "訪問中：ハナ（あと3日）",
            }));
        }
    }
}
