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
| ★11 | 猶予中（孵化から30日）は空腹・水分・清潔・健康の値が下がらず、拒食の体重減もない。脱皮・成長・性格の判明は普通に進む。猶予が終わったトレイの個体は値が下がり始め、トレイの行に「世話が必要です。ケージへ移してください」を出す。一括の世話（えさ・水・掃除）はトレイの個体を含めない | spec 決定2。トレイで世話の操作を作ると「ケージの代わり」になってしまう（案Cを避けた理由と同じ） |
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
| `Assets/Scripts/Core/BreedingData.cs` | 変更 | `EggPlace.Incubator`・`EggFailure.Mold/Heat`・`Egg.IncubatorId`（Task 3） |
| `Assets/Scripts/Core/IncubatorService.cs` | 新規 | 移す・水・温度・取り除く（Task 3）、時間の適用（Task 4） |
| `Assets/Scripts/Core/IncubationReport.cs` | 新規 | 孵卵器・孵化の出来事（Task 4） |
| `Assets/Scripts/Core/BreedingService.cs` | 変更 | 孵卵器の卵を室温の発生から外す（Task 4） |
| `Assets/Scripts/Core/Candling.cs` | 新規 | `CandlingStage`・`Candling`・`HatchSex`（Task 5） |
| `Assets/Scripts/Core/Hatchery.cs` | 新規 | 孵化・産卵床の後片付け・トレイから移す（Task 6） |
| `Assets/Scripts/Core/PetState.cs`・`OfflineProgressCalculator.cs` | 変更 | 母父・猶予（Task 7） |
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
