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
