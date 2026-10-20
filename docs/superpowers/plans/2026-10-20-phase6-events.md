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
| ★16 | イベントタブの金額は spec §2.6 の例どおり「28,500円」の形にする（`EventText.Yen`）。ほかのタブの既存の「¥28,500」（`ShopText.Yen`）は変えない | spec のログの例が「円」。既存のタブを変えると段階1〜5のテストが動く |
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

---

### Task 1: 開催の予定（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/EventSchedule.cs`
- Test: `Assets/Tests/EditMode/EventScheduleTests.cs`

- [ ] **Step 1: テストを書く**

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EventScheduleTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        private readonly GameCalendar calendar = new GameCalendar(Epoch);

        [Test]
        public void NumberZero_IsTheSpringFestivalInApril2026()
        {
            Assert.That(EventSchedule.MonthOf(0), Is.EqualTo(4));
            Assert.That(EventSchedule.YearOf(0), Is.EqualTo(2026));
            Assert.That(EventSchedule.KindOf(0), Is.EqualTo(EventKind.Big));
            Assert.That(EventSchedule.NameOf(0), Is.EqualTo("ジャパン・レプタイルズ・フェスタ（春）"));
        }

        [TestCase(1, 5, 2026, EventKind.Small, "レプタイル・マルシェ")]
        [TestCase(3, 7, 2026, EventKind.Big, "ジャパン・レプタイルズ・フェスタ（夏）")]
        [TestCase(6, 10, 2026, EventKind.Big, "ジャパン・レプタイルズ・フェスタ（秋）")]
        [TestCase(8, 12, 2026, EventKind.Big, "ジャパン・レプタイルズ・フェスタ（冬）")]
        [TestCase(9, 1, 2027, EventKind.Small, "レプタイル・マルシェ")]
        [TestCase(12, 4, 2027, EventKind.Big, "ジャパン・レプタイルズ・フェスタ（春）")]
        public void Months_KindsAndNames(int number, int month, int year, EventKind kind, string name)
        {
            Assert.That(EventSchedule.MonthOf(number), Is.EqualTo(month));
            Assert.That(EventSchedule.YearOf(number), Is.EqualTo(year));
            Assert.That(EventSchedule.KindOf(number), Is.EqualTo(kind));
            Assert.That(EventSchedule.NameOf(number), Is.EqualTo(name));
        }

        [Test]
        public void EachYear_HasFourBigEvents()
        {
            var big = 0;
            for (var n = 0; n < 12; n++)
            {
                if (EventSchedule.IsBig(n))
                {
                    big++;
                }
            }

            Assert.That(big, Is.EqualTo(4));
        }

        [Test]
        public void DateAndDeadline_AreThe15thAndThe14th()
        {
            Assert.That(EventSchedule.DateOf(1).ToDisplayText(), Is.EqualTo("2026年5月15日"));
            Assert.That(EventSchedule.DeadlineDateOf(1).ToDisplayText(), Is.EqualTo("2026年5月14日"));
            Assert.That(EventSchedule.DateOf(9).ToDisplayText(), Is.EqualTo("2027年1月15日"));
        }

        [Test]
        public void StartUtc_IsMidnightOfThe15th()
        {
            Assert.That(EventSchedule.StartUtc(calendar, 0), Is.EqualTo(Epoch + TimeSpan.FromMinutes(14 * 48)));
            Assert.That(calendar.DateAt(EventSchedule.StartUtc(calendar, 1)).ToDisplayText(), Is.EqualTo("2026年5月15日"));
            Assert.That(calendar.DateAt(EventSchedule.StartUtc(calendar, 1) - TimeSpan.FromSeconds(1)).ToDisplayText(), Is.EqualTo("2026年5月14日"));
            Assert.That(EventSchedule.EndUtc(calendar, 1) - EventSchedule.StartUtc(calendar, 1), Is.EqualTo(TimeSpan.FromMinutes(48)));
        }

        [Test]
        public void NextOpenNumber_IsTheNextEventThatHasNotStarted()
        {
            var second = TimeSpan.FromSeconds(1);
            Assert.That(EventSchedule.NextOpenNumber(calendar, Epoch), Is.EqualTo(0));
            Assert.That(EventSchedule.NextOpenNumber(calendar, EventSchedule.StartUtc(calendar, 0) - second), Is.EqualTo(0));
            Assert.That(EventSchedule.NextOpenNumber(calendar, EventSchedule.StartUtc(calendar, 0)), Is.EqualTo(1));
            Assert.That(EventSchedule.NextOpenNumber(calendar, EventSchedule.StartUtc(calendar, 1) - second), Is.EqualTo(1));
            Assert.That(EventSchedule.NextOpenNumber(calendar, EventSchedule.StartUtc(calendar, 1)), Is.EqualTo(2));
        }

        [Test]
        public void HasStartedAndDaysUntilStart()
        {
            var start = EventSchedule.StartUtc(calendar, 2);
            Assert.That(EventSchedule.HasStarted(calendar, 2, start - TimeSpan.FromSeconds(1)), Is.False);
            Assert.That(EventSchedule.HasStarted(calendar, 2, start), Is.True);
            Assert.That(EventSchedule.GameDaysUntilStart(calendar, 2, start - GameCalendar.RealTimeFor(3)), Is.EqualTo(3d).Within(1e-9));
        }

        [Test]
        public void NumbersPerKind()
        {
            Assert.That(EventSchedule.BoothFee(EventKind.Small), Is.EqualTo(3000));
            Assert.That(EventSchedule.BoothFee(EventKind.Big), Is.EqualTo(10000));
            Assert.That(EventSchedule.MaxListings(EventKind.Small), Is.EqualTo(5));
            Assert.That(EventSchedule.MaxListings(EventKind.Big), Is.EqualTo(10));
            Assert.That(EventSchedule.ActionCount(EventKind.Small), Is.EqualTo(5));
            Assert.That(EventSchedule.ActionCount(EventKind.Big), Is.EqualTo(8));
            Assert.That(EventSchedule.SaleCap(EventKind.Small), Is.EqualTo(1.2d));
            Assert.That(EventSchedule.SaleCap(EventKind.Big), Is.EqualTo(1.5d));
            Assert.That(EventSchedule.RareChance(EventKind.Small), Is.EqualTo(0.15d));
            Assert.That(EventSchedule.RareChance(EventKind.Big), Is.EqualTo(0.25d));
        }

        [Test]
        public void ActionWindows_SplitTenToFiveEvenly()
        {
            Assert.That(EventSchedule.ActionWindowStart(0, 5), Is.EqualTo(600));
            Assert.That(EventSchedule.ActionWindowStart(4, 5), Is.EqualTo(936));
            Assert.That(EventSchedule.ActionWindowLength(5), Is.EqualTo(84));
            Assert.That(EventSchedule.ActionWindowStart(7, 8) + EventSchedule.ActionWindowLength(8), Is.LessThanOrEqualTo(1020));
        }
    }
}
```

- [ ] **Step 2: 実装を書く**

```csharp
using System;

namespace TerrariumDays.Core
{
    public enum EventKind
    {
        Small,
        Big
    }

    /// <summary>What one event action is used for (phase 6 spec §2.3). Appended values only.</summary>
    public enum EventActionKind
    {
        Serve,
        Browse
    }

    /// <summary>
    /// The monthly event calendar (§11, phase 6 spec §2.1): every month on the 15th, big in April,
    /// July, October and December. Event number n is the n-th game month (April 2026 = 0).
    /// </summary>
    public static class EventSchedule
    {
        public const int EventDayOfMonth = 15;
        public const int DeadlineDayOfMonth = 14;
        public const int OpenMinuteOfDay = 10 * 60;
        public const int CloseMinuteOfDay = 17 * 60;
        public const string SmallName = "レプタイル・マルシェ";
        public const string BigName = "ジャパン・レプタイルズ・フェスタ";

        public static int MonthOf(int number) => (GameCalendar.StartMonth - 1 + number) % 12 + 1;

        public static int YearOf(int number) => GameCalendar.StartYear + (GameCalendar.StartMonth - 1 + number) / 12;

        public static bool IsBig(int number)
        {
            var month = MonthOf(number);
            return month == 4 || month == 7 || month == 10 || month == 12;
        }

        public static EventKind KindOf(int number) => IsBig(number) ? EventKind.Big : EventKind.Small;

        public static string NameOf(int number)
        {
            switch (MonthOf(number))
            {
                case 4:
                    return BigName + "（春）";
                case 7:
                    return BigName + "（夏）";
                case 10:
                    return BigName + "（秋）";
                case 12:
                    return BigName + "（冬）";
                default:
                    return SmallName;
            }
        }

        public static GameDate DateOf(int number) => new GameDate(YearOf(number), MonthOf(number), EventDayOfMonth);

        public static GameDate DeadlineDateOf(int number) => new GameDate(YearOf(number), MonthOf(number), DeadlineDayOfMonth);

        /// <summary>Game days from the epoch to 0:00 on the event day (day 0 is the 1st).</summary>
        public static double StartGameDay(int number) => (double)number * GameCalendar.DaysPerMonth + (EventDayOfMonth - 1);

        public static DateTimeOffset StartUtc(GameCalendar calendar, int number) =>
            calendar.EpochUtc + GameCalendar.RealTimeFor(StartGameDay(number));

        /// <summary>0:00 on the day after the event (the "continue" notice is for events past this).</summary>
        public static DateTimeOffset EndUtc(GameCalendar calendar, int number) =>
            calendar.EpochUtc + GameCalendar.RealTimeFor(StartGameDay(number) + 1d);

        public static bool HasStarted(GameCalendar calendar, int number, DateTimeOffset nowUtc) => nowUtc >= StartUtc(calendar, number);

        public static double GameDaysUntilStart(GameCalendar calendar, int number, DateTimeOffset nowUtc) =>
            GameCalendar.GameDaysBetween(nowUtc, StartUtc(calendar, number));

        /// <summary>The first event whose day has not started yet: the only one that takes entries.</summary>
        public static int NextOpenNumber(GameCalendar calendar, DateTimeOffset nowUtc)
        {
            var days = calendar.GameDaysAt(nowUtc);
            var first = EventDayOfMonth - 1;
            return days < first ? 0 : (int)Math.Floor((days - first) / GameCalendar.DaysPerMonth) + 1;
        }

        public static long BoothFee(EventKind kind) => kind == EventKind.Big ? 10000L : 3000L;

        public static int MaxListings(EventKind kind) => kind == EventKind.Big ? 10 : 5;

        public static int ActionCount(EventKind kind) => kind == EventKind.Big ? 8 : 5;

        /// <summary>The cap of the §10.2 sale chance.</summary>
        public static double SaleCap(EventKind kind) => kind == EventKind.Big ? 1.5d : 1.2d;

        /// <summary>The chance that one visited booth shows at least one rare animal (user, 2026-10-20).</summary>
        public static double RareChance(EventKind kind) => kind == EventKind.Big ? 0.25d : 0.15d;

        /// <summary>Action k covers an equal slice of 10:00–17:00 (only for the log's clock; it never changes results).</summary>
        public static int ActionWindowStart(int index, int actionCount) =>
            OpenMinuteOfDay + (CloseMinuteOfDay - OpenMinuteOfDay) * index / actionCount;

        public static int ActionWindowLength(int actionCount) => (CloseMinuteOfDay - OpenMinuteOfDay) / actionCount;
    }
}
```

先に `grep -n "GameDaysAt\|GameDaysBetween\|RealTimeFor\|StartMonth\|StartYear\|DaysPerMonth" Assets/Scripts/Core/GameCalendar.cs` で、`GameDaysAt` が double を返すインスタンスメソッド、`GameDaysBetween`・`RealTimeFor` が static、定数が public であることを確かめる。

- [ ] **Step 3: テストを流す**

`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.EventScheduleTests` → 全部通る。続けて EditMode 全体を流し、件数が減っていないこと。

- [ ] **Step 4: コミット**

```bash
git add Assets/Scripts/Core/EventSchedule.cs* Assets/Tests/EditMode/EventScheduleTests.cs*
git commit -m "Add the event schedule"
```

（コミットメッセージの最後に共通の Co-Authored-By 行を付ける。以下のタスクも同じ。）

---

### Task 2: 売れる確率（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/EventOdds.cs`
- Test: `Assets/Tests/EditMode/EventOddsTests.cs`

- [ ] **Step 1: テストを書く**

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EventOddsTests
    {
        [Test]
        public void SaleChance_AtTheBaseValue_IsHalfTheCap()
        {
            Assert.That(EventOdds.SaleChance(10000, 0, 10000, EventKind.Small), Is.EqualTo(0.6d).Within(1e-9));
            Assert.That(EventOdds.SaleChance(10000, 0, 10000, EventKind.Big), Is.EqualTo(0.75d).Within(1e-9));
        }

        [Test]
        public void SaleChance_At130Percent_IsAQuarterOfTheCap()
        {
            Assert.That(EventOdds.SaleChance(13000, 0, 10000, EventKind.Small), Is.EqualTo(0.3d).Within(1e-9));
        }

        [Test]
        public void SaleChance_At70Percent_IsThreeQuartersOfTheCapUpTo98Percent()
        {
            Assert.That(EventOdds.SaleChance(7000, 0, 10000, EventKind.Small), Is.EqualTo(0.9d).Within(1e-9));
            Assert.That(EventOdds.SaleChance(7000, 0, 10000, EventKind.Big), Is.EqualTo(EventOdds.MaxChance));
            Assert.That(EventOdds.SaleChance(1000, 0, 10000, EventKind.Big), Is.EqualTo(0.98d));
        }

        [Test]
        public void Tolerance_UsesHalfOfItForTheEffectivePrice()
        {
            Assert.That(EventOdds.EffectivePrice(10000, 20), Is.EqualTo(9000d).Within(1e-9));
            Assert.That(EventOdds.SaleChance(10000, 20, 9000, EventKind.Small), Is.EqualTo(0.6d).Within(1e-9));
            Assert.That(EventOdds.SaleChance(10000, 20, 10000, EventKind.Small), Is.GreaterThan(EventOdds.SaleChance(10000, 0, 10000, EventKind.Small)));
        }

        [Test]
        public void SaleChance_WithoutABaseValueOrPrice_IsZero()
        {
            Assert.That(EventOdds.SaleChance(10000, 0, 0, EventKind.Small), Is.EqualTo(0d));
            Assert.That(EventOdds.SaleChance(0, 0, 10000, EventKind.Small), Is.EqualTo(0d));
        }

        [TestCase(0.1d, 5)]
        [TestCase(0.6d, 5)]
        [TestCase(0.98d, 5)]
        [TestCase(0.3d, 8)]
        [TestCase(0.75d, 8)]
        public void PerActionChance_AllServeMatchesEventChance(double eventChance, int actions)
        {
            var a = EventOdds.PerActionChance(eventChance, actions);

            Assert.That(1d - Math.Pow(1d - a, actions), Is.EqualTo(eventChance).Within(1e-12));
            Assert.That(a, Is.LessThan(eventChance));
        }

        [Test]
        public void ActionChance_BrowseIsSixtyPercentOfServe()
        {
            var serve = EventOdds.ActionChance(0.6d, 5, EventActionKind.Serve);

            Assert.That(serve, Is.EqualTo(EventOdds.PerActionChance(0.6d, 5)));
            Assert.That(EventOdds.ActionChance(0.6d, 5, EventActionKind.Browse), Is.EqualTo(serve * 0.6d).Within(1e-12));
            Assert.That(EventOdds.PerActionChance(0.6d, 0), Is.EqualTo(0d));
        }

        [Test]
        public void ExpectedValue_IsTheEffectivePriceTimesTheChanceToTheNearest100()
        {
            Assert.That(EventOdds.ExpectedValue(10000, 10, 0.6d), Is.EqualTo(5700));
            Assert.That(EventOdds.ExpectedValue(12345, 0, 0.5d), Is.EqualTo(6200));
            Assert.That(EventOdds.ExpectedValue(10000, 0, 0d), Is.EqualTo(0));
        }

        [Test]
        public void SaleAmount_TakesUpToTheToleranceOff()
        {
            Assert.That(EventOdds.SaleAmount(10000, 20, 0d), Is.EqualTo(10000));
            Assert.That(EventOdds.SaleAmount(10000, 20, 0.5d), Is.EqualTo(9000));
            Assert.That(EventOdds.SaleAmount(10000, 20, 1d), Is.EqualTo(8000));
            Assert.That(EventOdds.SaleAmount(10000, 0, 0.9d), Is.EqualTo(10000));
            Assert.That(EventOdds.SaleAmount(12340, 5, 0.37d), Is.EqualTo(12100));
        }

        [Test]
        public void Tolerances_AreTheFiveSteps()
        {
            Assert.That(EventOdds.TolerancePercents, Is.EqualTo(new[] { 0, 5, 10, 15, 20 }));
            foreach (var valid in new[] { 0, 5, 10, 15, 20 })
            {
                Assert.That(EventOdds.IsValidTolerance(valid), Is.True);
            }

            foreach (var invalid in new[] { -5, 3, 25 })
            {
                Assert.That(EventOdds.IsValidTolerance(invalid), Is.False);
            }
        }
    }
}
```

- [ ] **Step 2: 実装を書く**

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Event sale odds (§10.2, phase 6 spec §2.4). P is the chance over the whole event; one
    /// serve action gets a = 1 − (1 − P)^(1/N) so that N serves give exactly P, and a browse
    /// action gets a × 0.6. Pure: the caller supplies the base value and the random numbers.
    /// </summary>
    public static class EventOdds
    {
        public static readonly double K = Math.Log(3d) / 0.3d;
        public const double MaxChance = 0.98d;
        public const double BrowseFactor = 0.6d;
        public static readonly int[] TolerancePercents = { 0, 5, 10, 15, 20 };

        public static bool IsValidTolerance(int percent) => Array.IndexOf(TolerancePercents, percent) >= 0;

        /// <summary>The price the odds see: price × (1 − d/2).</summary>
        public static double EffectivePrice(long price, int tolerancePercent) => price * (1d - tolerancePercent / 200d);

        public static double SaleChance(long price, int tolerancePercent, long baseValue, EventKind kind)
        {
            if (price <= 0 || baseValue <= 0)
            {
                return 0d;
            }

            var r = EffectivePrice(price, tolerancePercent) / baseValue;
            return Math.Min(MaxChance, EventSchedule.SaleCap(kind) / (1d + Math.Exp(K * (r - 1d))));
        }

        public static double PerActionChance(double eventChance, int actionCount) =>
            actionCount <= 0 ? 0d : 1d - Math.Pow(1d - eventChance, 1d / actionCount);

        public static double ActionChance(double eventChance, int actionCount, EventActionKind kind) =>
            PerActionChance(eventChance, actionCount) * (kind == EventActionKind.Browse ? BrowseFactor : 1d);

        /// <summary>Effective price × P to the nearest 100 yen (0 when it cannot sell).</summary>
        public static long ExpectedValue(long price, int tolerancePercent, double chance) =>
            (long)Math.Round(EffectivePrice(price, tolerancePercent) * chance / 100d, MidpointRounding.AwayFromZero) * 100L;

        /// <summary>What the buyer pays: price × (1 − d × u) to the nearest 100 yen, u in [0, 1].</summary>
        public static long SaleAmount(long price, int tolerancePercent, double u) =>
            MarketPrice.RoundYen(price * (1d - tolerancePercent / 100d * u));
    }
}
```

`MarketPrice.RoundYen` の戻り値が long であること（`grep -n "RoundYen" Assets/Scripts/Core/MarketPrice.cs`）。

- [ ] **Step 3: テストを流す**

`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.EventOddsTests` → 全部通る。EditMode 全体の件数が減っていないこと。

- [ ] **Step 4: コミット**

```bash
git add Assets/Scripts/Core/EventOdds.cs* Assets/Tests/EditMode/EventOddsTests.cs*
git commit -m "Add the event sale odds"
```

---

### Task 3: 状態の型・出品できる条件・乱数の種（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/EventState.cs`
- Create: `Assets/Scripts/Core/EventRules.cs`
- Modify: `Assets/Scripts/Core/Colony.cs:89`（`Shop` プロパティの次に1行）
- Test: `Assets/Tests/EditMode/EventRulesTests.cs`

- [ ] **Step 1: テストを書く**

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EventRulesTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        private readonly EconomyTuning economy = new EconomyTuning();
        private readonly CareTuning care = new CareTuning();

        private Colony NewColony(out PetState pet)
        {
            var colony = Colony.CreateNew(Now, economy, care, new Random(1));
            pet = colony.Animals[0];
            return colony;
        }

        [Test]
        public void NewColony_HasNoEntry()
        {
            var colony = NewColony(out _);

            Assert.That(colony.Events, Is.Not.Null);
            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(colony.Events.LastSummary, Is.Null);
            Assert.That(colony.Events.LastSummarySeen, Is.True);
            Assert.That(colony.Events.NoticeNumber, Is.EqualTo(-1));
        }

        [Test]
        public void Check_AHealthyCagedAnimal_CanBeListed()
        {
            var colony = NewColony(out var pet);

            Assert.That(EventRules.Check(colony, pet), Is.EqualTo(ListingProblem.None));
        }

        [Test]
        public void Check_TrayGravidPairingWeakAndMissing_CannotBeListed()
        {
            var colony = NewColony(out var pet);
            var tray = colony.AddAnimal(new PetState { Name = "トレイ" }, null);
            Assert.That(EventRules.Check(colony, tray), Is.EqualTo(ListingProblem.InTray));

            pet.Gravid = new GravidState();
            Assert.That(EventRules.Check(colony, pet), Is.EqualTo(ListingProblem.Gravid));
            pet.Gravid = null;

            pet.Weak = true;
            Assert.That(EventRules.Check(colony, pet), Is.EqualTo(ListingProblem.Weak));
            pet.Weak = false;

            colony.Pairings.Add(new Pairing { Id = 1, MaleId = pet.Id, FemaleId = 99 });
            Assert.That(EventRules.Check(colony, pet), Is.EqualTo(ListingProblem.Pairing));
            colony.Pairings.Clear();

            colony.RemoveAnimal(pet);
            Assert.That(EventRules.Check(colony, pet), Is.EqualTo(ListingProblem.NotFound));
            Assert.That(EventRules.Check(colony, null), Is.EqualTo(ListingProblem.NotFound));
        }

        [Test]
        public void BaseValue_IsTheMarketPriceWithTheEventDemand()
        {
            var colony = NewColony(out var pet);
            pet.Weak = true;

            Assert.That(EventRules.BaseValue(pet, 42, EventKind.Big),
                Is.EqualTo(MarketPrice.For(pet, EventDemand.For(42, pet.Genotype, true), true)));
            Assert.That(EventRules.BaseValue(pet, 42, EventKind.Small),
                Is.EqualTo(MarketPrice.For(pet, EventDemand.For(42, pet.Genotype, false), true)));
        }

        [Test]
        public void MorphLabel_AddsTheSexMark()
        {
            var pet = new PetState { Genotype = Genotype.Normal(), Sex = Sex.Female, SexRevealed = true };
            var look = MorphNamer.VisualName(pet.Genotype);

            Assert.That(EventRules.MorphLabel(pet), Is.EqualTo(look + "♀"));
            pet.Sex = Sex.Male;
            Assert.That(EventRules.MorphLabel(pet), Is.EqualTo(look + "♂"));
            pet.SexRevealed = false;
            Assert.That(EventRules.MorphLabel(pet), Is.EqualTo(look + "?"));
        }

        [Test]
        public void DemandSeed_DependsOnTheColonyAndTheNumber()
        {
            Assert.That(EventRandom.DemandSeed(123, 0), Is.EqualTo(EventRandom.DemandSeed(123, 0)));
            Assert.That(EventRandom.DemandSeed(123, 0), Is.Not.EqualTo(EventRandom.DemandSeed(123, 1)));
            Assert.That(EventRandom.DemandSeed(123, 0), Is.Not.EqualTo(EventRandom.DemandSeed(456, 0)));
            Assert.That(EventRandom.DemandSeed(123, 0), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ListingSeed_IgnoresTheOrderButNotThePricesOrTolerances()
        {
            var a = new EventListing { AnimalId = 3, Price = 20000, TolerancePercent = 5 };
            var b = new EventListing { AnimalId = 8, Price = 9000, TolerancePercent = 0 };
            var seed = EventRandom.ListingSeed(1, 4, new List<EventListing> { a, b });

            Assert.That(EventRandom.ListingSeed(1, 4, new List<EventListing> { b, a }), Is.EqualTo(seed));
            Assert.That(EventRandom.ListingSeed(1, 5, new List<EventListing> { a, b }), Is.Not.EqualTo(seed));
            Assert.That(EventRandom.ListingSeed(1, 4, new List<EventListing> { a, new EventListing { AnimalId = 8, Price = 10000 } }), Is.Not.EqualTo(seed));
            Assert.That(EventRandom.ListingSeed(1, 4, new List<EventListing> { a, new EventListing { AnimalId = 8, Price = 9000, TolerancePercent = 10 } }), Is.Not.EqualTo(seed));
            Assert.That(EventRandom.ListingSeed(1, 4, new List<EventListing>()), Is.Not.EqualTo(seed));
        }

        [Test]
        public void ForActionAndForBooth_AreFixedByTheirArguments()
        {
            Assert.That(EventRandom.ForAction(77, 2, EventActionKind.Serve).NextDouble(),
                Is.EqualTo(EventRandom.ForAction(77, 2, EventActionKind.Serve).NextDouble()));
            Assert.That(EventRandom.ForAction(77, 2, EventActionKind.Serve).NextDouble(),
                Is.Not.EqualTo(EventRandom.ForAction(77, 2, EventActionKind.Browse).NextDouble()));
            Assert.That(EventRandom.ForAction(77, 2, EventActionKind.Serve).NextDouble(),
                Is.Not.EqualTo(EventRandom.ForAction(77, 3, EventActionKind.Serve).NextDouble()));
            Assert.That(EventRandom.ForBooth(77, 2).NextDouble(), Is.EqualTo(EventRandom.ForBooth(77, 2).NextDouble()));
        }

        [Test]
        public void IsListed_OnlyWhileUnsoldInTheEntry()
        {
            var state = new EventState();
            Assert.That(state.IsListed(5), Is.False);

            state.Entry = new EventEntry { Number = 1 };
            state.Entry.Listings.Add(new EventListing { AnimalId = 5, Price = 1000 });
            Assert.That(state.IsListed(5), Is.True);
            Assert.That(state.IsListed(6), Is.False);

            state.Entry.Listings[0].SoldInAction = 2;
            Assert.That(state.IsListed(5), Is.False);
        }

        [Test]
        public void Summary_TotalsAndNet()
        {
            var summary = new EventSummary { Number = 1, BoothFee = 3000 };
            summary.Sold.Add(new EventAnimalLine { Amount = 20000 });
            summary.Sold.Add(new EventAnimalLine { Amount = 8500 });
            summary.Bought.Add(new EventAnimalLine { Amount = 12000 });

            Assert.That(summary.TotalSales, Is.EqualTo(28500));
            Assert.That(summary.PurchaseTotal, Is.EqualTo(12000));
            Assert.That(summary.Net, Is.EqualTo(13500));
        }
    }
}
```

- [ ] **Step 2: 状態の型を書く**（`Assets/Scripts/Core/EventState.cs`）

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>One animal put up at the event (phase 6 spec §2.2). The frozen values are set at 0:00 on the day.</summary>
    public sealed class EventListing
    {
        public int AnimalId { get; set; }
        public long Price { get; set; }
        public int TolerancePercent { get; set; }
        public long FrozenBase { get; set; }
        public double FrozenChance { get; set; }

        /// <summary>The action it sold in, or -1 while unsold.</summary>
        public int SoldInAction { get; set; } = -1;

        public bool Sold => SoldInAction >= 0;
    }

    /// <summary>One animal in a log or the summary: sold, unsold or bought. Name and look are copied when it happens.</summary>
    public sealed class EventAnimalLine
    {
        public int AnimalId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MorphLabel { get; set; } = string.Empty;
        public long Amount { get; set; }
        public int MinuteOfDay { get; set; }
    }

    /// <summary>An animal another booth showed (spec §2.5). Animal is null once bought.</summary>
    public sealed class EventBoothOffer
    {
        public int OfferId { get; set; }
        public int ActionIndex { get; set; }
        public PetState Animal { get; set; }
        public string MorphLabel { get; set; } = string.Empty;
        public long Price { get; set; }
        public bool Rare { get; set; }
        public bool Bought { get; set; }
    }

    public sealed class EventActionRecord
    {
        public int Index { get; set; }
        public EventActionKind Kind { get; set; }
        public DateTimeOffset PlayedAtUtc { get; set; }

        /// <summary>When the visited booth shows up in the log (browse only).</summary>
        public int BoothMinuteOfDay { get; set; }

        public List<EventAnimalLine> Sales { get; set; } = new List<EventAnimalLine>();
    }

    /// <summary>The one event the player has entered and not finished yet.</summary>
    public sealed class EventEntry
    {
        public int Number { get; set; }
        public long FeePaid { get; set; }
        public bool Frozen { get; set; }

        /// <summary>Fixed at 0:00 from the event number and the listings (spec §2.7).</summary>
        public int ListingSeed { get; set; }

        public int NextOfferId { get; set; } = 1;
        public List<EventListing> Listings { get; set; } = new List<EventListing>();
        public List<EventActionRecord> Actions { get; set; } = new List<EventActionRecord>();
        public List<EventBoothOffer> BoothOffers { get; set; } = new List<EventBoothOffer>();
        public List<EventAnimalLine> Purchases { get; set; } = new List<EventAnimalLine>();

        public EventListing ListingOf(int animalId) => Listings.Find(l => l.AnimalId == animalId);
    }

    /// <summary>The last finished event's result (spec §2.6).</summary>
    public sealed class EventSummary
    {
        public int Number { get; set; }
        public long BoothFee { get; set; }
        public List<EventAnimalLine> Sold { get; set; } = new List<EventAnimalLine>();
        public List<EventAnimalLine> Unsold { get; set; } = new List<EventAnimalLine>();
        public List<EventAnimalLine> Bought { get; set; } = new List<EventAnimalLine>();

        public long TotalSales => Sum(Sold);

        public long PurchaseTotal => Sum(Bought);

        public long Net => TotalSales - PurchaseTotal - BoothFee;

        private static long Sum(List<EventAnimalLine> lines)
        {
            var total = 0L;
            foreach (var line in lines)
            {
                total += line.Amount;
            }

            return total;
        }
    }

    public sealed class EventState
    {
        public EventEntry Entry { get; set; }
        public EventSummary LastSummary { get; set; }

        /// <summary>False from the finish until the player closes the result screen.</summary>
        public bool LastSummarySeen { get; set; } = true;

        /// <summary>The event the deadline notices are counting for, and how far they got (0 none, 1 three days, 2 the day before).</summary>
        public int NoticeNumber { get; set; } = -1;

        public int NoticeStage { get; set; }

        public bool IsListed(int animalId)
        {
            var listing = Entry?.ListingOf(animalId);
            return listing != null && !listing.Sold;
        }
    }
}
```

- [ ] **Step 3: 出品の条件と乱数を書く**（`Assets/Scripts/Core/EventRules.cs`）

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TerrariumDays.Core
{
    /// <summary>Why an animal cannot be listed (spec ★A). Appended values only.</summary>
    public enum ListingProblem
    {
        None,
        NotFound,
        InTray,
        Gravid,
        Pairing,
        Weak
    }

    public static class EventRules
    {
        public static ListingProblem Check(Colony colony, PetState pet)
        {
            if (pet == null || colony.AnimalById(pet.Id) != pet)
            {
                return ListingProblem.NotFound;
            }

            if (colony.PairingOf(pet) != null)
            {
                return ListingProblem.Pairing;
            }

            if (colony.CageOf(pet) == null)
            {
                return ListingProblem.InTray;
            }

            if (pet.Gravid != null)
            {
                return ListingProblem.Gravid;
            }

            return pet.Weak ? ListingProblem.Weak : ListingProblem.None;
        }

        /// <summary>The §10.1 market price with this event's demand (the base value of the odds).</summary>
        public static long BaseValue(PetState pet, int demandSeed, EventKind kind) =>
            MarketPrice.For(pet, EventDemand.For(demandSeed, pet.Genotype, kind == EventKind.Big), pet.Weak);

        /// <summary>"レイプター♀" — the look plus the sex mark ("?" while unknown).</summary>
        public static string MorphLabel(PetState pet) =>
            MorphNamer.VisualName(pet.Genotype) + (!pet.SexKnown ? "?" : pet.Sex == Sex.Female ? "♀" : "♂");
    }

    /// <summary>
    /// Event seeds (spec §2.7). Built with <see cref="EventDemand.Unit"/> so they are the same on
    /// every platform; the actions and booths then use <see cref="BreedingRandom.For"/> streams 6 and 7.
    /// </summary>
    public static class EventRandom
    {
        public const int ActionStream = 6;
        public const int BoothStream = 7;

        public static int DemandSeed(int colonySeed, int number) =>
            ToSeed(EventDemand.Unit(colonySeed, "event-demand:" + number.ToString(CultureInfo.InvariantCulture)));

        public static int ListingSeed(int colonySeed, int number, IEnumerable<EventListing> listings)
        {
            var sorted = new List<EventListing>(listings);
            sorted.Sort((x, y) => x.AnimalId.CompareTo(y.AnimalId));
            var key = new StringBuilder("event-listing:").Append(number.ToString(CultureInfo.InvariantCulture)).Append('|');
            foreach (var listing in sorted)
            {
                key.Append(listing.AnimalId.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(listing.Price.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(listing.TolerancePercent.ToString(CultureInfo.InvariantCulture)).Append(';');
            }

            return ToSeed(EventDemand.Unit(colonySeed, key.ToString()));
        }

        public static Random ForAction(int listingSeed, int actionIndex, EventActionKind kind) =>
            BreedingRandom.For(listingSeed, ActionStream, actionIndex, (int)kind);

        public static Random ForBooth(int listingSeed, int actionIndex) =>
            BreedingRandom.For(listingSeed, BoothStream, actionIndex, 0);

        private static int ToSeed(double unit) => (int)(unit * int.MaxValue);
    }
}
```

- [ ] **Step 4: `Colony` に欄を足す**

`Assets/Scripts/Core/Colony.cs:89` の `public ShopStock Shop { get; set; } = new ShopStock();` の次の行に足す：

```csharp
        /// <summary>The event entry, the last result and the deadline notices (phase 6).</summary>
        public EventState Events { get; set; } = new EventState();
```

- [ ] **Step 5: テストを流す**

`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.EventRulesTests` → 全部通る。EditMode 全体の件数が減っていないこと（セーブはまだ `Events` を書かないが、既存のテストはそのまま通る）。

- [ ] **Step 6: コミット**

```bash
git add Assets/Scripts/Core/EventState.cs* Assets/Scripts/Core/EventRules.cs* Assets/Scripts/Core/Colony.cs Assets/Tests/EditMode/EventRulesTests.cs*
git commit -m "Add the event state, listing rules and seeds"
```

---

### Task 4: ブースの売り物（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/BoothStockGenerator.cs`
- Test: `Assets/Tests/EditMode/BoothStockGeneratorTests.cs`

先に `grep -n "enum GrowthStage" -A4 Assets/Scripts/Core/*.cs` で並びが `Baby, Juvenile, Adult`（0・1・2）であること、`grep -n "enum GeneId" -A12 Assets/Scripts/Core/*.cs` で `TremperAlbino・BellAlbino・RainwaterAlbino・Eclipse・Blizzard・MurphyPatternless・MackSnow・WhiteAndYellow` があることを確かめる（`ShopStockGenerator.cs` の表がこの名前を使っている）。

- [ ] **Step 1: テストを書く**

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class BoothStockGeneratorTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();

        [Test]
        public void ShopStock_IsNeverRare()
        {
            for (var seed = 0; seed < 200; seed++)
            {
                foreach (var offer in ShopStockGenerator.Generate(new Random(seed), Now, care))
                {
                    Assert.That(BoothStockGenerator.IsRare(offer.Animal), Is.False, $"seed {seed}: {MorphNamer.FullName(offer.Animal.Genotype, offer.Animal.Known)}");
                }
            }
        }

        [Test]
        public void RareAnimals_AreAlwaysRare()
        {
            for (var seed = 0; seed < 300; seed++)
            {
                var pet = BoothStockGenerator.RareAnimal(new Random(seed), Now, care);
                Assert.That(BoothStockGenerator.IsRare(pet), Is.True, $"seed {seed}: {MorphNamer.FullName(pet.Genotype, pet.Known)}");
            }
        }

        [Test]
        public void IsRare_Conditions()
        {
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2).Set(GeneId.MackSnow, 1))), Is.True);
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal().Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2))), Is.False);
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal().Set(GeneId.BellAlbino, 2).Set(GeneId.MackSnow, 1))), Is.True);
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal().Set(GeneId.BellAlbino, 2))), Is.False);
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal(85d, 30d).Set(GeneId.MackSnow, 1))), Is.True);
            Assert.That(BoothStockGenerator.IsRare(Pet(Genotype.Normal(85d, 95d))), Is.False);

            var hets = Pet(Genotype.Normal().Set(GeneId.Eclipse, 1).Set(GeneId.Blizzard, 1).Set(GeneId.BellAlbino, 1));
            hets.Known.SetHet(GeneId.Eclipse, 1d).SetHet(GeneId.Blizzard, 1d).SetHet(GeneId.BellAlbino, 1d);
            Assert.That(BoothStockGenerator.IsRare(hets), Is.True);
            hets.Known.SetHet(GeneId.BellAlbino, 0.66d);
            Assert.That(BoothStockGenerator.IsRare(hets), Is.False);
        }

        [Test]
        public void RareBoothRate_SmallIsAbout15Percent() => AssertRareBoothRate(EventKind.Small, 0.15d, 0.02d);

        [Test]
        public void RareBoothRate_BigIsAbout25Percent() => AssertRareBoothRate(EventKind.Big, 0.25d, 0.025d);

        private void AssertRareBoothRate(EventKind kind, double expected, double tolerance)
        {
            var rareBooths = 0;
            const int booths = 4000;
            for (var seed = 0; seed < booths; seed++)
            {
                var offers = BoothStockGenerator.Generate(new Random(seed), kind, 7, Now, care, 1, 0);
                var anyRare = false;
                for (var i = 0; i < offers.Count; i++)
                {
                    Assert.That(offers[i].Rare, Is.EqualTo(BoothStockGenerator.IsRare(offers[i].Animal)));
                    if (offers[i].Rare)
                    {
                        Assert.That(i, Is.EqualTo(0), "the rare animal comes first");
                        anyRare = true;
                    }
                }

                if (anyRare)
                {
                    rareBooths++;
                }
            }

            Assert.That(rareBooths / (double)booths, Is.EqualTo(expected).Within(tolerance));
        }

        [Test]
        public void Generate_ShowsOneToThreeAnimalsWithIdsAndPrices()
        {
            var counts = new int[4];
            for (var seed = 0; seed < 500; seed++)
            {
                var offers = BoothStockGenerator.Generate(new Random(seed), EventKind.Big, 7, Now, care, 10, 3);
                Assert.That(offers.Count, Is.InRange(1, 3));
                counts[offers.Count]++;
                for (var i = 0; i < offers.Count; i++)
                {
                    var offer = offers[i];
                    var pet = offer.Animal;
                    Assert.That(offer.OfferId, Is.EqualTo(10 + i));
                    Assert.That(offer.ActionIndex, Is.EqualTo(3));
                    Assert.That(offer.Bought, Is.False);
                    Assert.That(offer.MorphLabel, Is.EqualTo(EventRules.MorphLabel(pet)));
                    var market = MarketPrice.For(pet, EventDemand.For(7, pet.Genotype, true), false);
                    Assert.That(offer.Price, Is.InRange(market, MarketPrice.RoundYen(market * 1.15d)));
                    Assert.That(offer.Price % 100, Is.EqualTo(0));
                    Assert.That(pet.Weak, Is.False);
                    Assert.That(pet.PersonalityKnown, Is.False);
                    Assert.That(pet.Name, Is.Empty);
                    Assert.That(pet.SexRevealed, Is.EqualTo(pet.Stage != GrowthStage.Baby));
                }
            }

            Assert.That(counts[1], Is.GreaterThan(0));
            Assert.That(counts[2], Is.GreaterThan(0));
            Assert.That(counts[3], Is.GreaterThan(0));
        }

        [Test]
        public void Stages_AreMostlyJuvenileAndAdult()
        {
            var stages = new int[3];
            var total = 0;
            for (var seed = 0; seed < 3000; seed++)
            {
                foreach (var offer in BoothStockGenerator.Generate(new Random(seed), EventKind.Small, 7, Now, care, 1, 0))
                {
                    stages[(int)offer.Animal.Stage]++;
                    total++;
                    var weight = offer.Animal.WeightGrams;
                    switch (offer.Animal.Stage)
                    {
                        case GrowthStage.Baby:
                            Assert.That(weight, Is.InRange(4d, 12d));
                            break;
                        case GrowthStage.Juvenile:
                            Assert.That(weight, Is.InRange(16d, 30d));
                            break;
                        default:
                            Assert.That(weight, Is.InRange(45d, 70d));
                            break;
                    }
                }
            }

            Assert.That(stages[(int)GrowthStage.Baby] / (double)total, Is.EqualTo(0.20d).Within(0.03d));
            Assert.That(stages[(int)GrowthStage.Juvenile] / (double)total, Is.EqualTo(0.45d).Within(0.03d));
            Assert.That(stages[(int)GrowthStage.Adult] / (double)total, Is.EqualTo(0.35d).Within(0.03d));
        }

        [Test]
        public void Generate_IsFixedByTheRandom()
        {
            var a = BoothStockGenerator.Generate(new Random(9), EventKind.Small, 7, Now, care, 1, 0);
            var b = BoothStockGenerator.Generate(new Random(9), EventKind.Small, 7, Now, care, 1, 0);

            Assert.That(a.Count, Is.EqualTo(b.Count));
            for (var i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].Price, Is.EqualTo(b[i].Price));
                Assert.That(MorphNamer.FullName(a[i].Animal.Genotype, a[i].Animal.Known), Is.EqualTo(MorphNamer.FullName(b[i].Animal.Genotype, b[i].Animal.Known)));
            }
        }

        private static PetState Pet(Genotype genotype) => new PetState { Genotype = genotype, Known = new KnownGenetics() };
    }
}
```

- [ ] **Step 2: 実装を書く**

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// What another booth shows when the player browses (phase 6 spec §2.5): 1–3 animals, at most
    /// one of them rare (shown first), priced at market × event demand × 1.0–1.15. The ordinary
    /// animals are made like the shop's, but with more juveniles and adults (★P). No eggs.
    /// </summary>
    public static class BoothStockGenerator
    {
        public const int MinOffers = 1;
        public const int MaxOffers = 3;
        public const double BabyShare = 0.20d;
        public const double JuvenileShare = 0.45d;
        public const double HighPolygenic = 80d;
        public const double MinMarkup = 1.0d;
        public const double MaxMarkup = 1.15d;

        private static readonly GeneId[] Recessives =
        {
            GeneId.TremperAlbino, GeneId.BellAlbino, GeneId.RainwaterAlbino, GeneId.Eclipse, GeneId.Blizzard, GeneId.MurphyPatternless
        };

        /// <summary>
        /// Looks the shop never has (weights sum to 100). Each passes <see cref="IsRare(Genotype, KnownGenetics)"/>.
        /// A null result is the "three proven hets" look, built in <see cref="RareAnimal"/>.
        /// </summary>
        private static readonly List<(int Weight, Func<Random, Genotype> Build)> RareLooks = new List<(int, Func<Random, Genotype>)>
        {
            (12, r => Plain(r).Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2).Set(GeneId.MackSnow, 1)),
            (10, r => Plain(r).Set(GeneId.TremperAlbino, 2).Set(GeneId.Blizzard, 2).Set(GeneId.MackSnow, 1)),
            (8, r => Plain(r).Set(GeneId.WhiteAndYellow, 1).Set(GeneId.TremperAlbino, 2).Set(GeneId.Eclipse, 2)),
            (12, r => Plain(r).Set(GeneId.BellAlbino, 2).Set(GeneId.Eclipse, 2)),
            (12, r => Plain(r).Set(GeneId.RainwaterAlbino, 2).Set(GeneId.Eclipse, 2)),
            (10, r => Plain(r).Set(GeneId.BellAlbino, 2).Set(GeneId.MackSnow, 1)),
            (8, r => Plain(r).Set(GeneId.RainwaterAlbino, 2).Set(GeneId.Blizzard, 2)),
            (8, r => Plain(r).Set(GeneId.BellAlbino, 2).Set(GeneId.MurphyPatternless, 2)),
            (10, r => HighColour(r)),
            (10, r => null),
        };

        public static bool IsRare(PetState pet) => IsRare(pet.Genotype, pet.Known);

        /// <summary>
        /// Rare (★6): three or more visible genes; Bell or Rainwater with another visible gene; three
        /// or more proven hidden hets; or a visible gene with hypo or tangerine at 80 or more.
        /// </summary>
        public static bool IsRare(Genotype genotype, KnownGenetics known)
        {
            var visible = 0;
            var bellOrRainwater = false;
            var provenHets = 0;
            foreach (var gene in Genes.All)
            {
                if (genotype.Shows(gene))
                {
                    visible++;
                    bellOrRainwater |= gene == GeneId.BellAlbino || gene == GeneId.RainwaterAlbino;
                }
                else if (known != null && Genes.IsRecessive(gene) && KnownGenetics.IsProvenHet(known.HetProbability(gene)))
                {
                    provenHets++;
                }
            }

            return visible >= 3
                || (bellOrRainwater && visible >= 2)
                || provenHets >= 3
                || (visible >= 1 && (genotype.Hypo >= HighPolygenic || genotype.Tangerine >= HighPolygenic));
        }

        public static List<EventBoothOffer> Generate(Random random, EventKind kind, int demandSeed, DateTimeOffset nowUtc,
            CareTuning care, int firstOfferId, int actionIndex)
        {
            var count = random.Next(MinOffers, MaxOffers + 1);
            var hasRare = random.NextDouble() < EventSchedule.RareChance(kind);
            var offers = new List<EventBoothOffer>();
            for (var i = 0; i < count; i++)
            {
                var pet = i == 0 && hasRare ? RareAnimal(random, nowUtc, care) : CommonAnimal(random, nowUtc, care);
                var market = MarketPrice.For(pet, EventDemand.For(demandSeed, pet.Genotype, kind == EventKind.Big));
                offers.Add(new EventBoothOffer
                {
                    OfferId = firstOfferId + i,
                    ActionIndex = actionIndex,
                    Animal = pet,
                    MorphLabel = EventRules.MorphLabel(pet),
                    Price = MarketPrice.RoundYen(market * BreedingRandom.Uniform(random, MinMarkup, MaxMarkup)),
                    Rare = IsRare(pet),
                });
            }

            return offers;
        }

        /// <summary>A shop-style animal with the booth's stage mix: baby 20%, juvenile 45%, adult 35% (★7).</summary>
        public static PetState CommonAnimal(Random random, DateTimeOffset nowUtc, CareTuning care)
        {
            var pet = ShopStockGenerator.Animal(random, nowUtc, care);
            var roll = random.NextDouble();
            GrowthStage stage;
            double weight;
            double ageMonths;
            if (roll < BabyShare)
            {
                stage = GrowthStage.Baby;
                weight = 4d + random.NextDouble() * 8d;
                ageMonths = 1d + random.NextDouble() * 2d;
            }
            else if (roll < BabyShare + JuvenileShare)
            {
                stage = GrowthStage.Juvenile;
                weight = 16d + random.NextDouble() * 14d;
                ageMonths = 4d + random.NextDouble() * 3d;
            }
            else
            {
                stage = GrowthStage.Adult;
                weight = 45d + random.NextDouble() * 25d;
                ageMonths = 10d + random.NextDouble() * 4d;
            }

            pet.Stage = stage;
            pet.WeightGrams = weight;
            pet.SexRevealed = stage != GrowthStage.Baby;
            // One real day is one game month, so an age in months is that many real days back.
            pet.HatchedAtUtc = nowUtc.AddDays(-ageMonths);
            pet.NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(stage, care);
            return pet;
        }

        public static PetState RareAnimal(Random random, DateTimeOffset nowUtc, CareTuning care)
        {
            var pet = CommonAnimal(random, nowUtc, care);
            var genotype = PickRareLook(random);
            var known = new KnownGenetics();
            if (genotype == null)
            {
                genotype = Plain(random);
                ProveThreeHets(genotype, known, random);
            }

            pet.Genotype = genotype;
            pet.Known = known;
            return pet;
        }

        private static Genotype PickRareLook(Random random)
        {
            var total = 0;
            foreach (var look in RareLooks)
            {
                total += look.Weight;
            }

            var roll = random.Next(total);
            foreach (var look in RareLooks)
            {
                if (roll < look.Weight)
                {
                    return look.Build(random);
                }

                roll -= look.Weight;
            }

            return RareLooks[0].Build(random);
        }

        /// <summary>Three different hidden recessive genes, each carried once and disclosed as proven.</summary>
        private static void ProveThreeHets(Genotype genotype, KnownGenetics known, Random random)
        {
            var genes = new List<GeneId>(Recessives);
            for (var i = 0; i < 3; i++)
            {
                var pick = random.Next(i, genes.Count);
                (genes[i], genes[pick]) = (genes[pick], genes[i]);
                genotype.Set(genes[i], 1);
                known.SetHet(genes[i], 1d);
            }
        }

        /// <summary>One visible gene with very high hypo and tangerine (both 80–100).</summary>
        private static Genotype HighColour(Random random)
        {
            var genotype = Genotype.Normal(HighPolygenic + random.NextDouble() * 20d, HighPolygenic + random.NextDouble() * 20d);
            switch (random.Next(3))
            {
                case 0:
                    return genotype.Set(GeneId.TremperAlbino, 2);
                case 1:
                    return genotype.Set(GeneId.MackSnow, 1);
                default:
                    return genotype.Set(GeneId.Eclipse, 2);
            }
        }

        private static Genotype Plain(Random random) => Genotype.Normal(Low(random), Low(random));

        private static double Low(Random random) =>
            StarterGenetics.MinPolygenic + random.NextDouble() * (StarterGenetics.MaxPolygenic - StarterGenetics.MinPolygenic);
    }
}
```

- [ ] **Step 3: テストを流す**

`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.BoothStockGeneratorTests` → 全部通る。割合のテスト（`RareBoothRate_SmallIsAbout15Percent`・`RareBoothRate_BigIsAbout25Percent`・`Stages_AreMostlyJuvenileAndAdult`）が外れたら、種の数や許容幅を変えずに止めて報告する（重みは計画者が直す）。`ShopStock_IsNeverRare` が外れたら、店の表（`ShopStockGenerator.Templates`）が段階6の計画の後に変わった印なので止めて報告する。EditMode 全体の件数が減っていないこと。

- [ ] **Step 4: コミット**

```bash
git add Assets/Scripts/Core/BoothStockGenerator.cs* Assets/Tests/EditMode/BoothStockGeneratorTests.cs*
git commit -m "Add the event booth stock"
```

---

### Task 5: 申し込み・直し・開催日0:00の固定・通知（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/EventService.cs`
- Test: `Assets/Tests/EditMode/EventTestRoom.cs`（Task 5・6 のテストが共有する部屋の作り方）
- Test: `Assets/Tests/EditMode/EventServiceTests.cs`

**Produces（Task 6・9・12・14 が使う）:** `EventService(CareTuning)`、`OpenNumber`・`BaseValueOf`・`ChanceOf`・`Register`・`UpdateListings`・`PendingNumber`・`Advance`、`EventReport`、`ListingRequest`、`EventRegisterResult`。`EventService` は `partial`（Task 6 が `EventService.Day.cs` で足す）。

- [ ] **Step 1: テスト用の部屋を書く**（`Assets/Tests/EditMode/EventTestRoom.cs`）

```csharp
using System;
using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    /// <summary>Rooms for the event tests. Day 0 (2026-04-01) is Epoch, so event 0 (big, spring) starts 14 game days later.</summary>
    internal static class EventTestRoom
    {
        public static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

        /// <summary>The starter baby plus (animals − 1) shop animals, each in its own cage, three racks and plenty of money.</summary>
        public static Colony Create(int animals, out GameCalendar calendar)
        {
            var care = new CareTuning();
            var colony = Colony.CreateNew(Epoch, new EconomyTuning(), care, new Random(1));
            colony.RackCount = 3;
            colony.Wallet.Money = 1000000;
            colony.Animals[0].Name = "テスト0";
            for (var i = 1; i < animals; i++)
            {
                var pet = ShopStockGenerator.Animal(new Random(100 + i), Epoch, care);
                pet.Name = "テスト" + i;
                colony.AddAnimal(pet, colony.AddCage(CageSize.Standard));
            }

            calendar = new GameCalendar(colony.CalendarEpochUtc);
            return colony;
        }

        public static List<ListingRequest> AtBaseValue(EventService service, Colony colony, int number)
        {
            var requests = new List<ListingRequest>();
            foreach (var pet in colony.Animals)
            {
                requests.Add(new ListingRequest { AnimalId = pet.Id, Price = service.BaseValueOf(colony, pet, number) });
            }

            return requests;
        }

        public static List<ListingRequest> AtPrice(Colony colony, long price)
        {
            var requests = new List<ListingRequest>();
            foreach (var pet in colony.Animals)
            {
                requests.Add(new ListingRequest { AnimalId = pet.Id, Price = price });
            }

            return requests;
        }

        public static List<ListingRequest> One(int animalId, long price, int tolerancePercent = 0) =>
            new List<ListingRequest> { new ListingRequest { AnimalId = animalId, Price = price, TolerancePercent = tolerancePercent } };
    }
}
```

- [ ] **Step 2: テストを書く**（`Assets/Tests/EditMode/EventServiceTests.cs`）

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EventServiceTests
    {
        private const string Spring = "ジャパン・レプタイルズ・フェスタ（春）";
        private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);
        private static readonly DateTimeOffset Epoch = EventTestRoom.Epoch;
        private readonly EventService service = new EventService(new CareTuning());

        private static List<ListingRequest> None() => new List<ListingRequest>();

        [Test]
        public void OpenNumber_IsTheNextEventUntilEntered()
        {
            var colony = EventTestRoom.Create(2, out var calendar);

            Assert.That(service.OpenNumber(colony, calendar, Epoch), Is.EqualTo(0));
            Assert.That(service.Register(colony, calendar, 0, None(), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            Assert.That(service.OpenNumber(colony, calendar, Epoch), Is.EqualTo(-1));
        }

        [Test]
        public void Register_PaysTheFeeAndKeepsTheListingsInIdOrder()
        {
            var colony = EventTestRoom.Create(3, out var calendar);
            var money = colony.Wallet.Money;
            var requests = new List<ListingRequest>
            {
                new ListingRequest { AnimalId = colony.Animals[2].Id, Price = 20000, TolerancePercent = 5 },
                new ListingRequest { AnimalId = colony.Animals[0].Id, Price = 9000 },
            };

            Assert.That(service.Register(colony, calendar, 0, requests, Epoch), Is.EqualTo(EventRegisterResult.Ok));

            Assert.That(colony.Wallet.Money, Is.EqualTo(money - 10000));
            var fee = colony.Wallet.Ledger[colony.Wallet.Ledger.Count - 1];
            Assert.That(fee.Category, Is.EqualTo(LedgerCategory.BoothFee));
            Assert.That(fee.Amount, Is.EqualTo(-10000));
            Assert.That(fee.Note, Is.EqualTo(Spring + "の出店料"));
            Assert.That(fee.AtUtc, Is.EqualTo(Epoch));

            var entry = colony.Events.Entry;
            Assert.That(entry.Number, Is.EqualTo(0));
            Assert.That(entry.FeePaid, Is.EqualTo(10000));
            Assert.That(entry.Frozen, Is.False);
            Assert.That(entry.Listings.Count, Is.EqualTo(2));
            Assert.That(entry.Listings[0].AnimalId, Is.EqualTo(colony.Animals[0].Id));
            Assert.That(entry.Listings[1].Price, Is.EqualTo(20000));
            Assert.That(entry.Listings[1].TolerancePercent, Is.EqualTo(5));
            Assert.That(colony.Events.IsListed(colony.Animals[2].Id), Is.True);
            Assert.That(colony.Events.IsListed(colony.Animals[1].Id), Is.False);
        }

        [Test]
        public void Register_WithNoListings_IsAllowed()
        {
            var colony = EventTestRoom.Create(1, out var calendar);

            Assert.That(service.Register(colony, calendar, 0, None(), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            Assert.That(colony.Events.Entry.Listings, Is.Empty);
        }

        [Test]
        public void Register_RejectsTheWholeRequest()
        {
            var colony = EventTestRoom.Create(11, out var calendar);
            var id = colony.Animals[1].Id;
            var ledger = colony.Wallet.Ledger.Count;

            Assert.That(service.Register(colony, calendar, 1, EventTestRoom.One(id, 1000), Epoch), Is.EqualTo(EventRegisterResult.NotOpen));
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.AtPrice(colony, 1000), Epoch), Is.EqualTo(EventRegisterResult.TooMany));

            var twice = EventTestRoom.One(id, 1000);
            twice.Add(new ListingRequest { AnimalId = id, Price = 2000 });
            Assert.That(service.Register(colony, calendar, 0, twice, Epoch), Is.EqualTo(EventRegisterResult.Duplicate));

            colony.Animals[1].Gravid = new GravidState();
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(id, 1000), Epoch), Is.EqualTo(EventRegisterResult.NotEligible));
            colony.Animals[1].Gravid = null;
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(999, 1000), Epoch), Is.EqualTo(EventRegisterResult.NotEligible));

            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(id, 50), Epoch), Is.EqualTo(EventRegisterResult.InvalidPrice));
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(id, 1000, 3), Epoch), Is.EqualTo(EventRegisterResult.InvalidTolerance));

            colony.Wallet.Money = 9999;
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(id, 1000), Epoch), Is.EqualTo(EventRegisterResult.NotEnoughMoney));
            Assert.That(colony.Wallet.Money, Is.EqualTo(9999));

            Assert.That(colony.Wallet.Ledger.Count, Is.EqualTo(ledger));
            Assert.That(colony.Events.Entry, Is.Null);
        }

        [Test]
        public void Register_Twice_IsAlreadyRegisteredOrPending()
        {
            var colony = EventTestRoom.Create(1, out var calendar);
            Assert.That(service.Register(colony, calendar, 0, None(), Epoch), Is.EqualTo(EventRegisterResult.Ok));

            Assert.That(service.Register(colony, calendar, 0, None(), Epoch), Is.EqualTo(EventRegisterResult.AlreadyRegistered));
            var afterTheDay = EventSchedule.StartUtc(calendar, 0) + Second;
            Assert.That(EventSchedule.NextOpenNumber(calendar, afterTheDay), Is.EqualTo(1));
            Assert.That(service.Register(colony, calendar, 1, None(), afterTheDay), Is.EqualTo(EventRegisterResult.Pending));
        }

        [Test]
        public void UpdateListings_BeforeTheDay_ReplacesThemWithoutARefund()
        {
            var colony = EventTestRoom.Create(3, out var calendar);
            Assert.That(service.UpdateListings(colony, calendar, None(), Epoch), Is.EqualTo(EventRegisterResult.NotOpen));
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(colony.Animals[0].Id, 1000), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            var money = colony.Wallet.Money;

            Assert.That(service.UpdateListings(colony, calendar, EventTestRoom.AtPrice(colony, 3000), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            Assert.That(colony.Events.Entry.Listings.Count, Is.EqualTo(3));
            Assert.That(service.UpdateListings(colony, calendar, EventTestRoom.One(colony.Animals[0].Id, 50), Epoch), Is.EqualTo(EventRegisterResult.InvalidPrice));
            Assert.That(colony.Events.Entry.Listings.Count, Is.EqualTo(3));

            Assert.That(service.UpdateListings(colony, calendar, None(), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            Assert.That(colony.Events.Entry.Listings, Is.Empty);
            Assert.That(colony.Wallet.Money, Is.EqualTo(money));

            Assert.That(service.UpdateListings(colony, calendar, None(), EventSchedule.StartUtc(calendar, 0)), Is.EqualTo(EventRegisterResult.Frozen));
        }

        [Test]
        public void Advance_AtMidnight_FreezesTheValuesOnce()
        {
            var colony = EventTestRoom.Create(3, out var calendar);
            var requests = EventTestRoom.AtPrice(colony, 5000);
            requests[2].TolerancePercent = 10;
            Assert.That(service.Register(colony, calendar, 0, requests, Epoch), Is.EqualTo(EventRegisterResult.Ok));
            colony.Animals[1].Gravid = new GravidState();
            var start = EventSchedule.StartUtc(calendar, 0);

            var before = service.Advance(colony, calendar, start - Second);
            Assert.That(colony.Events.Entry.Frozen, Is.False);
            Assert.That(before.HasEvents, Is.False);

            var report = service.Advance(colony, calendar, start);
            var entry = colony.Events.Entry;
            Assert.That(report.Withdrawn, Is.EqualTo(new[] { "テスト1" }));
            Assert.That(report.DayStartedNumber, Is.EqualTo(0));
            Assert.That(report.HasEvents, Is.True);
            Assert.That(entry.Frozen, Is.True);
            Assert.That(entry.Listings.Count, Is.EqualTo(2));
            foreach (var listing in entry.Listings)
            {
                var pet = colony.AnimalById(listing.AnimalId);
                Assert.That(listing.FrozenBase, Is.EqualTo(service.BaseValueOf(colony, pet, 0)));
                Assert.That(listing.FrozenChance, Is.EqualTo(EventOdds.SaleChance(listing.Price, listing.TolerancePercent, listing.FrozenBase, EventKind.Big)));
                Assert.That(listing.FrozenChance, Is.EqualTo(service.ChanceOf(colony, pet, 0, listing.Price, listing.TolerancePercent)));
            }

            Assert.That(entry.ListingSeed, Is.EqualTo(EventRandom.ListingSeed(colony.BreedingSeed, 0, entry.Listings)));

            var again = service.Advance(colony, calendar, start + TimeSpan.FromMinutes(1));
            Assert.That(again.DayStartedNumber, Is.EqualTo(-1));
            Assert.That(again.Withdrawn, Is.Empty);
        }

        [Test]
        public void Advance_OpenedAfterTheDay_FreezesWithoutTheDayNotice()
        {
            var colony = EventTestRoom.Create(2, out var calendar);
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.AtPrice(colony, 5000), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            var end = EventSchedule.EndUtc(calendar, 0);

            Assert.That(service.PendingNumber(colony, calendar, end - Second), Is.EqualTo(-1));
            Assert.That(service.PendingNumber(colony, calendar, end), Is.EqualTo(0));

            var report = service.Advance(colony, calendar, end + Second);
            Assert.That(colony.Events.Entry.Frozen, Is.True);
            Assert.That(report.DayStartedNumber, Is.EqualTo(-1));
        }

        [Test]
        public void Advance_DropsListingsWhoseAnimalIsGone()
        {
            var colony = EventTestRoom.Create(2, out var calendar);
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.AtPrice(colony, 5000), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            var gone = colony.Animals[1];
            colony.RemoveAnimal(gone);

            var report = service.Advance(colony, calendar, Epoch + TimeSpan.FromMinutes(1));

            Assert.That(report.Withdrawn, Is.EqualTo(new[] { "テスト1" }));
            Assert.That(colony.Events.Entry.Listings.Count, Is.EqualTo(1));
            Assert.That(colony.Events.IsListed(gone.Id), Is.False);
        }

        [Test]
        public void Advance_DeadlineNotices_ThreeDaysAndTheDayBefore()
        {
            var colony = EventTestRoom.Create(1, out var calendar);
            var start = EventSchedule.StartUtc(calendar, 0);

            Assert.That(service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(3.1d)).DeadlineNoticeNumber, Is.EqualTo(-1));

            var three = service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(2.9d));
            Assert.That(three.DeadlineNoticeNumber, Is.EqualTo(0));
            Assert.That(three.DeadlineDaysLeft, Is.EqualTo(3));
            Assert.That(service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(2d)).DeadlineNoticeNumber, Is.EqualTo(-1));

            var last = service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(0.9d));
            Assert.That(last.DeadlineNoticeNumber, Is.EqualTo(0));
            Assert.That(last.DeadlineDaysLeft, Is.EqualTo(1));
            Assert.That(service.Advance(colony, calendar, start - Second).DeadlineNoticeNumber, Is.EqualTo(-1));

            var next = service.Advance(colony, calendar, start + GameCalendar.RealTimeFor(27.1d));
            Assert.That(next.DeadlineNoticeNumber, Is.EqualTo(1));
            Assert.That(next.DeadlineDaysLeft, Is.EqualTo(3));
        }

        [Test]
        public void Advance_JumpingToTheDayBefore_ShowsOnlyTheNewerNotice()
        {
            var colony = EventTestRoom.Create(1, out var calendar);
            var start = EventSchedule.StartUtc(calendar, 0);

            var report = service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(0.5d));
            Assert.That(report.DeadlineNoticeNumber, Is.EqualTo(0));
            Assert.That(report.DeadlineDaysLeft, Is.EqualTo(1));
            Assert.That(service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(0.4d)).DeadlineNoticeNumber, Is.EqualTo(-1));
        }

        [Test]
        public void Advance_NoDeadlineNoticesWhileEntered()
        {
            var colony = EventTestRoom.Create(1, out var calendar);
            Assert.That(service.Register(colony, calendar, 0, None(), Epoch), Is.EqualTo(EventRegisterResult.Ok));

            var report = service.Advance(colony, calendar, EventSchedule.StartUtc(calendar, 0) - GameCalendar.RealTimeFor(0.9d));

            Assert.That(report.DeadlineNoticeNumber, Is.EqualTo(-1));
        }
    }
}
```

- [ ] **Step 3: 実装を書く**（`Assets/Scripts/Core/EventService.cs`）

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Why a registration or a listing change was refused. Any problem refuses the whole request (planner).</summary>
    public enum EventRegisterResult
    {
        Ok,
        NotOpen,
        Pending,
        AlreadyRegistered,
        TooMany,
        NotEligible,
        Duplicate,
        InvalidPrice,
        InvalidTolerance,
        NotEnoughMoney,
        Frozen
    }

    public sealed class ListingRequest
    {
        public int AnimalId { get; set; }
        public long Price { get; set; }
        public int TolerancePercent { get; set; }
    }

    /// <summary>What one <see cref="EventService.Advance"/> found for the notices (★9). -1 = nothing.</summary>
    public sealed class EventReport
    {
        /// <summary>Names of the animals whose listing was taken back (missing, or not eligible at 0:00).</summary>
        public List<string> Withdrawn { get; } = new List<string>();

        public int DeadlineNoticeNumber { get; set; } = -1;

        /// <summary>3 for the three-days notice, 1 for the day-before notice.</summary>
        public int DeadlineDaysLeft { get; set; }

        /// <summary>The event whose day just began while the app was open.</summary>
        public int DayStartedNumber { get; set; } = -1;

        /// <summary>Set by the session on load: an entered event whose day is over but not played to the end.</summary>
        public int PendingNumber { get; set; } = -1;

        public bool HasEvents => Withdrawn.Count > 0 || DeadlineNoticeNumber >= 0 || DayStartedNumber >= 0 || PendingNumber >= 0;
    }

    /// <summary>
    /// Entering events (phase 6 spec §2.1–2.2): register with listings and pay the fee, change the
    /// listings until the day before, freeze everything at 0:00 on the day, and the deadline notices.
    /// The day itself is in EventService.Day.cs. Pure: the caller passes the game time.
    /// </summary>
    public sealed partial class EventService
    {
        public const long MinPrice = 100L;

        private readonly CareTuning care;

        public EventService(CareTuning care)
        {
            this.care = care;
        }

        public CareTuning Care => care;

        /// <summary>The event that takes entries now, or -1 while an entered event is not finished (★M・★Q).</summary>
        public int OpenNumber(Colony colony, GameCalendar calendar, DateTimeOffset nowUtc) =>
            colony.Events.Entry != null ? -1 : EventSchedule.NextOpenNumber(calendar, nowUtc);

        public long BaseValueOf(Colony colony, PetState pet, int number) =>
            EventRules.BaseValue(pet, EventRandom.DemandSeed(colony.BreedingSeed, number), EventSchedule.KindOf(number));

        /// <summary>The whole-event sale chance P for this price and tolerance (the preparation screen recomputes it until 0:00, ★N).</summary>
        public double ChanceOf(Colony colony, PetState pet, int number, long price, int tolerancePercent) =>
            EventOdds.SaleChance(price, tolerancePercent, BaseValueOf(colony, pet, number), EventSchedule.KindOf(number));

        public EventRegisterResult Register(Colony colony, GameCalendar calendar, int number, IList<ListingRequest> requests, DateTimeOffset nowUtc)
        {
            requests = requests ?? new List<ListingRequest>();
            var entry = colony.Events.Entry;
            if (entry != null)
            {
                return entry.Number == number ? EventRegisterResult.AlreadyRegistered : EventRegisterResult.Pending;
            }

            if (number != EventSchedule.NextOpenNumber(calendar, nowUtc))
            {
                return EventRegisterResult.NotOpen;
            }

            var problem = Validate(colony, number, requests);
            if (problem != EventRegisterResult.Ok)
            {
                return problem;
            }

            var fee = EventSchedule.BoothFee(EventSchedule.KindOf(number));
            if (!colony.Wallet.TrySpend(fee, LedgerCategory.BoothFee, $"{EventSchedule.NameOf(number)}の出店料", nowUtc))
            {
                return EventRegisterResult.NotEnoughMoney;
            }

            entry = new EventEntry { Number = number, FeePaid = fee };
            SetListings(entry, requests);
            colony.Events.Entry = entry;
            return EventRegisterResult.Ok;
        }

        /// <summary>Replaces the listings until the day before (★B). The fee is never refunded; 0 listings is allowed (★2).</summary>
        public EventRegisterResult UpdateListings(Colony colony, GameCalendar calendar, IList<ListingRequest> requests, DateTimeOffset nowUtc)
        {
            requests = requests ?? new List<ListingRequest>();
            var entry = colony.Events.Entry;
            if (entry == null)
            {
                return EventRegisterResult.NotOpen;
            }

            if (entry.Frozen || EventSchedule.HasStarted(calendar, entry.Number, nowUtc))
            {
                return EventRegisterResult.Frozen;
            }

            var problem = Validate(colony, entry.Number, requests);
            if (problem != EventRegisterResult.Ok)
            {
                return problem;
            }

            SetListings(entry, requests);
            return EventRegisterResult.Ok;
        }

        /// <summary>The entered event if its day is over and it is still not played to the end, else -1.</summary>
        public int PendingNumber(Colony colony, GameCalendar calendar, DateTimeOffset nowUtc)
        {
            var entry = colony.Events.Entry;
            return entry != null && nowUtc >= EventSchedule.EndUtc(calendar, entry.Number) ? entry.Number : -1;
        }

        /// <summary>Called by the session after every catch-up: drops missing animals, freezes at 0:00, counts the deadline notices.</summary>
        public EventReport Advance(Colony colony, GameCalendar calendar, DateTimeOffset nowUtc)
        {
            var report = new EventReport();
            var entry = colony.Events.Entry;
            if (entry == null)
            {
                UpdateDeadlineNotice(colony.Events, calendar, nowUtc, report);
                return report;
            }

            DropMissing(colony, entry, report);
            if (!entry.Frozen && EventSchedule.HasStarted(calendar, entry.Number, nowUtc))
            {
                Freeze(colony, entry, report);
                if (nowUtc < EventSchedule.EndUtc(calendar, entry.Number))
                {
                    report.DayStartedNumber = entry.Number;
                }
            }

            return report;
        }

        private static EventRegisterResult Validate(Colony colony, int number, IList<ListingRequest> requests)
        {
            if (requests.Count > EventSchedule.MaxListings(EventSchedule.KindOf(number)))
            {
                return EventRegisterResult.TooMany;
            }

            var seen = new HashSet<int>();
            foreach (var request in requests)
            {
                if (!seen.Add(request.AnimalId))
                {
                    return EventRegisterResult.Duplicate;
                }

                if (EventRules.Check(colony, colony.AnimalById(request.AnimalId)) != ListingProblem.None)
                {
                    return EventRegisterResult.NotEligible;
                }

                if (request.Price < MinPrice)
                {
                    return EventRegisterResult.InvalidPrice;
                }

                if (!EventOdds.IsValidTolerance(request.TolerancePercent))
                {
                    return EventRegisterResult.InvalidTolerance;
                }
            }

            return EventRegisterResult.Ok;
        }

        private static void SetListings(EventEntry entry, IList<ListingRequest> requests)
        {
            entry.Listings = new List<EventListing>();
            foreach (var request in requests)
            {
                entry.Listings.Add(new EventListing { AnimalId = request.AnimalId, Price = request.Price, TolerancePercent = request.TolerancePercent });
            }

            entry.Listings.Sort((a, b) => a.AnimalId.CompareTo(b.AnimalId));
        }

        /// <summary>Unsold listings whose animal is no longer in the room are taken back (planner: checked on every Advance).</summary>
        private static void DropMissing(Colony colony, EventEntry entry, EventReport report)
        {
            for (var i = 0; i < entry.Listings.Count; i++)
            {
                var listing = entry.Listings[i];
                if (listing.Sold || colony.AnimalById(listing.AnimalId) != null)
                {
                    continue;
                }

                report.Withdrawn.Add(NameOf(colony, listing.AnimalId));
                entry.Listings.RemoveAt(i);
                i--;
            }
        }

        /// <summary>0:00 on the day (★N): ineligible animals are taken back, then the base values, chances and seed are fixed.</summary>
        private void Freeze(Colony colony, EventEntry entry, EventReport report)
        {
            var kind = EventSchedule.KindOf(entry.Number);
            var kept = new List<EventListing>();
            foreach (var listing in entry.Listings)
            {
                var pet = colony.AnimalById(listing.AnimalId);
                if (EventRules.Check(colony, pet) != ListingProblem.None)
                {
                    report.Withdrawn.Add(NameOf(colony, listing.AnimalId));
                    continue;
                }

                listing.FrozenBase = BaseValueOf(colony, pet, entry.Number);
                listing.FrozenChance = EventOdds.SaleChance(listing.Price, listing.TolerancePercent, listing.FrozenBase, kind);
                kept.Add(listing);
            }

            kept.Sort((a, b) => a.AnimalId.CompareTo(b.AnimalId));
            entry.Listings = kept;
            entry.ListingSeed = EventRandom.ListingSeed(colony.BreedingSeed, entry.Number, kept);
            entry.Frozen = true;
        }

        /// <summary>Stage 1 at three days left, stage 2 on the day before; only a higher stage makes a notice (★L・★9).</summary>
        private static void UpdateDeadlineNotice(EventState state, GameCalendar calendar, DateTimeOffset nowUtc, EventReport report)
        {
            var number = EventSchedule.NextOpenNumber(calendar, nowUtc);
            if (state.NoticeNumber != number)
            {
                state.NoticeNumber = number;
                state.NoticeStage = 0;
            }

            var daysLeft = EventSchedule.GameDaysUntilStart(calendar, number, nowUtc);
            var stage = daysLeft <= 1d ? 2 : daysLeft <= 3d ? 1 : 0;
            if (stage <= state.NoticeStage)
            {
                return;
            }

            state.NoticeStage = stage;
            report.DeadlineNoticeNumber = number;
            report.DeadlineDaysLeft = stage == 2 ? 1 : 3;
        }

        private static string NameOf(Colony colony, int animalId)
        {
            var pet = colony.AnimalById(animalId);
            if (pet != null)
            {
                return pet.Name;
            }

            var record = colony.Lineage.Find(r => r.Id == animalId);
            return record != null ? record.Name : $"#{animalId}";
        }
    }
}
```

- [ ] **Step 4: テストを流す**

`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.EventServiceTests` → 全部通る。EditMode 全体の件数が減っていないこと。

- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/EventService.cs* Assets/Tests/EditMode/EventTestRoom.cs* Assets/Tests/EditMode/EventServiceTests.cs*
git commit -m "Add event registration, freezing and deadline notices"
```

---

### Task 6: 当日の行動・残りを全部接客・買い物・集計（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/EventService.Day.cs`
- Test: `Assets/Tests/EditMode/EventDayTests.cs`

**Produces（Task 9・13・15 が使う）:** `EventActionResult`・`EventBuyResult`、`EventService.ActionsLeft`・`PlayAction(colony, calendar, kind, playedAtUtc, out EventActionRecord)`・`ServeAllRemaining(colony, calendar, playedAtUtc, out List<EventActionRecord>)`・`CheckBuy`・`Buy`・`Finish`・`DismissSummary`。

- [ ] **Step 1: テストを書く**

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EventDayTests
    {
        private const string Spring = "ジャパン・レプタイルズ・フェスタ（春）";
        private static readonly DateTimeOffset Epoch = EventTestRoom.Epoch;
        private readonly CareTuning care = new CareTuning();
        private EventService service;

        [SetUp]
        public void SetUp() => service = new EventService(care);

        private static DateTimeOffset Day(GameCalendar calendar, double gameDaysLate = 0d) =>
            EventSchedule.StartUtc(calendar, 0) + GameCalendar.RealTimeFor(gameDaysLate) + TimeSpan.FromMinutes(1);

        /// <summary>Registers event 0 (big, 8 actions) on the epoch and freezes it when the player opens the app on the day.</summary>
        private Colony Entered(int animals, Func<Colony, List<ListingRequest>> requests, out GameCalendar calendar, double gameDaysLate = 0d)
        {
            var colony = EventTestRoom.Create(animals, out calendar);
            Assert.That(service.Register(colony, calendar, 0, requests(colony), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            service.Advance(colony, calendar, Day(calendar, gameDaysLate));
            return colony;
        }

        private List<ListingRequest> AtBase(Colony colony) => EventTestRoom.AtBaseValue(service, colony, 0);

        private static List<ListingRequest> Cheap(Colony colony) => EventTestRoom.AtPrice(colony, 1000);

        private static List<ListingRequest> Nothing(Colony colony) => new List<ListingRequest>();

        private string Transcript(EventActionKind[] kinds, double gameDaysLate)
        {
            var colony = Entered(5, AtBase, out var calendar, gameDaysLate);
            var text = new StringBuilder();
            for (var i = 0; i < kinds.Length; i++)
            {
                Assert.That(service.PlayAction(colony, calendar, kinds[i], Day(calendar, gameDaysLate) + TimeSpan.FromMinutes(i), out var record),
                    Is.EqualTo(EventActionResult.Ok));
                text.Append(record.Kind).Append('|').Append(record.BoothMinuteOfDay).Append('|');
                foreach (var sale in record.Sales)
                {
                    text.Append(sale.AnimalId).Append(':').Append(sale.Amount).Append(':').Append(sale.MinuteOfDay).Append(';');
                }

                text.Append('\n');
            }

            foreach (var offer in colony.Events.Entry.BoothOffers)
            {
                text.Append(offer.OfferId).Append(':').Append(offer.MorphLabel).Append(':').Append(offer.Price).Append(':').Append(offer.Rare).Append('\n');
            }

            return text.Append(colony.Wallet.Money).ToString();
        }

        private static readonly EventActionKind[] Mixed =
        {
            EventActionKind.Serve, EventActionKind.Browse, EventActionKind.Serve, EventActionKind.Serve,
            EventActionKind.Browse, EventActionKind.Serve, EventActionKind.Serve, EventActionKind.Serve,
        };

        [Test]
        public void SameChoices_GiveSameResults()
        {
            Assert.That(Transcript(Mixed, 0d), Is.EqualTo(Transcript(Mixed, 0d)));
        }

        [Test]
        public void PlayingLate_GivesSameResults()
        {
            Assert.That(Transcript(Mixed, 5d), Is.EqualTo(Transcript(Mixed, 0d)));
        }

        [Test]
        public void AllServe_SaleRateIsNearEventChance()
        {
            var listed = 0;
            var sold = 0;
            var expected = 0d;
            for (var trial = 0; trial < 300; trial++)
            {
                var colony = Entered(5, AtBase, out var calendar);
                var entry = colony.Events.Entry;
                entry.ListingSeed = 7919 * trial + 13;
                foreach (var listing in entry.Listings)
                {
                    expected += listing.FrozenChance;
                }

                listed += entry.Listings.Count;
                Assert.That(service.ServeAllRemaining(colony, calendar, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
                sold += colony.Events.LastSummary.Sold.Count;
            }

            Assert.That(sold / (double)listed, Is.EqualTo(expected / listed).Within(0.04d));
        }

        [Test]
        public void PlayAction_BeforeTheDayOrWithoutAnEntry_IsRefused()
        {
            var empty = EventTestRoom.Create(1, out var emptyCalendar);
            Assert.That(service.PlayAction(empty, emptyCalendar, EventActionKind.Serve, Day(emptyCalendar), out _), Is.EqualTo(EventActionResult.NoEvent));

            var colony = EventTestRoom.Create(2, out var calendar);
            Assert.That(service.Register(colony, calendar, 0, Nothing(colony), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            var before = EventSchedule.StartUtc(calendar, 0) - TimeSpan.FromSeconds(1);
            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, before, out var record), Is.EqualTo(EventActionResult.NotYet));
            Assert.That(record, Is.Null);
            Assert.That(service.ServeAllRemaining(colony, calendar, before, out _), Is.EqualTo(EventActionResult.NotYet));
            Assert.That(service.ActionsLeft(colony), Is.EqualTo(8));
        }

        [Test]
        public void Sales_PayIntoTheLedgerAndKeepTheLineage()
        {
            var colony = Entered(5, Cheap, out var calendar);
            var fullNames = new Dictionary<int, string>();
            foreach (var pet in colony.Animals)
            {
                fullNames[pet.Id] = MorphNamer.FullName(pet.Genotype, pet.Known);
            }

            var money = colony.Wallet.Money;
            var playedAt = Day(calendar) + TimeSpan.FromMinutes(5);

            Assert.That(service.ServeAllRemaining(colony, calendar, playedAt, out _), Is.EqualTo(EventActionResult.Ok));

            var sold = colony.Events.LastSummary.Sold;
            Assert.That(sold, Is.Not.Empty);
            foreach (var line in sold)
            {
                Assert.That(colony.AnimalById(line.AnimalId), Is.Null);
                var lineage = colony.Lineage.Find(r => r.Id == line.AnimalId);
                Assert.That(lineage, Is.Not.Null);
                Assert.That(lineage.Name, Is.EqualTo(line.Name));
                Assert.That(line.Amount, Is.EqualTo(1000));
                var note = $"{Spring}：{line.Name}（{fullNames[line.AnimalId]}）が売れました";
                var entry = colony.Wallet.Ledger.Find(e => e.Category == LedgerCategory.EventSale && e.Note == note);
                Assert.That(entry, Is.Not.Null, note);
                Assert.That(entry.Amount, Is.EqualTo(1000));
                Assert.That(entry.AtUtc, Is.EqualTo(playedAt));
            }

            Assert.That(colony.Wallet.Money, Is.EqualTo(money + 1000L * sold.Count));
        }

        [Test]
        public void SaleTimes_AreInsideTheirActionWindows()
        {
            var colony = Entered(5, Cheap, out var calendar);

            Assert.That(service.ServeAllRemaining(colony, calendar, Day(calendar), out var records), Is.EqualTo(EventActionResult.Ok));

            Assert.That(records.Count, Is.EqualTo(8));
            for (var i = 0; i < records.Count; i++)
            {
                var start = EventSchedule.ActionWindowStart(i, 8);
                Assert.That(records[i].Index, Is.EqualTo(i));
                foreach (var sale in records[i].Sales)
                {
                    Assert.That(sale.MinuteOfDay, Is.InRange(start, start + EventSchedule.ActionWindowLength(8) - 1));
                }
            }
        }

        [Test]
        public void Browse_AddsOneToThreeOffers()
        {
            var colony = Entered(2, Nothing, out var calendar);
            var entry = colony.Events.Entry;

            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out var first), Is.EqualTo(EventActionResult.Ok));
            var count = entry.BoothOffers.Count;
            Assert.That(count, Is.InRange(1, 3));
            Assert.That(entry.NextOfferId, Is.EqualTo(1 + count));
            Assert.That(first.BoothMinuteOfDay, Is.InRange(600, 600 + EventSchedule.ActionWindowLength(8) - 1));
            for (var i = 0; i < count; i++)
            {
                Assert.That(entry.BoothOffers[i].OfferId, Is.EqualTo(1 + i));
                Assert.That(entry.BoothOffers[i].ActionIndex, Is.EqualTo(0));
            }

            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            Assert.That(entry.BoothOffers[count].OfferId, Is.EqualTo(1 + count));
            Assert.That(entry.BoothOffers[count].ActionIndex, Is.EqualTo(1));
            Assert.That(service.ActionsLeft(colony), Is.EqualTo(6));
        }

        [Test]
        public void Buy_PutsTheAnimalInTheLowestEmptyCageAndRecordsIt()
        {
            var colony = Entered(2, Nothing, out var calendar);
            colony.AddCage(CageSize.Standard);
            colony.AddCage(CageSize.Small);
            var at = Day(calendar) + TimeSpan.FromMinutes(3);
            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, at, out _), Is.EqualTo(EventActionResult.Ok));
            var entry = colony.Events.Entry;
            var offer = entry.BoothOffers[0];
            var pet = offer.Animal;
            var price = offer.Price;
            var fullName = MorphNamer.FullName(pet.Genotype, pet.Known);
            var cage = colony.FirstEmptyCage();
            var newName = $"レオパ{colony.NextAnimalId}";
            var money = colony.Wallet.Money;

            Assert.That(service.CheckBuy(colony, offer.OfferId), Is.EqualTo(EventBuyResult.Ok));
            Assert.That(service.Buy(colony, offer.OfferId, at), Is.EqualTo(EventBuyResult.Ok));

            Assert.That(colony.Wallet.Money, Is.EqualTo(money - price));
            Assert.That(colony.CageOf(pet), Is.SameAs(cage));
            Assert.That(pet.Name, Is.EqualTo(newName));
            Assert.That(pet.PersonalityKnown, Is.False);
            Assert.That(pet.PersonalityRevealAtUtc, Is.EqualTo(at + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays)));
            Assert.That(offer.Bought, Is.True);
            Assert.That(offer.Animal, Is.Null);
            Assert.That(entry.Purchases.Count, Is.EqualTo(1));
            Assert.That(entry.Purchases[0].Name, Is.EqualTo(newName));
            Assert.That(entry.Purchases[0].Amount, Is.EqualTo(price));
            Assert.That(entry.Purchases[0].MorphLabel, Is.EqualTo(offer.MorphLabel));

            var ledger = colony.Wallet.Ledger[colony.Wallet.Ledger.Count - 1];
            Assert.That(ledger.Category, Is.EqualTo(LedgerCategory.AnimalPurchase));
            Assert.That(ledger.Amount, Is.EqualTo(-price));
            Assert.That(ledger.Note, Is.EqualTo($"{Spring}：{newName}（{fullName}）を購入"));
            Assert.That(ledger.AtUtc, Is.EqualTo(at));

            Assert.That(service.Buy(colony, offer.OfferId, at), Is.EqualTo(EventBuyResult.AlreadyBought));
            Assert.That(service.Buy(colony, 999, at), Is.EqualTo(EventBuyResult.NotFound));
        }

        [Test]
        public void Buy_WithNoEmptyCage_IsRefusedBeforeTheMoney()
        {
            var noEntry = EventTestRoom.Create(1, out _);
            Assert.That(service.CheckBuy(noEntry, 1), Is.EqualTo(EventBuyResult.NoEvent));

            var colony = Entered(2, Nothing, out var calendar);
            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            for (var cage = colony.FirstEmptyCage(); cage != null; cage = colony.FirstEmptyCage())
            {
                colony.AddAnimal(new PetState { Name = "満室" }, cage);
            }

            colony.Wallet.Money = 0;
            var offer = colony.Events.Entry.BoothOffers[0];

            Assert.That(service.CheckBuy(colony, offer.OfferId), Is.EqualTo(EventBuyResult.NoEmptyCage));
            Assert.That(service.Buy(colony, offer.OfferId, Day(calendar)), Is.EqualTo(EventBuyResult.NoEmptyCage));
            Assert.That(offer.Bought, Is.False);

            colony.AddCage(CageSize.Standard);
            Assert.That(service.CheckBuy(colony, offer.OfferId), Is.EqualTo(EventBuyResult.NotEnoughMoney));
        }

        [Test]
        public void LastServe_FinishesWhenNothingIsLeftToBuy()
        {
            var colony = Entered(3, AtBase, out var calendar);
            for (var i = 0; i < 8; i++)
            {
                Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            }

            var summary = colony.Events.LastSummary;
            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(summary.Number, Is.EqualTo(0));
            Assert.That(summary.BoothFee, Is.EqualTo(10000));
            Assert.That(summary.Sold.Count + summary.Unsold.Count, Is.EqualTo(3));
            Assert.That(colony.Events.LastSummarySeen, Is.False);
            foreach (var line in summary.Unsold)
            {
                Assert.That(colony.AnimalById(line.AnimalId), Is.Not.Null);
                Assert.That(colony.Events.IsListed(line.AnimalId), Is.False);
            }

            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, Day(calendar), out _), Is.EqualTo(EventActionResult.NoEvent));
            Assert.That(service.OpenNumber(colony, calendar, Day(calendar)), Is.EqualTo(1));

            service.DismissSummary(colony);
            Assert.That(colony.Events.LastSummarySeen, Is.True);
        }

        [Test]
        public void LastAction_WithUnboughtOffers_WaitsForFinish()
        {
            var colony = Entered(3, AtBase, out var calendar);
            Assert.That(service.Finish(colony), Is.False);
            for (var i = 0; i < 7; i++)
            {
                Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            }

            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));

            Assert.That(colony.Events.Entry, Is.Not.Null);
            Assert.That(service.ActionsLeft(colony), Is.EqualTo(0));
            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, Day(calendar), out _), Is.EqualTo(EventActionResult.NoActionsLeft));
            Assert.That(service.Finish(colony), Is.True);
            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(colony.Events.LastSummary.Bought, Is.Empty);
        }

        [Test]
        public void BuyingTheLastOffer_AfterTheActions_Finishes()
        {
            var colony = Entered(1, Nothing, out var calendar);
            colony.AddCage(CageSize.Standard);
            colony.AddCage(CageSize.Standard);
            colony.AddCage(CageSize.Standard);
            for (var i = 0; i < 7; i++)
            {
                Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            }

            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            var offers = new List<EventBoothOffer>(colony.Events.Entry.BoothOffers);
            foreach (var offer in offers)
            {
                Assert.That(service.Buy(colony, offer.OfferId, Day(calendar)), Is.EqualTo(EventBuyResult.Ok));
            }

            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(colony.Events.LastSummary.Bought.Count, Is.EqualTo(offers.Count));
        }

        [Test]
        public void ServeAllRemaining_PlaysTheRestAsServesAndFinishes()
        {
            var colony = Entered(3, AtBase, out var calendar);
            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));

            Assert.That(service.ServeAllRemaining(colony, calendar, Day(calendar), out var records), Is.EqualTo(EventActionResult.Ok));

            Assert.That(records.Count, Is.EqualTo(7));
            foreach (var record in records)
            {
                Assert.That(record.Kind, Is.EqualTo(EventActionKind.Serve));
            }

            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(colony.Events.LastSummary, Is.Not.Null);
        }
    }
}
```

`Sales_PayIntoTheLedgerAndKeepTheLineage` は価格1,000円（全体の確率がほぼ98%）で5匹出すので、売れた個体が1匹もいないことはまず起きない。もし `sold` が空で落ちたら、種を変えずに止めて報告する。

- [ ] **Step 2: 実装を書く**（`Assets/Scripts/Core/EventService.Day.cs`）

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public enum EventActionResult
    {
        Ok,
        NoEvent,
        NotYet,
        NoActionsLeft
    }

    public enum EventBuyResult
    {
        Ok,
        NoEvent,
        NotFound,
        AlreadyBought,
        NotEnoughMoney,
        NoEmptyCage
    }

    /// <summary>
    /// The event day (phase 6 spec §2.3–2.7): each action is decided by its own seed, so the same
    /// choices give the same results whenever they are played; the ledger uses the play time (★R).
    /// </summary>
    public sealed partial class EventService
    {
        public int ActionsLeft(Colony colony)
        {
            var entry = colony.Events.Entry;
            return entry == null ? 0 : Math.Max(0, ActionCountOf(entry) - entry.Actions.Count);
        }

        public EventActionResult PlayAction(Colony colony, GameCalendar calendar, EventActionKind kind, DateTimeOffset playedAtUtc, out EventActionRecord record)
        {
            record = null;
            var entry = colony.Events.Entry;
            if (entry == null)
            {
                return EventActionResult.NoEvent;
            }

            if (!EventSchedule.HasStarted(calendar, entry.Number, playedAtUtc))
            {
                return EventActionResult.NotYet;
            }

            var count = ActionCountOf(entry);
            if (entry.Actions.Count >= count)
            {
                return EventActionResult.NoActionsLeft;
            }

            if (!entry.Frozen)
            {
                // Advance normally froze it at 0:00; this covers a caller that skipped it.
                Freeze(colony, entry, new EventReport());
            }

            var index = entry.Actions.Count;
            record = new EventActionRecord { Index = index, Kind = kind, PlayedAtUtc = playedAtUtc };
            var windowStart = EventSchedule.ActionWindowStart(index, count);
            var windowLength = EventSchedule.ActionWindowLength(count);
            var eventName = EventSchedule.NameOf(entry.Number);
            var random = EventRandom.ForAction(entry.ListingSeed, index, kind);
            var listings = new List<EventListing>(entry.Listings);
            listings.Sort((a, b) => a.AnimalId.CompareTo(b.AnimalId));
            foreach (var listing in listings)
            {
                // Always three draws per listing, so one listing's result never shifts another's.
                var saleRoll = random.NextDouble();
                var discountRoll = random.NextDouble();
                var minuteRoll = random.NextDouble();
                if (listing.Sold || saleRoll >= EventOdds.ActionChance(listing.FrozenChance, count, kind))
                {
                    continue;
                }

                var pet = colony.AnimalById(listing.AnimalId);
                if (pet == null)
                {
                    continue;
                }

                var amount = EventOdds.SaleAmount(listing.Price, listing.TolerancePercent, discountRoll);
                var note = $"{eventName}：{pet.Name}（{MorphNamer.FullName(pet.Genotype, pet.Known)}）が売れました";
                listing.SoldInAction = index;
                record.Sales.Add(new EventAnimalLine
                {
                    AnimalId = pet.Id,
                    Name = pet.Name,
                    MorphLabel = EventRules.MorphLabel(pet),
                    Amount = amount,
                    MinuteOfDay = windowStart + (int)(minuteRoll * windowLength),
                });
                colony.RemoveAnimal(pet);
                colony.Wallet.Earn(amount, LedgerCategory.EventSale, note, playedAtUtc);
            }

            record.Sales.Sort((a, b) => a.MinuteOfDay != b.MinuteOfDay ? a.MinuteOfDay.CompareTo(b.MinuteOfDay) : a.AnimalId.CompareTo(b.AnimalId));

            if (kind == EventActionKind.Browse)
            {
                var offers = BoothStockGenerator.Generate(EventRandom.ForBooth(entry.ListingSeed, index), EventSchedule.KindOf(entry.Number),
                    EventRandom.DemandSeed(colony.BreedingSeed, entry.Number), playedAtUtc, care, entry.NextOfferId, index);
                entry.NextOfferId += offers.Count;
                entry.BoothOffers.AddRange(offers);
                record.BoothMinuteOfDay = windowStart + windowLength / 2;
            }

            entry.Actions.Add(record);
            FinishIfDone(colony, entry);
            return EventActionResult.Ok;
        }

        /// <summary>「残りを全部接客にする」: the remaining actions as serves, then the event is finished even with unbought offers (spec §2.3・§2.7).</summary>
        public EventActionResult ServeAllRemaining(Colony colony, GameCalendar calendar, DateTimeOffset playedAtUtc, out List<EventActionRecord> records)
        {
            records = new List<EventActionRecord>();
            var entry = colony.Events.Entry;
            if (entry == null)
            {
                return EventActionResult.NoEvent;
            }

            if (!EventSchedule.HasStarted(calendar, entry.Number, playedAtUtc))
            {
                return EventActionResult.NotYet;
            }

            while (colony.Events.Entry == entry && ActionsLeft(colony) > 0)
            {
                PlayAction(colony, calendar, EventActionKind.Serve, playedAtUtc, out var record);
                records.Add(record);
            }

            if (colony.Events.Entry == entry)
            {
                Finish(colony);
            }

            return EventActionResult.Ok;
        }

        /// <summary>The buy dialog's check: the cage before the money, so a full room says 「ケージが足りません」 (spec §2.5).</summary>
        public EventBuyResult CheckBuy(Colony colony, int offerId)
        {
            var entry = colony.Events.Entry;
            if (entry == null)
            {
                return EventBuyResult.NoEvent;
            }

            var offer = entry.BoothOffers.Find(o => o.OfferId == offerId);
            if (offer == null)
            {
                return EventBuyResult.NotFound;
            }

            if (offer.Bought || offer.Animal == null)
            {
                return EventBuyResult.AlreadyBought;
            }

            if (colony.FirstEmptyCage() == null)
            {
                return EventBuyResult.NoEmptyCage;
            }

            return colony.Wallet.Money < offer.Price ? EventBuyResult.NotEnoughMoney : EventBuyResult.Ok;
        }

        /// <summary>Like the shop (★8): named レオパN, personality shown after 7 game days, into the lowest-numbered empty cage.</summary>
        public EventBuyResult Buy(Colony colony, int offerId, DateTimeOffset nowUtc)
        {
            var problem = CheckBuy(colony, offerId);
            if (problem != EventBuyResult.Ok)
            {
                return problem;
            }

            var entry = colony.Events.Entry;
            var offer = entry.BoothOffers.Find(o => o.OfferId == offerId);
            var pet = offer.Animal;
            var name = $"レオパ{colony.NextAnimalId}";
            var note = $"{EventSchedule.NameOf(entry.Number)}：{name}（{MorphNamer.FullName(pet.Genotype, pet.Known)}）を購入";
            if (!colony.Wallet.TrySpend(offer.Price, LedgerCategory.AnimalPurchase, note, nowUtc))
            {
                return EventBuyResult.NotEnoughMoney;
            }

            pet.Name = name;
            pet.LastSavedAtUtc = nowUtc;
            pet.LastShedAtUtc = nowUtc;
            pet.NextShedAtUtc = nowUtc + SheddingModel.IntervalFor(pet.Stage, care);
            pet.PersonalityKnown = false;
            pet.PersonalityRevealAtUtc = nowUtc + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays);
            colony.AddAnimal(pet, colony.FirstEmptyCage());
            offer.Animal = null;
            offer.Bought = true;
            entry.Purchases.Add(new EventAnimalLine { AnimalId = pet.Id, Name = name, MorphLabel = offer.MorphLabel, Amount = offer.Price });
            FinishIfDone(colony, entry);
            return EventBuyResult.Ok;
        }

        /// <summary>「集計へ」: allowed once every action is used. Makes the summary and opens the next event.</summary>
        public bool Finish(Colony colony)
        {
            var entry = colony.Events.Entry;
            if (entry == null || entry.Actions.Count < ActionCountOf(entry))
            {
                return false;
            }

            var summary = new EventSummary { Number = entry.Number, BoothFee = entry.FeePaid };
            foreach (var action in entry.Actions)
            {
                summary.Sold.AddRange(action.Sales);
            }

            foreach (var listing in entry.Listings)
            {
                if (listing.Sold)
                {
                    continue;
                }

                var pet = colony.AnimalById(listing.AnimalId);
                summary.Unsold.Add(new EventAnimalLine
                {
                    AnimalId = listing.AnimalId,
                    Name = pet != null ? pet.Name : string.Empty,
                    MorphLabel = pet != null ? EventRules.MorphLabel(pet) : string.Empty,
                    Amount = listing.Price,
                });
            }

            summary.Bought.AddRange(entry.Purchases);
            colony.Events.LastSummary = summary;
            colony.Events.LastSummarySeen = false;
            colony.Events.Entry = null;
            return true;
        }

        /// <summary>「閉じる」 on the result screen (★11).</summary>
        public void DismissSummary(Colony colony) => colony.Events.LastSummarySeen = true;

        private static int ActionCountOf(EventEntry entry) => EventSchedule.ActionCount(EventSchedule.KindOf(entry.Number));

        /// <summary>★5: after the last action the event waits while anything is left to buy.</summary>
        private void FinishIfDone(Colony colony, EventEntry entry)
        {
            if (entry.Actions.Count >= ActionCountOf(entry) && !entry.BoothOffers.Exists(o => !o.Bought))
            {
                Finish(colony);
            }
        }
    }
}
```

- [ ] **Step 3: テストを流す**

`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.EventDayTests` → 全部通る。`AllServe_SaleRateIsNearEventChance` が外れたら、試行の数や許容幅を変えずに止めて報告する（Review Focus 1）。`PlayingLate_GivesSameResults` が外れたら、ブースの個体の値段が遊んだ時刻に依存している印（`MarketPrice.For` が年齢を見ている等）なので止めて報告する。EditMode 全体の件数が減っていないこと。

- [ ] **Step 4: コミット**

```bash
git add Assets/Scripts/Core/EventService.Day.cs* Assets/Tests/EditMode/EventDayTests.cs*
git commit -m "Add the event day: actions, booth purchases and the summary"
```

---

### Task 7: イベントの文字と演出の台本（transcriber）

**Files:**
- Create: `Assets/Scripts/UI/EventText.cs`
- Create: `Assets/Scripts/UI/EventReplay.cs`
- Test: `Assets/Tests/EditMode/EventTextTests.cs`

**Produces（Task 12〜14 が使う）:** `EventText`（金額・時刻・通知の文・ログの行・失敗の文・確認の文）と `EventReplay`（`DurationSeconds`・`Script`・`RevealedCount`・`ReplayLine`）。どちらも `TerrariumDays.UI` の名前空間（`BreedingText`・`ShopText` と同じ置き場所。テストのアセンブリは `TerrariumDays.Runtime` を参照しているので UI の型も見える）。

- [ ] **Step 1: テストを書く**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;

namespace TerrariumDays.Tests
{
    public sealed class EventTextTests
    {
        private const string Spring = "ジャパン・レプタイルズ・フェスタ（春）";
        private const string Marche = "レプタイル・マルシェ";

        private static EventAnimalLine Sale(int minute, long amount, string label = "レイプター♀") =>
            new EventAnimalLine { AnimalId = minute, Name = "ハナ", MorphLabel = label, Amount = amount, MinuteOfDay = minute };

        [Test]
        public void Yen_UsesTheEn()
        {
            Assert.That(EventText.Yen(28500), Is.EqualTo("28,500円"));
            Assert.That(EventText.Yen(0), Is.EqualTo("0円"));
            Assert.That(EventText.Yen(1234567), Is.EqualTo("1,234,567円"));
        }

        [Test]
        public void SignedYen_ShowsThePlusOrMinus()
        {
            Assert.That(EventText.SignedYen(12000), Is.EqualTo("+12,000円"));
            Assert.That(EventText.SignedYen(0), Is.EqualTo("0円"));
            Assert.That(EventText.SignedYen(-3000), Is.EqualTo("−3,000円"));
        }

        [Test]
        public void Clock_IsHoursAndMinutes()
        {
            Assert.That(EventText.Clock(600), Is.EqualTo("10:00"));
            Assert.That(EventText.Clock(684), Is.EqualTo("11:24"));
            Assert.That(EventText.Clock(1019), Is.EqualTo("16:59"));
        }

        [Test]
        public void Notices_MatchThePlannedWording()
        {
            Assert.That(EventText.DeadlineNotice(1, 3), Is.EqualTo(Marche + "（5月15日）の申し込みはあと3日で締め切りです"));
            Assert.That(EventText.DeadlineNotice(0, 1), Is.EqualTo(Spring + "の申し込みは今日までです"));
            Assert.That(EventText.DayStartedNotice(0), Is.EqualTo("今日は" + Spring + "です。イベントタブで遊べます"));
            Assert.That(EventText.PendingNotice(0), Is.EqualTo(Spring + "の続きがあります（イベントタブ）"));
            Assert.That(EventText.WithdrawnNotice(new[] { "ハナ", "タロウ" }), Is.EqualTo("出品を取り消しました：ハナ、タロウ"));
        }

        [Test]
        public void NoticesOf_ListsTheReportInAFixedOrder()
        {
            var report = new EventReport { DeadlineNoticeNumber = 1, DeadlineDaysLeft = 3, DayStartedNumber = 0, PendingNumber = 0 };
            report.Withdrawn.Add("ハナ");

            Assert.That(EventText.NoticesOf(report), Is.EqualTo(new[]
            {
                "出品を取り消しました：ハナ",
                "今日は" + Spring + "です。イベントタブで遊べます",
                Spring + "の続きがあります（イベントタブ）",
                Marche + "（5月15日）の申し込みはあと3日で締め切りです",
            }));
            Assert.That(EventText.NoticesOf(new EventReport()), Is.Empty);
        }

        [Test]
        public void LogLines_MatchTheSpecExample()
        {
            Assert.That(EventText.SaleLine(Sale(684, 28500)), Is.EqualTo("11:24 レイプター♀ 売約（28,500円）"));
            Assert.That(EventText.BoothLine(705, 2), Is.EqualTo("11:45 他のブースで売り物を2匹見つけた"));
            Assert.That(EventText.NoSaleLine(652), Is.EqualTo("10:52 売約はなかった"));
            Assert.That(EventText.OfferLine(new EventBoothOffer { MorphLabel = "ハイポ?", Price = 8000 }), Is.EqualTo("ハイポ? 8,000円"));
            Assert.That(EventText.OfferLine(new EventBoothOffer { MorphLabel = "マックスノー ベル♂", Price = 120000, Rare = true }),
                Is.EqualTo("掘り出し物！ マックスノー ベル♂ 120,000円"));
        }

        [Test]
        public void Labels_ForThePreparationScreen()
        {
            Assert.That(EventText.Chance(0.754d), Is.EqualTo("売れる見込み 75%"));
            Assert.That(EventText.Tolerance(0), Is.EqualTo("値引きしない"));
            Assert.That(EventText.Tolerance(15), Is.EqualTo("15%まで値引き"));
            Assert.That(EventText.ExpectedTotal(12345.6d), Is.EqualTo("期待値の合計 12,346円"));
            Assert.That(EventText.ActionsLeft(3, 8), Is.EqualTo("残りの行動 3/8"));
            Assert.That(EventText.Listings(2, 5), Is.EqualTo("出品 2/5匹"));
        }

        [Test]
        public void FailureMessages_CoverEveryRefusal()
        {
            foreach (EventRegisterResult result in System.Enum.GetValues(typeof(EventRegisterResult)))
            {
                Assert.That(EventText.RegisterFailure(result), result == EventRegisterResult.Ok ? Is.Empty : Is.Not.Empty, result.ToString());
            }

            foreach (EventBuyResult result in System.Enum.GetValues(typeof(EventBuyResult)))
            {
                Assert.That(EventText.BuyFailure(result), result == EventBuyResult.Ok ? Is.Empty : Is.Not.Empty, result.ToString());
            }

            Assert.That(EventText.BuyFailure(EventBuyResult.NoEmptyCage), Is.EqualTo("ケージが足りません"));
            Assert.That(EventText.BuyFailure(EventBuyResult.NotEnoughMoney), Is.EqualTo("所持金が足りません"));
            Assert.That(EventText.RegisterFailure(EventRegisterResult.NotEnoughMoney), Is.EqualTo("出店料が足りません"));
        }

        [Test]
        public void Confirmations()
        {
            Assert.That(EventText.ConfirmRegister(0), Is.EqualTo(Spring + "に申し込みますか？（出店料 10,000円・取り消せません）"));
            Assert.That(EventText.ConfirmBuy(new EventBoothOffer { MorphLabel = "ハイポ?", Price = 8000 }), Is.EqualTo("ハイポ?を8,000円で買いますか？"));
        }

        [Test]
        public void Duration_GrowsWithTheEventsUpToFifteenSeconds()
        {
            Assert.That(EventReplay.DurationSeconds(0), Is.EqualTo(10d));
            Assert.That(EventReplay.DurationSeconds(2), Is.EqualTo(13d));
            Assert.That(EventReplay.DurationSeconds(9), Is.EqualTo(15d));
        }

        [Test]
        public void Script_ServeWithSales_ListsThemInTimeOrder()
        {
            var record = new EventActionRecord { Index = 1, Kind = EventActionKind.Serve };
            record.Sales.Add(Sale(700, 9000));
            record.Sales.Add(Sale(720, 28500, "ハイポ♂"));

            var lines = EventReplay.Script(record, new List<EventBoothOffer>(), 8);

            Assert.That(lines.Count, Is.EqualTo(3));
            Assert.That(lines[0].Text, Is.EqualTo("10:52 接客を始めた"));
            Assert.That(lines[0].Highlight, Is.False);
            Assert.That(lines[1].Text, Is.EqualTo("11:40 レイプター♀ 売約（9,000円）"));
            Assert.That(lines[1].Bubble, Is.EqualTo(EventReplay.SaleBubble));
            Assert.That(lines[1].Highlight, Is.True);
            Assert.That(lines[2].MinuteOfDay, Is.EqualTo(720));
            Assert.That(EventReplay.EventCount(record, new List<EventBoothOffer>()), Is.EqualTo(2));
        }

        [Test]
        public void Script_ServeWithoutSales_SaysSo()
        {
            var record = new EventActionRecord { Index = 0, Kind = EventActionKind.Serve };

            var lines = EventReplay.Script(record, new List<EventBoothOffer>(), 8);

            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[1].Text, Is.EqualTo("10:51 売約はなかった"));
        }

        [Test]
        public void Script_Browse_ShowsOnlyThisActionsOffers()
        {
            var record = new EventActionRecord { Index = 2, Kind = EventActionKind.Browse, BoothMinuteOfDay = 731 };
            record.Sales.Add(Sale(710, 9000));
            var offers = new List<EventBoothOffer>
            {
                new EventBoothOffer { OfferId = 1, ActionIndex = 0, MorphLabel = "ハイポ?", Price = 8000 },
                new EventBoothOffer { OfferId = 2, ActionIndex = 2, MorphLabel = "ノーマル♀", Price = 6000 },
                new EventBoothOffer { OfferId = 3, ActionIndex = 2, MorphLabel = "マックスノー ベル♂", Price = 120000, Rare = true },
            };

            var lines = EventReplay.Script(record, offers, 8);

            Assert.That(lines.Count, Is.EqualTo(5));
            Assert.That(lines[0].Text, Is.EqualTo("11:45 他のブースを見に行った"));
            Assert.That(lines[1].Text, Is.EqualTo("11:50 レイプター♀ 売約（9,000円）"));
            Assert.That(lines[2].Text, Is.EqualTo("12:11 他のブースで売り物を2匹見つけた"));
            Assert.That(lines[3].Text, Is.EqualTo("ノーマル♀ 6,000円"));
            Assert.That(lines[3].OfferId, Is.EqualTo(2));
            Assert.That(lines[4].Highlight, Is.True);
            Assert.That(lines[4].Bubble, Is.EqualTo(EventReplay.RareBubble));
            Assert.That(EventReplay.EventCount(record, offers), Is.EqualTo(3));
        }

        [Test]
        public void RevealedCount_ShowsTheFirstLineAtOnceAndAllAtTheEnd()
        {
            Assert.That(EventReplay.RevealedCount(4, -1d, 12d), Is.EqualTo(0));
            Assert.That(EventReplay.RevealedCount(4, 0d, 12d), Is.EqualTo(1));
            Assert.That(EventReplay.RevealedCount(4, 2.9d, 12d), Is.EqualTo(1));
            Assert.That(EventReplay.RevealedCount(4, 3d, 12d), Is.EqualTo(2));
            Assert.That(EventReplay.RevealedCount(4, 11.9d, 12d), Is.EqualTo(4));
            Assert.That(EventReplay.RevealedCount(4, 99d, 12d), Is.EqualTo(4));
            Assert.That(EventReplay.RevealedCount(0, 5d, 12d), Is.EqualTo(0));
        }
    }
}
```

時刻の確かめ：大規模（8行動）の時間帯の長さは420/8＝52分、行動1は10:52から、行動2は11:45から。接客で売約がないときの行は時間帯の終わりの1分前（行動0なら10:51）。

- [ ] **Step 2: `EventText` を書く**（`Assets/Scripts/UI/EventText.cs`）

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>The event tab's words (phase 6 spec §2.6・§3, planner ★9・★12・★15・★16). Money is "28,500円" here only.</summary>
    public static class EventText
    {
        public const string ListedStatus = "出品中";

        public static string Yen(long yen) => yen.ToString("#,0", CultureInfo.InvariantCulture) + "円";

        public static string SignedYen(long yen) => yen > 0 ? "+" + Yen(yen) : yen < 0 ? "−" + Yen(-yen) : Yen(0);

        public static string Clock(int minuteOfDay) => $"{minuteOfDay / 60}:{minuteOfDay % 60:00}";

        public static string DateLabel(int number) => $"{EventSchedule.MonthOf(number)}月{EventSchedule.EventDayOfMonth}日";

        public static string DeadlineNotice(int number, int daysLeft) =>
            daysLeft <= 1
                ? $"{EventSchedule.NameOf(number)}の申し込みは今日までです"
                : $"{EventSchedule.NameOf(number)}（{DateLabel(number)}）の申し込みはあと{daysLeft}日で締め切りです";

        public static string DayStartedNotice(int number) => $"今日は{EventSchedule.NameOf(number)}です。イベントタブで遊べます";

        public static string PendingNotice(int number) => $"{EventSchedule.NameOf(number)}の続きがあります（イベントタブ）";

        public static string WithdrawnNotice(IEnumerable<string> names) => "出品を取り消しました：" + string.Join("、", names);

        /// <summary>The notices of one report: withdrawn, the day began, unfinished, deadline. The caller folds them like phase 5 (first one + 「ほか○件」).</summary>
        public static List<string> NoticesOf(EventReport report)
        {
            var notices = new List<string>();
            if (report.Withdrawn.Count > 0)
            {
                notices.Add(WithdrawnNotice(report.Withdrawn));
            }

            if (report.DayStartedNumber >= 0)
            {
                notices.Add(DayStartedNotice(report.DayStartedNumber));
            }

            if (report.PendingNumber >= 0)
            {
                notices.Add(PendingNotice(report.PendingNumber));
            }

            if (report.DeadlineNoticeNumber >= 0)
            {
                notices.Add(DeadlineNotice(report.DeadlineNoticeNumber, report.DeadlineDaysLeft));
            }

            return notices;
        }

        public static string SaleLine(EventAnimalLine sale) => $"{Clock(sale.MinuteOfDay)} {sale.MorphLabel} 売約（{Yen(sale.Amount)}）";

        public static string NoSaleLine(int minuteOfDay) => $"{Clock(minuteOfDay)} 売約はなかった";

        public static string BoothLine(int minuteOfDay, int offerCount) => $"{Clock(minuteOfDay)} 他のブースで売り物を{offerCount}匹見つけた";

        public static string OfferLine(EventBoothOffer offer) => (offer.Rare ? "掘り出し物！ " : string.Empty) + $"{offer.MorphLabel} {Yen(offer.Price)}";

        /// <summary>A summary row: 「レイプター♀ ハナ 28,500円」.</summary>
        public static string AnimalLine(EventAnimalLine line) => $"{line.MorphLabel} {line.Name} {Yen(line.Amount)}";

        public static string Chance(double chance) => $"売れる見込み {Math.Round(chance * 100d, MidpointRounding.AwayFromZero):0}%";

        public static string Tolerance(int percent) => percent == 0 ? "値引きしない" : $"{percent}%まで値引き";

        public static string ExpectedTotal(double yen) => "期待値の合計 " + Yen((long)Math.Round(yen, MidpointRounding.AwayFromZero));

        public static string ActionsLeft(int left, int count) => $"残りの行動 {left}/{count}";

        public static string Listings(int count, int max) => $"出品 {count}/{max}匹";

        public static string ConfirmRegister(int number) =>
            $"{EventSchedule.NameOf(number)}に申し込みますか？（出店料 {Yen(EventSchedule.BoothFee(EventSchedule.KindOf(number)))}・取り消せません）";

        public static string ConfirmBuy(EventBoothOffer offer) => $"{offer.MorphLabel}を{Yen(offer.Price)}で買いますか？";

        public static string RegisterFailure(EventRegisterResult result)
        {
            switch (result)
            {
                case EventRegisterResult.NotOpen:
                    return "この回はもう受け付けていません";
                case EventRegisterResult.Pending:
                    return "前の回がまだ終わっていません";
                case EventRegisterResult.AlreadyRegistered:
                    return "もう申し込んでいます";
                case EventRegisterResult.TooMany:
                    return "出品できる数を超えています";
                case EventRegisterResult.NotEligible:
                    return "出品できない個体が入っています";
                case EventRegisterResult.Duplicate:
                    return "同じ個体が2回入っています";
                case EventRegisterResult.InvalidPrice:
                    return "価格が低すぎます";
                case EventRegisterResult.InvalidTolerance:
                    return "値引きの幅が正しくありません";
                case EventRegisterResult.NotEnoughMoney:
                    return "出店料が足りません";
                case EventRegisterResult.Frozen:
                    return "開催日になったので出品は変えられません";
                default:
                    return string.Empty;
            }
        }

        public static string BuyFailure(EventBuyResult result)
        {
            switch (result)
            {
                case EventBuyResult.NoEvent:
                    return "イベントは終わりました";
                case EventBuyResult.NotFound:
                    return "売り物が見つかりません";
                case EventBuyResult.AlreadyBought:
                    return "もう買いました";
                case EventBuyResult.NotEnoughMoney:
                    return "所持金が足りません";
                case EventBuyResult.NoEmptyCage:
                    return "ケージが足りません";
                default:
                    return string.Empty;
            }
        }
    }
}
```

`EventSchedule.EventDayOfMonth`（15）は Task 1 の定数。名前が違ったら Task 1 のコードに合わせて直してよい（値は15のまま）。

- [ ] **Step 3: `EventReplay` を書く**（`Assets/Scripts/UI/EventReplay.cs`）

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>One line of the replay log. Offer lines carry the offer to buy (OfferId &gt; 0).</summary>
    public sealed class ReplayLine
    {
        public int MinuteOfDay { get; set; }
        public string Text { get; set; }

        /// <summary>The visitor's speech bubble, or null.</summary>
        public string Bubble { get; set; }

        public bool Highlight { get; set; }
        public int OfferId { get; set; }
    }

    /// <summary>
    /// The 10–15 s replay of one action (★K・★12). It only shows results that are already decided,
    /// so skipping or closing the app never changes anything.
    /// </summary>
    public static class EventReplay
    {
        public const double BaseSeconds = 10d;
        public const double SecondsPerEvent = 1.5d;
        public const double MaxSeconds = 15d;
        public const string ServeBubble = "いらっしゃいませ！";
        public const string BrowseBubble = "どんな子がいるかな";
        public const string SaleBubble = "この子にします！";
        public const string RareBubble = "掘り出し物！";

        public static double DurationSeconds(int events) => Math.Min(MaxSeconds, BaseSeconds + SecondsPerEvent * Math.Max(0, events));

        /// <summary>Sales plus the offers this action found.</summary>
        public static int EventCount(EventActionRecord record, IList<EventBoothOffer> offers) =>
            record.Sales.Count + OffersOf(record, offers).Count;

        public static List<ReplayLine> Script(EventActionRecord record, IList<EventBoothOffer> offers, int actionCount)
        {
            var start = EventSchedule.ActionWindowStart(record.Index, actionCount);
            var length = EventSchedule.ActionWindowLength(actionCount);
            var browse = record.Kind == EventActionKind.Browse;
            var lines = new List<ReplayLine>
            {
                new ReplayLine
                {
                    MinuteOfDay = start,
                    Text = $"{EventText.Clock(start)} {(browse ? "他のブースを見に行った" : "接客を始めた")}",
                    Bubble = browse ? BrowseBubble : ServeBubble,
                },
            };

            var timed = new List<ReplayLine>();
            foreach (var sale in record.Sales)
            {
                timed.Add(new ReplayLine { MinuteOfDay = sale.MinuteOfDay, Text = EventText.SaleLine(sale), Bubble = SaleBubble, Highlight = true });
            }

            if (browse)
            {
                var found = OffersOf(record, offers);
                timed.Add(new ReplayLine { MinuteOfDay = record.BoothMinuteOfDay, Text = EventText.BoothLine(record.BoothMinuteOfDay, found.Count) });
                foreach (var offer in found)
                {
                    timed.Add(new ReplayLine
                    {
                        MinuteOfDay = record.BoothMinuteOfDay,
                        Text = EventText.OfferLine(offer),
                        Bubble = offer.Rare ? RareBubble : null,
                        Highlight = offer.Rare,
                        OfferId = offer.OfferId,
                    });
                }
            }
            else if (record.Sales.Count == 0)
            {
                timed.Add(new ReplayLine { MinuteOfDay = start + length - 1, Text = EventText.NoSaleLine(start + length - 1) });
            }

            // OrderBy is stable: the booth line stays before its offers.
            lines.AddRange(timed.OrderBy(l => l.MinuteOfDay));
            return lines;
        }

        /// <summary>Line i appears at duration·i/count; everything is shown once the time is up (or on 「飛ばす」).</summary>
        public static int RevealedCount(int lineCount, double elapsedSeconds, double durationSeconds)
        {
            if (lineCount <= 0 || elapsedSeconds < 0d)
            {
                return 0;
            }

            if (elapsedSeconds >= durationSeconds || durationSeconds <= 0d)
            {
                return lineCount;
            }

            return Math.Min(lineCount, (int)Math.Floor(elapsedSeconds * lineCount / durationSeconds) + 1);
        }

        private static List<EventBoothOffer> OffersOf(EventActionRecord record, IList<EventBoothOffer> offers)
        {
            var found = new List<EventBoothOffer>();
            if (record.Kind != EventActionKind.Browse || offers == null)
            {
                return found;
            }

            foreach (var offer in offers)
            {
                if (offer.ActionIndex == record.Index)
                {
                    found.Add(offer);
                }
            }

            return found;
        }
    }
}
```

- [ ] **Step 4: テストを流す**

`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.EventTextTests` → 全部通る。EditMode 全体の件数が減っていないこと。

- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/UI/EventText.cs* Assets/Scripts/UI/EventReplay.cs* Assets/Tests/EditMode/EventTextTests.cs*
git commit -m "Add the event texts and the replay script"
```

---

### Task 8: 出品中のロック（transcriber）

**Files:**
- Modify: `Assets/Scripts/Core/BreedingRules.cs`（`PairingProblem` の最後に `Listed`）
- Modify: `Assets/Scripts/Core/BreedingService.cs`（`CheckPair`・`CheckCandidate`）
- Modify: `Assets/Scripts/Core/ShopService.cs`（`ShopResult` の最後に `Listed`、`Wholesale`）
- Modify: `Assets/Scripts/UI/BreedingText.cs`（`ProblemLabel`）・`Assets/Scripts/UI/ShopText.cs`（`FailureMessage`）
- Test: `Assets/Tests/EditMode/EventLockTests.cs`

**Produces:** 出品中（確定前の回に出品している）個体はペアリングに使えず、卸売り・トレイの「売る」もできない（★C・★10・計画者の判断24）。理由の文は「出品中です」「出品中の個体は売れません」。

- [ ] **Step 1: テストを書く**

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;

namespace TerrariumDays.Tests
{
    public sealed class EventLockTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly GameCalendar calendar = new GameCalendar(Epoch);

        private static PetState Adult(Sex sex) =>
            new PetState
            {
                Name = sex == Sex.Male ? "タロウ" : "ハナ",
                Sex = sex,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                HatchedAtUtc = Epoch.AddDays(-12),
                Personality = Personality.Calm,
                PersonalityKnown = true,
                LastSavedAtUtc = Epoch,
            };

        private static Colony Room(out PetState male, out PetState female)
        {
            var colony = new Colony { CalendarEpochUtc = Epoch };
            male = colony.AddAnimal(Adult(Sex.Male), colony.AddCage(CageSize.Standard));
            female = colony.AddAnimal(Adult(Sex.Female), colony.AddCage(CageSize.Standard));
            return colony;
        }

        private static void List(Colony colony, PetState pet)
        {
            colony.Events.Entry = new EventEntry { Number = 0, FeePaid = 10000 };
            colony.Events.Entry.Listings.Add(new EventListing { AnimalId = pet.Id, Price = 5000 });
        }

        [Test]
        public void ListedAnimals_CannotPair()
        {
            var breeding = new BreedingService(care);
            var colony = Room(out var male, out var female);
            Assert.That(breeding.CheckPair(colony, male, female, Epoch, calendar), Is.EqualTo(PairingProblem.None));

            List(colony, male);
            Assert.That(breeding.CheckPair(colony, male, female, Epoch, calendar), Is.EqualTo(PairingProblem.Listed));
            Assert.That(breeding.CheckCandidate(colony, male, Epoch, calendar), Is.EqualTo(PairingProblem.Listed));
            Assert.That(breeding.CheckCandidate(colony, female, Epoch, calendar), Is.EqualTo(PairingProblem.None));
            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar), Is.EqualTo(PairingProblem.Listed));
            Assert.That(colony.Pairings, Is.Empty);

            List(colony, female);
            Assert.That(breeding.CheckPair(colony, male, female, Epoch, calendar), Is.EqualTo(PairingProblem.Listed));
        }

        [Test]
        public void ListedAnimals_CannotBeSoldToTheShop()
        {
            var shop = new ShopService(new EconomyTuning(), care);
            var colony = Room(out var male, out var female);
            List(colony, male);

            Assert.That(shop.Wholesale(colony, male.Id, Epoch), Is.EqualTo(ShopResult.Listed));
            Assert.That(colony.AnimalById(male.Id), Is.SameAs(male));
            Assert.That(shop.Wholesale(colony, female.Id, Epoch), Is.EqualTo(ShopResult.Ok));
        }

        [Test]
        public void TheLock_EndsWithTheEvent()
        {
            var breeding = new BreedingService(care);
            var colony = Room(out var male, out var female);
            List(colony, male);

            colony.Events.Entry = null;

            Assert.That(breeding.CheckPair(colony, male, female, Epoch, calendar), Is.EqualTo(PairingProblem.None));
        }

        [Test]
        public void Texts_ForTheLock()
        {
            Assert.That(BreedingText.ProblemLabel(PairingProblem.Listed), Is.EqualTo("出品中です"));
            Assert.That(ShopText.FailureMessage(ShopResult.Listed), Is.EqualTo("出品中の個体は売れません"));
        }
    }
}
```

`ShopService` のコンストラクタ・`colony.Pairings` の名前が違ったら、既存の `ShopServiceTests`・`BreedingServiceTests` の作り方に合わせる（中身は変えない）。

- [ ] **Step 2: 列挙を足す**

`BreedingRules.cs` の `PairingProblem` の `AlreadyPairing` の後に `,` と `Listed` を足す（最後に足す。計画者の判断18）。`ShopService.cs` の `ShopResult` の `InPairing` の後に `,` と `Listed` を足す。

- [ ] **Step 3: 判定を足す**

`BreedingService.CheckPair` の、メスの `Gravid` の判定（`return PairingProblem.Gravid;` の `}`）の直後、`AlreadyPairing` の `return` の前に：

```csharp
            if (colony.Events.IsListed(male.Id) || colony.Events.IsListed(female.Id))
            {
                return PairingProblem.Listed;
            }
```

`BreedingService.CheckCandidate` の `pet.Gravid` の判定の直後、`colony.PairingOf(pet) != null` の判定の前に：

```csharp
            if (colony.Events.IsListed(pet.Id))
            {
                return PairingProblem.Listed;
            }
```

`ShopService.Wholesale` の `InPairing` の判定の直後、`var pay = ...` の前に：

```csharp
            if (colony.Events.IsListed(pet.Id))
            {
                return ShopResult.Listed;
            }
```

- [ ] **Step 4: 文を足す**

`BreedingText.ProblemLabel` の `case PairingProblem.AlreadyPairing:` の2行の後に：

```csharp
                case PairingProblem.Listed:
                    return "出品中です";
```

`ShopText.FailureMessage` の `case ShopResult.InPairing:` の2行の後に：

```csharp
                case ShopResult.Listed:
                    return "出品中の個体は売れません";
```

- [ ] **Step 5: テストを流す**

`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.EventLockTests` → 全部通る。続けて EditMode 全体を流し、`BreedingServiceTests`・`ShopServiceTests`・`BreedingTextTests` が落ちていないこと（列挙を最後に足したので既存の値は変わらない）。PlayMode はここでは流さなくてよい（Task 14 で画面に出す）。

- [ ] **Step 6: コミット**

```bash
git add Assets/Scripts/Core/BreedingRules.cs Assets/Scripts/Core/BreedingService.cs Assets/Scripts/Core/ShopService.cs Assets/Scripts/UI/BreedingText.cs Assets/Scripts/UI/ShopText.cs Assets/Tests/EditMode/EventLockTests.cs*
git commit -m "Lock listed animals out of pairing and wholesale"
```

---

## Task 9〜16 の書き方

ここからは配線・セーブ・画面の作業なので、完全なコードではなく「何を・どこに・どう確かめるか」を書く。行番号は計画を書いた時点（main 12284bb＋Task 1〜8）の目安で、先に `grep -n` で場所を確かめてから直す。Core の型と API は Task 1〜8 の **Produces** のとおりに使い、名前を変えない。

---

### Task 9: セッションとセーブ（スキーマ4・異常系・デバッグの時計）（programmer）

**Files:**
- Modify: `Assets/Scripts/Core/ColonySession.cs`
- Modify: `Assets/Scripts/Core/ColonySaveData.cs`
- Modify: `Assets/Scripts/Core/ColonySaveService.cs`
- Test: `Assets/Tests/EditMode/ColonySessionTests.cs`・`Assets/Tests/EditMode/ColonySaveServiceTests.cs`（足す）

**Produces（Task 12〜15 が使う）:** `ColonySession.Events`（`EventService`）、`ColonyTickReport.Events`（`EventReport`）、`ColonySession.SkipToNextEventDay()`（`ColonyTickReport` を返す）、スキーマ4のセーブ。

**セッション**
1. `ColonyTickReport`（`ColonySession.cs:6`）に `public EventReport Events { get; set; } = new EventReport();` を足し、`HasEvents` に `|| Events.HasEvents` を足す。
2. `ColonySession` に `private readonly EventService events;` と `public EventService Events => events;` を足す（`breeding`・`incubation` と同じ並び。コンストラクタで `new EventService(care)`）。
3. `ApplyUntil`（`:157`）の最後、`report.Restocked = ...`（`:206`）の後・`return report;` の前に `report.Events = events.Advance(Colony, Calendar, targetUtc);`。個体の時間を進めた後に呼ぶので、閉じている間に0:00を過ぎた回は開いた時点の個体で固定される（計画者の判断16）。
4. `Load()`（`:89`）で `ApplyUntil` の後に `report.Events.PendingNumber = events.PendingNumber(Colony, Calendar, GameNowUtc);`。続きの通知はアプリを開いたときだけ（★9）。
5. `SkipToNextEventDay()`：`var number = Colony.Events.Entry?.Number ?? EventSchedule.NextOpenNumber(Calendar, GameNowUtc);`、`var target = EventSchedule.StartUtc(Calendar, number) + TimeSpan.FromSeconds(1);`、`target <= GameNowUtc` なら空の `ColonyTickReport` を返し、そうでなければ `return SimulateGameTime(target - GameNowUtc);`（★14。既存の `SimulateGameTime`（`:134`）と同じくゲーム時計のずれに加える）。

**セーブ（スキーマ4、計画者の判断22・23）**
1. `ColonySaveService.CurrentSchemaVersion`（`:16`）を4にする。**`:80` の `probe.schemaVersion >= CurrentSchemaVersion` を `>= 3` に変える**。そのままだとスキーマ3のファイルが `MigrateLegacy`（段階1・2用）に回って中身が消える。3→4 はバックアップを作らない（欄を足すだけ）。
2. `ColonySaveData.cs` に足す型（すべて `[Serializable]`、欄は小文字始まり、既存の `*SaveData` と同じ書き方）：
   - `EventStateSaveData`：`hasEntry`、`entry`（`EventEntrySaveData`）、`hasSummary`、`summary`（`EventSummarySaveData`）、`summarySeen`（bool。`hasSummary` が false なら読むときに true 扱い）、`noticeNumberPlusOne`（0＝なし）、`noticeStage`。
   - `EventEntrySaveData`：`number`、`feePaid`、`frozen`、`listingSeed`、`nextOfferId`、`listings`（`EventListingSaveData` の List）、`actions`（`EventActionSaveData`）、`boothOffers`（`EventBoothOfferSaveData`）、`purchases`（`EventAnimalLineSaveData`）。
   - `EventListingSaveData`：`animalId`、`price`、`tolerancePercent`、`frozenBase`、`frozenChance`、`soldInActionPlusOne`（0＝売れていない）。
   - `EventAnimalLineSaveData`：`animalId`、`name`、`morphLabel`、`amount`、`minuteOfDay`。
   - `EventActionSaveData`：`index`、`kind`（string。`EventActionKind` の名前）、`playedAtUtc`（既存の時刻の欄と同じ書き方）、`boothMinuteOfDay`、`sales`。
   - `EventBoothOfferSaveData`：`offerId`、`actionIndex`、`hasAnimal`、`animal`（`AnimalSaveData`）、`morphLabel`、`price`、`rare`、`bought`。
   - `EventSummarySaveData`：`number`、`boothFee`、`sold`・`unsold`・`bought`（`EventAnimalLineSaveData` の List）。合計と差し引きは `EventSummary` が計算する（`TotalSales`・`PurchaseTotal`・`Net`）ので保存しない。
   - `ColonySaveData`（`:8`）に `public EventStateSaveData events = new EventStateSaveData();`。
3. 書き出し：`ToSaveData`（`:529` 付近の `schemaVersion = CurrentSchemaVersion` の近く）で `events` を作る。ブースの個体は `ToAnimalSaveData`（`:680`）で書く（ショップの売り物 `:594` と同じ）。
4. 読み込み：`FromSaveData`（`:185`）の最後で `colony.Events` を作る。ブースの個体は `FromAnimalSaveData(a, nowUtc, random, owned: false)`（`:469`、ショップ `:320` と同じ）。旧ファイル（スキーマ3）は `events` が空の既定値になり、`hasEntry`＝false・`noticeNumberPlusOne`＝0 で「申し込みなし」に読める。
5. 異常系（計画者の判断23。例外を出さずにその部分だけ捨てて読み込む）：
   - `entry.number < 0` → 申し込みを捨てる（`Entry = null`）。
   - 固定前（`frozen`＝false）の出品：個体がいない・同じ個体の重複・価格が `EventService.MinPrice` 未満・`EventOdds.IsValidTolerance` が false・`MaxListings` を超えた分 → その出品を取り消す。固定後の出品は売れた個体がもういないので個体の有無で捨てない。
   - `actions` が `ActionCount` を超えた分は切り捨て。`kind` が読めなければ `Serve`。`index` は並び順で振り直す。
   - `hasAnimal`＝false か個体が読めないブースの売り物は、`bought`＝true のもの以外捨てる。`nextOfferId` は残った売り物の最大の番号＋1 より小さければ直す（最低1）。
   - `noticeStage` は0〜2に丸める。

**テスト（足すもの。既存のテストの書き方に合わせる）**
- `ColonySaveServiceTests`：申し込み（固定前・出品2件）／当日（固定・行動2回・ブースの売り物1件と買った1件）／結果だけ（`LastSummary` と未読）の3つの往復で中身が同じ。スキーマ3のファイル（既存のテストのスキーマ3の JSON を使う）が `MigrateLegacy` を通らずに読めて `Events.Entry` が null・バックアップが作られない。異常系：番号−1、価格0の出品、重複、許容幅3、行動が10回、`kind`＝"???"、個体のないブースの売り物。
- `ColonySessionTests`：①申し込んでから `SkipToNextEventDay` → `report.Events.DayStartedNumber == 番号`・固定済み。②申し込まずに `SkipToNextEventDay` → 受付中の回の0:00の1秒後。③申し込み済みで開催日の終わりを過ぎてから `Load` → `report.Events.PendingNumber == 番号`。④保存して読み直したあと同じ選択で `PlayAction` すると読み直す前と同じ結果（Task 6 の再現性がセーブを通っても保たれる）。

**確かめ方:** EditMode 全体を流す（624＋Task 1〜9 の分。減っていないこと）。PlayMode も1回流す（セッションの変更で既存の画面テストが落ちていないこと）。

---

### Task 10: 来場者とブースの絵（graphics）

**Files:**
- Create: `tools/sprites/event_sprites.py`（`tools/sprites/candling_sprites.py`・`gecko_sprites.py` と同じ書き方・同じパレットの考え方）
- Create: `Assets/Resources/Event/*.png`（と Unity が作る `.meta`。インポート設定は `Assets/Resources/Candling` の既存の png に合わせる：Point・圧縮なし・Sprite）

**Produces（Task 11・13 が使う）:** 次のファイル名。名前を変えない。
- `booth_bg.png`：自分のブースの背景（机・ケース・のぼり）。横長、画面の上半分に置く。
- `booth_case.png`：机の上の展示ケース1つ（出品の数だけ並べる。売れたケースは Task 13 で「SOLD」の札を重ねる）。
- `sold_tag.png`：売約の札。
- `visitor_a_0.png`・`visitor_a_1.png`、`visitor_b_0/1`、`visitor_c_0/1`：来場者3種類×歩きの2コマ（spec §2.6 の「2〜3種類・2コマ」）。右向きだけ作り、左向きは画面で反転する。
- `bubble.png`：吹き出し（9スライスで伸ばせる形。中の文字は画面が書く）。
- `other_booth.png`：「他のブースを見る」の演出で出す別のブース。

**確かめ方:** スクリプトを流して png ができること。`sips -Z 800` で縮めた一覧（来場者の2コマを並べたもの）を HQ に見せる。ドット絵の線の太さ・輪郭・影の向きを既存の個体・ケージの絵とそろえる。

**コミット:** `git commit -m "Add the event booth and visitor sprites"`

---


### Task 11: イベントタブの枠（準備・当日・結果・買う確認）（graphics）

**Files:**
- Modify: `Assets/UI/Terrarium.uxml`（`placeholder-panel`（`:128`）の前に `events-panel` を足す。ほかのタブの `*-panel` と同じ `class="panel"`）
- Modify: `Assets/UI/Terrarium.uss`
- Modify: `Assets/Scripts/UI/ShellNavigator.cs`（`events-panel` を `Q` して、`ShowTab` で `tab == ShellTab.Events` のとき出す。`hasPanel` に足す。`PlaceholderTextFor` の `ShellTab.Events` の行（「イベントは段階6で追加されます」、`:74`）を消す）
- Modify: `Assets/Tests/EditMode/ShellTests.cs:72`（イベントタブで `placeholder-panel` が出ず `events-panel` が出る、に書き換える）

**Produces（Task 12・13 が `Q` で使う名前。変えない）:**
- `events-panel` の中に3つの面：`event-prep`・`event-day`・`event-result`（同時に1つだけ出す。出し分けは Task 12・13）。
- 準備：`event-prep-title`（回の名前と日付）、`event-prep-info`（出店料・出品の上限・締め切り）、`event-prep-list`（ScrollView。出品できる個体の行がここに入る）、`event-prep-total`（期待値の合計）、`event-prep-count`（「出品 2/5匹」）、`event-prep-register-button`（「申し込む」）、`event-prep-save-button`（「出品を直す」）、`event-prep-message`。行の雛形は USS のクラス `event-listing-row`・`event-listing-check`・`event-listing-name`・`event-listing-price`・`event-listing-minus`・`event-listing-plus`・`event-listing-tolerance`・`event-listing-chance`・`event-listing-reason`（出せない理由）で、行そのものは Task 12 がコードで作る。
- 当日：`event-day-title`、`event-day-left`（「残りの行動 3/8」）、`event-day-stage`（ブースの絵と来場者が動く領域。`booth_bg` を背景に、ケース・来場者・吹き出しは Task 13 が子として足す）、`event-day-log`（ScrollView。画面の下に固定、spec §2.6）、`event-day-serve-button`（「接客する」）、`event-day-browse-button`（「他のブースを見る」）、`event-day-serve-all-button`（「残りを全部接客にする」）、`event-day-skip-button`（「飛ばす」）、`event-day-finish-button`（「集計へ」）、`event-day-offers`（ブースの売り物の行。行は `event-offer-row`・`event-offer-label`・`event-offer-buy`）。
- 結果：`event-result-title`、`event-result-sold`・`event-result-unsold`・`event-result-bought`（それぞれ一覧）、`event-result-totals`（売上・仕入れ・出店料・差し引き）、`event-result-close-button`（「閉じる」）。
- 買う確認：`event-buy-modal`（既存の確認ダイアログとは別。`ConfirmDialog` は「はい」を押せなくできないため）。中に `event-buy-message`、`event-buy-reason`（「ケージが足りません」など）、`event-buy-yes-button`、`event-buy-no-button`。

**見た目:** 縦長の画面で、当日は上から 題・残りの行動 → ブースの絵（画面の約4割）→ 売り物（あるときだけ）→ ボタン2列 → ログ（下に固定、4〜5行見える）。押せないボタンは既存の無効のボタンと同じ見た目。金額の文字は他のタブと同じ大きさ。

**確かめ方:** UXML だけで仮の文字を入れた状態の3面と買う確認を、`ScreenCaptureTests` に `CaptureEventFrames`（`[Explicit]`、既存の Capture と同じ書き方）を足して撮り、`sips -Z 800` で HQ に見せる。EditMode の `ShellTests` を流す。仮の文字はコミット前に UXML から消す（Task 12・13 が入れる）。

**コミット:** `git commit -m "Add the event tab layout"`

---

### Task 12: 準備の画面（programmer、キャプチャあり）

**Files:**
- Create: `Assets/Scripts/UI/EventView.cs`（`IncubatorView`（`Assets/Scripts/UI/IncubatorView.cs`）と同じ形：コンストラクタで `VisualElement root` を受け取り `Q` する、`Invalidate()`、`Render(...)`）
- Modify: `Assets/Scripts/UI/TerrariumView.cs`（`incubatorView = ...`（`:1293`）の近くで `eventView` を作る、`:1495` の近くで `navigator.Tab == ShellTab.Events` のとき `eventView?.Render(session, now)`、`OnTabChanged`（`:645`）で `eventView?.Invalidate()`）
- Test: `Assets/Tests/PlayMode/TerrariumViewTests.cs`（足す）

**中身（spec §2.1・§2.2、★2・★3・★B・★M・★N）:**
- 面の出し分け：`Colony.Events.LastSummary != null && !LastSummarySeen` → 結果（Task 13）。それ以外で `Entry != null && HasStarted(Entry.Number)` → 当日（Task 13）。それ以外 → 準備。
- 準備の対象の回：`Entry != null` ならその回（出品を直す）、なければ `session.Events.OpenNumber(...)`。
- 題：`EventSchedule.NameOf(n)`＋`EventText.DateLabel(n)`。情報：出店料 `EventText.Yen(BoothFee)`・出品の上限 `MaxListings`・締め切り（14日）。申し込み済みなら「申し込み済み（出店料は返りません）」。
- 個体の行（手元の全個体、番号の順）：チェック・名前とモルフ（`EventRules.MorphLabel`）・価格（−／＋で1,000円ずつ、最低1,000円）・値引きの幅（押すたびに 0→5→10→15→20→0%、`EventText.Tolerance`）・`EventText.Chance(session.Events.ChanceOf(...))`。`EventRules.Check` が `None` でない個体はチェックできず、理由（トレイ・抱卵中・ペアリング中・衰弱中）を出す。上限に達したら残りのチェックを押せなくする。
- 価格の初期値は `session.Events.BaseValueOf(...)` を1,000円に丸めた値（最低1,000円）。許容幅の初期値は0%。申し込み済みの回では保存されている出品の値を出す。
- 期待値の合計：チェックした個体の `EventOdds.ExpectedValue(price, tol, chance)` の和を `EventText.ExpectedTotal`。
- 「申し込む」：`ConfirmDialog` で `EventText.ConfirmRegister(n)` → はいで `session.Events.Register(...)`。失敗は `EventText.RegisterFailure` を `event-prep-message` に出す。成功したら保存（`session.Save()`）して描き直す。
- 「出品を直す」（申し込み済みのときだけ出す）：`UpdateListings` → 保存。0匹にもできる（★2）。
- 画面で選んでいる途中の値（チェック・価格・幅）は `EventView` が持ち、`Invalidate` か回が変わったときだけ初期値に戻す。毎フレームの `Render` で消さない。
- 確率は描くたびに計算し直す（0:00 まで変わりうる、★N）。

**テスト（PlayMode、`TerrariumViewTests` の既存の作り方で）:** ①イベントタブで準備の面が出て、回の名前と出店料が出る。②抱卵中の個体はチェックできず「抱卵中」と出る。③2匹チェックして申し込むと所持金が出店料だけ減り `Entry` ができ、題に「申し込み済み」が出る。④＋を押すと価格が1,000円上がり、売れる見込みが下がる。

**キャプチャ:** `ScreenCaptureTests` に `CaptureEventPrep`（申し込み前に3匹チェックした状態・申し込み済み）を足し、`sips -Z 800` で HQ に見せる。

**コミット:** `git commit -m "Add the event preparation screen"`
