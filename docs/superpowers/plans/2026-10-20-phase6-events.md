# 段階6：相場・イベント 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 毎月15日のイベント（小規模「レプタイル・マルシェ」・大規模「ジャパン・レプタイルズ・フェスタ（春・夏・秋・冬）」）に、前日までに出品（最大5／10匹・価格・値引きの許容幅）して出店料を払って申し込む。価格を動かすたびに基準値（需要込み）・売れる確率・期待値を出す。開催日0:00に出品を固定し、当日は決まった回数（5／8回）の行動を「接客」か「他のブースを見る」に使う。行動ごとに売れた個体を台帳の「イベント売上」に移し、ブースではショップに出ないモルフ・遺伝要素の個体を買える。イベントは遊ぶ人を待ち（締め切りなし）、結果は乱数の種から決まる。演出（ブース・来場者のドット絵・吹き出し・下のログ）は確定済みの結果を見せるだけで、飛ばせる。最後に集計を出す。

**Architecture:** 開催の予定（`EventSchedule`）、売れる確率の計算（`EventOdds`）、状態の型と出品できる条件・乱数の種（`EventState`・`EventRules`・`EventRandom`）、ブースの売り物（`BoothStockGenerator`）、申し込み・固定・通知（`EventService`）、当日の行動・買い物・集計（`EventService.Day`）は Unity に依存しない Core の純粋なクラスにして EditMode テストで確かめる。出品中のロックは `BreedingService`・`ShopService` に1行ずつ足す。画面の文字と演出の台本は純粋な `EventText`・`EventReplay`（UI アセンブリ）に分けてテストする。`ColonySession.ApplyUntil` が最後に `EventService.Advance` を呼び、`ColonySaveService` がスキーマ4で `events` を保存する（スキーマ3は「申し込みなし」で読む）。イベントタブ（新しい `EventView`）が準備・当日・結果の3画面を切り替える。

**Tech Stack:** Unity 6000.5.10f1（UI Toolkit、JsonUtility、NUnit / Unity Test Framework）、C#、macOS 上の CLI（`scripts/run-unity-tests.sh`、`scripts/test-summary.py`、`scripts/capture-screens.sh`、`scripts/build-ios.sh`）、Python（`tools/sprites`）。

**Spec:** `docs/superpowers/specs/2026-10-20-phase6-events-design.md`（正）。親：`docs/superpowers/specs/2026-09-25-breeder-sim-design.md`（§10 相場と販売確率、§11 イベント、§12 セーブ、§13 テスト、§14 段階6）。

## Global Constraints

- ユーザーへの応答は日本語。コード・コメント・コミットメッセージは英語（CLAUDE.md）。
- 開催（spec §2.1・親 §11）：毎月15日。4・7・10・12月は大規模（出店料10,000円・最大10匹・行動8回・上限1.5・珍しい個体25%）、ほかの月は小規模（3,000円・最大5匹・行動5回・上限1.2・15%）。イベントの番号＝ゲーム開始からの月の通し番号（2026年4月＝0）。
- 売れる確率（親 §10.2・spec §2.4）：r＝実効の価格 ÷ 基準値（需要込み）、実効の価格＝価格 ×（1 − d/2）。P＝上限 ÷（1＋exp(k(r−1))）、k＝ln3 ÷ 0.3、最大98%。1回の接客 a＝1 −（1 − P）^(1/N)、ブースを見る行動は a × 0.6。売値＝価格 ×（1 − d × u）を100円単位に丸める。期待値＝実効の価格 × P。許容幅 d は 0・5・10・15・20%。
- 基準値（親 §10.1）は既存の `MarketPrice.For(pet, EventDemand.For(...), pet.Weak)`。新しい相場の式は作らない。
- ブース（spec §2.5）：1回1〜3匹。珍しい個体が1匹以上出る確率は小規模15%・大規模25%。値段は相場 × 需要 × 1.0〜1.15、値切りなし。卵は売らない。空きケージがないと買えない（「ケージが足りません」、「購入」は押せない）。
- 行動の結果は乱数の種（イベントの番号、出品の内容と価格、行動の番号、行動の種類）から決める。時刻（遊んだ時刻）に結果を依存させない。
- 台帳の時刻は行動を遊んだ時刻（ゲーム内の今）。説明の文にイベント名を入れる。売れた個体は `Colony.RemoveAnimal` で手放す（家系の記録は既存の処理で残る）。
- セーブはスキーマ4（`CurrentSchemaVersion = 4`）。スキーマ3のファイルは「申し込みなし」で読む（`MigrateLegacy` を通さない）。スキーマ1・2・段階1〜5のファイルも読めること。JsonUtility の制約（フィールド、辞書なし、旧ファイルにない int は0、string は null、入れ子のクラスは null にならない）。
- 時間の扱い：ゲーム内の今＝現実の今＋`Colony.GameClockOffset`。ゲーム内1日＝実時間48分（`GameCalendar.RealTimeFor(days)`）。
- 画面の文字は日本語。マイナス記号は ASCII の `-`。範囲は全角の「〜」。金額は「28,500円」（`CultureInfo.InvariantCulture` の `#,0`）。
- 固定の高さの欄は `visibility`、画面（パネル・モーダル）の切り替えは `display`（既存の方針）。
- 画面のタスクは「1画面・4ファイル以内」。指示書の行番号は段階6開始時（HEAD 2e72385）の `cs-outline` のもの。前のタスクでずれるので、作業前に `python3 ~/.claude/skills/minimal-read-edit/scripts/cs-outline.py <file> [--grep Word]` で確かめる。`TerrariumView.cs`（約73KB）は丸ごと読まない。
- 画面のタスクの確認は画面キャプチャで行い、**縮小した画像**を見る：`mkdir -p Logs/Screens/small && sips -Z 800 Logs/Screens/<name>.png --out Logs/Screens/small/<name>.png`。報告には縮小画像のパスを書く（原寸を読み込まない）。
- 新しい .cs ファイルの .meta は手書きしない。Unity に生成させる。`scripts/test-summary.py` が「.meta will be ignored」を報告したら失敗扱い。
- テストの件数は減らさない（段階6開始時：EditMode 624 passed、PlayMode 103 passed / 14 skipped）。

## Review Focus（各タスクの task-reviewer と最後の全体レビューで必ず見る5点）

1. 全部を接客に使うと全体の確率がちょうど P になる — Task 2 `EventOddsTests.PerActionChance_AllServeMatchesEventChance`、Task 6 `EventDayTests.AllServe_SaleRateIsNearEventChance`。
2. 同じ選び方なら同じ結果、当日を過ぎてから遊んでも同じ — Task 6 `EventDayTests.PlayingLate_GivesSameResults`・`SameChoices_GiveSameResults`。
3. 珍しい個体はショップに出ず、出る割合が15%／25% — Task 4 `BoothStockGeneratorTests.ShopStock_IsNeverRare`・`RareBoothRate_SmallIsAbout15Percent`・`RareBoothRate_BigIsAbout25Percent`。
4. セーブ：スキーマ3は申し込みなしで読み、当日の途中の状態が往復し、異常値の出品は取り消す — Task 9 `ColonySaveServiceTests.Schema3Save_LoadsWithNoEvent`・`EventMidDay_RoundTrips`・`EventListing_MissingAnimalIsDropped`。
5. 段階の完了条件：申し込み → 当日 → 接客と買い物 → 集計 → 台帳に売上 — Task 15 `TerrariumViewTests.EventLoop_RegisterPlayBuyAndLedger`。

## 計画者の判断（仕様書に書かれていない点）

★はプレイヤーに見える判断（HQ が利用者に確認するための印）。spec の ★A〜R はそのまま採用し、ここでは繰り返さない。

| # | 判断 | 理由 |
|---|---|---|
| ★1 | 最初のイベントは番号0＝2026年4月15日（大規模・春）。需要の種は番号とコロニーの種（`BreedingSeed`）から作る（部屋ごとに需要が違う） | 番号は spec §2.1 の「月の通し番号」。番号だけを種にすると全員同じ需要になる |
| ★2 | 出品0匹でも申し込める（買い物だけの参加。出店料は同じ）。申し込みは取り消せないが、前日までは出品を0匹まで減らせる | spec は「売る場所であり買う場所」。取り消すと出店料の扱いが増える（★B で返らない） |
| ★3 | 価格の初期値はその時点の基準値（需要込み）。−／＋で1,000円ずつ、最低1,000円。値引きの許容幅の初期値は0%。準備の画面の下に「期待値の合計」を出す | 1,000円刻みは操作の回数が少なく、確率の変化も分かる。0%は「何もしない」の意味が明確 |
| ★4 | 開催時間は10:00〜17:00。行動 k はこれを N 等分した時間帯で、ログの時刻はその中の乱数 | ログの「11:24」の形（spec §2.6）を出すため。時刻は演出だけで結果を変えない |
| ★5 | 行動を使い切ったとき（「残りを全部接客にする」も同じ）、まだ買っていない売り物があれば「集計へ」を押すまで買える。なければすぐ集計（確定）する | 最後の行動が「他のブースを見る」だと、見せた売り物を買えずに確定してしまう（spec §2.7 の「使い切ったら確定」と ★O の両立） |
| ★6 | 「珍しい」の判定（どれか1つ）：見える遺伝要素が3つ以上／ベル・レインウォーターとほかの見える要素の組み合わせ／確定ヘテロ3つ以上／見える要素があり、ハイポかタンジェリンの値が80以上。ショップの品ぞろえはこの条件を満たさない（テストで確かめる） | spec §2.5 の4つの方針を判定できる形にした。ショップは見える要素2つまで・ヘテロ2つまで・見える要素の個体の多因子は40以下 |
| ★7 | ブースの成長段階はベビー20%・ヤング45%・アダルト35%。体重はベビー4〜12g・ヤング16〜30g・アダルト45〜70g。ヤング以上は雌雄が分かる。珍しくない売り物はショップと同じ作り方（`ShopStockGenerator.Animal`）で、成長段階だけ置き換える | ★P。ショップと同じ形にそろえる |
| ★8 | ブースで買った個体の名前は「レオパ番号」、性格は7日後に判明、番号の小さい空きケージへ入れる（ショップと同じ） | spec §2.5 |
| ★9 | 通知の文：3日前「{名前}（5月15日）の申し込みはあと3日で締め切りです」、前日「{名前}の申し込みは今日までです」、当日0:00「今日は{名前}です。イベントタブで遊べます」、続き「{名前}の続きがあります（イベントタブ）」、取り消し「出品を取り消しました：ハナ、タロウ」。閉じている間に3日前と前日の両方を過ぎたら新しい方だけ出す | ★L。段階5と同じく「最初の1件＋（ほか○件）」にまとめられる |
| ★10 | 「出品中」はケージ詳細の状態の行とペアリングの候補（理由「出品中」）に出す。卸売り・トレイの「売る」は「出品中の個体は売れません」 | ★C |
| ★11 | 結果の画面は「閉じる」を押すまで（アプリを閉じても）出す。閉じると次の回の準備の画面になる | 確定はすぐでも、集計を見逃さないため |
| ★12 | 演出の長さは1行動あたり10秒＋（売約と見せた売り物1つにつき1.5秒）、最大15秒。「飛ばす」で残りのログを一度に出す。演出の間は行動のボタンを押せない | ★K（10〜15秒）の範囲で、出来事が多いほど長くする |
| ★13 | 台帳の文：「{名前}の出店料」「{名前}：ハナ（レイプター）が売れました」「{名前}：レオパ12（…）を購入」（モルフは `MorphNamer.FullName`） | ★R |
| ★14 | デバッグ「次のイベントの当日へ」：申し込みがあればその回、なければ受付中の回の開催日0:00の1秒後まで時計を進める（既存の `SimulateGameTime` と同じくゲーム時計のずれに加える） | spec §4。PlayMode と画面キャプチャでも使う |
| ★15 | ブースの値段は100円単位。ログ・集計の個体の書き方は「レイプター♀」（雌雄不明は「?」） | spec §2.6 の例 |
| 16 | 閉じている間に開催日0:00を過ぎたら、開いたとき（`ApplyUntil` の中、個体の時間を進めた後）の個体の状態で固定する | 0:00 の時点の個体の状態を遡って作れない。ずれは最大で閉じていた時間だけ |
| 17 | 乱数：需要の種＝`EventDemand.Unit(BreedingSeed, "event-demand:"+番号)`、出品の種＝`EventDemand.Unit(BreedingSeed, "event-listing:"+番号+"|"+出品（番号:価格:幅）を並べ替えて連結)`。行動は `BreedingRandom.For(出品の種, 6, 行動の番号, 種類)`、ブースは `BreedingRandom.For(出品の種, 7, 行動の番号, 0)`。行動では出品を個体の番号の順に、1匹につき必ず3つ（売れたか・値引き・時刻）引く | 既存の FNV-1a＋SplitMix64 を流用（環境で変わらない）。引く数を固定すると、売れた個体があっても後の個体の結果がずれない |
| 18 | `PairingProblem.Listed`・`ShopResult.Listed` は列挙の最後に足す | 既存の値の番号を変えない |
| 19 | 売れた個体・売れ残り・買った個体の行は1つの型 `EventAnimalLine`（番号・名前・モルフ表示・金額・時刻）にする。売れた個体の名前とモルフは売れた時点で記録する | 売れた個体は手元から消えるため。セーブの型も1つで済む |
| 20 | 申し込み・直しで受け付ける価格は100円以上、許容幅は5段階だけ。出品できない個体・同じ個体の重複・上限を超える数は申し込み全体を断る（理由を返す）。画面ではそうならないように押せなくする | 画面の制御を Core でも守る |
| 21 | 準備中に出品の個体がいなくなったら（`AnimalById` が null）、`Advance` のたびに取り消して通知する。名前は家系の記録（`Colony.Lineage`）から取る | spec §2.9 |
| 22 | セーブ：`events`（`EventStateSaveData`）を足す。JsonUtility の入れ子は null にならないので、`hasEntry`・`hasSummary` の旗と、「番号＋1」の欄（0＝なし）を使う。スキーマ3→4 ではバックアップを作らない（欄を足すだけ。段階5と同じ） | 旧ファイルの int は0になる |
| 23 | セーブの異常系：番号が負の申し込みは捨てる。固定前の出品は、個体がいない・重複・価格が0以下・許容幅が5段階にない・上限を超えた分を取り消す。使った行動が回数を超えた分は切り捨て、種類が読めなければ接客。個体の入っていないブースの売り物は捨てる | spec §2.9 の「異常な値はその出品を取り消して読み込む」 |
| 24 | 出品中のロックは「申し込み中（確定前）のイベントに出品している」こと。売れた個体はもういないので、確定と同時に全部外れる | ★C |

## ファイル構成

| ファイル | 種別 | 役割 |
|---|---|---|
| `Assets/Scripts/Core/EventSchedule.cs` | 新規 | `EventKind`・開催の予定・規模ごとの数値・名前・時間帯（Task 1） |
| `Assets/Scripts/Core/EventOdds.cs` | 新規 | 売れる確率・1回ごとの確率・期待値・売値（Task 2） |
| `Assets/Scripts/Core/EventState.cs` | 新規 | 状態の型（`EventState`・`EventEntry`・`EventListing`・`EventActionRecord`・`EventBoothOffer`・`EventAnimalLine`・`EventSummary`）（Task 3） |
| `Assets/Scripts/Core/EventRules.cs` | 新規 | `ListingProblem`・出品できる条件・基準値・モルフ表示・`EventRandom`（Task 3） |
| `Assets/Scripts/Core/Colony.cs` | 変更 | `Events` の欄（Task 3） |
| `Assets/Scripts/Core/BoothStockGenerator.cs` | 新規 | ブースの売り物・珍しさの判定（Task 4） |
| `Assets/Scripts/Core/EventService.cs` | 新規 | 申し込み・直し・固定・通知（`EventReport`）（Task 5） |
| `Assets/Scripts/Core/EventService.Day.cs` | 新規 | 行動・残りを全部接客・買い物・集計（Task 6） |
| `Assets/Scripts/UI/EventText.cs`・`Assets/Scripts/UI/EventReplay.cs` | 新規 | 画面の文字・通知の文・演出の台本（Task 7） |
| `Assets/Scripts/Core/BreedingRules.cs`・`BreedingService.cs`・`ShopService.cs` | 変更 | 出品中のロック（Task 8） |
| `Assets/Scripts/Core/ColonySession.cs`・`ColonySaveData.cs`・`ColonySaveService.cs` | 変更 | 結線・スキーマ4・異常系・デバッグの時計（Task 9） |
| `tools/sprites/event_sprites.py`・`Assets/Resources/Event/*.png` | 新規 | 来場者3種×2コマ・ブースの絵（Task 10） |
| `Assets/UI/Terrarium.uxml`・`Terrarium.uss`・`Assets/Scripts/UI/ShellNavigator.cs` | 変更 | イベントタブの枠（準備・当日・結果・購入ダイアログ）（Task 11） |
| `Assets/Scripts/UI/EventView.cs` | 新規 | 準備の画面（Task 12）、当日と結果の画面（Task 13） |
| `Assets/Scripts/UI/TerrariumView.cs` | 変更 | 結線（Task 12〜14） |
| `Assets/Scripts/UI/BreedingText.cs`・`ShopText.cs`・`CageStatusText.cs` | 変更 | 「出品中」の表示（Task 14） |
| テスト | 新規・変更 | EditMode：`EventScheduleTests`・`EventOddsTests`・`EventRulesTests`・`BoothStockGeneratorTests`・`EventServiceTests`・`EventDayTests`・`EventTextTests`・`EventLockTests`（新規）、`ColonySaveServiceTests`・`ColonySessionTests`。PlayMode：`TerrariumViewTests`・`ScreenCaptureTests` |
| `GAME.md`・`CLAUDE.md` | 変更 | イベントの説明・スキーマ4（Task 16） |

テストの実行（全タスク共通）：

```bash
scripts/run-unity-tests.sh EditMode && python3 scripts/test-summary.py Logs/EditMode-results.xml
scripts/run-unity-tests.sh PlayMode && python3 scripts/test-summary.py Logs/PlayMode-results.xml
```

特定のテストだけ流すときは `-testFilter <完全名>` を付ける（例：`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.EventOddsTests`）。各タスクの前に全体の件数を控え、タスクの後に減っていないことを確かめる。

## タスクの一覧と作業者

| Task | 内容 | 作業者 |
|---|---|---|
| 1 | 開催の予定（規模・番号・名前・日時・数値・時間帯） | transcriber |
| 2 | 売れる確率・1回ごとの確率・期待値・売値 | transcriber |
| 3 | 状態の型・出品できる条件・乱数の種・`Colony.Events` | transcriber |
| 4 | ブースの売り物と珍しさの判定 | transcriber |
| 5 | 申し込み・直し・開催日0:00の固定・通知 | transcriber |
| 6 | 当日の行動・残りを全部接客・買い物・集計 | transcriber |
| 7 | 画面の文字と演出の台本（`EventText`・`EventReplay`） | transcriber |
| 8 | 出品中のロック（ペアリング・卸売り） | transcriber |
| 9 | セッションとセーブ（結線・スキーマ4・異常系・デバッグの時計） | programmer |
| 10 | 来場者とブースのドット絵 | graphics |
| 11 | イベントタブの枠（準備・当日・結果・購入ダイアログ） | graphics |
| 12 | 準備の画面（出品・価格・許容幅・その場の数字・申し込み・直し） | programmer（画面キャプチャで確認） |
| 13 | 当日と結果の画面（行動・演出・ブースの買い物・集計） | programmer（画面キャプチャで確認） |
| 14 | 通知・「出品中」の表示・デバッグ「次のイベントの当日へ」 | programmer |
| 15 | PlayMode の1周「申し込み → 当日 → 接客と買い物 → 集計 → 台帳に売上」（段階の完了条件） | programmer |
| 16 | 文書の更新と最終確認（実機） | programmer（最後に HQ が利用者に確認） |

依存：1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12 → 13 → 14 → 15 → 16。順番に行う（1〜8 は Core と純粋な文字、9 で結線、10 以降は画面）。1・2 は互いに独立。7・8 は 6 の後なら入れ替えてよい。10 は Core と独立なので、HQ の判断でいつ行ってもよい。

---

## transcriber タスク共通の前提（Task 1〜8）

計画者は次の点を段階6開始時（HEAD 2e72385）のコードで確かめたが、作業の最初に `grep -n` で確かめ、違っていたら**機械的な置き換えだけ**（列挙値の名前、戻り値を変数で受けるか否か）をしてよい。それ以上の変更が要るときは止めて報告する。

- `Colony.CreateNew(DateTimeOffset, EconomyTuning, CareTuning, Random)` は標準ケージ1つ（ベビー1匹入り）と所持金50,000円で始まる。`Colony.CagesPerRack = 4`、`RackCount` は書き換えられる。`AddCage(CageSize.Standard)` は上限を超えると null。
- `Colony.AddAnimal(PetState, Cage)` は追加した個体を返し、`cage` が null なら孵化トレイ（ケージに入れない）。`RemoveAnimal(pet)` は家系の記録（`Lineage`、`LineageRecord.Id`・`Name`）を残す。`FirstEmptyCage()` は番号の小さい空きケージ。
- `Colony.BreedingSeed` は `CalendarEpochUtc` から決まる int。`Colony.Pairings` は `Pairing { MaleId, FemaleId }` の一覧で、`PairingOf(pet)` がそれを探す。
- `PetState`：`Sex`（`Sex.Female`／`Sex.Male`）、`SexRevealed`（`SexKnown` はこれを返す）、`Stage`（`GrowthStage.Baby/Juvenile/Adult`）、`WeightGrams`、`Weak`、`Gravid`（`GravidState`、null＝抱卵していない）、`PersonalityKnown`、`PersonalityRevealAtUtc`、`Genotype`、`Known`。
- `Genotype.Normal(hypo, tangerine)`・`Set(GeneId, copies)`（自分を返す）・`Shows(GeneId)`・`Hypo`・`Tangerine`。`Genes.All`・`Genes.IsRecessive`。`KnownGenetics.SetHet(gene, p)`・`HetProbability(gene)`・`KnownGenetics.IsProvenHet(p)`。
- `MarketPrice.For(pet, demand, weak)`・`MarketPrice.RoundYen(double)`（100円単位、最低100円）。`EventDemand.For(seed, genotype, isBig)`・`EventDemand.Unit(seed, key)`。`BreedingRandom.For(seed, stream, id, index)`・`BreedingRandom.Uniform(random, min, max)`。
- `ShopStockGenerator.Animal(Random, DateTimeOffset, CareTuning)`・`Generate(Random, DateTimeOffset, CareTuning)` は public。`SheddingModel.IntervalFor(stage, care)`、`PersonalityTraits.Roll(Random)`、`CareTuning.PersonalityRevealGameDays`。
- `Wallet.TrySpend(amount, category, note, atUtc)`・`Earn(...)`、`LedgerCategory.BoothFee`・`EventSale`・`AnimalPurchase`、`LedgerEntry.AtUtc/Category/Amount/Note`。
- テストの名前空間は `TerrariumDays.Tests`、Core は `TerrariumDays.Core`、UI は `TerrariumDays.UI`。
