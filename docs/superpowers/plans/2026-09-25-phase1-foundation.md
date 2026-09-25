# 段階1：土台（カレンダー・複数ケージ・体重・ホームとタブ・一括の世話・維持費）実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 1匹用の試作を、複数の個体・ケージ・お金を持つ「飼育データ全体（コロニー）」の上で動くように作り替え、ホーム画面とタブを追加する。

**Architecture:** ゲームのルールは Unity に依存しない純粋なクラス（Core / Gameplay）に置き、EditMode テストで確かめる。時間経過・保存・電気代の請求は新しい `ColonySession` に集め、全個体をまとめて扱う。`TerrariumView` は「選んでいるケージの個体を表示・操作する」役割に絞り、画面の切り替えは新しい `ShellNavigator`・`HomeView`・`CageListView` が受け持つ。

**Tech Stack:** Unity 6000.5.10f1（UI Toolkit、JsonUtility、NUnit / Unity Test Framework）、C#、macOS 上の CLI（`scripts/run-unity-tests.sh`、`scripts/test-summary.py`、`scripts/capture-screens.sh`、`scripts/build-ios.sh`）。

**Spec:** `docs/superpowers/specs/2026-09-25-breeder-sim-design.md`（本計画は §3・§5.3〜5.5・§6・§12・§14 段階1 を実装する）

## Global Constraints

- ユーザーへの応答は日本語。コード・コメント・コミットメッセージは英語（CLAUDE.md）。
- 時間：現実1日＝ゲーム内1か月、1か月＝30日、ゲーム内1日＝現実48分。ゲーム内の開始日は2026年4月1日。
- 世話の値の減り方は現実時間基準。年齢・脱皮・成長前の拒食・電気代はゲーム内時間基準。
- 閉じている間の計算は最大12時間分（既存の `MaxOfflineProgressHours`）を全個体に適用する。
- 成長段階：ベビー15g未満／ヤング15g以上／アダルト40g以上かつ生後10か月以上。条件を満たしたらゲーム内3日間の拒食 → 脱皮して段階が上がる。
- 体重：孵化時3g。餌1回あたり 2.2g ×（1 − 体重 ÷ 上限）。上限はメス60g・オス75g・雌雄不明65g。拒食中はゲーム内1日あたり0.1g減る。
  - ※仕様書は1.6gだが、仕様書の「約10日でアダルトの体重」を満たすには2.2gが必要（1日2.5回の餌で約10.7日）。仕様書もTask 2で2.2gに直す。
- 脱皮：ベビー・ヤングはゲーム内17.5日ごと、アダルトは45日ごと（仕様の2〜3週間／1〜2か月の中央値）。脱皮前ゲーム内2日間は拒食する。
- 雌雄はヤング以上で判明。
- お金：開始50,000円。餌代はベビー30円／ヤング50円／アダルト80円。電気代はゲーム内の月が変わるたびに、ケージ1つ300円・孵卵器1台500円。お金が足りないと餌を買えない。電気代はマイナスになってもよい。
- 1ラック＝4ケージ。最初はラック1台、標準ケージ1つ、簡易孵卵器1台（孵卵器の画面は段階5）。
- セーブはスキーマ3。スキーマ1・2のファイルは自動で移行し、元のファイルを `<保存先>.v2.bak` に残す。
- 装飾は、この段階では1匹ごと（既存のまま）。ケージごとの装飾は段階3で行う。
- 画面の文字は日本語（成長段階もベビー／ヤング／アダルト）。
- 表示でレイアウトがずれないよう、固定の高さの欄は `visibility` で表示を切り替える（既存の方針）。

## ファイル構成

| ファイル | 種別 | 役割 |
|---|---|---|
| `Assets/Scripts/Core/GameCalendar.cs` | 新規 | 現実時間 ↔ ゲーム内日付 |
| `Assets/Scripts/Core/Sex.cs` | 新規 | 雌雄の列挙型 |
| `Assets/Scripts/Core/GrowthModel.cs` | 新規 | 体重・年齢・成長段階・餌による増加 |
| `Assets/Scripts/Core/SheddingModel.cs` | 新規 | 段階ごとの脱皮間隔 |
| `Assets/Scripts/Core/Wallet.cs` | 新規 | 所持金と台帳 |
| `Assets/Scripts/Core/EconomyTuning.cs` | 新規 | 金額の設定値 |
| `Assets/Scripts/Core/MaintenanceCosts.cs` | 新規 | 餌代・電気代の計算と請求 |
| `Assets/Scripts/Core/Colony.cs` | 新規 | 個体・ケージ・ラック・お金をまとめた飼育データ |
| `Assets/Scripts/Core/ColonySaveData.cs` | 新規 | スキーマ3の JSON の形 |
| `Assets/Scripts/Core/ColonySaveService.cs` | 新規 | スキーマ3の保存・読み込み、スキーマ1/2からの移行 |
| `Assets/Scripts/Core/ColonySession.cs` | 新規 | 全個体の時間経過・再同期・保存・請求 |
| `Assets/Scripts/Gameplay/ColonyCareService.cs` | 新規 | 餌代つきの給餌と一括の世話 |
| `Assets/Scripts/UI/ShellNavigator.cs` | 新規 | ホーム／タブ／パネルの切り替え |
| `Assets/Scripts/UI/HomeView.cs` | 新規 | ホーム画面のラック表示 |
| `Assets/Scripts/UI/CageListView.cs` | 新規 | ケージ一覧と一括の世話のボタン |
| `Assets/Scripts/UI/CageStatusText.cs` | 新規 | ケージの注意マークの文字列（純粋関数） |
| `Assets/Scripts/Core/PetState.cs` | 変更 | 個体の情報を追加。成長度（Growth）を廃止 |
| `Assets/Scripts/Core/CareTuning.cs` | 変更 | 成長・脱皮の設定値をゲーム内時間基準に変更 |
| `Assets/Scripts/Core/AppetiteModel.cs` | 変更 | 成長前の拒食を「段階が上がる予定日」で判定 |
| `Assets/Scripts/Core/OfflineProgressCalculator.cs` | 変更 | 体重・成長段階・脱皮をゲーム内時間で進める |
| `Assets/Scripts/Gameplay/CareService.cs` | 変更 | 餌で体重が増える |
| `Assets/Scripts/Core/SaveService.cs`・`GrowthGaugeCalculator.cs` | 削除 | `ColonySaveService`・`GrowthModel` に置き換え |
| `Assets/Scripts/UI/TerrariumView.cs` | 変更 | `ColonySession` を使い、選んでいる個体を表示する |
| `Assets/UI/Terrarium.uxml`・`Terrarium.uss` | 変更 | ヘッダー・ホーム・タブ・一覧を追加 |
| `Assets/Tests/EditMode/*`・`Assets/Tests/PlayMode/*` | 変更・新規 | 各タスクのテスト |
| `GAME.md`・`CLAUDE.md`・`AGENTS.md` | 変更 | 範囲の変更とゲームの目的 |

---

### Task 1: ゲーム内カレンダー

**Files:**
- Create: `Assets/Scripts/Core/GameCalendar.cs`
- Test: `Assets/Tests/EditMode/GameCalendarTests.cs`

**Interfaces:**
- Produces: `GameCalendar(DateTimeOffset epochUtc)`、`double GameDaysAt(DateTimeOffset utc)`、`int MonthIndexAt(DateTimeOffset utc)`、`GameDate DateAt(DateTimeOffset utc)`、`static TimeSpan RealTimeFor(double gameDays)`、`static double GameDaysBetween(DateTimeOffset from, DateTimeOffset to)`、`GameDate.ToDisplayText()`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class GameCalendarTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);
        private readonly GameCalendar calendar = new GameCalendar(Epoch);

        [Test]
        public void TheGameStartsOnTheFirstOfAprilTwentyTwentySix()
        {
            var date = calendar.DateAt(Epoch);

            Assert.That((date.Year, date.Month, date.Day), Is.EqualTo((2026, 4, 1)));
            Assert.That(date.ToDisplayText(), Is.EqualTo("2026年4月1日"));
        }

        [Test]
        public void FortyEightRealMinutesAreOneGameDay()
        {
            Assert.That(calendar.GameDaysAt(Epoch.AddMinutes(48)), Is.EqualTo(1d).Within(1e-9));
            Assert.That(GameCalendar.RealTimeFor(3d), Is.EqualTo(TimeSpan.FromMinutes(144)));
        }

        [Test]
        public void OneRealDayIsOneGameMonth()
        {
            Assert.That(calendar.MonthIndexAt(Epoch.AddDays(1)), Is.EqualTo(1));
            var date = calendar.DateAt(Epoch.AddDays(1));
            Assert.That((date.Year, date.Month, date.Day), Is.EqualTo((2026, 5, 1)));
        }

        [Test]
        public void TheYearRollsOverAfterMarch()
        {
            var date = calendar.DateAt(Epoch.AddDays(9));

            Assert.That((date.Year, date.Month), Is.EqualTo((2027, 1)));
        }

        [Test]
        public void TimesBeforeTheEpochClampToTheStartDate()
        {
            Assert.That(calendar.GameDaysAt(Epoch.AddHours(-5)), Is.EqualTo(0d));
            Assert.That(calendar.MonthIndexAt(Epoch.AddHours(-5)), Is.EqualTo(0));
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: コンパイルエラー（`GameCalendar` がない）

- [ ] **Step 3: 実装する**

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>A date on the in-game calendar.</summary>
    public readonly struct GameDate
    {
        public readonly int Year;
        public readonly int Month;
        public readonly int Day;

        public GameDate(int year, int month, int day)
        {
            Year = year;
            Month = month;
            Day = day;
        }

        public string ToDisplayText() => $"{Year}年{Month}月{Day}日";
    }

    /// <summary>
    /// Compressed game time: one real day is one game month (30 days), so one game day is
    /// 48 real minutes. Day 0 is 1 April 2026 at the colony's epoch.
    /// </summary>
    public sealed class GameCalendar
    {
        public const double RealMinutesPerGameDay = 48d;
        public const int DaysPerMonth = 30;
        public const int StartYear = 2026;
        public const int StartMonth = 4;

        public GameCalendar(DateTimeOffset epochUtc)
        {
            EpochUtc = epochUtc;
        }

        public DateTimeOffset EpochUtc { get; }

        public static TimeSpan RealTimeFor(double gameDays) => TimeSpan.FromMinutes(gameDays * RealMinutesPerGameDay);

        public static double GameDaysBetween(DateTimeOffset from, DateTimeOffset to) =>
            (to - from).TotalMinutes / RealMinutesPerGameDay;

        public double GameDaysAt(DateTimeOffset utc) => Math.Max(0d, GameDaysBetween(EpochUtc, utc));

        public int MonthIndexAt(DateTimeOffset utc) => (int)Math.Floor(GameDaysAt(utc) / DaysPerMonth);

        public GameDate DateAt(DateTimeOffset utc)
        {
            var days = GameDaysAt(utc);
            var monthIndex = (int)Math.Floor(days / DaysPerMonth);
            var monthsFromJanuary = StartMonth - 1 + monthIndex;
            var year = StartYear + monthsFromJanuary / 12;
            var month = monthsFromJanuary % 12 + 1;
            var day = (int)Math.Floor(days - monthIndex * DaysPerMonth) + 1;
            return new GameDate(year, month, day);
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: `EditMode-results.xml: ... failed=0`

- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/GameCalendar.cs Assets/Tests/EditMode/GameCalendarTests.cs
git commit -m "Add the compressed game calendar (1 real day = 1 game month)"
```

---

### Task 2: 個体の情報と体重による成長段階

**Files:**
- Create: `Assets/Scripts/Core/Sex.cs`, `Assets/Scripts/Core/GrowthModel.cs`, `Assets/Tests/EditMode/GrowthModelTests.cs`
- Modify: `Assets/Scripts/Core/PetState.cs`, `Assets/Scripts/Core/CareTuning.cs`, `Assets/Scripts/Gameplay/CareService.cs`, `Assets/Tests/EditMode/PetStateTests.cs`, `Assets/Tests/EditMode/CareTuningTests.cs`, `Assets/Tests/EditMode/CareServiceTests.cs`, `docs/superpowers/specs/2026-09-25-breeder-sim-design.md`
- Delete: `Assets/Scripts/Core/GrowthGaugeCalculator.cs`, `Assets/Tests/EditMode/GrowthGaugeCalculatorTests.cs`

**Interfaces:**
- Consumes: なし（`OfflineProgressCalculator` と `AppetiteModel` の修正は Task 3。この Task では `Growth` を参照している箇所がコンパイルエラーになるので、Task 3 まで続けて行う）
- Produces:
  - `enum Sex { Female, Male }`
  - `PetState` の新しいプロパティ：`int Id`、`string Name`、`Sex Sex`、`bool SexKnown`（ヤング以上で true）、`double WeightGrams`、`DateTimeOffset HatchedAtUtc`、`GrowthStage Stage`（保存される値。`GrowthStage` プロパティは `Stage` を返す）、`DateTimeOffset? StageUpDueAtUtc`
  - `GrowthModel.NextStage(GrowthStage) : GrowthStage?`、`GrowthModel.MinWeightFor(GrowthStage, CareTuning) : double`、`GrowthModel.AgeMonths(PetState, DateTimeOffset) : double`、`GrowthModel.MeetsNextStage(PetState, DateTimeOffset, CareTuning) : bool`、`GrowthModel.WeightCap(PetState, CareTuning) : double`、`GrowthModel.FeedGain(PetState, CareTuning) : double`、`GrowthModel.ProgressToNextStage(PetState, DateTimeOffset, CareTuning) : double`（0〜1）、`GrowthModel.StageLabel(GrowthStage) : string`
  - `CareTuning` の新しい値：`HatchlingWeightGrams=3`、`JuvenileMinWeightGrams=15`、`AdultMinWeightGrams=40`、`AdultMinAgeMonths=10`、`FeedWeightGainGrams=2.2`、`FemaleWeightCapGrams=60`、`MaleWeightCapGrams=75`、`UnknownSexWeightCapGrams=65`、`FastingWeightLossPerGameDay=0.1`、`PreGrowthFastGameDays=3`、`PreShedGameDays=2`、`YoungShedIntervalGameDays=17.5`、`AdultShedIntervalGameDays=45`
  - 削除：`PetState.Growth`、`PetState.GrowthStageFromGrowth`、`CareTuning.GrowthPerHour`、`CareTuning.ShedIntervalDays`、`CareTuning.PreShedDays`、`CareTuning.PreGrowthGaugePercent`、`GrowthGaugeCalculator`

- [ ] **Step 1: 失敗するテストを書く**（`Assets/Tests/EditMode/GrowthModelTests.cs`）

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class GrowthModelTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning tuning = new CareTuning();

        private PetState Pet(double weight, GrowthStage stage, double ageMonths, Sex sex = Sex.Female) => new PetState
        {
            WeightGrams = weight,
            Stage = stage,
            HatchedAtUtc = Now.AddDays(-ageMonths),
            Sex = sex,
        };

        [Test]
        public void ANewPetIsAThreeGramBabyWhoseSexIsNotKnownYet()
        {
            var pet = new PetState();

            Assert.That(pet.WeightGrams, Is.EqualTo(3d));
            Assert.That(pet.Stage, Is.EqualTo(GrowthStage.Baby));
            Assert.That(pet.SexKnown, Is.False);
        }

        [Test]
        public void SexBecomesKnownFromTheJuvenileStage()
        {
            Assert.That(Pet(20d, GrowthStage.Juvenile, 4).SexKnown, Is.True);
        }

        [TestCase(14.9, GrowthStage.Baby, 1, false)]
        [TestCase(15.0, GrowthStage.Baby, 1, true)]
        [TestCase(45.0, GrowthStage.Juvenile, 9.9, false)]
        [TestCase(39.9, GrowthStage.Juvenile, 12, false)]
        [TestCase(40.0, GrowthStage.Juvenile, 10, true)]
        [TestCase(70.0, GrowthStage.Adult, 30, false)]
        public void MeetsNextStage_NeedsWeightAndForAdultsAge(double weight, GrowthStage stage, double ageMonths, bool expected)
        {
            Assert.That(GrowthModel.MeetsNextStage(Pet(weight, stage, ageMonths), Now, tuning), Is.EqualTo(expected));
        }

        [Test]
        public void FeedGain_ShrinksAsThePetNearsItsCap()
        {
            var light = GrowthModel.FeedGain(Pet(3d, GrowthStage.Baby, 0), tuning);
            var heavy = GrowthModel.FeedGain(Pet(50d, GrowthStage.Adult, 12), tuning);

            Assert.That(light, Is.EqualTo(2.2d * (1d - 3d / 65d)).Within(1e-9), "sex unknown → 65 g cap");
            Assert.That(heavy, Is.EqualTo(2.2d * (1d - 50d / 60d)).Within(1e-9), "adult female → 60 g cap");
            Assert.That(GrowthModel.FeedGain(Pet(80d, GrowthStage.Adult, 12, Sex.Male), tuning), Is.EqualTo(0d));
        }

        [Test]
        public void TwoAndAHalfMealsADay_ReachAdultWeightInAboutTenDays()
        {
            var pet = Pet(3d, GrowthStage.Baby, 0, Sex.Male);
            var meals = 0;
            while (pet.WeightGrams < tuning.AdultMinWeightGrams && meals < 200)
            {
                if (pet.WeightGrams >= tuning.JuvenileMinWeightGrams)
                {
                    pet.Stage = GrowthStage.Juvenile;
                }

                pet.WeightGrams += GrowthModel.FeedGain(pet, tuning);
                meals++;
            }

            Assert.That(meals / 2.5, Is.InRange(9d, 12d));
        }

        [Test]
        public void ProgressToNextStage_UsesTheSlowerOfWeightAndAgeForAdults()
        {
            // 40 g reached but only 5 of 10 months old → half way.
            Assert.That(GrowthModel.ProgressToNextStage(Pet(40d, GrowthStage.Juvenile, 5), Now, tuning), Is.EqualTo(0.5).Within(1e-9));
            // Baby at 9 g: (9-3)/(15-3).
            Assert.That(GrowthModel.ProgressToNextStage(Pet(9d, GrowthStage.Baby, 1), Now, tuning), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(GrowthModel.ProgressToNextStage(Pet(60d, GrowthStage.Adult, 20), Now, tuning), Is.EqualTo(1d));
        }

        [TestCase(GrowthStage.Baby, "ベビー")]
        [TestCase(GrowthStage.Juvenile, "ヤング")]
        [TestCase(GrowthStage.Adult, "アダルト")]
        public void StageLabelsAreJapanese(GrowthStage stage, string label)
        {
            Assert.That(GrowthModel.StageLabel(stage), Is.EqualTo(label));
        }

        [Test]
        public void Feeding_AddsWeight()
        {
            var pet = Pet(10d, GrowthStage.Baby, 1);

            new CareService(tuning).Feed(pet);

            Assert.That(pet.WeightGrams, Is.EqualTo(10d + 2.2d * (1d - 10d / 65d)).Within(1e-9));
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: コンパイルエラー（`GrowthModel`・`Sex`・`WeightGrams` がない）

- [ ] **Step 3: `Sex.cs` を作る**

```csharp
namespace TerrariumDays.Core
{
    public enum Sex
    {
        Female,
        Male
    }
}
```

- [ ] **Step 4: `CareTuning.cs` を変更する**

`GrowthPerHour` の行、および `ShedIntervalDays`・`PreShedDays`・`PreGrowthGaugePercent` の3行とそのコメントを削除し、`AnorexiaHungerDecayMultiplier` の直前に次を追加する。

```csharp
        // Body weight and growth stages (grams / game months). Stage-up needs these, then a
        // PreGrowthFastGameDays fast, then a shed.
        public double HatchlingWeightGrams { get; set; } = 3d;
        public double JuvenileMinWeightGrams { get; set; } = 15d;
        public double AdultMinWeightGrams { get; set; } = 40d;
        public double AdultMinAgeMonths { get; set; } = 10d;
        public double FeedWeightGainGrams { get; set; } = 2.2d;
        public double FemaleWeightCapGrams { get; set; } = 60d;
        public double MaleWeightCapGrams { get; set; } = 75d;
        public double UnknownSexWeightCapGrams { get; set; } = 65d;
        public double FastingWeightLossPerGameDay { get; set; } = 0.1d;
        public double PreGrowthFastGameDays { get; set; } = 3d;

        // Shedding, in game days.
        public double PreShedGameDays { get; set; } = 2d;
        public double YoungShedIntervalGameDays { get; set; } = 17.5d;
        public double AdultShedIntervalGameDays { get; set; } = 45d;
```

- [ ] **Step 5: `PetState.cs` を変更する**

`private double growth;`・`Growth` プロパティ・`GrowthStage => GrowthStageFromGrowth(Growth)`・`GrowthStageFromGrowth` メソッドを削除する。`NextShedAtUtc` の既定値を `DateTimeOffset.UtcNow + GameCalendar.RealTimeFor(new CareTuning().YoungShedIntervalGameDays)` に変える。クラスの XML コメントを「One animal: identity, body, care state and schedules. Plain data.」に変え、次を追加する。

```csharp
        private double weightGrams = 3d;

        public int Id { get; set; }

        public string Name { get; set; } = "レオパ";

        /// <summary>True sex. Shown to the player only once <see cref="SexKnown"/>.</summary>
        public Sex Sex { get; set; } = Sex.Female;

        public bool SexKnown => Stage != GrowthStage.Baby;

        public double WeightGrams
        {
            get => weightGrams;
            set => weightGrams = Math.Max(0d, value);
        }

        public DateTimeOffset HatchedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Stored growth stage; it only advances after the pre-growth fast.</summary>
        public GrowthStage Stage { get; set; } = GrowthStage.Baby;

        public GrowthStage GrowthStage => Stage;

        /// <summary>When the pre-growth fast ends and the stage goes up; null when not fasting for growth.</summary>
        public DateTimeOffset? StageUpDueAtUtc { get; set; }
```

- [ ] **Step 6: `GrowthModel.cs` を作る**

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Growth by body weight and age, as breeders judge it. Pure so it is EditMode tested.
    /// </summary>
    public static class GrowthModel
    {
        public static GrowthStage? NextStage(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Baby:
                    return GrowthStage.Juvenile;
                case GrowthStage.Juvenile:
                    return GrowthStage.Adult;
                default:
                    return null;
            }
        }

        public static double MinWeightFor(GrowthStage stage, CareTuning tuning)
        {
            switch (stage)
            {
                case GrowthStage.Juvenile:
                    return tuning.JuvenileMinWeightGrams;
                case GrowthStage.Adult:
                    return tuning.AdultMinWeightGrams;
                default:
                    return tuning.HatchlingWeightGrams;
            }
        }

        /// <summary>Age in game months: one real day is one game month.</summary>
        public static double AgeMonths(PetState pet, DateTimeOffset nowUtc) =>
            Math.Max(0d, (nowUtc - pet.HatchedAtUtc).TotalDays);

        public static bool MeetsNextStage(PetState pet, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var next = NextStage(pet.Stage);
            if (!next.HasValue || pet.WeightGrams < MinWeightFor(next.Value, tuning))
            {
                return false;
            }

            return next.Value != GrowthStage.Adult || AgeMonths(pet, nowUtc) >= tuning.AdultMinAgeMonths;
        }

        public static double WeightCap(PetState pet, CareTuning tuning)
        {
            if (!pet.SexKnown)
            {
                return tuning.UnknownSexWeightCapGrams;
            }

            return pet.Sex == Sex.Female ? tuning.FemaleWeightCapGrams : tuning.MaleWeightCapGrams;
        }

        public static double FeedGain(PetState pet, CareTuning tuning) =>
            tuning.FeedWeightGainGrams * Math.Max(0d, 1d - pet.WeightGrams / WeightCap(pet, tuning));

        public static double ProgressToNextStage(PetState pet, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var next = NextStage(pet.Stage);
            if (!next.HasValue)
            {
                return 1d;
            }

            var from = MinWeightFor(pet.Stage, tuning);
            var to = MinWeightFor(next.Value, tuning);
            var byWeight = Clamp01((pet.WeightGrams - from) / (to - from));
            if (next.Value != GrowthStage.Adult)
            {
                return byWeight;
            }

            return Math.Min(byWeight, Clamp01(AgeMonths(pet, nowUtc) / tuning.AdultMinAgeMonths));
        }

        public static string StageLabel(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Juvenile:
                    return "ヤング";
                case GrowthStage.Adult:
                    return "アダルト";
                default:
                    return "ベビー";
            }
        }

        private static double Clamp01(double value) => value < 0d ? 0d : value > 1d ? 1d : value;
    }
}
```

- [ ] **Step 7: `CareService.Feed(PetState)` で体重を増やす**

```csharp
        public void Feed(PetState state)
        {
            state.Hunger += tuning.FeedHungerAmount;
            state.WeightGrams += GrowthModel.FeedGain(state, tuning);
        }
```

- [ ] **Step 8: 古いテストを新しい仕様に合わせる**
  - `GrowthGaugeCalculator.cs` と `GrowthGaugeCalculatorTests.cs`（`.meta` も）を削除する。
  - `PetStateTests.cs`：`Growth` の既定値の行、`Growth = -5d` とその確認の行、`GrowthStage_IsDerivedFromGrowthAtDocumentedBoundaries` のテストを削除する。
  - `CareTuningTests.cs`：`GrowthPerHour` の行を `Assert.That(tuning.FeedWeightGainGrams, Is.EqualTo(2.2d));` に置き換える。
  - `CareServiceTests.cs`：`Growth = 10d` と `state.Growth` の確認を、`WeightGrams` の確認に置き換える（例：`Feed_IncreasesOnlyHungerByTheTunedAmount` では `Assert.That(state.WeightGrams, Is.GreaterThan(3d));` とし、水・掃除のテストでは `Assert.That(state.WeightGrams, Is.EqualTo(3d));`）。
  - 仕様書 §5.3 の「1.6g」を「2.2g」に直す。

- [ ] **Step 9: Task 3 に続けて進める**（`OfflineProgressCalculator` と `AppetiteModel` が `Growth` を参照しているため、ここではまだコンパイルできない）

---

### Task 3: ゲーム内時間での体重・成長前の拒食・脱皮

**Files:**
- Create: `Assets/Scripts/Core/SheddingModel.cs`
- Modify: `Assets/Scripts/Core/AppetiteModel.cs`, `Assets/Scripts/Core/OfflineProgressCalculator.cs`, `Assets/Tests/EditMode/AppetiteAndSheddingTests.cs`, `Assets/Tests/EditMode/OfflineProgressCalculatorTests.cs`

**Interfaces:**
- Consumes: Task 1 の `GameCalendar.RealTimeFor`、Task 2 の `GrowthModel`・`PetState`・`CareTuning`
- Produces: `SheddingModel.IntervalFor(GrowthStage, CareTuning) : TimeSpan`、`AppetiteModel.Evaluate(PetState, DateTimeOffset, CareTuning)`（成長前の拒食は `StageUpDueAtUtc` で判定）、`OfflineProgressResult.NewGrowthStage` / `ShedCount`（既存のまま）

- [ ] **Step 1: 失敗するテストを書く**
  - `AppetiteAndSheddingTests.cs` の `StateWithShedIn` を次に置き換え、成長ゲージを使うテスト（`Appetite_IsLostJustBeforeTheGrowthGaugeFills`、`Appetite_AnAdultHasNoGrowthRefusal`、`Offline_RefusingFoodBeforeAStageUp_DoesNotStallGrowth`、`Offline_GrowingIntoTheNextStage_TriggersAShed`、`Save_...` の2件）を削除して、下の新しいテストを追加する。`PreShedDays` は `PreShedGameDays`（ゲーム内日数）に読み替える。

```csharp
        private PetState StateWithShedIn(TimeSpan untilShed, double weight = 10d, GrowthStage stage = GrowthStage.Baby, double ageMonths = 2d)
        {
            return new PetState
            {
                WeightGrams = weight,
                Stage = stage,
                HatchedAtUtc = Now.AddDays(-ageMonths),
                LastSavedAtUtc = Now,
                NextShedAtUtc = Now + untilShed,
            };
        }

        [Test]
        public void Appetite_IsLostInTheGameDaysBeforeAShed()
        {
            var state = StateWithShedIn(GameCalendar.RealTimeFor(tuning.PreShedGameDays - 0.5));

            Assert.That(AppetiteModel.Evaluate(state, Now, tuning), Is.EqualTo(AppetiteState.PreShed));
        }

        [Test]
        public void Appetite_IsLostWhileAStageUpIsPending()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(3));
            state.StageUpDueAtUtc = Now + TimeSpan.FromMinutes(30);

            Assert.That(AppetiteModel.Evaluate(state, Now, tuning), Is.EqualTo(AppetiteState.PreGrowth));
            Assert.That(AppetiteModel.Evaluate(state, Now + TimeSpan.FromMinutes(31), tuning), Is.EqualTo(AppetiteState.Normal));
        }

        [Test]
        public void Offline_ReachingTheNextStagesWeight_FastsThreeGameDaysThenGrowsAndSheds()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(3), weight: 15.2d);
            var fast = GameCalendar.RealTimeFor(tuning.PreGrowthFastGameDays);

            var first = new OfflineProgressCalculator(tuning).Apply(state, Now, Now + TimeSpan.FromMinutes(1));
            Assert.That(first.NewGrowthStage, Is.Null);
            Assert.That(state.StageUpDueAtUtc, Is.EqualTo(Now + TimeSpan.FromMinutes(1) + fast));
            Assert.That(AppetiteModel.Evaluate(state, Now + TimeSpan.FromMinutes(2), tuning), Is.EqualTo(AppetiteState.PreGrowth));

            var second = new OfflineProgressCalculator(tuning).Apply(state, state.LastSavedAtUtc + first.AppliedElapsed, Now + TimeSpan.FromMinutes(1) + fast);
            Assert.That(second.NewGrowthStage, Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(second.ShedCount, Is.EqualTo(1));
            Assert.That(state.StageUpDueAtUtc, Is.Null);
        }

        [Test]
        public void Offline_AnUnderAgeHeavyJuvenile_DoesNotBecomeAnAdult()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(3), weight: 50d, stage: GrowthStage.Juvenile, ageMonths: 6d);

            var result = new OfflineProgressCalculator(tuning).Apply(state, Now, Now + TimeSpan.FromHours(12));

            Assert.That(result.NewGrowthStage, Is.Null);
            Assert.That(state.StageUpDueAtUtc, Is.Null);
        }

        [Test]
        public void Offline_FastingLosesATenthOfAGramPerGameDay()
        {
            var state = StateWithShedIn(GameCalendar.RealTimeFor(1d), weight: 20d, stage: GrowthStage.Juvenile);

            new OfflineProgressCalculator(tuning).Apply(state, Now, Now + GameCalendar.RealTimeFor(1d));

            Assert.That(state.WeightGrams, Is.EqualTo(20d - 0.1d).Within(1e-6));
        }

        [TestCase(GrowthStage.Baby, 17.5)]
        [TestCase(GrowthStage.Juvenile, 17.5)]
        [TestCase(GrowthStage.Adult, 45d)]
        public void ShedInterval_DependsOnTheStage(GrowthStage stage, double gameDays)
        {
            Assert.That(SheddingModel.IntervalFor(stage, tuning), Is.EqualTo(GameCalendar.RealTimeFor(gameDays)));
        }
```

  - `Offline_PassingTheShedDate_ShedsAndSchedulesTheNextOne` の最後の確認を `Now + TimeSpan.FromHours(3) + SheddingModel.IntervalFor(GrowthStage.Baby, tuning)` に変える。
  - `Offline_WhileRefusingFood_HungerDropsSlowerAndDoesNotHurtHealth` の `StateWithShedIn(TimeSpan.FromDays(1))` を `StateWithShedIn(GameCalendar.RealTimeFor(1d))` に変える。
  - `OfflineProgressCalculatorTests.cs`：`Growth` を含むタプルの比較を、`Growth` を `WeightGrams` に置き換えた形にする。`Apply_WhenAllCareStatsStayAtOrAboveHealthyThreshold_RecoversHealthAndAdvancesGrowth` は名前を `..._RecoversHealth` にして `Growth` の確認を削除する。`Apply_WhenAnyCareStatStaysBelowLowThreshold_DecreasesHealthOnly` と `..._NeitherZone_...` の `Growth` の確認を削除する。`Apply_WhenGrowthCrossesAStageBoundary_ReturnsTheNewStageExactlyOnce` と `Apply_WhenGrowthChangesWithoutCrossingAStageBoundary_ReturnsNoEvent` は削除する（上の新しいテストが置き換える）。

- [ ] **Step 2: テストが失敗することを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: コンパイルエラー（`SheddingModel` がない）

- [ ] **Step 3: `SheddingModel.cs` を作る**

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>Real-time length of the shed cycle for a growth stage (game days → real time).</summary>
    public static class SheddingModel
    {
        public static TimeSpan IntervalFor(GrowthStage stage, CareTuning tuning) =>
            GameCalendar.RealTimeFor(stage == GrowthStage.Adult ? tuning.AdultShedIntervalGameDays : tuning.YoungShedIntervalGameDays);
    }
}
```

- [ ] **Step 4: `AppetiteModel.Evaluate` を置き換える**

```csharp
        public static AppetiteState Evaluate(PetState state, DateTimeOffset nowUtc, CareTuning tuning)
        {
            if (nowUtc >= state.NextShedAtUtc - GameCalendar.RealTimeFor(tuning.PreShedGameDays))
            {
                return AppetiteState.PreShed;
            }

            if (state.StageUpDueAtUtc.HasValue && nowUtc < state.StageUpDueAtUtc.Value)
            {
                return AppetiteState.PreGrowth;
            }

            return AppetiteState.Normal;
        }
```

- [ ] **Step 5: `OfflineProgressCalculator.Apply` のステップのループを置き換える**

```csharp
            for (var i = 0; i < stepCount; i++)
            {
                var stepEndUtc = previousUtc + TimeSpan.FromTicks(step.Ticks * (i + 1));
                var refusing = AppetiteModel.IsRefusingFood(state, stepEndUtc, tuning);
                ApplyStep(state, refusing);

                if (refusing)
                {
                    state.WeightGrams -= tuning.FastingWeightLossPerGameDay * step.TotalMinutes / GameCalendar.RealMinutesPerGameDay;
                }

                var shedNow = stepEndUtc >= state.NextShedAtUtc;
                if (!state.StageUpDueAtUtc.HasValue && GrowthModel.MeetsNextStage(state, stepEndUtc, tuning))
                {
                    // Pre-growth fast first; the stage goes up (with a shed) when it ends.
                    state.StageUpDueAtUtc = stepEndUtc + GameCalendar.RealTimeFor(tuning.PreGrowthFastGameDays);
                }
                else if (state.StageUpDueAtUtc.HasValue && stepEndUtc >= state.StageUpDueAtUtc.Value)
                {
                    state.Stage = GrowthModel.NextStage(state.Stage) ?? state.Stage;
                    state.StageUpDueAtUtc = null;
                    shedNow = true;
                }

                if (shedNow)
                {
                    state.LastShedAtUtc = stepEndUtc;
                    state.NextShedAtUtc = stepEndUtc + SheddingModel.IntervalFor(state.Stage, tuning);
                    sheds++;
                }
            }
```

`ApplyStep` から `state.Growth += tuning.GrowthPerHour * stepHours;` の行を削除する（成長は体重で決まる）。クラスの XML コメントに「Weight, stage-up and sheds run on game time」を追記する。

- [ ] **Step 6: テストが通ることを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: EditMode の失敗0（`TerrariumView` はまだ `Growth` を参照しているので、PlayMode は Task 8 まで失敗してよい。EditMode のアセンブリは UI に依存しないのでコンパイルされる。もし `Assembly-CSharp` のコンパイルエラーで EditMode も動かない場合は、Task 8 の Step 3〜4 のうち `Growth` の参照を消す部分だけ先に行う）

- [ ] **Step 7: コミット**

```bash
git add -A Assets/Scripts/Core Assets/Scripts/Gameplay Assets/Tests/EditMode docs/superpowers/specs
git commit -m "Grow pets by weight and age on game time; stage-up after a pre-growth fast"
```

---

### Task 4: お金（所持金・台帳・餌代・電気代）

**Files:**
- Create: `Assets/Scripts/Core/Wallet.cs`, `Assets/Scripts/Core/EconomyTuning.cs`, `Assets/Scripts/Core/MaintenanceCosts.cs`, `Assets/Tests/EditMode/EconomyTests.cs`

**Interfaces:**
- Consumes: Task 1 `GameCalendar`、`GrowthStage`
- Produces:
  - `enum LedgerCategory { Food, Electricity, BoothFee, Purchase, EventSale, Wholesale, Other }`
  - `sealed class LedgerEntry { DateTimeOffset AtUtc; LedgerCategory Category; long Amount; string Note; }`（`Amount` は収入が正、支出が負）
  - `Wallet`：`long Money`、`List<LedgerEntry> Ledger`、`bool TrySpend(long amount, LedgerCategory category, string note, DateTimeOffset atUtc)`、`void Charge(long amount, …)`（マイナスになってもよい）、`void Earn(long amount, …)`
  - `EconomyTuning`：`StartingMoney=50000`、`FeedCostBaby=30`、`FeedCostJuvenile=50`、`FeedCostAdult=80`、`ElectricityPerCagePerMonth=300`、`ElectricityPerIncubatorPerMonth=500`
  - `MaintenanceCosts.FeedCost(GrowthStage, EconomyTuning) : long`、`MaintenanceCosts.MonthlyElectricity(int cages, int incubators, EconomyTuning) : long`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EconomyTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero);
        private readonly EconomyTuning tuning = new EconomyTuning();

        [Test]
        public void TrySpend_RecordsAnExpenseWhenAffordable()
        {
            var wallet = new Wallet { Money = 100 };

            Assert.That(wallet.TrySpend(80, LedgerCategory.Food, "餌代", Now), Is.True);

            Assert.That(wallet.Money, Is.EqualTo(20));
            Assert.That(wallet.Ledger[0].Amount, Is.EqualTo(-80));
            Assert.That(wallet.Ledger[0].Category, Is.EqualTo(LedgerCategory.Food));
        }

        [Test]
        public void TrySpend_RefusesWithoutEnoughMoneyAndRecordsNothing()
        {
            var wallet = new Wallet { Money = 10 };

            Assert.That(wallet.TrySpend(30, LedgerCategory.Food, "餌代", Now), Is.False);

            Assert.That(wallet.Money, Is.EqualTo(10));
            Assert.That(wallet.Ledger, Is.Empty);
        }

        [Test]
        public void Charge_MayGoNegative()
        {
            var wallet = new Wallet { Money = 100 };

            wallet.Charge(800, LedgerCategory.Electricity, "電気代", Now);

            Assert.That(wallet.Money, Is.EqualTo(-700));
        }

        [TestCase(GrowthStage.Baby, 30)]
        [TestCase(GrowthStage.Juvenile, 50)]
        [TestCase(GrowthStage.Adult, 80)]
        public void FeedCost_DependsOnTheStage(GrowthStage stage, long yen)
        {
            Assert.That(MaintenanceCosts.FeedCost(stage, tuning), Is.EqualTo(yen));
        }

        [Test]
        public void MonthlyElectricity_IsPerCageAndPerIncubator()
        {
            Assert.That(MaintenanceCosts.MonthlyElectricity(3, 1, tuning), Is.EqualTo(3 * 300 + 500));
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: コンパイルエラー（`Wallet` などがない）

- [ ] **Step 3: 実装する**

`Assets/Scripts/Core/Wallet.cs`

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public enum LedgerCategory
    {
        Food,
        Electricity,
        BoothFee,
        Purchase,
        EventSale,
        Wholesale,
        Other
    }

    /// <summary>One money movement: positive = income, negative = expense.</summary>
    public sealed class LedgerEntry
    {
        public DateTimeOffset AtUtc { get; set; }
        public LedgerCategory Category { get; set; }
        public long Amount { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Money on hand plus every movement, for the ledger screen.</summary>
    public sealed class Wallet
    {
        public long Money { get; set; }

        public List<LedgerEntry> Ledger { get; set; } = new List<LedgerEntry>();

        /// <summary>Pays only if affordable (e.g. food). Returns false and records nothing otherwise.</summary>
        public bool TrySpend(long amount, LedgerCategory category, string note, DateTimeOffset atUtc)
        {
            if (amount > Money)
            {
                return false;
            }

            Charge(amount, category, note, atUtc);
            return true;
        }

        /// <summary>A bill that must be paid even into the red (e.g. electricity).</summary>
        public void Charge(long amount, LedgerCategory category, string note, DateTimeOffset atUtc)
        {
            Money -= amount;
            Ledger.Add(new LedgerEntry { AtUtc = atUtc, Category = category, Amount = -amount, Note = note });
        }

        public void Earn(long amount, LedgerCategory category, string note, DateTimeOffset atUtc)
        {
            Money += amount;
            Ledger.Add(new LedgerEntry { AtUtc = atUtc, Category = category, Amount = amount, Note = note });
        }
    }
}
```

`Assets/Scripts/Core/EconomyTuning.cs`

```csharp
namespace TerrariumDays.Core
{
    /// <summary>Every price and running cost in yen, in one place.</summary>
    public sealed class EconomyTuning
    {
        public long StartingMoney { get; set; } = 50000;

        public long FeedCostBaby { get; set; } = 30;
        public long FeedCostJuvenile { get; set; } = 50;
        public long FeedCostAdult { get; set; } = 80;

        public long ElectricityPerCagePerMonth { get; set; } = 300;
        public long ElectricityPerIncubatorPerMonth { get; set; } = 500;
    }
}
```

`Assets/Scripts/Core/MaintenanceCosts.cs`

```csharp
namespace TerrariumDays.Core
{
    public static class MaintenanceCosts
    {
        public static long FeedCost(GrowthStage stage, EconomyTuning tuning)
        {
            switch (stage)
            {
                case GrowthStage.Juvenile:
                    return tuning.FeedCostJuvenile;
                case GrowthStage.Adult:
                    return tuning.FeedCostAdult;
                default:
                    return tuning.FeedCostBaby;
            }
        }

        public static long MonthlyElectricity(int cages, int incubators, EconomyTuning tuning) =>
            cages * tuning.ElectricityPerCagePerMonth + incubators * tuning.ElectricityPerIncubatorPerMonth;
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: 失敗0

- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/Wallet.cs Assets/Scripts/Core/EconomyTuning.cs Assets/Scripts/Core/MaintenanceCosts.cs Assets/Tests/EditMode/EconomyTests.cs
git commit -m "Add wallet, ledger, feed and electricity costs"
```

---

### Task 5: コロニー（個体・ケージ・ラック）と一括の世話

**Files:**
- Create: `Assets/Scripts/Core/Colony.cs`, `Assets/Scripts/Gameplay/ColonyCareService.cs`, `Assets/Tests/EditMode/ColonyTests.cs`

**Interfaces:**
- Consumes: Task 2 `PetState`・`GrowthModel`、Task 3 `AppetiteModel`、Task 4 `Wallet`・`EconomyTuning`・`MaintenanceCosts`
- Produces:
  - `enum CageSize { Small, Standard, Large }`
  - `sealed class Cage { int Id; CageSize Size; int AnimalId = -1; bool IsEmpty; }`
  - `Colony`：`DateTimeOffset CalendarEpochUtc`、`Wallet Wallet`、`List<PetState> Animals`、`List<Cage> Cages`、`int RackCount`、`int IncubatorCount`、`int NextAnimalId`、`int NextCageId`、`int LastBilledMonthIndex`、`const int CagesPerRack = 4`、`int CageCapacity`、`PetState AnimalById(int)`、`PetState AnimalIn(Cage)`、`Cage CageOf(PetState)`、`bool CanAddCage`、`Cage AddCage(CageSize)`（満杯なら null）、`PetState AddAnimal(PetState, Cage)`、`List<Cage> OccupiedCages()`、`static Colony CreateNew(DateTimeOffset nowUtc, EconomyTuning, CareTuning, Random)`
  - `enum FeedOutcome { Ate, RefusedPreShed, RefusedPreGrowth, NotEnoughMoney }`
  - `sealed class BulkCareResult { int Fed; int Refused; int NoMoney; string ToMessage(); }`
  - `ColonyCareService(CareTuning, EconomyTuning)`：`FeedOutcome Feed(Colony, PetState, DateTimeOffset)`、`BulkCareResult FeedAll(Colony, DateTimeOffset)`、`int RefreshWaterAll(Colony)`、`int CleanAll(Colony)`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class ColonyTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly EconomyTuning economy = new EconomyTuning();

        private Colony NewColony() => Colony.CreateNew(Now, economy, care, new Random(1));

        [Test]
        public void ANewColonyHasOneBabyInOneStandardCageAndStartingMoney()
        {
            var colony = NewColony();

            Assert.That(colony.Wallet.Money, Is.EqualTo(50000));
            Assert.That(colony.RackCount, Is.EqualTo(1));
            Assert.That(colony.IncubatorCount, Is.EqualTo(1));
            Assert.That(colony.Cages, Has.Count.EqualTo(1));
            Assert.That(colony.Cages[0].Size, Is.EqualTo(CageSize.Standard));
            var pet = colony.AnimalIn(colony.Cages[0]);
            Assert.That(pet.Id, Is.EqualTo(1));
            Assert.That(pet.Stage, Is.EqualTo(GrowthStage.Baby));
            Assert.That(colony.CageOf(pet), Is.SameAs(colony.Cages[0]));
        }

        [Test]
        public void ARackHoldsFourCages()
        {
            var colony = NewColony();

            for (var i = 0; i < 3; i++)
            {
                Assert.That(colony.AddCage(CageSize.Small), Is.Not.Null);
            }

            Assert.That(colony.CanAddCage, Is.False);
            Assert.That(colony.AddCage(CageSize.Small), Is.Null);

            colony.RackCount = 2;
            Assert.That(colony.AddCage(CageSize.Large).Id, Is.EqualTo(5));
        }

        [Test]
        public void AddAnimal_GivesItTheNextIdAndPutsItInTheCage()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);

            var pet = colony.AddAnimal(new PetState(), cage);

            Assert.That(pet.Id, Is.EqualTo(2));
            Assert.That(cage.AnimalId, Is.EqualTo(2));
            Assert.That(colony.OccupiedCages(), Has.Count.EqualTo(2));
        }

        [Test]
        public void Feed_ChargesTheFeedCostAndAddsWeight()
        {
            var colony = NewColony();
            var pet = colony.Animals[0];
            pet.NextShedAtUtc = Now.AddDays(3);
            var weight = pet.WeightGrams;

            var outcome = new ColonyCareService(care, economy).Feed(colony, pet, Now);

            Assert.That(outcome, Is.EqualTo(FeedOutcome.Ate));
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000 - 30));
            Assert.That(pet.WeightGrams, Is.GreaterThan(weight));
        }

        [Test]
        public void Feed_WithoutMoney_DoesNotFeed()
        {
            var colony = NewColony();
            colony.Wallet.Money = 10;
            var pet = colony.Animals[0];
            pet.NextShedAtUtc = Now.AddDays(3);
            var hunger = pet.Hunger;

            Assert.That(new ColonyCareService(care, economy).Feed(colony, pet, Now), Is.EqualTo(FeedOutcome.NotEnoughMoney));
            Assert.That(pet.Hunger, Is.EqualTo(hunger));
        }

        [Test]
        public void Feed_WhileFasting_IsFreeAndRefused()
        {
            var colony = NewColony();
            var pet = colony.Animals[0];
            pet.NextShedAtUtc = Now.AddMinutes(10);

            Assert.That(new ColonyCareService(care, economy).Feed(colony, pet, Now), Is.EqualTo(FeedOutcome.RefusedPreShed));
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000));
        }

        [Test]
        public void FeedAll_ReportsWhoAteAndWhoRefused()
        {
            var colony = NewColony();
            colony.Animals[0].NextShedAtUtc = Now.AddDays(3);
            var second = colony.AddAnimal(new PetState { NextShedAtUtc = Now.AddMinutes(10) }, colony.AddCage(CageSize.Standard));

            var result = new ColonyCareService(care, economy).FeedAll(colony, Now);

            Assert.That((result.Fed, result.Refused, result.NoMoney), Is.EqualTo((1, 1, 0)));
            Assert.That(result.ToMessage(), Is.EqualTo("1匹が食べました（1匹は拒食中）"));
            Assert.That(second.Hunger, Is.EqualTo(80d));
        }

        [Test]
        public void WaterAndCleanAll_CareForEveryPet()
        {
            var colony = NewColony();
            colony.AddAnimal(new PetState { Hydration = 10d, Cleanliness = 10d }, colony.AddCage(CageSize.Standard));
            var service = new ColonyCareService(care, economy);

            Assert.That(service.RefreshWaterAll(colony), Is.EqualTo(2));
            Assert.That(service.CleanAll(colony), Is.EqualTo(2));
            Assert.That(colony.Animals[1].Hydration, Is.EqualTo(10d + care.RefreshWaterHydrationAmount));
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: コンパイルエラー（`Colony` などがない）

- [ ] **Step 3: `Colony.cs` を作る**

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public enum CageSize
    {
        Small,
        Standard,
        Large
    }

    /// <summary>One cage on a rack; holds at most one animal (pairing comes later).</summary>
    public sealed class Cage
    {
        public int Id { get; set; }
        public CageSize Size { get; set; } = CageSize.Standard;
        public int AnimalId { get; set; } = -1;
        public bool IsEmpty => AnimalId < 0;
    }

    /// <summary>
    /// The whole breeding room: animals, cages on racks, money and the calendar anchor.
    /// Plain data plus lookups; rules live in the services that use it.
    /// </summary>
    public sealed class Colony
    {
        public const int CagesPerRack = 4;

        public DateTimeOffset CalendarEpochUtc { get; set; }
        public Wallet Wallet { get; set; } = new Wallet();
        public List<PetState> Animals { get; set; } = new List<PetState>();
        public List<Cage> Cages { get; set; } = new List<Cage>();
        public int RackCount { get; set; } = 1;
        public int IncubatorCount { get; set; } = 1;
        public int NextAnimalId { get; set; } = 1;
        public int NextCageId { get; set; } = 1;
        public int LastBilledMonthIndex { get; set; }

        public int CageCapacity => RackCount * CagesPerRack;

        public bool CanAddCage => Cages.Count < CageCapacity;

        public PetState AnimalById(int id) => Animals.Find(a => a.Id == id);

        public PetState AnimalIn(Cage cage) => cage == null || cage.IsEmpty ? null : AnimalById(cage.AnimalId);

        public Cage CageOf(PetState pet) => pet == null ? null : Cages.Find(c => c.AnimalId == pet.Id);

        public List<Cage> OccupiedCages() => Cages.FindAll(c => !c.IsEmpty);

        public Cage AddCage(CageSize size)
        {
            if (!CanAddCage)
            {
                return null;
            }

            var cage = new Cage { Id = NextCageId++, Size = size };
            Cages.Add(cage);
            return cage;
        }

        public PetState AddAnimal(PetState pet, Cage cage)
        {
            pet.Id = NextAnimalId++;
            Animals.Add(pet);
            if (cage != null)
            {
                cage.AnimalId = pet.Id;
            }

            return pet;
        }

        /// <summary>A fresh room: one baby of random sex in one standard cage, starting money, one simple incubator.</summary>
        public static Colony CreateNew(DateTimeOffset nowUtc, EconomyTuning economy, CareTuning care, Random random)
        {
            var colony = new Colony { CalendarEpochUtc = nowUtc };
            colony.Wallet.Money = economy.StartingMoney;
            var cage = colony.AddCage(CageSize.Standard);
            colony.AddAnimal(new PetState
            {
                Name = "レオパ1",
                Sex = random.NextDouble() < 0.5 ? Sex.Female : Sex.Male,
                WeightGrams = care.HatchlingWeightGrams,
                HatchedAtUtc = nowUtc,
                LastSavedAtUtc = nowUtc,
                LastShedAtUtc = nowUtc,
                NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(GrowthStage.Baby, care),
            }, cage);
            return colony;
        }
    }
}
```

- [ ] **Step 4: `ColonyCareService.cs` を作る**

```csharp
using System;
using TerrariumDays.Core;

namespace TerrariumDays.Gameplay
{
    public enum FeedOutcome
    {
        Ate,
        RefusedPreShed,
        RefusedPreGrowth,
        NotEnoughMoney
    }

    public sealed class BulkCareResult
    {
        public int Fed { get; set; }
        public int Refused { get; set; }
        public int NoMoney { get; set; }

        public string ToMessage()
        {
            var text = $"{Fed}匹が食べました";
            if (Refused > 0 && NoMoney > 0)
            {
                return text + $"（{Refused}匹は拒食中、{NoMoney}匹はお金が足りず）";
            }

            if (Refused > 0)
            {
                return text + $"（{Refused}匹は拒食中）";
            }

            return NoMoney > 0 ? text + $"（{NoMoney}匹はお金が足りず）" : text;
        }
    }

    /// <summary>Care across the colony, with food paid for from the wallet.</summary>
    public sealed class ColonyCareService
    {
        private readonly CareTuning care;
        private readonly EconomyTuning economy;
        private readonly CareService careService;

        public ColonyCareService(CareTuning care, EconomyTuning economy)
        {
            this.care = care;
            this.economy = economy;
            careService = new CareService(care);
        }

        public FeedOutcome Feed(Colony colony, PetState pet, DateTimeOffset nowUtc)
        {
            var appetite = AppetiteModel.Evaluate(pet, nowUtc, care);
            if (appetite == AppetiteState.PreShed)
            {
                return FeedOutcome.RefusedPreShed;
            }

            if (appetite == AppetiteState.PreGrowth)
            {
                return FeedOutcome.RefusedPreGrowth;
            }

            var cost = MaintenanceCosts.FeedCost(pet.Stage, economy);
            if (!colony.Wallet.TrySpend(cost, LedgerCategory.Food, $"餌代（{pet.Name}）", nowUtc))
            {
                return FeedOutcome.NotEnoughMoney;
            }

            careService.Feed(pet);
            return FeedOutcome.Ate;
        }

        public BulkCareResult FeedAll(Colony colony, DateTimeOffset nowUtc)
        {
            var result = new BulkCareResult();
            foreach (var cage in colony.OccupiedCages())
            {
                switch (Feed(colony, colony.AnimalIn(cage), nowUtc))
                {
                    case FeedOutcome.Ate:
                        result.Fed++;
                        break;
                    case FeedOutcome.NotEnoughMoney:
                        result.NoMoney++;
                        break;
                    default:
                        result.Refused++;
                        break;
                }
            }

            return result;
        }

        public int RefreshWaterAll(Colony colony)
        {
            var count = 0;
            foreach (var cage in colony.OccupiedCages())
            {
                careService.RefreshWater(colony.AnimalIn(cage));
                count++;
            }

            return count;
        }

        public int CleanAll(Colony colony)
        {
            var count = 0;
            foreach (var cage in colony.OccupiedCages())
            {
                careService.Clean(colony.AnimalIn(cage));
                count++;
            }

            return count;
        }
    }
}
```

- [ ] **Step 5: テストが通ることを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: 失敗0

- [ ] **Step 6: コミット**

```bash
git add Assets/Scripts/Core/Colony.cs Assets/Scripts/Gameplay/ColonyCareService.cs Assets/Tests/EditMode/ColonyTests.cs
git commit -m "Add the colony model with racks, cages and paid bulk care"
```

---

### Task 6: スキーマ3の保存と、スキーマ1/2からの移行

**Files:**
- Create: `Assets/Scripts/Core/ColonySaveData.cs`, `Assets/Scripts/Core/ColonySaveService.cs`, `Assets/Tests/EditMode/ColonySaveServiceTests.cs`
- Delete: `Assets/Scripts/Core/SaveService.cs`, `Assets/Tests/EditMode/SaveServiceTests.cs`（スキーマ1/2の読み込みは `ColonySaveService` の移行に含める。`PetSaveData.cs` は移行用の形として残す）

**Interfaces:**
- Consumes: Task 5 `Colony`・`Cage`、Task 2 `PetState`、`PetSaveData`（既存）
- Produces: `ColonySaveService(CareTuning, EconomyTuning)`：`Colony LoadOrCreate(string path, DateTimeOffset nowUtc, Random random)`、`void Save(string path, Colony colony)`、`const int CurrentSchemaVersion = 3`、`static string BackupPathFor(string path)`（`path + ".v2.bak"`）、`bool LastLoadMigrated`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using System.IO;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class ColonySaveServiceTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        private readonly ColonySaveService service = new ColonySaveService(new CareTuning(), new EconomyTuning());
        private string path;

        [SetUp]
        public void SetUp() => path = Path.Combine(Path.GetTempPath(), $"colony-{Guid.NewGuid():N}.json");

        [TearDown]
        public void TearDown()
        {
            File.Delete(path);
            File.Delete(ColonySaveService.BackupPathFor(path));
        }

        [Test]
        public void WithoutAFile_CreatesAndSavesANewColony()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(service.LastLoadMigrated, Is.False);
        }

        [Test]
        public void RoundTripsEverything()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            var pet = colony.Animals[0];
            pet.Name = "ハナ";
            pet.Sex = Sex.Male;
            pet.WeightGrams = 22.5d;
            pet.Stage = GrowthStage.Juvenile;
            pet.StageUpDueAtUtc = Now.AddMinutes(90);
            pet.Hunger = 41d;
            colony.AddCage(CageSize.Large);
            colony.RackCount = 2;
            colony.LastBilledMonthIndex = 3;
            colony.Wallet.Charge(300, LedgerCategory.Electricity, "電気代", Now);

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            var back = loaded.Animals[0];
            Assert.That((back.Name, back.Sex, back.WeightGrams, back.Stage), Is.EqualTo(("ハナ", Sex.Male, 22.5d, GrowthStage.Juvenile)));
            Assert.That(back.StageUpDueAtUtc, Is.EqualTo(Now.AddMinutes(90)));
            Assert.That(back.Hunger, Is.EqualTo(41d));
            Assert.That(back.HatchedAtUtc, Is.EqualTo(pet.HatchedAtUtc));
            Assert.That(loaded.Cages, Has.Count.EqualTo(2));
            Assert.That(loaded.Cages[1].Size, Is.EqualTo(CageSize.Large));
            Assert.That(loaded.Cages[0].AnimalId, Is.EqualTo(back.Id));
            Assert.That((loaded.RackCount, loaded.LastBilledMonthIndex, loaded.NextAnimalId, loaded.NextCageId), Is.EqualTo((2, 3, 2, 3)));
            Assert.That(loaded.Wallet.Money, Is.EqualTo(49700));
            Assert.That(loaded.Wallet.Ledger[0].Category, Is.EqualTo(LedgerCategory.Electricity));
            Assert.That(loaded.CalendarEpochUtc, Is.EqualTo(Now));
        }

        [Test]
        public void ASchemaTwoSave_IsMigratedIntoCageOneAndBackedUp()
        {
            File.WriteAllText(path,
                "{\"schemaVersion\":2,\"lastSavedAtUtc\":\"2026-09-25T11:00:00.0000000+00:00\",\"hunger\":70,\"hydration\":60," +
                "\"cleanliness\":50,\"health\":90,\"growth\":50,\"growthStage\":\"Juvenile\",\"selectedDecorId\":\"plant_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\",\"plant_01\"],\"lastShedAtUtc\":\"2026-09-20T00:00:00.0000000+00:00\"," +
                "\"nextShedAtUtc\":\"2026-11-19T00:00:00.0000000+00:00\"}");

            var colony = service.LoadOrCreate(path, Now, new Random(3));

            Assert.That(service.LastLoadMigrated, Is.True);
            Assert.That(File.Exists(ColonySaveService.BackupPathFor(path)), Is.True);
            var pet = colony.AnimalIn(colony.Cages[0]);
            Assert.That(pet.Stage, Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(pet.WeightGrams, Is.EqualTo(15d).Within(1e-9));
            Assert.That((pet.Hunger, pet.Hydration, pet.Cleanliness, pet.Health), Is.EqualTo((70d, 60d, 50d, 90d)));
            Assert.That(pet.SelectedDecorId, Is.EqualTo("plant_01"));
            Assert.That(pet.UnlockedDecorIds, Is.EquivalentTo(new[] { "rock_01", "plant_01" }));
            Assert.That(pet.HatchedAtUtc, Is.EqualTo(Now.AddDays(-5)));
            Assert.That(pet.NextShedAtUtc - Now, Is.LessThanOrEqualTo(SheddingModel.IntervalFor(GrowthStage.Juvenile, new CareTuning())),
                "the old 60-real-day shed date is pulled into the new game-time cycle");
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000));
            Assert.That(colony.IncubatorCount, Is.EqualTo(1));
        }

        [TestCase(0d, 3d)]
        [TestCase(25d, 9d)]
        [TestCase(75d, 30d)]
        [TestCase(100d, 45d)]
        public void MigratedWeight_FollowsTheOldGrowthGauge(double growth, double grams)
        {
            Assert.That(ColonySaveService.WeightFromLegacyGrowth(growth), Is.EqualTo(grams).Within(1e-9));
        }

        [Test]
        public void AnUnreadableFile_StartsFreshWithoutOverwritingIt()
        {
            File.WriteAllText(path, "not json");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(File.ReadAllText(path), Is.EqualTo("not json"));
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: コンパイルエラー（`ColonySaveService` がない）

- [ ] **Step 3: `ColonySaveData.cs` を作る**

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Schema-3 save: the whole colony. JsonUtility shape (public fields, lists only).</summary>
    [Serializable]
    public sealed class ColonySaveData
    {
        public int schemaVersion;
        public string calendarEpochUtc;
        public long money;
        public List<LedgerSaveData> ledger = new List<LedgerSaveData>();
        public List<AnimalSaveData> animals = new List<AnimalSaveData>();
        public List<CageSaveData> cages = new List<CageSaveData>();
        public int rackCount;
        public int incubatorCount;
        public int nextAnimalId;
        public int nextCageId;
        public int lastBilledMonthIndex;
    }

    [Serializable]
    public sealed class AnimalSaveData
    {
        public int id;
        public string name;
        public string sex;
        public double weightGrams;
        public string hatchedAtUtc;
        public string stage;
        public string stageUpDueAtUtc;
        public double hunger;
        public double hydration;
        public double cleanliness;
        public double health;
        public string selectedDecorId;
        public List<string> unlockedDecorIds;
        public string lastSavedAtUtc;
        public string lastShedAtUtc;
        public string nextShedAtUtc;
    }

    [Serializable]
    public sealed class CageSaveData
    {
        public int id;
        public string size;
        public int animalId;
    }

    [Serializable]
    public sealed class LedgerSaveData
    {
        public string atUtc;
        public string category;
        public long amount;
        public string note;
    }

    /// <summary>Reads only the schema version, to choose between loading and migrating.</summary>
    [Serializable]
    public sealed class SchemaProbe
    {
        public int schemaVersion;
    }
}
```

- [ ] **Step 4: `ColonySaveService.cs` を作る**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Saves and loads the colony (schema 3). Schema 1/2 single-pet saves are migrated into
    /// cage 1 and the original file is kept as a backup.
    /// </summary>
    public sealed class ColonySaveService
    {
        public const int CurrentSchemaVersion = 3;
        private const string TimestampFormat = "o";

        private readonly CareTuning care;
        private readonly EconomyTuning economy;

        public ColonySaveService(CareTuning care, EconomyTuning economy)
        {
            this.care = care;
            this.economy = economy;
        }

        public bool LastLoadMigrated { get; private set; }

        public static string BackupPathFor(string path) => path + ".v2.bak";

        public Colony LoadOrCreate(string path, DateTimeOffset nowUtc, System.Random random)
        {
            LastLoadMigrated = false;
            if (!File.Exists(path))
            {
                var created = Colony.CreateNew(nowUtc, economy, care, random);
                Save(path, created);
                return created;
            }

            try
            {
                var json = File.ReadAllText(path);
                var probe = JsonUtility.FromJson<SchemaProbe>(json);
                if (probe == null)
                {
                    throw new InvalidDataException("Empty save.");
                }

                if (probe.schemaVersion >= CurrentSchemaVersion)
                {
                    return FromSaveData(JsonUtility.FromJson<ColonySaveData>(json));
                }

                File.Copy(path, BackupPathFor(path), true);
                var migrated = MigrateLegacy(JsonUtility.FromJson<PetSaveData>(json), nowUtc, random);
                LastLoadMigrated = true;
                Save(path, migrated);
                return migrated;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Save data at '{path}' could not be loaded ({ex.Message}); starting fresh without overwriting the file.");
                return Colony.CreateNew(nowUtc, economy, care, random);
            }
        }

        public void Save(string path, Colony colony)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(ToSaveData(colony)));
        }

        /// <summary>Old 0–100 growth gauge → grams: 0→3, 50→15, 100→45.</summary>
        public static double WeightFromLegacyGrowth(double growth)
        {
            growth = Math.Max(0d, Math.Min(100d, growth));
            return growth <= 50d ? 3d + growth / 50d * 12d : 15d + (growth - 50d) / 50d * 30d;
        }

        private Colony MigrateLegacy(PetSaveData data, DateTimeOffset nowUtc, System.Random random)
        {
            var colony = new Colony { CalendarEpochUtc = nowUtc };
            colony.Wallet.Money = economy.StartingMoney;
            var cage = colony.AddCage(CageSize.Standard);
            var stage = Enum.TryParse(data.growthStage, out GrowthStage parsed) ? parsed : GrowthStage.Baby;
            var ageMonths = stage == GrowthStage.Adult ? 12d : stage == GrowthStage.Juvenile ? 5d : 1d;
            var lastSaved = Parse(data.lastSavedAtUtc, nowUtc);
            var nextShed = Parse(data.nextShedAtUtc, nowUtc);
            var interval = SheddingModel.IntervalFor(stage, care);
            if (nextShed - nowUtc > interval)
            {
                nextShed = nowUtc + interval;
            }

            colony.AddAnimal(new PetState
            {
                Name = "レオパ1",
                Sex = random.NextDouble() < 0.5 ? Sex.Female : Sex.Male,
                WeightGrams = WeightFromLegacyGrowth(data.growth),
                Stage = stage,
                HatchedAtUtc = nowUtc.AddDays(-ageMonths),
                Hunger = data.hunger,
                Hydration = data.hydration,
                Cleanliness = data.cleanliness,
                Health = data.health,
                SelectedDecorId = string.IsNullOrEmpty(data.selectedDecorId) ? PetState.DefaultDecorId : data.selectedDecorId,
                UnlockedDecorIds = data.unlockedDecorIds ?? new List<string> { PetState.DefaultDecorId },
                LastSavedAtUtc = lastSaved,
                LastShedAtUtc = Parse(data.lastShedAtUtc, lastSaved),
                NextShedAtUtc = nextShed,
            }, cage);
            return colony;
        }

        private static Colony FromSaveData(ColonySaveData data)
        {
            var colony = new Colony
            {
                CalendarEpochUtc = Parse(data.calendarEpochUtc, DateTimeOffset.UtcNow),
                RackCount = data.rackCount,
                IncubatorCount = data.incubatorCount,
                NextAnimalId = data.nextAnimalId,
                NextCageId = data.nextCageId,
                LastBilledMonthIndex = data.lastBilledMonthIndex,
            };
            colony.Wallet.Money = data.money;
            foreach (var entry in data.ledger)
            {
                colony.Wallet.Ledger.Add(new LedgerEntry
                {
                    AtUtc = Parse(entry.atUtc, DateTimeOffset.UtcNow),
                    Category = Enum.TryParse(entry.category, out LedgerCategory category) ? category : LedgerCategory.Other,
                    Amount = entry.amount,
                    Note = entry.note,
                });
            }

            foreach (var a in data.animals)
            {
                colony.Animals.Add(new PetState
                {
                    Id = a.id,
                    Name = a.name,
                    Sex = Enum.TryParse(a.sex, out Sex sex) ? sex : Sex.Female,
                    WeightGrams = a.weightGrams,
                    HatchedAtUtc = Parse(a.hatchedAtUtc, DateTimeOffset.UtcNow),
                    Stage = Enum.TryParse(a.stage, out GrowthStage stage) ? stage : GrowthStage.Baby,
                    StageUpDueAtUtc = string.IsNullOrEmpty(a.stageUpDueAtUtc) ? (DateTimeOffset?)null : Parse(a.stageUpDueAtUtc, DateTimeOffset.UtcNow),
                    Hunger = a.hunger,
                    Hydration = a.hydration,
                    Cleanliness = a.cleanliness,
                    Health = a.health,
                    SelectedDecorId = string.IsNullOrEmpty(a.selectedDecorId) ? PetState.DefaultDecorId : a.selectedDecorId,
                    UnlockedDecorIds = a.unlockedDecorIds ?? new List<string> { PetState.DefaultDecorId },
                    LastSavedAtUtc = Parse(a.lastSavedAtUtc, DateTimeOffset.UtcNow),
                    LastShedAtUtc = Parse(a.lastShedAtUtc, DateTimeOffset.UtcNow),
                    NextShedAtUtc = Parse(a.nextShedAtUtc, DateTimeOffset.UtcNow),
                });
            }

            foreach (var c in data.cages)
            {
                colony.Cages.Add(new Cage
                {
                    Id = c.id,
                    Size = Enum.TryParse(c.size, out CageSize size) ? size : CageSize.Standard,
                    AnimalId = c.animalId,
                });
            }

            return colony;
        }

        private static ColonySaveData ToSaveData(Colony colony)
        {
            var data = new ColonySaveData
            {
                schemaVersion = CurrentSchemaVersion,
                calendarEpochUtc = Format(colony.CalendarEpochUtc),
                money = colony.Wallet.Money,
                rackCount = colony.RackCount,
                incubatorCount = colony.IncubatorCount,
                nextAnimalId = colony.NextAnimalId,
                nextCageId = colony.NextCageId,
                lastBilledMonthIndex = colony.LastBilledMonthIndex,
            };
            foreach (var entry in colony.Wallet.Ledger)
            {
                data.ledger.Add(new LedgerSaveData
                {
                    atUtc = Format(entry.AtUtc),
                    category = entry.Category.ToString(),
                    amount = entry.Amount,
                    note = entry.Note,
                });
            }

            foreach (var a in colony.Animals)
            {
                data.animals.Add(new AnimalSaveData
                {
                    id = a.Id,
                    name = a.Name,
                    sex = a.Sex.ToString(),
                    weightGrams = a.WeightGrams,
                    hatchedAtUtc = Format(a.HatchedAtUtc),
                    stage = a.Stage.ToString(),
                    stageUpDueAtUtc = a.StageUpDueAtUtc.HasValue ? Format(a.StageUpDueAtUtc.Value) : string.Empty,
                    hunger = a.Hunger,
                    hydration = a.Hydration,
                    cleanliness = a.Cleanliness,
                    health = a.Health,
                    selectedDecorId = a.SelectedDecorId,
                    unlockedDecorIds = a.UnlockedDecorIds,
                    lastSavedAtUtc = Format(a.LastSavedAtUtc),
                    lastShedAtUtc = Format(a.LastShedAtUtc),
                    nextShedAtUtc = Format(a.NextShedAtUtc),
                });
            }

            foreach (var c in colony.Cages)
            {
                data.cages.Add(new CageSaveData { id = c.Id, size = c.Size.ToString(), animalId = c.AnimalId });
            }

            return data;
        }

        private static string Format(DateTimeOffset value) => value.ToString(TimestampFormat, CultureInfo.InvariantCulture);

        private static DateTimeOffset Parse(string value, DateTimeOffset fallback) =>
            string.IsNullOrEmpty(value) ? fallback : DateTimeOffset.ParseExact(value, TimestampFormat, CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 5: `SaveService.cs`・`SaveServiceTests.cs`（`.meta` も）を削除し、テストが通ることを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: 失敗0（`TerrariumView` がまだ `SaveService` を使っているので、アセンブリが別でなければ Task 8 と一緒に確認する）

- [ ] **Step 6: コミット**

```bash
git add -A Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Save the colony as schema 3 and migrate single-pet saves into cage 1"
```

---

### Task 7: 飼育セッション（全個体の時間経過・再同期・保存・電気代）

**Files:**
- Create: `Assets/Scripts/Core/ColonySession.cs`, `Assets/Tests/EditMode/ColonySessionTests.cs`

**Interfaces:**
- Consumes: Task 1〜6 のすべて、既存 `TimeService`・`OfflineProgressCalculator`
- Produces:
  - `sealed class ColonyTickReport { List<(PetState Pet, GrowthStage Stage)> StageUps; List<PetState> Sheds; long ElectricityCharged; TimeSpan AppliedElapsed; }`
  - `ColonySession(string savePath, TimeService clock, CareTuning care, EconomyTuning economy, System.Random random)`
  - `Colony Colony`、`GameCalendar Calendar`、`DateTimeOffset GameNowUtc`、`bool Migrated`
  - `ColonyTickReport Load()`：読み込み（または作成・移行）→ 閉じていた間を全個体に適用 → 電気代 → 再同期 → 保存
  - `ColonyTickReport Advance(TimeSpan realDelta)`：開いている間の時間経過（`TimeService.ScaleElapsed` で倍率をかける）
  - `ColonyTickReport Resume()`：バックグラウンドからの復帰
  - `ColonyTickReport SimulateGameTime(TimeSpan span)`：デバッグ用（全個体を span だけ進める）
  - `void Save()`：全個体を再同期してから保存（既存の `ResyncClock` の考え方を全個体に適用）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using System.IO;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class ColonySessionTests
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly EconomyTuning economy = new EconomyTuning();
        private DateTimeOffset realNow;
        private string path;

        [SetUp]
        public void SetUp()
        {
            realNow = Start;
            path = Path.Combine(Path.GetTempPath(), $"session-{Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown() => File.Delete(path);

        private ColonySession NewSession() =>
            new ColonySession(path, new TimeService(() => realNow), care, economy, new Random(4));

        [Test]
        public void Load_AppliesTheTimeAwayToEveryAnimal()
        {
            var first = NewSession();
            first.Load();
            first.Colony.AddAnimal(new PetState { LastSavedAtUtc = realNow, NextShedAtUtc = realNow.AddDays(3) }, first.Colony.AddCage(CageSize.Standard));
            first.Colony.Animals[0].NextShedAtUtc = realNow.AddDays(3);
            first.Save();

            realNow += TimeSpan.FromHours(2);
            var second = NewSession();
            second.Load();

            foreach (var pet in second.Colony.Animals)
            {
                Assert.That(pet.Hunger, Is.EqualTo(80d - care.HungerDecayPerHour * 2d).Within(1e-6));
            }
        }

        [Test]
        public void ANewGameMonth_BillsElectricityOncePerMonth()
        {
            var session = NewSession();
            session.Load();

            realNow += TimeSpan.FromHours(12);
            var sameMonth = session.Resume();
            realNow += TimeSpan.FromHours(13);
            var nextMonth = session.Resume();

            Assert.That(sameMonth.ElectricityCharged, Is.EqualTo(0));
            Assert.That(nextMonth.ElectricityCharged, Is.EqualTo(MaintenanceCosts.MonthlyElectricity(1, 1, economy)));
            Assert.That(session.Colony.Wallet.Money, Is.EqualTo(50000 - 800));
            Assert.That(session.Colony.LastBilledMonthIndex, Is.EqualTo(1));
        }

        [Test]
        public void ACareActionAfterTheDebugClockRanAhead_IsNotUndoneByTheNextTick()
        {
            var session = NewSession();
            session.Load();
            var pet = session.Colony.Animals[0];
            pet.NextShedAtUtc = realNow.AddDays(3);
            var clock = new TimeService(() => realNow) { TimeMultiplier = 600d };
            session.UseClock(clock);

            session.Advance(TimeSpan.FromSeconds(6)); // one game-clock hour ahead
            new CareService(care).Clean(pet);
            session.Save();
            session.Advance(TimeSpan.FromSeconds(0.1)); // one minute

            Assert.That(pet.Cleanliness, Is.EqualTo(100d - care.CleanlinessDecayPerHour / 60d).Within(1e-6));
        }

        [Test]
        public void Resume_AppliesTheBackgroundTimeOnce()
        {
            var session = NewSession();
            session.Load();
            session.Colony.Animals[0].NextShedAtUtc = realNow.AddDays(3);
            session.Save();

            realNow += TimeSpan.FromHours(2);
            session.Resume();

            Assert.That(session.Colony.Animals[0].Hunger, Is.EqualTo(80d - care.HungerDecayPerHour * 2d).Within(1e-6));
        }

        [Test]
        public void StageUpsAreReportedWithThePet()
        {
            var session = NewSession();
            session.Load();
            var pet = session.Colony.Animals[0];
            pet.WeightGrams = 15.5d;
            pet.NextShedAtUtc = realNow.AddDays(3);

            var report = session.SimulateGameTime(GameCalendar.RealTimeFor(care.PreGrowthFastGameDays) + TimeSpan.FromMinutes(2));

            Assert.That(report.StageUps, Has.Count.EqualTo(1));
            Assert.That(report.StageUps[0].Pet, Is.SameAs(pet));
            Assert.That(report.StageUps[0].Stage, Is.EqualTo(GrowthStage.Juvenile));
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: コンパイルエラー（`ColonySession` がない）

- [ ] **Step 3: 実装する**

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public sealed class ColonyTickReport
    {
        public List<(PetState Pet, GrowthStage Stage)> StageUps { get; } = new List<(PetState, GrowthStage)>();
        public List<PetState> Sheds { get; } = new List<PetState>();
        public long ElectricityCharged { get; set; }
        public TimeSpan AppliedElapsed { get; set; }

        public bool HasEvents => StageUps.Count > 0 || Sheds.Count > 0 || ElectricityCharged > 0;
    }

    /// <summary>
    /// Owns the colony while the app runs: loading/migrating, applying elapsed time to every
    /// animal (offline, resume, live with the debug multiplier), monthly electricity, and
    /// saving. Keeps each animal's applied-until time and the shared game clock on one
    /// timeline (see the simulated-clock-bookkeeping skill).
    /// </summary>
    public sealed class ColonySession
    {
        private readonly string savePath;
        private readonly CareTuning care;
        private readonly EconomyTuning economy;
        private readonly System.Random random;
        private readonly ColonySaveService saveService;
        private readonly OfflineProgressCalculator calculator;
        private TimeService clock;

        public ColonySession(string savePath, TimeService clock, CareTuning care, EconomyTuning economy, System.Random random)
        {
            this.savePath = savePath;
            this.clock = clock;
            this.care = care;
            this.economy = economy;
            this.random = random;
            saveService = new ColonySaveService(care, economy);
            calculator = new OfflineProgressCalculator(care);
        }

        public Colony Colony { get; private set; }

        public GameCalendar Calendar { get; private set; }

        /// <summary>The game clock: real time, or ahead of it while a debug multiplier runs.</summary>
        public DateTimeOffset GameNowUtc { get; private set; }

        public bool Migrated { get; private set; }

        public TimeService Clock => clock;

        public void UseClock(TimeService newClock)
        {
            clock = newClock;
        }

        public ColonyTickReport Load()
        {
            var nowUtc = clock.UtcNow();
            Colony = saveService.LoadOrCreate(savePath, nowUtc, random);
            Migrated = saveService.LastLoadMigrated;
            Calendar = new GameCalendar(Colony.CalendarEpochUtc);
            GameNowUtc = nowUtc;
            var report = ApplyUntil(nowUtc);
            Resync(nowUtc);
            saveService.Save(savePath, Colony);
            return report;
        }

        public ColonyTickReport Advance(TimeSpan realDelta)
        {
            var scaled = clock.ScaleElapsed(realDelta);
            if (scaled <= TimeSpan.Zero)
            {
                return new ColonyTickReport();
            }

            GameNowUtc += scaled;
            return ApplyUntil(GameNowUtc);
        }

        public ColonyTickReport Resume()
        {
            var nowUtc = clock.UtcNow();
            GameNowUtc = nowUtc;
            var report = ApplyUntil(nowUtc);
            Save();
            return report;
        }

        public ColonyTickReport SimulateGameTime(TimeSpan span)
        {
            GameNowUtc += span;
            return ApplyUntil(GameNowUtc);
        }

        public void Save()
        {
            Resync(clock.UtcNow());
            saveService.Save(savePath, Colony);
        }

        private ColonyTickReport ApplyUntil(DateTimeOffset targetUtc)
        {
            var report = new ColonyTickReport();
            foreach (var pet in Colony.Animals)
            {
                var stageBefore = pet.Stage;
                var result = calculator.Apply(pet, pet.LastSavedAtUtc, targetUtc);
                pet.LastSavedAtUtc += result.AppliedElapsed;
                if (result.AppliedElapsed > report.AppliedElapsed)
                {
                    report.AppliedElapsed = result.AppliedElapsed;
                }

                if (pet.Stage != stageBefore)
                {
                    report.StageUps.Add((pet, pet.Stage));
                }

                if (result.ShedCount > 0)
                {
                    report.Sheds.Add(pet);
                }
            }

            report.ElectricityCharged = BillElectricity(targetUtc);
            return report;
        }

        private long BillElectricity(DateTimeOffset atUtc)
        {
            var month = Calendar.MonthIndexAt(atUtc);
            long charged = 0;
            while (Colony.LastBilledMonthIndex < month)
            {
                Colony.LastBilledMonthIndex++;
                var bill = MaintenanceCosts.MonthlyElectricity(Colony.Cages.Count, Colony.IncubatorCount, economy);
                Colony.Wallet.Charge(bill, LedgerCategory.Electricity, "電気代", atUtc);
                charged += bill;
            }

            return charged;
        }

        /// <summary>
        /// Re-anchors every animal and the game clock on real time, keeping each animal's
        /// sub-step remainder so a care action is never replayed away.
        /// </summary>
        private void Resync(DateTimeOffset nowUtc)
        {
            var step = TimeSpan.FromMinutes(care.OfflineProgressStepMinutes);
            foreach (var pet in Colony.Animals)
            {
                var pending = GameNowUtc - pet.LastSavedAtUtc;
                if (pending < TimeSpan.Zero || pending >= step)
                {
                    pending = TimeSpan.Zero;
                }

                pet.LastSavedAtUtc = nowUtc - pending;
            }

            GameNowUtc = nowUtc;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: 失敗0

- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/ColonySession.cs Assets/Tests/EditMode/ColonySessionTests.cs
git commit -m "Run time, electricity and saving for the whole colony in ColonySession"
```

---

### Task 8: 画面を飼育セッションにつなぐ（選んでいる個体を表示する）

**Files:**
- Modify: `Assets/Scripts/UI/TerrariumView.cs`, `Assets/Tests/PlayMode/TerrariumViewTests.cs`, `Assets/UI/Terrarium.uxml`（デバッグの成長度スライダーを体重に変える）

**Interfaces:**
- Consumes: Task 7 `ColonySession`、Task 5 `ColonyCareService`・`FeedOutcome`、Task 2 `GrowthModel`
- Produces（Task 9 が使う）：
  - `TerrariumView.Session : ColonySession`
  - `TerrariumView.LoadColony(string path, DateTimeOffset nowUtc, TimeService clock = null)`（旧 `LoadStateAndApplyOfflineProgress` を置き換える）
  - `TerrariumView.SelectCage(int cageId)`（そのケージの個体を `State` にして再描画し、ヤモリの表示を作り直す）
  - `TerrariumView.CurrentCage : Cage`
  - `TerrariumView.ShowCageStep(int delta)`（前後のケージへ。端で折り返す）
  - `TerrariumView.OnFeedAllClicked()` / `OnWaterAllClicked()` / `OnCleanAllClicked()`（一括の世話。結果を `ShowFeedback` に出す）
  - `TerrariumView.OnDebugWeightChanged(double grams)`（旧 `OnDebugGrowthChanged`）
  - `event Action ColonyChanged`（お金・個体が変わったら Task 9 のホーム・一覧を描き直す）

- [ ] **Step 1: PlayMode テストを新しい API に合わせて書き換える（失敗させる）**
  - 追加する補助メソッド：

```csharp
        private string SaveColonyWith(PetState pet, DateTimeOffset nowUtc)
        {
            var path = CreateTempSavePath();
            var colony = Colony.CreateNew(nowUtc, new EconomyTuning(), new CareTuning(), new System.Random(1));
            var cage = colony.Cages[0];
            colony.Animals.Clear();
            cage.AnimalId = -1;
            colony.NextAnimalId = 1;
            colony.AddAnimal(pet, cage);
            new ColonySaveService(new CareTuning(), new EconomyTuning()).Save(path, colony);
            return path;
        }
```

  - `view.LoadStateAndApplyOfflineProgress(path, now[, clock])` をすべて `view.LoadColony(path, now[, clock])` に置き換える。
  - `new SaveService().Save(path, state)` のあとに読み込むテストは、`var path = SaveColonyWith(state, nowUtc);` に置き換える。
  - `new SaveService().LoadOrCreateDefault(path, now)` で読み戻すテストは、`new ColonySaveService(new CareTuning(), new EconomyTuning()).LoadOrCreate(path, now, new System.Random(1)).Animals[0]` に置き換える。
  - `LoadStateAndApplyOfflineProgress_WhenElapsedTimeCrossesAStageBoundary_ShowsTheMilestoneModal` は、`WeightGrams = 15.5d`・`NextShedAtUtc = nowUtc.AddDays(3)`・`LastSavedAtUtc = nowUtc - GameCalendar.RealTimeFor(care.PreGrowthFastGameDays) - TimeSpan.FromMinutes(5)` の個体を保存してから読み込み、マイルストーンが表示されることを確認する形に変える。
  - `Render_SetsGrowthStageTextAndStageRelativeGaugeWidth` は次に置き換える：

```csharp
        [Test]
        public void Render_ShowsTheJapaneseStageWithWeightAndTheProgressToTheNextStage()
        {
            var state = new PetState { Stage = GrowthStage.Baby, WeightGrams = 9d };

            view.Render(state, new CareTuning());

            Assert.That(growthStageLabel.text, Is.EqualTo("ベビー 9.0g"));
            Assert.That(growthGaugeFill.style.width.value.value, Is.EqualTo(50f).Within(0.01f));
        }
```

  - `OnDebugGrowthChanged_UpdatesStateAndRerenders` は次に置き換える（SetUp の `debugGrowthSlider` の名前を `debug-weight-slider`、範囲を 0〜80 に変える）：

```csharp
        [Test]
        public void OnDebugWeightChanged_UpdatesStateAndRerenders()
        {
            view.OnDebugWeightChanged(9d);

            Assert.That(view.State.WeightGrams, Is.EqualTo(9d));
            Assert.That(growthStageLabel.text, Is.EqualTo("ベビー 9.0g"));
        }
```

  - 一括の世話のテストを追加する：

```csharp
        [Test]
        public void OnFeedAllClicked_FeedsEveryPetAndSaysHowMany()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var path = SaveColonyWith(new PetState { Hunger = 20d, LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) }, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));
            view.Session.Colony.AddAnimal(new PetState { Hunger = 20d, LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) },
                view.Session.Colony.AddCage(CageSize.Standard));

            view.OnFeedAllClicked();

            Assert.That(feedbackLabel.text, Is.EqualTo("2匹が食べました"));
            Assert.That(view.Session.Colony.Wallet.Money, Is.EqualTo(50000 - 60));
        }

        [Test]
        public void ShowCageStep_WrapsAroundAndSelectsThatCagesPet()
        {
            var nowUtc = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var path = SaveColonyWith(new PetState { LastSavedAtUtc = nowUtc, NextShedAtUtc = nowUtc.AddDays(3) }, nowUtc);
            view.LoadColony(path, nowUtc, new TimeService(() => nowUtc));
            var second = view.Session.Colony.AddAnimal(new PetState { Name = "ふたり目" }, view.Session.Colony.AddCage(CageSize.Standard));

            view.ShowCageStep(1);
            Assert.That(view.State, Is.SameAs(second));

            view.ShowCageStep(1);
            Assert.That(view.State, Is.SameAs(view.Session.Colony.Animals[0]));
        }
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `scripts/run-unity-tests.sh PlayMode; scripts/test-summary.py`
Expected: コンパイルエラー（`LoadColony` などがない）

- [ ] **Step 3: `TerrariumView` を変更する（保存と時間経過をセッションに移す）**
  - 削除するフィールド：`saveService`・`offlineProgressCalculator`・`savePath`・`virtualNow`。削除するメソッド：`LoadStateAndApplyOfflineProgress`・`ResyncClock`・`ApplyCalculatorResult`・`ApplyLiveTickDelta` の本体。
  - 追加するフィールド：

```csharp
        private readonly EconomyTuning economyTuning = new EconomyTuning();
        private ColonySession session;
        private ColonyCareService colonyCare;
        private Cage currentCage;

        public ColonySession Session => session;
        public Cage CurrentCage => currentCage;
        public event Action ColonyChanged;
```

  - 追加するメソッド：

```csharp
        /// <summary>
        /// Loads (or creates/migrates) the colony, applies the time away to every animal and
        /// shows the first occupied cage. Public so tests can drive it without a UIDocument.
        /// </summary>
        public void LoadColony(string atSavePath, DateTimeOffset nowUtc, TimeService clock = null)
        {
            tuning = new CareTuning();
            timeService = clock ?? new TimeService(() => nowUtc);
            session = new ColonySession(atSavePath, timeService, tuning, economyTuning, new System.Random());
            colonyCare = new ColonyCareService(tuning, economyTuning);
            careService = new CareService(tuning);
            var report = session.Load();
            var first = session.Colony.OccupiedCages();
            SelectCage(first.Count > 0 ? first[0].Id : session.Colony.Cages[0].Id);
            HandleReport(report);
            if (session.Migrated)
            {
                ShowFeedback("データを新しい形式に移しました（ケージ1）");
            }
        }

        public void SelectCage(int cageId)
        {
            currentCage = session.Colony.Cages.Find(c => c.Id == cageId);
            var pet = session.Colony.AnimalIn(currentCage);
            if (pet == null)
            {
                return;
            }

            Initialize(pet, tuning);
            RebuildPetActor();
            ColonyChanged?.Invoke();
        }

        public void ShowCageStep(int delta)
        {
            var occupied = session.Colony.OccupiedCages();
            if (occupied.Count == 0)
            {
                return;
            }

            var index = Math.Max(0, occupied.IndexOf(currentCage));
            var next = ((index + delta) % occupied.Count + occupied.Count) % occupied.Count;
            SelectCage(occupied[next].Id);
        }

        private void HandleReport(ColonyTickReport report)
        {
            if (report.AppliedElapsed > TimeSpan.Zero)
            {
                lastAppliedElapsed = report.AppliedElapsed;
                UpdateDebugAppliedElapsedLabel();
            }

            foreach (var (pet, stage) in report.StageUps)
            {
                DecorUnlockService.GrantUnlocksForStage(pet, stage);
                if (pet == state)
                {
                    ShowMilestoneModal(stage);
                }
            }

            if (report.Sheds.Contains(state))
            {
                ShowFeedback(ShedMessage);
            }

            if (state != null)
            {
                Render(state, tuning);
            }

            if (report.HasEvents)
            {
                ColonyChanged?.Invoke();
            }
        }

        public void OnFeedAllClicked() => OnBulkCare(() => colonyCare.FeedAll(session.Colony, GameNowUtc()).ToMessage());

        public void OnWaterAllClicked() => OnBulkCare(() => $"{colonyCare.RefreshWaterAll(session.Colony)}匹の水を替えました");

        public void OnCleanAllClicked() => OnBulkCare(() => $"{colonyCare.CleanAll(session.Colony)}ケージを掃除しました");

        private void OnBulkCare(Func<string> action)
        {
            if (session == null)
            {
                return;
            }

            ShowFeedback(action());
            Render(state, tuning);
            SaveCurrentState();
            ColonyChanged?.Invoke();
        }
```

  - `RebuildPetActor()` は、`BindElements` のうち `petActor = new PetActor(...)` を作る部分を取り出した private メソッドにする（既存の `petActor?.Effects.Clear()` → 新しい `PetActor` → `SetViewSize`（`terrariumProjection` があれば）→ `PlaceDecor()`）。選ぶケージが変わるたびに、ヤモリの状態（歩行位置など）を作り直す。
  - `GameNowUtc()` は `session != null ? session.GameNowUtc : (timeService?.UtcNow() ?? DateTimeOffset.UtcNow)` を返す。
  - `SaveCurrentState()` の本体を `session?.Save();` に置き換える。
  - `OnApplicationPause(false)` の本体を `if (session != null) HandleReport(session.Resume());` に置き換える。
  - `LiveTickLoop` の中を `HandleReport(session.Advance(TimeSpan.FromSeconds(Mathf.Min(Time.unscaledDeltaTime, MaxLiveTickFrameSeconds))));` に置き換える（`session` が null のときは何もしない）。
  - `ApplyLiveTickDelta(TimeSpan)` は `if (session != null) HandleReport(session.Advance(realDelta));` にする（既存テストの呼び出しを残すため）。
  - `OnDebugSimulate12HoursClicked` の本体を `HandleReport(session.SimulateGameTime(TimeSpan.FromHours(12))); SaveCurrentState();` にする。
  - `OnDebugClearSaveClicked` は、保存ファイルを消してから `LoadColony(path, timeService.UtcNow(), timeService)` を呼ぶ（`path` は `session` に持たせた保存先。`ColonySession` に `public string SavePath => savePath;` を追加する）。
  - `OnFeedClicked` の中の `careService.Feed(state, GameNowUtc())` を `colonyCare.Feed(session.Colony, state, GameNowUtc())` に置き換え、`FeedOutcome.NotEnoughMoney` なら `ShowFeedback("お金が足りなくて餌を買えません")` を出して終わる。`RefusedPreShed`・`RefusedPreGrowth` は既存の拒食の表示を使う。`session` が null のとき（EditMode 相当のテスト）は既存どおり `careService.Feed(state, GameNowUtc())` を使う。
  - `Awake` の `LoadStateAndApplyOfflineProgress(resolvedSavePath, new TimeService().UtcNow());` を `LoadColony(resolvedSavePath, DateTimeOffset.UtcNow, new TimeService());` に置き換える。

- [ ] **Step 4: 成長段階の表示と、デバッグのスライダーを体重に変える**
  - `Render` の成長段階とゲージの2行を次に置き換える：

```csharp
            growthStageLabel.text = $"{GrowthModel.StageLabel(petState.Stage)} {petState.WeightGrams:0.0}g";
            growthGaugeFill.style.width = new Length((float)(GrowthModel.ProgressToNextStage(petState, GameNowUtc(), careTuning) * 100d), LengthUnit.Percent);
```

  - `debugGrowthSlider` を `debugWeightSlider`（名前 `debug-weight-slider`）に、`OnDebugGrowthChanged` を `OnDebugWeightChanged(double grams)`（`state.WeightGrams = grams;`）に、`RefreshDebugPanel` の値の設定を `(float)state.WeightGrams` に変える。
  - `Terrarium.uxml` のデバッグパネルの成長度の行を次に変える：

```xml
                <ui:VisualElement class="debug-slider-row">
                    <ui:Label text="体重(g)" class="debug-slider-label" />
                    <ui:Slider name="debug-weight-slider" low-value="0" high-value="80" value="3" class="debug-slider" />
                </ui:VisualElement>
```

  - `ShowMilestoneModal` の `milestoneStageLabel.text = stage.ToString();` を `GrowthModel.StageLabel(stage)` に変える。

- [ ] **Step 5: テストが通ることを確認する**

Run: `scripts/run-unity-tests.sh; scripts/test-summary.py`
Expected: EditMode・PlayMode とも失敗0

- [ ] **Step 6: コミット**

```bash
git add -A Assets/Scripts/UI/TerrariumView.cs Assets/Scripts/Core/ColonySession.cs Assets/Tests/PlayMode Assets/UI/Terrarium.uxml
git commit -m "Drive the cage screen from the colony session; weight-based stage display"
```

---

### Task 9: ヘッダー・ホーム画面・タブ・ケージ一覧

**Files:**
- Create: `Assets/Scripts/UI/ShellNavigator.cs`, `Assets/Scripts/UI/HomeView.cs`, `Assets/Scripts/UI/CageListView.cs`, `Assets/Scripts/UI/CageStatusText.cs`, `Assets/Tests/EditMode/ShellTests.cs`
- Modify: `Assets/UI/Terrarium.uxml`, `Assets/UI/Terrarium.uss`, `Assets/Scripts/UI/TerrariumView.cs`

**Interfaces:**
- Consumes: Task 8 `TerrariumView.SelectCage`・`ShowCageStep`・`OnFeedAllClicked` など・`ColonyChanged`・`Session`、既存 `PetSpriteLibrary`
- Produces:
  - `enum ShellScreen { Home, Main }`、`enum ShellTab { Cages, Incubator, Shop, Events, Ledger }`
  - `ShellNavigator(VisualElement root)`：`ShellScreen Screen`、`ShellTab Tab`、`bool ShowingCageDetail`、`void ShowHome()`、`void ShowCageDetail()`、`void ShowCageList()`、`void ShowTab(ShellTab)`、`static string PlaceholderTextFor(ShellTab)`
  - `CageStatusText.AlertsFor(PetState, DateTimeOffset, CareTuning) : string`（例：「空腹・脱皮前」、なければ空文字）、`CageStatusText.TitleFor(Cage, PetState) : string`（例：「ケージ1 レオパ1（ベビー・性別不明）」）
  - `HomeView(VisualElement rackList, PetSpriteLibrary)`：`void Render(Colony, DateTimeOffset, CareTuning)`、`event Action<int> CageSelected`
  - `CageListView(VisualElement list, PetSpriteLibrary)`：`void Render(Colony, DateTimeOffset, CareTuning)`、`event Action<int> CageSelected`

- [ ] **Step 1: 失敗するテストを書く**（`Assets/Tests/EditMode/ShellTests.cs`）

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;
using UnityEngine.UIElements;

namespace TerrariumDays.Tests
{
    public sealed class ShellTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        private static VisualElement BuildShell()
        {
            var root = new VisualElement();
            foreach (var name in new[] { "home-screen", "main-screen", "cage-list-panel", "cage-detail-panel", "placeholder-panel" })
            {
                root.Add(new VisualElement { name = name });
            }

            root.Add(new Label { name = "placeholder-label" });
            foreach (var name in new[] { "tab-cages", "tab-incubator", "tab-shop", "tab-events", "tab-ledger" })
            {
                root.Add(new Button { name = name });
            }

            return root;
        }

        private static DisplayStyle Display(VisualElement root, string name) => root.Q(name).style.display.value;

        [Test]
        public void TheAppStartsOnTheHomeScreen()
        {
            var root = BuildShell();
            var nav = new ShellNavigator(root);

            Assert.That(nav.Screen, Is.EqualTo(ShellScreen.Home));
            Assert.That(Display(root, "home-screen"), Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Display(root, "main-screen"), Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ShowCageDetail_EntersTheMainScreenOnTheCagesTab()
        {
            var root = BuildShell();
            var nav = new ShellNavigator(root);

            nav.ShowCageDetail();

            Assert.That((nav.Screen, nav.Tab, nav.ShowingCageDetail), Is.EqualTo((ShellScreen.Main, ShellTab.Cages, true)));
            Assert.That(Display(root, "cage-detail-panel"), Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Display(root, "cage-list-panel"), Is.EqualTo(DisplayStyle.None));
            Assert.That(Display(root, "home-screen"), Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TabsNotBuiltYet_ShowWhenTheyArrive()
        {
            var root = BuildShell();
            var nav = new ShellNavigator(root);

            nav.ShowTab(ShellTab.Shop);

            Assert.That(Display(root, "placeholder-panel"), Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Label>("placeholder-label").text, Is.EqualTo("ショップは段階3で追加されます"));
            Assert.That(root.Q<Button>("tab-shop").ClassListContains("tab-selected"), Is.True);
        }

        [Test]
        public void CageTitle_HidesTheSexOfABaby()
        {
            var cage = new Cage { Id = 2 };

            Assert.That(CageStatusText.TitleFor(cage, new PetState { Name = "ハナ" }), Is.EqualTo("ケージ2 ハナ（ベビー・性別不明）"));
            Assert.That(CageStatusText.TitleFor(cage, new PetState { Name = "ハナ", Stage = GrowthStage.Juvenile, Sex = Sex.Male }), Is.EqualTo("ケージ2 ハナ（ヤング・♂）"));
            Assert.That(CageStatusText.TitleFor(cage, null), Is.EqualTo("ケージ2（空き）"));
        }

        [Test]
        public void Alerts_ListWhatNeedsAttention()
        {
            var care = new CareTuning();
            var pet = new PetState { Hunger = 30d, Hydration = 80d, Cleanliness = 30d, NextShedAtUtc = Now.AddMinutes(30) };

            Assert.That(CageStatusText.AlertsFor(pet, Now, care), Is.EqualTo("空腹・汚れ・脱皮前"));
            Assert.That(CageStatusText.AlertsFor(new PetState { NextShedAtUtc = Now.AddDays(3) }, Now, care), Is.EqualTo(string.Empty));
        }

        [Test]
        public void HomeView_ShowsFourSlotsPerRackAndReportsTheTappedCage()
        {
            var colony = Colony.CreateNew(Now, new EconomyTuning(), new CareTuning(), new System.Random(1));
            var rackList = new VisualElement();
            var home = new HomeView(rackList, null);
            var selected = -1;
            home.CageSelected += id => selected = id;

            home.Render(colony, Now, new CareTuning());

            var slots = rackList.Query<Button>(className: "rack-slot").ToList();
            Assert.That(slots, Has.Count.EqualTo(4));
            Assert.That(slots[0].enabledSelf, Is.True);
            Assert.That(slots[1].enabledSelf, Is.False, "no cage bought for this slot yet");
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = slots[0];
                slots[0].SendEvent(click);
            }

            Assert.That(selected, Is.EqualTo(colony.Cages[0].Id));
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `scripts/run-unity-tests.sh EditMode; scripts/test-summary.py`
Expected: コンパイルエラー（`ShellNavigator` などがない）

- [ ] **Step 3: `ShellNavigator.cs` を作る**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    public enum ShellScreen
    {
        Home,
        Main
    }

    public enum ShellTab
    {
        Cages,
        Incubator,
        Shop,
        Events,
        Ledger
    }

    /// <summary>
    /// Swaps full-screen views (home ↔ main) and the tab panels. Screens are overlays, so
    /// they use display; nothing in-flow is toggled here.
    /// </summary>
    public sealed class ShellNavigator
    {
        private readonly VisualElement home;
        private readonly VisualElement main;
        private readonly VisualElement cageList;
        private readonly VisualElement cageDetail;
        private readonly VisualElement placeholder;
        private readonly Label placeholderLabel;
        private readonly Dictionary<ShellTab, Button> tabButtons = new Dictionary<ShellTab, Button>();

        public ShellNavigator(VisualElement root)
        {
            home = root.Q("home-screen");
            main = root.Q("main-screen");
            cageList = root.Q("cage-list-panel");
            cageDetail = root.Q("cage-detail-panel");
            placeholder = root.Q("placeholder-panel");
            placeholderLabel = root.Q<Label>("placeholder-label");
            Bind(root, "tab-cages", ShellTab.Cages);
            Bind(root, "tab-incubator", ShellTab.Incubator);
            Bind(root, "tab-shop", ShellTab.Shop);
            Bind(root, "tab-events", ShellTab.Events);
            Bind(root, "tab-ledger", ShellTab.Ledger);
            ShowHome();
        }

        public ShellScreen Screen { get; private set; }

        public ShellTab Tab { get; private set; }

        public bool ShowingCageDetail { get; private set; }

        public event Action<ShellTab> TabChanged;

        public static string PlaceholderTextFor(ShellTab tab)
        {
            switch (tab)
            {
                case ShellTab.Incubator:
                    return "孵卵器は段階5で追加されます";
                case ShellTab.Shop:
                    return "ショップは段階3で追加されます";
                case ShellTab.Events:
                    return "イベントは段階6で追加されます";
                case ShellTab.Ledger:
                    return "台帳は段階3で追加されます";
                default:
                    return string.Empty;
            }
        }

        public void ShowHome()
        {
            Screen = ShellScreen.Home;
            SetDisplay(home, true);
            SetDisplay(main, false);
        }

        public void ShowCageDetail()
        {
            ShowingCageDetail = true;
            ShowTab(ShellTab.Cages);
        }

        public void ShowCageList()
        {
            ShowingCageDetail = false;
            ShowTab(ShellTab.Cages);
        }

        public void ShowTab(ShellTab tab)
        {
            Screen = ShellScreen.Main;
            Tab = tab;
            SetDisplay(home, false);
            SetDisplay(main, true);
            SetDisplay(cageDetail, tab == ShellTab.Cages && ShowingCageDetail);
            SetDisplay(cageList, tab == ShellTab.Cages && !ShowingCageDetail);
            SetDisplay(placeholder, tab != ShellTab.Cages);
            if (placeholderLabel != null)
            {
                placeholderLabel.text = PlaceholderTextFor(tab);
            }

            foreach (var pair in tabButtons)
            {
                pair.Value.EnableInClassList("tab-selected", pair.Key == tab);
            }

            TabChanged?.Invoke(tab);
        }

        private void Bind(VisualElement root, string name, ShellTab tab)
        {
            var button = root.Q<Button>(name);
            if (button == null)
            {
                return;
            }

            tabButtons[tab] = button;
            button.clicked += () =>
            {
                if (tab == ShellTab.Cages)
                {
                    ShowCageList();
                }
                else
                {
                    ShowTab(tab);
                }
            };
        }

        private static void SetDisplay(VisualElement element, bool visible)
        {
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
```

- [ ] **Step 4: `CageStatusText.cs` を作る**

```csharp
using System;
using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>Short texts shown on home slots and in the cage list.</summary>
    public static class CageStatusText
    {
        public static string TitleFor(Cage cage, PetState pet)
        {
            if (pet == null)
            {
                return $"ケージ{cage.Id}（空き）";
            }

            var sex = !pet.SexKnown ? "性別不明" : pet.Sex == Sex.Female ? "♀" : "♂";
            return $"ケージ{cage.Id} {pet.Name}（{GrowthModel.StageLabel(pet.Stage)}・{sex}）";
        }

        public static string AlertsFor(PetState pet, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var alerts = new List<string>();
            if (pet.Hunger < tuning.HealthyCareThreshold)
            {
                alerts.Add("空腹");
            }

            if (pet.Hydration < tuning.HealthyCareThreshold)
            {
                alerts.Add("水");
            }

            if (pet.Cleanliness < tuning.HealthyCareThreshold)
            {
                alerts.Add("汚れ");
            }

            switch (AppetiteModel.Evaluate(pet, nowUtc, tuning))
            {
                case AppetiteState.PreShed:
                    alerts.Add("脱皮前");
                    break;
                case AppetiteState.PreGrowth:
                    alerts.Add("成長前");
                    break;
            }

            return string.Join("・", alerts);
        }
    }
}
```

- [ ] **Step 5: `HomeView.cs` と `CageListView.cs` を作る**

```csharp
using System;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>The breeding room: racks of four slots, each a bought cage or an empty shelf.</summary>
    public sealed class HomeView
    {
        private readonly VisualElement rackList;
        private readonly PetSpriteLibrary sprites;

        public HomeView(VisualElement rackList, PetSpriteLibrary sprites)
        {
            this.rackList = rackList;
            this.sprites = sprites;
        }

        public event Action<int> CageSelected;

        public void Render(Colony colony, DateTimeOffset nowUtc, CareTuning tuning)
        {
            rackList.Clear();
            for (var rack = 0; rack < colony.RackCount; rack++)
            {
                var row = new VisualElement();
                row.AddToClassList("rack-row");
                for (var slot = 0; slot < Colony.CagesPerRack; slot++)
                {
                    var index = rack * Colony.CagesPerRack + slot;
                    row.Add(index < colony.Cages.Count
                        ? CageSlot(colony, colony.Cages[index], nowUtc, tuning)
                        : EmptyShelf());
                }

                rackList.Add(row);
            }
        }

        private Button CageSlot(Colony colony, Cage cage, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var pet = colony.AnimalIn(cage);
            var slot = new Button(() => CageSelected?.Invoke(cage.Id));
            slot.AddToClassList("rack-slot");
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList("rack-thumb");
            var frame = pet != null ? sprites?.Frame(PetClip.Idle, 0) : null;
            if (frame != null)
            {
                thumb.style.backgroundImage = new StyleBackground(frame);
            }

            slot.Add(thumb);
            slot.Add(Label(pet != null ? pet.Name : "空きケージ", "rack-name"));
            slot.Add(Label(pet != null ? CageStatusText.AlertsFor(pet, nowUtc, tuning) : string.Empty, "rack-alert"));
            return slot;
        }

        private static Button EmptyShelf()
        {
            var slot = new Button();
            slot.AddToClassList("rack-slot");
            slot.AddToClassList("rack-slot-empty");
            slot.Add(Label("空き棚", "rack-name"));
            slot.SetEnabled(false);
            return slot;
        }

        private static Label Label(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }

    /// <summary>The Cages tab list: one row per cage, tap to open it.</summary>
    public sealed class CageListView
    {
        private readonly VisualElement list;
        private readonly PetSpriteLibrary sprites;

        public CageListView(VisualElement list, PetSpriteLibrary sprites)
        {
            this.list = list;
            this.sprites = sprites;
        }

        public event Action<int> CageSelected;

        public void Render(Colony colony, DateTimeOffset nowUtc, CareTuning tuning)
        {
            list.Clear();
            foreach (var cage in colony.Cages)
            {
                var pet = colony.AnimalIn(cage);
                var row = new Button(() => CageSelected?.Invoke(cage.Id));
                row.AddToClassList("cage-row");
                row.SetEnabled(pet != null);
                var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
                thumb.AddToClassList("cage-row-thumb");
                var frame = pet != null ? sprites?.Frame(PetClip.Idle, 0) : null;
                if (frame != null)
                {
                    thumb.style.backgroundImage = new StyleBackground(frame);
                }

                row.Add(thumb);
                var texts = new VisualElement { pickingMode = PickingMode.Ignore };
                texts.AddToClassList("cage-row-texts");
                texts.Add(new Label(CageStatusText.TitleFor(cage, pet)) { pickingMode = PickingMode.Ignore });
                var detail = pet != null
                    ? $"{pet.WeightGrams:0.0}g　{CageStatusText.AlertsFor(pet, nowUtc, tuning)}"
                    : string.Empty;
                var detailLabel = new Label(detail) { pickingMode = PickingMode.Ignore };
                detailLabel.AddToClassList("cage-row-detail");
                texts.Add(detailLabel);
                row.Add(texts);
                list.Add(row);
            }
        }
    }
}
```

（2つのクラスを1ファイルにまとめたので、ファイル名は `HomeView.cs` とし、`CageListView.cs` は作らない。Files の一覧の `CageListView.cs` はこの1ファイルに含まれる。）

- [ ] **Step 6: UXML を画面構成に合わせて組み替える**

`Terrarium.uxml` の `root` の中身を、次の順に並べ替える（既存の要素は名前を変えずに移すだけ）。

```xml
    <ui:VisualElement name="root" class="root">
        <ui:VisualElement name="app-header" class="app-header">
            <ui:Button name="home-button" text="ホーム" class="header-button" />
            <ui:Label name="game-date-label" text="2026年4月1日" class="header-date" />
            <ui:Label name="money-label" text="所持金 ¥50,000" class="header-money" />
        </ui:VisualElement>
        <!-- 既存の environment-bar をここへ移す -->
        <ui:VisualElement name="home-screen" class="screen">
            <ui:Label text="飼育部屋" class="screen-title" />
            <ui:ScrollView name="rack-list" class="rack-list" />
            <ui:Button name="home-incubator-button" text="孵卵器（段階5で使えるようになります）" class="home-incubator" />
        </ui:VisualElement>
        <ui:VisualElement name="main-screen" class="screen">
            <ui:VisualElement name="tab-content" class="tab-content">
                <ui:VisualElement name="cage-list-panel" class="panel">
                    <ui:VisualElement class="bulk-bar">
                        <ui:Button name="feed-all-button" text="全員に餌" class="bulk-button" />
                        <ui:Button name="water-all-button" text="全ケージ水替え" class="bulk-button" />
                        <ui:Button name="clean-all-button" text="全ケージ掃除" class="bulk-button" />
                    </ui:VisualElement>
                    <ui:ScrollView name="cage-list" class="cage-list" />
                </ui:VisualElement>
                <ui:VisualElement name="cage-detail-panel" class="panel">
                    <!-- 既存の top-bar（中に prev-cage-button / next-cage-button / cage-title-label を追加）、
                         terrarium-view、status-section、feedback-label、bottom-bar をここへ移す -->
                </ui:VisualElement>
                <ui:VisualElement name="placeholder-panel" class="panel">
                    <ui:Label name="placeholder-label" text="" class="placeholder-label" />
                </ui:VisualElement>
            </ui:VisualElement>
            <ui:VisualElement name="tab-bar" class="tab-bar">
                <ui:Button name="tab-cages" text="ケージ" class="tab-button" />
                <ui:Button name="tab-incubator" text="孵卵器" class="tab-button" />
                <ui:Button name="tab-shop" text="ショップ" class="tab-button" />
                <ui:Button name="tab-events" text="イベント" class="tab-button" />
                <ui:Button name="tab-ledger" text="台帳" class="tab-button" />
            </ui:VisualElement>
        </ui:VisualElement>
        <!-- 既存の milestone-modal / decor-drawer / debug-panel はここ（root 直下）に残す -->
    </ui:VisualElement>
```

`top-bar` の中身は次にする（`debug-button` は残す）：

```xml
        <ui:VisualElement name="top-bar" class="top-bar">
            <ui:VisualElement class="cage-nav">
                <ui:Button name="prev-cage-button" text="◀" class="cage-nav-button" />
                <ui:Label name="cage-title-label" text="ケージ1" class="cage-title-label" />
                <ui:Button name="next-cage-button" text="▶" class="cage-nav-button" />
            </ui:VisualElement>
            <ui:Label name="growth-stage-label" text="ベビー 3.0g" class="growth-stage-label" />
            <ui:VisualElement name="growth-gauge-track" class="gauge-track">
                <ui:VisualElement name="growth-gauge-fill" class="gauge-fill" />
            </ui:VisualElement>
            <ui:Button name="debug-button" text="デバッグ" class="debug-button" />
        </ui:VisualElement>
```

- [ ] **Step 7: USS を追加する**（`Terrarium.uss` の末尾）

```css
/* App shell: header, screens and the tab bar. Screens fill the space between header and
   the bottom; heights are fixed so nothing shifts as text changes. */
.app-header {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    height: 40px;
    flex-shrink: 0;
    padding: 0 12px;
}

.header-button {
    height: 32px;
    min-width: 64px;
    border-radius: 8px;
    border-width: 0;
    background-color: rgba(90, 62, 40, 0.15);
    color: rgb(90, 62, 40);
    font-size: 14px;
}

.header-date,
.header-money {
    font-size: 14px;
    color: rgb(90, 62, 40);
    -unity-font-style: bold;
}

.screen {
    flex-grow: 1;
    flex-direction: column;
}

.screen-title {
    font-size: 22px;
    -unity-font-style: bold;
    color: rgb(90, 62, 40);
    margin: 8px 16px;
}

.rack-list {
    flex-grow: 1;
    padding: 0 12px;
}

.rack-row {
    flex-direction: row;
    justify-content: space-between;
    margin-bottom: 12px;
    padding: 8px 4px;
    border-bottom-width: 6px;
    border-bottom-color: rgb(140, 100, 70);
}

.rack-slot {
    width: 23%;
    height: 110px;
    margin: 0;
    padding: 4px;
    border-radius: 8px;
    border-width: 2px;
    border-color: rgb(140, 100, 70);
    background-color: rgb(245, 225, 190);
    flex-direction: column;
    align-items: center;
}

.rack-slot-empty {
    background-color: rgba(90, 62, 40, 0.06);
    border-color: rgba(90, 62, 40, 0.2);
}

.rack-thumb {
    width: 100%;
    height: 56px;
    -unity-background-scale-mode: scale-to-fit;
}

.rack-name {
    font-size: 12px;
    color: rgb(90, 62, 40);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.rack-alert {
    height: 16px;
    font-size: 10px;
    color: rgb(229, 57, 53);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.home-incubator {
    height: 48px;
    margin: 8px 16px 12px;
    border-radius: 12px;
}

.tab-content {
    flex-grow: 1;
}

.panel {
    flex-grow: 1;
    flex-direction: column;
}

.bulk-bar {
    flex-direction: row;
    justify-content: space-between;
    padding: 8px 12px;
}

.bulk-button {
    width: 32%;
    height: 44px;
    margin: 0;
    border-radius: 10px;
    border-width: 0;
    background-color: rgb(255, 167, 38);
    color: rgb(255, 255, 255);
    font-size: 13px;
    -unity-font-style: bold;
}

.cage-list {
    flex-grow: 1;
    padding: 0 12px;
}

.cage-row {
    flex-direction: row;
    align-items: center;
    height: 64px;
    margin: 0 0 8px 0;
    border-radius: 10px;
    border-width: 0;
    background-color: rgba(90, 62, 40, 0.08);
}

.cage-row-thumb {
    width: 72px;
    height: 56px;
    -unity-background-scale-mode: scale-to-fit;
}

.cage-row-texts {
    flex-grow: 1;
    -unity-text-align: middle-left;
    color: rgb(90, 62, 40);
}

.cage-row-detail {
    font-size: 12px;
    color: rgb(120, 90, 60);
}

.placeholder-label {
    margin: 40px 16px;
    font-size: 16px;
    color: rgb(90, 62, 40);
    -unity-text-align: middle-center;
    white-space: normal;
}

.tab-bar {
    flex-direction: row;
    height: 56px;
    flex-shrink: 0;
    border-top-width: 1px;
    border-top-color: rgba(90, 62, 40, 0.2);
}

.tab-button {
    flex-grow: 1;
    flex-basis: 0;
    margin: 0;
    border-width: 0;
    border-radius: 0;
    background-color: rgba(0, 0, 0, 0);
    color: rgb(140, 110, 80);
    font-size: 12px;
}

.tab-selected {
    color: rgb(255, 140, 0);
    -unity-font-style: bold;
}

.cage-nav {
    flex-direction: row;
    align-items: center;
    height: 36px;
}

.cage-nav-button {
    width: 44px;
    height: 36px;
    border-radius: 8px;
    border-width: 0;
    background-color: rgba(90, 62, 40, 0.12);
    color: rgb(90, 62, 40);
}

.cage-title-label {
    flex-grow: 1;
    font-size: 14px;
    color: rgb(90, 62, 40);
    -unity-text-align: middle-center;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
```

- [ ] **Step 8: `TerrariumView` にシェルをつなぐ**
  - フィールド：`private ShellNavigator navigator; private HomeView homeView; private CageListView cageListView; private Label moneyLabel; private Label gameDateLabel; private Label cageTitleLabel;`
  - `Awake` で、`BindElements` のあとに次を行う：

```csharp
            var root = document.rootVisualElement;
            navigator = new ShellNavigator(root);
            var sprites = PetSpriteLibrary.LoadFromResources();
            homeView = new HomeView(root.Q("rack-list"), sprites);
            cageListView = new CageListView(root.Q("cage-list"), sprites);
            moneyLabel = root.Q<Label>("money-label");
            gameDateLabel = root.Q<Label>("game-date-label");
            cageTitleLabel = root.Q<Label>("cage-title-label");
            homeView.CageSelected += id => { SelectCage(id); navigator.ShowCageDetail(); };
            cageListView.CageSelected += id => { SelectCage(id); navigator.ShowCageDetail(); };
            root.Q<Button>("home-button").clicked += navigator.ShowHome;
            root.Q<Button>("prev-cage-button").clicked += () => ShowCageStep(-1);
            root.Q<Button>("next-cage-button").clicked += () => ShowCageStep(1);
            root.Q<Button>("feed-all-button").clicked += OnFeedAllClicked;
            root.Q<Button>("water-all-button").clicked += OnWaterAllClicked;
            root.Q<Button>("clean-all-button").clicked += OnCleanAllClicked;
            ColonyChanged += RefreshShell;
            navigator.TabChanged += _ => RefreshShell();
```

  - `RefreshShell()` を追加する：

```csharp
        private void RefreshShell()
        {
            if (session == null)
            {
                return;
            }

            var now = GameNowUtc();
            if (moneyLabel != null)
            {
                moneyLabel.text = $"所持金 ¥{session.Colony.Wallet.Money:N0}";
            }

            if (gameDateLabel != null)
            {
                gameDateLabel.text = session.Calendar.DateAt(now).ToDisplayText();
            }

            if (cageTitleLabel != null && currentCage != null)
            {
                cageTitleLabel.text = CageStatusText.TitleFor(currentCage, state);
            }

            homeView?.Render(session.Colony, now, tuning);
            cageListView?.Render(session.Colony, now, tuning);
        }
```

  - `UpdateClock` の最後で `RefreshShell()` を呼ぶ（1秒ごとに、日付・所持金・注意マークが更新される）。
  - スワイプ：`terrariumViewElement` に `PointerDownEvent` と `PointerUpEvent` を登録し、横方向の移動が60px以上なら `ShowCageStep(dx < 0 ? 1 : -1)` を呼ぶ（タップの `ClickEvent` とは別に動く）。

- [ ] **Step 9: テストが通ることを確認する**

Run: `scripts/run-unity-tests.sh; scripts/test-summary.py`
Expected: EditMode・PlayMode とも失敗0

- [ ] **Step 10: コミット**

```bash
git add -A Assets/Scripts/UI Assets/UI Assets/Tests/EditMode/ShellTests.cs
git commit -m "Add the header, home rack screen, tab bar and cage list with bulk care"
```

---

### Task 10: 確認用のデバッグ機能・文書・画面キャプチャ・実機確認

**Files:**
- Modify: `Assets/UI/Terrarium.uxml`（デバッグパネルに2つのボタン）、`Assets/Scripts/UI/TerrariumView.cs`、`Assets/Tests/PlayMode/ScreenCaptureTests.cs`、`GAME.md`、`CLAUDE.md`、`AGENTS.md`

**Interfaces:**
- Consumes: Task 5 `Colony.AddCage`・`AddAnimal`、Task 8・9 の画面

- [ ] **Step 1: デバッグパネルに「ケージを追加」「個体を追加」を足す**（段階3のショップができるまでの確認用。`Debug.isDebugBuild` のときだけ表示されるパネルの中なので、製品版には出ない）

`Terrarium.uxml` のデバッグパネル（`debug-clear-save-button` の直前）に：

```xml
                <ui:VisualElement class="debug-multiplier-row">
                    <ui:Button name="debug-add-cage-button" text="ケージ追加" class="debug-small-button" />
                    <ui:Button name="debug-add-pet-button" text="個体追加" class="debug-small-button" />
                </ui:VisualElement>
```

`TerrariumView` に：

```csharp
        public void OnDebugAddCageClicked()
        {
            if (session?.Colony.AddCage(CageSize.Standard) == null)
            {
                ShowFeedback("ラックがいっぱいです");
                return;
            }

            SaveCurrentState();
            ColonyChanged?.Invoke();
        }

        public void OnDebugAddPetClicked()
        {
            var empty = session?.Colony.Cages.Find(c => c.IsEmpty);
            if (empty == null)
            {
                ShowFeedback("空きケージがありません");
                return;
            }

            var now = GameNowUtc();
            session.Colony.AddAnimal(new PetState
            {
                Name = $"レオパ{session.Colony.NextAnimalId}",
                Sex = UnityEngine.Random.value < 0.5f ? Sex.Female : Sex.Male,
                HatchedAtUtc = now,
                LastSavedAtUtc = now,
                LastShedAtUtc = now,
                NextShedAtUtc = now + SheddingModel.IntervalFor(GrowthStage.Baby, tuning),
            }, empty);
            SaveCurrentState();
            ColonyChanged?.Invoke();
        }
```

`Awake` でそれぞれのボタンの `clicked` に登録する。

- [ ] **Step 2: 画面キャプチャを新しい画面構成に合わせる**

`ScreenCaptureTests.CaptureTerrariumScreens` の最初のキャプチャの前に次を足し、ホーム・ケージ一覧・ケージ詳細の3枚を撮る（既存の4枚はケージ詳細を開いた状態で撮る）：

```csharp
            yield return Capture(outputDir, "00-home");
            var navigatorField = typeof(TerrariumView).GetField("navigator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var navigator = (ShellNavigator)navigatorField.GetValue(view);
            navigator.ShowCageList();
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "00-cage-list");
            navigator.ShowCageDetail();
            yield return new WaitForSeconds(1.2f);
```

- [ ] **Step 3: 文書を更新する**
  - `GAME.md`：§1 の一文を「スマホ縦画面で、ヒョウモントカゲモドキのブリーダーとして飼育・繁殖・孵化・イベント販売を回すシミュレーション。」に変える。§10 の対象外から「Multiple pets, breeding, currencies, shops」を外し、「Death of adult animals」を残す。末尾に「Detailed spec: docs/superpowers/specs/2026-09-25-breeder-sim-design.md」を足す。
  - `CLAUDE.md`・`AGENTS.md`：「Project goal」の一文を「…a leopard-gecko breeder simulation: care, breeding, incubation and event sales across multiple cages.」に変える。「Expected Unity layout」に `Core/Colony*.cs`・`Core/GameCalendar.cs` の説明を1行ずつ足す。

- [ ] **Step 4: すべてのテスト・画面キャプチャ・実機を確認する**

Run:
```bash
scripts/run-unity-tests.sh; scripts/test-summary.py
scripts/capture-screens.sh
scripts/build-ios.sh --run
```
Expected:
- テストの失敗0
- `Logs/Screens/00-home.png`：ラック1台（ケージ1つ＋空き棚3つ）、上部に日付と所持金
- `Logs/Screens/00-cage-list.png`：一括の世話のボタンとケージ1の行
- ケージ詳細：ケージ名と◀▶、「ベビー 3.0g」の表示
- iPhone：既存のセーブがケージ1に移行され、「データを新しい形式に移しました」が表示される

- [ ] **Step 5: コミット**

```bash
git add -A
git commit -m "Phase 1: debug cage/pet tools, docs and captures for the colony foundation"
```

---

## 自己点検（仕様との対応）

| 仕様 | 対応するタスク |
|---|---|
| §3 時間（現実1日＝1か月、世話は現実時間、年齢・脱皮はゲーム内時間、最大12時間分） | Task 1・3・7 |
| §5.3 体重・成長段階・成長前の拒食・日本語の段階名・ゲージ | Task 2・3・8 |
| §5.4 脱皮の間隔（段階ごと）と脱皮前の拒食 | Task 3 |
| §5.5 衰弱 | **段階2で行う**（性格や繁殖の条件と一緒に。段階1の範囲（§14）には入っていない） |
| §6.1 ケージ・ラック・1ケージ1匹 | Task 5・9 |
| §6.2 ホーム・タブ・ケージ詳細・スワイプ・動くヤモリは1匹だけ | Task 9（台帳は段階3、孵卵器は段階5のプレースホルダー） |
| §6.3 個別・一括の世話、餌代、電気代、お金が足りないとき | Task 4・5・7・8 |
| §12 スキーマ3と移行（バックアップ） | Task 6 |
| §13 テスト方針（段階1の分） | 各タスク |
| §14 段階1の完了条件 | Task 10 |
