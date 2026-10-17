# 段階5：孵卵器・孵化・家系図 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 産卵床・床置きの卵を手動で孵卵器へ移し、設定温度（24〜34℃）と湿度を管理して孵化させる。孵卵器は機種ごとに実際の温度の揺れ方が違い、湿度はゲーム内1日2%下がる。湿度・高温・低温で卵が死ぬことがある。卵はキャンドリングで段階が分かる。発生100%の有精卵は孵化し、中盤3分の1の平均温度で雌雄が決まり、ベビーは孵化トレイ（ケージに入っていない個体）に入る。孵化から1か月は世話なしでも弱らない。トレイから「ケージへ移す」「売る」。台帳に両親の列と3世代の家系図を加え、手放した個体は家系の記録に残す。あわせて、スプライトキャッシュの上限とセーブ読み込みの異常系テスト（持ち越し）を片付ける。

**Architecture:** 孵卵器の状態（`Incubator`）、実温・失敗の数値（`IncubatorRules`）、孵卵器の操作と時間の適用（`IncubatorService`・`IncubationReport`）、キャンドリングと雌雄（`Candling`・`HatchSex`）、孵化（`Hatchery`）、弱らない猶予（`OfflineProgressCalculator`）、家系（`LineageRecord`・`FamilyTree`）、スプライトキャッシュの捨て方（`SpriteCachePolicy`）は、Unity に依存しない Core の純粋なクラスにして EditMode テストで確かめる。`ColonySession.ApplyUntil` が繁殖の後に孵卵器と孵化を進め、`ColonySaveService` がスキーマ3のまま新しいフィールドを保存する（旧 `incubators` の機種一覧から移行）。画面の文字は純粋な `IncubatorText` に分けてテストし、孵卵器タブ（新しい `IncubatorView`）・ケージ詳細・ホーム・台帳（家系図モーダル）・通知に表示する。

**Tech Stack:** Unity 6000.5.10f1（UI Toolkit、JsonUtility、NUnit / Unity Test Framework）、C#、macOS 上の CLI（`scripts/run-unity-tests.sh`、`scripts/test-summary.py`、`scripts/capture-screens.sh`、`scripts/build-ios.sh`）、Python（`tools/sprites`）。

**Spec:** `docs/superpowers/specs/2026-10-17-phase5-incubation-design.md`（正）。親：`docs/superpowers/specs/2026-09-25-breeder-sim-design.md`（§5.1 雌雄の表、§7、§8 孵卵器、§12 セーブ、§13 テスト、§14 段階5）。

## Global Constraints

- ユーザーへの応答は日本語。コード・コメント・コミットメッセージは英語（CLAUDE.md）。
- 孵卵器（§8・spec §2.1）：容量は簡易6・標準12・高級24個。設定温度24〜34℃・1℃刻み。新品と移行分は28℃・湿度80%で始める。実温は、簡易＝室温に引っ張られる（設定との差の10%、最大±2℃）、標準＝設定±0.5℃、高級＝設定どおり。
- 湿度（spec §3）：ゲーム内1日2%下がる。60%未満は1日10%で乾いて死ぬ、95%超は1日10%でカビで死ぬ。「水を足す」は＋15%（上限100%、無料）。適正の目安は70〜90%。
- 温度（spec §3）：実温33℃以上は1日5%、35℃以上は1日15%で死ぬ（奇形は作らない）。24℃未満は段階4の規則（`EggDevelopment` の低温の累計3日で死ぬ）。
- 確率は `BreedingRandom`（再現できる乱数）で、ゲーム内1日単位で判定する。
- 発生日数は段階4の `EggDevelopment.DaysToHatch`・`Apply` をそのまま使う。
- 雌雄（親 §5.1）：中盤3分の1の平均温度で、メスの確率は28℃以下95%・29℃70%・30℃60%・31℃以上25%、間は直線。
- キャンドリング（spec §3）：産卵7日未満「まだ分からない」、無精卵、発生〜33%初期、〜66%中期、〜95%後期、95%超孵化直前、死んだ卵「駄目になった卵」。
- 孵化（spec §2.4）：名前「レオパ番号」、ベビー、雌雄はヤングで判明（既存の脱皮の規則）、性格は7日後に判明（ショップと同じ）、母父の番号、ケージには入れない（孵化トレイ）、1か月の猶予。
- 数値は `CareTuning` にまとめる（既存の書き方：`public double X { get; set; } = 2d;`）。新しい金額はない。
- セーブはスキーマ3のまま（フィールドを追加する）。段階1〜4のファイル、スキーマ1・2の旧ファイルも読めること。JsonUtility の制約（フィールド、辞書なし、旧ファイルにない int は0、string は null）。
- 時間の扱い：ゲーム内の今＝現実の今＋`Colony.GameClockOffset`。世話の値・卵の発生・湿度は1回に最大12時間分（`CareTuning.MaxOfflineProgressHours`）。
- 画面の文字は日本語。マイナス記号は ASCII の `-`。範囲は全角の「〜」。
- 固定の高さの欄は `visibility`、画面（パネル・モーダル）の切り替えは `display`（既存の方針）。
- 画面のタスクは「1画面・4ファイル以内」。指示書の行番号は段階5開始時（HEAD ac2bfc3）の `cs-outline` のもの。前のタスクでずれるので、作業前に `python3 ~/.claude/skills/minimal-read-edit/scripts/cs-outline.py <file> [--grep Word]` で確かめる。
- 画面のタスクの確認は画面キャプチャで行い、**縮小した画像**を見る：`mkdir -p Logs/Screens/small && sips -Z 800 Logs/Screens/<name>.png --out Logs/Screens/small/<name>.png`。報告には縮小画像のパスを書く（原寸を読み込まない）。
- 新しい .cs ファイルの .meta は手書きしない。Unity に生成させる。`scripts/test-summary.py` が「.meta will be ignored」を報告したら失敗扱い。
- ケージに入っていない個体＝孵化トレイの個体（案A）。「どの個体もケージにいる」前提のコード（`CageOf` が null になる）を各タスクで確かめる。

## 計画者の判断（仕様書に書かれていない点）

★はプレイヤーに見える判断（HQ が利用者に確認するための印）。

| # | 判断 | 理由 |
|---|---|---|
| ★1 | 孵卵器の名前は「孵卵器1（簡易）」の形（番号は買った順、1から）。カードの見出しにこれと「3／6個」を出す | 同じ機種を2台持てるので番号が要る。ショップの品名（簡易・標準・高級）と同じ言葉を使う |
| ★2 | 実温の表示は小数1桁（「実温 27.8℃」）。設定温度は整数（「設定 28℃」）。標準機の揺らぎはゲーム内1日ごとに変わる（その日の一様乱数 ±0.5）。簡易機は今の室温で計算する（過去の室温は保存しない） | 揺らぎが毎秒変わると表示がちらつく。判定が1日単位なので実温も1日単位にそろえる |
| ★3 | 温度の −／＋ は1℃ずつ、24℃・34℃で押せなくなる。変えた温度はその時点から効く | spec §3 の範囲。確認ダイアログは出さない（無料で、すぐ戻せるため） |
| ★4 | 湿度は時間に比例して連続的に下がる（0%で止まる）。乾燥・カビ・高温の判定はゲーム内の日付が変わる瞬間に、その時点の湿度と、その日の実温で1回ずつ行う。判定するのは有精で生きている卵だけ（無精卵は「死なない」） | spec の「1日単位で判定」。無精卵の死を出すと、無精卵の判明と区別がつかない |
| ★5 | 死んだ理由は4種：乾燥（湿度不足）・カビ（湿度過多）・高温・低温。通知と卵の説明に理由を出す（「孵卵器1の卵が駄目になりました（カビ）」） | 原因が分からないと設定を直せない |
| ★6 | 湿度の警告は、卵が入っている孵卵器が時間の適用中に60%を下回ったときに1回だけ通知する。カードの湿度バーは60%未満と95%超で警告色。「水を足す」で95%を超えたら、その場で「湿度が95%を超えました。カビに注意してください」と出す | 空の孵卵器の警告は意味がない。毎秒通知しないため、下回った瞬間だけにする |
| ★7 | 「孵卵器へ移す（全部）」は、卵の番号順に、番号の小さい孵卵器の空きから詰める。床置きの卵（乾く前）も救える。結果は「孵卵器へ3個移しました（1個は空きがなく残りました）」。全部入らなければ「孵卵器に空きがありません」。移した卵の発生と中盤3分の1の記録はそのまま引き継ぐ | spec §4.2。番号順にすると結果が予想しやすい |
| ★8 | 産卵床の無精卵は産卵から7日で、産卵床で死んだ卵は死んだ時点で、自動で取り除いて通知する（「ケージ2の産卵床の卵を1個取り除きました（無精卵）」） | 卵が入っている間は産卵床を外せない（段階4）ため、孵らない卵が残ると産卵床が永久に外せなくなる。キャンドリングは孵卵器タブだけ（spec §4.1） |
| ★9 | 孵化はその時間の適用の終わりの時刻に起きる（閉じている間に100%になった卵も、開いたときの時刻で孵化）。産卵床の卵の雌雄は、中盤3分の1の記録が無ければ今の室温、孵卵器の卵は設定温度で決める | 発生は1回最大12時間分なので、孵化の正確な時刻を遡る意味が薄い。記録が無いのはデバッグの卵だけ |
| ★10 | 性格は両親から受け継ぐ（`PersonalityTraits.Inherit`、親のどちらかがもういなければ `Roll`）。判明は孵化から7日後（`PersonalityRevealGameDays`、ショップと同じ通知） | spec「ショップの個体と同じく後から判明」。親 §5.2 の性格の継承を使う |
| ★11 | 猶予中（孵化から30日）は空腹・水分・清潔・健康の値が下がらず、拒食の体重減もない。脱皮・成長・性格の判明は普通に進む。猶予中のトレイの行には「お世話不要（あと12日）」（残り日数は切り上げ）を出す。猶予が終わったトレイの個体は値が下がり始め、トレイの行に「世話が必要です。ケージへ移してください」を出す。一括の世話（えさ・水・掃除）はトレイの個体を含めない | spec 決定2。トレイで世話の操作を作ると「ケージの代わり」になってしまう（案Cを避けた理由と同じ） |
| ★12 | 孵化トレイに定員はない。トレイの「ケージへ移す」は番号の小さい空きケージへ入れる。空きがなければボタンを押せず、行に「空いているケージがありません」 | spec §2.4。ショップのケージで空きを作る導線は既存 |
| ★13 | トレイの「売る」は既存の卸売りの確認ダイアログ（`OnShopWholesaleRequested`）をそのまま使う | spec §2.4 |
| ★14 | ホームの孵卵器ボタンは「孵卵器（卵5・トレイ1）」。卵は孵卵器に入っている卵の数（産卵床の卵は含めない）。両方0なら「孵卵器」だけ | spec §4.2 の例。産卵床の卵はケージ側の印で既に見える |
| ★15 | 台帳の個体一覧にトレイの個体も並べ、場所は「孵化トレイ」。各行に「両親：ハナ×タロウ」（不明なら「両親：不明」）。行をタップすると家系図のモーダルを開き、中に「ケージを見る」（ケージにいる個体だけ）と「閉じる」を置く。**今の「行をタップするとケージが開く」動きは、このボタンに移る** | spec「タップで家系図」。今のタップの意味と衝突するので、ケージへの移動をモーダルのボタンに残す |
| ★16 | 家系図は3段（上：祖父母4人、中：両親、下：本人）。各枠は名前・モルフ名・性別（♀／♂／?）。親が分からない枠は「不明」、手放した個体は記録の名前に「（手放した）」を添える | spec §4.2 の3世代 |
| ★17 | ショップの「（段階Nで使えます）」の表示は、標準・高級孵卵器と産卵床の両方から外す（`usableFromPhase` を0にする） | spec §2.1。産卵床は段階4で使えるようになったのに表示が残っていた |
| ★18 | 通知の文：孵化「レオパ7が孵化しました（孵化トレイ）」、無精卵「孵卵器1の卵が無精卵と分かりました」、死亡（★5）、湿度「孵卵器1の湿度が60%を下回りました」、産卵床（★8）。段階4と同じく、繁殖の通知と合わせて「最初の1件＋（ほか○件）」 | spec §4.2 |
| ★19 | 卵のマスのタップで出す説明は、通知欄ではなくカードの下の1行（「卵12：中期（発生48%）・産卵から21日」）。「取り除く」は無精卵と死んだ卵のマスの下に出し、押すと確認なしで捨てる | 説明の表示先が spec にない。無精卵・死んだ卵は戻す意味がないので確認を省く |
| ★20 | デバッグ「孵卵器に卵を追加」は、空きのある最初の孵卵器に有精卵3個（発生90%、産卵は約54日前、中盤3分の1は設定温度、遺伝子は普通、親は不明）。空きが足りなければ入る分だけ | spec §4.2。親を不明にするのは、家系図の「不明」も同時に確かめられるため |
| 21 | 孵卵器は売れない・外せない。電気代は今までどおり台数で数える | spec にない操作を増やさない |
| 22 | `Incubator` に `AppliedUntilUtc` を持たせ、まだ一度も適用していない（既定値の）孵卵器は最初の適用で今の時刻に合わせるだけにする（湿度を一気に下げない） | ショップで買った直後・移行直後に12時間分の湿度が引かれるのを防ぐ。`BuyItem` に時刻を足さずに済む |
| 23 | 卵の `IncubatorId`：0＝孵卵器の外（旧セーブも0になる）。置き場所 `EggPlace.Incubator` を加える。死因 `EggFailure.Mold`・`Heat` を加える（湿度不足は既存の `Dried`） | JsonUtility の既定値0を「無い」に使える |
| 24 | 個体の `MotherId`・`FatherId`：0＝不明。`CareGraceUntilUtc`：null＝猶予なし | 旧セーブ・ショップの個体は0／null で読める |
| 25 | 家系の記録 `LineageRecord` は spec の4項目（番号・名前・モルフ名・雌雄）に、性別が判明していたかと、その個体の母父の番号を加える | 手放した親の、さらに親（祖父母）を家系図に出すため。性別不明のまま売ったベビーの性別を家系図で明かさないため |
| 26 | 乱数の流れ：`IncubationStream=3`（卵の1日の判定：卵番号・日の番号）、`IncubatorTemperatureStream=4`（標準機の揺らぎ：孵卵器番号・日の番号）、`HatchStream=5`（雌雄と性格：卵番号・0） | 段階4と同じく、処理の順番や途中の保存で結果が変わらない |
| 27 | セーブ：新しい `incubatorStates` が空でなければそれを使い、空なら旧 `incubators`（なければ `incubatorCount`）から28℃・80%で移行する。旧2欄も書き続ける（古いビルドで開いても台数が減らない） | spec §5 |
| 28 | セーブの異常系：孵卵器の機種が読めなければ簡易、設定温度は24〜34℃・湿度は0〜100%に収める、番号が0以下や重複なら読み込み順に振り直す。孵卵器の卵で孵卵器が見つからないものは、空きのある最初の孵卵器へ（なければ1台目に、容量を超えて入れる）。新しい時刻の欄（猶予・孵卵器の適用時刻）は読めなければ null／今として扱い、セーブ全体を壊れた扱いにしない。`lineage` が null なら空 | 段階2からの持ち越し（異常系）。1つの欄の破損でコロニー全体を作り直すのは損失が大きい |
| 29 | スプライトキャッシュ：上限32組（色を変えた `PetSpriteLibrary` とサムネイルを合わせて、見た目のキー単位）。使ったものを新しい順に並べ、上限を超えたら「今画面に出ている個体（ケージ詳細・ホーム・一覧・ショップ・トレイ）のキー」以外の一番古いものから捨て、テクスチャとスプライトを `Destroy` する | spec §6。捨てたものは次に必要になったとき作り直されるだけ |
| 30 | キャンドリングの絵：7種（まだ分からない・無精卵・初期・中期・後期・孵化直前・駄目になった卵）を `tools/sprites` の生成スクリプトで 64×64 の PNG にして `Assets/Resources/Candling/` に置く。光に透かした卵（橙色の楕円）に、血管と胚の影の大きさで段階を描き分ける | 既存の絵と同じ作り方。手描きの素材を増やさない |

## ファイル構成

| ファイル | 種別 | 役割 |
|---|---|---|
| `Assets/Scripts/Core/Incubator.cs` | 新規 | `Incubator`（Task 1） |
| `Assets/Scripts/Core/Colony.cs` | 変更 | `Incubators` の型・`AddIncubator`・`IncubatorById`（Task 1）、`TrayAnimals`・`FirstEmptyCage`（Task 6）、`Lineage`・`RemoveAnimal` の記録（Task 8） |
| `Assets/Scripts/Core/ShopService.cs`・`ShopCatalog.cs` | 変更 | 孵卵器を買う・「段階Nで使えます」を外す（Task 1） |
| `Assets/Scripts/Core/ColonySaveData.cs`・`ColonySaveService.cs` | 変更 | 孵卵器の状態と移行（Task 1）、卵・個体・家系の保存と異常系（Task 11） |
| `Assets/Scripts/Core/IncubatorRules.cs` | 新規 | 容量・設定の範囲・実温・1日の失敗（Task 2） |
| `Assets/Scripts/Core/CareTuning.cs` | 変更 | 孵卵器・孵化の数値（Task 2） |
| `Assets/Scripts/Core/BreedingRandom.cs` | 変更 | 新しい乱数の流れ（Task 2） |
| `Assets/Scripts/Core/BreedingData.cs` | 変更 | `EggFailure.Mold/Heat`（Task 2）、`EggPlace.Incubator`・`Egg.IncubatorId`（Task 3） |
| `Assets/Scripts/Core/IncubatorService.cs` | 新規 | 移す・水・温度・取り除く（Task 3）、時間の適用（Task 4） |
| `Assets/Scripts/Core/IncubationReport.cs` | 新規 | 孵卵器・孵化の出来事（Task 4） |
| `Assets/Scripts/Core/BreedingService.cs` | 変更 | 孵卵器の卵を室温の発生から外す（Task 4） |
| `Assets/Scripts/Core/Candling.cs` | 新規 | `CandlingStage`・`Candling`・`HatchSex`（Task 5） |
| `Assets/Scripts/Core/Hatchery.cs` | 新規 | 孵化・産卵床の後片付け・トレイから移す（Task 6） |
| `Assets/Scripts/Core/PetState.cs` | 変更 | 母父の番号・猶予の時刻（Task 6。孵化で使うため） |
| `Assets/Scripts/Core/OfflineProgressCalculator.cs` | 変更 | 猶予中は世話の値を下げない（Task 7） |
| `Assets/Scripts/Core/FamilyTree.cs` | 新規 | `LineageRecord`・`FamilyMember`・`FamilyTree`（Task 8） |
| `Assets/Scripts/Core/SpriteCachePolicy.cs` | 新規 | キャッシュの捨て方（Task 9） |
| `Assets/Scripts/UI/IncubatorText.cs` | 新規 | 孵卵器・孵化・家系の文字（Task 10） |
| `Assets/Scripts/Core/ColonySession.cs` | 変更 | 孵卵器と孵化の適用・`Incubation` サービス（Task 11） |
| `Assets/Scripts/UI/MorphSprites.cs`・`PetSpriteLibrary.cs` | 変更 | キャッシュの上限（Task 12） |
| `tools/sprites/candling_sprites.py`・`Assets/Resources/Candling/*.png` | 新規 | キャンドリングの絵（Task 13） |
| `Assets/UI/Terrarium.uxml`・`Terrarium.uss`・`Assets/Scripts/UI/ShellNavigator.cs` | 変更 | 孵卵器タブの枠・家系図モーダル（Task 14・18） |
| `Assets/Scripts/UI/IncubatorView.cs` | 新規 | 孵卵器カード（Task 15）、孵化トレイ（Task 16） |
| `Assets/Scripts/UI/TerrariumView.cs` | 変更 | 結線（Task 15〜20） |
| `Assets/Scripts/UI/HomeView.cs` | 変更 | 孵卵器ボタンの件数（Task 17） |
| `Assets/Scripts/UI/LedgerView.cs` | 変更 | 両親の列・トレイの個体・家系図（Task 18） |
| テスト | 新規・変更 | EditMode：`IncubatorRulesTests`・`IncubatorServiceTests`・`CandlingTests`・`HatcheryTests`・`FamilyTreeTests`・`SpriteCachePolicyTests`・`IncubatorTextTests`（新規）、`ColonySaveServiceTests`・`ColonySessionTests`・`OfflineProgressCalculatorTests`・`ShopServiceTests`・`InventoryAndDecorTests`・`ColonyTests`。PlayMode：`TerrariumViewTests`・`ScreenCaptureTests` |
| `GAME.md`・`CLAUDE.md` | 変更 | 孵卵器・孵化・家系の説明（Task 21） |

テストの実行（全タスク共通）：

```bash
scripts/run-unity-tests.sh EditMode && python3 scripts/test-summary.py Logs/EditMode-results.xml
scripts/run-unity-tests.sh PlayMode && python3 scripts/test-summary.py Logs/PlayMode-results.xml
```

特定のテストだけ流すときは `-testFilter <完全名>` を付ける（例：`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.IncubatorServiceTests`）。各タスクの前に全体の件数を控え、タスクの後に減っていないことを確かめる。

## タスクの一覧と作業者

| Task | 内容 | 作業者 |
|---|---|---|
| 1 | 孵卵器を1台ずつの状態にする（型・購入・ショップ表示・セーブの移行） | programmer |
| 2 | 孵卵器の数値（容量・設定の範囲・実温・1日の失敗）と乱数の流れ | transcriber |
| 3 | 卵の置き場所と孵卵器の操作（移す・水・温度・取り除く） | transcriber |
| 4 | 孵卵器の時間の適用（湿度・発生・1日の判定・報告） | transcriber |
| 5 | キャンドリングと雌雄 | transcriber |
| 6 | 孵化・産卵床の後片付け・トレイからケージへ | transcriber |
| 7 | 母父の番号と弱らない猶予 | transcriber |
| 8 | 家系の記録と3世代の家系図 | transcriber |
| 9 | スプライトキャッシュの捨て方（純粋な方針） | transcriber |
| 10 | 孵卵器・孵化・家系の文字（`IncubatorText`） | transcriber |
| 11 | セッションとセーブ（適用の結線・新しい欄・異常系テスト） | programmer |
| 12 | スプライトキャッシュの上限の結線 | programmer |
| 13 | キャンドリングの絵（7種） | graphics |
| 14 | 孵卵器タブの枠（パネル・カードとトレイの見た目） | graphics |
| 15 | 孵卵器カード（温度・湿度・卵のマス・説明・取り除く） | programmer（画面キャプチャで確認） |
| 16 | 孵化トレイ（ケージへ移す・売る・猶予の表示） | programmer（画面キャプチャで確認） |
| 17 | ケージ詳細の「孵卵器へ移す（全部）」とホームの件数 | programmer（画面キャプチャで確認） |
| 18 | 台帳の両親の列と家系図モーダル | programmer（枠は graphics の USS を流用、画面キャプチャで確認） |
| 19 | 通知とデバッグ「孵卵器に卵を追加」 | programmer |
| 20 | PlayMode の1周「産卵 → 孵卵器へ移す → 孵化 → ケージへ移す」（段階の完了条件） | programmer |
| 21 | 文書の更新と最終確認（実機） | programmer（最後に HQ が利用者に確認） |

依存：1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12 → 13 → 14 → 15 → 16 → 17 → 18 → 19 → 20 → 21。順番に行う（1〜10 は Core と純粋な文字、11〜12 で結線、13 以降は画面）。2・5・8・9・10 は互いに独立なので、HQ の判断で順番を入れ替えてよい（どれも 1 の後）。

---

## transcriber タスク共通の前提（Task 2〜10）

計画者は次の点を記憶から書いている。作業の最初に `grep -n` で確かめ、違っていたら**機械的な置き換えだけ**（列挙値の名前、戻り値を変数で受けるか否か）をしてよい。それ以上の変更が要るときは止めて報告する。

- `Colony.AddAnimal(PetState, Cage)` が追加した `PetState` を返す（返さないなら、テストでは先に変数に入れてから渡す）。
- ケージの大きさの列挙が `CageSize.Standard` という名前である（違えば既存の最小のサイズ名に置き換える）。
- `Sex` の値が `Sex.Female` / `Sex.Male`。
- `PetState.SexKnown` は `SexRevealed` から計算される読み取り専用のプロパティで、`SexRevealed = false` なら false。
- `Egg.Failed` は `Failure != EggFailure.None` を返す。


### Task 1: 孵卵器を1台ずつの状態にする（programmer）

**Files:**
- Create: `Assets/Scripts/Core/Incubator.cs`
- Modify: `Assets/Scripts/Core/Colony.cs`（53〜54行の `Incubators`・`IncubatorCount`）
- Modify: `Assets/Scripts/Core/ShopService.cs`（106行 `colony.Incubators.Add(item.Incubator)`）、`Assets/Scripts/Core/ShopCatalog.cs`（18・63・65行、`usableFromPhase`）
- Modify: `Assets/Scripts/Core/ColonySaveData.cs`・`ColonySaveService.cs`（読み込み 197〜210行、書き込み 453・465行）、`Assets/Scripts/Core/ColonySession.cs`（204行の電気代は `IncubatorCount` のままでよい）
- Modify（テストの書き換え）：`ColonyTests.cs:23`、`ColonySaveServiceTests.cs:99/275/294/344/627`、`InventoryAndDecorTests.cs:59/60/143/145`、`ShopServiceTests.cs:170`（すべて `Assets/Tests/EditMode/`）

**Interfaces（後のタスクがこの形を前提にする。名前を変えない）:**

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// One incubator the player owns (phase 5 spec §2.1): model, set temperature, humidity
    /// and how far its eggs have been advanced. Ids start at 1 in purchase order.
    /// </summary>
    public sealed class Incubator
    {
        public int Id { get; set; }

        public IncubatorModel Model { get; set; } = IncubatorModel.Simple;

        /// <summary>Set temperature in whole degrees (24〜34℃, §2.1).</summary>
        public int SetTemperatureC { get; set; } = 28;

        /// <summary>0〜100%. Drops 2% per game day (§3).</summary>
        public double HumidityPercent { get; set; } = 80d;

        /// <summary>Game time its eggs and humidity were advanced to; default = never (first advance only syncs).</summary>
        public DateTimeOffset AppliedUntilUtc { get; set; }
    }
}
```

`Colony` の変更：

```csharp
public List<Incubator> Incubators { get; set; } = new List<Incubator> { new Incubator { Id = 1 } };

public int IncubatorCount => Incubators.Count;

public Incubator IncubatorById(int id) => Incubators.Find(i => i.Id == id);

/// <summary>Adds a new incubator (28℃, 80%) with the next id (max + 1).</summary>
public Incubator AddIncubator(IncubatorModel model)
{
    var nextId = 1;
    foreach (var existing in Incubators)
    {
        nextId = Math.Max(nextId, existing.Id + 1);
    }

    var incubator = new Incubator { Id = nextId, Model = model };
    Incubators.Add(incubator);
    return incubator;
}
```

- [ ] **Step 1: 現状の件数を控える**（EditMode・PlayMode の全体を流し、合格数を報告に書く）。
- [ ] **Step 2: 失敗するテストを書く**（`ColonyTests` に追加）：
  - 新しいコロニーは孵卵器1台（`Id == 1`、`Model == Simple`、28℃、80%、`AppliedUntilUtc == default`）。
  - `AddIncubator(IncubatorModel.Standard)` を2回呼ぶと Id が 2・3 になる。途中の1台を消した後でも Id は最大＋1（重複しない）。
  - `ShopServiceTests`：標準孵卵器を買うと `Incubators[1].Model == Standard`、`Id == 2`、28℃・80%。
  - `ColonySaveServiceTests`：
    - 新形式の往復：孵卵器2台（2台目は標準、31℃、湿度55.5、`AppliedUntilUtc` あり）を保存して読み込むと全部の値が戻る。
    - 旧形式（`incubators` に `["Simple","Luxury"]` だけ、`incubatorStates` 無し）を読むと2台、Id 1・2、どちらも28℃・80%、`AppliedUntilUtc == default`。
    - さらに古い形式（`incubatorCount: 2` だけ）を読むと簡易2台。
    - 保存したファイルに旧2欄（`incubators`・`incubatorCount`）も書かれている（古いビルドで開いても台数が減らない、判断27）。
- [ ] **Step 3: 実装する**。上の `Incubator`・`Colony` の形。`ShopService.BuyItem` の106行は `colony.AddIncubator(item.Incubator)` にする。`ShopCatalog` の標準・高級孵卵器と産卵床の `usableFromPhase` を0にする（判断★17。ショップの文字のテストが「段階Nで使えます」を期待していたら、期待値を外す）。`ShopCatalog` 63・65行で `Incubators` を数えている箇所は `Incubators.Count` / `i.Model` に合わせる。
- [ ] **Step 4: セーブ**。`ColonySaveData` に次を足す（旧2欄は消さない）：

```csharp
[Serializable]
public sealed class IncubatorSaveData
{
    public int id;
    public string model;
    public int setTemperatureC;
    public double humidityPercent;
    public string appliedUntilUtc;
}

// ColonySaveData のフィールド
public List<IncubatorSaveData> incubatorStates = new List<IncubatorSaveData>();
```

  読み込み（197〜210行を置き換え）：`incubatorStates` が null でなく空でなければそれを使う。空なら旧 `incubators`（機種名の一覧）→ それも無ければ `incubatorCount` 台の簡易機から、Id 1,2,… ・28℃・80%・`AppliedUntilUtc = default` で作る。どれも無ければ簡易1台。**この Task では正常な値の読み書きだけ**。範囲外・重複 Id・壊れた時刻などの異常系は Task 11 で扱う（ここで `Parse` が例外を投げてもよいが、`appliedUntilUtc` が null／空なら default にする）。書き込み（453・465行）：`incubatorStates` に全台を書き、旧 `incubators` に機種名、`incubatorCount` に台数を書き続ける。時刻の文字列化は既存の卵の `appliedUntilUtc` と同じ書き方に合わせる（550行付近）。
- [ ] **Step 5: 既存テストの書き換え**。`colony.Incubators` を `IncubatorModel` の一覧として比べている箇所（上の行番号）は `colony.Incubators.Select(i => i.Model)`（`using System.Linq;`）で比べる。期待値の意味は変えない。
- [ ] **Step 6: 全体を流す**。件数が Step 1 より減っていないこと、`.meta will be ignored` が出ていないこと。
- [ ] **Step 7: コミット**

```bash
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Track each incubator's model, temperature and humidity"
```

---

### Task 2: 孵卵器の数値と乱数の流れ（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/IncubatorRules.cs`
- Create: `Assets/Tests/EditMode/QueuedRandom.cs`、`Assets/Tests/EditMode/IncubatorRulesTests.cs`
- Modify: `Assets/Scripts/Core/CareTuning.cs`（85行 `EggColdFailGameDays` の直後に追加）
- Modify: `Assets/Scripts/Core/BreedingRandom.cs`（13行 `ClutchStream` の直後に追加）
- Modify: `Assets/Scripts/Core/BreedingData.cs`（`enum EggFailure` に `Mold`・`Heat` を追加）

作業の前に `grep -rn "class QueuedRandom" Assets/Tests` で同名のクラスが無いことを確かめる（あれば作業を止めて報告）。`grep -rn "EggFailure\." Assets/Scripts` で `switch` が `default` を持たずに全部の値を並べている箇所があれば報告する（コンパイルは通るが表示が抜けるため）。

- [ ] **Step 1: テストを書く**

`Assets/Tests/EditMode/QueuedRandom.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Tests
{
    /// <summary>Random whose NextDouble returns queued values (then 0.999); Next(n) returns 0.</summary>
    public sealed class QueuedRandom : Random
    {
        private readonly Queue<double> values;

        public QueuedRandom(params double[] values)
        {
            this.values = new Queue<double>(values);
        }

        public override double NextDouble() => values.Count > 0 ? values.Dequeue() : 0.999d;

        public override int Next(int maxValue) => 0;
    }
}
```

`Assets/Tests/EditMode/IncubatorRulesTests.cs`：

```csharp
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class IncubatorRulesTests
    {
        private readonly CareTuning care = new CareTuning();

        [TestCase(IncubatorModel.Simple, 6)]
        [TestCase(IncubatorModel.Standard, 12)]
        [TestCase(IncubatorModel.Luxury, 24)]
        public void Capacity_MatchesTheModel(IncubatorModel model, int expected)
        {
            Assert.AreEqual(expected, IncubatorRules.Capacity(model));
        }

        [TestCase(20, 24)]
        [TestCase(24, 24)]
        [TestCase(30, 30)]
        [TestCase(34, 34)]
        [TestCase(40, 34)]
        public void ClampSetting_KeepsTheRange(int input, int expected)
        {
            Assert.AreEqual(expected, IncubatorRules.ClampSetting(input));
        }

        [Test]
        public void ClampHumidity_KeepsZeroToHundred()
        {
            Assert.AreEqual(0d, IncubatorRules.ClampHumidity(-5d));
            Assert.AreEqual(100d, IncubatorRules.ClampHumidity(120d));
            Assert.AreEqual(55.5d, IncubatorRules.ClampHumidity(55.5d));
        }

        [Test]
        public void RealTemperature_Simple_IsPulledTenPercentTowardTheRoom()
        {
            Assert.AreEqual(27.4d, IncubatorRules.RealTemperature(IncubatorModel.Simple, 28, 22d, new QueuedRandom(), care), 1e-9);
        }

        [Test]
        public void RealTemperature_Simple_DriftIsCappedAtTwoDegrees()
        {
            Assert.AreEqual(26d, IncubatorRules.RealTemperature(IncubatorModel.Simple, 28, 0d, new QueuedRandom(), care), 1e-9);
            Assert.AreEqual(30d, IncubatorRules.RealTemperature(IncubatorModel.Simple, 28, 60d, new QueuedRandom(), care), 1e-9);
        }

        [Test]
        public void RealTemperature_Standard_StaysWithinHalfADegree()
        {
            for (var day = 0; day < 100; day++)
            {
                var random = BreedingRandom.For(1234, BreedingRandom.IncubatorTemperatureStream, 1, day);
                var celsius = IncubatorRules.RealTemperature(IncubatorModel.Standard, 28, 10d, random, care);
                Assert.That(celsius, Is.InRange(27.5d, 28.5d));
            }
        }

        [Test]
        public void RealTemperature_Standard_IsTheSameForTheSameDay()
        {
            var first = IncubatorRules.RealTemperature(IncubatorModel.Standard, 28, 22d,
                BreedingRandom.For(99, BreedingRandom.IncubatorTemperatureStream, 2, 5), care);
            var second = IncubatorRules.RealTemperature(IncubatorModel.Standard, 28, 22d,
                BreedingRandom.For(99, BreedingRandom.IncubatorTemperatureStream, 2, 5), care);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void RealTemperature_Luxury_IsExactlyTheSetting()
        {
            Assert.AreEqual(28d, IncubatorRules.RealTemperature(IncubatorModel.Luxury, 28, 10d, new QueuedRandom(0.9d), care));
        }

        [Test]
        public void DailyFailure_LowHumidity_DriesOnALowFirstRoll()
        {
            Assert.AreEqual(EggFailure.Dried, IncubatorRules.DailyFailure(59d, 28d, new QueuedRandom(0.05d, 0.99d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(59d, 28d, new QueuedRandom(0.2d, 0.99d), care));
        }

        [Test]
        public void DailyFailure_HighHumidity_MoldsOnALowFirstRoll()
        {
            Assert.AreEqual(EggFailure.Mold, IncubatorRules.DailyFailure(96d, 28d, new QueuedRandom(0.05d, 0.99d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(96d, 28d, new QueuedRandom(0.2d, 0.99d), care));
        }

        [Test]
        public void DailyFailure_TheEdgesOfTheSafeRangeAreSafe()
        {
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(60d, 28d, new QueuedRandom(0d, 0.99d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(95d, 28d, new QueuedRandom(0d, 0.99d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(80d, 28d, new QueuedRandom(0d, 0.99d), care));
        }

        [Test]
        public void DailyFailure_From33Degrees_FivePercentHeat()
        {
            Assert.AreEqual(EggFailure.Heat, IncubatorRules.DailyFailure(80d, 33d, new QueuedRandom(0.99d, 0.04d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(80d, 33d, new QueuedRandom(0.99d, 0.06d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(80d, 32.9d, new QueuedRandom(0.99d, 0d), care));
        }

        [Test]
        public void DailyFailure_From35Degrees_FifteenPercentHeat()
        {
            Assert.AreEqual(EggFailure.Heat, IncubatorRules.DailyFailure(80d, 35d, new QueuedRandom(0.99d, 0.14d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(80d, 35d, new QueuedRandom(0.99d, 0.16d), care));
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**：`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.IncubatorRulesTests` → コンパイルエラー（`IncubatorRules` が無い）。
- [ ] **Step 3: 実装する**

`CareTuning.cs`（`EggColdFailGameDays` の直後）：

```csharp
        /// <summary>Incubator humidity lost per game day (phase 5 §3).</summary>
        public double IncubatorHumidityLossPerGameDay { get; set; } = 2d;

        /// <summary>Below this humidity an egg may dry out each game day.</summary>
        public double HumidityDryBelowPercent { get; set; } = 60d;

        /// <summary>Above this humidity an egg may go mouldy each game day.</summary>
        public double HumidityMoldAbovePercent { get; set; } = 95d;

        public double HumidityDailyFailChance { get; set; } = 0.10d;

        public double HeatRiskFromC { get; set; } = 33d;

        public double HeatDailyFailChance { get; set; } = 0.05d;

        public double SevereHeatFromC { get; set; } = 35d;

        public double SevereHeatDailyFailChance { get; set; } = 0.15d;

        /// <summary>"Add water" raises humidity by this much (capped at 100%).</summary>
        public double AddWaterPercent { get; set; } = 15d;

        /// <summary>Candling shows anything (and infertile eggs are known) from this egg age.</summary>
        public double CandlingRevealGameDays { get; set; } = 7d;

        /// <summary>Hatchlings' care stats do not drop for this long (phase 5 decision 2).</summary>
        public double HatchlingCareGraceGameDays { get; set; } = 30d;

        /// <summary>A simple incubator drifts this share of (room - set) toward the room.</summary>
        public double SimpleIncubatorRoomPull { get; set; } = 0.1d;

        public double SimpleIncubatorMaxDriftC { get; set; } = 2d;

        /// <summary>A standard incubator wanders up to ± this much, one value per game day.</summary>
        public double StandardIncubatorJitterC { get; set; } = 0.5d;
```

`BreedingRandom.cs`（`ClutchStream` の直後）：

```csharp
        public const int IncubationStream = 3;
        public const int IncubatorTemperatureStream = 4;
        public const int HatchStream = 5;
```

`BreedingData.cs` の `EggFailure`：

```csharp
    public enum EggFailure
    {
        None,
        Dried,
        Cold,
        Mold,
        Heat
    }
```

`Assets/Scripts/Core/IncubatorRules.cs`：

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Incubator numbers (phase 5 spec §2.1, §3): capacity, the setting range, the real
    /// temperature each model holds, and the once-per-game-day egg failure check.
    /// </summary>
    public static class IncubatorRules
    {
        public const int MinSetC = 24;
        public const int MaxSetC = 34;
        public const int DefaultSetC = 28;
        public const double DefaultHumidityPercent = 80d;
        public const double MaxHumidityPercent = 100d;

        public static int Capacity(IncubatorModel model)
        {
            switch (model)
            {
                case IncubatorModel.Standard:
                    return 12;
                case IncubatorModel.Luxury:
                    return 24;
                default:
                    return 6;
            }
        }

        public static int ClampSetting(int celsius) => Math.Max(MinSetC, Math.Min(MaxSetC, celsius));

        public static double ClampHumidity(double percent) => Math.Max(0d, Math.Min(MaxHumidityPercent, percent));

        /// <summary>
        /// The temperature the eggs actually get. Simple: pulled toward the room (10%, at most ±2℃).
        /// Standard: set ± 0.5℃ (one draw from <paramref name="random"/>, seeded per game day).
        /// Luxury: exactly the setting.
        /// </summary>
        public static double RealTemperature(IncubatorModel model, int setC, double roomC, Random random, CareTuning care)
        {
            switch (model)
            {
                case IncubatorModel.Simple:
                    var drift = (roomC - setC) * care.SimpleIncubatorRoomPull;
                    drift = Math.Max(-care.SimpleIncubatorMaxDriftC, Math.Min(care.SimpleIncubatorMaxDriftC, drift));
                    return setC + drift;
                case IncubatorModel.Standard:
                    return setC + BreedingRandom.Uniform(random, -care.StandardIncubatorJitterC, care.StandardIncubatorJitterC);
                default:
                    return setC;
            }
        }

        /// <summary>
        /// One game day's check for one live fertile egg. Always draws twice (humidity, then heat)
        /// so the stream stays aligned whatever the conditions.
        /// </summary>
        public static EggFailure DailyFailure(double humidityPercent, double realC, Random random, CareTuning care)
        {
            var humidityRoll = random.NextDouble();
            var heatRoll = random.NextDouble();

            if (humidityPercent < care.HumidityDryBelowPercent && humidityRoll < care.HumidityDailyFailChance)
            {
                return EggFailure.Dried;
            }

            if (humidityPercent > care.HumidityMoldAbovePercent && humidityRoll < care.HumidityDailyFailChance)
            {
                return EggFailure.Mold;
            }

            var heatChance = realC >= care.SevereHeatFromC ? care.SevereHeatDailyFailChance
                : realC >= care.HeatRiskFromC ? care.HeatDailyFailChance
                : 0d;
            return heatRoll < heatChance ? EggFailure.Heat : EggFailure.None;
        }
    }
}
```

- [ ] **Step 4: 合格を確かめる**：同じフィルタで全部合格。続けて EditMode 全体を流し、件数が減っていないこと。
- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/IncubatorRules.cs Assets/Scripts/Core/CareTuning.cs Assets/Scripts/Core/BreedingRandom.cs Assets/Scripts/Core/BreedingData.cs Assets/Tests/EditMode/QueuedRandom.cs Assets/Tests/EditMode/IncubatorRulesTests.cs
git commit -m "Add incubator rules: capacity, real temperature and daily egg failure"
```

（.meta は Unity が生成したものを一緒に `git add` する。`git status` で確かめる。）

---

### Task 3: 卵の置き場所と孵卵器の操作（transcriber）

**Files:**
- Modify: `Assets/Scripts/Core/BreedingData.cs`（`enum EggPlace` に `Incubator`、`Egg` に `IncubatorId`）
- Create: `Assets/Scripts/Core/IncubatorService.cs`
- Create: `Assets/Tests/EditMode/IncubatorServiceTests.cs`

- [ ] **Step 1: テストを書く** `Assets/Tests/EditMode/IncubatorServiceTests.cs`：

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class IncubatorServiceTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Now = Epoch + GameCalendar.RealTimeFor(20);

        private readonly CareTuning care = new CareTuning();
        private IncubatorService service;

        [SetUp]
        public void SetUp()
        {
            service = new IncubatorService(care);
        }

        private static Colony NewColony() => new Colony { CalendarEpochUtc = Epoch };

        private static Egg AddCageEgg(Colony colony, Cage cage, double development = 10d, bool fertile = true)
        {
            var egg = new Egg
            {
                Id = colony.NextEggId++,
                CageId = cage.Id,
                LaidAtUtc = Epoch,
                Place = EggPlace.NestBox,
                Fertile = fertile,
                DevelopmentPercent = development,
                MiddleThirdGameDays = 2d,
                MiddleThirdTemperatureSum = 54d
            };
            colony.Eggs.Add(egg);
            return egg;
        }

        [Test]
        public void MoveAllFromCage_MovesEggsIntoTheFirstIncubator()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            var first = AddCageEgg(colony, cage);
            var second = AddCageEgg(colony, cage);

            var result = service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(2, result.Moved);
            Assert.AreEqual(0, result.Left);
            foreach (var egg in new[] { first, second })
            {
                Assert.AreEqual(EggPlace.Incubator, egg.Place);
                Assert.AreEqual(1, egg.IncubatorId);
                Assert.AreEqual(-1, egg.CageId);
            }

            Assert.IsEmpty(colony.EggsIn(cage));
        }

        [Test]
        public void MoveAllFromCage_KeepsDevelopmentAndTheMiddleThirdRecord()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            var egg = AddCageEgg(colony, cage, 40d);

            service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(40d, egg.DevelopmentPercent);
            Assert.AreEqual(2d, egg.MiddleThirdGameDays);
            Assert.AreEqual(54d, egg.MiddleThirdTemperatureSum);
        }

        [Test]
        public void MoveAllFromCage_LeavesWhatDoesNotFit_LowestEggIdsGoFirst()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            for (var i = 0; i < 8; i++)
            {
                AddCageEgg(colony, cage);
            }

            var result = service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(6, result.Moved);
            Assert.AreEqual(2, result.Left);
            var left = colony.EggsIn(cage);
            Assert.AreEqual(2, left.Count);
            Assert.AreEqual(7, left[0].Id);
            Assert.AreEqual(8, left[1].Id);
        }

        [Test]
        public void MoveAllFromCage_FillsTheNextIncubatorInIdOrder()
        {
            var colony = NewColony();
            colony.AddIncubator(IncubatorModel.Standard);
            var cage = colony.AddCage(CageSize.Standard);
            for (var i = 0; i < 8; i++)
            {
                AddCageEgg(colony, cage);
            }

            var result = service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(8, result.Moved);
            Assert.AreEqual(6, IncubatorService.EggsInIncubator(colony, colony.Incubators[0]).Count);
            Assert.AreEqual(2, IncubatorService.EggsInIncubator(colony, colony.Incubators[1]).Count);
        }

        [Test]
        public void MoveAllFromCage_NoFreeSlot_MovesNothing()
        {
            var colony = NewColony();
            service.AddDebugEggs(colony, Now, 6);
            var cage = colony.AddCage(CageSize.Standard);
            AddCageEgg(colony, cage);

            var result = service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(0, result.Moved);
            Assert.AreEqual(1, result.Left);
        }

        [Test]
        public void MoveAllFromCage_SkipsDeadEggs()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            var dead = AddCageEgg(colony, cage);
            dead.Failure = EggFailure.Cold;

            var result = service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(0, result.Moved);
            Assert.AreEqual(0, result.Left);
            Assert.AreEqual(EggPlace.NestBox, dead.Place);
        }

        [Test]
        public void AddWater_AddsFifteenPercent_CapsAtHundred_AndWarnsAboveNinetyFive()
        {
            var incubator = new Incubator { Id = 1, HumidityPercent = 70d };

            Assert.IsFalse(service.AddWater(incubator));
            Assert.AreEqual(85d, incubator.HumidityPercent, 1e-9);

            Assert.IsTrue(service.AddWater(incubator));
            Assert.AreEqual(100d, incubator.HumidityPercent, 1e-9);
        }

        [Test]
        public void SetTemperature_ClampsToTheRange()
        {
            var incubator = new Incubator { Id = 1 };

            Assert.AreEqual(34, service.SetTemperature(incubator, 35));
            Assert.AreEqual(34, incubator.SetTemperatureC);
            Assert.AreEqual(24, service.SetTemperature(incubator, 10));
            Assert.AreEqual(29, service.SetTemperature(incubator, 29));
        }

        [Test]
        public void RemoveEgg_OnlyKnownInfertileOrDeadEggs()
        {
            var colony = NewColony();
            service.AddDebugEggs(colony, Now, 3);
            var live = colony.Eggs[0];
            var infertile = colony.Eggs[1];
            infertile.Fertile = false;
            infertile.LaidAtUtc = Now - GameCalendar.RealTimeFor(6.9d);
            var dead = colony.Eggs[2];
            dead.Failure = EggFailure.Mold;

            Assert.IsFalse(service.RemoveEgg(colony, live, Now));
            Assert.IsFalse(service.RemoveEgg(colony, infertile, Now), "infertile but not candled yet");
            Assert.IsTrue(service.RemoveEgg(colony, dead, Now));

            infertile.LaidAtUtc = Now - GameCalendar.RealTimeFor(7d);
            Assert.IsTrue(service.RemoveEgg(colony, infertile, Now));
            Assert.AreEqual(1, colony.Eggs.Count);
        }

        [Test]
        public void AddDebugEggs_AddsThreeFertileEggsAtNinetyPercentWithUnknownParents()
        {
            var colony = NewColony();

            Assert.AreEqual(3, service.AddDebugEggs(colony, Now));

            Assert.AreEqual(3, colony.Eggs.Count);
            foreach (var egg in colony.Eggs)
            {
                Assert.IsTrue(egg.Fertile);
                Assert.AreEqual(90d, egg.DevelopmentPercent);
                Assert.AreEqual(0, egg.MotherId);
                Assert.AreEqual(0, egg.FatherId);
                Assert.AreEqual(EggPlace.Incubator, egg.Place);
                Assert.AreEqual(1, egg.IncubatorId);
                Assert.AreEqual(28d, egg.MiddleThirdTemperatureSum / egg.MiddleThirdGameDays, 1e-9);
                Assert.Less(egg.LaidAtUtc, Now);
            }
        }

        [Test]
        public void AddDebugEggs_OnlyFillsTheFreeSlots()
        {
            var colony = NewColony();
            service.AddDebugEggs(colony, Now, 5);

            Assert.AreEqual(1, service.AddDebugEggs(colony, Now));
            Assert.AreEqual(0, service.AddDebugEggs(colony, Now));
            Assert.AreEqual(6, IncubatorService.EggsInIncubator(colony, colony.Incubators[0]).Count);
            Assert.AreEqual(0, IncubatorService.FreeSlots(colony, colony.Incubators[0]));
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**（`-testFilter TerrariumDays.Tests.IncubatorServiceTests` → コンパイルエラー）。
- [ ] **Step 3: 実装する**

`BreedingData.cs`：

```csharp
    public enum EggPlace
    {
        /// <summary>In the cage's nest box: develops at room temperature (§7.7).</summary>
        NestBox,

        /// <summary>On the cage floor (no nest box): dries out after two game days (§7.7).</summary>
        Loose,

        /// <summary>In an incubator (<see cref="Egg.IncubatorId"/>); develops at its real temperature (phase 5).</summary>
        Incubator
    }
```

`Egg` に（`CageId` の直後）：

```csharp
        /// <summary>The incubator holding the egg; 0 = not in an incubator (old saves read 0).</summary>
        public int IncubatorId { get; set; }
```

`Assets/Scripts/Core/IncubatorService.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Result of "move all eggs to the incubator" (phase 5 §4.2).</summary>
    public sealed class EggMoveResult
    {
        public EggMoveResult(int moved, int left)
        {
            Moved = moved;
            Left = left;
        }

        public int Moved { get; }

        /// <summary>Live eggs that stayed in the cage because no incubator had room.</summary>
        public int Left { get; }
    }

    /// <summary>
    /// Incubator actions (phase 5 spec §2.2, §3): moving eggs in, water, temperature,
    /// removing known-bad eggs; and advancing incubators over time (<see cref="Advance"/>).
    /// No Unity dependency.
    /// </summary>
    public sealed partial class IncubatorService
    {
        private readonly CareTuning care;

        public IncubatorService(CareTuning care)
        {
            this.care = care ?? new CareTuning();
        }

        /// <summary>Eggs in this incubator, lowest id first.</summary>
        public static List<Egg> EggsInIncubator(Colony colony, Incubator incubator)
        {
            var eggs = colony.Eggs.FindAll(e => e.Place == EggPlace.Incubator && e.IncubatorId == incubator.Id);
            eggs.Sort((a, b) => a.Id.CompareTo(b.Id));
            return eggs;
        }

        public static int FreeSlots(Colony colony, Incubator incubator) =>
            Math.Max(0, IncubatorRules.Capacity(incubator.Model) - EggsInIncubator(colony, incubator).Count);

        /// <summary>Eggs inside any incubator (the home button count, ★14).</summary>
        public static int EggsInIncubators(Colony colony) => colony.Eggs.FindAll(e => e.Place == EggPlace.Incubator).Count;

        /// <summary>The lowest-id incubator with a free slot, or null.</summary>
        public static Incubator FirstWithFreeSlot(Colony colony)
        {
            Incubator best = null;
            foreach (var incubator in colony.Incubators)
            {
                if (FreeSlots(colony, incubator) > 0 && (best == null || incubator.Id < best.Id))
                {
                    best = incubator;
                }
            }

            return best;
        }

        /// <summary>
        /// Moves the cage's live eggs (nest box or floor) into incubators: lowest egg id first,
        /// into the lowest-id incubator with room (★7). Development and the middle-third
        /// record carry over. Dead eggs stay where they are.
        /// </summary>
        public EggMoveResult MoveAllFromCage(Colony colony, Cage cage)
        {
            var eggs = colony.EggsIn(cage).FindAll(e => !e.Failed);
            eggs.Sort((a, b) => a.Id.CompareTo(b.Id));
            var moved = 0;
            var left = 0;
            foreach (var egg in eggs)
            {
                var target = FirstWithFreeSlot(colony);
                if (target == null)
                {
                    left++;
                    continue;
                }

                egg.Place = EggPlace.Incubator;
                egg.IncubatorId = target.Id;
                egg.CageId = -1;
                moved++;
            }

            return new EggMoveResult(moved, left);
        }

        /// <summary>Adds water (+15%, capped at 100%). Returns true when humidity is now above the mould line (★6).</summary>
        public bool AddWater(Incubator incubator)
        {
            incubator.HumidityPercent = IncubatorRules.ClampHumidity(incubator.HumidityPercent + care.AddWaterPercent);
            return incubator.HumidityPercent > care.HumidityMoldAbovePercent;
        }

        /// <summary>Sets the temperature (clamped to 24〜34℃). Returns the value set.</summary>
        public int SetTemperature(Incubator incubator, int celsius)
        {
            incubator.SetTemperatureC = IncubatorRules.ClampSetting(celsius);
            return incubator.SetTemperatureC;
        }

        /// <summary>An egg can be thrown away once it is dead, or known infertile (7 game days, §3).</summary>
        public bool CanRemove(Egg egg, DateTimeOffset gameNowUtc) =>
            egg != null
            && egg.Place == EggPlace.Incubator
            && (egg.Failed || (!egg.Fertile && GameCalendar.GameDaysBetween(egg.LaidAtUtc, gameNowUtc) >= care.CandlingRevealGameDays));

        public bool RemoveEgg(Colony colony, Egg egg, DateTimeOffset gameNowUtc) =>
            CanRemove(egg, gameNowUtc) && colony.Eggs.Remove(egg);

        /// <summary>
        /// Debug (★20): fertile eggs at 90% development, parents unknown, into the first incubator
        /// with room (only as many as fit). Returns how many were added.
        /// </summary>
        public int AddDebugEggs(Colony colony, DateTimeOffset gameNowUtc, int count = 3)
        {
            var incubator = FirstWithFreeSlot(colony);
            if (incubator == null)
            {
                return 0;
            }

            var added = Math.Min(count, FreeSlots(colony, incubator));
            var daysToHatch = EggDevelopment.DaysToHatch(incubator.SetTemperatureC);
            for (var i = 0; i < added; i++)
            {
                colony.Eggs.Add(new Egg
                {
                    Id = colony.NextEggId++,
                    MotherId = 0,
                    FatherId = 0,
                    CageId = -1,
                    LaidAtUtc = gameNowUtc - GameCalendar.RealTimeFor(daysToHatch * 0.9d),
                    Place = EggPlace.Incubator,
                    IncubatorId = incubator.Id,
                    Fertile = true,
                    ChildGenotype = Genotype.Normal(),
                    ChildKnown = new KnownGenetics(),
                    DevelopmentPercent = 90d,
                    MiddleThirdGameDays = daysToHatch / 3d,
                    MiddleThirdTemperatureSum = incubator.SetTemperatureC * daysToHatch / 3d,
                    AppliedUntilUtc = gameNowUtc
                });
            }

            return added;
        }
    }
}
```

（`partial` にしてあるのは Task 4 で `Advance` を別ファイル `IncubatorService.Advance.cs` に足すため。）

- [ ] **Step 4: 合格を確かめる**（フィルタ → 全体）。
- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/BreedingData.cs Assets/Scripts/Core/IncubatorService.cs* Assets/Tests/EditMode/IncubatorServiceTests.cs*
git commit -m "Add incubator actions: move eggs in, water, temperature, remove"
```

---

### Task 4: 孵卵器の時間の適用（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/IncubationReport.cs`
- Create: `Assets/Scripts/Core/IncubatorService.Advance.cs`
- Modify: `Assets/Scripts/Core/BreedingService.cs`（`AdvanceEggs` のループ）
- Create: `Assets/Tests/EditMode/IncubationAdvanceTests.cs`

- [ ] **Step 1: テストを書く** `Assets/Tests/EditMode/IncubationAdvanceTests.cs`：

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class IncubationAdvanceTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly CareTuning care = new CareTuning();
        private IncubatorService service;

        [SetUp]
        public void SetUp()
        {
            service = new IncubatorService(care);
        }

        private static Colony NewColony(IncubatorModel model = IncubatorModel.Luxury, int setC = 28)
        {
            var colony = new Colony { CalendarEpochUtc = Epoch };
            colony.Incubators[0].Model = model;
            colony.Incubators[0].SetTemperatureC = setC;
            colony.Incubators[0].AppliedUntilUtc = Epoch;
            return colony;
        }

        private static Egg AddEgg(Colony colony, bool fertile = true)
        {
            var egg = new Egg
            {
                Id = colony.NextEggId++,
                CageId = -1,
                LaidAtUtc = Epoch,
                Place = EggPlace.Incubator,
                IncubatorId = colony.Incubators[0].Id,
                Fertile = fertile
            };
            colony.Eggs.Add(egg);
            return egg;
        }

        private static DateTimeOffset AtDay(double gameDays) => Epoch + GameCalendar.RealTimeFor(gameDays);

        [Test]
        public void FirstAdvance_OnlySyncsTheClock()
        {
            var colony = NewColony();
            colony.Incubators[0].AppliedUntilUtc = default;
            var target = AtDay(5);

            var report = service.Advance(colony, target, 22d);

            Assert.AreEqual(target, colony.Incubators[0].AppliedUntilUtc);
            Assert.AreEqual(80d, colony.Incubators[0].HumidityPercent);
            Assert.IsFalse(report.HasEvents);
        }

        [Test]
        public void Advance_AtOrBeforeTheLastTime_DoesNothing()
        {
            var colony = NewColony();
            colony.Incubators[0].AppliedUntilUtc = AtDay(3);

            service.Advance(colony, AtDay(2), 22d);

            Assert.AreEqual(AtDay(3), colony.Incubators[0].AppliedUntilUtc);
            Assert.AreEqual(80d, colony.Incubators[0].HumidityPercent);
        }

        [Test]
        public void Humidity_DropsTwoPercentPerGameDay()
        {
            var colony = NewColony();

            service.Advance(colony, AtDay(5), 22d);

            Assert.AreEqual(70d, colony.Incubators[0].HumidityPercent, 1e-6);
        }

        [Test]
        public void Humidity_OnlyTwelveHoursAreApplied()
        {
            var colony = NewColony();
            var target = Epoch + TimeSpan.FromHours(24);

            service.Advance(colony, target, 22d);

            Assert.AreEqual(80d - 2d * 15d, colony.Incubators[0].HumidityPercent, 1e-6);
            Assert.AreEqual(target, colony.Incubators[0].AppliedUntilUtc);
        }

        [Test]
        public void Humidity_StopsAtZero()
        {
            var colony = NewColony();
            colony.Incubators[0].HumidityPercent = 5d;

            service.Advance(colony, AtDay(5), 22d);

            Assert.AreEqual(0d, colony.Incubators[0].HumidityPercent, 1e-9);
        }

        [Test]
        public void Eggs_DevelopAtTheLuxurySetting()
        {
            var colony = NewColony(IncubatorModel.Luxury, 30);
            var egg = AddEgg(colony);
            var twin = new Egg { Fertile = true, LaidAtUtc = Epoch };
            for (var day = 0; day < 5; day++)
            {
                EggDevelopment.Apply(twin, 1d, 30d, care);
            }

            service.Advance(colony, AtDay(5), 22d);

            Assert.AreEqual(twin.DevelopmentPercent, egg.DevelopmentPercent, 1e-6);
            Assert.Greater(egg.DevelopmentPercent, 0d);
        }

        [Test]
        public void Eggs_InASimpleIncubator_FeelTheRoom()
        {
            var colony = NewColony(IncubatorModel.Simple, 28);
            var egg = AddEgg(colony);
            var twin = new Egg { Fertile = true, LaidAtUtc = Epoch };
            for (var day = 0; day < 5; day++)
            {
                EggDevelopment.Apply(twin, 1d, 27d, care);
            }

            service.Advance(colony, AtDay(5), 18d);

            Assert.AreEqual(twin.DevelopmentPercent, egg.DevelopmentPercent, 1e-6);
        }

        [Test]
        public void DryIncubator_KillsEggsAndReportsThem()
        {
            var colony = NewColony();
            colony.Incubators[0].HumidityPercent = 0d;
            for (var i = 0; i < 6; i++)
            {
                AddEgg(colony);
            }

            var report = service.Advance(colony, Epoch + TimeSpan.FromHours(12), 22d);

            var dried = colony.Eggs.FindAll(e => e.Failure == EggFailure.Dried);
            Assert.IsNotEmpty(dried);
            Assert.AreEqual(dried.Count, report.EggsDied.Count);
            Assert.IsEmpty(report.HumidityLowIncubatorIds, "it was already dry, so no new warning");
        }

        [Test]
        public void InfertileEggs_NeverDie_AndAreRevealedAtSevenDays()
        {
            var colony = NewColony();
            colony.Incubators[0].HumidityPercent = 0d;
            colony.Incubators[0].AppliedUntilUtc = AtDay(6);
            var egg = AddEgg(colony, fertile: false);

            var report = service.Advance(colony, AtDay(8), 22d);

            Assert.AreEqual(EggFailure.None, egg.Failure);
            Assert.AreEqual(0d, egg.DevelopmentPercent);
            CollectionAssert.Contains(report.InfertileRevealed, egg);

            var later = service.Advance(colony, AtDay(9), 22d);
            Assert.IsEmpty(later.InfertileRevealed);
        }

        [Test]
        public void HumidityLow_IsReportedOnceWhenItCrossesSixty()
        {
            var colony = NewColony();
            colony.Incubators[0].HumidityPercent = 61d;
            AddEgg(colony);

            var first = service.Advance(colony, AtDay(1), 22d);
            var second = service.Advance(colony, AtDay(2), 22d);

            CollectionAssert.AreEqual(new[] { 1 }, first.HumidityLowIncubatorIds);
            Assert.IsEmpty(second.HumidityLowIncubatorIds);
        }

        [Test]
        public void HumidityLow_IsNotReportedForAnEmptyIncubator()
        {
            var colony = NewColony();
            colony.Incubators[0].HumidityPercent = 61d;

            var report = service.Advance(colony, AtDay(1), 22d);

            Assert.IsEmpty(report.HumidityLowIncubatorIds);
        }

        [Test]
        public void Results_AreTheSameInOneStepOrTwo()
        {
            var oneStep = NewColony(IncubatorModel.Standard, 29);
            var twoSteps = NewColony(IncubatorModel.Standard, 29);
            oneStep.Incubators[0].HumidityPercent = 0d;
            twoSteps.Incubators[0].HumidityPercent = 0d;
            for (var i = 0; i < 6; i++)
            {
                AddEgg(oneStep);
                AddEgg(twoSteps);
            }

            service.Advance(oneStep, Epoch + TimeSpan.FromHours(10), 22d);
            service.Advance(twoSteps, Epoch + TimeSpan.FromHours(3), 22d);
            service.Advance(twoSteps, Epoch + TimeSpan.FromHours(10), 22d);

            for (var i = 0; i < 6; i++)
            {
                Assert.AreEqual(oneStep.Eggs[i].Failure, twoSteps.Eggs[i].Failure, $"egg {i}");
                Assert.AreEqual(oneStep.Eggs[i].DevelopmentPercent, twoSteps.Eggs[i].DevelopmentPercent, 1e-6, $"egg {i}");
            }
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**（`-testFilter TerrariumDays.Tests.IncubationAdvanceTests` → コンパイルエラー）。
- [ ] **Step 3: 実装する**

`Assets/Scripts/Core/IncubationReport.cs`：

```csharp
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>What happened in incubators and nest boxes during one advance (phase 5 §4.2 notices).</summary>
    public sealed class IncubationReport
    {
        /// <summary>Hatchlings, now in the hatchling tray (Task 6).</summary>
        public List<PetState> Hatched { get; } = new List<PetState>();

        /// <summary>Incubator eggs that reached 7 game days and turned out infertile.</summary>
        public List<Egg> InfertileRevealed { get; } = new List<Egg>();

        /// <summary>Incubator eggs that died (Failure says why; IncubatorId says where).</summary>
        public List<Egg> EggsDied { get; } = new List<Egg>();

        /// <summary>Incubators (with eggs) whose humidity fell below 60% during this advance.</summary>
        public List<int> HumidityLowIncubatorIds { get; } = new List<int>();

        /// <summary>Dead or infertile eggs cleared from nest boxes (CageId says where, ★8).</summary>
        public List<Egg> NestBoxEggsRemoved { get; } = new List<Egg>();

        public bool HasEvents =>
            Hatched.Count + InfertileRevealed.Count + EggsDied.Count + HumidityLowIncubatorIds.Count + NestBoxEggsRemoved.Count > 0;
    }
}
```

`Assets/Scripts/Core/IncubatorService.Advance.cs`：

```csharp
using System;

namespace TerrariumDays.Core
{
    public sealed partial class IncubatorService
    {
        /// <summary>
        /// Advances every incubator to <paramref name="targetUtc"/> (game time): humidity falls
        /// continuously, eggs develop at the day's real temperature, and at each game-day boundary
        /// every live fertile egg gets one failure check (★4). At most 12 hours are applied.
        /// An incubator that was never advanced only syncs its clock (decision 22).
        /// </summary>
        public IncubationReport Advance(Colony colony, DateTimeOffset targetUtc, double roomC)
        {
            var report = new IncubationReport();
            var maxSpan = TimeSpan.FromHours(care.MaxOfflineProgressHours);
            var epoch = colony.CalendarEpochUtc;
            var seed = colony.BreedingSeed;

            foreach (var incubator in colony.Incubators)
            {
                if (incubator.AppliedUntilUtc == default)
                {
                    incubator.AppliedUntilUtc = targetUtc;
                    continue;
                }

                if (targetUtc <= incubator.AppliedUntilUtc)
                {
                    continue;
                }

                var previousUtc = incubator.AppliedUntilUtc;
                var cursor = targetUtc - previousUtc > maxSpan ? targetUtc - maxSpan : previousUtc;
                incubator.AppliedUntilUtc = targetUtc;

                var eggs = EggsInIncubator(colony, incubator);
                foreach (var egg in eggs)
                {
                    if (egg.Fertile || egg.Failed)
                    {
                        continue;
                    }

                    var revealUtc = egg.LaidAtUtc + GameCalendar.RealTimeFor(care.CandlingRevealGameDays);
                    if (revealUtc > previousUtc && revealUtc <= targetUtc)
                    {
                        report.InfertileRevealed.Add(egg);
                    }
                }

                var wasHumidEnough = incubator.HumidityPercent >= care.HumidityDryBelowPercent;

                while (cursor < targetUtc)
                {
                    var dayIndex = DayIndexAt(epoch, cursor);
                    var boundary = epoch + GameCalendar.RealTimeFor(dayIndex + 1);
                    var segmentEnd = boundary < targetUtc ? boundary : targetUtc;
                    var gameDays = GameCalendar.GameDaysBetween(cursor, segmentEnd);

                    var temperatureRandom = BreedingRandom.For(seed, BreedingRandom.IncubatorTemperatureStream, incubator.Id, dayIndex);
                    var realC = IncubatorRules.RealTemperature(incubator.Model, incubator.SetTemperatureC, roomC, temperatureRandom, care);

                    incubator.HumidityPercent = Math.Max(0d, incubator.HumidityPercent - care.IncubatorHumidityLossPerGameDay * gameDays);

                    foreach (var egg in eggs)
                    {
                        if (egg.Failed)
                        {
                            continue;
                        }

                        EggDevelopment.Apply(egg, gameDays, realC, care);
                        if (egg.Failed)
                        {
                            report.EggsDied.Add(egg);
                        }
                    }

                    if (boundary <= targetUtc)
                    {
                        foreach (var egg in eggs)
                        {
                            if (!egg.Fertile || egg.Failed)
                            {
                                continue;
                            }

                            var random = BreedingRandom.For(seed, BreedingRandom.IncubationStream, egg.Id, dayIndex);
                            var failure = IncubatorRules.DailyFailure(incubator.HumidityPercent, realC, random, care);
                            if (failure != EggFailure.None)
                            {
                                egg.Failure = failure;
                                report.EggsDied.Add(egg);
                            }
                        }
                    }

                    cursor = segmentEnd;
                }

                if (wasHumidEnough && incubator.HumidityPercent < care.HumidityDryBelowPercent && eggs.Count > 0)
                {
                    report.HumidityLowIncubatorIds.Add(incubator.Id);
                }
            }

            return report;
        }

        /// <summary>Game-day number containing <paramref name="utc"/> (day 0 starts at the epoch), robust to rounding at boundaries.</summary>
        private static int DayIndexAt(DateTimeOffset epochUtc, DateTimeOffset utc)
        {
            var index = (int)Math.Floor(GameCalendar.GameDaysBetween(epochUtc, utc));
            while (epochUtc + GameCalendar.RealTimeFor(index + 1) <= utc)
            {
                index++;
            }

            while (epochUtc + GameCalendar.RealTimeFor(index) > utc)
            {
                index--;
            }

            return index;
        }
    }
}
```

`BreedingService.cs`：`AdvanceEggs` の卵のループ（`cs-outline.py Assets/Scripts/Core/BreedingService.cs --grep AdvanceEggs` で場所を確かめる）の先頭に次の1行を足す。孵卵器の卵は室温で発生させず、床置きの乾燥の判定にもかけない。

```csharp
                if (egg.Place == EggPlace.Incubator)
                {
                    continue;
                }
```

ループが `foreach` でなく `RemoveAll` などで書かれていて1行で足せない場合は、作業を止めて報告する（自分で書き換えない）。

- [ ] **Step 4: 合格を確かめる**（フィルタ → EditMode 全体。`BreedingServiceTests` が全部合格のままであること）。
- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/IncubationReport.cs* Assets/Scripts/Core/IncubatorService.Advance.cs* Assets/Scripts/Core/BreedingService.cs Assets/Tests/EditMode/IncubationAdvanceTests.cs*
git commit -m "Advance incubators over time: humidity, development and daily failure"
```

---

### Task 5: キャンドリングと雌雄（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/Candling.cs`
- Create: `Assets/Tests/EditMode/CandlingTests.cs`

- [ ] **Step 1: テストを書く** `Assets/Tests/EditMode/CandlingTests.cs`：

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class CandlingTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly CareTuning care = new CareTuning();

        private static DateTimeOffset AtDay(double gameDays) => Epoch + GameCalendar.RealTimeFor(gameDays);

        private static Egg NewEgg(bool fertile, double development) =>
            new Egg { LaidAtUtc = Epoch, Fertile = fertile, DevelopmentPercent = development };

        [Test]
        public void BeforeSevenDays_NothingIsKnown()
        {
            Assert.AreEqual(CandlingStage.Unknown, Candling.StageOf(NewEgg(false, 0d), AtDay(6.9d), care));
            Assert.AreEqual(CandlingStage.Unknown, Candling.StageOf(NewEgg(true, 20d), AtDay(6.9d), care));
        }

        [Test]
        public void FromSevenDays_InfertileEggsShow()
        {
            Assert.AreEqual(CandlingStage.Infertile, Candling.StageOf(NewEgg(false, 0d), AtDay(7d), care));
        }

        [TestCase(0d, CandlingStage.Early)]
        [TestCase(33d, CandlingStage.Early)]
        [TestCase(33.1d, CandlingStage.Middle)]
        [TestCase(66d, CandlingStage.Middle)]
        [TestCase(66.1d, CandlingStage.Late)]
        [TestCase(95d, CandlingStage.Late)]
        [TestCase(95.1d, CandlingStage.AboutToHatch)]
        [TestCase(100d, CandlingStage.AboutToHatch)]
        public void FertileEggs_ShowTheirDevelopment(double development, CandlingStage expected)
        {
            Assert.AreEqual(expected, Candling.StageOf(NewEgg(true, development), AtDay(10d), care));
        }

        [Test]
        public void DeadEggs_ShowAsDead_EvenEarly()
        {
            var egg = NewEgg(true, 5d);
            egg.Failure = EggFailure.Mold;

            Assert.AreEqual(CandlingStage.Dead, Candling.StageOf(egg, AtDay(2d), care));
        }

        [TestCase(26d, 0.95d)]
        [TestCase(28d, 0.95d)]
        [TestCase(28.5d, 0.825d)]
        [TestCase(29d, 0.70d)]
        [TestCase(30d, 0.60d)]
        [TestCase(30.5d, 0.425d)]
        [TestCase(31d, 0.25d)]
        [TestCase(33d, 0.25d)]
        public void FemaleChance_FollowsTheTable(double celsius, double expected)
        {
            Assert.AreEqual(expected, HatchSex.FemaleChance(celsius), 1e-9);
        }

        [Test]
        public void MiddleThirdAverage_UsesTheRecord_OrTheFallback()
        {
            var recorded = new Egg { MiddleThirdGameDays = 10d, MiddleThirdTemperatureSum = 305d };
            var unrecorded = new Egg();

            Assert.AreEqual(30.5d, HatchSex.MiddleThirdAverage(recorded, 26d), 1e-9);
            Assert.AreEqual(26d, HatchSex.MiddleThirdAverage(unrecorded, 26d), 1e-9);
        }

        [Test]
        public void Decide_ComparesOneRollWithTheChance()
        {
            var egg = new Egg();

            Assert.AreEqual(Sex.Female, HatchSex.Decide(egg, 28d, new QueuedRandom(0.5d)));
            Assert.AreEqual(Sex.Male, HatchSex.Decide(egg, 31d, new QueuedRandom(0.5d)));
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**（`-testFilter TerrariumDays.Tests.CandlingTests`）。
- [ ] **Step 3: 実装する** `Assets/Scripts/Core/Candling.cs`：

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>What candling shows (phase 5 spec §3).</summary>
    public enum CandlingStage
    {
        Unknown,
        Infertile,
        Early,
        Middle,
        Late,
        AboutToHatch,
        Dead
    }

    public static class Candling
    {
        /// <summary>
        /// Dead eggs always show as dead; otherwise nothing is known before 7 game days;
        /// then infertile, or the development band (≤33 early, ≤66 middle, ≤95 late, above: about to hatch).
        /// </summary>
        public static CandlingStage StageOf(Egg egg, DateTimeOffset gameNowUtc, CareTuning care)
        {
            if (egg.Failed)
            {
                return CandlingStage.Dead;
            }

            if (GameCalendar.GameDaysBetween(egg.LaidAtUtc, gameNowUtc) < care.CandlingRevealGameDays)
            {
                return CandlingStage.Unknown;
            }

            if (!egg.Fertile)
            {
                return CandlingStage.Infertile;
            }

            var development = egg.DevelopmentPercent;
            if (development <= 33d)
            {
                return CandlingStage.Early;
            }

            if (development <= 66d)
            {
                return CandlingStage.Middle;
            }

            return development <= 95d ? CandlingStage.Late : CandlingStage.AboutToHatch;
        }
    }

    /// <summary>Temperature-dependent sex (parent spec §5.1): the middle third's average temperature.</summary>
    public static class HatchSex
    {
        /// <summary>Female chance: ≤28℃ 95%, 29℃ 70%, 30℃ 60%, ≥31℃ 25%, straight lines between.</summary>
        public static double FemaleChance(double celsius)
        {
            if (celsius <= 28d)
            {
                return 0.95d;
            }

            if (celsius <= 29d)
            {
                return Lerp(0.95d, 0.70d, celsius - 28d);
            }

            if (celsius <= 30d)
            {
                return Lerp(0.70d, 0.60d, celsius - 29d);
            }

            if (celsius <= 31d)
            {
                return Lerp(0.60d, 0.25d, celsius - 30d);
            }

            return 0.25d;
        }

        /// <summary>The middle third's average temperature, or <paramref name="fallbackC"/> when nothing was recorded.</summary>
        public static double MiddleThirdAverage(Egg egg, double fallbackC) =>
            egg.MiddleThirdGameDays > 0d ? egg.MiddleThirdTemperatureSum / egg.MiddleThirdGameDays : fallbackC;

        /// <summary>One draw from <paramref name="random"/>.</summary>
        public static Sex Decide(Egg egg, double fallbackC, Random random) =>
            random.NextDouble() < FemaleChance(MiddleThirdAverage(egg, fallbackC)) ? Sex.Female : Sex.Male;

        private static double Lerp(double from, double to, double t) => from + (to - from) * t;
    }
}
```

- [ ] **Step 4: 合格を確かめる**（フィルタ → 全体）。
- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/Candling.cs* Assets/Tests/EditMode/CandlingTests.cs*
git commit -m "Add candling stages and temperature-dependent hatch sex"
```

---

### Task 6: 孵化・産卵床の後片付け・トレイからケージへ（transcriber）

**Files:**
- Modify: `Assets/Scripts/Core/PetState.cs`（`NextShedAtUtc` の後に3つのプロパティ）
- Modify: `Assets/Scripts/Core/Colony.cs`（`OccupiedCages()` の直後に2つのメソッド）
- Create: `Assets/Scripts/Core/Hatchery.cs`
- Create: `Assets/Tests/EditMode/HatcheryTests.cs`

- [ ] **Step 1: テストを書く** `Assets/Tests/EditMode/HatcheryTests.cs`：

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class HatcheryTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Now = Epoch + GameCalendar.RealTimeFor(60);

        private readonly CareTuning care = new CareTuning();
        private Hatchery hatchery;

        [SetUp]
        public void SetUp()
        {
            hatchery = new Hatchery(care);
        }

        private static Colony NewColony() => new Colony { CalendarEpochUtc = Epoch };

        private static Egg AddEgg(Colony colony, EggPlace place, double development, bool fertile = true, double middleC = 0d, int cageId = -1)
        {
            var egg = new Egg
            {
                Id = colony.NextEggId++,
                MotherId = 0,
                FatherId = 0,
                CageId = cageId,
                LaidAtUtc = Epoch,
                Place = place,
                IncubatorId = place == EggPlace.Incubator ? colony.Incubators[0].Id : 0,
                Fertile = fertile,
                DevelopmentPercent = development
            };
            if (middleC > 0d)
            {
                egg.MiddleThirdGameDays = 20d;
                egg.MiddleThirdTemperatureSum = middleC * 20d;
            }

            colony.Eggs.Add(egg);
            return egg;
        }

        [Test]
        public void ADevelopedEgg_HatchesIntoTheTray()
        {
            var colony = NewColony();
            var egg = AddEgg(colony, EggPlace.Incubator, 100d, middleC: 26d);
            egg.MotherId = 41;
            egg.FatherId = 42;
            var report = new IncubationReport();

            hatchery.Advance(colony, Now, 22d, report);

            Assert.AreEqual(1, report.Hatched.Count);
            var pet = report.Hatched[0];
            CollectionAssert.Contains(colony.Animals, pet);
            CollectionAssert.Contains(colony.TrayAnimals(), pet);
            Assert.IsNull(colony.CageOf(pet));
            Assert.AreEqual($"レオパ{pet.Id}", pet.Name);
            Assert.AreEqual(GrowthStage.Baby, pet.Stage);
            Assert.IsFalse(pet.SexRevealed);
            Assert.IsFalse(pet.PersonalityKnown);
            Assert.AreEqual(Now + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays), pet.PersonalityRevealAtUtc);
            Assert.AreEqual(care.HatchlingWeightGrams, pet.WeightGrams, 1e-9);
            Assert.AreEqual(Now, pet.HatchedAtUtc);
            Assert.AreEqual(Now, pet.LastSavedAtUtc);
            Assert.AreEqual(41, pet.MotherId);
            Assert.AreEqual(42, pet.FatherId);
            Assert.AreEqual(Now + GameCalendar.RealTimeFor(care.HatchlingCareGraceGameDays), pet.CareGraceUntilUtc);
            CollectionAssert.DoesNotContain(colony.Eggs, egg);
        }

        [Test]
        public void UndevelopedInfertileOrDeadEggs_DoNotHatch()
        {
            var colony = NewColony();
            AddEgg(colony, EggPlace.Incubator, 99.9d);
            AddEgg(colony, EggPlace.Incubator, 100d, fertile: false);
            var dead = AddEgg(colony, EggPlace.Incubator, 100d);
            dead.Failure = EggFailure.Heat;
            var report = new IncubationReport();

            hatchery.Advance(colony, Now, 22d, report);

            Assert.IsEmpty(report.Hatched);
            Assert.AreEqual(3, colony.Eggs.Count, "incubator eggs are removed by the player, not automatically");
        }

        [Test]
        public void ParentsThatExist_PassOnAPersonality_WithoutErrors()
        {
            var colony = NewColony();
            var mother = colony.AddAnimal(new PetState { Sex = Sex.Female }, null);
            var father = colony.AddAnimal(new PetState { Sex = Sex.Male }, null);
            var egg = AddEgg(colony, EggPlace.Incubator, 100d);
            egg.MotherId = mother.Id;
            egg.FatherId = father.Id;
            var report = new IncubationReport();

            hatchery.Advance(colony, Now, 22d, report);

            Assert.AreEqual(1, report.Hatched.Count);
            Assert.AreEqual(mother.Id, report.Hatched[0].MotherId);
        }

        [Test]
        public void CoolMiddleThird_GivesMostlyFemales()
        {
            var colony = NewColony();
            for (var i = 0; i < 200; i++)
            {
                AddEgg(colony, EggPlace.Incubator, 100d, middleC: 26d);
            }

            var report = new IncubationReport();
            hatchery.Advance(colony, Now, 22d, report);

            Assert.AreEqual(200, report.Hatched.Count);
            Assert.GreaterOrEqual(report.Hatched.FindAll(p => p.Sex == Sex.Female).Count, 170);
        }

        [Test]
        public void NoRecord_InAnIncubator_UsesTheSetTemperature()
        {
            var colony = NewColony();
            colony.Incubators[0].SetTemperatureC = 32;
            for (var i = 0; i < 200; i++)
            {
                AddEgg(colony, EggPlace.Incubator, 100d);
            }

            var report = new IncubationReport();
            hatchery.Advance(colony, Now, 26d, report);

            Assert.LessOrEqual(report.Hatched.FindAll(p => p.Sex == Sex.Female).Count, 80);
        }

        [Test]
        public void NoRecord_InANestBox_UsesTheRoomTemperature()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            for (var i = 0; i < 200; i++)
            {
                AddEgg(colony, EggPlace.NestBox, 100d, cageId: cage.Id);
            }

            var report = new IncubationReport();
            hatchery.Advance(colony, Now, 32d, report);

            Assert.AreEqual(200, report.Hatched.Count);
            Assert.LessOrEqual(report.Hatched.FindAll(p => p.Sex == Sex.Female).Count, 80);
        }

        [Test]
        public void Sex_IsTheSameForTheSameEgg()
        {
            var first = NewColony();
            var second = NewColony();
            for (var i = 0; i < 20; i++)
            {
                AddEgg(first, EggPlace.Incubator, 100d, middleC: 29.5d);
                AddEgg(second, EggPlace.Incubator, 100d, middleC: 29.5d);
            }

            var firstReport = new IncubationReport();
            var secondReport = new IncubationReport();
            hatchery.Advance(first, Now, 22d, firstReport);
            hatchery.Advance(second, Now, 22d, secondReport);

            var firstSexes = new List<Sex>();
            var secondSexes = new List<Sex>();
            firstReport.Hatched.ForEach(p => firstSexes.Add(p.Sex));
            secondReport.Hatched.ForEach(p => secondSexes.Add(p.Sex));
            CollectionAssert.AreEqual(firstSexes, secondSexes);
        }

        [Test]
        public void NestBox_InfertileEggs_AreClearedAtSevenDays()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            var egg = AddEgg(colony, EggPlace.NestBox, 0d, fertile: false, cageId: cage.Id);

            var early = new IncubationReport();
            hatchery.Advance(colony, Epoch + GameCalendar.RealTimeFor(6.9d), 26d, early);
            Assert.IsEmpty(early.NestBoxEggsRemoved);
            CollectionAssert.Contains(colony.Eggs, egg);

            var report = new IncubationReport();
            hatchery.Advance(colony, Epoch + GameCalendar.RealTimeFor(7d), 26d, report);
            CollectionAssert.AreEqual(new[] { egg }, report.NestBoxEggsRemoved);
            CollectionAssert.DoesNotContain(colony.Eggs, egg);
            Assert.AreEqual(cage.Id, egg.CageId, "the report keeps where it was");
        }

        [Test]
        public void NestBox_DeadEggs_AreClearedAtOnce()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            var egg = AddEgg(colony, EggPlace.NestBox, 20d, cageId: cage.Id);
            egg.Failure = EggFailure.Cold;
            var report = new IncubationReport();

            hatchery.Advance(colony, Epoch + GameCalendar.RealTimeFor(3d), 26d, report);

            CollectionAssert.AreEqual(new[] { egg }, report.NestBoxEggsRemoved);
            CollectionAssert.DoesNotContain(colony.Eggs, egg);
        }

        [Test]
        public void MoveToCage_UsesTheLowestEmptyCage()
        {
            var colony = NewColony();
            var occupied = colony.AddCage(CageSize.Standard);
            colony.AddAnimal(new PetState(), occupied);
            var empty = colony.AddCage(CageSize.Standard);
            var hatchling = colony.AddAnimal(new PetState(), null);

            Assert.AreEqual(MoveToCageResult.Ok, Hatchery.MoveToCage(colony, hatchling));
            Assert.AreSame(empty, colony.CageOf(hatchling));
            Assert.IsEmpty(colony.TrayAnimals());
            Assert.AreEqual(MoveToCageResult.NotInTray, Hatchery.MoveToCage(colony, hatchling));
        }

        [Test]
        public void MoveToCage_WithoutAnEmptyCage_Refuses()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            colony.AddAnimal(new PetState(), cage);
            var hatchling = colony.AddAnimal(new PetState(), null);

            Assert.IsNull(colony.FirstEmptyCage());
            Assert.AreEqual(MoveToCageResult.NoEmptyCage, Hatchery.MoveToCage(colony, hatchling));
            CollectionAssert.Contains(colony.TrayAnimals(), hatchling);
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**（`-testFilter TerrariumDays.Tests.HatcheryTests`）。
- [ ] **Step 3: 実装する**

`PetState.cs`（`NextShedAtUtc` の後）：

```csharp
        /// <summary>Mother's animal id; 0 = unknown (shop animals, old saves).</summary>
        public int MotherId { get; set; }

        /// <summary>Father's animal id; 0 = unknown.</summary>
        public int FatherId { get; set; }

        /// <summary>Until this time the care stats do not drop (hatchlings, phase 5 decision 2); null = no grace.</summary>
        public DateTimeOffset? CareGraceUntilUtc { get; set; }
```

`Colony.cs`（`OccupiedCages()` の直後）：

```csharp
        /// <summary>Animals in no cage: the hatchling tray (phase 5 decision 1, plan A).</summary>
        public List<PetState> TrayAnimals() => Animals.FindAll(a => CageOf(a) == null);

        /// <summary>The lowest-id empty cage, or null.</summary>
        public Cage FirstEmptyCage()
        {
            Cage best = null;
            foreach (var cage in Cages)
            {
                if (cage.IsEmpty && (best == null || cage.Id < best.Id))
                {
                    best = cage;
                }
            }

            return best;
        }
```

`Assets/Scripts/Core/Hatchery.cs`：

```csharp
using System;

namespace TerrariumDays.Core
{
    public enum MoveToCageResult
    {
        Ok,
        NoEmptyCage,
        NotInTray
    }

    /// <summary>
    /// Hatching (phase 5 spec §2.4) and nest-box clean-up (★8). Fertile live eggs at 100%
    /// hatch wherever they are into the hatchling tray; sex comes from the middle third.
    /// </summary>
    public sealed class Hatchery
    {
        private readonly CareTuning care;

        public Hatchery(CareTuning care)
        {
            this.care = care ?? new CareTuning();
        }

        public void Advance(Colony colony, DateTimeOffset gameNowUtc, double roomC, IncubationReport report)
        {
            foreach (var egg in colony.Eggs.ToArray())
            {
                if (egg.Fertile && !egg.Failed && egg.DevelopmentPercent >= 100d)
                {
                    report.Hatched.Add(Hatch(colony, egg, gameNowUtc, FallbackTemperature(colony, egg, roomC)));
                    continue;
                }

                if (egg.Place != EggPlace.NestBox)
                {
                    continue;
                }

                var knownInfertile = !egg.Fertile
                    && GameCalendar.GameDaysBetween(egg.LaidAtUtc, gameNowUtc) >= care.CandlingRevealGameDays;
                if (egg.Failed || knownInfertile)
                {
                    colony.Eggs.Remove(egg);
                    report.NestBoxEggsRemoved.Add(egg);
                }
            }
        }

        /// <summary>Turns the egg into a baby in the tray (no cage). The egg is removed.</summary>
        public PetState Hatch(Colony colony, Egg egg, DateTimeOffset gameNowUtc, double fallbackC)
        {
            var random = BreedingRandom.For(colony.BreedingSeed, BreedingRandom.HatchStream, egg.Id, 0);
            var sex = HatchSex.Decide(egg, fallbackC, random);
            var mother = colony.AnimalById(egg.MotherId);
            var father = colony.AnimalById(egg.FatherId);
            var personality = mother != null && father != null
                ? PersonalityTraits.Inherit(mother.Personality, father.Personality, random)
                : PersonalityTraits.Roll(random);

            var pet = new PetState
            {
                Sex = sex,
                SexRevealed = false,
                Personality = personality,
                PersonalityKnown = false,
                PersonalityRevealAtUtc = gameNowUtc + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays),
                Genotype = egg.ChildGenotype,
                Known = egg.ChildKnown,
                HatchedAtUtc = gameNowUtc,
                Stage = GrowthStage.Baby,
                LastSavedAtUtc = gameNowUtc,
                LastShedAtUtc = gameNowUtc,
                NextShedAtUtc = gameNowUtc + SheddingModel.IntervalFor(GrowthStage.Baby, care),
                MotherId = egg.MotherId,
                FatherId = egg.FatherId,
                CareGraceUntilUtc = gameNowUtc + GameCalendar.RealTimeFor(care.HatchlingCareGraceGameDays)
            };
            pet.WeightGrams = care.HatchlingWeightGrams;

            colony.AddAnimal(pet, null);
            pet.Name = $"レオパ{pet.Id}";
            colony.Eggs.Remove(egg);
            return pet;
        }

        /// <summary>Tray → the lowest-id empty cage (★12).</summary>
        public static MoveToCageResult MoveToCage(Colony colony, PetState pet)
        {
            if (pet == null || !colony.Animals.Contains(pet) || colony.CageOf(pet) != null)
            {
                return MoveToCageResult.NotInTray;
            }

            var cage = colony.FirstEmptyCage();
            if (cage == null)
            {
                return MoveToCageResult.NoEmptyCage;
            }

            cage.AnimalId = pet.Id;
            return MoveToCageResult.Ok;
        }

        /// <summary>Sex fallback when no middle third was recorded (★9): the incubator's setting, else the room.</summary>
        private static double FallbackTemperature(Colony colony, Egg egg, double roomC)
        {
            if (egg.Place != EggPlace.Incubator)
            {
                return roomC;
            }

            var incubator = colony.IncubatorById(egg.IncubatorId);
            return incubator != null ? incubator.SetTemperatureC : roomC;
        }
    }
}
```

`colony.Eggs.ToArray()` は `List<T>.ToArray()`（Linq 不要）。

- [ ] **Step 4: 合格を確かめる**（フィルタ → 全体）。
- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/PetState.cs Assets/Scripts/Core/Colony.cs Assets/Scripts/Core/Hatchery.cs* Assets/Tests/EditMode/HatcheryTests.cs*
git commit -m "Hatch developed eggs into the tray and clear dead nest-box eggs"
```

---

### Task 7: 弱らない猶予（transcriber）

**Files:**
- Modify: `Assets/Scripts/Core/OfflineProgressCalculator.cs`（`Apply` のループ、37〜56行）
- Create: `Assets/Tests/EditMode/CareGraceTests.cs`

- [ ] **Step 1: テストを書く** `Assets/Tests/EditMode/CareGraceTests.cs`：

```csharp
using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class CareGraceTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        private OfflineProgressCalculator calculator;

        [SetUp]
        public void SetUp()
        {
            calculator = new OfflineProgressCalculator(new CareTuning());
        }

        private static PetState NewPet() => new PetState
        {
            Stage = GrowthStage.Baby,
            HatchedAtUtc = Epoch,
            LastSavedAtUtc = Epoch,
            LastShedAtUtc = Epoch,
            NextShedAtUtc = Epoch + TimeSpan.FromDays(30)
        };

        [Test]
        public void DuringGrace_CareStatsDoNotDrop()
        {
            var pet = NewPet();
            pet.CareGraceUntilUtc = Epoch + TimeSpan.FromHours(10);
            var hunger = pet.Hunger;
            var hydration = pet.Hydration;
            var cleanliness = pet.Cleanliness;
            var health = pet.Health;

            calculator.Apply(pet, Epoch, Epoch + TimeSpan.FromHours(6));

            Assert.AreEqual(hunger, pet.Hunger, 1e-9);
            Assert.AreEqual(hydration, pet.Hydration, 1e-9);
            Assert.AreEqual(cleanliness, pet.Cleanliness, 1e-9);
            Assert.AreEqual(health, pet.Health, 1e-9);
        }

        [Test]
        public void WithoutGrace_CareStatsDrop()
        {
            var pet = NewPet();
            var hunger = pet.Hunger;

            calculator.Apply(pet, Epoch, Epoch + TimeSpan.FromHours(6));

            Assert.Less(pet.Hunger, hunger);
        }

        [Test]
        public void GraceEndingMidway_OnlyTheRestCounts()
        {
            var control = NewPet();
            var partial = NewPet();
            partial.CareGraceUntilUtc = Epoch + TimeSpan.FromHours(1);
            var hunger = partial.Hunger;

            calculator.Apply(control, Epoch, Epoch + TimeSpan.FromHours(6));
            calculator.Apply(partial, Epoch, Epoch + TimeSpan.FromHours(6));

            Assert.Less(partial.Hunger, hunger);
            Assert.Greater(partial.Hunger, control.Hunger);
        }

        [Test]
        public void DuringGrace_SheddingStillHappens()
        {
            var pet = NewPet();
            pet.CareGraceUntilUtc = Epoch + TimeSpan.FromHours(10);
            pet.NextShedAtUtc = Epoch + TimeSpan.FromHours(1);

            calculator.Apply(pet, Epoch, Epoch + TimeSpan.FromHours(6));

            Assert.Greater(pet.LastShedAtUtc, Epoch);
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**（`-testFilter TerrariumDays.Tests.CareGraceTests` → `DuringGrace_CareStatsDoNotDrop` と `GraceEndingMidway_OnlyTheRestCounts` が失敗）。`WithoutGrace_CareStatsDrop` が最初から失敗する（空腹の初期値が下がらない作り）ときは作業を止めて報告する。
- [ ] **Step 3: 実装する**。`Apply` のループの先頭を次のように変える（`stepEndUtc` の行の直後に `inGrace` を足し、`ApplyStep` と拒食の体重減を `!inGrace` のときだけ行う）：

```csharp
            for (var i = 0; i < stepCount; i++)
            {
                var stepEndUtc = previousUtc + TimeSpan.FromTicks(step.Ticks * (i + 1));
                var inGrace = state.CareGraceUntilUtc.HasValue && stepEndUtc <= state.CareGraceUntilUtc.Value;
                var refusing = AppetiteModel.IsRefusingFood(state, stepEndUtc, tuning);
                if (!inGrace)
                {
                    ApplyStep(state, refusing);
                }

                if (!state.Weak && state.Health <= tuning.WeakHealthThreshold)
                {
                    state.Weak = true;
                }
                else if (state.Weak && state.Health >= tuning.WeakRecoveryHealth)
                {
                    state.Weak = false;
                }

                if (refusing && !inGrace)
                {
                    state.WeightGrams -= tuning.FastingWeightLossPerGameDay * step.TotalMinutes / GameCalendar.RealMinutesPerGameDay;
                }
```

（以降の脱皮・成長の部分は変えない。クラスの summary に「Hatchlings in their care grace (PetState.CareGraceUntilUtc) skip the care-stat decay and fasting weight loss.」を1文足す。）

- [ ] **Step 4: 合格を確かめる**（フィルタ → 全体。`OfflineProgressCalculatorTests` が全部合格のまま）。
- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/OfflineProgressCalculator.cs Assets/Tests/EditMode/CareGraceTests.cs*
git commit -m "Skip care-stat decay during a hatchling's care grace"
```

---

### Task 8: 家系の記録と3世代の家系図（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/FamilyTree.cs`
- Modify: `Assets/Scripts/Core/Colony.cs`（`Lineage` プロパティ、`RemoveAnimal` で記録）
- Create: `Assets/Tests/EditMode/FamilyTreeTests.cs`

- [ ] **Step 1: テストを書く** `Assets/Tests/EditMode/FamilyTreeTests.cs`：

```csharp
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class FamilyTreeTests
    {
        private static PetState Add(Colony colony, string name, Sex sex, int motherId = 0, int fatherId = 0, bool sexKnown = true)
        {
            var pet = colony.AddAnimal(new PetState { Sex = sex, SexRevealed = sexKnown }, null);
            pet.Name = name;
            pet.MotherId = motherId;
            pet.FatherId = fatherId;
            return pet;
        }

        [Test]
        public void ShopAnimal_HasUnknownParentsAndGrandparents()
        {
            var colony = new Colony();
            var pet = Add(colony, "ハナ", Sex.Female);

            var tree = FamilyTree.For(colony, pet);

            Assert.AreEqual("ハナ", tree.Self.Name);
            Assert.IsTrue(tree.Mother.Unknown);
            Assert.IsTrue(tree.Father.Unknown);
            Assert.IsTrue(tree.MaternalGrandmother.Unknown);
            Assert.IsTrue(tree.PaternalGrandfather.Unknown);
        }

        [Test]
        public void ThreeGenerations_AreFilledFromLivingAnimals()
        {
            var colony = new Colony();
            var grandma = Add(colony, "ウメ", Sex.Female);
            var grandpa = Add(colony, "マツ", Sex.Male);
            var mother = Add(colony, "ハナ", Sex.Female, grandma.Id, grandpa.Id);
            var father = Add(colony, "タロウ", Sex.Male);
            var child = Add(colony, "レオパ9", Sex.Female, mother.Id, father.Id, sexKnown: false);

            var tree = FamilyTree.For(colony, child);

            Assert.AreEqual("ハナ", tree.Mother.Name);
            Assert.AreEqual("タロウ", tree.Father.Name);
            Assert.AreEqual("ウメ", tree.MaternalGrandmother.Name);
            Assert.AreEqual("マツ", tree.MaternalGrandfather.Name);
            Assert.IsTrue(tree.PaternalGrandmother.Unknown);
            Assert.IsFalse(tree.Self.SexKnown);
            Assert.IsFalse(tree.Mother.Gone);
            Assert.IsFalse(string.IsNullOrEmpty(tree.Mother.MorphName));
        }

        [Test]
        public void SoldAnimals_StayInTheLineage()
        {
            var colony = new Colony();
            var grandma = Add(colony, "ウメ", Sex.Female);
            var mother = Add(colony, "ハナ", Sex.Female, grandma.Id);
            var child = Add(colony, "レオパ9", Sex.Male, mother.Id);

            colony.RemoveAnimal(grandma);
            colony.RemoveAnimal(mother);
            var tree = FamilyTree.For(colony, child);

            Assert.AreEqual("ハナ", tree.Mother.Name);
            Assert.IsTrue(tree.Mother.Gone);
            Assert.AreEqual("ウメ", tree.MaternalGrandmother.Name, "the record keeps the sold mother's own mother");
            Assert.IsTrue(tree.MaternalGrandmother.Gone);
        }

        [Test]
        public void RemoveAnimal_RecordsNameMorphSexAndParents_Once()
        {
            var colony = new Colony();
            var pet = Add(colony, "ハナ", Sex.Female, 3, 4, sexKnown: false);

            colony.RemoveAnimal(pet);
            colony.RemoveAnimal(pet);

            Assert.AreEqual(1, colony.Lineage.Count);
            var record = colony.Lineage[0];
            Assert.AreEqual(pet.Id, record.Id);
            Assert.AreEqual("ハナ", record.Name);
            Assert.AreEqual(MorphNamer.FullName(pet.Genotype, pet.Known), record.MorphName);
            Assert.AreEqual(Sex.Female, record.Sex);
            Assert.IsFalse(record.SexKnown);
            Assert.AreEqual(3, record.MotherId);
            Assert.AreEqual(4, record.FatherId);
        }

        [Test]
        public void AParentIdWithNoAnimalAndNoRecord_IsUnknown()
        {
            var colony = new Colony();
            var child = Add(colony, "レオパ9", Sex.Male, 77, 0);

            var tree = FamilyTree.For(colony, child);

            Assert.IsTrue(tree.Mother.Unknown);
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**（`-testFilter TerrariumDays.Tests.FamilyTreeTests`）。
- [ ] **Step 3: 実装する**

`Assets/Scripts/Core/FamilyTree.cs`：

```csharp
namespace TerrariumDays.Core
{
    /// <summary>What we remember about an animal that left the room (phase 5 spec §2.5, decision 25).</summary>
    public sealed class LineageRecord
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string MorphName { get; set; } = string.Empty;

        public Sex Sex { get; set; } = Sex.Female;

        public bool SexKnown { get; set; }

        public int MotherId { get; set; }

        public int FatherId { get; set; }
    }

    /// <summary>One box in the family tree.</summary>
    public sealed class FamilyMember
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string MorphName { get; set; } = string.Empty;

        public Sex Sex { get; set; } = Sex.Female;

        public bool SexKnown { get; set; }

        /// <summary>Left the room; known only from the lineage record.</summary>
        public bool Gone { get; set; }

        /// <summary>No information (shop animal's parents, debug eggs).</summary>
        public bool Unknown { get; set; }

        public int MotherId { get; set; }

        public int FatherId { get; set; }

        public static FamilyMember UnknownMember() => new FamilyMember { Unknown = true };
    }

    public sealed class FamilyTreeData
    {
        public FamilyMember Self { get; set; }
        public FamilyMember Mother { get; set; }
        public FamilyMember Father { get; set; }
        public FamilyMember MaternalGrandmother { get; set; }
        public FamilyMember MaternalGrandfather { get; set; }
        public FamilyMember PaternalGrandmother { get; set; }
        public FamilyMember PaternalGrandfather { get; set; }
    }

    /// <summary>Three generations (§4.2): living animals first, then lineage records, else unknown.</summary>
    public static class FamilyTree
    {
        public static FamilyTreeData For(Colony colony, PetState pet)
        {
            var self = FromPet(pet);
            var mother = Member(colony, self.MotherId);
            var father = Member(colony, self.FatherId);
            return new FamilyTreeData
            {
                Self = self,
                Mother = mother,
                Father = father,
                MaternalGrandmother = Member(colony, mother.MotherId),
                MaternalGrandfather = Member(colony, mother.FatherId),
                PaternalGrandmother = Member(colony, father.MotherId),
                PaternalGrandfather = Member(colony, father.FatherId)
            };
        }

        public static FamilyMember Member(Colony colony, int id)
        {
            if (id <= 0)
            {
                return FamilyMember.UnknownMember();
            }

            var pet = colony.AnimalById(id);
            if (pet != null)
            {
                return FromPet(pet);
            }

            var record = colony.Lineage.Find(r => r.Id == id);
            if (record == null)
            {
                return FamilyMember.UnknownMember();
            }

            return new FamilyMember
            {
                Id = record.Id,
                Name = record.Name ?? string.Empty,
                MorphName = record.MorphName ?? string.Empty,
                Sex = record.Sex,
                SexKnown = record.SexKnown,
                Gone = true,
                MotherId = record.MotherId,
                FatherId = record.FatherId
            };
        }

        public static LineageRecord RecordOf(PetState pet) => new LineageRecord
        {
            Id = pet.Id,
            Name = pet.Name,
            MorphName = MorphNamer.FullName(pet.Genotype, pet.Known),
            Sex = pet.Sex,
            SexKnown = pet.SexKnown,
            MotherId = pet.MotherId,
            FatherId = pet.FatherId
        };

        private static FamilyMember FromPet(PetState pet) => new FamilyMember
        {
            Id = pet.Id,
            Name = pet.Name,
            MorphName = MorphNamer.FullName(pet.Genotype, pet.Known),
            Sex = pet.Sex,
            SexKnown = pet.SexKnown,
            MotherId = pet.MotherId,
            FatherId = pet.FatherId
        };
    }
}
```

`Colony.cs`：`Eggs` の行の近くに

```csharp
        /// <summary>Animals that left the room, for the family tree (phase 5 §2.5).</summary>
        public List<LineageRecord> Lineage { get; set; } = new List<LineageRecord>();
```

`RemoveAnimal` の最後の `return true;` の直前に

```csharp
            Lineage.RemoveAll(r => r.Id == pet.Id);
            Lineage.Add(FamilyTree.RecordOf(pet));
```

（2回目の `RemoveAnimal` は `Animals.Remove` が false で早く返るので、記録は1件のまま。）

- [ ] **Step 4: 合格を確かめる**（フィルタ → 全体）。
- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/FamilyTree.cs* Assets/Scripts/Core/Colony.cs Assets/Tests/EditMode/FamilyTreeTests.cs*
git commit -m "Record animals that leave and build a three-generation family tree"
```

---

### Task 9: スプライトキャッシュの捨て方（transcriber）

**Files:**
- Create: `Assets/Scripts/Core/SpriteCachePolicy.cs`
- Create: `Assets/Tests/EditMode/SpriteCachePolicyTests.cs`

- [ ] **Step 1: テストを書く** `Assets/Tests/EditMode/SpriteCachePolicyTests.cs`：

```csharp
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class SpriteCachePolicyTests
    {
        [Test]
        public void UnderCapacity_NothingIsEvicted()
        {
            var policy = new SpriteCachePolicy(3);
            policy.Touch("a");
            policy.Touch("b");

            Assert.IsEmpty(policy.Evict(null));
            Assert.AreEqual(2, policy.Count);
        }

        [Test]
        public void OverCapacity_TheLeastRecentlyUsedGoesFirst()
        {
            var policy = new SpriteCachePolicy(2);
            policy.Touch("a");
            policy.Touch("b");
            policy.Touch("a");
            policy.Touch("c");

            CollectionAssert.AreEqual(new[] { "b" }, policy.Evict(null));
            Assert.IsTrue(policy.Contains("a"));
            Assert.IsTrue(policy.Contains("c"));
            Assert.IsFalse(policy.Contains("b"));
        }

        [Test]
        public void KeysInUse_AreNeverEvicted()
        {
            var policy = new SpriteCachePolicy(1);
            policy.Touch("a");
            policy.Touch("b");
            policy.Touch("c");

            var evicted = policy.Evict(new[] { "a" });

            CollectionAssert.AreEquivalent(new[] { "b" }, evicted);
            Assert.IsTrue(policy.Contains("a"));
            Assert.IsTrue(policy.Contains("c"));
            Assert.AreEqual(2, policy.Count, "stays over capacity rather than dropping a sprite on screen");
        }

        [Test]
        public void EverythingInUse_EvictsNothing()
        {
            var policy = new SpriteCachePolicy(1);
            policy.Touch("a");
            policy.Touch("b");

            Assert.IsEmpty(policy.Evict(new[] { "a", "b" }));
            Assert.AreEqual(2, policy.Count);
        }

        [Test]
        public void Remove_ForgetsAKey()
        {
            var policy = new SpriteCachePolicy(4);
            policy.Touch("a");

            Assert.IsTrue(policy.Remove("a"));
            Assert.IsFalse(policy.Remove("a"));
            Assert.AreEqual(0, policy.Count);
        }

        [Test]
        public void DefaultCapacity_IsThirtyTwo()
        {
            Assert.AreEqual(32, new SpriteCachePolicy().Capacity);
        }
    }
}
```

- [ ] **Step 2: 失敗を確かめる**（`-testFilter TerrariumDays.Tests.SpriteCachePolicyTests`）。
- [ ] **Step 3: 実装する** `Assets/Scripts/Core/SpriteCachePolicy.cs`：

```csharp
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Least-recently-used bookkeeping for the recoloured sprite caches (phase 5 spec §6, decision 29).
    /// Holds keys only; the caller destroys the textures for the keys <see cref="Evict"/> returns.
    /// </summary>
    public sealed class SpriteCachePolicy
    {
        public const int DefaultCapacity = 32;

        private readonly LinkedList<string> order = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> nodes = new Dictionary<string, LinkedListNode<string>>();

        public SpriteCachePolicy(int capacity = DefaultCapacity)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }

        public int Count => nodes.Count;

        public bool Contains(string key) => key != null && nodes.ContainsKey(key);

        /// <summary>Marks the key as just used (adds it when new).</summary>
        public void Touch(string key)
        {
            if (key == null)
            {
                return;
            }

            if (nodes.TryGetValue(key, out var node))
            {
                order.Remove(node);
                order.AddFirst(node);
                return;
            }

            nodes[key] = order.AddFirst(key);
        }

        public bool Remove(string key)
        {
            if (key == null || !nodes.TryGetValue(key, out var node))
            {
                return false;
            }

            order.Remove(node);
            nodes.Remove(key);
            return true;
        }

        /// <summary>
        /// While over capacity, forgets the oldest keys that are not in <paramref name="inUse"/>
        /// and returns them (oldest first). Keys in use are kept even if that leaves it over capacity.
        /// </summary>
        public List<string> Evict(ICollection<string> inUse)
        {
            var evicted = new List<string>();
            var node = order.Last;
            while (nodes.Count > Capacity && node != null)
            {
                var previous = node.Previous;
                if (inUse == null || !inUse.Contains(node.Value))
                {
                    evicted.Add(node.Value);
                    nodes.Remove(node.Value);
                    order.Remove(node);
                }

                node = previous;
            }

            return evicted;
        }
    }
}
```

- [ ] **Step 4: 合格を確かめる**（フィルタ → 全体）。
- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/Core/SpriteCachePolicy.cs* Assets/Tests/EditMode/SpriteCachePolicyTests.cs*
git commit -m "Add an LRU policy for the recoloured sprite caches"
```

---

### Task 10: 孵卵器・孵化・家系の文字（transcriber）

**Files:**
- Create: `Assets/Scripts/UI/IncubatorText.cs`
- Create: `Assets/Tests/EditMode/IncubatorTextTests.cs`

作業の前に、既存の `BreedingText` のテスト（`grep -rln "BreedingText" Assets/Tests/EditMode`）が UI のクラスをどう参照しているか（`using TerrariumDays.UI;`）を確かめ、同じ書き方にする。EditMode のテストから UI のクラスが参照できなければ作業を止めて報告する。

- [ ] **Step 1: テストを書く** `Assets/Tests/EditMode/IncubatorTextTests.cs`：

```csharp
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
```

- [ ] **Step 2: 失敗を確かめる**（`-testFilter TerrariumDays.Tests.IncubatorTextTests`）。
- [ ] **Step 3: 実装する** `Assets/Scripts/UI/IncubatorText.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>Japanese text for the incubator tab, hatchling tray, family tree and notices (phase 5 §4).</summary>
    public static class IncubatorText
    {
        public const string NoEmptyCage = "空いているケージがありません";
        public const string TrayLocation = "孵化トレイ";
        public const string NeedsCare = "世話が必要です。ケージへ移してください";

        public static string ModelLabel(IncubatorModel model)
        {
            switch (model)
            {
                case IncubatorModel.Standard:
                    return "標準";
                case IncubatorModel.Luxury:
                    return "高級";
                default:
                    return "簡易";
            }
        }

        public static string IncubatorName(Incubator incubator) => $"孵卵器{incubator.Id}（{ModelLabel(incubator.Model)}）";

        public static string Occupancy(int count, int capacity) => $"{count}／{capacity}個";

        public static string SetTemperature(int celsius) => $"設定 {celsius}℃";

        public static string RealTemperature(double celsius) =>
            $"実温 {celsius.ToString("0.0", CultureInfo.InvariantCulture)}℃";

        public static string Humidity(double percent) =>
            $"湿度 {(int)Math.Round(percent, MidpointRounding.AwayFromZero)}%";

        public static bool IsHumidityWarning(double percent, CareTuning care) =>
            percent < care.HumidityDryBelowPercent || percent > care.HumidityMoldAbovePercent;

        public static string CandlingLabel(CandlingStage stage)
        {
            switch (stage)
            {
                case CandlingStage.Infertile:
                    return "無精卵";
                case CandlingStage.Early:
                    return "初期";
                case CandlingStage.Middle:
                    return "中期";
                case CandlingStage.Late:
                    return "後期";
                case CandlingStage.AboutToHatch:
                    return "孵化直前";
                case CandlingStage.Dead:
                    return "駄目になった卵";
                default:
                    return "まだ分からない";
            }
        }

        public static string FailureLabel(EggFailure failure)
        {
            switch (failure)
            {
                case EggFailure.Dried:
                    return "乾燥";
                case EggFailure.Mold:
                    return "カビ";
                case EggFailure.Heat:
                    return "高温";
                case EggFailure.Cold:
                    return "低温";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The line under the card when an egg is tapped (★19).</summary>
        public static string EggDetail(Egg egg, CandlingStage stage, DateTimeOffset gameNowUtc)
        {
            var days = Math.Max(0, (int)Math.Floor(GameCalendar.GameDaysBetween(egg.LaidAtUtc, gameNowUtc)));
            string state;
            switch (stage)
            {
                case CandlingStage.Early:
                case CandlingStage.Middle:
                case CandlingStage.Late:
                case CandlingStage.AboutToHatch:
                    var percent = (int)Math.Floor(Math.Min(100d, egg.DevelopmentPercent));
                    state = $"{CandlingLabel(stage)}（発生{percent}%）";
                    break;
                case CandlingStage.Dead:
                    state = $"{CandlingLabel(stage)}（{FailureLabel(egg.Failure)}）";
                    break;
                default:
                    state = CandlingLabel(stage);
                    break;
            }

            return $"卵{egg.Id}：{state}・産卵から{days}日";
        }

        /// <summary>Result of "孵卵器へ移す（全部）" (★7).</summary>
        public static string MoveResult(int moved, int left)
        {
            if (moved == 0)
            {
                return left > 0 ? "孵卵器に空きがありません" : "移す卵がありません";
            }

            return left == 0
                ? $"孵卵器へ{moved}個移しました"
                : $"孵卵器へ{moved}個移しました（{left}個は空きがなく残りました）";
        }

        /// <summary>The home screen's incubator button (★14).</summary>
        public static string HomeButton(int eggsInIncubators, int trayAnimals) =>
            eggsInIncubators == 0 && trayAnimals == 0 ? "孵卵器" : $"孵卵器（卵{eggsInIncubators}・トレイ{trayAnimals}）";

        /// <summary>The ledger row's parents (★15).</summary>
        public static string ParentsLine(FamilyMember mother, FamilyMember father)
        {
            if (mother.Unknown && father.Unknown)
            {
                return "両親：不明";
            }

            return $"両親：{ShortName(mother)}×{ShortName(father)}";
        }

        /// <summary>One family-tree box: name (gone marker), morph, sex mark (★16).</summary>
        public static string TreeBox(FamilyMember member)
        {
            if (member.Unknown)
            {
                return "不明";
            }

            var name = member.Gone ? $"{member.Name}（手放した）" : member.Name;
            return $"{name}\n{member.MorphName}\n{SexMark(member.Sex, member.SexKnown)}";
        }

        public static string SexMark(Sex sex, bool known) => !known ? "?" : sex == Sex.Female ? "♀" : "♂";

        /// <summary>Notices for one advance (★18), in a fixed order.</summary>
        public static List<string> Messages(IncubationReport report, CareTuning care)
        {
            var messages = new List<string>();
            foreach (var pet in report.Hatched)
            {
                messages.Add($"{pet.Name}が孵化しました（{TrayLocation}）");
            }

            foreach (var egg in report.InfertileRevealed)
            {
                messages.Add($"孵卵器{egg.IncubatorId}の卵が無精卵と分かりました");
            }

            foreach (var egg in report.EggsDied)
            {
                messages.Add($"孵卵器{egg.IncubatorId}の卵が駄目になりました（{FailureLabel(egg.Failure)}）");
            }

            foreach (var id in report.HumidityLowIncubatorIds)
            {
                messages.Add($"孵卵器{id}の湿度が{care.HumidityDryBelowPercent.ToString("0", CultureInfo.InvariantCulture)}%を下回りました");
            }

            foreach (var egg in report.NestBoxEggsRemoved)
            {
                var reason = egg.Failed ? FailureLabel(egg.Failure) : "無精卵";
                messages.Add($"ケージ{egg.CageId}の産卵床の卵を1個取り除きました（{reason}）");
            }

            return messages;
        }

        /// <summary>A tray row's title.</summary>
        public static string TrayRow(PetState pet)
        {
            var sex = !pet.SexKnown ? "性別不明" : pet.Sex == Sex.Female ? "メス" : "オス";
            return $"{pet.Name}（{GrowthModel.StageLabel(pet.Stage)}・{sex}）";
        }

        /// <summary>Grace remaining (days rounded up), or the needs-care warning (★11).</summary>
        public static string GraceLine(PetState pet, DateTimeOffset gameNowUtc)
        {
            if (pet.CareGraceUntilUtc.HasValue && gameNowUtc < pet.CareGraceUntilUtc.Value)
            {
                var days = (int)Math.Ceiling(GameCalendar.GameDaysBetween(gameNowUtc, pet.CareGraceUntilUtc.Value));
                return $"お世話不要（あと{days}日）";
            }

            return NeedsCare;
        }

        /// <summary>Shown right after "水を足す" pushes humidity above the mould line (★6).</summary>
        public static string MoldWarning(CareTuning care) =>
            $"湿度が{care.HumidityMoldAbovePercent.ToString("0", CultureInfo.InvariantCulture)}%を超えました。カビに注意してください";

        private static string ShortName(FamilyMember member) => member.Unknown ? "不明" : member.Name;
    }
}
```

- [ ] **Step 4: 合格を確かめる**（フィルタ → 全体）。
- [ ] **Step 5: コミット**

```bash
git add Assets/Scripts/UI/IncubatorText.cs* Assets/Tests/EditMode/IncubatorTextTests.cs*
git commit -m "Add incubator, hatchling and family tree text"
```

---
