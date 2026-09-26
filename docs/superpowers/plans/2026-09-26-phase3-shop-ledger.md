# 段階3：お金の台帳とショップ（相場・在庫・用品・卸売り・ケージごとの装飾・台帳タブ） 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 相場（§10.1 全部。イベントの需要は「イベントなし＝1.0」で呼ぶ）を計算し、ショップで生体（毎月4〜6匹）と用品（ケージ・ラック・装飾・産卵床・孵卵器）を買い、手持ちの個体をいつでも相場の40%で卸売りでき、そのすべてが台帳（お金の出入り）に残るようにする。装飾は成長による解放をやめて購入制・ケージごと（小型1／標準2／大型3枠）にし、既存のセーブの装飾は一度だけ所持品へ移す。台帳タブ（個体一覧・お金の出入り）を作る。

**Architecture:** 相場（`MarketPrice`・`EventDemand`）、所持品（`Inventory`）、装飾の枠（`DecorSlots`）、在庫（`ShopStock`・`ShopStockGenerator`）、売り買い（`ShopCatalog`・`ShopService`）、性格の判明（`PersonalityReveal`）はすべて Unity に依存しない Core の純粋なクラスにし、EditMode テストで確かめる。`ColonySession` が月替わりの入荷と性格の判明を時間の適用に組み込み、`ColonySaveService` がスキーマ3のまま新しいフィールドを保存・移行する。画面はショップ（`ShopView`）と台帳（`LedgerView`）のパネルを `ShellNavigator` のタブに加え、文字の組み立ては純粋な `ShopText`・`LedgerText` に分けてテストする。

**Tech Stack:** Unity 6000.5.10f1（UI Toolkit、JsonUtility、NUnit / Unity Test Framework）、C#、macOS 上の CLI（`scripts/run-unity-tests.sh`、`scripts/test-summary.py`、`scripts/capture-screens.sh`、`scripts/build-ios.sh`）。

**Spec:** `docs/superpowers/specs/2026-09-25-breeder-sim-design.md`（本計画は §6.1 の装飾の枠・§6.2 の台帳（家系図と売上を除く）・§6.3 の金額の一元化・§9 全体・§10.1 全体・§12 のショップの在庫と所持品・§14 段階3 を実装する）。引き継ぎ：`docs/superpowers/handoffs/2026-09-26-phase3-kickoff.md`。

## Global Constraints

- ユーザーへの応答は日本語。コード・コメント・コミットメッセージは英語（CLAUDE.md）。
- 相場（§10.1）：基本価格の表、通称のない組み合わせ＝最も高い要素×1.3^(追加の要素の数)、ヘテロ1つ+20%・ポッシブルヘテロは確率×0.12、メス×1.2、ベビー×0.8／ヤング×1.0／アダルト×1.3、性格の倍率（§5.2）、衰弱×0.3（段階3では常に「衰弱なし」で呼ぶ）、多因子で最大×1.5、イベントの需要（モルフごとに0.8〜1.3、乱数の種から。大規模では基本価格15,000円以上のモルフに×1.15。段階3では「イベントなし」＝1.0で呼ぶ）。
- ショップ（§9）：生体は毎月（ゲーム内）4〜6匹が入れ替わる。販売価格は相場×1.2。卸売りはいつでも相場の40%。ベビーは雌雄不明。性格は購入後に判明（購入からゲーム内7日）。
- 商品：小型ケージ3,000／標準6,000／大型10,000、ラック8,000、装飾（各1,000〜3,000）、産卵床1,500、孵卵器 標準15,000／高級40,000（§8 の価格）。
- 装飾はケージごと（小型1／標準2／大型3）。成長による解放はやめる。
- 金額はすべて `EconomyTuning` にまとめる（§6.3）。モルフの基本価格の表だけは相場の定数として `MarketPrice` に置く。
- お金の動きはすべて `Wallet` を通し、台帳（`Ledger`）に残す。金額は負にしない（負なら例外）。
- セーブはスキーマ3のまま（フィールドを追加する）。段階1・段階2で保存されたスキーマ3のファイル、スキーマ1・2の旧ファイルも読めること。装飾の所持品への移行は一度だけ。
- 画面の文字は日本語。マイナス記号は ASCII の `-` を使う（フォントに U+2212 がない可能性があるため）。
- 表示でレイアウトがずれないよう、固定の高さの欄は `visibility` で表示を切り替える（既存の方針）。画面（パネル・モーダル）の切り替えは `display`。
- 新しい .cs ファイルの .meta は手書きしない。Unity に生成させる（`scripts/run-unity-tests.sh` の実行で生成される）。`scripts/test-summary.py` が「.meta will be ignored」を報告したら失敗扱い。削除するファイルは .meta も一緒に `git rm` する。

## 計画者の判断（仕様書に書かれていない点）

| 判断 | 理由 |
|---|---|
| 基本価格は `MorphNamer.VisualName` の単語ごとに表から引く。通称（レイプター・ブレイジングブリザード）は1要素。スーパースノーは1要素（25,000）。「ハイポ」は要素に数えず、多因子の倍率で扱う。単語が1つも表にないときはノーマル（5,000） | 仕様の表にハイポがない。名前の規則（§4.2）と価格の規則を1か所（名前）にそろえると食い違いが出ない |
| 多因子の倍率＝1＋0.25×(ハイポの50超過分/50)＋0.25×(タンジェリンの50超過分/50)（各0〜1に収める）。最大×1.5 | 仕様は「最大×1.5」とだけ定める。50以下（スターターの10〜40）は倍率なし |
| ヘテロの加算は足し算（1＋0.2×確定の数＋Σ確率×0.12）。「ヘテロ不明」は加算なし。本当の遺伝子型に隠れたヘテロは価格に入れない（プレイヤーが知っている情報で決まる） | 「66%なら+8%」という仕様の例が足し算の書き方。相場は買い手が知っている情報で決まる |
| メス×1.2は雌雄が判明しているときだけ。性格の倍率は性格が判明しているときだけ | ベビーは雌雄不明、ショップの個体は性格不明（§9）。わからないものは値段に反映できない |
| 相場は100円単位に四捨五入（`MidpointRounding.AwayFromZero`）、最低100円。販売価格（×1.2）と卸値（×0.4）も同じ丸め | 値札らしい金額にする |
| イベントの需要は（イベントの種, 見た目の名前）の決まった hash（FNV-1a＋SplitMix64）から 0.8〜1.3。`string.GetHashCode` は使わない。大規模の×1.15 は組み合わせ後の基本価格で判定 | 実行環境によらず同じ結果（§11 の再現性）。イベント自体は段階6 |
| 衰弱（§5.5）は `MarketPrice.For(pet, demand, weak)` の引数として作り、段階3では常に `false` | 衰弱は段階4で入る |
| 在庫の作り方：重み付きの15種の型（ノーマル20、マックスノー8、トレンパー8、タンジェリン6、ベル5、レインウォーター5、エクリプス6、ブリザード6、マーフィー5、W&Y6、スーパースノー5、ブレイジングブリザード5、レイプター5、マックスノートレンパー5、ハイポタンジェリン5）。ヤング25%・ベビー75%。見た目に出ない劣性遺伝子ごとに15%で確定ヘテロ、10%でポッシブルヘテロ（50%か66%、表示どおりの確率で本当に持つ）、表示するヘテロは最大2つ。多因子は10〜40（タンジェリン型は65〜95、ハイポは75〜95）。体重：ベビー4〜12g・ヤング16〜30g。年齢：ベビー1〜3か月・ヤング4〜7か月 | 利用者の決定「多くはベビー、一部ヤング、ノーマルから人気モルフまで、確定・ポッシブルヘテロを表示」を数値にしたもの。ヘテロの表示は正直にする（ショップの信用） |
| 入荷は「ゲーム内の月が在庫の月と違うとき」に丸ごと入れ替える。乱数の種は（ショップの種, 月の番号）から決める。最初の読み込みでも入荷する。売れた（買った）枠は月末まで空く。価格は表示のたびに相場から計算する（在庫に値段を保存しない） | 同じ月なら何度開いても同じ在庫（再現可能）。相場は段階3では変わらないが、段階6で需要が入っても表示が追いつく |
| ショップの種は新規ゲーム・旧形式の移行で `random.Next()`。スターター遺伝子の後に引く。既存のスキーマ3のセーブは種0 | 既存のテストの乱数の並びを変えない |
| 性格の判明は「購入時刻（ゲームの時計）＋ゲーム内7日」を `PetState.PersonalityRevealAtUtc` に持ち、時間の適用で判明させる。判明時に「○○の性格は「おっとり」のようです」と出す | 利用者の決定（7日）。デバッグの早送りの後に保存で時計が戻る既知の問題（段階4の前に直す）があり、早送り中は判明が遅れることがある |
| 生体を買うには空きケージが要る。最初の空きケージ（番号順）に入れる。名前は購入時に「レオパN」 | 1ケージ1匹（§1）。入れ先を選ぶ画面は段階3では作らない |
| 最後の1匹も卸売りできる（個体0匹を許す）。0匹のときは、ホームと一覧はすべて空きケージ、ケージの詳細には入れない（◀▶ は何もしない）、一括の世話は「個体がいません。ショップで迎えましょう」、台帳の個体欄も同じ文、選択中の個体はなし（`state == null`）。0匹のセーブも読み書きできる。ショップで買えば元に戻る | 利用者の決定（HQ 経由、2026-09-26）。行き詰まりはない（卸売りの代金と所持金で買い直せる） |
| 孵卵器と産卵床は段階3で売り、ショップの行に「段階N で使えます」と書く | 利用者の承認（HQ 経由、2026-09-26） |
| 新規ゲームはケージ1に岩を1つ置いた状態で始める | 利用者の承認（HQ 経由、2026-09-26） |
| スキーマ番号のない JSON の扱い（下の行の規則） | 利用者の承認（HQ 経由、2026-09-26） |
| 装飾は1つのケージに同じ物を1つまで。置く場所は枠の番号で決める（床：枠0＝左奥 X0.15・奥行0.45、枠1＝右手前 X0.85・0.6、枠2＝中央奥 X0.5・0.38）。吊り下げ（保温ランプ）は自分の X を使う。隠れ家は枠の順で最初の床の装飾。装飾がなければ隠れ家なし | 今の装飾ごとの位置（岩・植物・流木が左に重なる）のままだと3つ置いたとき重なる。枠の位置は画面キャプチャで確かめて調整してよい |
| 装飾の値段：岩1,000・水入れ1,000・観葉植物1,500・流木2,000・保温ランプ3,000。産卵床1,500（§9）。孵卵器は §8 の標準15,000・高級40,000。ラックは最大4台（16ケージ） | 仕様は「各1,000〜3,000」とだけ定める。見た目の大きさ・効果の順。ラックの上限はホーム画面の縦の長さのため |
| 旧データの装飾の移行：各個体の解放済みの装飾を1つずつ所持品に入れ（個体の数だけ足す）、置いていた装飾は外す（ケージは空）。`inventoryVersion` で一度だけ行い、移したときは「装飾を所持品に移しました。ケージの「そうしょく」から置けます」と出す | 利用者の決定。1匹ごとの水槽にあった物をどのケージにも置き直せるよう、個体ごとに数える |
| 新規ゲームは、ケージ1に岩を置いた状態で始める（所持品は空） | これまでの最初の見た目（岩の隠れ家）を保つ |
| 孵卵器（標準・高級）と産卵床は段階3で売る。孵卵器は `Colony.Incubators`（機種の一覧）に加わり、電気代（1台500円/月）がかかる。ショップの行に「段階5で使えます」（産卵床は「段階4で使えます」）と書く | 仕様 §14 の段階3「用品」。使えるのは後の段階なので、買う前に分かるようにする |
| 台帳の区分に `AnimalPurchase`（生体の購入）を加える。用品は `Purchase` | 台帳の画面で生体と用品を分けて見せる |
| `Wallet` の3つの操作は負の金額で `ArgumentOutOfRangeException` | 引き継ぎの指摘。負の金額は向きの間違い（Charge と Earn の取り違え）なので早く気づけるようにする |
| 台帳のお金の欄は新しい順に最大100件と今月の収入・支出を表示。セーブには全件残す（まとめる処理は段階6の売上の履歴と一緒に考える） | 餌代の行が毎日増えるため。1年で約1MB程度と見込み、今は問題にならない |
| 台帳の個体一覧：名前・モルフ（知っている遺伝情報）・性格・雌雄・段階・体重・年齢。両親と家系図は段階4、売上の履歴は段階6 | 利用者の決定。両親のデータは繁殖（段階4）で初めてできる |
| 壊れたセーブの控え（コピー）自体が失敗したときは、この起動ではセーブを書かない（`ColonySession.SaveBlocked`）。画面に「セーブデータを読めず、控えも作れませんでした。空き容量を確かめてアプリを開き直してください」 | 引き継ぎの指摘。元のファイルを上書きして失うのを防ぐ |
| スキーマ番号のない JSON は、`"lastSavedAtUtc"` と `"growthStage"` の両方を含むときだけ旧形式（スキーマ1）とみなす。それ以外は壊れたセーブの扱い | 引き継ぎの指摘。スキーマ1は本当に番号を持たない（既存のテスト）ので、番号なしを全部拒むことはできない |
| ホームからタブへ：下のタブバーをホーム画面にも出す（`main-screen` の外に移す）。ホームにいる間はどのタブも選択表示にしない。`home-incubator-button` は孵卵器タブを開く | 引き継ぎの指摘（段階1の最終レビュー）。ケージに入らずにショップ・台帳へ行ける |
| サムネイルは待機の1コマ目を、体の範囲（`TerrariumArtLayout.Pet`）＋6px で切り出したスプライトにする（モルフごとにキャッシュ） | 引き継ぎの指摘（256px の枠の大半が余白） |
| プロフィール欄は語ごとの小さなラベル（折り返さない）を横に並べて折り返す（`profile-chips`）。語の区切りはモルフ名の空白 | 引き継ぎの指摘（「好奇心旺盛」「ヘテロ不明」が文字の途中で分かれる）。UI Toolkit は日本語をどこでも折り返すため |
| 購入と卸売りは確認のダイアログ（はい／いいえ）を出してから行う | 誤タップでお金や個体を失わないため |
| 持ち越し：デバッグの早送りが保存のたびに戻る問題、色を変えたスプライトのキャッシュの上限は段階4の前に回す（引き継ぎどおり） | 段階3の完了条件（購入・売却が台帳に残る）に関わらない |

## ファイル構成

| ファイル | 種別 | 役割 |
|---|---|---|
| `Assets/Scripts/Core/Wallet.cs` | 変更 | 負の金額の拒否、`LedgerCategory.AnimalPurchase` |
| `Assets/Scripts/Core/EconomyTuning.cs` | 変更 | ショップの値段・上限・販売倍率・卸売り率 |
| `Assets/Scripts/Core/MarketPrice.cs` | 新規 | 相場（§10.1） |
| `Assets/Scripts/Core/EventDemand.cs` | 新規 | イベントの需要（決まった hash） |
| `Assets/Scripts/Core/Inventory.cs` | 新規 | 所持品（id ごとの数） |
| `Assets/Scripts/Core/DecorSlots.cs` | 新規 | 装飾の一覧（`DecorItems`）、枠の数、置く・外す、状態の文字 |
| `Assets/Scripts/Core/Colony.cs` | 変更 | `Cage.DecorIds`・`Inventory`・`Incubators`（`IncubatorModel`）・`Shop`・`RemoveAnimal`、新規ゲームで岩を置く |
| `Assets/Scripts/Core/TerrariumArtLayout.cs` | 変更 | 枠の位置 `DecorSlotSpots`・`PlacementFor(id, slot)` |
| `Assets/Scripts/Core/ShopStock.cs` | 新規 | `ShopOffer`・`ShopStock`（入荷） |
| `Assets/Scripts/Core/ShopStockGenerator.cs` | 新規 | 生体の在庫の生成 |
| `Assets/Scripts/Core/ShopCatalog.cs` | 新規 | 用品の一覧 |
| `Assets/Scripts/Core/ShopService.cs` | 新規 | 生体・用品の購入、卸売り、価格 |
| `Assets/Scripts/Core/PersonalityReveal.cs` | 新規 | 購入後の性格の判明 |
| `Assets/Scripts/Core/ThumbnailCrop.cs` | 新規 | サムネイルの切り出し範囲 |
| `Assets/Scripts/Core/PetState.cs` | 変更 | `PersonalityRevealAtUtc` 追加、装飾のフィールド削除（Task 6） |
| `Assets/Scripts/Core/CareTuning.cs` | 変更 | `PersonalityRevealGameDays` |
| `Assets/Scripts/Core/ColonySession.cs` | 変更 | 入荷・性格の判明・`SaveBlocked`・`DecorMovedToInventory` |
| `Assets/Scripts/Core/ColonySaveData.cs`・`ColonySaveService.cs` | 変更 | 所持品・ケージの装飾・孵卵器・在庫・判明時刻の保存、装飾の移行、壊れたセーブの扱い |
| `Assets/Scripts/Gameplay/DecorCatalog.cs`・`DecorDefinition.cs`・`DecorUnlockService.cs` | 削除 | 成長による解放をやめる（Task 6） |
| `Assets/Scripts/UI/CageStatusText.cs` | 変更 | `ProfileTokens`・`SexLabel` |
| `Assets/Scripts/UI/MorphSprites.cs` | 変更 | `Thumbnail(pet)` |
| `Assets/Scripts/UI/HomeView.cs` | 変更 | 切り出したサムネイル |
| `Assets/Scripts/UI/ShellNavigator.cs` | 変更 | ショップ・台帳のパネル、ホームでのタブバー |
| `Assets/Scripts/UI/ShopText.cs`・`ShopView.cs` | 新規 | ショップの文字とパネル |
| `Assets/Scripts/UI/LedgerText.cs`・`LedgerView.cs` | 新規 | 台帳の文字とパネル |
| `Assets/Scripts/UI/ConfirmDialog.cs` | 新規 | はい／いいえのダイアログ |
| `Assets/Scripts/UI/TerrariumView.cs` | 変更 | 装飾の複数表示と引き出し、ショップ・台帳の結線、購読の解除 |
| `Assets/UI/Terrarium.uxml`・`Terrarium.uss` | 変更 | 装飾の3枠、タブバーの位置、プロフィールの語、ショップ・台帳のパネル、確認のダイアログ |
| テスト | 新規・変更 | `EconomyTests`・`MarketPriceTests`・`InventoryAndDecorTests`・`ShopStockTests`・`ShopServiceTests`・`ColonySaveServiceTests`・`ColonySessionTests`・`ShellTests`・`ShopAndLedgerTextTests`（EditMode）、`DecorUnlockServiceTests` 削除、`TerrariumViewTests`・`ScreenCaptureTests`（PlayMode） |
| `GAME.md` | 変更 | お金・相場・ショップ・卸売り・装飾・台帳の説明 |

テストの実行（全タスク共通）：

```bash
scripts/run-unity-tests.sh EditMode && python3 scripts/test-summary.py Logs/EditMode-results.xml
scripts/run-unity-tests.sh PlayMode && python3 scripts/test-summary.py Logs/PlayMode-results.xml
```

特定のテストだけ流すときは `-testFilter <完全名>` を付ける（例：`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.MarketPriceTests`）。開始時点の件数は EditMode 299・PlayMode 44（＋Explicit 2）。

## タスクの一覧と作業者

| Task | 内容 | 作業者 |
|---|---|---|
| 1 | お金の土台と相場（`Wallet`・`EconomyTuning`・`MarketPrice`・`EventDemand`） | transcriber |
| 2 | 所持品・装飾の枠・部屋のデータ（`Inventory`・`DecorSlots`・`Colony`・`TerrariumArtLayout`） | transcriber |
| 3 | ショップの在庫（`ShopStock`・`ShopStockGenerator`） | transcriber |
| 4 | 売り買いと性格の判明（`ShopCatalog`・`ShopService`・`PersonalityReveal`） | transcriber |
| 5 | セッションとセーブ（入荷・判明・保存・装飾の移行・壊れたセーブ） | programmer |
| 6 | 装飾をケージごとに（画面・解放の廃止） | programmer（画面キャプチャで確認） |
| 7 | 画面の持ち越し修正（ホームのタブバー・購読の解除・サムネイル・プロフィールの語） | graphics |
| 8 | ショップのタブ（生体・用品・卸売り・確認のダイアログ） | programmer |
| 9 | 台帳のタブ（個体一覧・お金の出入り） | programmer |
| 10 | 文書の更新と最終確認（実機） | programmer（最後に HQ が利用者に確認） |

依存：1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10（順番に行う。4 は 1〜3 を使う。6 以降は画面）。

---

### Task 1: お金の土台と相場

**Files:**
- Modify: `Assets/Scripts/Core/Wallet.cs`
- Modify: `Assets/Scripts/Core/EconomyTuning.cs`
- Create: `Assets/Scripts/Core/MarketPrice.cs`
- Create: `Assets/Scripts/Core/EventDemand.cs`
- Test: `Assets/Tests/EditMode/EconomyTests.cs`（追加）、`Assets/Tests/EditMode/MarketPriceTests.cs`（新規）

**Interfaces:**
- Produces: `LedgerCategory.AnimalPurchase`、`Wallet.TrySpend/Charge/Earn`（負の金額で `ArgumentOutOfRangeException`）、`EconomyTuning.SmallCagePrice/StandardCagePrice/LargeCagePrice/CagePrice(CageSize)/RackPrice/MaxRacks/NestBoxPrice/StandardIncubatorPrice/LuxuryIncubatorPrice/DecorPrices/ShopMarkup/WholesaleRate`、`MarketPrice.MorphPrices/BasePrice(Genotype)/HetMultiplier(Genotype, KnownGenetics)/PolygenicMultiplier(Genotype)/StageMultiplier(GrowthStage)/For(PetState, double demand = 1, bool weak = false)/RoundYen(double)/ShopPrice(long, EconomyTuning)/WholesalePrice(long, EconomyTuning)`、`EventDemand.None/For(int eventSeed, Genotype, bool isBigEvent)/Unit(int, string)`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/EconomyTests.cs` のクラスの末尾（`MonthlyElectricity_IsPerCageAndPerIncubator` の後）に追加する：

```csharp
        [Test]
        public void TrySpend_CanSpendExactlyEverything()
        {
            var wallet = new Wallet { Money = 80 };

            Assert.That(wallet.TrySpend(80, LedgerCategory.Purchase, "岩を購入", Now), Is.True);

            Assert.That(wallet.Money, Is.EqualTo(0));
            Assert.That(wallet.Ledger, Has.Count.EqualTo(1));
        }

        [Test]
        public void Earn_RecordsIncome()
        {
            var wallet = new Wallet { Money = 100 };

            wallet.Earn(1600, LedgerCategory.Wholesale, "卸売り", Now);

            Assert.That(wallet.Money, Is.EqualTo(1700));
            Assert.That(wallet.Ledger[0].Amount, Is.EqualTo(1600));
            Assert.That(wallet.Ledger[0].Category, Is.EqualTo(LedgerCategory.Wholesale));
        }

        [Test]
        public void NegativeAmounts_AreRejectedAndRecordNothing()
        {
            var wallet = new Wallet { Money = 100 };

            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(-1, LedgerCategory.Food, "x", Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Charge(-1, LedgerCategory.Electricity, "x", Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Earn(-1, LedgerCategory.Wholesale, "x", Now));

            Assert.That(wallet.Money, Is.EqualTo(100));
            Assert.That(wallet.Ledger, Is.Empty);
        }

        [Test]
        public void ShopPrices_FollowTheSpec()
        {
            Assert.That(tuning.CagePrice(CageSize.Small), Is.EqualTo(3000));
            Assert.That(tuning.CagePrice(CageSize.Standard), Is.EqualTo(6000));
            Assert.That(tuning.CagePrice(CageSize.Large), Is.EqualTo(10000));
            Assert.That(tuning.RackPrice, Is.EqualTo(8000));
            Assert.That(tuning.MaxRacks, Is.EqualTo(4));
            Assert.That(tuning.NestBoxPrice, Is.EqualTo(1500));
            Assert.That(tuning.StandardIncubatorPrice, Is.EqualTo(15000));
            Assert.That(tuning.LuxuryIncubatorPrice, Is.EqualTo(40000));
            Assert.That(tuning.ShopMarkup, Is.EqualTo(1.2d));
            Assert.That(tuning.WholesaleRate, Is.EqualTo(0.4d));
            Assert.That(tuning.DecorPrices.Count, Is.EqualTo(5));
            foreach (var price in tuning.DecorPrices.Values)
            {
                Assert.That(price, Is.InRange(1000L, 3000L));
            }
        }
```

`Assets/Tests/EditMode/MarketPriceTests.cs`:

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class MarketPriceTests
    {
        private readonly EconomyTuning economy = new EconomyTuning();

        /// <summary>Sex and personality unknown, so neither multiplier applies unless a test sets them.</summary>
        private static PetState Pet(Genotype genotype, GrowthStage stage, KnownGenetics known = null) => new PetState
        {
            Genotype = genotype,
            Known = known ?? new KnownGenetics(),
            Stage = stage,
            SexRevealed = false,
            PersonalityKnown = false,
        };

        [Test]
        public void ANormalBaby_IsFiveThousandTimesPointEight()
        {
            Assert.That(MarketPrice.For(Pet(Genotype.Normal(), GrowthStage.Baby)), Is.EqualTo(4000));
        }

        [Test]
        public void BasePrice_UsesTheTableAndCombinations()
        {
            Assert.That(MarketPrice.BasePrice(Genotype.Normal()), Is.EqualTo(5000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.WhiteAndYellow, 1)), Is.EqualTo(20000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.MackSnow, 2)), Is.EqualTo(25000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2)), Is.EqualTo(25000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2)), Is.EqualTo(30000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.MackSnow, 1).Set(GeneId.TremperAlbino, 2)), Is.EqualTo(13000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.Eclipse, 2).Set(GeneId.Blizzard, 2)), Is.EqualTo(19500));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.MackSnow, 2).Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2)), Is.EqualTo(39000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal().Set(GeneId.MackSnow, 1).Set(GeneId.Eclipse, 2).Set(GeneId.MurphyPatternless, 2)), Is.EqualTo(25350));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal(tangerine: 90d)), Is.EqualTo(12000));
            Assert.That(MarketPrice.BasePrice(Genotype.Normal(hypo: 90d)), Is.EqualTo(5000), "hypo alone is priced by the polygenic multiplier");
        }

        [Test]
        public void AKnownFemaleWithAKnownCalmPersonality_GetsBothMultipliers()
        {
            var pet = Pet(Genotype.Normal().Set(GeneId.TremperAlbino, 2), GrowthStage.Juvenile);
            pet.Sex = Sex.Female;
            pet.SexRevealed = true;
            pet.Personality = Personality.Calm;
            pet.PersonalityKnown = true;

            Assert.That(MarketPrice.For(pet), Is.EqualTo(13200));
        }

        [Test]
        public void AnUnknownSexOrPersonality_AddsNothing()
        {
            var pet = Pet(Genotype.Normal().Set(GeneId.TremperAlbino, 2), GrowthStage.Juvenile);
            pet.Sex = Sex.Female;
            pet.Personality = Personality.Calm;

            Assert.That(MarketPrice.For(pet), Is.EqualTo(10000));
        }

        [Test]
        public void ProvenAndPossibleHets_AddTwentyPercentAndProbabilityTimesTwelvePercent()
        {
            var known = new KnownGenetics().SetHet(GeneId.Blizzard, 1d).SetHet(GeneId.MurphyPatternless, 0.66d);
            var raptor = Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2).Set(GeneId.Blizzard, 1);
            var pet = Pet(raptor, GrowthStage.Adult, known);
            pet.Sex = Sex.Male;
            pet.SexRevealed = true;

            Assert.That(MarketPrice.HetMultiplier(raptor, known), Is.EqualTo(1.2792d).Within(1e-9));
            Assert.That(MarketPrice.For(pet), Is.EqualTo(49900));
        }

        [Test]
        public void UnknownHets_AddNothing()
        {
            var carrier = Genotype.Normal().Set(GeneId.Eclipse, 1);

            Assert.That(MarketPrice.HetMultiplier(carrier, KnownGenetics.Unknown()), Is.EqualTo(1d));
        }

        [TestCase(GrowthStage.Baby, 0.8d)]
        [TestCase(GrowthStage.Juvenile, 1d)]
        [TestCase(GrowthStage.Adult, 1.3d)]
        public void StageMultiplier(GrowthStage stage, double multiplier)
        {
            Assert.That(MarketPrice.StageMultiplier(stage), Is.EqualTo(multiplier));
        }

        [Test]
        public void Polygenic_AddsUpToFiftyPercent()
        {
            Assert.That(MarketPrice.PolygenicMultiplier(Genotype.Normal(25d, 25d)), Is.EqualTo(1d));
            Assert.That(MarketPrice.PolygenicMultiplier(Genotype.Normal(100d, 100d)), Is.EqualTo(1.5d).Within(1e-9));
            Assert.That(MarketPrice.PolygenicMultiplier(Genotype.Normal(25d, 90d)), Is.EqualTo(1.2d).Within(1e-9));
            Assert.That(MarketPrice.For(Pet(Genotype.Normal(25d, 90d), GrowthStage.Juvenile)), Is.EqualTo(14400));
        }

        [Test]
        public void WeaknessAndDemand_Multiply()
        {
            var pet = Pet(Genotype.Normal(), GrowthStage.Juvenile);

            Assert.That(MarketPrice.For(pet, weak: true), Is.EqualTo(1500));
            Assert.That(MarketPrice.For(pet, demand: 1.3d), Is.EqualTo(6500));
            Assert.That(MarketPrice.For(pet, demand: EventDemand.None), Is.EqualTo(5000));
        }

        [Test]
        public void ShopAndWholesalePrices_AreMarketTimesTheirRates()
        {
            Assert.That(MarketPrice.ShopPrice(4000, economy), Is.EqualTo(4800));
            Assert.That(MarketPrice.WholesalePrice(4000, economy), Is.EqualTo(1600));
        }

        [Test]
        public void RoundYen_RoundsToHundredsWithAFloor()
        {
            Assert.That(MarketPrice.RoundYen(12349d), Is.EqualTo(12300));
            Assert.That(MarketPrice.RoundYen(12351d), Is.EqualTo(12400));
            Assert.That(MarketPrice.RoundYen(10d), Is.EqualTo(100));
        }

        [Test]
        public void Demand_IsTheSameForTheSameEventAndMorph()
        {
            var eclipse = Genotype.Normal().Set(GeneId.Eclipse, 2);

            Assert.That(EventDemand.For(12, eclipse, false), Is.EqualTo(EventDemand.For(12, eclipse, false)));
        }

        [Test]
        public void Demand_SpreadsBetweenPointEightAndOnePointThree()
        {
            var min = double.MaxValue;
            var max = double.MinValue;
            for (var seed = 0; seed < 200; seed++)
            {
                var demand = EventDemand.For(seed, Genotype.Normal(), false);
                Assert.That(demand, Is.GreaterThanOrEqualTo(0.8d).And.LessThan(1.3d));
                min = Math.Min(min, demand);
                max = Math.Max(max, demand);
            }

            Assert.That(min, Is.LessThan(0.85d));
            Assert.That(max, Is.GreaterThan(1.25d));
        }

        [Test]
        public void Demand_DiffersBetweenMorphs()
        {
            var tremper = Genotype.Normal().Set(GeneId.TremperAlbino, 2);
            var differ = 0;
            for (var seed = 0; seed < 20; seed++)
            {
                if (Math.Abs(EventDemand.For(seed, Genotype.Normal(), false) - EventDemand.For(seed, tremper, false)) > 1e-9)
                {
                    differ++;
                }
            }

            Assert.That(differ, Is.GreaterThanOrEqualTo(15));
        }

        [Test]
        public void ABigEvent_BoostsOnlyMorphsFromFifteenThousandYen()
        {
            var eclipse = Genotype.Normal().Set(GeneId.Eclipse, 2);
            var tremper = Genotype.Normal().Set(GeneId.TremperAlbino, 2);

            Assert.That(EventDemand.For(5, eclipse, true), Is.EqualTo(EventDemand.For(5, eclipse, false) * 1.15d).Within(1e-12));
            Assert.That(EventDemand.For(5, tremper, true), Is.EqualTo(EventDemand.For(5, tremper, false)));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.MarketPriceTests`
Expected: コンパイルエラー（`MarketPrice` などがない）。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/Wallet.cs` を丸ごと置き換える：

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
        Other,
        AnimalPurchase
    }

    /// <summary>One money movement: positive = income, negative = expense.</summary>
    public sealed class LedgerEntry
    {
        public DateTimeOffset AtUtc { get; set; }
        public LedgerCategory Category { get; set; }
        public long Amount { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Money on hand plus every movement, for the ledger screen. Amounts passed in are never negative.</summary>
    public sealed class Wallet
    {
        public long Money { get; set; }

        public List<LedgerEntry> Ledger { get; set; } = new List<LedgerEntry>();

        /// <summary>Pays only if affordable (e.g. food, shop). Returns false and records nothing otherwise.</summary>
        public bool TrySpend(long amount, LedgerCategory category, string note, DateTimeOffset atUtc)
        {
            RequireNonNegative(amount);
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
            RequireNonNegative(amount);
            Money -= amount;
            Ledger.Add(new LedgerEntry { AtUtc = atUtc, Category = category, Amount = -amount, Note = note });
        }

        public void Earn(long amount, LedgerCategory category, string note, DateTimeOffset atUtc)
        {
            RequireNonNegative(amount);
            Money += amount;
            Ledger.Add(new LedgerEntry { AtUtc = atUtc, Category = category, Amount = amount, Note = note });
        }

        private static void RequireNonNegative(long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount,
                    "Amounts are never negative: use Charge/TrySpend for expenses and Earn for income.");
            }
        }
    }
}
```

`Assets/Scripts/Core/EconomyTuning.cs` を丸ごと置き換える：

```csharp
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Every price and running cost in yen, in one place (§6.3).</summary>
    public sealed class EconomyTuning
    {
        public long StartingMoney { get; set; } = 50000;

        public long FeedCostBaby { get; set; } = 30;
        public long FeedCostJuvenile { get; set; } = 50;
        public long FeedCostAdult { get; set; } = 80;

        public long ElectricityPerCagePerMonth { get; set; } = 300;
        public long ElectricityPerIncubatorPerMonth { get; set; } = 500;

        // Shop (§9, §8).
        public long SmallCagePrice { get; set; } = 3000;
        public long StandardCagePrice { get; set; } = 6000;
        public long LargeCagePrice { get; set; } = 10000;
        public long RackPrice { get; set; } = 8000;
        public int MaxRacks { get; set; } = 4;
        public long NestBoxPrice { get; set; } = 1500;
        public long StandardIncubatorPrice { get; set; } = 15000;
        public long LuxuryIncubatorPrice { get; set; } = 40000;

        /// <summary>Per decor id (see DecorItems).</summary>
        public IReadOnlyDictionary<string, long> DecorPrices { get; set; } = new Dictionary<string, long>
        {
            ["rock_01"] = 1000,
            ["water_dish_01"] = 1000,
            ["plant_01"] = 1500,
            ["driftwood_01"] = 2000,
            ["heat_lamp_01"] = 3000,
        };

        /// <summary>Shop animals sell at market × this (§9).</summary>
        public double ShopMarkup { get; set; } = 1.2d;

        /// <summary>Wholesale pays market × this (§9).</summary>
        public double WholesaleRate { get; set; } = 0.4d;

        public long CagePrice(CageSize size) =>
            size == CageSize.Small ? SmallCagePrice : size == CageSize.Large ? LargeCagePrice : StandardCagePrice;
    }
}
```

`Assets/Scripts/Core/MarketPrice.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// The market reference price (相場, §10.1): morph base price, het bonus, individual
    /// multipliers and the event demand factor, rounded to 100 yen. Uses only what the
    /// player can know (visible genes, known hets, revealed sex and personality).
    /// </summary>
    public static class MarketPrice
    {
        public const long NormalPrice = 5000;
        public const double CombinationStep = 1.3d;
        public const double ProvenHetBonus = 0.2d;
        public const double PossibleHetBonusPerProbability = 0.12d;
        public const double FemaleMultiplier = 1.2d;
        public const double WeakMultiplier = 0.3d;
        public const double PolygenicBonusPerTrait = 0.25d;
        public const double PolygenicBonusFrom = 50d;

        /// <summary>Base price per word of <see cref="MorphNamer.VisualName"/>. ハイポ is priced by <see cref="PolygenicMultiplier"/>.</summary>
        public static readonly IReadOnlyDictionary<string, long> MorphPrices = new Dictionary<string, long>
        {
            ["ノーマル"] = 5000,
            ["マックスノー"] = 10000,
            ["トレンパーアルビノ"] = 10000,
            ["タンジェリン"] = 12000,
            ["ベルアルビノ"] = 15000,
            ["レインウォーターアルビノ"] = 15000,
            ["エクリプス"] = 15000,
            ["ブリザード"] = 15000,
            ["マーフィーパターンレス"] = 15000,
            ["ホワイト&イエロー"] = 20000,
            ["スーパースノー"] = 25000,
            ["ブレイジングブリザード"] = 25000,
            ["レイプター"] = 30000,
        };

        /// <summary>Most expensive element × 1.3^(number of other elements); a trade name is one element.</summary>
        public static long BasePrice(Genotype genotype)
        {
            long highest = 0;
            var elements = 0;
            foreach (var word in MorphNamer.VisualName(genotype).Split(' '))
            {
                if (!MorphPrices.TryGetValue(word, out var price))
                {
                    continue;
                }

                elements++;
                highest = Math.Max(highest, price);
            }

            if (elements == 0)
            {
                return NormalPrice;
            }

            return (long)Math.Round(highest * Math.Pow(CombinationStep, elements - 1), MidpointRounding.AwayFromZero);
        }

        /// <summary>1 + 0.2 per proven het + 0.12 × probability per possible het (recessive genes that do not show).</summary>
        public static double HetMultiplier(Genotype genotype, KnownGenetics known)
        {
            var bonus = 0d;
            foreach (var gene in Genes.All)
            {
                if (!Genes.IsRecessive(gene) || genotype.Shows(gene))
                {
                    continue;
                }

                var p = known.HetProbability(gene);
                bonus += p >= 1d ? ProvenHetBonus : p * PossibleHetBonusPerProbability;
            }

            return 1d + bonus;
        }

        /// <summary>Up to ×1.5: each of hypo and tangerine adds up to 0.25 above 50.</summary>
        public static double PolygenicMultiplier(Genotype genotype) =>
            1d + PolygenicBonusPerTrait * (Above(genotype.Hypo) + Above(genotype.Tangerine));

        public static double StageMultiplier(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Juvenile:
                    return 1d;
                case GrowthStage.Adult:
                    return 1.3d;
                default:
                    return 0.8d;
            }
        }

        /// <summary>
        /// The market price of this animal. <paramref name="demand"/> is the event demand
        /// (<see cref="EventDemand.None"/> outside events); <paramref name="weak"/> is §5.5 (phase 4).
        /// </summary>
        public static long For(PetState pet, double demand = 1d, bool weak = false)
        {
            var yen = BasePrice(pet.Genotype) * HetMultiplier(pet.Genotype, pet.Known) * PolygenicMultiplier(pet.Genotype)
                * StageMultiplier(pet.Stage) * demand;
            if (pet.SexKnown && pet.Sex == Sex.Female)
            {
                yen *= FemaleMultiplier;
            }

            if (pet.PersonalityKnown)
            {
                yen *= PersonalityTraits.PriceMultiplier(pet.Personality);
            }

            if (weak)
            {
                yen *= WeakMultiplier;
            }

            return RoundYen(yen);
        }

        /// <summary>Nearest 100 yen, at least 100.</summary>
        public static long RoundYen(double yen) =>
            Math.Max(100L, (long)Math.Round(yen / 100d, MidpointRounding.AwayFromZero) * 100L);

        public static long ShopPrice(long market, EconomyTuning economy) => RoundYen(market * economy.ShopMarkup);

        public static long WholesalePrice(long market, EconomyTuning economy) => RoundYen(market * economy.WholesaleRate);

        private static double Above(double value) =>
            Math.Max(0d, Math.Min(1d, (value - PolygenicBonusFrom) / (100d - PolygenicBonusFrom)));
    }
}
```

`Assets/Scripts/Core/EventDemand.cs`:

```csharp
namespace TerrariumDays.Core
{
    /// <summary>
    /// Per-event demand for a morph (§10.1): 0.8–1.3 from the event's seed, and ×1.15 at big
    /// events for morphs whose base price is 15,000 yen or more. Events arrive in phase 6;
    /// until then callers use <see cref="None"/>.
    /// </summary>
    public static class EventDemand
    {
        public const double None = 1d;
        public const double Min = 0.8d;
        public const double Max = 1.3d;
        public const double BigEventBoost = 1.15d;
        public const long BigEventBoostFromBasePrice = 15000;

        public static double For(int eventSeed, Genotype genotype, bool isBigEvent)
        {
            var demand = Min + (Max - Min) * Unit(eventSeed, MorphNamer.VisualName(genotype));
            return isBigEvent && MarketPrice.BasePrice(genotype) >= BigEventBoostFromBasePrice ? demand * BigEventBoost : demand;
        }

        /// <summary>
        /// A number in [0, 1) fixed by (seed, key) on every platform (string.GetHashCode is not):
        /// FNV-1a over the seed and the key's UTF-16 units, finished with SplitMix64.
        /// </summary>
        public static double Unit(int seed, string key)
        {
            unchecked
            {
                var h = 14695981039346656037UL;
                h = (h ^ (uint)seed) * 1099511628211UL;
                foreach (var c in key)
                {
                    h = (h ^ c) * 1099511628211UL;
                }

                h ^= h >> 30;
                h *= 0xBF58476D1CE4E5B9UL;
                h ^= h >> 27;
                h *= 0x94D049BB133111EBUL;
                h ^= h >> 31;
                return (h >> 11) * (1d / (1UL << 53));
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.MarketPriceTests` と `-testFilter TerrariumDays.Tests.EconomyTests`
Expected: PASS（全件）。続けて EditMode 全体も PASS。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Wallet.cs Assets/Scripts/Core/EconomyTuning.cs Assets/Scripts/Core/MarketPrice.cs* Assets/Scripts/Core/EventDemand.cs* Assets/Tests/EditMode/EconomyTests.cs Assets/Tests/EditMode/MarketPriceTests.cs*
git commit -m "Add market price, event demand and shop prices; reject negative wallet amounts"
```

---

### Task 2: 所持品・装飾の枠・部屋のデータ

**Files:**
- Create: `Assets/Scripts/Core/Inventory.cs`
- Create: `Assets/Scripts/Core/DecorSlots.cs`
- Modify: `Assets/Scripts/Core/Colony.cs`
- Modify: `Assets/Scripts/Core/TerrariumArtLayout.cs`
- Modify: `Assets/Scripts/Core/ColonySaveService.cs`（孵卵器の数の読み込み1か所だけ）
- Test: `Assets/Tests/EditMode/InventoryAndDecorTests.cs`（新規）

**Interfaces:**
- Consumes: なし（Task 1 と独立だが順番に行う）
- Produces: `Inventory`（`Count(id)`・`Add(id, n)`・`TryTake(id)`・`Items`）、`DecorItems.All`（`(Id, Label)`）・`DecorItems.IsDecor`・`DecorItems.LabelOf`・`DecorItems.StarterDecorId`、`DecorResult`、`DecorSlots.SlotsFor/FreeSlots/Place/Remove/StatusText/SlotSummary`、`Cage.DecorIds`、`IncubatorModel`、`Colony.Inventory`・`Colony.Incubators`・`Colony.IncubatorCount`（読み取り専用）・`Colony.RemoveAnimal(PetState) → bool`、`TerrariumArtLayout.DecorSlotSpots`・`TerrariumArtLayout.PlacementFor(string decorId, int slot)`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/InventoryAndDecorTests.cs`:

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class InventoryAndDecorTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        private static Colony NewColony() => Colony.CreateNew(Now, new EconomyTuning(), new CareTuning(), new Random(1));

        [Test]
        public void Inventory_AddsAndTakes()
        {
            var inventory = new Inventory();
            inventory.Add("plant_01", 2);

            Assert.That(inventory.Count("plant_01"), Is.EqualTo(2));
            Assert.That(inventory.TryTake("plant_01"), Is.True);
            Assert.That(inventory.TryTake("plant_01"), Is.True);
            Assert.That(inventory.TryTake("plant_01"), Is.False);
            Assert.That(inventory.Count("plant_01"), Is.EqualTo(0));
            Assert.That(inventory.Items, Is.Empty);
        }

        [Test]
        public void Inventory_RejectsNegativeAmounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory().Add("rock_01", -1));
        }

        [Test]
        public void Inventory_ListsItemsInIdOrder()
        {
            var inventory = new Inventory();
            inventory.Add("water_dish_01", 1);
            inventory.Add("plant_01", 3);

            Assert.That(inventory.Items.Select(i => i.Key), Is.EqualTo(new[] { "plant_01", "water_dish_01" }));
        }

        [TestCase(CageSize.Small, 1)]
        [TestCase(CageSize.Standard, 2)]
        [TestCase(CageSize.Large, 3)]
        public void SlotsFor_FollowsTheCageSize(CageSize size, int slots)
        {
            Assert.That(DecorSlots.SlotsFor(size), Is.EqualTo(slots));
        }

        [Test]
        public void ANewColony_StartsWithARockInCageOneAndOneSimpleIncubator()
        {
            var colony = NewColony();

            Assert.That(colony.Cages[0].DecorIds, Is.EqualTo(new[] { "rock_01" }));
            Assert.That(colony.Inventory.Count("rock_01"), Is.EqualTo(0));
            Assert.That(colony.Incubators, Is.EqualTo(new[] { IncubatorModel.Simple }));
            Assert.That(colony.IncubatorCount, Is.EqualTo(1));
        }

        [Test]
        public void Place_TakesFromTheInventoryAndFillsASlot()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Small);
            colony.Inventory.Add("plant_01", 1);

            Assert.That(DecorSlots.Place(colony, cage, "plant_01"), Is.EqualTo(DecorResult.Ok));

            Assert.That(cage.DecorIds, Is.EqualTo(new[] { "plant_01" }));
            Assert.That(colony.Inventory.Count("plant_01"), Is.EqualTo(0));
        }

        [Test]
        public void Place_RefusesWhenAlreadyPlacedFullOrNotOwned()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Small);
            colony.Inventory.Add("plant_01", 2);
            colony.Inventory.Add("rock_01", 1);

            Assert.That(DecorSlots.Place(colony, cage, "plant_01"), Is.EqualTo(DecorResult.Ok));
            Assert.That(DecorSlots.Place(colony, cage, "plant_01"), Is.EqualTo(DecorResult.AlreadyPlaced));
            Assert.That(DecorSlots.Place(colony, cage, "rock_01"), Is.EqualTo(DecorResult.SlotsFull));
            Assert.That(colony.Inventory.Count("rock_01"), Is.EqualTo(1));

            var big = colony.AddCage(CageSize.Large);
            Assert.That(DecorSlots.Place(colony, big, "water_dish_01"), Is.EqualTo(DecorResult.NotOwned));
        }

        [Test]
        public void Remove_ReturnsTheItemToTheInventory()
        {
            var colony = NewColony();
            var cage = colony.Cages[0];

            Assert.That(DecorSlots.Remove(colony, cage, "rock_01"), Is.EqualTo(DecorResult.Ok));
            Assert.That(cage.DecorIds, Is.Empty);
            Assert.That(colony.Inventory.Count("rock_01"), Is.EqualTo(1));
            Assert.That(DecorSlots.Remove(colony, cage, "rock_01"), Is.EqualTo(DecorResult.NotPlaced));
        }

        [Test]
        public void StatusText_TellsWhatATapWillDo()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);

            Assert.That(DecorSlots.StatusText(colony, cage, "plant_01"), Is.EqualTo("未所持（ショップで購入）"));
            colony.Inventory.Add("plant_01", 1);
            Assert.That(DecorSlots.StatusText(colony, cage, "plant_01"), Is.EqualTo("所持1（タップで置く）"));
            DecorSlots.Place(colony, cage, "plant_01");
            Assert.That(DecorSlots.StatusText(colony, cage, "plant_01"), Is.EqualTo("置いています（タップで外す）"));
            Assert.That(DecorSlots.SlotSummary(cage), Is.EqualTo("装飾の枠 1/2"));

            colony.Inventory.Add("rock_01", 1);
            DecorSlots.Place(colony, cage, "rock_01");
            colony.Inventory.Add("water_dish_01", 1);
            Assert.That(DecorSlots.StatusText(colony, cage, "water_dish_01"), Is.EqualTo("所持1・枠がいっぱい"));
        }

        [Test]
        public void RemoveAnimal_FreesItsCageAndKeepsTheDecor()
        {
            var colony = NewColony();
            var pet = colony.Animals[0];
            var cage = colony.CageOf(pet);

            Assert.That(colony.RemoveAnimal(pet), Is.True);

            Assert.That(cage.IsEmpty, Is.True);
            Assert.That(colony.Animals, Is.Empty);
            Assert.That(cage.DecorIds, Is.EqualTo(new[] { "rock_01" }));
            Assert.That(colony.RemoveAnimal(pet), Is.False);
        }

        [Test]
        public void Incubators_AreCountedFromTheList()
        {
            var colony = NewColony();
            colony.Incubators.Add(IncubatorModel.Standard);

            Assert.That(colony.IncubatorCount, Is.EqualTo(2));
        }

        [Test]
        public void ArtLayout_PutsFloorDecorOnItsSlotSpotAndKeepsHangingDecorWhereItHangs()
        {
            var layout = new TerrariumArtLayout();

            var rock = layout.PlacementFor("rock_01", 1);
            Assert.That((rock.X, rock.Depth, rock.IsHanging), Is.EqualTo((0.85f, 0.6f, false)));
            Assert.That(rock.BodyWidthFraction, Is.EqualTo(layout.Decor["rock_01"].BodyWidthFraction));

            var lamp = layout.PlacementFor("heat_lamp_01", 1);
            Assert.That((lamp.X, lamp.IsHanging), Is.EqualTo((0.72f, true)));

            Assert.That(layout.PlacementFor("rock_01", 5).X, Is.EqualTo(layout.Decor["rock_01"].X), "no spot for that slot: keep its own place");
        }

        [Test]
        public void DecorItems_AllHaveArtAndLabels()
        {
            var layout = new TerrariumArtLayout();
            foreach (var decor in DecorItems.All)
            {
                Assert.That(layout.Decor.ContainsKey(decor.Id), Is.True, decor.Id);
                Assert.That(DecorItems.IsDecor(decor.Id), Is.True);
            }

            Assert.That(DecorItems.IsDecor("nest_box"), Is.False);
            Assert.That(DecorItems.LabelOf("driftwood_01"), Is.EqualTo("流木"));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.InventoryAndDecorTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/Inventory.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Owned items not in use (decor, nest boxes), counted by item id.</summary>
    public sealed class Inventory
    {
        private readonly SortedDictionary<string, int> counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int Count(string itemId) => counts.TryGetValue(itemId, out var n) ? n : 0;

        public void Add(string itemId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Use TryTake to remove items.");
            }

            if (amount == 0)
            {
                return;
            }

            counts[itemId] = Count(itemId) + amount;
        }

        public bool TryTake(string itemId)
        {
            var n = Count(itemId);
            if (n <= 0)
            {
                return false;
            }

            if (n == 1)
            {
                counts.Remove(itemId);
            }
            else
            {
                counts[itemId] = n - 1;
            }

            return true;
        }

        /// <summary>Items in id order (for the save file and lists).</summary>
        public IEnumerable<KeyValuePair<string, int>> Items => counts;
    }
}
```

`Assets/Scripts/Core/DecorSlots.cs`:

```csharp
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
```

`Assets/Scripts/Core/Colony.cs` の変更：
1. `CageSize` の後に：

```csharp
    public enum IncubatorModel
    {
        Simple,
        Standard,
        Luxury
    }
```

2. `Cage` に `public List<string> DecorIds { get; set; } = new List<string>();`（`AnimalId` の後）。
3. `Colony` のプロパティ：`public int IncubatorCount { get; set; } = 1;` を次に置き換え、`Inventory` を加える：

```csharp
        public Inventory Inventory { get; set; } = new Inventory();
        public List<IncubatorModel> Incubators { get; set; } = new List<IncubatorModel> { IncubatorModel.Simple };
        public int IncubatorCount => Incubators.Count;
```

4. `AddAnimal` の後に：

```csharp
        /// <summary>Takes the animal out of the room (sold); its cage becomes empty and keeps its decor.</summary>
        public bool RemoveAnimal(PetState pet)
        {
            if (pet == null || !Animals.Remove(pet))
            {
                return false;
            }

            foreach (var cage in Cages)
            {
                if (cage.AnimalId == pet.Id)
                {
                    cage.AnimalId = -1;
                }
            }

            return true;
        }
```

5. `CreateNew` の `var cage = colony.AddCage(CageSize.Standard);` の直後に `cage.DecorIds.Add(DecorItems.StarterDecorId);`。要約コメントを「one standard cage with a rock」に直す。

`Assets/Scripts/Core/ColonySaveService.cs`（`IncubatorCount` が読み取り専用になるため）：`FromSaveData` のオブジェクト初期化子から `IncubatorCount = data.incubatorCount,` を消し、`colony.Wallet.Money = data.money;` の直前に次を入れる（Task 5 で機種の一覧の保存に置き換える）：

```csharp
            colony.Incubators.Clear();
            for (var i = 0; i < data.incubatorCount; i++)
            {
                colony.Incubators.Add(IncubatorModel.Simple);
            }
```

`Assets/Scripts/Core/TerrariumArtLayout.cs`：`Decor` の後に追加：

```csharp
        /// <summary>
        /// Where a cage's decor slots stand on the floor, by slot index (X, Depth). Floor decor
        /// uses its slot's spot so up to three pieces do not overlap; hanging decor keeps its own X.
        /// </summary>
        public IReadOnlyList<(float X, float Depth)> DecorSlotSpots { get; set; } = new List<(float, float)>
        {
            (0.15f, 0.45f),
            (0.85f, 0.6f),
            (0.5f, 0.38f),
        };

        public DecorPlacement PlacementFor(string decorId, int slot)
        {
            var own = Decor[decorId];
            if (own.IsHanging || slot < 0 || slot >= DecorSlotSpots.Count)
            {
                return own;
            }

            var spot = DecorSlotSpots[slot];
            return DecorPlacement.OnFloor(own.Sprite, spot.X, spot.Depth, own.BodyWidthFraction);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.InventoryAndDecorTests`、続けて EditMode・PlayMode 全体。
Expected: PASS（既存のテストに影響しない。`ColonyTests` の `IncubatorCount` は1のまま）。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Inventory.cs* Assets/Scripts/Core/DecorSlots.cs* Assets/Scripts/Core/Colony.cs Assets/Scripts/Core/TerrariumArtLayout.cs Assets/Scripts/Core/ColonySaveService.cs Assets/Tests/EditMode/InventoryAndDecorTests.cs*
git commit -m "Add inventory, per-cage decor slots and incubator models to the colony"
```

---

### Task 3: ショップの在庫

**Files:**
- Create: `Assets/Scripts/Core/ShopStock.cs`
- Create: `Assets/Scripts/Core/ShopStockGenerator.cs`
- Modify: `Assets/Scripts/Core/Colony.cs`（`Shop`・新規ゲームで種）
- Test: `Assets/Tests/EditMode/ShopStockTests.cs`（新規）

**Interfaces:**
- Consumes: `Genotype`・`KnownGenetics`・`PersonalityTraits.Roll`・`SheddingModel.IntervalFor`・`StarterGenetics.MinPolygenic/MaxPolygenic`
- Produces: `ShopOffer`（`OfferId`・`Animal`）、`ShopStock`（`Seed`・`StockMonthIndex`・`NeverStocked`・`Offers`・`EnsureStocked(int monthIndex, DateTimeOffset nowUtc, CareTuning care) → bool`）、`ShopStockGenerator.MinOffers/MaxOffers/JuvenileChance/MaxDisclosedHets/SeedFor(int, int)/Generate(Random, DateTimeOffset, CareTuning)/Animal(Random, DateTimeOffset, CareTuning)`、`Colony.Shop`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/ShopStockTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class ShopStockTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();

        private IEnumerable<PetState> ManyAnimals(int months)
        {
            for (var month = 0; month < months; month++)
            {
                foreach (var offer in ShopStockGenerator.Generate(new Random(ShopStockGenerator.SeedFor(7, month)), Now, care))
                {
                    yield return offer.Animal;
                }
            }
        }

        [Test]
        public void Generate_OffersFourToSixNumberedAnimals()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var offers = ShopStockGenerator.Generate(new Random(seed), Now, care);

                Assert.That(offers.Count, Is.InRange(ShopStockGenerator.MinOffers, ShopStockGenerator.MaxOffers));
                Assert.That(offers.Select(o => o.OfferId), Is.EqualTo(Enumerable.Range(1, offers.Count)));
            }
        }

        [Test]
        public void Generate_IsTheSameForTheSameSeed()
        {
            string Describe(List<ShopOffer> offers) => string.Join("|", offers.Select(o =>
                $"{MorphNamer.FullName(o.Animal.Genotype, o.Animal.Known)}:{o.Animal.Stage}:{o.Animal.WeightGrams}:{o.Animal.Sex}:{o.Animal.Personality}"));

            Assert.That(Describe(ShopStockGenerator.Generate(new Random(3), Now, care)),
                Is.EqualTo(Describe(ShopStockGenerator.Generate(new Random(3), Now, care))));
        }

        [Test]
        public void Animals_AreMostlyBabiesWithUnknownSexAndAllHaveAnUnknownPersonality()
        {
            var animals = ManyAnimals(100).ToList();
            var babies = animals.Count(a => a.Stage == GrowthStage.Baby);

            Assert.That(babies / (double)animals.Count, Is.InRange(0.6d, 0.9d));
            foreach (var a in animals)
            {
                Assert.That(a.Stage, Is.EqualTo(GrowthStage.Baby).Or.EqualTo(GrowthStage.Juvenile));
                Assert.That(a.PersonalityKnown, Is.False);
                Assert.That(a.SexRevealed, Is.EqualTo(a.Stage == GrowthStage.Juvenile));
                Assert.That(a.Known.HetsUnknown, Is.False);
                if (a.Stage == GrowthStage.Baby)
                {
                    Assert.That(a.WeightGrams, Is.GreaterThanOrEqualTo(4d).And.LessThan(15d));
                }
                else
                {
                    Assert.That(a.WeightGrams, Is.GreaterThanOrEqualTo(16d).And.LessThan(40d));
                }
            }
        }

        [Test]
        public void Animals_RangeFromNormalToPopularMorphs()
        {
            var names = ManyAnimals(100).Select(a => MorphNamer.VisualName(a.Genotype)).Distinct().ToList();

            Assert.That(names, Has.Member("ノーマル"));
            Assert.That(names, Has.Member("レイプター"));
            Assert.That(names.Count, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void DisclosedHets_AreTruthfulAndAtMostTwo()
        {
            var sawProven = false;
            var sawPossible = false;
            foreach (var a in ManyAnimals(100))
            {
                var disclosed = 0;
                foreach (var gene in Genes.All)
                {
                    if (!Genes.IsRecessive(gene) || a.Genotype.Shows(gene))
                    {
                        continue;
                    }

                    var p = a.Known.HetProbability(gene);
                    if (p >= 1d)
                    {
                        Assert.That(a.Genotype.Copies(gene), Is.EqualTo(1));
                        sawProven = true;
                    }
                    else if (p > 0d)
                    {
                        Assert.That(p, Is.EqualTo(0.5d).Or.EqualTo(0.66d));
                        sawPossible = true;
                    }
                    else
                    {
                        Assert.That(a.Genotype.Copies(gene), Is.EqualTo(0));
                    }

                    if (p > 0d)
                    {
                        disclosed++;
                    }
                }

                Assert.That(disclosed, Is.LessThanOrEqualTo(ShopStockGenerator.MaxDisclosedHets));
            }

            Assert.That(sawProven && sawPossible, Is.True);
        }

        [Test]
        public void EnsureStocked_RestocksOnlyWhenTheMonthChanges()
        {
            var stock = new ShopStock { Seed = 11 };
            Assert.That(stock.StockMonthIndex, Is.EqualTo(ShopStock.NeverStocked));

            Assert.That(stock.EnsureStocked(0, Now, care), Is.True);
            var first = stock.Offers;
            Assert.That(stock.StockMonthIndex, Is.EqualTo(0));

            Assert.That(stock.EnsureStocked(0, Now, care), Is.False);
            Assert.That(stock.Offers, Is.SameAs(first));

            Assert.That(stock.EnsureStocked(1, Now, care), Is.True);
            Assert.That(stock.Offers, Is.Not.SameAs(first));
        }

        [Test]
        public void EnsureStocked_IsTheSameForTheSameSeedAndMonth()
        {
            var a = new ShopStock { Seed = 11 };
            var b = new ShopStock { Seed = 11 };
            a.EnsureStocked(4, Now, care);
            b.EnsureStocked(4, Now, care);

            Assert.That(a.Offers.Select(o => MorphNamer.FullName(o.Animal.Genotype, o.Animal.Known)),
                Is.EqualTo(b.Offers.Select(o => MorphNamer.FullName(o.Animal.Genotype, o.Animal.Known))));
            Assert.That(ShopStockGenerator.SeedFor(11, 4), Is.Not.EqualTo(ShopStockGenerator.SeedFor(11, 5)));
        }

        [Test]
        public void ANewColony_HasAShopThatHasNotBeenStocked()
        {
            var colony = Colony.CreateNew(Now, new EconomyTuning(), care, new Random(1));

            Assert.That(colony.Shop.StockMonthIndex, Is.EqualTo(ShopStock.NeverStocked));
            Assert.That(colony.Shop.Offers, Is.Empty);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.ShopStockTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/ShopStock.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>One animal for sale. <see cref="Animal"/> becomes the colony's own once bought.</summary>
    public sealed class ShopOffer
    {
        public int OfferId { get; set; }
        public PetState Animal { get; set; }
    }

    /// <summary>This game month's animals for sale (§9). Prices are computed live from the market.</summary>
    public sealed class ShopStock
    {
        public const int NeverStocked = -1;

        public int Seed { get; set; }

        public int StockMonthIndex { get; set; } = NeverStocked;

        public List<ShopOffer> Offers { get; set; } = new List<ShopOffer>();

        /// <summary>Replaces every offer when the game month differs from the stocked one. Same (Seed, month) → same stock.</summary>
        public bool EnsureStocked(int monthIndex, DateTimeOffset nowUtc, CareTuning care)
        {
            if (StockMonthIndex == monthIndex)
            {
                return false;
            }

            StockMonthIndex = monthIndex;
            Offers = ShopStockGenerator.Generate(new Random(ShopStockGenerator.SeedFor(Seed, monthIndex)), nowUtc, care);
            return true;
        }
    }
}
```

`Assets/Scripts/Core/ShopStockGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Makes a month's shop animals (§9): 4–6, mostly babies, from normal to popular morphs,
    /// with proven and possible hets disclosed truthfully. Sex is unknown for babies and the
    /// personality is unknown until kept a while after purchase.
    /// </summary>
    public static class ShopStockGenerator
    {
        public const int MinOffers = 4;
        public const int MaxOffers = 6;
        public const double JuvenileChance = 0.25d;
        public const double ProvenHetChance = 0.15d;
        public const double PossibleHetChance = 0.1d;
        public const int MaxDisclosedHets = 2;

        /// <summary>Weighted looks (weights sum to 100).</summary>
        private static readonly List<(int Weight, Func<Random, Genotype> Build)> Templates = new List<(int, Func<Random, Genotype>)>
        {
            (20, r => Plain(r)),
            (8, r => Plain(r).Set(GeneId.MackSnow, 1)),
            (8, r => Plain(r).Set(GeneId.TremperAlbino, 2)),
            (6, r => Genotype.Normal(Low(r), High(r))),
            (5, r => Plain(r).Set(GeneId.BellAlbino, 2)),
            (5, r => Plain(r).Set(GeneId.RainwaterAlbino, 2)),
            (6, r => Plain(r).Set(GeneId.Eclipse, 2)),
            (6, r => Plain(r).Set(GeneId.Blizzard, 2)),
            (5, r => Plain(r).Set(GeneId.MurphyPatternless, 2)),
            (6, r => Plain(r).Set(GeneId.WhiteAndYellow, 1)),
            (5, r => Plain(r).Set(GeneId.MackSnow, 2)),
            (5, r => Plain(r).Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2)),
            (5, r => Plain(r).Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2)),
            (5, r => Plain(r).Set(GeneId.MackSnow, 1).Set(GeneId.TremperAlbino, 2)),
            (5, r => Genotype.Normal(75d + r.NextDouble() * 20d, High(r))),
        };

        public static int SeedFor(int shopSeed, int monthIndex) => unchecked(shopSeed * 31 + monthIndex * 7919 + 17);

        public static List<ShopOffer> Generate(Random random, DateTimeOffset nowUtc, CareTuning care)
        {
            var count = random.Next(MinOffers, MaxOffers + 1);
            var offers = new List<ShopOffer>();
            for (var i = 0; i < count; i++)
            {
                offers.Add(new ShopOffer { OfferId = i + 1, Animal = Animal(random, nowUtc, care) });
            }

            return offers;
        }

        public static PetState Animal(Random random, DateTimeOffset nowUtc, CareTuning care)
        {
            var genotype = PickGenotype(random);
            var known = DiscloseHets(genotype, random);
            var juvenile = random.NextDouble() < JuvenileChance;
            var stage = juvenile ? GrowthStage.Juvenile : GrowthStage.Baby;
            var weight = juvenile ? 16d + random.NextDouble() * 14d : 4d + random.NextDouble() * 8d;
            // One real day is one game month, so an age in months is that many real days back.
            var ageMonths = juvenile ? 4d + random.NextDouble() * 3d : 1d + random.NextDouble() * 2d;
            var sex = random.NextDouble() < 0.5d ? Sex.Female : Sex.Male;
            var personality = PersonalityTraits.Roll(random);
            return new PetState
            {
                Name = string.Empty,
                Sex = sex,
                SexRevealed = juvenile,
                Personality = personality,
                PersonalityKnown = false,
                Genotype = genotype,
                Known = known,
                WeightGrams = weight,
                Stage = stage,
                HatchedAtUtc = nowUtc.AddDays(-ageMonths),
                LastSavedAtUtc = nowUtc,
                LastShedAtUtc = nowUtc,
                NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(stage, care),
            };
        }

        private static Genotype PickGenotype(Random random)
        {
            var total = 0;
            foreach (var template in Templates)
            {
                total += template.Weight;
            }

            var roll = random.Next(total);
            foreach (var template in Templates)
            {
                if (roll < template.Weight)
                {
                    return template.Build(random);
                }

                roll -= template.Weight;
            }

            return Templates[0].Build(random);
        }

        /// <summary>Per hidden recessive gene: 15% proven het, 10% possible het (50% or 66%, carried with that chance). At most two shown.</summary>
        private static KnownGenetics DiscloseHets(Genotype genotype, Random random)
        {
            var known = new KnownGenetics();
            var disclosed = 0;
            foreach (var gene in Genes.All)
            {
                if (!Genes.IsRecessive(gene) || genotype.Shows(gene))
                {
                    continue;
                }

                var roll = random.NextDouble();
                if (disclosed >= MaxDisclosedHets)
                {
                    continue;
                }

                if (roll < ProvenHetChance)
                {
                    genotype.Set(gene, 1);
                    known.SetHet(gene, 1d);
                    disclosed++;
                }
                else if (roll < ProvenHetChance + PossibleHetChance)
                {
                    var p = random.NextDouble() < 0.5d ? 0.5d : 0.66d;
                    if (random.NextDouble() < p)
                    {
                        genotype.Set(gene, 1);
                    }

                    known.SetHet(gene, p);
                    disclosed++;
                }
            }

            return known;
        }

        private static Genotype Plain(Random r) => Genotype.Normal(Low(r), Low(r));

        private static double Low(Random r) =>
            StarterGenetics.MinPolygenic + r.NextDouble() * (StarterGenetics.MaxPolygenic - StarterGenetics.MinPolygenic);

        private static double High(Random r) => 65d + r.NextDouble() * 30d;
    }
}
```

`Assets/Scripts/Core/Colony.cs`：プロパティに `public ShopStock Shop { get; set; } = new ShopStock();` を加え、`CreateNew` の `colony.AddAnimal(pet, cage);` の直後（`return colony;` の前）に `colony.Shop.Seed = random.Next();` を入れる（スターター遺伝子の後に引くので既存の乱数の並びは変わらない）。

- [ ] **Step 4: Run the tests to verify they pass**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.ShopStockTests`、続けて EditMode 全体。
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/ShopStock.cs* Assets/Scripts/Core/ShopStockGenerator.cs* Assets/Scripts/Core/Colony.cs Assets/Tests/EditMode/ShopStockTests.cs*
git commit -m "Generate a seeded monthly shop stock of mostly baby animals with honest hets"
```

---

### Task 4: 売り買いと性格の判明

**Files:**
- Create: `Assets/Scripts/Core/ShopCatalog.cs`
- Create: `Assets/Scripts/Core/ShopService.cs`
- Create: `Assets/Scripts/Core/PersonalityReveal.cs`
- Modify: `Assets/Scripts/Core/PetState.cs`（`PersonalityRevealAtUtc`）
- Modify: `Assets/Scripts/Core/CareTuning.cs`（`PersonalityRevealGameDays`）
- Test: `Assets/Tests/EditMode/ShopServiceTests.cs`（新規）

**Interfaces:**
- Consumes: Task 1〜3 のすべて
- Produces: `ShopItemKind`・`ShopItem`（`Id`・`Label`・`Price`・`Kind`・`CageSize`・`Incubator`・`UsableFromPhase`）、`ShopCatalog.NestBoxId/Items(EconomyTuning)/Find(string, EconomyTuning)`、`ShopResult`（`Ok, NotFound, NotEnoughMoney, NoEmptyCage, NoRackSpace, RackLimit`）、`ShopService(EconomyTuning, CareTuning)`・`PriceOf(ShopOffer)`・`MarketOf(PetState)`・`WholesalePriceOf(PetState)`・`BuyAnimal(Colony, int offerId, DateTimeOffset)`・`BuyItem(Colony, string itemId, DateTimeOffset)`・`Wholesale(Colony, int animalId, DateTimeOffset)`、`PetState.PersonalityRevealAtUtc`、`CareTuning.PersonalityRevealGameDays`、`PersonalityReveal.ApplyIfDue(PetState, DateTimeOffset) → bool`・`PersonalityReveal.Message(PetState)`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/ShopServiceTests.cs`:

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class ShopServiceTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        private readonly EconomyTuning economy = new EconomyTuning();
        private readonly CareTuning care = new CareTuning();
        private ShopService shop;

        [SetUp]
        public void SetUp() => shop = new ShopService(economy, care);

        private Colony ColonyWithOffer(out ShopOffer offer)
        {
            var colony = Colony.CreateNew(Now, economy, care, new Random(1));
            offer = new ShopOffer { OfferId = 7, Animal = ShopStockGenerator.Animal(new Random(5), Now, care) };
            colony.Shop.Offers.Add(offer);
            return colony;
        }

        [Test]
        public void PriceOf_IsTheMarketTimesOnePointTwo()
        {
            ColonyWithOffer(out var offer);

            Assert.That(shop.PriceOf(offer), Is.EqualTo(MarketPrice.ShopPrice(MarketPrice.For(offer.Animal), economy)));
        }

        [Test]
        public void BuyAnimal_PaysPutsItInTheFirstEmptyCageAndHidesItsPersonality()
        {
            var colony = ColonyWithOffer(out var offer);
            var empty = colony.AddCage(CageSize.Small);
            colony.AddCage(CageSize.Large);
            colony.Wallet.Money = 1_000_000;
            var price = shop.PriceOf(offer);

            Assert.That(shop.BuyAnimal(colony, 7, Now), Is.EqualTo(ShopResult.Ok));

            var pet = colony.AnimalIn(empty);
            Assert.That(pet, Is.SameAs(offer.Animal));
            Assert.That(pet.Name, Is.EqualTo("レオパ2"));
            Assert.That(pet.PersonalityKnown, Is.False);
            Assert.That(pet.PersonalityRevealAtUtc, Is.EqualTo(Now + GameCalendar.RealTimeFor(7d)));
            Assert.That(pet.LastSavedAtUtc, Is.EqualTo(Now));
            Assert.That(colony.Wallet.Money, Is.EqualTo(1_000_000 - price));
            var entry = colony.Wallet.Ledger.Last();
            Assert.That((entry.Category, entry.Amount), Is.EqualTo((LedgerCategory.AnimalPurchase, -price)));
            StringAssert.StartsWith("レオパ2（", entry.Note);
            Assert.That(colony.Shop.Offers, Is.Empty);
        }

        [Test]
        public void BuyAnimal_WithoutAnEmptyCage_ChangesNothing()
        {
            var colony = ColonyWithOffer(out _);
            colony.Wallet.Money = 1_000_000;

            Assert.That(shop.BuyAnimal(colony, 7, Now), Is.EqualTo(ShopResult.NoEmptyCage));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(colony.Shop.Offers, Has.Count.EqualTo(1));
            Assert.That(colony.Wallet.Money, Is.EqualTo(1_000_000));
        }

        [Test]
        public void BuyAnimal_WithoutEnoughMoney_ChangesNothing()
        {
            var colony = ColonyWithOffer(out var offer);
            colony.AddCage(CageSize.Standard);
            colony.Wallet.Money = shop.PriceOf(offer) - 1;

            Assert.That(shop.BuyAnimal(colony, 7, Now), Is.EqualTo(ShopResult.NotEnoughMoney));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(colony.Shop.Offers, Has.Count.EqualTo(1));
            Assert.That(colony.Wallet.Ledger, Is.Empty);
        }

        [Test]
        public void BuyAnimal_WithExactlyEnoughMoney_LeavesZero()
        {
            var colony = ColonyWithOffer(out var offer);
            colony.AddCage(CageSize.Standard);
            colony.Wallet.Money = shop.PriceOf(offer);

            Assert.That(shop.BuyAnimal(colony, 7, Now), Is.EqualTo(ShopResult.Ok));
            Assert.That(colony.Wallet.Money, Is.EqualTo(0));
        }

        [Test]
        public void BuyAnimal_AnOfferThatIsGone_IsNotFound()
        {
            var colony = ColonyWithOffer(out _);

            Assert.That(shop.BuyAnimal(colony, 99, Now), Is.EqualTo(ShopResult.NotFound));
        }

        [Test]
        public void BuyItem_ACageNeedsRoomOnARack()
        {
            var colony = ColonyWithOffer(out _);

            for (var i = 0; i < 3; i++)
            {
                Assert.That(shop.BuyItem(colony, "cage_large", Now), Is.EqualTo(ShopResult.Ok));
            }

            Assert.That(shop.BuyItem(colony, "cage_small", Now), Is.EqualTo(ShopResult.NoRackSpace));
            Assert.That(colony.Cages.Count, Is.EqualTo(4));
            Assert.That(colony.Cages[3].Size, Is.EqualTo(CageSize.Large));
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000 - 3 * 10000));
            Assert.That(colony.Wallet.Ledger.Last().Category, Is.EqualTo(LedgerCategory.Purchase));
            Assert.That(colony.Wallet.Ledger.Last().Note, Is.EqualTo("大型ケージを購入"));
        }

        [Test]
        public void BuyItem_RacksStopAtTheLimit()
        {
            var colony = ColonyWithOffer(out _);
            colony.Wallet.Money = 1_000_000;

            for (var i = 1; i < economy.MaxRacks; i++)
            {
                Assert.That(shop.BuyItem(colony, "rack", Now), Is.EqualTo(ShopResult.Ok));
            }

            Assert.That(colony.RackCount, Is.EqualTo(economy.MaxRacks));
            Assert.That(shop.BuyItem(colony, "rack", Now), Is.EqualTo(ShopResult.RackLimit));
        }

        [Test]
        public void BuyItem_DecorAndNestBoxesGoToTheInventory()
        {
            var colony = ColonyWithOffer(out _);

            Assert.That(shop.BuyItem(colony, "driftwood_01", Now), Is.EqualTo(ShopResult.Ok));
            Assert.That(shop.BuyItem(colony, ShopCatalog.NestBoxId, Now), Is.EqualTo(ShopResult.Ok));

            Assert.That(colony.Inventory.Count("driftwood_01"), Is.EqualTo(1));
            Assert.That(colony.Inventory.Count(ShopCatalog.NestBoxId), Is.EqualTo(1));
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000 - 2000 - 1500));
        }

        [Test]
        public void BuyItem_AnIncubatorAddsItsModel()
        {
            var colony = ColonyWithOffer(out _);

            Assert.That(shop.BuyItem(colony, "incubator_standard", Now), Is.EqualTo(ShopResult.Ok));

            Assert.That(colony.Incubators, Is.EqualTo(new[] { IncubatorModel.Simple, IncubatorModel.Standard }));
            Assert.That(shop.BuyItem(colony, "incubator_luxury", Now), Is.EqualTo(ShopResult.NotEnoughMoney));
        }

        [Test]
        public void BuyItem_AnUnknownId_IsNotFound()
        {
            Assert.That(shop.BuyItem(ColonyWithOffer(out _), "golden_cage", Now), Is.EqualTo(ShopResult.NotFound));
        }

        [Test]
        public void Wholesale_PaysFortyPercentOfTheMarketAndFreesTheCage()
        {
            var colony = ColonyWithOffer(out _);
            var second = colony.AddAnimal(new PetState { Name = "レオパ2", Stage = GrowthStage.Juvenile, PersonalityKnown = false }, colony.AddCage(CageSize.Standard));
            var cage = colony.CageOf(second);
            var pay = MarketPrice.WholesalePrice(MarketPrice.For(second), economy);

            Assert.That(shop.WholesalePriceOf(second), Is.EqualTo(pay));
            Assert.That(shop.Wholesale(colony, second.Id, Now), Is.EqualTo(ShopResult.Ok));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(cage.IsEmpty, Is.True);
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000 + pay));
            var entry = colony.Wallet.Ledger.Last();
            Assert.That((entry.Category, entry.Amount), Is.EqualTo((LedgerCategory.Wholesale, pay)));
            StringAssert.StartsWith("レオパ2（", entry.Note);
        }

        [Test]
        public void Wholesale_TheLastAnimalCanBeSoldLeavingAnEmptyRoom()
        {
            var colony = ColonyWithOffer(out _);
            var cage = colony.Cages[0];

            Assert.That(shop.Wholesale(colony, colony.Animals[0].Id, Now), Is.EqualTo(ShopResult.Ok));

            Assert.That(colony.Animals, Is.Empty);
            Assert.That(cage.IsEmpty, Is.True);
            Assert.That(colony.OccupiedCages(), Is.Empty);
            Assert.That(shop.Wholesale(colony, 99, Now), Is.EqualTo(ShopResult.NotFound));
        }

        [Test]
        public void AfterSellingEverything_AnAnimalCanBeBoughtBack()
        {
            var colony = ColonyWithOffer(out var offer);
            shop.Wholesale(colony, colony.Animals[0].Id, Now);
            colony.Wallet.Money = 1_000_000;

            Assert.That(shop.BuyAnimal(colony, offer.OfferId, Now), Is.EqualTo(ShopResult.Ok));
            Assert.That(colony.AnimalIn(colony.Cages[0]), Is.SameAs(offer.Animal));
        }

        [Test]
        public void BuyingThenSelling_LeavesBothInTheLedger()
        {
            var colony = ColonyWithOffer(out var offer);
            colony.AddCage(CageSize.Standard);
            colony.Wallet.Money = 1_000_000;
            var price = shop.PriceOf(offer);
            shop.BuyAnimal(colony, 7, Now);
            var pay = shop.WholesalePriceOf(offer.Animal);

            shop.Wholesale(colony, offer.Animal.Id, Now.AddMinutes(5));

            Assert.That(colony.Wallet.Ledger.Select(e => (e.Category, e.Amount)), Is.EqualTo(new[]
            {
                (LedgerCategory.AnimalPurchase, -price),
                (LedgerCategory.Wholesale, pay),
            }));
            Assert.That(colony.Wallet.Money, Is.EqualTo(1_000_000 - price + pay));
        }

        [Test]
        public void Catalog_ListsEverySpecItem()
        {
            var items = ShopCatalog.Items(economy);

            Assert.That(items.Select(i => i.Id), Is.EqualTo(new[]
            {
                "cage_small", "cage_standard", "cage_large", "rack",
                "rock_01", "plant_01", "water_dish_01", "heat_lamp_01", "driftwood_01",
                ShopCatalog.NestBoxId, "incubator_standard", "incubator_luxury",
            }));
            Assert.That(ShopCatalog.Find("cage_standard", economy).Price, Is.EqualTo(6000));
            Assert.That(ShopCatalog.Find("heat_lamp_01", economy).Price, Is.EqualTo(3000));
            Assert.That(ShopCatalog.Find("incubator_luxury", economy).Price, Is.EqualTo(40000));
            Assert.That(ShopCatalog.Find("incubator_luxury", economy).UsableFromPhase, Is.EqualTo(5));
            Assert.That(ShopCatalog.Find(ShopCatalog.NestBoxId, economy).UsableFromPhase, Is.EqualTo(4));
            Assert.That(ShopCatalog.Find("rack", economy).UsableFromPhase, Is.EqualTo(0));
        }

        [Test]
        public void PersonalityReveal_HappensOnceWhenDue()
        {
            var pet = new PetState { Name = "レオパ3", Personality = Personality.Curious, PersonalityKnown = false, PersonalityRevealAtUtc = Now };

            Assert.That(PersonalityReveal.ApplyIfDue(pet, Now.AddMinutes(-1)), Is.False);
            Assert.That(PersonalityReveal.ApplyIfDue(pet, Now), Is.True);
            Assert.That(pet.PersonalityKnown, Is.True);
            Assert.That(pet.PersonalityRevealAtUtc, Is.Null);
            Assert.That(PersonalityReveal.ApplyIfDue(pet, Now.AddDays(1)), Is.False);
            Assert.That(PersonalityReveal.Message(pet), Is.EqualTo("レオパ3の性格は「好奇心旺盛」のようです"));
        }

        [Test]
        public void PersonalityReveal_WithoutADate_DoesNothing()
        {
            var pet = new PetState { PersonalityKnown = false };

            Assert.That(PersonalityReveal.ApplyIfDue(pet, Now), Is.False);
            Assert.That(pet.PersonalityKnown, Is.False);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.ShopServiceTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/PetState.cs`：`PersonalityKnown` の後に：

```csharp
        /// <summary>For a bought animal: when its personality becomes known (game clock). Null otherwise.</summary>
        public DateTimeOffset? PersonalityRevealAtUtc { get; set; }
```

`Assets/Scripts/Core/CareTuning.cs`：最後のプロパティの後に：

```csharp
        /// <summary>A bought animal's personality shows after it has been kept this many game days (§5.2, §9).</summary>
        public double PersonalityRevealGameDays { get; set; } = 7d;
```

`Assets/Scripts/Core/PersonalityReveal.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>A bought animal's personality becomes known a while after purchase (§5.2, §9).</summary>
    public static class PersonalityReveal
    {
        public static bool ApplyIfDue(PetState pet, DateTimeOffset nowUtc)
        {
            if (pet.PersonalityKnown || !pet.PersonalityRevealAtUtc.HasValue || pet.PersonalityRevealAtUtc.Value > nowUtc)
            {
                return false;
            }

            pet.PersonalityKnown = true;
            pet.PersonalityRevealAtUtc = null;
            return true;
        }

        public static string Message(PetState pet) =>
            $"{pet.Name}の性格は「{PersonalityTraits.Label(pet.Personality)}」のようです";
    }
}
```

`Assets/Scripts/Core/ShopCatalog.cs`:

```csharp
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
```

`Assets/Scripts/Core/ShopService.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    public enum ShopResult
    {
        Ok,
        NotFound,
        NotEnoughMoney,
        NoEmptyCage,
        NoRackSpace,
        RackLimit
    }

    /// <summary>
    /// Buying animals and supplies and wholesaling animals (§9). Every yen goes through the
    /// wallet so the ledger records it. Prices come from the market (§10.1) with no event demand.
    /// </summary>
    public sealed class ShopService
    {
        private readonly EconomyTuning economy;
        private readonly CareTuning care;

        public ShopService(EconomyTuning economy, CareTuning care)
        {
            this.economy = economy;
            this.care = care;
        }

        public long MarketOf(PetState pet) => MarketPrice.For(pet, EventDemand.None);

        public long PriceOf(ShopOffer offer) => MarketPrice.ShopPrice(MarketOf(offer.Animal), economy);

        public long WholesalePriceOf(PetState pet) => MarketPrice.WholesalePrice(MarketOf(pet), economy);

        public ShopResult BuyAnimal(Colony colony, int offerId, DateTimeOffset nowUtc)
        {
            var offer = colony.Shop.Offers.Find(o => o.OfferId == offerId);
            if (offer == null)
            {
                return ShopResult.NotFound;
            }

            var cage = colony.Cages.Find(c => c.IsEmpty);
            if (cage == null)
            {
                return ShopResult.NoEmptyCage;
            }

            var pet = offer.Animal;
            var name = $"レオパ{colony.NextAnimalId}";
            var note = $"{name}（{MorphNamer.FullName(pet.Genotype, pet.Known)}）を購入";
            if (!colony.Wallet.TrySpend(PriceOf(offer), LedgerCategory.AnimalPurchase, note, nowUtc))
            {
                return ShopResult.NotEnoughMoney;
            }

            colony.Shop.Offers.Remove(offer);
            pet.Name = name;
            pet.LastSavedAtUtc = nowUtc;
            pet.LastShedAtUtc = nowUtc;
            pet.NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(pet.Stage, care);
            pet.PersonalityKnown = false;
            pet.PersonalityRevealAtUtc = nowUtc + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays);
            colony.AddAnimal(pet, cage);
            return ShopResult.Ok;
        }

        public ShopResult BuyItem(Colony colony, string itemId, DateTimeOffset nowUtc)
        {
            var item = ShopCatalog.Find(itemId, economy);
            if (item == null)
            {
                return ShopResult.NotFound;
            }

            if (item.Kind == ShopItemKind.Cage && !colony.CanAddCage)
            {
                return ShopResult.NoRackSpace;
            }

            if (item.Kind == ShopItemKind.Rack && colony.RackCount >= economy.MaxRacks)
            {
                return ShopResult.RackLimit;
            }

            if (!colony.Wallet.TrySpend(item.Price, LedgerCategory.Purchase, $"{item.Label}を購入", nowUtc))
            {
                return ShopResult.NotEnoughMoney;
            }

            switch (item.Kind)
            {
                case ShopItemKind.Cage:
                    colony.AddCage(item.CageSize);
                    break;
                case ShopItemKind.Rack:
                    colony.RackCount++;
                    break;
                case ShopItemKind.Incubator:
                    colony.Incubators.Add(item.Incubator);
                    break;
                default:
                    colony.Inventory.Add(item.Id, 1);
                    break;
            }

            return ShopResult.Ok;
        }

        /// <summary>Sells one of the player's animals to the shop at 40% of the market. Selling the last one is allowed (§ planner: 0 animals).</summary>
        public ShopResult Wholesale(Colony colony, int animalId, DateTimeOffset nowUtc)
        {
            var pet = colony.AnimalById(animalId);
            if (pet == null)
            {
                return ShopResult.NotFound;
            }

            var pay = WholesalePriceOf(pet);
            colony.RemoveAnimal(pet);
            colony.Wallet.Earn(pay, LedgerCategory.Wholesale, $"{pet.Name}（{MorphNamer.FullName(pet.Genotype, pet.Known)}）を卸売り", nowUtc);
            return ShopResult.Ok;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.ShopServiceTests`、続けて EditMode 全体。
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/ShopCatalog.cs* Assets/Scripts/Core/ShopService.cs* Assets/Scripts/Core/PersonalityReveal.cs* Assets/Scripts/Core/PetState.cs Assets/Scripts/Core/CareTuning.cs Assets/Tests/EditMode/ShopServiceTests.cs*
git commit -m "Add shop purchases, wholesale and delayed personality reveal, all through the ledger"
```

---

### Task 5: セッションとセーブ（入荷・判明・保存・装飾の移行・壊れたセーブ）

**Files:**
- Modify: `Assets/Scripts/Core/ColonySaveData.cs`
- Modify: `Assets/Scripts/Core/ColonySaveService.cs`
- Modify: `Assets/Scripts/Core/ColonySession.cs`
- Modify: `Assets/Scripts/UI/TerrariumView.cs`（`LoadColony` と `HandleReport` の通知だけ）
- Test: `Assets/Tests/EditMode/ColonySaveServiceTests.cs`・`Assets/Tests/EditMode/ColonySessionTests.cs`（追加）

**Interfaces:**
- Consumes: Task 2〜4
- Produces: `ColonySaveService.CurrentInventoryVersion`（=1）・`LastLoadMovedDecor`・`LastLoadBackupFailed`、`ColonySession.SaveBlocked`・`DecorMovedToInventory`、`ColonyTickReport.PersonalityReveals`（`List<PetState>`）・`Restocked`（`bool`、`HasEvents` に含む）、UI の通知文 `TerrariumView.DecorMovedMessage`・`TerrariumView.SaveBlockedMessage`（`public const string`）

**保存の形（JsonUtility、すべてフィールド）：**

```csharp
    // ColonySaveData に追加
        public int inventoryVersion;
        public List<InventorySaveData> inventory = new List<InventorySaveData>();
        public List<string> incubators = new List<string>();
        public int shopSeed;
        public bool shopStocked;
        public int shopMonthIndex;
        public List<ShopOfferSaveData> shopOffers = new List<ShopOfferSaveData>();

    // AnimalSaveData に追加
        public string personalityRevealAtUtc;

    // CageSaveData に追加
        public List<string> decorIds = new List<string>();

    [Serializable]
    public sealed class InventorySaveData
    {
        public string id;
        public int count;
    }

    [Serializable]
    public sealed class ShopOfferSaveData
    {
        public int offerId;
        public AnimalSaveData animal;
    }
```

`selectedDecorId`・`unlockedDecorIds` は読み込み（移行）のために `AnimalSaveData` に残す。

**読み込み・保存の規則：**
1. 個体の変換を2つの関数に切り出す：`private static AnimalSaveData ToAnimalSaveData(PetState a)`（今の `ToSaveData` のループの中身）と `private PetState FromAnimalSaveData(AnimalSaveData a, DateTimeOffset nowUtc, System.Random random, bool owned)`（今の `FromSaveData` のループの中身）。ショップの在庫の個体も同じ関数で読み書きする。
2. `personalityRevealAtUtc`：空なら null。`owned` が true で `!PersonalityKnown` かつ null のときは `nowUtc + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays)`（判明しないまま残らないように）。ショップの個体（`owned: false`）には入れない。
3. 孵卵器：`data.incubators` に1つ以上あれば各文字列を `IncubatorModel` に（読めなければ `Simple`）。空なら `incubatorCount` の数だけ `Simple`（Task 2 の仮の処理を置き換える）。保存は `incubators` と `incubatorCount` の両方を書く。
4. 所持品とケージの装飾：`data.inventoryVersion >= CurrentInventoryVersion` なら `inventory` と各ケージの `decorIds`（null は空）を読む。そうでなければ移行：各個体の `unlockedDecorIds`（null なら `{ "rock_01" }`）のうち `DecorItems.IsDecor` のものを1つずつ `Inventory.Add`、ケージの装飾は空、`LastLoadMovedDecor = true`。保存は常に `inventoryVersion = CurrentInventoryVersion`。
5. 旧形式（スキーマ1・2）の移行（`MigrateLegacy`）：`data.unlockedDecorIds`（null なら岩）を同じように所持品に入れ、ケージ1は空、`LastLoadMovedDecor = true`、`colony.Shop.Seed = random.Next()`（個体を作った後に引く）。
6. ショップ：`Shop.Seed = data.shopSeed`、`StockMonthIndex = data.shopStocked ? data.shopMonthIndex : ShopStock.NeverStocked`、`Offers` は `shopOffers` から（`animal` が null の行は飛ばす）。保存は `shopStocked = StockMonthIndex != NeverStocked`。
7. 壊れたセーブ：`catch` の中の控えのコピーが失敗したら `LastLoadBackupFailed = true`（`LoadOrCreate` の最初で false に戻す）。
8. スキーマ番号のない JSON：`probe.schemaVersion <= 0` かつ JSON が `"lastSavedAtUtc"` と `"growthStage"` の両方を含まないときは `throw new InvalidDataException("No schema version and not a schema-1 pet save.");`（壊れたセーブの扱いになる）。

**セッション（`ColonySession`）：**

```csharp
        public bool SaveBlocked { get; private set; }
        public bool DecorMovedToInventory { get; private set; }

        // Load(): LoadOrCreate の直後
            DecorMovedToInventory = saveService.LastLoadMovedDecor;
            SaveBlocked = saveService.LastLoadBackupFailed;
        // Load() の最後の saveService.Save を if (!SaveBlocked) で囲む。
        // Save(): Resync の後、if (SaveBlocked) return; してから保存。

        // ApplyUntil(): 各個体の処理の最後（SexRevealed の後）
                if (PersonalityReveal.ApplyIfDue(pet, targetUtc))
                {
                    report.PersonalityReveals.Add(pet);
                }
        // ApplyUntil(): 電気代の後
            report.Restocked = Colony.Shop.EnsureStocked(Calendar.MonthIndexAt(targetUtc), targetUtc, care);
```

`ColonyTickReport` に `public List<PetState> PersonalityReveals { get; } = new List<PetState>();` と `public bool Restocked { get; set; }`、`HasEvents` に `|| PersonalityReveals.Count > 0 || Restocked` を加える。

**画面（`TerrariumView`）：**

```csharp
        public const string DecorMovedMessage = "装飾を所持品に移しました。ケージの「そうしょく」から置けます";
        public const string SaveBlockedMessage = "セーブデータを読めず、控えも作れませんでした。空き容量を確かめてアプリを開き直してください";
```

`LoadColony` の最後：`session.SaveBlocked` なら `ShowFeedback(SaveBlockedMessage)`、そうでなく `session.DecorMovedToInventory` なら `ShowFeedback(DecorMovedMessage)`（`Migrated` の通知より後に呼び、上書きされてよい）。`HandleReport`：選択中の個体が `report.PersonalityReveals` にあれば `ShowFeedback(PersonalityReveal.Message(state))`（雌雄の判明の通知の後）。

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/ColonySaveServiceTests.cs`：先頭に `using System.Linq;` を加え（`UnityEngine` は `using` しない。`Random` が `UnityEngine.Random` とあいまいになるため、`LogType` は完全名で書く）、既存の `ASchemaTwoSave_IsMigratedIntoCageOneAndBackedUp` の最後に次を加える：

```csharp
            Assert.That(service.LastLoadMovedDecor, Is.True);
            Assert.That(colony.Inventory.Count("rock_01"), Is.EqualTo(1));
            Assert.That(colony.Inventory.Count("plant_01"), Is.EqualTo(1));
            Assert.That(colony.Cages[0].DecorIds, Is.Empty);
```

新しいテストを加える：

```csharp
        [Test]
        public void RoundTrip_KeepsShopInventoryDecorIncubatorsAndRevealTime()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            colony.Shop.Seed = 42;
            colony.Shop.EnsureStocked(3, Now, care);
            colony.Inventory.Add("plant_01", 2);
            colony.Inventory.Add(ShopCatalog.NestBoxId, 1);
            var cage = colony.AddCage(CageSize.Large);
            cage.DecorIds.Add("driftwood_01");
            colony.Incubators.Add(IncubatorModel.Luxury);
            var pet = colony.Animals[0];
            pet.PersonalityKnown = false;
            pet.PersonalityRevealAtUtc = Now.AddHours(3);

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            Assert.That(service.LastLoadMovedDecor, Is.False);
            Assert.That(loaded.Shop.Seed, Is.EqualTo(42));
            Assert.That(loaded.Shop.StockMonthIndex, Is.EqualTo(3));
            Assert.That(loaded.Shop.Offers.Select(o => o.OfferId), Is.EqualTo(colony.Shop.Offers.Select(o => o.OfferId)));
            Assert.That(loaded.Shop.Offers.Select(o => MorphNamer.FullName(o.Animal.Genotype, o.Animal.Known)),
                Is.EqualTo(colony.Shop.Offers.Select(o => MorphNamer.FullName(o.Animal.Genotype, o.Animal.Known))));
            Assert.That(loaded.Shop.Offers.All(o => !o.Animal.PersonalityKnown && o.Animal.PersonalityRevealAtUtc == null), Is.True);
            Assert.That(loaded.Inventory.Count("plant_01"), Is.EqualTo(2));
            Assert.That(loaded.Inventory.Count(ShopCatalog.NestBoxId), Is.EqualTo(1));
            Assert.That(loaded.Cages[0].DecorIds, Is.EqualTo(new[] { "rock_01" }));
            Assert.That(loaded.Cages[1].DecorIds, Is.EqualTo(new[] { "driftwood_01" }));
            Assert.That(loaded.Incubators, Is.EqualTo(new[] { IncubatorModel.Simple, IncubatorModel.Luxury }));
            Assert.That(loaded.Animals[0].PersonalityRevealAtUtc, Is.EqualTo(Now.AddHours(3)));
        }

        [Test]
        public void AColonyWithNoAnimals_SavesAndLoads()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            colony.RemoveAnimal(colony.Animals[0]);

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(loaded.Animals, Is.Empty);
            Assert.That(loaded.Cages, Has.Count.EqualTo(1));
            Assert.That(loaded.Cages[0].IsEmpty, Is.True);
            Assert.That(loaded.NextAnimalId, Is.EqualTo(2));
        }

        [Test]
        public void ANeverStockedShop_StaysNeverStockedAfterAReload()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            service.Save(path, colony);

            Assert.That(service.LoadOrCreate(path, Now, new Random(1)).Shop.StockMonthIndex, Is.EqualTo(ShopStock.NeverStocked));
        }

        [Test]
        public void Load_Phase2File_MovesEachAnimalsUnlockedDecorIntoTheInventoryOnce()
        {
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"animals\":[" +
                "{\"id\":1,\"name\":\"レオパ1\",\"sex\":\"Male\",\"weightGrams\":45.0,\"stage\":\"Adult\",\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\"," +
                "\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100,\"selectedDecorId\":\"plant_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\",\"plant_01\",\"water_dish_01\"],\"genomeVersion\":1,\"personality\":\"Calm\",\"personalityKnown\":true}," +
                "{\"id\":2,\"name\":\"レオパ2\",\"sex\":\"Female\",\"weightGrams\":5.0,\"stage\":\"Baby\",\"hatchedAtUtc\":\"2026-09-24T00:00:00.0000000+00:00\"," +
                "\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100,\"selectedDecorId\":\"rock_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\"],\"genomeVersion\":1,\"personality\":\"Shy\",\"personalityKnown\":true}]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1},{\"id\":2,\"size\":\"Standard\",\"animalId\":2}]," +
                "\"rackCount\":1,\"incubatorCount\":1,\"nextAnimalId\":3,\"nextCageId\":3}");

            var colony = service.LoadOrCreate(path, Now, new Random(3));

            Assert.That(service.LastLoadMovedDecor, Is.True);
            Assert.That(colony.Inventory.Count("rock_01"), Is.EqualTo(2));
            Assert.That(colony.Inventory.Count("plant_01"), Is.EqualTo(1));
            Assert.That(colony.Inventory.Count("water_dish_01"), Is.EqualTo(1));
            Assert.That(colony.Cages.TrueForAll(c => c.DecorIds.Count == 0), Is.True);
            Assert.That(colony.Incubators, Is.EqualTo(new[] { IncubatorModel.Simple }));
            Assert.That(colony.Shop.StockMonthIndex, Is.EqualTo(ShopStock.NeverStocked));

            service.Save(path, colony);
            var again = service.LoadOrCreate(path, Now, new Random(3));

            Assert.That(service.LastLoadMovedDecor, Is.False);
            Assert.That(again.Inventory.Count("rock_01"), Is.EqualTo(2));
        }

        [Test]
        public void AFileWithoutASchemaThatIsNotAnOldPetSave_IsTreatedAsUnreadable()
        {
            File.WriteAllText(path, "{\"foo\":1}");
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*could not be loaded.*"));

            service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(service.LastLoadFailed, Is.True);
            Assert.That(service.LastLoadMigrated, Is.False);
            Assert.That(File.Exists(ColonySaveService.BackupPathFor(path)), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo("{\"foo\":1}"));
        }

        [Test]
        public void ACorruptBackupThatCannotBeWritten_IsReported()
        {
            File.WriteAllText(path, "not json");
            var backupPath = ColonySaveService.CorruptBackupPathFor(path, Now);
            Directory.CreateDirectory(backupPath);
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*Could not back up.*"));
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*could not be loaded.*"));
            try
            {
                service.LoadOrCreate(path, Now, new Random(1));

                Assert.That(service.LastLoadBackupFailed, Is.True);
            }
            finally
            {
                Directory.Delete(backupPath);
            }
        }
```

`Assets/Tests/EditMode/ColonySessionTests.cs`：先頭に `using System.Text.RegularExpressions;` と `using UnityEngine.TestTools;` を加え、テストを加える：

```csharp
        [Test]
        public void Load_StocksTheShopAndANewGameMonthRestocksIt()
        {
            var session = NewSession();
            var first = session.Load();

            Assert.That(first.Restocked, Is.True);
            Assert.That(session.Colony.Shop.Offers.Count, Is.InRange(ShopStockGenerator.MinOffers, ShopStockGenerator.MaxOffers));
            var month = session.Colony.Shop.StockMonthIndex;

            Assert.That(session.SimulateGameTime(GameCalendar.RealTimeFor(1d)).Restocked, Is.False);
            var next = session.SimulateGameTime(GameCalendar.RealTimeFor(30d));

            Assert.That(next.Restocked, Is.True);
            Assert.That(next.HasEvents, Is.True);
            Assert.That(session.Colony.Shop.StockMonthIndex, Is.EqualTo(month + 1));
        }

        [Test]
        public void ABoughtAnimal_RevealsItsPersonalityAfterSevenGameDays()
        {
            var session = NewSession();
            session.Load();
            session.Colony.AddCage(CageSize.Standard);
            session.Colony.Wallet.Money = 1_000_000;
            var offer = session.Colony.Shop.Offers[0];

            Assert.That(new ShopService(economy, care).BuyAnimal(session.Colony, offer.OfferId, session.GameNowUtc), Is.EqualTo(ShopResult.Ok));

            Assert.That(session.SimulateGameTime(GameCalendar.RealTimeFor(6.9d)).PersonalityReveals, Is.Empty);
            var later = session.SimulateGameTime(GameCalendar.RealTimeFor(0.2d));

            Assert.That(later.PersonalityReveals, Is.EquivalentTo(new[] { offer.Animal }));
            Assert.That(offer.Animal.PersonalityKnown, Is.True);
            Assert.That(later.HasEvents, Is.True);
        }

        [Test]
        public void Load_WhenTheCorruptBackupCannotBeWritten_BlocksSavingSoTheFileSurvives()
        {
            File.WriteAllText(path, "not json");
            var backupPath = ColonySaveService.CorruptBackupPathFor(path, Start);
            Directory.CreateDirectory(backupPath);
            LogAssert.Expect(UnityEngine.LogType.Error, new Regex(".*Could not back up.*"));
            LogAssert.Expect(UnityEngine.LogType.Error, new Regex(".*could not be loaded.*"));
            try
            {
                var session = NewSession();
                session.Load();
                session.Save();

                Assert.That(session.SaveBlocked, Is.True);
                Assert.That(File.ReadAllText(path), Is.EqualTo("not json"));
            }
            finally
            {
                Directory.Delete(backupPath);
            }
        }

        [Test]
        public void Load_ReportsWhenDecorWasMovedToTheInventory()
        {
            File.WriteAllText(path,
                "{\"schemaVersion\":2,\"lastSavedAtUtc\":\"2026-09-25T11:00:00.0000000+00:00\",\"hunger\":70,\"hydration\":60," +
                "\"cleanliness\":50,\"health\":90,\"growth\":50,\"growthStage\":\"Juvenile\",\"selectedDecorId\":\"plant_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\",\"plant_01\"]}");

            var session = NewSession();
            session.Load();

            Assert.That(session.DecorMovedToInventory, Is.True);
            Assert.That(session.SaveBlocked, Is.False);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.ColonySaveServiceTests` と `-testFilter TerrariumDays.Tests.ColonySessionTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

上の「保存の形」「読み込み・保存の規則」1〜8、「セッション」「画面」のとおり。要点：
- `LoadOrCreate` の最初で `LastLoadMovedDecor = false; LastLoadBackupFailed = false;`。
- 8 の判定は `probe` を読んだ直後、`probe.schemaVersion >= CurrentSchemaVersion` の分岐の前に置く：

```csharp
                if (probe.schemaVersion <= 0 && !(json.Contains("\"lastSavedAtUtc\"") && json.Contains("\"growthStage\"")))
                {
                    throw new InvalidDataException("No schema version and not a schema-1 pet save.");
                }
```

- 所持品の読み込み：

```csharp
            if (data.inventoryVersion >= CurrentInventoryVersion)
            {
                if (data.inventory != null)
                {
                    foreach (var item in data.inventory)
                    {
                        if (!string.IsNullOrEmpty(item.id) && item.count > 0)
                        {
                            colony.Inventory.Add(item.id, item.count);
                        }
                    }
                }
            }
            else
            {
                foreach (var a in data.animals)
                {
                    MoveUnlockedDecorToInventory(colony, a.unlockedDecorIds);
                }

                LastLoadMovedDecor = true;
            }

        private static void MoveUnlockedDecorToInventory(Colony colony, List<string> unlockedDecorIds)
        {
            foreach (var id in unlockedDecorIds ?? new List<string> { DecorItems.StarterDecorId })
            {
                if (DecorItems.IsDecor(id))
                {
                    colony.Inventory.Add(id, 1);
                }
            }
        }
```

- ケージの読み込み：`DecorIds = data.inventoryVersion >= CurrentInventoryVersion && c.decorIds != null ? new List<string>(c.decorIds) : new List<string>()`。
- 既存の `Assert.That(pet.SelectedDecorId, ...)` などの段階2のアサートはこのタスクでは残す（Task 6 で消す）。`PetState.SelectedDecorId`・`UnlockedDecorIds` の読み書きもこのタスクでは今のまま。

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode・PlayMode 全体。
Expected: PASS。PlayMode の既存テストで、新規ゲームの読み込みで入荷の `HasEvents` が true になる影響（`ColonyChanged` が1回多く呼ばれる）で落ちるものがないこと。落ちたら、テストの期待を直すのではなく原因を報告に書く。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/ColonySaveData.cs Assets/Scripts/Core/ColonySaveService.cs Assets/Scripts/Core/ColonySession.cs Assets/Scripts/UI/TerrariumView.cs Assets/Tests/EditMode/ColonySaveServiceTests.cs Assets/Tests/EditMode/ColonySessionTests.cs
git commit -m "Save shop stock, inventory and per-cage decor; migrate unlocked decor once; guard unreadable saves"
```

---

### Task 6: 装飾をケージごとに（画面・解放の廃止）

**Files:**
- Delete: `Assets/Scripts/Gameplay/DecorCatalog.cs`・`DecorDefinition.cs`・`DecorUnlockService.cs`（各 .meta も）、`Assets/Tests/EditMode/DecorUnlockServiceTests.cs`（.meta も）
- Modify: `Assets/Scripts/Core/PetState.cs`（`DefaultDecorId`・`SelectedDecorId`・`UnlockedDecorIds` を削除）
- Modify: `Assets/Scripts/Core/ColonySaveService.cs`（上の3つの読み書きを削除。`AnimalSaveData` のフィールドは移行のため残し、保存時は書かない＝null）
- Modify: `Assets/Scripts/UI/TerrariumView.cs`・`Assets/UI/Terrarium.uxml`・`Assets/UI/Terrarium.uss`
- Test: `Assets/Tests/EditMode/PetStateTests.cs`（装飾の2行を削除）、`Assets/Tests/EditMode/ColonySaveServiceTests.cs`（`SelectedDecorId`・`UnlockedDecorIds` のアサートを削除）、`Assets/Tests/PlayMode/TerrariumViewTests.cs`（装飾のテストを書き直す）

**Interfaces:**
- Consumes: `DecorItems`・`DecorSlots`・`TerrariumArtLayout.PlacementFor`・`Cage.DecorIds`・`Colony.Inventory`
- Produces: UXML の `decor-image-0`・`decor-image-1`・`decor-image-2`（`decor-image` を置き換え）、`decor-slot-label`（引き出しの見出しの下）

変更点：
1. 水槽の装飾は、選択中のケージ（`currentCage`）の `DecorIds` を枠の順に `decor-image-{i}` に描く。空の枠は `display: none`。クラス `decor-icon-{id}` の付け外しは枠ごとに覚えておく（今の `appliedDecorIconClass` を配列に）。
2. 位置は `artLayout.PlacementFor(id, i)`。描画順は枠ごとに登録する（`DrawTieBreak.Decor`、奥行きは床の装飾ならその枠の `Depth`、吊り下げは `DrawSortKey.BehindFloor`）。
3. 隠れ家：枠の順で最初の床の装飾に `petActor.SetShelterAt(...)`。床の装飾がなければ `petActor.ClearShelter()`。
4. `SelectCage`・ケージの切り替え・引き出しでの変更のたびに描き直す（今の `RenderDecorImage(state)` の呼び出し箇所を `RenderDecorImages()` に）。
5. 引き出し：見出しの下に `<ui:Label name="decor-slot-label" class="decor-status-label" />`、文字は `DecorSlots.SlotSummary(currentCage)`。5つの行は今の UXML のまま。各行の状態は `DecorSlots.StatusText(session.Colony, currentCage, id)`、行のタップ：置いてあれば `DecorSlots.Remove`、なければ `DecorSlots.Place`。`Ok` なら描き直して `SaveCurrentState()`。行は常に押せる（状態の文字が理由を示す）。
6. `HandleReport` の `DecorUnlockService.GrantUnlocksForStage` を削除。`DecorCatalog.All` の代わりに `DecorItems.All`（`.Id`・`.Label`）。
7. `using TerrariumDays.Gameplay;` が不要になったファイルからは外す（`PetAnimation` などまだ使うファイルは残す）。

主なコード（参考）：

```csharp
        private readonly VisualElement[] decorImageElements = new VisualElement[3];
        private readonly string[] appliedDecorIconClasses = new string[3];

        private void RenderDecorImages()
        {
            for (var i = 0; i < decorImageElements.Length; i++)
            {
                var element = decorImageElements[i];
                if (element == null)
                {
                    continue;
                }

                if (appliedDecorIconClasses[i] != null)
                {
                    element.RemoveFromClassList(appliedDecorIconClasses[i]);
                    appliedDecorIconClasses[i] = null;
                }

                var id = DecorIdAt(i);
                element.style.display = id == null ? DisplayStyle.None : DisplayStyle.Flex;
                if (id != null)
                {
                    appliedDecorIconClasses[i] = $"decor-icon-{id}";
                    element.AddToClassList(appliedDecorIconClasses[i]);
                }
            }

            PlaceDecor();
        }

        private string DecorIdAt(int slot) =>
            currentCage != null && slot < currentCage.DecorIds.Count ? currentCage.DecorIds[slot] : null;
```

- [ ] **Step 1: Write the failing tests**

PlayMode（`TerrariumViewTests.cs`。既存の装飾のテスト（`DecorCatalog` を使う行113付近のセットアップ、行415〜425のテスト）を置き換え、既存のセットアップ・一時セーブの書き方に合わせる）：
- 新しいゲーム：`decor-image-0` が表示され `decor-icon-rock_01` を持つ。`decor-image-1`・`-2` は `display: none`。
- 所持品に `plant_01` を1つ入れて `OnDecorRowClicked("plant_01")` → `currentCage.DecorIds` が `rock_01, plant_01`、`decor-image-1` に `decor-icon-plant_01`、所持品の `plant_01` は0、`decor-status-plant_01` の文字が「置いています（タップで外す）」、`decor-slot-label` が「装飾の枠 2/2」。
- もう一度 `OnDecorRowClicked("plant_01")` → 外れて所持品が1に戻る。
- 未所持の `water_dish_01` の行をタップしても何も変わらず、状態の文字は「未所持（ショップで購入）」。
- 岩を外すと（床の装飾がなくなると）ペットの隠れ家がなくなる（`PetActor`/`PetBehaviour` に隠れ家の有無を読む手段があればそれで。なければこの確認は省き、報告に書く）。

EditMode：`PetStateTests` と `ColonySaveServiceTests` の装飾のアサートを削除（`DecorUnlockServiceTests` はファイルごと削除）。

- [ ] **Step 2: Run the tests to verify they fail**

Run: PlayMode
Expected: コンパイルエラーまたは FAIL。

- [ ] **Step 3: Write the implementation**

上の「変更点」1〜7。削除は `git rm Assets/Scripts/Gameplay/DecorCatalog.cs Assets/Scripts/Gameplay/DecorCatalog.cs.meta ...` のように .meta ごと。UXML の `decor-image` は3つの `<ui:VisualElement name="decor-image-N" class="decor-image" />` に置き換える（同じ親、同じ位置）。

- [ ] **Step 4: Run the tests and captures**

Run: EditMode・PlayMode 全体 PASS。`grep -rn "SelectedDecorId\|UnlockedDecorIds\|DecorUnlockService\|DecorCatalog\|DefaultDecorId" Assets/Scripts Assets/Tests` が何も出さないこと（`ColonySaveData.cs` の `selectedDecorId`/`unlockedDecorIds` フィールドは小文字なので対象外）。続けて `scripts/capture-screens.sh`。大型ケージに3つ（岩・流木・水入れ）と保温ランプの組み合わせを置いた画面を `ScreenCaptureTests` の Explicit のテスト `CaptureDecorSlots`（`Logs/Screens/decor-slots.png`）で撮り、装飾が重ならず床に立っていること、ヤモリが隠れ家の横で眠れることを確かめる。重なるときは `TerrariumArtLayout.DecorSlotSpots` の値を直し、Task 2 のテスト `ArtLayout_PutsFloorDecorOnItsSlotSpot...` の期待値も同じ値に直す（報告に書く）。

- [ ] **Step 5: Commit**

```bash
git add -A Assets/Scripts Assets/UI Assets/Tests
git commit -m "Place bought decor per cage in up to three slots and drop growth-based unlocks"
```

---

### Task 7: 画面の持ち越し修正（ホームのタブバー・購読の解除・サムネイル・プロフィールの語）

**Files:**
- Create: `Assets/Scripts/Core/ThumbnailCrop.cs`
- Modify: `Assets/Scripts/UI/CageStatusText.cs`（`ProfileTokens`・`SexLabel`）
- Modify: `Assets/Scripts/UI/MorphSprites.cs`（`Thumbnail`）
- Modify: `Assets/Scripts/UI/HomeView.cs`（`HomeView`・`CageListView` のサムネイル）
- Modify: `Assets/Scripts/UI/ShellNavigator.cs`・`Assets/Scripts/UI/TerrariumView.cs`・`Assets/UI/Terrarium.uxml`・`Assets/UI/Terrarium.uss`
- Test: `Assets/Tests/EditMode/ShellTests.cs`（追加）、`Assets/Tests/PlayMode/TerrariumViewTests.cs`（`profile-label` の確認を `profile-chips` に）

**Interfaces:**
- Produces: `PixelRect`・`ThumbnailCrop.BodyRect(SpriteFootprint, float padding)`、`CageStatusText.ProfileTokens(PetState) → List<string>`・`CageStatusText.SexLabel(PetState) → string`、`MorphSprites.Thumbnail(PetState) → Sprite`・`MorphSprites.ThumbnailPadding`、UXML の `profile-chips`（`profile-label` を置き換え）

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/ShellTests.cs` に追加：

```csharp
        [Test]
        public void ProfileTokens_SplitTheNameIntoWordsThatMustNotBreak()
        {
            var pet = new PetState
            {
                Genotype = Genotype.Normal().Set(GeneId.Eclipse, 2),
                Known = new KnownGenetics().SetHet(GeneId.TremperAlbino, 1d).SetHet(GeneId.Blizzard, 0.66d),
                Personality = Personality.Curious,
            };

            Assert.That(CageStatusText.ProfileTokens(pet), Is.EqualTo(new[]
            {
                "エクリプス", "ヘテロトレンパーアルビノ", "66%ポッシブルヘテロブリザード", "好奇心旺盛",
            }));
        }

        [Test]
        public void ProfileTokens_KeepHetsUnknownWithTheNameAndHideAnUnknownPersonality()
        {
            var pet = new PetState { Known = KnownGenetics.Unknown(), PersonalityKnown = false };

            Assert.That(CageStatusText.ProfileTokens(pet), Is.EqualTo(new[] { "ノーマル（ヘテロ不明）", "性格不明" }));
        }

        [Test]
        public void SexLabel_HidesTheSexUntilItIsKnown()
        {
            Assert.That(CageStatusText.SexLabel(new PetState { Sex = Sex.Female, SexRevealed = false }), Is.EqualTo("性別不明"));
            Assert.That(CageStatusText.SexLabel(new PetState { Sex = Sex.Female, SexRevealed = true }), Is.EqualTo("♀メス"));
            Assert.That(CageStatusText.SexLabel(new PetState { Sex = Sex.Male, SexRevealed = true }), Is.EqualTo("♂オス"));
        }

        [Test]
        public void ThumbnailCrop_IsTheBodyPlusPaddingWithYFromTheBottom()
        {
            var pet = new TerrariumArtLayout().Pet;

            var rect = ThumbnailCrop.BodyRect(pet, 6f);
            Assert.That((rect.X, rect.Y, rect.Width, rect.Height), Is.EqualTo((16f, 82f, 194f, 82f)));

            var clamped = ThumbnailCrop.BodyRect(pet, 30f);
            Assert.That((clamped.X, clamped.Y, clamped.Width, clamped.Height), Is.EqualTo((0f, 58f, 234f, 130f)));
        }

        [Test]
        public void TheTabBarCanBeUsedFromHomeAndShowsNoSelectionThere()
        {
            var root = BuildShell();
            var nav = new ShellNavigator(root);

            nav.ShowTab(ShellTab.Ledger);
            Assert.That(Display(root, "main-screen"), Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Button>("tab-ledger").ClassListContains("tab-selected"), Is.True);

            nav.ShowHome();
            foreach (var name in new[] { "tab-cages", "tab-incubator", "tab-shop", "tab-events", "tab-ledger" })
            {
                Assert.That(root.Q<Button>(name).ClassListContains("tab-selected"), Is.False, name);
            }
        }
```

（`BuildShell` はタブのボタンを画面の外に置いているので、このテストは「ホームからでもタブのボタンで移れる」ことと「ホームでは選択表示がない」ことを確かめる。実際の UXML でタブバーがホームに見えることは画面キャプチャで確かめる。）

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.ShellTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/ThumbnailCrop.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>A rectangle in texture pixels, Y measured up from the bottom (Unity's Sprite.Create rect).</summary>
    public readonly struct PixelRect
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public PixelRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    /// <summary>Crops a thumbnail to the sprite's visible body so it is not mostly transparent padding.</summary>
    public static class ThumbnailCrop
    {
        public static PixelRect BodyRect(SpriteFootprint sprite, float padding)
        {
            var left = Math.Max(0f, sprite.BodyLeft - padding);
            var right = Math.Min(sprite.ImageWidth, sprite.BodyRight + padding);
            var top = Math.Max(0f, sprite.BodyTop - padding);
            var bottom = Math.Min(sprite.ImageHeight, sprite.BodyBottom + padding);
            return new PixelRect(left, sprite.ImageHeight - bottom, right - left, bottom - top);
        }
    }
}
```

`CageStatusText` に追加（`ProfileFor` は残す）：

```csharp
        /// <summary>The profile as words that must not be broken across lines (§4.2 name words, then the personality).</summary>
        public static List<string> ProfileTokens(PetState pet)
        {
            var tokens = new List<string>(MorphNamer.FullName(pet.Genotype, pet.Known).Split(' '));
            tokens.Add(pet.PersonalityKnown ? PersonalityTraits.Label(pet.Personality) : "性格不明");
            return tokens;
        }

        public static string SexLabel(PetState pet) => !pet.SexKnown ? "性別不明" : pet.Sex == Sex.Female ? "♀メス" : "♂オス";
```

`MorphSprites.Thumbnail`（モルフの `Key` ごとにキャッシュ。フレームの大きさが足跡の画像の大きさと違うときは倍率をかける）：

```csharp
        public const float ThumbnailPadding = 6f;
        private static readonly Dictionary<string, Sprite> thumbnails = new Dictionary<string, Sprite>();

        public static Sprite Thumbnail(PetState pet)
        {
            var key = MorphAppearance.PaletteFor(pet.Genotype, pet.Stage).Key;
            if (thumbnails.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var frame = For(pet).Frame(PetClip.Idle, 0);
            if (frame == null)
            {
                return null;
            }

            var footprint = new TerrariumArtLayout().Pet;
            var crop = ThumbnailCrop.BodyRect(footprint, ThumbnailPadding);
            var scale = frame.rect.width / footprint.ImageWidth;
            var rect = new Rect(frame.rect.x + crop.X * scale, frame.rect.y + crop.Y * scale, crop.Width * scale, crop.Height * scale);
            var sprite = Sprite.Create(frame.texture, rect, new Vector2(0.5f, 0.5f), frame.pixelsPerUnit);
            thumbnails[key] = sprite;
            return sprite;
        }
```

画面：
1. `HomeView.CageSlot` と `CageListView` の行は `MorphSprites.Thumbnail(pet)` を背景にする。USS の `.rack-thumb`・`.cage-row-thumb` は切り出した比率（約 194:82）に合わせて横長にし、`-unity-background-scale-mode: scale-to-fit`。ホーム・一覧の画面キャプチャで、体が枠いっぱいに見えることを確かめる。
2. プロフィール：UXML の `profile-label` を `<ui:VisualElement name="profile-chips" class="profile-chips" />` に置き換え、`Render` で `CageStatusText.ProfileTokens(petState)` から語ごとの `Label`（クラス `profile-chip`、`white-space: nowrap`）を作る（文字が変わったときだけ作り直す）。USS：`.profile-chips { flex-direction: row; flex-wrap: wrap; }`、`.profile-chip { font-size: 18px; color: rgb(110, 84, 60); white-space: nowrap; margin-right: 6px; }`。PlayMode テストの `profile-label` の確認は、語のラベルの文字をつないだものに直す。長い名前でも上のバーからはみ出さず、ケージ移動の ◀▶ とデバッグボタンに重ならないこと（画面キャプチャ）。
3. タブバー：UXML の `tab-bar` を `main-screen` の外（`main-screen` の直後、モーダルの前）へ移す。`ShellNavigator.ShowHome` はすべてのタブボタンから `tab-selected` を外す。`home-incubator-button` の文字を「孵卵器」にし、押すと `navigator.ShowTab(ShellTab.Incubator)`。ホームの `rack-list` がタブバーに隠れないこと（キャプチャ）。
4. 購読の解除：`TerrariumView.Awake` の `ColonyChanged += RefreshShell`、`navigator.TabChanged += ...`（名前付きのメソッド `OnTabChanged` に）、`homeView.CageSelected`・`cageListView.CageSelected`（名前付きのメソッド `OnCageTapped(int)` に）、`home-button`・`prev/next`・一括の世話・`home-incubator-button` の `clicked` を、`OnDestroy` で外す（ボタンは Awake で `Q` した参照をフィールドに持つ）。以後のタスクで加える購読も同じ形にする。

- [ ] **Step 4: Run the tests and captures**

Run: EditMode・PlayMode 全体 PASS。`scripts/capture-screens.sh` の `00-home.png`・`00-cage-list.png` と詳細画面を開き、サムネイル・タブバー・プロフィールの語の折り返しを確かめる。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts Assets/UI Assets/Tests
git commit -m "Show the tab bar at home, crop thumbnails to the body, wrap profiles by word and unsubscribe on destroy"
```

---

### Task 8: ショップのタブ（生体・用品・卸売り・確認のダイアログ）

**Files:**
- Create: `Assets/Scripts/UI/ShopText.cs`・`Assets/Scripts/UI/ShopView.cs`・`Assets/Scripts/UI/ConfirmDialog.cs`
- Modify: `Assets/Scripts/UI/ShellNavigator.cs`・`Assets/Scripts/UI/TerrariumView.cs`・`Assets/UI/Terrarium.uxml`・`Assets/UI/Terrarium.uss`
- Test: `Assets/Tests/EditMode/ShopAndLedgerTextTests.cs`（新規）、`Assets/Tests/EditMode/ShellTests.cs`（ショップはパネルに）、`Assets/Tests/PlayMode/TerrariumViewTests.cs`（追加）、`Assets/Tests/PlayMode/ScreenCaptureTests.cs`（撮影を追加）

**Interfaces:**
- Consumes: `ShopService`・`ShopCatalog`・`ShopStock`・`MarketPrice`・`CageStatusText.ProfileTokens/SexLabel`・`MorphSprites.Thumbnail`・`GameCalendar`
- Produces: `ShopText`（下のコード）、`ShopSection`（`Animals, Supplies, Wholesale`）、`ShopView`（`Render(Colony, ShopService, GameCalendar, DateTimeOffset)`・`Invalidate()`・イベント `OfferBuyRequested(int offerId)`・`ItemBuyRequested(string itemId)`・`WholesaleRequested(int animalId)`・プロパティ `Section`）、`ConfirmDialog`（`Show(string message, Action onYes)`・`Hide()`・`IsOpen`）、UXML の `shop-panel`・`shop-section-animals`・`shop-section-supplies`・`shop-section-wholesale`・`shop-restock-label`・`shop-message-label`・`shop-list`、`confirm-modal`・`confirm-message-label`・`confirm-yes-button`・`confirm-no-button`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/ShopAndLedgerTextTests.cs`（Task 9 でこのファイルに台帳のテストを足す）：

```csharp
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
```

`ShellTests`：`TabsNotBuiltYet_ShowWhenTheyArrive` を直す：偽の root に `shop-panel` を加え、`ShowTab(ShellTab.Shop)` で `shop-panel` が Flex・`placeholder-panel` が None。`ShowTab(ShellTab.Events)` では `placeholder-panel` が Flex で文字が「イベントは段階6で追加されます」。

PlayMode（`TerrariumViewTests.cs`）：
- ショップのタブを開くと `shop-list` に在庫の数（4〜6）の行があり、各行に価格（`ShopText.Yen(shopService.PriceOf(offer))`）が出る。
- 空きケージを1つ加え、所持金を十分にして、最初の行の「買う」→ 確認のダイアログの「はい」で、個体が増え、所持金が価格だけ減り、台帳の最後が `AnimalPurchase`、`shop-message-label` が `BoughtAnimalMessage`。
- 空きケージがないとき「買う」→「はい」で `shop-message-label` が `FailureMessage(NoEmptyCage)` で何も変わらない。
- 「いいえ」では何も変わらない。
- 用品の区分で「標準ケージ」を買うとケージが増え、ホーム（タブバーからホーム）で空きケージの枠が増えている。
- 卸売りの区分：個体が2匹のとき、選択中でない方を卸すと1匹になり、所持金が卸値だけ増える。選択中の個体を卸したときは、別の個体のケージが選ばれる（ケージの詳細が消えた個体を表示しない）。1匹のときも卸せ、確認の文に `ShopText.LastAnimalWarning` が付く。

- [ ] **Step 2: Run the tests to verify they fail**

Run: EditMode・PlayMode
Expected: コンパイルエラーまたは FAIL。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/UI/ShopText.cs`:

```csharp
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
                default:
                    return string.Empty;
            }
        }

        public static string ConfirmBuy(string what, long yen) => $"{what}を{Yen(yen)}で買いますか？";

        public static string ConfirmWholesale(PetState pet, long yen) => $"{pet.Name}を{Yen(yen)}で卸しますか？\n（相場の40%。取り消せません）";

        /// <summary>Shown when the last animal is being sold, appended to the confirmation.</summary>
        public const string LastAnimalWarning = "\nこれで個体がいなくなります（ショップで迎え直せます）";

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
```

画面の組み立て（`HomeView` と同じく、コードで行を作り、表示内容の署名が変わったときだけ作り直す）：
1. UXML：`tab-content` の中、`placeholder-panel` の前に `shop-panel`（クラス `panel`）。中身：区分のボタン3つ（`shop-section-animals`「生体」・`shop-section-supplies`「用品」・`shop-section-wholesale`「卸売り」、選択中は `tab-selected` と同じ見た目のクラス）、`shop-restock-label`（生体の区分だけ）、`shop-message-label`（固定の高さ、`visibility` で切り替え）、`shop-list`（ScrollView）。ルートの最後に `confirm-modal`（`milestone-modal-overlay` と同じ作り。`confirm-message-label`・`confirm-yes-button`「はい」・`confirm-no-button`「いいえ」）。
2. `ShopView` の行：
   - 生体：サムネイル（`MorphSprites.Thumbnail`）・語（`ProfileTokens` のうち性格の語を除いたもの。性格は `OfferDetail` に含まれる）・`OfferDetail`・価格・「買う」ボタン → `OfferBuyRequested(offerId)`。在庫が0のときは「今月の入荷は売り切れました」。
   - 用品：`ItemLabel`・価格・「買う」→ `ItemBuyRequested(id)`。所持数があるもの（装飾・産卵床）は「所持N」も出す。
   - 卸売り：自分の個体ごとに、サムネイル・名前・語・`WholesaleLine(market, pay)`・「卸す」→ `WholesaleRequested(animalId)`（1匹だけのときも押せる。確認の文に `LastAnimalWarning` を付ける）。個体が0匹なら「個体がいません。ショップで迎えましょう」の1行。
3. `TerrariumView`：`ShopService` を `LoadColony` で作る。3つのイベントを受けて `ConfirmDialog.Show(ShopText.ConfirmBuy(...) or ConfirmWholesale(...), onYes)`。「はい」で `shopService.BuyAnimal/BuyItem/Wholesale(session.Colony, ..., GameNowUtc())`、結果の文字を `shop-message-label` に、成功なら `SaveCurrentState()`・`ColonyChanged?.Invoke()`・`shopView.Invalidate()`。卸した個体が選択中だったら、最初の埋まったケージを `SelectCage`。`RefreshShell` でショップのタブが開いているときだけ `shopView.Render(...)`。`report.Restocked` のときも `shopView.Invalidate()`。
4. `ShellNavigator`：`shop-panel`（と Task 9 の `ledger-panel`）を持ち、`ShowTab` で `SetDisplay(shop, tab == ShellTab.Shop)`。`placeholder-panel` は、そのタブのパネルがないときだけ表示（`var hasPanel = tab == ShellTab.Cages || (tab == ShellTab.Shop && shop != null) || (tab == ShellTab.Ledger && ledger != null);`）。`PlaceholderTextFor(Shop)` は削除してよい（テストも合わせる）。
5. 購読は名前付きのメソッドにし、`OnDestroy` で外す（Task 7 の形）。
6. **個体0匹への対応**（最後の1匹を卸せるため。利用者の決定）：
   - `CageStatusText` に `public const string NoAnimalsMessage = "個体がいません。ショップで迎えましょう";` を加える。
   - `TerrariumView` に `ClearSelection()`：`state = null`、`currentCage = null`、`petActor` を捨てて `petElement` と装飾の3枠を `display: none`、ケージの詳細を開いていたら `navigator.ShowCageList()`。卸売りで個体がいなくなったとき、`LoadColony` で埋まったケージがないとき（今は `session.Colony.Cages[0]` を選ぼうとして失敗する）に呼ぶ。個体を買ったとき・デバッグで個体を加えたときは、選択がなければその個体のケージを `SelectCage` する（詳細画面には移らない）。
   - `state` を使うすべての処理（`Update` の毎フレームの処理、`HandleReport`、`Render`、世話のボタン、ペットのタップ、デバッグの各ボタンとスライダー、`RefreshShell` の見出し、`PlaceDecor`・装飾の引き出し）が `state == null` / `currentCage == null` で何もしないことを確かめる（`grep -n "state\." Assets/Scripts/UI/TerrariumView.cs` で1つずつ見る）。◀▶（`ShowCageStep`）は今のまま何もしない。
   - 一括の世話（餌・水・掃除）は、個体が0匹なら何もせず `ShowFeedback(CageStatusText.NoAnimalsMessage)`。ケージの一覧は、0匹のとき一覧の上に同じ文のラベル（`cage-list-empty-label`、固定の高さで `visibility` 切り替え）を出す。
   - テスト（PlayMode、`TerrariumViewTests.cs`）：新しいゲームでショップの卸売りから唯一の個体を卸す →（a）`session.Colony.Animals` が0、`view.State` が null、ケージの詳細が開いていない、（b）ホームを描いても例外がなく全スロットが「空きケージ」で押せない、（c）`OnFeedAllClicked`・`OnWaterAllClicked`・`OnCleanAllClicked` で例外がなくフィードバックが `NoAnimalsMessage`、所持金が変わらない、（d）`ShowCageStep(1)` で例外がない、（e）数フレーム（`yield return null` を数回）と `SimulateGameTime` を進めても例外がない、（f）保存して同じセーブで `LoadColony` し直しても例外がなく `State` が null、（g）ショップで生体を買うとそのケージが選ばれ、ケージの一覧から詳細を開ける。EditMode（`ShellTests`）：個体0匹の `Colony` で `HomeView.Render` が全スロットを「空きケージ」にし、`CageListSignature` が例外を出さない。

- [ ] **Step 4: Run the tests and captures**

Run: EditMode・PlayMode 全体 PASS。0匹の状態のホーム・ケージの一覧・台帳の画面も撮る（`Logs/Screens/empty-home.png` など）。`ScreenCaptureTests` に生体・用品・卸売りの区分と確認のダイアログの撮影を加え（`Logs/Screens/shop-animals.png` など）、行が画面幅に収まり、長いモルフ名が語の単位で折り返し、ボタンが押せる大きさであることを確かめる。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts Assets/UI Assets/Tests
git commit -m "Add the shop tab for animals, supplies and wholesale with a confirmation dialog"
```

---

### Task 9: 台帳のタブ（個体一覧・お金の出入り）

**Files:**
- Create: `Assets/Scripts/UI/LedgerText.cs`・`Assets/Scripts/UI/LedgerView.cs`
- Modify: `Assets/Scripts/UI/ShellNavigator.cs`・`Assets/Scripts/UI/TerrariumView.cs`・`Assets/UI/Terrarium.uxml`・`Assets/UI/Terrarium.uss`
- Test: `Assets/Tests/EditMode/ShopAndLedgerTextTests.cs`（追加）、`Assets/Tests/EditMode/ShellTests.cs`、`Assets/Tests/PlayMode/TerrariumViewTests.cs`・`ScreenCaptureTests.cs`

**Interfaces:**
- Consumes: `Wallet.Ledger`・`GameCalendar`・`GrowthModel.AgeMonths/StageLabel`・`CageStatusText.ProfileTokens/SexLabel`・`MorphSprites.Thumbnail`
- Produces: `LedgerText`（下のコード）、`LedgerSection`（`Animals, Money`）、`LedgerView`（`Render(Colony, GameCalendar, DateTimeOffset)`・`Invalidate()`・イベント `AnimalTapped(int animalId)`）、UXML の `ledger-panel`・`ledger-section-animals`・`ledger-section-money`・`ledger-summary-label`・`ledger-list`

- [ ] **Step 1: Write the failing tests**

`ShopAndLedgerTextTests.cs` に追加：

```csharp
        [Test]
        public void CategoryLabels()
        {
            Assert.That(LedgerText.CategoryLabel(LedgerCategory.Food), Is.EqualTo("餌代"));
            Assert.That(LedgerText.CategoryLabel(LedgerCategory.Electricity), Is.EqualTo("電気代"));
            Assert.That(LedgerText.CategoryLabel(LedgerCategory.BoothFee), Is.EqualTo("出店料"));
            Assert.That(LedgerText.CategoryLabel(LedgerCategory.Purchase), Is.EqualTo("用品"));
            Assert.That(LedgerText.CategoryLabel(LedgerCategory.AnimalPurchase), Is.EqualTo("生体の購入"));
            Assert.That(LedgerText.CategoryLabel(LedgerCategory.EventSale), Is.EqualTo("イベント売上"));
            Assert.That(LedgerText.CategoryLabel(LedgerCategory.Wholesale), Is.EqualTo("卸売り"));
            Assert.That(LedgerText.CategoryLabel(LedgerCategory.Other), Is.EqualTo("その他"));
        }

        [Test]
        public void SignedYen_UsesAnAsciiMinus()
        {
            Assert.That(LedgerText.SignedYen(1600), Is.EqualTo("+¥1,600"));
            Assert.That(LedgerText.SignedYen(-30), Is.EqualTo("-¥30"));
            Assert.That(LedgerText.SignedYen(0), Is.EqualTo("+¥0"));
        }

        [Test]
        public void EntryLine_ShowsTheGameDateCategoryAndNote()
        {
            var calendar = new GameCalendar(Epoch);
            var entry = new LedgerEntry { AtUtc = Epoch + GameCalendar.RealTimeFor(2.5d), Category = LedgerCategory.Food, Amount = -30, Note = "レオパ1の餌" };

            Assert.That(LedgerText.EntryLine(entry, calendar), Is.EqualTo("4月3日　餌代　レオパ1の餌"));
        }

        [Test]
        public void MonthTotals_SplitIncomeAndExpenseForThatGameMonthOnly()
        {
            var calendar = new GameCalendar(Epoch);
            var inApril = Epoch + GameCalendar.RealTimeFor(3d);
            var inMay = Epoch + GameCalendar.RealTimeFor(33d);
            var ledger = new[]
            {
                new LedgerEntry { AtUtc = inApril, Amount = -300 },
                new LedgerEntry { AtUtc = inMay, Amount = -30 },
                new LedgerEntry { AtUtc = inMay, Amount = 1600 },
                new LedgerEntry { AtUtc = inMay, Amount = -4800 },
            };

            Assert.That(LedgerText.MonthTotals(ledger, calendar, 1), Is.EqualTo((1600L, 4830L)));
            Assert.That(LedgerText.MonthSummary(1600, 4830), Is.EqualTo("今月　収入 ¥1,600　支出 ¥4,830　差引 -¥3,230"));
        }

        [Test]
        public void Recent_IsNewestFirstAndCapped()
        {
            var ledger = new System.Collections.Generic.List<LedgerEntry>();
            for (var i = 0; i < LedgerText.MaxEntriesShown + 5; i++)
            {
                ledger.Add(new LedgerEntry { Amount = i });
            }

            var recent = LedgerText.Recent(ledger);

            Assert.That(recent.Count, Is.EqualTo(LedgerText.MaxEntriesShown));
            Assert.That(recent[0].Amount, Is.EqualTo(LedgerText.MaxEntriesShown + 4));
        }

        [Test]
        public void AnimalDetail_ShowsSexStageWeightAndAge()
        {
            var pet = new PetState { Sex = Sex.Male, SexRevealed = true, Stage = GrowthStage.Juvenile, WeightGrams = 22.04d, HatchedAtUtc = Epoch.AddDays(-5.5d) };

            Assert.That(LedgerText.AnimalDetail(pet, Epoch), Is.EqualTo("♂オス・ヤング・22.0g・生後5か月"));
        }
```

`ShellTests`：`ledger-panel` を偽の root に加え、`ShowTab(ShellTab.Ledger)` で `ledger-panel` が Flex・`placeholder-panel` が None。

PlayMode：台帳のタブを開くと、個体の区分に個体の数だけ行があり、各行に名前と `AnimalDetail` が出る。行をタップするとその個体のケージの詳細が開く。お金の区分では、餌をやった後に最新の行が「餌代」で `-¥30`（ベビー）になり、`ledger-summary-label` が `MonthSummary` と同じ。生体を買った後は「生体の購入」の行が出る。

- [ ] **Step 2: Run the tests to verify they fail**

Run: EditMode・PlayMode
Expected: コンパイルエラーまたは FAIL。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/UI/LedgerText.cs`:

```csharp
using System;
using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>Texts on the ledger tab (§6.2): the animal list and money in/out.</summary>
    public static class LedgerText
    {
        public const int MaxEntriesShown = 100;

        public static string CategoryLabel(LedgerCategory category)
        {
            switch (category)
            {
                case LedgerCategory.Food:
                    return "餌代";
                case LedgerCategory.Electricity:
                    return "電気代";
                case LedgerCategory.BoothFee:
                    return "出店料";
                case LedgerCategory.Purchase:
                    return "用品";
                case LedgerCategory.AnimalPurchase:
                    return "生体の購入";
                case LedgerCategory.EventSale:
                    return "イベント売上";
                case LedgerCategory.Wholesale:
                    return "卸売り";
                default:
                    return "その他";
            }
        }

        /// <summary>ASCII minus: the UI font may lack U+2212.</summary>
        public static string SignedYen(long amount) => amount >= 0 ? $"+¥{amount:N0}" : $"-¥{-amount:N0}";

        public static string EntryLine(LedgerEntry entry, GameCalendar calendar)
        {
            var date = calendar.DateAt(entry.AtUtc);
            return $"{date.Month}月{date.Day}日　{CategoryLabel(entry.Category)}　{entry.Note}";
        }

        /// <summary>Income and expense (both positive) for one game month.</summary>
        public static (long Income, long Expense) MonthTotals(IEnumerable<LedgerEntry> ledger, GameCalendar calendar, int monthIndex)
        {
            long income = 0;
            long expense = 0;
            foreach (var entry in ledger)
            {
                if (calendar.MonthIndexAt(entry.AtUtc) != monthIndex)
                {
                    continue;
                }

                if (entry.Amount >= 0)
                {
                    income += entry.Amount;
                }
                else
                {
                    expense -= entry.Amount;
                }
            }

            return (income, expense);
        }

        public static string MonthSummary(long income, long expense) =>
            $"今月　収入 ¥{income:N0}　支出 ¥{expense:N0}　差引 {SignedYen(income - expense)}";

        /// <summary>Newest first, at most <see cref="MaxEntriesShown"/>.</summary>
        public static List<LedgerEntry> Recent(IList<LedgerEntry> ledger)
        {
            var recent = new List<LedgerEntry>();
            for (var i = ledger.Count - 1; i >= 0 && recent.Count < MaxEntriesShown; i--)
            {
                recent.Add(ledger[i]);
            }

            return recent;
        }

        public static string AgeLabel(PetState pet, DateTimeOffset nowUtc) =>
            $"生後{(int)Math.Floor(GrowthModel.AgeMonths(pet, nowUtc))}か月";

        public static string AnimalDetail(PetState pet, DateTimeOffset nowUtc) =>
            $"{CageStatusText.SexLabel(pet)}・{GrowthModel.StageLabel(pet.Stage)}・{pet.WeightGrams:0.0}g・{AgeLabel(pet, nowUtc)}";
    }
}
```

画面：
1. UXML：`tab-content` の中に `ledger-panel`（クラス `panel`）。区分のボタン `ledger-section-animals`「個体」・`ledger-section-money`「お金」、`ledger-summary-label`（お金の区分だけ）、`ledger-list`（ScrollView）。
2. `LedgerView`：
   - 個体：ケージの順に、サムネイル・名前・語（`ProfileTokens`、折り返さないラベル）・`AnimalDetail`。行のタップ → `AnimalTapped(animalId)`。個体が0匹なら `CageStatusText.NoAnimalsMessage` の1行（PlayMode テストで確かめる）。
   - お金：`MonthSummary(MonthTotals(..., calendar.MonthIndexAt(now)))` と、`Recent` の各行（左に `EntryLine`、右に `SignedYen`。収入は緑系、支出は茶系の色）。
   - 署名：個体の区分は `HomeView.CageListSignature` に年齢の月数を足したもの、お金の区分は台帳の件数と所持金。変わったときだけ作り直す。
3. `TerrariumView`：`AnimalTapped` で、その個体のケージを `SelectCage` → `navigator.ShowCageDetail()`。`RefreshShell` で台帳のタブが開いているときだけ `ledgerView.Render(...)`。購読は `OnDestroy` で外す。
4. `ShellNavigator`：`ledger-panel` を表示する（Task 8 の `hasPanel`）。`PlaceholderTextFor(Ledger)` は削除してよい。

- [ ] **Step 4: Run the tests and captures**

Run: EditMode・PlayMode 全体 PASS。`ScreenCaptureTests` に台帳の2つの区分の撮影を加え（`Logs/Screens/ledger-animals.png`・`ledger-money.png`）、金額が右にそろい、長いメモが画面からはみ出さないことを確かめる。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts Assets/UI Assets/Tests
git commit -m "Add the ledger tab with the animal list and money in and out"
```

---

### Task 10: 文書の更新と最終確認

**Files:**
- Modify: `GAME.md`（お金と台帳・相場の決まり方・ショップ（毎月の入荷、販売価格、性格の判明）・用品・卸売り・ケージごとの装飾と枠・装飾の移行を、1節ずつ短く。成長による装飾の解放の記述は削除）
- Modify: `CLAUDE.md`・`AGENTS.md`（装飾の id は `DecorItems`・`TerrariumArtLayout.Decor`・USS の `decor-icon-*` の3か所をそろえること。金額は `EconomyTuning`、モルフの基本価格は `MarketPrice` にあること。該当する記述がある場合だけ）

- [ ] **Step 1:** 上の文書を更新する（段階1・2の記述と矛盾しないこと）。
- [ ] **Step 2:** EditMode・PlayMode 全体を実行して PASS、`test-summary.py` が「.meta will be ignored」を報告しないことを確かめる。
- [ ] **Step 3:** `scripts/capture-screens.sh` と、Task 6・8・9 で加えた撮影を実行し、画像を確かめる。
- [ ] **Step 4:** Commit：

```bash
git add GAME.md CLAUDE.md AGENTS.md
git commit -m "Document the market, shop, wholesale, per-cage decor and ledger"
```

- [ ] **Step 5:** `scripts/build-ios.sh --run` で実機に入れる（段階2のセーブが残っている端末で、起動時に「装飾を所持品に移しました…」が出ること、ケージ1の装飾が空で「そうしょく」から置き直せること、ショップで生体を買って台帳に「生体の購入」、卸して「卸売り」が残ることを利用者に確かめてもらう）。
