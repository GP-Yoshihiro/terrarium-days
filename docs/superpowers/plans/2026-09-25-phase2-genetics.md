# 段階2：遺伝・モルフ（見た目の描き分け）・雌雄の判明・性格 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 個体に遺伝子型・プレイヤーが知っている遺伝情報・性格・雌雄の判明フラグを持たせ、モルフ名と見た目の色をそこから決め、性格を日常の行動・拒食・体重の増え方に反映する。

**Architecture:** 遺伝（`Genes`・`Genotype`・`GeneticsCalculator`・`KnownGenetics`・`MorphNamer`）、性格（`PersonalityTraits`）、色（`MorphAppearance`）はすべて Unity に依存しない Core の純粋なクラスにし、EditMode テストで確かめる。スプライトは既存の通常色のフレームを実行時に色の置き換えで描き分け（`MorphRecolor`）、モルフごとにキャッシュする（`MorphSprites`）。脱皮前の白っぽい見た目も同じ仕組みで実行時に作り、`Resources/GeckoPreShed` は廃止する。

**Tech Stack:** Unity 6000.5.10f1（UI Toolkit、JsonUtility、NUnit / Unity Test Framework）、C#、macOS 上の CLI（`scripts/run-unity-tests.sh`、`scripts/test-summary.py`、`scripts/capture-screens.sh`、`scripts/build-ios.sh`）。

**Spec:** `docs/superpowers/specs/2026-09-25-breeder-sim-design.md`（本計画は §4 全体・§5.1 のうち「ヤングで判明」・§5.2・§12 の個体データ・§14 段階2 を実装する）

## Global Constraints

- ユーザーへの応答は日本語。コード・コメント・コミットメッセージは英語（CLAUDE.md）。
- 遺伝子（§4.1）：トレンパーアルビノ／ベルアルビノ／レインウォーターアルビノ（劣性・互いに別の遺伝子座）、エクリプス（劣性）、ブリザード（劣性）、マーフィーパターンレス（劣性）、マックスノー（共優性、2つでスーパースノー）、ホワイト&イエロー（優性）、ハイポ・タンジェリン（多因子 0〜100）。
- 多因子の子＝両親の平均＋正規分布のばらつき（標準偏差10）、0〜100に収める。
- 名前（§4.2）：見た目に出る遺伝子を並べ、見た目に出ないヘテロを「ヘテロ○○」で続ける。通称：トレンパーアルビノ＋ブリザード＝ブレイジングブリザード、トレンパーアルビノ＋エクリプス＝レイプター（表は拡張できること）。ハイポ70以上・タンジェリン60以上で名前に含める。何もなければ「ノーマル」。
- ポッシブルヘテロ（§4.3）：ヘテロ×ヘテロの見た目ノーマルの子は66%、ヘテロ×ノーマルの子は50%。
- 予測（§4.4）はプレイヤーが知っている情報から、実際の子は本当の遺伝子型から決める。
- 性格（§5.2）：おっとり／臆病／好奇心旺盛／気が強い／食いしん坊。係数・相性表・受け継ぎ（各親20%、残り60%ランダム）は仕様どおり。
- 雌雄はヤングになった時点で判明する。既存の1匹（移行した個体）は「ノーマル（ヘテロ不明）」、性格ランダム、雌雄は不明として引き継ぐ（§1 設計者の既定値）。
- 画面の文字は日本語。
- セーブはスキーマ3のまま（フィールドを追加する）。段階1で保存されたスキーマ3のファイルも読めること。
- 表示でレイアウトがずれないよう、固定の高さの欄は `visibility` で表示を切り替える（既存の方針）。
- 新しい .cs ファイルの .meta は手書きしない。Unity に生成させる（`scripts/run-unity-tests.sh` の実行で生成される）。`scripts/test-summary.py` が「.meta will be ignored」を報告したら失敗扱い。

## 計画者の判断（仕様書に書かれていない点）

| 判断 | 理由 |
|---|---|
| ホワイト&イエローは1つでも2つでも同じ見た目・同じ名前。予測では見た目がW&Yの親を「1つ持ち」とみなす | 仕様は優性とだけ定める。見た目で区別できないため |
| 予測表は単一遺伝子だけで作り、ハイポ・タンジェリンの名前は両親の平均値で判定する | 多因子の分布を表にすると複雑になり、段階4の画面まで使い道がない |
| 既存の個体・新規ゲームの最初の1匹・段階1のセーブの個体は「スターター遺伝子型」：ノーマル、ハイポ・タンジェリンは10〜40のランダム、劣性遺伝子ごとに10%で隠れヘテロ。プレイヤーには「ヘテロ不明」と表示 | 仕様の「ノーマル（ヘテロ不明）」に、繁殖で驚きが出る余地を持たせる |
| マーフィーパターンレスの斑点は、ヤング以上で消える | 仕様「成長とともに斑点が消える」を段階で表す |
| ノーマルの色は今のスプライトと同じにする。エクリプスは目のハイライトを消して真っ黒にする | 既存の見た目と画面キャプチャを変えない |
| 雌雄が判明していない個体は、ヤング以上なら次の脱皮で判明する（ベビーからヤングへの段階上げにも脱皮が伴う） | 移行した大人の個体でも行き詰まらずに判明する |
| 衰弱（§5.5）は段階4（繁殖）で入れる。価格の倍率と相性の表は、この段階で表として作りテストする（使うのは段階4・6） | 衰弱の効果（繁殖不可・価格×0.3）は段階4・6の機能にしか効かない |

## ファイル構成

| ファイル | 種別 | 役割 |
|---|---|---|
| `Assets/Scripts/Core/Genes.cs` | 新規 | 遺伝子の列挙・遺伝の仕方・日本語名・見た目に出るか |
| `Assets/Scripts/Core/Genotype.cs` | 新規 | 本当の遺伝子型（単一遺伝子のコピー数＋ハイポ・タンジェリン） |
| `Assets/Scripts/Core/GeneticsCalculator.cs` | 新規 | メンデルの確率、実際の子の決定、予測表 |
| `Assets/Scripts/Core/KnownGenetics.cs` | 新規 | プレイヤーが知っているヘテロ情報、子のポッシブルヘテロ |
| `Assets/Scripts/Core/MorphNamer.cs` | 新規 | 見た目の名前と、ヘテロを含む名前 |
| `Assets/Scripts/Core/StarterGenetics.cs` | 新規 | スターター遺伝子型・展示用モルフ一覧 |
| `Assets/Scripts/Core/Personality.cs` | 新規 | 性格・相性の列挙と `PersonalityTraits` |
| `Assets/Scripts/Core/Rgb.cs` | 新規 | 色（Unity の型を使わない） |
| `Assets/Scripts/Core/MorphAppearance.cs` | 新規 | `PaletteRole`・`MorphPalette`・モルフの色・脱皮前の色 |
| `Assets/Scripts/UI/MorphRecolor.cs` | 新規 | テクスチャの色の置き換え |
| `Assets/Scripts/UI/MorphSprites.cs` | 新規 | モルフごとのスプライトライブラリのキャッシュ |
| `Assets/Scripts/Core/PetState.cs` | 変更 | 遺伝子型・知っている情報・性格・雌雄判明フラグ |
| `Assets/Scripts/Core/PetBehaviourTuning.cs` | 変更 | `Clone()` |
| `Assets/Scripts/Core/GrowthModel.cs` | 変更 | 体重の増え方に性格の係数 |
| `Assets/Scripts/Core/AppetiteModel.cs` | 変更 | 脱皮前の拒食期間に性格の係数 |
| `Assets/Scripts/Core/OfflineProgressCalculator.cs` | 変更 | 成長前の拒食期間に性格の係数、脱皮で雌雄が判明 |
| `Assets/Scripts/Core/OfflineProgressResult.cs` | 変更 | `SexRevealed` |
| `Assets/Scripts/Core/ColonySession.cs` | 変更 | `ColonyTickReport.SexReveals` |
| `Assets/Scripts/Core/ColonySaveData.cs` | 変更 | 遺伝・性格・雌雄判明のフィールド |
| `Assets/Scripts/Core/ColonySaveService.cs` | 変更 | 上の保存・読み込み・移行 |
| `Assets/Scripts/Core/Colony.cs` | 変更 | 最初の1匹にスターター遺伝子型 |
| `Assets/Scripts/UI/PetSpriteLibrary.cs` | 変更 | パレットを受け取り、フレームを遅延で描き分ける |
| `Assets/Scripts/UI/PetActor.cs`・`HomeView.cs`・`CageStatusText.cs`・`TerrariumView.cs` | 変更 | モルフの見た目・名前・性格・判明の表示 |
| `Assets/UI/Terrarium.uxml`・`Terrarium.uss` | 変更 | プロフィール欄 |
| `Assets/Resources/Gecko/*.png.meta` | 変更 | `isReadable: 1` |
| `Assets/Resources/GeckoPreShed/` | 削除 | 実行時に作るため不要 |
| `tools/sprites/gecko_sprites.py` | 変更 | 脱皮前の画像を書き出さない |
| テスト | 新規・変更 | `GeneticsTests`・`MorphNamingTests`・`PersonalityTests`・`MorphAppearanceTests`・`MorphRecolorTests`（EditMode）、`ColonySaveServiceTests`・`OfflineProgressCalculatorTests` などの追加、`TerrariumViewTests`・`ScreenCaptureTests`（PlayMode） |
| `GAME.md`・`CLAUDE.md`・`AGENTS.md` | 変更 | 遺伝・性格・描き分けの説明 |

テストの実行（全タスク共通）：

```bash
scripts/run-unity-tests.sh EditMode && python3 scripts/test-summary.py Logs/EditMode-results.xml
scripts/run-unity-tests.sh PlayMode && python3 scripts/test-summary.py Logs/PlayMode-results.xml
```

特定のテストだけ流すときは `-testFilter <完全名>` を付ける（例：`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.GeneticsTests`）。

---

### Task 1: 遺伝子と遺伝子型、メンデルの確率と実際の子

**Files:**
- Create: `Assets/Scripts/Core/Genes.cs`
- Create: `Assets/Scripts/Core/Genotype.cs`
- Create: `Assets/Scripts/Core/GeneticsCalculator.cs`
- Test: `Assets/Tests/EditMode/GeneticsTests.cs`

**Interfaces:**
- Produces: `GeneId`（`TremperAlbino, BellAlbino, RainwaterAlbino, Eclipse, Blizzard, MurphyPatternless, MackSnow, WhiteAndYellow`）、`Inheritance`、`Genes.All`・`Genes.InheritanceOf`・`Genes.IsRecessive`・`Genes.IsVisible(gene, copies)`・`Genes.Label`、`Genotype`（`Copies`・`Set`・`Shows`・`Hypo`・`Tangerine`・`Clone`・`Normal`）、`GeneticsCalculator.CopiesOutcome(int, int) → double[3]`・`GeneticsCalculator.Breed(Genotype mother, Genotype father, Random) → Genotype`・`GeneticsCalculator.Gaussian(Random)`・`GeneticsCalculator.PolygenicSpread`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/GeneticsTests.cs`:

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class GeneticsTests
    {
        [TestCase(2, 2, 0d, 0d, 1d)]
        [TestCase(1, 1, 0.25d, 0.5d, 0.25d)]
        [TestCase(1, 0, 0.5d, 0.5d, 0d)]
        [TestCase(2, 0, 0d, 1d, 0d)]
        [TestCase(0, 0, 1d, 0d, 0d)]
        public void CopiesOutcome_FollowsMendel(int mother, int father, double none, double one, double two)
        {
            var odds = GeneticsCalculator.CopiesOutcome(mother, father);

            Assert.That(odds[0], Is.EqualTo(none).Within(1e-9));
            Assert.That(odds[1], Is.EqualTo(one).Within(1e-9));
            Assert.That(odds[2], Is.EqualTo(two).Within(1e-9));
        }

        [Test]
        public void Visibility_DependsOnInheritance()
        {
            Assert.That(Genes.IsVisible(GeneId.TremperAlbino, 1), Is.False);
            Assert.That(Genes.IsVisible(GeneId.TremperAlbino, 2), Is.True);
            Assert.That(Genes.IsVisible(GeneId.MackSnow, 1), Is.True);
            Assert.That(Genes.IsVisible(GeneId.WhiteAndYellow, 1), Is.True);
            Assert.That(Genes.IsVisible(GeneId.WhiteAndYellow, 0), Is.False);
        }

        [Test]
        public void Genotype_ClampsCopiesAndPolygenicValues()
        {
            var genotype = Genotype.Normal().Set(GeneId.Eclipse, 5);
            genotype.Hypo = 140d;
            genotype.Tangerine = -3d;

            Assert.That(genotype.Copies(GeneId.Eclipse), Is.EqualTo(2));
            Assert.That(genotype.Hypo, Is.EqualTo(100d));
            Assert.That(genotype.Tangerine, Is.EqualTo(0d));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var original = Genotype.Normal().Set(GeneId.Blizzard, 1);
            var copy = original.Clone().Set(GeneId.Blizzard, 2);

            Assert.That(original.Copies(GeneId.Blizzard), Is.EqualTo(1));
            Assert.That(copy.Copies(GeneId.Blizzard), Is.EqualTo(2));
        }

        [Test]
        public void Breed_HetTimesHet_GivesAboutAQuarterVisual()
        {
            var parent = Genotype.Normal().Set(GeneId.TremperAlbino, 1);
            var random = new Random(1234);
            var visual = 0;
            const int count = 4000;
            for (var i = 0; i < count; i++)
            {
                if (GeneticsCalculator.Breed(parent, parent, random).Shows(GeneId.TremperAlbino))
                {
                    visual++;
                }
            }

            Assert.That(visual / (double)count, Is.EqualTo(0.25d).Within(0.03d));
        }

        [Test]
        public void Breed_DifferentAlbinoStrains_NeverGivesAnAlbino()
        {
            var tremper = Genotype.Normal().Set(GeneId.TremperAlbino, 2);
            var bell = Genotype.Normal().Set(GeneId.BellAlbino, 2);
            var random = new Random(7);
            for (var i = 0; i < 200; i++)
            {
                var child = GeneticsCalculator.Breed(tremper, bell, random);
                Assert.That(child.Shows(GeneId.TremperAlbino) || child.Shows(GeneId.BellAlbino), Is.False);
                Assert.That(child.Copies(GeneId.TremperAlbino), Is.EqualTo(1));
                Assert.That(child.Copies(GeneId.BellAlbino), Is.EqualTo(1));
            }
        }

        [Test]
        public void Breed_MackSnowTimesMackSnow_GivesAboutAQuarterSuperSnow()
        {
            var parent = Genotype.Normal().Set(GeneId.MackSnow, 1);
            var random = new Random(99);
            var super = 0;
            const int count = 4000;
            for (var i = 0; i < count; i++)
            {
                if (GeneticsCalculator.Breed(parent, parent, random).Copies(GeneId.MackSnow) == 2)
                {
                    super++;
                }
            }

            Assert.That(super / (double)count, Is.EqualTo(0.25d).Within(0.03d));
        }

        [Test]
        public void Breed_Polygenic_CentresOnTheParentMeanWithSpreadTen()
        {
            var mother = Genotype.Normal(hypo: 40d, tangerine: 50d);
            var father = Genotype.Normal(hypo: 60d, tangerine: 50d);
            var random = new Random(5);
            const int count = 3000;
            double sum = 0d, sumSquares = 0d;
            for (var i = 0; i < count; i++)
            {
                var hypo = GeneticsCalculator.Breed(mother, father, random).Hypo;
                Assert.That(hypo, Is.InRange(0d, 100d));
                sum += hypo;
                sumSquares += hypo * hypo;
            }

            var mean = sum / count;
            var sd = Math.Sqrt(sumSquares / count - mean * mean);
            Assert.That(mean, Is.EqualTo(50d).Within(1d));
            Assert.That(sd, Is.EqualTo(GeneticsCalculator.PolygenicSpread).Within(1.5d));
        }

        [Test]
        public void Breed_IsReproducibleForTheSameSeed()
        {
            var mother = Genotype.Normal().Set(GeneId.Eclipse, 1).Set(GeneId.MackSnow, 1);
            var father = Genotype.Normal().Set(GeneId.Eclipse, 2);

            var a = GeneticsCalculator.Breed(mother, father, new Random(42));
            var b = GeneticsCalculator.Breed(mother, father, new Random(42));

            foreach (var gene in Genes.All)
            {
                Assert.That(a.Copies(gene), Is.EqualTo(b.Copies(gene)));
            }

            Assert.That(a.Hypo, Is.EqualTo(b.Hypo));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.GeneticsTests`
Expected: コンパイルエラー（`GeneticsCalculator` などが無い）。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/Genes.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>The single-locus genes (§4.1). Each albino strain is its own locus.</summary>
    public enum GeneId
    {
        TremperAlbino,
        BellAlbino,
        RainwaterAlbino,
        Eclipse,
        Blizzard,
        MurphyPatternless,
        MackSnow,
        WhiteAndYellow
    }

    public enum Inheritance
    {
        Recessive,
        Codominant,
        Dominant
    }

    public static class Genes
    {
        public static readonly GeneId[] All = (GeneId[])Enum.GetValues(typeof(GeneId));

        public static Inheritance InheritanceOf(GeneId gene)
        {
            switch (gene)
            {
                case GeneId.MackSnow:
                    return Inheritance.Codominant;
                case GeneId.WhiteAndYellow:
                    return Inheritance.Dominant;
                default:
                    return Inheritance.Recessive;
            }
        }

        public static bool IsRecessive(GeneId gene) => InheritanceOf(gene) == Inheritance.Recessive;

        /// <summary>Whether an animal carrying this many copies shows the gene.</summary>
        public static bool IsVisible(GeneId gene, int copies) => IsRecessive(gene) ? copies >= 2 : copies >= 1;

        public static string Label(GeneId gene)
        {
            switch (gene)
            {
                case GeneId.TremperAlbino:
                    return "トレンパーアルビノ";
                case GeneId.BellAlbino:
                    return "ベルアルビノ";
                case GeneId.RainwaterAlbino:
                    return "レインウォーターアルビノ";
                case GeneId.Eclipse:
                    return "エクリプス";
                case GeneId.Blizzard:
                    return "ブリザード";
                case GeneId.MurphyPatternless:
                    return "マーフィーパターンレス";
                case GeneId.MackSnow:
                    return "マックスノー";
                default:
                    return "ホワイト&イエロー";
            }
        }
    }
}
```

`Assets/Scripts/Core/Genotype.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// An animal's true genes: 0–2 copies per single-locus gene, plus the polygenic hypo and
    /// tangerine values (0–100). Hidden from the player; see <see cref="KnownGenetics"/>.
    /// </summary>
    public sealed class Genotype
    {
        private readonly int[] copies = new int[Genes.All.Length];
        private double hypo;
        private double tangerine;

        public double Hypo
        {
            get => hypo;
            set => hypo = Math.Max(0d, Math.Min(100d, value));
        }

        public double Tangerine
        {
            get => tangerine;
            set => tangerine = Math.Max(0d, Math.Min(100d, value));
        }

        public int Copies(GeneId gene) => copies[(int)gene];

        public Genotype Set(GeneId gene, int count)
        {
            copies[(int)gene] = Math.Max(0, Math.Min(2, count));
            return this;
        }

        public bool Shows(GeneId gene) => Genes.IsVisible(gene, Copies(gene));

        public Genotype Clone()
        {
            var clone = new Genotype { Hypo = Hypo, Tangerine = Tangerine };
            Array.Copy(copies, clone.copies, copies.Length);
            return clone;
        }

        public static Genotype Normal(double hypo = 25d, double tangerine = 25d) =>
            new Genotype { Hypo = hypo, Tangerine = tangerine };
    }
}
```

`Assets/Scripts/Core/GeneticsCalculator.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>Mendelian odds and the actual offspring genotype (§4.1, §4.4).</summary>
    public static class GeneticsCalculator
    {
        /// <summary>Standard deviation of a polygenic value around the parents' mean.</summary>
        public const double PolygenicSpread = 10d;

        /// <summary>Probabilities that a child carries 0, 1 or 2 copies.</summary>
        public static double[] CopiesOutcome(int motherCopies, int fatherCopies)
        {
            var m = Math.Max(0, Math.Min(2, motherCopies)) / 2d;
            var f = Math.Max(0, Math.Min(2, fatherCopies)) / 2d;
            return new[] { (1d - m) * (1d - f), m * (1d - f) + (1d - m) * f, m * f };
        }

        /// <summary>The child's true genotype. Deterministic for a given Random seed.</summary>
        public static Genotype Breed(Genotype mother, Genotype father, Random random)
        {
            var child = new Genotype();
            foreach (var gene in Genes.All)
            {
                child.Set(gene, Allele(mother.Copies(gene), random) + Allele(father.Copies(gene), random));
            }

            child.Hypo = (mother.Hypo + father.Hypo) / 2d + Gaussian(random) * PolygenicSpread;
            child.Tangerine = (mother.Tangerine + father.Tangerine) / 2d + Gaussian(random) * PolygenicSpread;
            return child;
        }

        /// <summary>A standard normal sample (Box–Muller).</summary>
        public static double Gaussian(Random random)
        {
            var u1 = 1d - random.NextDouble();
            var u2 = random.NextDouble();
            return Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2);
        }

        private static int Allele(int copies, Random random)
        {
            // Always draw, so one parent's copy count never shifts the other's random sequence.
            var roll = random.NextDouble();
            return copies >= 2 ? 1 : copies <= 0 ? 0 : roll < 0.5d ? 1 : 0;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.GeneticsTests`
Expected: PASS（全件）。続けて EditMode 全体も PASS（既存 211 件に影響しない）。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Genes.cs* Assets/Scripts/Core/Genotype.cs* Assets/Scripts/Core/GeneticsCalculator.cs* Assets/Tests/EditMode/GeneticsTests.cs*
git commit -m "Add genotype model with Mendelian odds and seeded breeding"
```

---

### Task 2: プレイヤーが知っている情報、モルフ名、予測表

**Files:**
- Create: `Assets/Scripts/Core/KnownGenetics.cs`
- Create: `Assets/Scripts/Core/MorphNamer.cs`
- Modify: `Assets/Scripts/Core/GeneticsCalculator.cs`（`PredictVisualOdds` と `MorphOdds` を追加）
- Test: `Assets/Tests/EditMode/MorphNamingTests.cs`

**Interfaces:**
- Consumes: Task 1 の `Genes`・`Genotype`・`GeneticsCalculator.CopiesOutcome`
- Produces: `KnownGenetics`（`HetsUnknown`・`HetProbability(GeneId)`・`SetHet(GeneId, double)`・`Clone()`・`Unknown()`・`BelievedCopies(Genotype, KnownGenetics, GeneId) → double[3]`・`Blend(double[], double[]) → double[3]`・`ForChild(Genotype mother, KnownGenetics motherKnown, Genotype father, KnownGenetics fatherKnown, Genotype child)`）、`MorphNamer.VisualName(Genotype)`・`MorphNamer.FullName(Genotype, KnownGenetics)`・`MorphNamer.CommonNames`・`MorphNamer.HypoNameThreshold`・`MorphNamer.TangerineNameThreshold`、`CommonName`、`MorphOdds { string Name; double Probability; }`、`GeneticsCalculator.PredictVisualOdds(Genotype mother, KnownGenetics motherKnown, Genotype father, KnownGenetics fatherKnown) → List<MorphOdds>`（確率の高い順）

`KnownGenetics` はヘテロの情報だけを持つ。見た目（見た目に出ている遺伝子とマックスノーのコピー数）はプレイヤーに見えているので、本当の遺伝子型の `Shows`（とマックスノーの `Copies`）をそのまま使う。

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/MorphNamingTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class MorphNamingTests
    {
        [Test]
        public void VisualName_NormalIsNormal()
        {
            Assert.That(MorphNamer.VisualName(Genotype.Normal()), Is.EqualTo("ノーマル"));
        }

        [Test]
        public void VisualName_AHetDoesNotShow()
        {
            Assert.That(MorphNamer.VisualName(Genotype.Normal().Set(GeneId.TremperAlbino, 1)), Is.EqualTo("ノーマル"));
        }

        [TestCase(GeneId.TremperAlbino, 2, "トレンパーアルビノ")]
        [TestCase(GeneId.Eclipse, 2, "エクリプス")]
        [TestCase(GeneId.MackSnow, 1, "マックスノー")]
        [TestCase(GeneId.MackSnow, 2, "スーパースノー")]
        [TestCase(GeneId.WhiteAndYellow, 1, "ホワイト&イエロー")]
        [TestCase(GeneId.WhiteAndYellow, 2, "ホワイト&イエロー")]
        public void VisualName_SingleGenes(GeneId gene, int copies, string expected)
        {
            Assert.That(MorphNamer.VisualName(Genotype.Normal().Set(gene, copies)), Is.EqualTo(expected));
        }

        [Test]
        public void VisualName_UsesCommonNames()
        {
            var blazing = Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2);
            var raptorSnow = Genotype.Normal().Set(GeneId.MackSnow, 1).Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2);

            Assert.That(MorphNamer.VisualName(blazing), Is.EqualTo("ブレイジングブリザード"));
            Assert.That(MorphNamer.VisualName(raptorSnow), Is.EqualTo("マックスノー レイプター"));
        }

        [Test]
        public void VisualName_BellAlbinoWithBlizzardHasNoCommonName()
        {
            var genotype = Genotype.Normal().Set(GeneId.BellAlbino, 2).Set(GeneId.Blizzard, 2);

            Assert.That(MorphNamer.VisualName(genotype), Is.EqualTo("ベルアルビノ ブリザード"));
        }

        [Test]
        public void VisualName_PolygenicNamesNeedTheThresholds()
        {
            Assert.That(MorphNamer.VisualName(Genotype.Normal(hypo: 69.9d)), Is.EqualTo("ノーマル"));
            Assert.That(MorphNamer.VisualName(Genotype.Normal(hypo: 70d)), Is.EqualTo("ハイポ"));
            Assert.That(MorphNamer.VisualName(Genotype.Normal(tangerine: 60d)), Is.EqualTo("タンジェリン"));
            Assert.That(MorphNamer.VisualName(Genotype.Normal(hypo: 80d, tangerine: 80d).Set(GeneId.MackSnow, 1)),
                Is.EqualTo("ハイポ タンジェリン マックスノー"));
        }

        [Test]
        public void FullName_ListsHetsAndPossibleHets()
        {
            var genotype = Genotype.Normal().Set(GeneId.TremperAlbino, 1).Set(GeneId.Eclipse, 1);
            var known = new KnownGenetics().SetHet(GeneId.TremperAlbino, 1d).SetHet(GeneId.Eclipse, 2d / 3d);

            Assert.That(MorphNamer.FullName(genotype, known), Is.EqualTo("ノーマル ヘテロトレンパーアルビノ 66%ポッシブルヘテロエクリプス"));
        }

        [Test]
        public void FullName_UnknownHets()
        {
            Assert.That(MorphNamer.FullName(Genotype.Normal(), KnownGenetics.Unknown()), Is.EqualTo("ノーマル（ヘテロ不明）"));
        }

        [Test]
        public void FullName_IgnoresHetInfoForAVisibleGene()
        {
            var genotype = Genotype.Normal().Set(GeneId.Eclipse, 2);
            var known = new KnownGenetics().SetHet(GeneId.Eclipse, 1d);

            Assert.That(MorphNamer.FullName(genotype, known), Is.EqualTo("エクリプス"));
        }

        [Test]
        public void ForChild_HetTimesHet_NormalLookingChildIs66Percent()
        {
            var het = Genotype.Normal().Set(GeneId.Blizzard, 1);
            var hetKnown = new KnownGenetics().SetHet(GeneId.Blizzard, 1d);

            var known = KnownGenetics.ForChild(het, hetKnown, het, hetKnown, Genotype.Normal());

            Assert.That(known.HetProbability(GeneId.Blizzard), Is.EqualTo(2d / 3d).Within(1e-9));
            Assert.That(known.HetsUnknown, Is.False);
        }

        [Test]
        public void ForChild_HetTimesNormal_Is50Percent()
        {
            var het = Genotype.Normal().Set(GeneId.Blizzard, 1);
            var hetKnown = new KnownGenetics().SetHet(GeneId.Blizzard, 1d);

            var known = KnownGenetics.ForChild(het, hetKnown, Genotype.Normal(), new KnownGenetics(), Genotype.Normal());

            Assert.That(known.HetProbability(GeneId.Blizzard), Is.EqualTo(0.5d).Within(1e-9));
        }

        [Test]
        public void ForChild_VisualTimesNormal_IsAProvenHet()
        {
            var visual = Genotype.Normal().Set(GeneId.Eclipse, 2);

            var known = KnownGenetics.ForChild(visual, new KnownGenetics(), Genotype.Normal(), new KnownGenetics(),
                Genotype.Normal().Set(GeneId.Eclipse, 1));

            Assert.That(known.HetProbability(GeneId.Eclipse), Is.EqualTo(1d));
        }

        [Test]
        public void ForChild_UsesWhatThePlayerKnowsNotTheTruth()
        {
            // The father secretly carries tremper, but the player does not know it.
            var secretHet = Genotype.Normal().Set(GeneId.TremperAlbino, 1);

            var known = KnownGenetics.ForChild(Genotype.Normal(), new KnownGenetics(), secretHet, KnownGenetics.Unknown(),
                Genotype.Normal().Set(GeneId.TremperAlbino, 1));

            Assert.That(known.HetProbability(GeneId.TremperAlbino), Is.EqualTo(0d));
        }

        [Test]
        public void ForChild_AVisualChildHasNoHetEntry()
        {
            var visual = Genotype.Normal().Set(GeneId.Eclipse, 2);

            var known = KnownGenetics.ForChild(visual, new KnownGenetics(), visual, new KnownGenetics(), visual.Clone());

            Assert.That(known.HetProbability(GeneId.Eclipse), Is.EqualTo(0d));
        }

        [Test]
        public void Predict_VisualTimesProvenHet_IsHalfAndHalf()
        {
            var visual = Genotype.Normal().Set(GeneId.TremperAlbino, 2);
            var het = Genotype.Normal().Set(GeneId.TremperAlbino, 1);
            var hetKnown = new KnownGenetics().SetHet(GeneId.TremperAlbino, 1d);

            var odds = GeneticsCalculator.PredictVisualOdds(visual, new KnownGenetics(), het, hetKnown);

            Assert.That(odds.Count, Is.EqualTo(2));
            Assert.That(odds.Single(o => o.Name == "トレンパーアルビノ").Probability, Is.EqualTo(0.5d).Within(1e-9));
            Assert.That(odds.Single(o => o.Name == "ノーマル").Probability, Is.EqualTo(0.5d).Within(1e-9));
        }

        [Test]
        public void Predict_MackSnowPair_IsQuarterHalfQuarter_SortedByProbability()
        {
            var snow = Genotype.Normal().Set(GeneId.MackSnow, 1);

            var odds = GeneticsCalculator.PredictVisualOdds(snow, new KnownGenetics(), snow, new KnownGenetics());

            Assert.That(odds[0].Name, Is.EqualTo("マックスノー"));
            Assert.That(odds[0].Probability, Is.EqualTo(0.5d).Within(1e-9));
            Assert.That(odds.Single(o => o.Name == "スーパースノー").Probability, Is.EqualTo(0.25d).Within(1e-9));
            Assert.That(odds.Single(o => o.Name == "ノーマル").Probability, Is.EqualTo(0.25d).Within(1e-9));
        }

        [Test]
        public void Predict_ProbabilitiesSumToOne()
        {
            var mother = Genotype.Normal().Set(GeneId.MackSnow, 1).Set(GeneId.Eclipse, 1).Set(GeneId.WhiteAndYellow, 1);
            var motherKnown = new KnownGenetics().SetHet(GeneId.Eclipse, 0.66d).SetHet(GeneId.Blizzard, 0.5d);
            var father = Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 1);
            var fatherKnown = new KnownGenetics().SetHet(GeneId.Blizzard, 1d).SetHet(GeneId.Eclipse, 1d);

            var odds = GeneticsCalculator.PredictVisualOdds(mother, motherKnown, father, fatherKnown);

            Assert.That(odds.Sum(o => o.Probability), Is.EqualTo(1d).Within(1e-9));
            Assert.That(odds.Select(o => o.Name).Distinct().Count(), Is.EqualTo(odds.Count));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.MorphNamingTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/KnownGenetics.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// What the player knows about an animal's hidden (het) genes (§4.3): per recessive gene
    /// the chance it carries one copy (1 = proven het, between 0 and 1 = possible het).
    /// What shows is visible to the player and is read from the genotype itself.
    /// </summary>
    public sealed class KnownGenetics
    {
        private readonly double[] het = new double[Genes.All.Length];

        /// <summary>Hets were never tracked (e.g. the starting animal): shown as 「ヘテロ不明」.</summary>
        public bool HetsUnknown { get; set; }

        public double HetProbability(GeneId gene) => het[(int)gene];

        public KnownGenetics SetHet(GeneId gene, double probability)
        {
            het[(int)gene] = Math.Max(0d, Math.Min(1d, probability));
            return this;
        }

        public KnownGenetics Clone()
        {
            var clone = new KnownGenetics { HetsUnknown = HetsUnknown };
            Array.Copy(het, clone.het, het.Length);
            return clone;
        }

        public static KnownGenetics Unknown() => new KnownGenetics { HetsUnknown = true };

        /// <summary>The copy count (0/1/2) the player believes an animal has, as probabilities.</summary>
        public static double[] BelievedCopies(Genotype genotype, KnownGenetics known, GeneId gene)
        {
            if (!Genes.IsRecessive(gene))
            {
                // Visible: mack snow shows 1 vs 2 copies; white & yellow looks the same with
                // either, so a visible W&Y is assumed to carry one.
                var copies = !genotype.Shows(gene) ? 0 : gene == GeneId.MackSnow ? genotype.Copies(gene) : 1;
                return OneHot(copies);
            }

            if (genotype.Shows(gene))
            {
                return OneHot(2);
            }

            var p = known.HetProbability(gene);
            return new[] { 1d - p, p, 0d };
        }

        /// <summary>Child copy odds from two parents' copy-count distributions.</summary>
        public static double[] Blend(double[] mother, double[] father)
        {
            var result = new double[3];
            for (var m = 0; m < 3; m++)
            {
                for (var f = 0; f < 3; f++)
                {
                    var weight = mother[m] * father[f];
                    if (weight <= 0d)
                    {
                        continue;
                    }

                    var outcome = GeneticsCalculator.CopiesOutcome(m, f);
                    for (var k = 0; k < 3; k++)
                    {
                        result[k] += weight * outcome[k];
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// What the player knows about a newly hatched child: for each recessive gene the child
        /// does not show, the chance it is het given what the player knew about the parents.
        /// </summary>
        public static KnownGenetics ForChild(Genotype mother, KnownGenetics motherKnown, Genotype father,
            KnownGenetics fatherKnown, Genotype child)
        {
            var known = new KnownGenetics();
            foreach (var gene in Genes.All)
            {
                if (!Genes.IsRecessive(gene) || child.Shows(gene))
                {
                    continue;
                }

                var odds = Blend(BelievedCopies(mother, motherKnown, gene), BelievedCopies(father, fatherKnown, gene));
                var notVisual = odds[0] + odds[1];
                if (notVisual > 0d)
                {
                    known.SetHet(gene, odds[1] / notVisual);
                }
            }

            return known;
        }

        private static double[] OneHot(int copies)
        {
            var result = new double[3];
            result[copies] = 1d;
            return result;
        }
    }
}
```

`Assets/Scripts/Core/MorphNamer.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>A trade name that replaces a set of visible genes (§4.2).</summary>
    public sealed class CommonName
    {
        public CommonName(string name, params GeneId[] genes)
        {
            Name = name;
            Genes = genes;
        }

        public string Name { get; }

        public GeneId[] Genes { get; }
    }

    /// <summary>Morph names as breeders write them (§4.2).</summary>
    public static class MorphNamer
    {
        public const double HypoNameThreshold = 70d;
        public const double TangerineNameThreshold = 60d;

        /// <summary>Order visible genes are written in.</summary>
        private static readonly GeneId[] NameOrder =
        {
            GeneId.WhiteAndYellow, GeneId.MackSnow, GeneId.TremperAlbino, GeneId.BellAlbino,
            GeneId.RainwaterAlbino, GeneId.Eclipse, GeneId.Blizzard, GeneId.MurphyPatternless
        };

        /// <summary>Checked in order; extend by adding entries.</summary>
        public static readonly List<CommonName> CommonNames = new List<CommonName>
        {
            new CommonName("ブレイジングブリザード", GeneId.TremperAlbino, GeneId.Blizzard),
            new CommonName("レイプター", GeneId.TremperAlbino, GeneId.Eclipse),
        };

        public static string VisualName(Genotype genotype)
        {
            var words = new List<string>();
            if (genotype.Hypo >= HypoNameThreshold)
            {
                words.Add("ハイポ");
            }

            if (genotype.Tangerine >= TangerineNameThreshold)
            {
                words.Add("タンジェリン");
            }

            // A gene belongs to at most one matched trade name; the name is written where
            // its first gene would have been.
            var usedBy = new Dictionary<GeneId, CommonName>();
            foreach (var common in CommonNames)
            {
                if (Array.TrueForAll(common.Genes, g => genotype.Shows(g) && !usedBy.ContainsKey(g)))
                {
                    foreach (var gene in common.Genes)
                    {
                        usedBy[gene] = common;
                    }
                }
            }

            var written = new HashSet<CommonName>();
            foreach (var gene in NameOrder)
            {
                if (!genotype.Shows(gene))
                {
                    continue;
                }

                if (usedBy.TryGetValue(gene, out var common))
                {
                    if (written.Add(common))
                    {
                        words.Add(common.Name);
                    }

                    continue;
                }

                words.Add(gene == GeneId.MackSnow && genotype.Copies(gene) == 2 ? "スーパースノー" : Genes.Label(gene));
            }

            return words.Count == 0 ? "ノーマル" : string.Join(" ", words);
        }

        /// <summary>The visual name followed by proven and possible hets as the player knows them.</summary>
        public static string FullName(Genotype genotype, KnownGenetics known)
        {
            var name = VisualName(genotype);
            foreach (var gene in Genes.All)
            {
                if (!Genes.IsRecessive(gene) || genotype.Shows(gene))
                {
                    continue;
                }

                var p = known.HetProbability(gene);
                if (p >= 1d)
                {
                    name += " ヘテロ" + Genes.Label(gene);
                }
                else if (p > 0d)
                {
                    name += $" {(int)Math.Floor(p * 100d + 1e-9)}%ポッシブルヘテロ{Genes.Label(gene)}";
                }
            }

            return known.HetsUnknown ? name + "（ヘテロ不明）" : name;
        }
    }
}
```

`Assets/Scripts/Core/GeneticsCalculator.cs` に追加（`using System.Collections.Generic;` を足す）:

```csharp
        /// <summary>
        /// Chance of each visual morph from what the player knows (§4.4). Single genes only;
        /// hypo/tangerine are judged on the parents' mean (see the plan's rulings).
        /// </summary>
        public static List<MorphOdds> PredictVisualOdds(Genotype mother, KnownGenetics motherKnown,
            Genotype father, KnownGenetics fatherKnown)
        {
            var perGene = new double[Genes.All.Length][];
            foreach (var gene in Genes.All)
            {
                perGene[(int)gene] = KnownGenetics.Blend(
                    KnownGenetics.BelievedCopies(mother, motherKnown, gene),
                    KnownGenetics.BelievedCopies(father, fatherKnown, gene));
            }

            var totals = new Dictionary<string, double>();
            var child = Genotype.Normal((mother.Hypo + father.Hypo) / 2d, (mother.Tangerine + father.Tangerine) / 2d);
            Enumerate(perGene, 0, 1d, child, totals);

            var result = new List<MorphOdds>();
            foreach (var pair in totals)
            {
                result.Add(new MorphOdds(pair.Key, pair.Value));
            }

            result.Sort((a, b) => b.Probability != a.Probability
                ? b.Probability.CompareTo(a.Probability)
                : string.CompareOrdinal(a.Name, b.Name));
            return result;
        }

        private static void Enumerate(double[][] perGene, int index, double probability, Genotype child,
            Dictionary<string, double> totals)
        {
            if (index == perGene.Length)
            {
                var name = MorphNamer.VisualName(child);
                totals.TryGetValue(name, out var sum);
                totals[name] = sum + probability;
                return;
            }

            for (var copies = 0; copies < 3; copies++)
            {
                var p = perGene[index][copies];
                if (p <= 0d)
                {
                    continue;
                }

                child.Set(Genes.All[index], copies);
                Enumerate(perGene, index + 1, probability * p, child, totals);
            }

            child.Set(Genes.All[index], 0);
        }
```

同じファイルの名前空間内に：

```csharp
    public readonly struct MorphOdds
    {
        public MorphOdds(string name, double probability)
        {
            Name = name;
            Probability = probability;
        }

        public string Name { get; }

        public double Probability { get; }
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.MorphNamingTests`
Expected: PASS。EditMode 全体も PASS。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/KnownGenetics.cs* Assets/Scripts/Core/MorphNamer.cs* Assets/Scripts/Core/GeneticsCalculator.cs Assets/Tests/EditMode/MorphNamingTests.cs*
git commit -m "Add morph names, player-known hets and offspring prediction"
```

---

### Task 3: 性格（表・受け継ぎ・行動／拒食／体重への影響）

**Files:**
- Create: `Assets/Scripts/Core/Personality.cs`
- Modify: `Assets/Scripts/Core/PetState.cs`（`Personality`・`PersonalityKnown` を追加）
- Modify: `Assets/Scripts/Core/PetBehaviourTuning.cs`（`Clone()` を追加）
- Modify: `Assets/Scripts/Core/GrowthModel.cs`（`FeedGain` に係数）
- Modify: `Assets/Scripts/Core/AppetiteModel.cs`（脱皮前の拒食期間に係数）
- Modify: `Assets/Scripts/Core/OfflineProgressCalculator.cs`（成長前の拒食期間に係数）
- Test: `Assets/Tests/EditMode/PersonalityTests.cs`

**Interfaces:**
- Produces: `Personality`（`Calm, Shy, Curious, Bold, Glutton`）、`Compatibility`（`Good, Normal, Bad`）、`PersonalityTraits.All`・`Label`・`ThreatMultiplier`・`WalkFrequencyMultiplier`・`HideMultiplier`・`FastMultiplier`・`WeightGainMultiplier`・`PriceMultiplier`・`IsEagerEater`・`CompatibilityOf(Personality male, Personality female)`・`MatingSuccessMultiplier(Compatibility)`・`ClutchDelta(Compatibility)`・`InheritChance`・`Inherit(Personality mother, Personality father, Random)`・`Roll(Random)`・`BehaviourTuningFor(PetBehaviourTuning, Personality) → PetBehaviourTuning`、`PetState.Personality`（既定 `Calm`）・`PetState.PersonalityKnown`（既定 `true`）、`PetBehaviourTuning.Clone()`

`Calm` の拒食・体重の係数は1なので、既定のままの既存テストは変わらない。

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/PersonalityTests.cs`:

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class PersonalityTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();

        [TestCase(Personality.Calm, "おっとり")]
        [TestCase(Personality.Shy, "臆病")]
        [TestCase(Personality.Curious, "好奇心旺盛")]
        [TestCase(Personality.Bold, "気が強い")]
        [TestCase(Personality.Glutton, "食いしん坊")]
        public void Labels(Personality personality, string label)
        {
            Assert.That(PersonalityTraits.Label(personality), Is.EqualTo(label));
        }

        [Test]
        public void Multipliers_MatchTheSpec()
        {
            Assert.That(PersonalityTraits.ThreatMultiplier(Personality.Calm), Is.EqualTo(0.5d));
            Assert.That(PersonalityTraits.ThreatMultiplier(Personality.Shy), Is.EqualTo(1.5d));
            Assert.That(PersonalityTraits.ThreatMultiplier(Personality.Bold), Is.EqualTo(1.5d));
            Assert.That(PersonalityTraits.WalkFrequencyMultiplier(Personality.Curious), Is.EqualTo(1.3d));
            Assert.That(PersonalityTraits.FastMultiplier(Personality.Shy), Is.EqualTo(1.5d));
            Assert.That(PersonalityTraits.FastMultiplier(Personality.Glutton), Is.EqualTo(0.5d));
            Assert.That(PersonalityTraits.WeightGainMultiplier(Personality.Bold), Is.EqualTo(1.1d));
            Assert.That(PersonalityTraits.WeightGainMultiplier(Personality.Glutton), Is.EqualTo(1.2d));
            Assert.That(PersonalityTraits.PriceMultiplier(Personality.Calm), Is.EqualTo(1.1d));
            Assert.That(PersonalityTraits.PriceMultiplier(Personality.Shy), Is.EqualTo(0.9d));
            Assert.That(PersonalityTraits.PriceMultiplier(Personality.Curious), Is.EqualTo(1.05d));
            Assert.That(PersonalityTraits.PriceMultiplier(Personality.Glutton), Is.EqualTo(1d));
        }

        [TestCase(Personality.Calm, Personality.Bold, Compatibility.Good)]
        [TestCase(Personality.Shy, Personality.Calm, Compatibility.Good)]
        [TestCase(Personality.Shy, Personality.Bold, Compatibility.Bad)]
        [TestCase(Personality.Bold, Personality.Shy, Compatibility.Bad)]
        [TestCase(Personality.Bold, Personality.Bold, Compatibility.Bad)]
        [TestCase(Personality.Curious, Personality.Glutton, Compatibility.Good)]
        [TestCase(Personality.Glutton, Personality.Glutton, Compatibility.Normal)]
        [TestCase(Personality.Shy, Personality.Shy, Compatibility.Normal)]
        public void Compatibility_TableRowsAreTheMale(Personality male, Personality female, Compatibility expected)
        {
            Assert.That(PersonalityTraits.CompatibilityOf(male, female), Is.EqualTo(expected));
        }

        [Test]
        public void Compatibility_Effects()
        {
            Assert.That(PersonalityTraits.MatingSuccessMultiplier(Compatibility.Good), Is.EqualTo(1.2d));
            Assert.That(PersonalityTraits.MatingSuccessMultiplier(Compatibility.Bad), Is.EqualTo(0.6d));
            Assert.That(PersonalityTraits.ClutchDelta(Compatibility.Good), Is.EqualTo(1));
            Assert.That(PersonalityTraits.ClutchDelta(Compatibility.Bad), Is.EqualTo(-1));
            Assert.That(PersonalityTraits.ClutchDelta(Compatibility.Normal), Is.EqualTo(0));
        }

        [Test]
        public void Inherit_TakesEachParentAboutAFifthOfTheTimePlusRandomShare()
        {
            var random = new Random(11);
            var fromMother = 0;
            const int count = 5000;
            for (var i = 0; i < count; i++)
            {
                if (PersonalityTraits.Inherit(Personality.Shy, Personality.Bold, random) == Personality.Shy)
                {
                    fromMother++;
                }
            }

            // 20% inherited + 60% random × 1/5.
            Assert.That(fromMother / (double)count, Is.EqualTo(0.32d).Within(0.025d));
        }

        [Test]
        public void BehaviourTuning_ScalesThreatWalkAndHiding()
        {
            var baseline = new PetBehaviourTuning();

            var calm = PersonalityTraits.BehaviourTuningFor(baseline, Personality.Calm);
            var shy = PersonalityTraits.BehaviourTuningFor(baseline, Personality.Shy);
            var curious = PersonalityTraits.BehaviourTuningFor(baseline, Personality.Curious);
            var glutton = PersonalityTraits.BehaviourTuningFor(baseline, Personality.Glutton);

            Assert.That(calm.WakeStartleChance, Is.EqualTo(baseline.WakeStartleChance * 0.5d).Within(1e-9));
            Assert.That(calm.ThreatTapCount, Is.EqualTo(8));
            Assert.That(shy.ThreatTapCount, Is.EqualTo(3));
            Assert.That(shy.DaySleepMaxSeconds, Is.EqualTo(baseline.DaySleepMaxSeconds * 1.5f).Within(1e-4));
            Assert.That(curious.NormalIdleMaxSeconds, Is.EqualTo(baseline.NormalIdleMaxSeconds / 1.3f).Within(1e-4));
            Assert.That(glutton.PreStrikeTailWagSeconds, Is.EqualTo(baseline.PreStrikeTailWagSeconds * 0.5f).Within(1e-4));
            Assert.That(baseline.ThreatTapCount, Is.EqualTo(4), "the shared tuning must not change");
        }

        [Test]
        public void FeedGain_UsesTheWeightGainMultiplier()
        {
            var calm = new PetState { WeightGrams = 10d, Personality = Personality.Calm };
            var glutton = new PetState { WeightGrams = 10d, Personality = Personality.Glutton };

            Assert.That(GrowthModel.FeedGain(glutton, care), Is.EqualTo(GrowthModel.FeedGain(calm, care) * 1.2d).Within(1e-9));
        }

        [Test]
        public void PreShedFast_IsLongerForShyAndShorterForGlutton()
        {
            // 2.5 game days before the shed: inside a shy (3 days) window, outside calm (2).
            var shedIn = GameCalendar.RealTimeFor(2.5d);
            var shy = new PetState { Personality = Personality.Shy, NextShedAtUtc = Now + shedIn };
            var calm = new PetState { Personality = Personality.Calm, NextShedAtUtc = Now + shedIn };
            // 1.5 game days before: inside calm (2), outside glutton (1).
            var glutton = new PetState { Personality = Personality.Glutton, NextShedAtUtc = Now + GameCalendar.RealTimeFor(1.5d) };

            Assert.That(AppetiteModel.Evaluate(shy, Now, care), Is.EqualTo(AppetiteState.PreShed));
            Assert.That(AppetiteModel.Evaluate(calm, Now, care), Is.EqualTo(AppetiteState.Normal));
            Assert.That(AppetiteModel.Evaluate(glutton, Now, care), Is.EqualTo(AppetiteState.Normal));
        }

        [Test]
        public void PreGrowthFast_IsScaledByPersonality()
        {
            var shy = new PetState
            {
                Personality = Personality.Shy,
                WeightGrams = 15.2d,
                HatchedAtUtc = Now.AddDays(-2d),
                LastSavedAtUtc = Now,
                NextShedAtUtc = Now + TimeSpan.FromDays(3),
            };

            new OfflineProgressCalculator(care).Apply(shy, Now, Now + TimeSpan.FromMinutes(1));

            Assert.That(shy.StageUpDueAtUtc, Is.EqualTo(Now + TimeSpan.FromMinutes(1) + GameCalendar.RealTimeFor(care.PreGrowthFastGameDays * 1.5d)));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.PersonalityTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/Personality.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>One per animal (§5.2).</summary>
    public enum Personality
    {
        Calm,
        Shy,
        Curious,
        Bold,
        Glutton
    }

    public enum Compatibility
    {
        Good,
        Normal,
        Bad
    }

    /// <summary>The effects of each personality (§5.2). Pure data so it is EditMode tested.</summary>
    public static class PersonalityTraits
    {
        public static readonly Personality[] All = (Personality[])Enum.GetValues(typeof(Personality));

        /// <summary>Chance of taking each parent's personality at hatching; the rest is random.</summary>
        public const double InheritChance = 0.2d;

        private const Compatibility G = Compatibility.Good;
        private const Compatibility N = Compatibility.Normal;
        private const Compatibility B = Compatibility.Bad;

        // Rows: male; columns: female. Order: Calm, Shy, Curious, Bold, Glutton.
        private static readonly Compatibility[,] CompatibilityTable =
        {
            { G, G, G, G, G },
            { G, N, N, B, N },
            { G, N, G, N, G },
            { G, B, N, B, N },
            { G, N, G, N, N },
        };

        public static string Label(Personality personality)
        {
            switch (personality)
            {
                case Personality.Shy:
                    return "臆病";
                case Personality.Curious:
                    return "好奇心旺盛";
                case Personality.Bold:
                    return "気が強い";
                case Personality.Glutton:
                    return "食いしん坊";
                default:
                    return "おっとり";
            }
        }

        public static double ThreatMultiplier(Personality p) =>
            p == Personality.Calm ? 0.5d : p == Personality.Shy || p == Personality.Bold ? 1.5d : 1d;

        public static double WalkFrequencyMultiplier(Personality p) => p == Personality.Curious ? 1.3d : 1d;

        /// <summary>How much longer it stays tucked by its shelter (sleep lengths).</summary>
        public static double HideMultiplier(Personality p) => p == Personality.Shy ? 1.5d : 1d;

        /// <summary>Length of food-refusal periods (pre-shed and pre-growth).</summary>
        public static double FastMultiplier(Personality p) =>
            p == Personality.Shy ? 1.5d : p == Personality.Glutton ? 0.5d : 1d;

        public static double WeightGainMultiplier(Personality p) =>
            p == Personality.Glutton ? 1.2d : p == Personality.Bold ? 1.1d : 1d;

        public static double PriceMultiplier(Personality p)
        {
            switch (p)
            {
                case Personality.Calm:
                    return 1.1d;
                case Personality.Shy:
                    return 0.9d;
                case Personality.Curious:
                    return 1.05d;
                default:
                    return 1d;
            }
        }

        public static bool IsEagerEater(Personality p) => p == Personality.Glutton;

        public static Compatibility CompatibilityOf(Personality male, Personality female) =>
            CompatibilityTable[(int)male, (int)female];

        public static double MatingSuccessMultiplier(Compatibility c) =>
            c == Compatibility.Good ? 1.2d : c == Compatibility.Bad ? 0.6d : 1d;

        public static int ClutchDelta(Compatibility c) =>
            c == Compatibility.Good ? 1 : c == Compatibility.Bad ? -1 : 0;

        public static Personality Inherit(Personality mother, Personality father, Random random)
        {
            var roll = random.NextDouble();
            if (roll < InheritChance)
            {
                return mother;
            }

            if (roll < InheritChance * 2d)
            {
                return father;
            }

            return Roll(random);
        }

        public static Personality Roll(Random random) => All[random.Next(All.Length)];

        /// <summary>A copy of the shared behaviour tuning with this personality's effects applied.</summary>
        public static PetBehaviourTuning BehaviourTuningFor(PetBehaviourTuning baseline, Personality p)
        {
            var tuning = baseline.Clone();
            var threat = ThreatMultiplier(p);
            tuning.WakeStartleChance = Math.Min(1d, baseline.WakeStartleChance * threat);
            tuning.ThreatTapCount = Math.Max(2, (int)Math.Round(baseline.ThreatTapCount / threat));

            var walk = (float)WalkFrequencyMultiplier(p);
            tuning.LivelyIdleMinSeconds = baseline.LivelyIdleMinSeconds / walk;
            tuning.LivelyIdleMaxSeconds = baseline.LivelyIdleMaxSeconds / walk;
            tuning.NormalIdleMinSeconds = baseline.NormalIdleMinSeconds / walk;
            tuning.NormalIdleMaxSeconds = baseline.NormalIdleMaxSeconds / walk;
            tuning.SluggishIdleMinSeconds = baseline.SluggishIdleMinSeconds / walk;
            tuning.SluggishIdleMaxSeconds = baseline.SluggishIdleMaxSeconds / walk;

            var hide = (float)HideMultiplier(p);
            tuning.MorningSleepMinSeconds = baseline.MorningSleepMinSeconds * hide;
            tuning.MorningSleepMaxSeconds = baseline.MorningSleepMaxSeconds * hide;
            tuning.DaySleepMinSeconds = baseline.DaySleepMinSeconds * hide;
            tuning.DaySleepMaxSeconds = baseline.DaySleepMaxSeconds * hide;
            tuning.NightSleepMinSeconds = baseline.NightSleepMinSeconds * hide;
            tuning.NightSleepMaxSeconds = baseline.NightSleepMaxSeconds * hide;

            if (IsEagerEater(p))
            {
                // Strikes at food almost at once.
                tuning.PreStrikeTailWagSeconds = baseline.PreStrikeTailWagSeconds * 0.5f;
            }

            return tuning;
        }
    }
}
```

`PetBehaviourTuning.cs` のクラス末尾に：

```csharp
        /// <summary>A shallow copy (all members are value types) for per-animal adjustments.</summary>
        public PetBehaviourTuning Clone() => (PetBehaviourTuning)MemberwiseClone();
```

`PetState.cs`（`Sex` の下）に：

```csharp
        public Personality Personality { get; set; } = Personality.Calm;

        /// <summary>False for a bought animal until it has been kept a while (§5.2); own hatchlings know it.</summary>
        public bool PersonalityKnown { get; set; } = true;
```

`GrowthModel.FeedGain`：

```csharp
        public static double FeedGain(PetState pet, CareTuning tuning) =>
            tuning.FeedWeightGainGrams * Math.Max(0d, 1d - pet.WeightGrams / WeightCap(pet, tuning))
            * PersonalityTraits.WeightGainMultiplier(pet.Personality);
```

`AppetiteModel.Evaluate` の最初の条件：

```csharp
            var preShed = GameCalendar.RealTimeFor(tuning.PreShedGameDays * PersonalityTraits.FastMultiplier(state.Personality));
            if (nowUtc >= state.NextShedAtUtc - preShed)
```

`OfflineProgressCalculator.Apply` の成長前の拒食の開始：

```csharp
                    state.StageUpDueAtUtc = stepEndUtc + GameCalendar.RealTimeFor(
                        tuning.PreGrowthFastGameDays * PersonalityTraits.FastMultiplier(state.Personality));
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体
Expected: PASS（`PersonalityTests` 全件と既存テスト）。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Personality.cs* Assets/Scripts/Core/PetState.cs Assets/Scripts/Core/PetBehaviourTuning.cs Assets/Scripts/Core/GrowthModel.cs Assets/Scripts/Core/AppetiteModel.cs Assets/Scripts/Core/OfflineProgressCalculator.cs Assets/Tests/EditMode/PersonalityTests.cs*
git commit -m "Add personalities with behaviour, fasting and weight-gain effects"
```

---

### Task 4: 個体データへの組み込み・セーブ・移行・雌雄の判明

**Files:**
- Create: `Assets/Scripts/Core/StarterGenetics.cs`
- Modify: `Assets/Scripts/Core/PetState.cs`（`Genotype`・`Known`・`SexRevealed`、`SexKnown` を置き換え）
- Modify: `Assets/Scripts/Core/OfflineProgressCalculator.cs`・`OfflineProgressResult.cs`（脱皮で判明）
- Modify: `Assets/Scripts/Core/ColonySession.cs`（`ColonyTickReport.SexReveals`）
- Modify: `Assets/Scripts/Core/ColonySaveData.cs`・`ColonySaveService.cs`・`Colony.cs`
- Modify: `Assets/Scripts/UI/TerrariumView.cs`（デバッグの個体追加だけ。表示は Task 7）
- Test: `Assets/Tests/EditMode/ColonySaveServiceTests.cs`・`OfflineProgressCalculatorTests.cs`・`ColonySessionTests.cs` に追加、`SexKnown` に依存していた既存テストを直す

**Interfaces:**
- Consumes: Task 1〜3 の型
- Produces:
  - `PetState.Genotype`（既定 `Genotype.Normal()`）・`PetState.Known`（既定 `new KnownGenetics()`）・`PetState.SexRevealed`（保存される。既定 `false`）・`PetState.SexKnown => SexRevealed`
  - `StarterGenetics.HiddenHetChance = 0.1`・`StarterGenetics.Apply(PetState, Random)`（遺伝子型・`Known = KnownGenetics.Unknown()`・性格を設定）・`StarterGenetics.Showcase`（`List<(string Label, Genotype Genotype)>`、デバッグと画面キャプチャ用）
  - `OfflineProgressResult.SexRevealed`（bool）、`ColonyTickReport.SexReveals`（`List<PetState>`、`HasEvents` に含める）
  - `AnimalSaveData` の追加フィールド：`int genomeVersion; List<GeneSaveData> genes; double hypo; double tangerine; List<HetSaveData> hets; bool hetsUnknown; string personality; bool personalityKnown; bool sexRevealed;`、`GeneSaveData { string gene; int copies; }`、`HetSaveData { string gene; double probability; }`、`ColonySaveService.CurrentGenomeVersion = 1`

判明のルール：`OfflineProgressCalculator` で脱皮した step の最後に、`state.Stage != GrowthStage.Baby && !state.SexRevealed` なら `SexRevealed = true` にし、結果の `SexRevealed` を true にする（段階上げの脱皮でも同じ）。

読み込みのルール：`genomeVersion < 1`（段階1のセーブ）の個体には `StarterGenetics.Apply` を使い、`sexRevealed` は保存値（無ければ false）を使う。`personality` が読めないときは `PersonalityTraits.Roll`。`FromSaveData` は `LoadOrCreate` の `random` を受け取るようにする。移行（スキーマ1・2）と `Colony.CreateNew` の最初の1匹にも `StarterGenetics.Apply` を使う。

- [ ] **Step 1: Write the failing tests**

`ColonySaveServiceTests.cs` に追加（既存のヘルパー・一時パスの書き方に合わせる）：

```csharp
        [Test]
        public void RoundTrip_KeepsGeneticsPersonalityAndSexReveal()
        {
            var colony = Colony.CreateNew(Now, economy, care, new Random(1));
            var pet = colony.Animals[0];
            pet.Genotype = Genotype.Normal(hypo: 72d, tangerine: 15d).Set(GeneId.MackSnow, 2).Set(GeneId.Eclipse, 1);
            pet.Known = new KnownGenetics().SetHet(GeneId.Eclipse, 2d / 3d);
            pet.Personality = Personality.Glutton;
            pet.PersonalityKnown = false;
            pet.SexRevealed = true;

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2)).Animals[0];

            Assert.That(loaded.Genotype.Copies(GeneId.MackSnow), Is.EqualTo(2));
            Assert.That(loaded.Genotype.Copies(GeneId.Eclipse), Is.EqualTo(1));
            Assert.That(loaded.Genotype.Hypo, Is.EqualTo(72d));
            Assert.That(loaded.Known.HetProbability(GeneId.Eclipse), Is.EqualTo(2d / 3d).Within(1e-9));
            Assert.That(loaded.Known.HetsUnknown, Is.False);
            Assert.That(loaded.Personality, Is.EqualTo(Personality.Glutton));
            Assert.That(loaded.PersonalityKnown, Is.False);
            Assert.That(loaded.SexRevealed, Is.True);
        }

        [Test]
        public void Load_Phase1SchemaThreeFile_GetsStarterGeneticsOnceAndKeepsThem()
        {
            // A schema-3 file written by phase 1: no genome fields at all.
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"animals\":[{\"id\":1,\"name\":\"レオパ1\",\"sex\":\"Male\",\"weightGrams\":45.0,\"stage\":\"Adult\"," +
                "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100}]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}],\"rackCount\":1,\"incubatorCount\":1,\"nextAnimalId\":2,\"nextCageId\":2}");

            var first = service.LoadOrCreate(path, Now, new Random(3)).Animals[0];

            Assert.That(first.Known.HetsUnknown, Is.True);
            Assert.That(MorphNamer.VisualName(first.Genotype), Is.EqualTo("ノーマル"));
            Assert.That(first.SexRevealed, Is.False, "migrated animals keep an unknown sex until their next shed");

            var colony = service.LoadOrCreate(path, Now, new Random(3));
            service.Save(path, colony);
            var again = service.LoadOrCreate(path, Now, new Random(999)).Animals[0];

            Assert.That(again.Personality, Is.EqualTo(colony.Animals[0].Personality));
            Assert.That(again.Genotype.Hypo, Is.EqualTo(colony.Animals[0].Genotype.Hypo));
        }

        [Test]
        public void Migrate_LegacyPet_IsNormalHetsUnknownWithUnknownSex()
        {
            // Reuse the existing schema-2 fixture helper of this test class for an adult pet.
            WriteLegacySave(growthStage: "Adult");

            var pet = service.LoadOrCreate(path, Now, new Random(4)).Animals[0];

            Assert.That(MorphNamer.FullName(pet.Genotype, pet.Known), Is.EqualTo("ノーマル（ヘテロ不明）"));
            Assert.That(pet.SexRevealed, Is.False);
            Assert.That(pet.PersonalityKnown, Is.True);
        }

        [Test]
        public void CreateNew_StartingAnimalHasStarterGenetics()
        {
            var pet = Colony.CreateNew(Now, economy, care, new Random(5)).Animals[0];

            Assert.That(pet.Known.HetsUnknown, Is.True);
            Assert.That(pet.Genotype.Hypo, Is.InRange(10d, 40d));
            Assert.That(pet.Genotype.Tangerine, Is.InRange(10d, 40d));
        }
```

`WriteLegacySave` が無ければ、このクラスにある既存のスキーマ2の書き方（段階1の移行テスト）をヘルパーに切り出して使う。

`OfflineProgressCalculatorTests.cs` に追加：

```csharp
        [Test]
        public void Shed_RevealsTheSexOfAJuvenileOrOlder()
        {
            var state = new PetState
            {
                Stage = GrowthStage.Juvenile,
                WeightGrams = 20d,
                HatchedAtUtc = Now.AddDays(-5d),
                LastSavedAtUtc = Now,
                NextShedAtUtc = Now + TimeSpan.FromMinutes(3),
            };

            var result = new OfflineProgressCalculator(new CareTuning()).Apply(state, Now, Now + TimeSpan.FromMinutes(10));

            Assert.That(result.SexRevealed, Is.True);
            Assert.That(state.SexKnown, Is.True);
        }

        [Test]
        public void Shed_OfABaby_KeepsTheSexHidden()
        {
            var state = new PetState
            {
                WeightGrams = 5d,
                HatchedAtUtc = Now.AddDays(-1d),
                LastSavedAtUtc = Now,
                NextShedAtUtc = Now + TimeSpan.FromMinutes(3),
            };

            var result = new OfflineProgressCalculator(new CareTuning()).Apply(state, Now, Now + TimeSpan.FromMinutes(10));

            Assert.That(result.ShedCount, Is.EqualTo(1));
            Assert.That(result.SexRevealed, Is.False);
            Assert.That(state.SexKnown, Is.False);
        }

        [Test]
        public void GrowingIntoAJuvenile_RevealsTheSex()
        {
            var tuning = new CareTuning();
            var state = new PetState
            {
                WeightGrams = 15.2d,
                HatchedAtUtc = Now.AddDays(-2d),
                LastSavedAtUtc = Now,
                NextShedAtUtc = Now + TimeSpan.FromDays(3),
                StageUpDueAtUtc = Now + TimeSpan.FromMinutes(2),
            };

            var result = new OfflineProgressCalculator(tuning).Apply(state, Now, Now + TimeSpan.FromMinutes(5));

            Assert.That(result.NewGrowthStage, Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(result.SexRevealed, Is.True);
        }
```

`ColonySessionTests.cs` に、判明が `ColonyTickReport.SexReveals` に入り `HasEvents` が true になるテストを1件追加する（既存の `Advance` のテストの形に合わせる）。

`Now` は各テストクラスの既存の定数を使う。既存テストで `Stage = Juvenile/Adult` にして `SexKnown` が true であることに頼っているもの（体重の上限など）は、`SexRevealed = true` を足して直す。

- [ ] **Step 2: Run the tests to verify they fail**

Run: EditMode 全体
Expected: コンパイルエラー（`SexRevealed`・`Genotype` などが無い）。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/StarterGenetics.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Genes for animals whose history is unknown (the starting animal, migrated saves):
    /// normal-looking, random hypo/tangerine, and a small chance of a hidden het per
    /// recessive gene. The player sees 「ノーマル（ヘテロ不明）」.
    /// </summary>
    public static class StarterGenetics
    {
        public const double HiddenHetChance = 0.1d;
        public const double MinPolygenic = 10d;
        public const double MaxPolygenic = 40d;

        public static void Apply(PetState pet, Random random)
        {
            var genotype = Genotype.Normal(
                MinPolygenic + random.NextDouble() * (MaxPolygenic - MinPolygenic),
                MinPolygenic + random.NextDouble() * (MaxPolygenic - MinPolygenic));
            foreach (var gene in Genes.All)
            {
                var roll = random.NextDouble();
                if (Genes.IsRecessive(gene) && roll < HiddenHetChance)
                {
                    genotype.Set(gene, 1);
                }
            }

            pet.Genotype = genotype;
            pet.Known = KnownGenetics.Unknown();
            pet.Personality = PersonalityTraits.Roll(random);
            pet.PersonalityKnown = true;
        }

        /// <summary>One of each look, for the debug menu and screen captures.</summary>
        public static readonly List<(string Label, Genotype Genotype)> Showcase = new List<(string, Genotype)>
        {
            ("ノーマル", Genotype.Normal()),
            ("トレンパーアルビノ", Genotype.Normal().Set(GeneId.TremperAlbino, 2)),
            ("ベルアルビノ", Genotype.Normal().Set(GeneId.BellAlbino, 2)),
            ("レインウォーターアルビノ", Genotype.Normal().Set(GeneId.RainwaterAlbino, 2)),
            ("エクリプス", Genotype.Normal().Set(GeneId.Eclipse, 2)),
            ("ブリザード", Genotype.Normal().Set(GeneId.Blizzard, 2)),
            ("マーフィーパターンレス", Genotype.Normal().Set(GeneId.MurphyPatternless, 2)),
            ("マックスノー", Genotype.Normal().Set(GeneId.MackSnow, 1)),
            ("スーパースノー", Genotype.Normal().Set(GeneId.MackSnow, 2)),
            ("ホワイト&イエロー", Genotype.Normal().Set(GeneId.WhiteAndYellow, 1)),
            ("ブレイジングブリザード", Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2)),
            ("レイプター", Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2)),
            ("ハイポ タンジェリン", Genotype.Normal(hypo: 85d, tangerine: 90d)),
        };
    }
}
```

`PetState.cs`：`public bool SexKnown => Stage != GrowthStage.Baby;` を次に置き換え、遺伝のプロパティを足す：

```csharp
        /// <summary>Stored: set when a juvenile-or-older animal sheds (§5.1 "known at juvenile").</summary>
        public bool SexRevealed { get; set; }

        public bool SexKnown => SexRevealed;

        /// <summary>True genes; hidden from the player except what shows.</summary>
        public Genotype Genotype { get; set; } = Genotype.Normal();

        /// <summary>What the player knows about this animal's hets.</summary>
        public KnownGenetics Known { get; set; } = new KnownGenetics();
```

`OfflineProgressResult` にコンストラクタ引数 `bool sexRevealed = false` とプロパティ `public bool SexRevealed { get; }` を足す。`OfflineProgressCalculator.Apply` の `if (shedNow) { ... }` の中に：

```csharp
                    if (state.Stage != GrowthStage.Baby && !state.SexRevealed)
                    {
                        state.SexRevealed = true;
                        sexRevealed = true;
                    }
```

（`var sexRevealed = false;` をループの前に宣言し、結果に渡す。）

`ColonySession`：`ColonyTickReport` に `public List<PetState> SexReveals { get; } = new List<PetState>();` を足して `HasEvents` に `|| SexReveals.Count > 0` を加え、各個体の `Apply` の結果が `SexRevealed` なら `report.SexReveals.Add(pet)`。

`ColonySaveData.cs`：`AnimalSaveData` に上記のフィールドを足し、次のクラスを追加：

```csharp
    [Serializable]
    public sealed class GeneSaveData
    {
        public string gene;
        public int copies;
    }

    [Serializable]
    public sealed class HetSaveData
    {
        public string gene;
        public double probability;
    }
```

`ColonySaveService.cs`：
- `public const int CurrentGenomeVersion = 1;`
- 保存：`genomeVersion = CurrentGenomeVersion`、`genes` はコピー数が1以上の遺伝子だけ（`gene = id.ToString()`）、`hypo`・`tangerine`、`hets` は確率が0より大きい遺伝子だけ、`hetsUnknown`、`personality = a.Personality.ToString()`、`personalityKnown`、`sexRevealed`。
- 読み込み：`FromSaveData(data, nowUtc, random)`。`a.genomeVersion >= 1` なら `genes`・`hets`（`Enum.TryParse` できないものは飛ばす。リストが null でもよい）・`hypo`・`tangerine`・`hetsUnknown` から組み立て、`personality` が `Enum.TryParse` できなければ `PersonalityTraits.Roll(random)`、`personalityKnown` をそのまま使う。`a.genomeVersion < 1` なら `StarterGenetics.Apply(pet, random)`。どちらの場合も `SexRevealed = a.sexRevealed`。
- 移行（`MigrateLegacy`）：作った `PetState` に `StarterGenetics.Apply(pet, random)`、`SexRevealed = false`。
- `Colony.CreateNew`：最初の1匹に `StarterGenetics.Apply(pet, random)`。

`TerrariumView.OnDebugAddPetClicked`：新しい個体に展示用のモルフを順番に割り当てる：

```csharp
            var showcase = StarterGenetics.Showcase[(session.Colony.NextAnimalId - 1) % StarterGenetics.Showcase.Count];
            var random = new System.Random();
            // ... in the PetState initializer:
                Genotype = showcase.Genotype.Clone(),
                Personality = PersonalityTraits.Roll(random),
```

（`Sex` の乱数も `random` に揃えてよい。）

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体、PlayMode 全体
Expected: PASS。PlayMode で雌雄表示に頼っているものがあれば、`SexRevealed` を設定するよう直す。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core Assets/Scripts/UI/TerrariumView.cs Assets/Tests
git commit -m "Store genes, personality and sex reveal per animal; reveal sex on a juvenile shed"
```

---

### Task 5: モルフの色（純粋なパレット）と脱皮前の色

**Files:**
- Create: `Assets/Scripts/Core/Rgb.cs`
- Create: `Assets/Scripts/Core/MorphAppearance.cs`
- Test: `Assets/Tests/EditMode/MorphAppearanceTests.cs`

**Interfaces:**
- Consumes: `Genotype`・`GeneId`・`GrowthStage`
- Produces: `Rgb`（`R, G, B` byte・`Lerp(Rgb, Rgb, double)`・値の等価）、`PaletteRole`（`Outline, Base, Light, Shade, Belly, BellyShade, Spot, Band, Eye, EyeHighlight, Mouth, Tongue, LegFar`）、`MorphPalette`（`this[PaletteRole]`・`Key`）、`MorphAppearance.Normal`・`MorphAppearance.PaletteFor(Genotype, GrowthStage) → MorphPalette`・`MorphAppearance.PreShed(Rgb, PaletteRole) → Rgb`

基準の色は `tools/sprites/gecko_sprites.py` の定数（OUTLINE〜LEG_FAR）と同じ値。変換は次の順に重ねる。「体の色」は `Base, Light, Shade, LegFar`。

| 順 | 条件 | 変換 |
|---|---|---|
| 1 | タンジェリン | t = clamp((値−30)/70, 0, 1)。体の色を橙色へ t だけ寄せる：Base→(250,130,40)、Light→(252,165,80)、Shade→(225,100,30)、LegFar→(205,95,30) |
| 2 | ホワイト&イエロー | 体の色を (250,246,230) へ 0.35 寄せる。Belly→(255,255,255) |
| 3 | マックスノー 1つ | 体の色を (238,236,220) へ 0.7 寄せる |
| 3 | スーパースノー | Base (242,242,238)、Light (252,252,250)、Shade (214,214,212)、LegFar (200,200,198)、Spot・Band (30,30,34)、Eye (20,20,22)、EyeHighlight＝Eye |
| 4 | ハイポ70以上 | Spot＝Base（体の斑点が消える。尾の帯は残る） |
| 5 | マーフィー（ヤング以上） | 体の色を (205,170,120) へ 0.5 寄せたあと、Spot＝Base、Band＝Shade |
| 6 | ブリザード | Base (205,205,210)、Light (228,228,232)、Shade (180,180,188)、LegFar (170,170,178)、Spot＝Base、Band＝Shade |
| 7 | アルビノ（3系統のどれか） | Spot が Base と違えば (196,140,112)、Band が Shade と違えば (206,158,128)。Eye (190,50,60) |
| 8 | エクリプス | アルビノでなければ Eye (12,12,14)。EyeHighlight＝Eye |

`Key` はパレットの13色を16進でつないだ文字列（同じ色なら同じキー）。

脱皮前：`c + (白 − c) × k` を成分ごとに `Math.Round`（偶数丸め。Python の `round` と同じ）。白は (236,236,242)、k は Outline と Eye で 0.3、それ以外 0.5（Python の `pre_shed` と同じ）。

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/MorphAppearanceTests.cs`:

```csharp
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class MorphAppearanceTests
    {
        private static MorphPalette For(Genotype genotype, GrowthStage stage = GrowthStage.Adult) =>
            MorphAppearance.PaletteFor(genotype, stage);

        [Test]
        public void Normal_MatchesTheSpriteGenerator()
        {
            Assert.That(MorphAppearance.Normal[PaletteRole.Outline], Is.EqualTo(new Rgb(58, 36, 26)));
            Assert.That(MorphAppearance.Normal[PaletteRole.Base], Is.EqualTo(new Rgb(246, 178, 58)));
            Assert.That(MorphAppearance.Normal[PaletteRole.Spot], Is.EqualTo(new Rgb(70, 44, 30)));
            Assert.That(MorphAppearance.Normal[PaletteRole.Eye], Is.EqualTo(new Rgb(30, 22, 18)));
            Assert.That(MorphAppearance.Normal[PaletteRole.LegFar], Is.EqualTo(new Rgb(206, 132, 44)));
        }

        [Test]
        public void ANormalLookingGenotype_KeepsTheNormalColours()
        {
            var het = Genotype.Normal(hypo: 30d, tangerine: 30d).Set(GeneId.Blizzard, 1);

            Assert.That(For(het).Key, Is.EqualTo(MorphAppearance.Normal.Key));
        }

        [Test]
        public void Albino_HasRedEyesAndPaleSpots()
        {
            var palette = For(Genotype.Normal().Set(GeneId.BellAlbino, 2));

            Assert.That(palette[PaletteRole.Eye], Is.EqualTo(new Rgb(190, 50, 60)));
            Assert.That(palette[PaletteRole.Spot], Is.EqualTo(new Rgb(196, 140, 112)));
            Assert.That(palette[PaletteRole.EyeHighlight], Is.EqualTo(new Rgb(255, 255, 255)));
        }

        [Test]
        public void Eclipse_HasSolidBlackEyes()
        {
            var palette = For(Genotype.Normal().Set(GeneId.Eclipse, 2));

            Assert.That(palette[PaletteRole.Eye], Is.EqualTo(new Rgb(12, 12, 14)));
            Assert.That(palette[PaletteRole.EyeHighlight], Is.EqualTo(palette[PaletteRole.Eye]));
        }

        [Test]
        public void Raptor_HasSolidRedEyes()
        {
            var palette = For(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2));

            Assert.That(palette[PaletteRole.Eye], Is.EqualTo(new Rgb(190, 50, 60)));
            Assert.That(palette[PaletteRole.EyeHighlight], Is.EqualTo(palette[PaletteRole.Eye]));
        }

        [Test]
        public void Blizzard_HasNoPattern()
        {
            var palette = For(Genotype.Normal().Set(GeneId.Blizzard, 2));

            Assert.That(palette[PaletteRole.Spot], Is.EqualTo(palette[PaletteRole.Base]));
            Assert.That(palette[PaletteRole.Band], Is.EqualTo(palette[PaletteRole.Shade]));
        }

        [Test]
        public void BlazingBlizzard_StaysPatternlessWithRedEyes()
        {
            var palette = For(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2));

            Assert.That(palette[PaletteRole.Spot], Is.EqualTo(palette[PaletteRole.Base]));
            Assert.That(palette[PaletteRole.Eye], Is.EqualTo(new Rgb(190, 50, 60)));
        }

        [Test]
        public void SuperSnow_IsWhiteWithBlackSpotsAndEyes()
        {
            var palette = For(Genotype.Normal().Set(GeneId.MackSnow, 2));

            Assert.That(palette[PaletteRole.Base], Is.EqualTo(new Rgb(242, 242, 238)));
            Assert.That(palette[PaletteRole.Spot], Is.EqualTo(new Rgb(30, 30, 34)));
            Assert.That(palette[PaletteRole.EyeHighlight], Is.EqualTo(palette[PaletteRole.Eye]));
        }

        [Test]
        public void MurphyPatternless_LosesSpotsOnlyOnceGrown()
        {
            var murphy = Genotype.Normal().Set(GeneId.MurphyPatternless, 2);

            Assert.That(For(murphy, GrowthStage.Baby)[PaletteRole.Spot], Is.EqualTo(MorphAppearance.Normal[PaletteRole.Spot]));
            var grown = For(murphy, GrowthStage.Juvenile);
            Assert.That(grown[PaletteRole.Spot], Is.EqualTo(grown[PaletteRole.Base]));
        }

        [Test]
        public void Hypo_RemovesBodySpotsButKeepsTailBands()
        {
            var palette = For(Genotype.Normal(hypo: 70d));

            Assert.That(palette[PaletteRole.Spot], Is.EqualTo(palette[PaletteRole.Base]));
            Assert.That(palette[PaletteRole.Band], Is.EqualTo(MorphAppearance.Normal[PaletteRole.Band]));
        }

        [Test]
        public void Tangerine_ShiftsTheBodyTowardsOrange()
        {
            var palette = For(Genotype.Normal(tangerine: 100d));

            Assert.That(palette[PaletteRole.Base], Is.EqualTo(new Rgb(250, 130, 40)));
        }

        [Test]
        public void MackSnowAndWhiteAndYellow_LightenTheBody()
        {
            var normalBase = MorphAppearance.Normal[PaletteRole.Base];

            Assert.That(For(Genotype.Normal().Set(GeneId.MackSnow, 1))[PaletteRole.Base].B, Is.GreaterThan(normalBase.B));
            Assert.That(For(Genotype.Normal().Set(GeneId.WhiteAndYellow, 1))[PaletteRole.Belly], Is.EqualTo(new Rgb(255, 255, 255)));
        }

        [Test]
        public void Key_IsEqualForEqualInputsAndDiffersBetweenMorphs()
        {
            var a = For(Genotype.Normal().Set(GeneId.Eclipse, 2));
            var b = For(Genotype.Normal().Set(GeneId.Eclipse, 2));

            Assert.That(a.Key, Is.EqualTo(b.Key));
            Assert.That(a.Key, Is.Not.EqualTo(MorphAppearance.Normal.Key));
        }

        [Test]
        public void PreShed_MatchesThePythonGenerator()
        {
            Assert.That(MorphAppearance.PreShed(new Rgb(58, 36, 26), PaletteRole.Outline), Is.EqualTo(new Rgb(111, 96, 91)));
            Assert.That(MorphAppearance.PreShed(new Rgb(246, 178, 58), PaletteRole.Base), Is.EqualTo(new Rgb(241, 207, 150)));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.MorphAppearanceTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/Rgb.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>An opaque colour without Unity types, so palettes are EditMode tested.</summary>
    public readonly struct Rgb : IEquatable<Rgb>
    {
        public Rgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public Rgb(int r, int g, int b) : this((byte)r, (byte)g, (byte)b)
        {
        }

        public byte R { get; }

        public byte G { get; }

        public byte B { get; }

        public static Rgb Lerp(Rgb from, Rgb to, double t) => new Rgb(
            (int)Math.Round(from.R + (to.R - from.R) * t),
            (int)Math.Round(from.G + (to.G - from.G) * t),
            (int)Math.Round(from.B + (to.B - from.B) * t));

        public bool Equals(Rgb other) => R == other.R && G == other.G && B == other.B;

        public override bool Equals(object obj) => obj is Rgb other && Equals(other);

        public override int GetHashCode() => (R << 16) | (G << 8) | B;

        public override string ToString() => $"{R:X2}{G:X2}{B:X2}";
    }
}
```

`Assets/Scripts/Core/MorphAppearance.cs`:

```csharp
using System;
using System.Text;

namespace TerrariumDays.Core
{
    /// <summary>The colour roles of the gecko sprites (tools/sprites/gecko_sprites.py).</summary>
    public enum PaletteRole
    {
        Outline,
        Base,
        Light,
        Shade,
        Belly,
        BellyShade,
        Spot,
        Band,
        Eye,
        EyeHighlight,
        Mouth,
        Tongue,
        LegFar
    }

    public sealed class MorphPalette
    {
        private readonly Rgb[] colors;

        public MorphPalette(Rgb[] colors)
        {
            this.colors = colors;
            var key = new StringBuilder();
            foreach (var color in colors)
            {
                key.Append(color.ToString());
            }

            Key = key.ToString();
        }

        public Rgb this[PaletteRole role] => colors[(int)role];

        /// <summary>Equal for equal colours: the sprite cache key.</summary>
        public string Key { get; }
    }

    /// <summary>How each morph recolours the base sprites (§4.5).</summary>
    public static class MorphAppearance
    {
        private static readonly Rgb PreShedWhite = new Rgb(236, 236, 242);
        private static readonly PaletteRole[] Body = { PaletteRole.Base, PaletteRole.Light, PaletteRole.Shade, PaletteRole.LegFar };

        public static readonly MorphPalette Normal = new MorphPalette(NormalColors());

        private static Rgb[] NormalColors() => new[]
        {
            new Rgb(58, 36, 26),    // Outline
            new Rgb(246, 178, 58),  // Base
            new Rgb(253, 206, 104), // Light
            new Rgb(222, 142, 46),  // Shade
            new Rgb(251, 238, 214), // Belly
            new Rgb(232, 212, 180), // BellyShade
            new Rgb(70, 44, 30),    // Spot
            new Rgb(84, 52, 34),    // Band
            new Rgb(30, 22, 18),    // Eye
            new Rgb(255, 255, 255), // EyeHighlight
            new Rgb(200, 70, 80),   // Mouth
            new Rgb(240, 110, 120), // Tongue
            new Rgb(206, 132, 44),  // LegFar
        };

        public static MorphPalette PaletteFor(Genotype g, GrowthStage stage)
        {
            var c = NormalColors();

            var tangerine = Math.Max(0d, Math.Min(1d, (g.Tangerine - 30d) / 70d));
            if (tangerine > 0d)
            {
                Toward(c, PaletteRole.Base, new Rgb(250, 130, 40), tangerine);
                Toward(c, PaletteRole.Light, new Rgb(252, 165, 80), tangerine);
                Toward(c, PaletteRole.Shade, new Rgb(225, 100, 30), tangerine);
                Toward(c, PaletteRole.LegFar, new Rgb(205, 95, 30), tangerine);
            }

            if (g.Shows(GeneId.WhiteAndYellow))
            {
                BodyToward(c, new Rgb(250, 246, 230), 0.35d);
                Set(c, PaletteRole.Belly, new Rgb(255, 255, 255));
            }

            if (g.Copies(GeneId.MackSnow) == 1)
            {
                BodyToward(c, new Rgb(238, 236, 220), 0.7d);
            }
            else if (g.Copies(GeneId.MackSnow) == 2)
            {
                Set(c, PaletteRole.Base, new Rgb(242, 242, 238));
                Set(c, PaletteRole.Light, new Rgb(252, 252, 250));
                Set(c, PaletteRole.Shade, new Rgb(214, 214, 212));
                Set(c, PaletteRole.LegFar, new Rgb(200, 200, 198));
                Set(c, PaletteRole.Spot, new Rgb(30, 30, 34));
                Set(c, PaletteRole.Band, new Rgb(30, 30, 34));
                Set(c, PaletteRole.Eye, new Rgb(20, 20, 22));
                Set(c, PaletteRole.EyeHighlight, c[(int)PaletteRole.Eye]);
            }

            if (g.Hypo >= MorphNamer.HypoNameThreshold)
            {
                Set(c, PaletteRole.Spot, c[(int)PaletteRole.Base]);
            }

            if (g.Shows(GeneId.MurphyPatternless) && stage != GrowthStage.Baby)
            {
                BodyToward(c, new Rgb(205, 170, 120), 0.5d);
                Set(c, PaletteRole.Spot, c[(int)PaletteRole.Base]);
                Set(c, PaletteRole.Band, c[(int)PaletteRole.Shade]);
            }

            if (g.Shows(GeneId.Blizzard))
            {
                Set(c, PaletteRole.Base, new Rgb(205, 205, 210));
                Set(c, PaletteRole.Light, new Rgb(228, 228, 232));
                Set(c, PaletteRole.Shade, new Rgb(180, 180, 188));
                Set(c, PaletteRole.LegFar, new Rgb(170, 170, 178));
                Set(c, PaletteRole.Spot, c[(int)PaletteRole.Base]);
                Set(c, PaletteRole.Band, c[(int)PaletteRole.Shade]);
            }

            var albino = g.Shows(GeneId.TremperAlbino) || g.Shows(GeneId.BellAlbino) || g.Shows(GeneId.RainwaterAlbino);
            if (albino)
            {
                if (!c[(int)PaletteRole.Spot].Equals(c[(int)PaletteRole.Base]))
                {
                    Set(c, PaletteRole.Spot, new Rgb(196, 140, 112));
                }

                if (!c[(int)PaletteRole.Band].Equals(c[(int)PaletteRole.Shade]))
                {
                    Set(c, PaletteRole.Band, new Rgb(206, 158, 128));
                }

                Set(c, PaletteRole.Eye, new Rgb(190, 50, 60));
            }

            if (g.Shows(GeneId.Eclipse))
            {
                if (!albino)
                {
                    Set(c, PaletteRole.Eye, new Rgb(12, 12, 14));
                }

                Set(c, PaletteRole.EyeHighlight, c[(int)PaletteRole.Eye]);
            }

            return new MorphPalette(c);
        }

        /// <summary>The milky pre-shed skin, as tools/sprites/gecko_sprites.py pre_shed() did.</summary>
        public static Rgb PreShed(Rgb color, PaletteRole role)
        {
            var k = role == PaletteRole.Outline || role == PaletteRole.Eye ? 0.3d : 0.5d;
            return Rgb.Lerp(color, PreShedWhite, k);
        }

        private static void Set(Rgb[] c, PaletteRole role, Rgb color) => c[(int)role] = color;

        private static void Toward(Rgb[] c, PaletteRole role, Rgb target, double t) =>
            c[(int)role] = Rgb.Lerp(c[(int)role], target, t);

        private static void BodyToward(Rgb[] c, Rgb target, double t)
        {
            foreach (var role in Body)
            {
                Toward(c, role, target, t);
            }
        }
    }
}
```

`Rgb.Lerp` は `Math.Round`（偶数丸め）なので、Python の `int(round(...))` と一致する。脱皮前の期待値 (111,96,91)・(241,207,150) はこの計算と一致する。

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Rgb.cs* Assets/Scripts/Core/MorphAppearance.cs* Assets/Tests/EditMode/MorphAppearanceTests.cs*
git commit -m "Add morph palettes and the pre-shed colour rule"
```

---

### Task 6: スプライトの色の置き換えとモルフごとのキャッシュ

**Files:**
- Modify: `Assets/Resources/Gecko/*.png.meta`（`isReadable: 0` → `isReadable: 1`、全62ファイル）
- Create: `Assets/Scripts/UI/MorphRecolor.cs`
- Create: `Assets/Scripts/UI/MorphSprites.cs`
- Modify: `Assets/Scripts/UI/PetSpriteLibrary.cs`
- Delete: `Assets/Resources/GeckoPreShed/`（フォルダと .meta）
- Modify: `tools/sprites/gecko_sprites.py`（`pre_shed`・`PRE_SHED_WHITE`・GeckoPreShed への書き出しを削除し、「脱皮前の色は `MorphAppearance.PreShed` で実行時に作る」とコメント）
- Test: `Assets/Tests/EditMode/MorphRecolorTests.cs`

**Interfaces:**
- Consumes: Task 5 の `MorphAppearance`・`MorphPalette`・`PaletteRole`・`Rgb`、Task 4 の `PetState.Genotype`・`Stage`
- Produces:
  - `MorphRecolor.Apply(Texture2D source, MorphPalette palette, bool preShed) → Texture2D`（不透明で基準パレットの色の画素だけ置き換える。脱皮前なら置き換え後に `MorphAppearance.PreShed`。基準パレットに無い色は、脱皮前なら k=0.5 で白へ寄せ、そうでなければそのまま。透明な画素はそのまま。出力は `FilterMode.Point`・`TextureWrapMode.Clamp`・同じ名前）
  - `PetSpriteLibrary(IEnumerable<Texture2D> geckoFrames, Texture2D heart, Texture2D zzz, MorphPalette palette = null)`（第4引数を置き換え。`palette` が null なら `MorphAppearance.Normal`）・`PetSpriteLibrary.WithPalette(MorphPalette) → PetSpriteLibrary`（元のフレームを共有する）・`Frame(clip, index, preShed)`・`Frame(clip, index)`・`FrameCount(clip)` は今と同じ。フレームは初めて要求されたときに作ってキャッシュする。通常色で脱皮前でないフレームは元のテクスチャをそのまま返す。
  - `MorphSprites.For(PetState pet) → PetSpriteLibrary`（`MorphAppearance.PaletteFor(pet.Genotype, pet.Stage).Key` ごとにキャッシュ。基準のライブラリは `PetSpriteLibrary.LoadFromResources()` を1度だけ呼ぶ）・`MorphSprites.Base`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/MorphRecolorTests.cs`:

```csharp
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;
using UnityEngine;

namespace TerrariumDays.Tests
{
    public sealed class MorphRecolorTests
    {
        private static Texture2D Pixels(params Color32[] pixels)
        {
            var texture = new Texture2D(pixels.Length, 1, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Color32 C(Rgb c, byte a = 255) => new Color32(c.R, c.G, c.B, a);

        [Test]
        public void Apply_ReplacesPaletteColoursByRole()
        {
            var normal = MorphAppearance.Normal;
            var albino = MorphAppearance.PaletteFor(Genotype.Normal().Set(GeneId.TremperAlbino, 2), GrowthStage.Adult);
            var source = Pixels(C(normal[PaletteRole.Eye]), C(normal[PaletteRole.Spot]), C(normal[PaletteRole.Base]));

            var result = MorphRecolor.Apply(source, albino, preShed: false).GetPixels32();

            Assert.That(result[0], Is.EqualTo(C(albino[PaletteRole.Eye])));
            Assert.That(result[1], Is.EqualTo(C(albino[PaletteRole.Spot])));
            Assert.That(result[2], Is.EqualTo(C(albino[PaletteRole.Base])));
        }

        [Test]
        public void Apply_KeepsTransparentAndUnknownPixels()
        {
            var unknown = new Color32(1, 2, 3, 255);
            var source = Pixels(new Color32(0, 0, 0, 0), unknown);

            var result = MorphRecolor.Apply(source, MorphAppearance.Normal, preShed: false).GetPixels32();

            Assert.That(result[0].a, Is.EqualTo(0));
            Assert.That(result[1], Is.EqualTo(unknown));
        }

        [Test]
        public void Apply_PreShed_WhitensTheRecolouredPixel()
        {
            var normal = MorphAppearance.Normal;
            var source = Pixels(C(normal[PaletteRole.Outline]));

            var result = MorphRecolor.Apply(source, normal, preShed: true).GetPixels32();

            Assert.That(result[0], Is.EqualTo(C(new Rgb(111, 96, 91))));
        }

        [Test]
        public void GeckoSprites_UseOnlyPaletteColours()
        {
            // Guards the generator and the palette against drifting apart.
            var known = new System.Collections.Generic.HashSet<Rgb>();
            foreach (PaletteRole role in System.Enum.GetValues(typeof(PaletteRole)))
            {
                known.Add(MorphAppearance.Normal[role]);
            }

            foreach (var texture in Resources.LoadAll<Texture2D>("Gecko"))
            {
                Assert.That(texture.isReadable, Is.True, texture.name + " must be imported with Read/Write enabled");
                foreach (var pixel in texture.GetPixels32())
                {
                    if (pixel.a > 0)
                    {
                        Assert.That(known.Contains(new Rgb(pixel.r, pixel.g, pixel.b)), Is.True,
                            $"{texture.name} has a colour outside the palette: {pixel}");
                    }
                }
            }
        }

        [Test]
        public void Library_WithPalette_RecoloursFramesLazilyAndCaches()
        {
            var normal = MorphAppearance.Normal;
            var frame = Pixels(C(normal[PaletteRole.Base]));
            frame.name = "idle_00";
            var library = new PetSpriteLibrary(new[] { frame }, null, null);
            var blizzard = MorphAppearance.PaletteFor(Genotype.Normal().Set(GeneId.Blizzard, 2), GrowthStage.Adult);

            var recoloured = library.WithPalette(blizzard);

            Assert.That(library.Frame(TerrariumDays.Gameplay.PetClip.Idle, 0), Is.SameAs(frame));
            var first = recoloured.Frame(TerrariumDays.Gameplay.PetClip.Idle, 0);
            Assert.That(first.GetPixel(0, 0), Is.EqualTo((Color)C(blizzard[PaletteRole.Base])));
            Assert.That(recoloured.Frame(TerrariumDays.Gameplay.PetClip.Idle, 0), Is.SameAs(first));
            Assert.That(recoloured.Frame(TerrariumDays.Gameplay.PetClip.Idle, 0, preShed: true), Is.Not.SameAs(first));
        }
    }
}
```

注：`first.GetPixel` を読むので、`MorphRecolor.Apply` の出力は読み取り可能のままにする（`Apply(false, false)`）。フレーム名の接頭辞は `PetAnimation.ResourcePrefix(PetClip.Idle)` に合わせる（`idle` でなければテストの名前を直す）。

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.MorphRecolorTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

1. 読み取り可能にする：

```bash
sed -i '' 's/isReadable: 0/isReadable: 1/' Assets/Resources/Gecko/*.png.meta
grep -c "isReadable: 1" Assets/Resources/Gecko/*.png.meta | grep -v ":1$"   # 出力が空なら全ファイル変更済み
```

2. `Assets/Scripts/UI/MorphRecolor.cs`:

```csharp
using System.Collections.Generic;
using TerrariumDays.Core;
using UnityEngine;

namespace TerrariumDays.UI
{
    /// <summary>Recolours a gecko frame from the base palette to a morph palette (§4.5).</summary>
    public static class MorphRecolor
    {
        private static Dictionary<int, PaletteRole> roleByColor;

        public static Texture2D Apply(Texture2D source, MorphPalette palette, bool preShed)
        {
            var roles = RoleByColor();
            var pixels = source.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a == 0)
                {
                    continue;
                }

                Rgb color;
                if (roles.TryGetValue((p.r << 16) | (p.g << 8) | p.b, out var role))
                {
                    color = palette[role];
                    if (preShed)
                    {
                        color = MorphAppearance.PreShed(color, role);
                    }
                }
                else if (preShed)
                {
                    color = MorphAppearance.PreShed(new Rgb(p.r, p.g, p.b), PaletteRole.Base);
                }
                else
                {
                    continue;
                }

                pixels[i] = new Color32(color.R, color.G, color.B, p.a);
            }

            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = source.name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            result.SetPixels32(pixels);
            result.Apply(false, false);
            return result;
        }

        private static Dictionary<int, PaletteRole> RoleByColor()
        {
            if (roleByColor != null)
            {
                return roleByColor;
            }

            roleByColor = new Dictionary<int, PaletteRole>();
            foreach (PaletteRole role in System.Enum.GetValues(typeof(PaletteRole)))
            {
                var c = MorphAppearance.Normal[role];
                roleByColor[(c.R << 16) | (c.G << 8) | c.B] = role;
            }

            return roleByColor;
        }
    }
}
```

3. `PetSpriteLibrary.cs` を書き換える：
   - フィールド：`private readonly Dictionary<PetClip, Texture2D[]> frames;`（元のフレーム。`WithPalette` で共有）、`private readonly MorphPalette palette;`、`private readonly Dictionary<(PetClip, int, bool), Texture2D> cache = new Dictionary<(PetClip, int, bool), Texture2D>();`
   - コンストラクタ：`PetSpriteLibrary(IEnumerable<Texture2D> geckoFrames, Texture2D heart, Texture2D zzz, MorphPalette palette = null)` は `Group(geckoFrames)` を作って private コンストラクタ `PetSpriteLibrary(Dictionary<PetClip, Texture2D[]> frames, Texture2D heart, Texture2D zzz, MorphPalette palette)` に渡す。
   - `public PetSpriteLibrary WithPalette(MorphPalette palette) => new PetSpriteLibrary(frames, Heart, Zzz, palette);`
   - `Frame(clip, index, preShed)`：idle へのフォールバックと index の丸めは今と同じ規則で元のフレームを決め、`palette.Key == MorphAppearance.Normal.Key && !preShed` なら元のフレームを返す。それ以外は `cache` から、無ければ `MorphRecolor.Apply(source, palette, preShed)` を作って入れる。
   - `Frame(clip, index)` は `Frame(clip, index, false)`。
   - `LoadFromResources()` は `GeckoPreShed` を読まない。
4. `Assets/Scripts/UI/MorphSprites.cs`:

```csharp
using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>One sprite library per morph look, shared by the cage view and home thumbnails.</summary>
    public static class MorphSprites
    {
        private static readonly Dictionary<string, PetSpriteLibrary> byKey = new Dictionary<string, PetSpriteLibrary>();
        private static PetSpriteLibrary baseLibrary;

        public static PetSpriteLibrary Base => baseLibrary ?? (baseLibrary = PetSpriteLibrary.LoadFromResources());

        public static PetSpriteLibrary For(PetState pet)
        {
            var palette = MorphAppearance.PaletteFor(pet.Genotype, pet.Stage);
            if (!byKey.TryGetValue(palette.Key, out var library))
            {
                library = Base.WithPalette(palette);
                byKey[palette.Key] = library;
            }

            return library;
        }
    }
}
```

5. `Assets/Resources/GeckoPreShed/` とその .meta を `git rm -r` で削除し、Python を上記のとおり直す（`python3 tools/sprites/gecko_sprites.py /tmp/gecko-check` で動くことを確かめ、出力フォルダは捨てる）。
6. `PetSpriteLibrary` のコンストラクタの第4引数を使っていた既存のテスト（`PetActorTests` など）を直す。

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体、PlayMode 全体
Expected: PASS。`scripts/capture-screens.sh` で `Logs/Screens/01-idle.png` を作り、段階1のときと見た目が変わっていないこと（ノーマルの色・脱皮前でない）を目で確かめる。

- [ ] **Step 5: Commit**

```bash
git add -A Assets/Resources/Gecko Assets/Resources/GeckoPreShed* Assets/Scripts/UI Assets/Tests tools/sprites/gecko_sprites.py
git commit -m "Recolour gecko frames per morph at runtime and derive the pre-shed look"
```

---

### Task 7: 画面への組み込み（見た目・名前・性格・雌雄の判明）

**Files:**
- Modify: `Assets/Scripts/UI/TerrariumView.cs`
- Modify: `Assets/Scripts/UI/HomeView.cs`
- Modify: `Assets/Scripts/UI/CageStatusText.cs`
- Modify: `Assets/UI/Terrarium.uxml`・`Assets/UI/Terrarium.uss`
- Test: `Assets/Tests/PlayMode/TerrariumViewTests.cs`・`Assets/Tests/EditMode/ShellTests.cs`（`CageStatusText` のテストがある所）・`Assets/Tests/PlayMode/ScreenCaptureTests.cs`

**Interfaces:**
- Consumes: `MorphSprites.For`・`MorphNamer.FullName`・`PersonalityTraits.Label`・`PersonalityTraits.BehaviourTuningFor`・`ColonyTickReport.SexReveals`・`StarterGenetics.Showcase`
- Produces: `CageStatusText.ProfileFor(PetState) → string`（`"{FullName}・{性格}"`、性格が未判明なら「性格不明」）・`CageStatusText.SexRevealMessage(PetState) → string`（`"{名前}は♀メスでした"` / `"{名前}は♂オスでした"`）・UXML の `profile-label`

変更点：
1. `RebuildPetActor`：`new PetActor(petElement, PersonalityTraits.BehaviourTuningFor(petBehaviourTuning, state.Personality), artLayout, new System.Random(), MorphSprites.For(state), effectsLayerElement ?? terrariumViewElement)`。`state` が null のときは今の `petBehaviourTuning` と `MorphSprites.Base`。
2. `HandleReport`：`report.StageUps` に選択中の個体があれば `RebuildPetActor()`（マーフィーの斑点など、段階で色が変わるため）。`report.SexReveals` に選択中の個体があれば `ShowFeedback(CageStatusText.SexRevealMessage(state))`。脱皮の通知と重なるときは判明の通知を優先する（`ShedMessage` の後に呼べば上書きされる）。
3. `HomeView`・`CageListView`：サムネイルを `MorphSprites.For(pet).Frame(PetClip.Idle, 0)` にする（コンストラクタの `PetSpriteLibrary sprites` 引数は削除）。`CageListSignature` にモルフの `Key` と `SexKnown` を含め、判明や段階での色の変化で作り直されるようにする。
4. UXML：`growth-stage-label` の直後（`top-bar` の中）に `<ui:Label name="profile-label" text="" class="profile-label" />`。USS：`.profile-label { font-size: 18px; color: rgb(110, 84, 60); white-space: normal; }`。`Render` で `profileLabel.text = CageStatusText.ProfileFor(petState)`。長い名前（例：「ハイポ タンジェリン マックスノー レイプター ヘテロブリザード」）でも上のバーが画面からはみ出さず、ケージ移動の ◀▶ とデバッグボタンに重ならないこと（画面キャプチャで確かめる）。
5. `CageStatusText.TitleFor` は今のまま（`SexKnown` が `SexRevealed` を見るようになったので、判明前は「性別不明」）。

- [ ] **Step 1: Write the failing tests**

EditMode（`CageStatusText` のテストがあるファイル）：

```csharp
        [Test]
        public void ProfileFor_ShowsMorphAndPersonality()
        {
            var pet = new PetState
            {
                Genotype = Genotype.Normal().Set(GeneId.Eclipse, 2),
                Known = new KnownGenetics().SetHet(GeneId.TremperAlbino, 1d),
                Personality = Personality.Curious,
            };

            Assert.That(CageStatusText.ProfileFor(pet), Is.EqualTo("エクリプス ヘテロトレンパーアルビノ・好奇心旺盛"));
        }

        [Test]
        public void ProfileFor_HidesAnUnknownPersonality()
        {
            var pet = new PetState { Known = KnownGenetics.Unknown(), PersonalityKnown = false };

            Assert.That(CageStatusText.ProfileFor(pet), Is.EqualTo("ノーマル（ヘテロ不明）・性格不明"));
        }

        [Test]
        public void SexRevealMessage()
        {
            Assert.That(CageStatusText.SexRevealMessage(new PetState { Name = "レオパ2", Sex = Sex.Male }), Is.EqualTo("レオパ2は♂オスでした"));
            Assert.That(CageStatusText.SexRevealMessage(new PetState { Name = "レオパ3", Sex = Sex.Female }), Is.EqualTo("レオパ3は♀メスでした"));
        }
```

PlayMode（`TerrariumViewTests.cs`。既存のセットアップ・一時セーブの書き方に合わせる）：
- 新しいゲームのケージ詳細で `profile-label` の文字が `"ノーマル（ヘテロ不明）・"` で始まる。
- デバッグで個体を追加した（2匹目＝展示用の2番目、トレンパーアルビノ）ケージを選ぶと、`profile-label` が「トレンパーアルビノ」で始まり、ペットの要素の背景画像が1匹目の通常色のフレームと別のテクスチャになる。
- ヤングで未判明の個体に `session.SimulateGameTime` などで脱皮を起こすと、フィードバックの文字が `SexRevealMessage` と同じになり、ケージの見出しから「性別不明」が消える。

`ScreenCaptureTests.cs` に Explicit のテストを追加：

```csharp
        /// <summary>One detail screen per showcase morph (grown, so murphy shows) into Logs/Screens/morph-*.png.</summary>
        [UnityTest]
        public IEnumerator CaptureMorphGallery()
        {
            // Same scene/RenderTexture setup as CaptureTerrariumScreens (extract a shared helper).
            // Then: for each StarterGenetics.Showcase entry, add a cage (raise RackCount so there is
            // room), add an adult animal (Stage Adult, 50 g, SexRevealed true) with that genotype,
            // SelectCage + ShowCageDetail, wait 1 s, Capture(outputDir, $"morph-{i:00}").
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: EditMode・PlayMode
Expected: コンパイルエラーまたは FAIL。

- [ ] **Step 3: Write the implementation**

上の「変更点」1〜5のとおり。`ProfileFor`：

```csharp
        public static string ProfileFor(PetState pet)
        {
            var personality = pet.PersonalityKnown ? PersonalityTraits.Label(pet.Personality) : "性格不明";
            return $"{MorphNamer.FullName(pet.Genotype, pet.Known)}・{personality}";
        }

        public static string SexRevealMessage(PetState pet) =>
            $"{pet.Name}は{(pet.Sex == Sex.Female ? "♀メス" : "♂オス")}でした";
```

- [ ] **Step 4: Run the tests and captures**

Run: EditMode・PlayMode 全体 PASS。続けて：

```bash
scripts/capture-screens.sh
scripts/run-unity-tests.sh PlayMode -testFilter TerrariumDays.Tests.ScreenCaptureTests.CaptureMorphGallery
```

`Logs/Screens/morph-00.png`〜`morph-12.png` を開き、仕様 §4.1 の見た目（アルビノの赤い目と薄い模様、エクリプスの黒い目、ブリザードの無地、スーパースノーの白地に黒い斑点、マーフィーの斑点なし、W&Y とマックスノーの明るい体、タンジェリンの橙色）になっていること、プロフィール欄が上のバーに収まっていることを確かめる。`00-home.png`・`00-cage-list.png` のサムネイルも確かめる。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI Assets/UI Assets/Tests
git commit -m "Show morph looks, names, personality and sex reveals in the cage and home screens"
```

---

### Task 8: 文書の更新と最終確認

**Files:**
- Modify: `GAME.md`（遺伝・モルフ名・ポッシブルヘテロ・性格の効果・雌雄の判明・見た目の描き分けを1節ずつ短く）
- Modify: `CLAUDE.md`・`AGENTS.md`（`Resources/Gecko` は Read/Write 有効が必要で、色は `MorphAppearance` のパレットに一致させること、脱皮前の色は実行時に作ること、`GeckoPreShed` は廃止したこと。スプライト生成スクリプトの説明も合わせる）
- Modify: `.claude/skills/procedural-pixel-sprite-animation/SKILL.md` と `.agents/skills/` の同名ファイル（パレットの色を変えたら `MorphAppearance.Normal` と `MorphRecolorTests.GeckoSprites_UseOnlyPaletteColours` も直す、という1行）

- [ ] **Step 1:** 上の文書を更新する（段階1の記述と矛盾しないこと。「雌雄はヤングで判明」は「ヤング以上で脱皮したときに判明」に直す）。
- [ ] **Step 2:** EditMode・PlayMode 全体を実行して PASS、`test-summary.py` が「.meta will be ignored」を報告しないことを確かめる。
- [ ] **Step 3:** `scripts/capture-screens.sh` と `CaptureMorphGallery` を実行し、画像を確かめる。
- [ ] **Step 4:** Commit：

```bash
git add GAME.md CLAUDE.md AGENTS.md .claude/skills .agents/skills
git commit -m "Document genetics, personalities and runtime morph recolouring"
```

- [ ] **Step 5:** `scripts/build-ios.sh --run` で実機に入れる（段階1のセーブが残っている端末で、移行した個体が「ノーマル（ヘテロ不明）・<性格>」と表示されることを利用者に確かめてもらう）。
