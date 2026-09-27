# 段階4：繁殖（ペアリング・抱卵・産卵・産卵床・衰弱・室温） 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 繁殖期（ゲーム内3〜9月）に条件を満たすオスとメスをペアリングし（メスがオスのケージを3日間訪問）、成功すれば抱卵して、決まった間隔で卵を産む。卵は産卵床があれば室温で発生し、なければゲーム内2日で乾く。健康0で衰弱し（30で回復）、衰弱中は繁殖できず相場×0.3。現在地の気温を室温（18〜30℃、取れないときは24℃）として Core に渡す。ペアリングは3つの入口（ケージ詳細・新しい「繁殖」タブ・台帳）から始められる。あわせて、デバッグの早送りが保存で戻る問題と、保存データの異常系・確定ヘテロの誤差を直す。

**Architecture:** ゲーム内時刻の差分（`Colony.GameClockOffset`）、室温（`RoomTemperature`・`RoomClimate`）、衰弱（`PetState.Weak` と `OfflineProgressCalculator`）、繁殖の条件と確率（`BreedingRules`・`BreedingForecast`・`BreedingRandom`）、繁殖の記録（`Pairing`・`GravidState`・`Egg`）、ペアリング・産卵・卵の時間の適用（`BreedingService`・`EggDevelopment`・`BreedingReport`）はすべて Unity に依存しない Core の純粋なクラスにし、EditMode テストで確かめる。`ColonySession` が時間の適用のたびに `BreedingService.Advance` を呼び、`ColonySaveService` がスキーマ3のまま新しいフィールドを保存する。画面の文字は純粋な `BreedingText` に分けてテストし、ホーム・ケージ一覧・ケージ詳細・繁殖タブ（`PairingView`）・台帳に表示する。

**Tech Stack:** Unity 6000.5.10f1（UI Toolkit、JsonUtility、NUnit / Unity Test Framework）、C#、macOS 上の CLI（`scripts/run-unity-tests.sh`、`scripts/test-summary.py`、`scripts/capture-screens.sh`、`scripts/build-ios.sh`）。

**Spec:** `docs/superpowers/specs/2026-09-25-breeder-sim-design.md`（本計画は §7 全体、§5.5、§6.1 の同居、§4.4 の予測（繁殖画面）、§3 の「全個体・全卵」、§8 の発生日数（室温での発生にだけ使う）、§14 段階4 を実装する。§8 の孵卵器・キャンドリング・孵化は段階5）。引き継ぎ：`docs/superpowers/handoffs/2026-09-27-phase4-kickoff.md`。

## Global Constraints

- ユーザーへの応答は日本語。コード・コメント・コミットメッセージは英語（CLAUDE.md）。
- 繁殖の条件（§7.1）：繁殖期＝ゲーム内3〜9月。メスは生後10か月以上・45g以上、オスは生後8か月以上・40g以上。どちらも衰弱していないこと。
- ペアリング（§7.3）：ゲーム内3日間。交尾成功率＝基本70%×相性の倍率（良い×1.2・悪い×0.6、`PersonalityTraits.MatingSuccessMultiplier`）×健康の係数×体重の係数。
- 抱卵・産卵（§7.4〜7.6）：成功するとゲーム内21〜28日後に最初の産卵、以後14〜28日ごと。1回2個（10%で1個）。回数4〜8（相性で±1、`PersonalityTraits.ClutchDelta`）。1回ごとに体重3〜5g減、40gを下回るとそのシーズンの産卵は終わり。繁殖期が終わると抱卵は解除。有精率90%（相性が悪いと75%）。
- 産卵床（§7.7）：あれば卵は産卵床の中で室温で発生。なければゲーム内2日で乾いて駄目になる。室温＝現在地の気温を18〜30℃に収めた値、取れないときは24℃。
- 衰弱（§5.5）：健康0で衰弱、30以上で解除。衰弱中は繁殖できず、相場×0.3（`MarketPrice.For` の `weak`）。
- 繁殖・衰弱のしきい値と日数はすべて `CareTuning` にまとめる（体重・月齢のしきい値と同じ場所）。金額は `EconomyTuning`（段階4で新しい金額はない）。
- セーブはスキーマ3のまま（フィールドを追加する）。段階1〜3で保存されたファイル、スキーマ1・2の旧ファイルも読めること。
- 時間の扱い：ゲーム内の今＝現実の今＋`Colony.GameClockOffset`。世話の値と卵の発生は1回に最大12時間分（`CareTuning.MaxOfflineProgressHours`）。予定の出来事（ペアリングの終わり・産卵・乾燥・繁殖期の終わり）は予定の時刻どおりに起きる。
- 画面の文字は日本語。マイナス記号は ASCII の `-`。範囲は全角の「〜」。
- 表示でレイアウトがずれないよう、固定の高さの欄は `visibility` で表示を切り替える（既存の方針）。画面（パネル・モーダル）の切り替えは `display`。
- 画面のタスクは「1画面・4ファイル以内」。指示書の行番号は段階4開始時の `cs-outline` のもの（前のタスクでずれるので、作業前に `python3 ~/.claude/skills/minimal-read-edit/scripts/cs-outline.py <file>` で確かめる）。
- 新しい .cs ファイルの .meta は手書きしない。Unity に生成させる（`scripts/run-unity-tests.sh` の実行で生成される）。`scripts/test-summary.py` が「.meta will be ignored」を報告したら失敗扱い。

## 計画者の判断（仕様書に書かれていない点）

| 判断 | 理由 |
|---|---|
| ゲーム内時刻：`Colony.GameClockOffset`（ゲームの時計が現実より進んでいる量、負にしない）を保存し、ゲーム内の今＝現実の今＋差分とする。早送り（倍率・「+12時間進める」）はこの差分を増やす。保存・再開・再読み込みでは戻らない。1xに戻しても差分は残り、消えるのは「セーブデータ削除」のときだけ | 引き継ぎの指摘（段階1から持ち越し）。保存のたびに日付が戻ると、電気代・入荷・産卵の予定が二重に起きたり止まったりする。巻き戻しを許すと、起きた出来事と日付が矛盾する |
| 保存の enum は定義された名前・値だけ受け付ける（`TryParseDefined`）。読めない値は既定値（性別メス・段階ベビー・ケージ標準・孵卵器簡易・台帳その他・性格は乱数） | `Enum.TryParse` は "99" のような数字も通し、遺伝子の配列の外を読んでセーブ全体が「壊れた」扱いになる（引き継ぎの異常系） |
| 確定ヘテロ：`KnownGenetics.SetHet` で 1−1e-9 以上を1、1e-9 以下を0に丸め（NaN は0）、判定は `KnownGenetics.IsProvenHet` に一本化（名前・相場） | 0.7+0.2+0.1＝0.9999999999999999 のような誤差で「99%ポッシブルヘテロ」と表示されるのを防ぐ。卵の子の遺伝情報（`KnownGenetics.ForChild`）を段階4で初めて作るため |
| 室温：Core の `RoomClimate` が最新の天気から 18〜30℃ に収めた値を持つ。位置情報オフ・取得失敗・オフラインのときは24℃（前回の表示文字が出ていても室温は24℃）。保存しない（起動直後は天気が届くまで24℃） | 利用者の決定5と仕様「天気が取れないときは24℃」。前回の気温を保存すると、位置情報をオフにした後も古い気温が残る |
| 衰弱：健康0以下で衰弱、30以上で解除（`CareTuning.WeakHealthThreshold`・`WeakRecoveryHealth`）。判定はオフライン計算の1分ごと。衰弱したら、ペアリングは中止、抱卵も終わる | §5.5「衰弱中は繁殖できない」。抱卵を一時停止にすると再開の条件が増える |
| 交尾成功率の係数：健康＝0.5＋0.5×（2匹のうち低いほうの健康÷100）、体重＝メスが45gで0.85→55g以上で1.0（直線）。上限95%。成功率はペアリング開始時に本当の性格で計算して保存し、終わりに1回だけ判定 | 仕様は「健康の係数×体重の係数」とだけ定める。健康・体重が十分なら仕様どおり「70%×相性」。開始時に固定すると画面の予測と結果の根拠がそろう |
| 予測（§7.2）：どちらかの性格が分かっていなければ相性は「不明」、成功率・回数は「普通」として表示する。子の確率はプレイヤーが知っている遺伝から（§4.4、`GeneticsCalculator.PredictVisualOdds`） | 知らない情報を表示に使わない（段階3の相場と同じ考え方） |
| ペアリングの条件には「性別が判明していること」も含む。抱卵中のメス・ペアリング中の個体は選べない | 雌雄不明ではオス・メスを選べない。同時に2つのペアリングはできない |
| 同居：オスのケージの `Cage.VisitorAnimalId` と `Colony.Pairings`。メスの元のケージは「空欄」に見せる（サムネイルも名前もなし、押せない）が、データ上は彼女のまま（`AnimalId` を残す。ショップはそこに生体を入れない）。ケージ詳細と左右スワイプは、いる個体のケージだけ（`Colony.ShownCages`）。訪問中のメスを台帳から開くとオスのケージが開く。動くヤモリはオスだけで、メスは「訪問中：○○」の表示にする | 利用者の決定2（訪問中の枠・元のケージは空欄）。1ケージ1匹の描画を変えずに、帰るケージを失わない |
| ペアリングの結果はゲーム内3日の終わりに判定する。成功ならすぐ抱卵、失敗・中止は通知する。ペアリングの終わりが繁殖期の外（10月以降）なら実らない | §7.3・§7.5。開始は繁殖期の中ならいつでもよい（9月末の開始は実らないことを画面で知らせない代わりに、結果の通知で理由を出す） |
| 乱数：繁殖の乱数は（コロニーの種、流れ、ペアリング番号、産卵の回数）から毎回作る（`BreedingRandom`）。コロニーの種はカレンダーの基準時刻から計算し、保存フィールドを増やさない | 閉じている間の計算と画面を開いたままの計算、途中の保存があっても同じ結果になる。既存の `System.Random` の並び（既存テスト）を変えない |
| 産卵の判定：産卵の時点で体重40g未満なら産まずに終了、産んだ後に40g未満になっても終了。繁殖期はペアリングが終わった年の3〜9月（`GravidState.SeasonYear`）で、外れたら終了 | §7.5 の「40gを下回るとそのシーズンの産卵は止まる」「繁殖期が終わると抱卵は解除」を、確かめられる時刻（産卵の時と時間の適用の終わり）で判定する |
| 予定の出来事（ペアリングの終わり・産卵・乾燥・繁殖期の終わり）は、どれだけ間が空いても予定の時刻どおりに起きる。卵の発生だけは個体と同じく1回に最大12時間分 | §3 の「最大12時間分」は世話の値と発生の量のこと。予定を捨てると、繁殖期をまたぐときに矛盾する |
| 卵の中身：子の本当の遺伝子型と、プレイヤーが知っている情報（`KnownGenetics.ForChild`）は産んだ時点で決めて卵に持たせる。父の遺伝子は抱卵の開始時に写す（父を売っても卵は変わらない）。雌雄と性格は孵化（段階5）で決める | 段階5の孵化が卵だけで完結する。§5.1 の雌雄は孵卵温度で決まる |
| 産卵床：`Cage.HasNestBox`（装飾の枠は使わない）。所持品から置き、外すと所持品に戻る。卵が入っている間は外せない。置く・外すはケージ詳細のボタン。産卵床の絵は描かない（文字とボタン） | 装飾の枠（1〜3）を産卵床に取られると飾れなくなる。段階4の完了条件は絵に関わらない |
| 産卵床の外の卵：ゲーム内2日で乾いて消え、通知する。後から産卵床を置いても助からない（孵卵器へ移す操作は段階5）。抱卵中のメスのケージに産卵床がなければ「産卵床なし」の注意を出す | §7.7。床に産まれた卵を拾う操作は仕様にない。産む前に気づけるようにする |
| 室温での発生：28℃60日・30℃52日・32℃45日の間を直線で結び、28℃未満も同じ傾き（24℃で76日）。24℃未満は発生せず、24℃未満の時間が合計ゲーム内3日になると卵が死ぬ（`CareTuning.EggColdFailGameDays`）。無精卵は発生しない。中盤3分の1の平均温度を記録する（段階5の雌雄に使う）。発生100%でも段階4では孵化しない（「孵化は段階5（孵卵器）で追加されます」） | §5.1・§8。室温は18〜30℃なので18〜24℃の扱いを決める必要がある。乱数ではなく日数にしてテストしやすくする |
| 無精卵・寒さで死んだ卵は段階4では見分けない（数だけ表示）。キャンドリングは段階5 | 利用者の決定4 |
| 繁殖タブ：6つ目のタブ「繁殖」をケージの隣に置く（ケージ／繁殖／孵卵器／ショップ／イベント／台帳）。中身は、繁殖期の案内・進行中の一覧（ペアリング・抱卵・卵、ペアリングをやめるボタン）・メスとオスの選択・予測・開始（確認ダイアログ）。ケージ詳細の「ペアリング」と台帳の「ペアリング」は、その個体を選んだ状態で繁殖タブを開く | 利用者の決定1（3つの入口）。選ぶ画面を1つにすると、実装と確認が1か所で済む |
| 卸売り：ペアリング中の個体は売れない（`ShopResult.InPairing`）。抱卵中のメスは売れる（抱卵は消え、産卵床の卵はケージに残る） | ペアリングの相手の居場所が壊れるのを防ぐ。抱卵中の出品禁止は §11（イベント、段階6） |
| 両親：卵に母・父の番号を持たせる。個体の「両親」と家系図は、子が生まれる段階5で台帳に加える | 段階3の計画では「両親と家系図は段階4」としたが、段階4では子が生まれず、表示するデータがない |
| 通知：1回の時間の適用で起きた繁殖・衰弱の出来事は、最初の1件＋「（ほか○件）」を通知欄に出す（開いているケージと関係なく） | 既存の通知欄は1行 |
| デバッグに「繁殖ペア追加」（大人のオス・メス、性別判明、生後12か月・55g、性格判明）を置く | 実機・画面キャプチャで、繁殖期と成長を待たずに確かめるため |
| 持ち越し：色を変えたスプライトのキャッシュの上限は段階5に回す | 段階4の完了条件に関わらない。段階4で新しい見た目が増えるのはショップの月4〜6匹だけで、キャッシュを本当に増やすのは孵化（段階5）。上限の方式（多因子の値を段階に丸めてキーを減らす、表示中でない物を破棄する等）は孵化の設計と一緒に決めるほうがよい |

## ファイル構成

| ファイル | 種別 | 役割 |
|---|---|---|
| `Assets/Scripts/Core/Colony.cs` | 変更 | `GameClockOffset`（Task 1）、`Cage.VisitorAnimalId`・`HasNestBox`、`Pairings`・`Eggs`・番号・`BreedingSeed`・同居の検索、`RemoveAnimal`（Task 6） |
| `Assets/Scripts/Core/ColonySession.cs` | 変更 | 時刻の差分（Task 1）、`Room`（Task 3）、衰弱と繁殖の適用・`Breeding`（Task 9） |
| `Assets/Scripts/Core/ColonySaveData.cs`・`ColonySaveService.cs` | 変更 | 時刻の差分（Task 1）、enum の読み方（Task 2）、衰弱・ペアリング・抱卵・卵・産卵床の保存（Task 9） |
| `Assets/Scripts/Core/KnownGenetics.cs`・`MorphNamer.cs`・`MarketPrice.cs` | 変更 | 確定ヘテロの丸めと判定（Task 2） |
| `Assets/Scripts/Core/RoomTemperature.cs` | 新規 | 室温（`RoomTemperature`・`RoomClimate`）（Task 3） |
| `Assets/Scripts/UI/WeatherService.cs` | 変更 | 天気の値を渡す（Task 3） |
| `Assets/Scripts/Core/PetState.cs` | 変更 | `Weak`（Task 4）、`Gravid`（Task 6） |
| `Assets/Scripts/Core/CareTuning.cs` | 変更 | 衰弱（Task 4）、繁殖の数値（Task 5） |
| `Assets/Scripts/Core/OfflineProgressCalculator.cs`・`OfflineProgressResult.cs` | 変更 | 衰弱の判定（Task 4） |
| `Assets/Scripts/Core/ShopService.cs` | 変更 | 衰弱の価格（Task 4）、ペアリング中は卸売り不可（Task 9） |
| `Assets/Scripts/Core/BreedingRandom.cs` | 新規 | 繁殖の乱数（Task 5） |
| `Assets/Scripts/Core/BreedingRules.cs` | 新規 | `PairingProblem`・`BreedingRules`・`BreedingForecast`（Task 5） |
| `Assets/Scripts/Core/BreedingData.cs` | 新規 | `Pairing`・`GravidState`・`EggPlace`・`EggFailure`・`Egg`（Task 6） |
| `Assets/Scripts/Core/BreedingService.cs` | 新規 | 条件・開始・中止・産卵床（Task 6）、時間の適用（Task 7） |
| `Assets/Scripts/Core/BreedingReport.cs` | 新規 | `BreedingEnd`・`ClutchReport`・`BreedingReport`（Task 7） |
| `Assets/Scripts/Core/EggDevelopment.cs` | 新規 | 温度による発生（Task 7） |
| `Assets/Scripts/UI/BreedingText.cs` | 新規 | 繁殖の文字（Task 8） |
| `Assets/Scripts/UI/ShopText.cs` | 変更 | ペアリング中の卸売りの文（Task 9） |
| `Assets/Scripts/UI/CageStatusText.cs`・`HomeView.cs` | 変更 | 訪問中の枠・衰弱の注意マーク・繁殖の注意（Task 10） |
| `Assets/Scripts/UI/TerrariumView.cs` | 変更 | 天気の値（Task 3）、ケージ詳細の繁殖表示・選択・通知・デバッグ（Task 11）、繁殖タブの結線（Task 13）、入口（Task 14） |
| `Assets/Scripts/UI/ShellNavigator.cs` | 変更 | 繁殖タブ（Task 12） |
| `Assets/Scripts/UI/PairingView.cs` | 新規 | 繁殖タブの中身（Task 13） |
| `Assets/Scripts/UI/LedgerView.cs` | 変更 | 個体の「ペアリング」（Task 14） |
| `Assets/UI/Terrarium.uxml`・`Terrarium.uss` | 変更 | ホームの印、ケージ詳細の繁殖欄、繁殖タブ、入口のボタン |
| テスト | 新規・変更 | EditMode：`ColonySessionTests`・`ColonySaveServiceTests`・`GeneticsTests`・`TimeAndWeatherTests`・`OfflineProgressCalculatorTests`・`ShopServiceTests`・`BreedingRulesTests`（新規）・`BreedingServiceTests`（新規）・`BreedingTextTests`（新規）・`ShellTests`・`ShopAndLedgerTextTests`。PlayMode：`TerrariumViewTests`・`ScreenCaptureTests` |
| `GAME.md`・`CLAUDE.md` | 変更 | 繁殖・衰弱・室温・ゲーム内時刻の差分の説明（Task 15） |

テストの実行（全タスク共通）：

```bash
scripts/run-unity-tests.sh EditMode && python3 scripts/test-summary.py Logs/EditMode-results.xml
scripts/run-unity-tests.sh PlayMode && python3 scripts/test-summary.py Logs/PlayMode-results.xml
```

特定のテストだけ流すときは `-testFilter <完全名>` を付ける（例：`scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.BreedingServiceTests`）。開始時点の件数は EditMode 388・PlayMode 61（＋Explicit 5）。

## タスクの一覧と作業者

| Task | 内容 | 作業者 |
|---|---|---|
| 1 | ゲーム内時刻の差分（早送りが保存で戻る問題） | transcriber |
| 2 | 保存データの異常系と確定ヘテロの誤差 | transcriber |
| 3 | 室温（天気の気温を Core へ） | transcriber |
| 4 | 衰弱（§5.5）の判定と価格 | transcriber |
| 5 | 繁殖の条件・成功率・予測・乱数 | transcriber |
| 6 | ペアリングの記録・同居・産卵床 | transcriber |
| 7 | ペアリングの結果・産卵のスケジュール・卵（§14 の完了条件） | transcriber |
| 8 | 繁殖の文字（`BreedingText`） | transcriber |
| 9 | セッションとセーブ（繁殖・衰弱の適用と保存、ペアリング中の卸売り） | programmer |
| 10 | ホーム・ケージ一覧の印（訪問中の枠・衰弱の注意マーク・繁殖の注意） | graphics |
| 11 | ケージ詳細の繁殖表示（衰弱の文言・訪問中・卵・産卵床・通知・デバッグ） | programmer（画面キャプチャで確認） |
| 12 | 繁殖タブの枠（6つ目のタブ） | graphics |
| 13 | 繁殖タブの中身（進行中・ペアの選択・予測・開始・中止） | programmer（画面キャプチャで確認） |
| 14 | ペアリングの入口（ケージ詳細のボタン・台帳の「ペアリング」） | programmer |
| 15 | 文書の更新と最終確認（実機） | programmer（最後に HQ が利用者に確認） |

依存：1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12 → 13 → 14 → 15（順番に行う。1〜8 は Core と純粋な文字、9 で結線、10 以降は画面）。

---

### Task 1: ゲーム内時刻の差分（早送りが保存で戻る問題）

**Files:**
- Modify: `Assets/Scripts/Core/Colony.cs`
- Modify: `Assets/Scripts/Core/ColonySaveData.cs`
- Modify: `Assets/Scripts/Core/ColonySaveService.cs`
- Modify: `Assets/Scripts/Core/ColonySession.cs`
- Test: `Assets/Tests/EditMode/ColonySessionTests.cs`・`Assets/Tests/EditMode/ColonySaveServiceTests.cs`（追加）

**Interfaces:**
- Produces: `Colony.GameClockOffset`（`TimeSpan`）、`ColonySaveData.gameClockOffsetTicks`。`ColonySession.GameNowUtc` は常に「現実の今＋差分」に再び合わせられる（`Load`・`Resume`・`Save`）。

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/ColonySessionTests.cs` のクラスの末尾に追加：

```csharp
        [Test]
        public void DebugFastForward_IsKeptWhenSavingAndReloading()
        {
            var session = NewSession();
            session.Load();
            session.Colony.Animals[0].NextShedAtUtc = realNow.AddDays(30);
            session.UseClock(new TimeService(() => realNow) { TimeMultiplier = 600d });

            realNow += TimeSpan.FromSeconds(60);
            session.Advance(TimeSpan.FromSeconds(60)); // 10 game-clock hours
            var gameNow = session.GameNowUtc;
            session.Save();

            Assert.That(gameNow, Is.EqualTo(Start + TimeSpan.FromHours(10)));
            Assert.That(session.GameNowUtc, Is.EqualTo(gameNow), "saving must not rewind the game clock");
            Assert.That(session.Colony.GameClockOffset, Is.EqualTo(TimeSpan.FromHours(10) - TimeSpan.FromSeconds(60)));

            var reloaded = NewSession();
            reloaded.Load();

            Assert.That(reloaded.GameNowUtc, Is.EqualTo(gameNow));
            Assert.That(reloaded.Calendar.DateAt(reloaded.GameNowUtc).ToDisplayText(),
                Is.EqualTo(session.Calendar.DateAt(gameNow).ToDisplayText()));
        }

        [Test]
        public void AFastForwardedMonth_IsNotRewoundOrBilledAgainAfterSaving()
        {
            var session = NewSession();
            session.Load();
            session.Colony.Animals[0].NextShedAtUtc = realNow.AddDays(30);

            var first = session.SimulateGameTime(TimeSpan.FromHours(25)); // into game month 1
            session.Save();
            var again = session.Resume();

            Assert.That(first.ElectricityCharged, Is.EqualTo(MaintenanceCosts.MonthlyElectricity(1, 1, economy)));
            Assert.That(again.ElectricityCharged, Is.EqualTo(0));
            Assert.That(session.GameNowUtc, Is.EqualTo(Start + TimeSpan.FromHours(25)));
            Assert.That(session.Calendar.MonthIndexAt(session.GameNowUtc), Is.EqualTo(1));
        }

        [Test]
        public void Resume_AfterAFastForward_AppliesOnlyTheRealTimeAway()
        {
            var session = NewSession();
            session.Load();
            var pet = session.Colony.Animals[0];
            pet.NextShedAtUtc = realNow.AddDays(30);
            session.SimulateGameTime(TimeSpan.FromHours(2));
            session.Save();

            realNow += TimeSpan.FromHours(1);
            session.Resume();

            Assert.That(session.GameNowUtc, Is.EqualTo(realNow + TimeSpan.FromHours(2)));
            Assert.That(pet.Hunger, Is.EqualTo(80d - care.HungerDecayPerHour * 3d).Within(1e-6));
        }
```

`Assets/Tests/EditMode/ColonySaveServiceTests.cs` のクラスの末尾に追加：

```csharp
        [Test]
        public void RoundTrip_KeepsTheGameClockOffset()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            colony.GameClockOffset = TimeSpan.FromHours(30);

            service.Save(path, colony);

            Assert.That(service.LoadOrCreate(path, Now, new Random(1)).GameClockOffset, Is.EqualTo(TimeSpan.FromHours(30)));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter "TerrariumDays.Tests.ColonySessionTests|TerrariumDays.Tests.ColonySaveServiceTests"`
Expected: コンパイルエラー（`GameClockOffset` がない）。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/Colony.cs`：`public int LastBilledMonthIndex { get; set; }` の次の行に追加：

```csharp
        /// <summary>
        /// How far the game clock runs ahead of real time. Debug fast-forward (the multiplier or
        /// "+12時間") adds to it and it is saved, so saving or reloading never rewinds the game
        /// date or replays scheduled events. Never negative; only a fresh save resets it.
        /// </summary>
        public TimeSpan GameClockOffset { get; set; } = TimeSpan.Zero;
```

`Assets/Scripts/Core/ColonySaveData.cs`：`ColonySaveData` の `public List<ShopOfferSaveData> shopOffers = ...;` の次に追加：

```csharp
        public long gameClockOffsetTicks;
```

`Assets/Scripts/Core/ColonySaveService.cs`：
1. `FromSaveData` の `new Colony { ... }` の初期化子に `LastBilledMonthIndex = data.lastBilledMonthIndex,` の次の行として追加：

```csharp
                GameClockOffset = TimeSpan.FromTicks(Math.Max(0L, data.gameClockOffsetTicks)),
```

2. `ToSaveData` の `new ColonySaveData { ... }` の初期化子に `shopMonthIndex = colony.Shop.StockMonthIndex,` の次の行として追加：

```csharp
                gameClockOffsetTicks = colony.GameClockOffset.Ticks,
```

`Assets/Scripts/Core/ColonySession.cs`：`Load`・`Advance`・`Resume`・`SimulateGameTime`・`Resync` を次のものに置き換える（ほかは変えない）：

```csharp
        public ColonyTickReport Load()
        {
            var realNow = clock.UtcNow();
            Colony = saveService.LoadOrCreate(savePath, realNow, random);
            Migrated = saveService.LastLoadMigrated;
            DecorMovedToInventory = saveService.LastLoadMovedDecor;
            SaveBlocked = saveService.LastLoadBackupFailed;
            Calendar = new GameCalendar(Colony.CalendarEpochUtc);
            GameNowUtc = realNow + Colony.GameClockOffset;
            var report = ApplyUntil(GameNowUtc);
            Resync(realNow);
            if (!SaveBlocked)
            {
                saveService.Save(savePath, Colony);
            }

            return report;
        }

        public ColonyTickReport Advance(TimeSpan realDelta)
        {
            var scaled = clock.ScaleElapsed(realDelta);
            if (scaled <= TimeSpan.Zero)
            {
                return new ColonyTickReport();
            }

            if (scaled > realDelta)
            {
                // Only the part beyond real time is fast-forward; it is kept across saves.
                Colony.GameClockOffset += scaled - realDelta;
            }

            GameNowUtc += scaled;
            return ApplyUntil(GameNowUtc);
        }

        public ColonyTickReport Resume()
        {
            GameNowUtc = clock.UtcNow() + Colony.GameClockOffset;
            var report = ApplyUntil(GameNowUtc);
            Save();
            return report;
        }

        public ColonyTickReport SimulateGameTime(TimeSpan span)
        {
            if (span <= TimeSpan.Zero)
            {
                return new ColonyTickReport();
            }

            Colony.GameClockOffset += span;
            GameNowUtc += span;
            return ApplyUntil(GameNowUtc);
        }
```

```csharp
        /// <summary>
        /// Re-anchors every animal and the game clock on real time plus the saved offset,
        /// keeping each animal's sub-step remainder so a care action is never replayed away.
        /// </summary>
        private void Resync(DateTimeOffset realNowUtc)
        {
            var gameNow = realNowUtc + Colony.GameClockOffset;
            var step = TimeSpan.FromMinutes(care.OfflineProgressStepMinutes);
            foreach (var pet in Colony.Animals)
            {
                var pending = GameNowUtc - pet.LastSavedAtUtc;
                if (pending < TimeSpan.Zero || pending >= step)
                {
                    pending = TimeSpan.Zero;
                }

                pet.LastSavedAtUtc = gameNow - pending;
            }

            GameNowUtc = gameNow;
        }
```

`Save()` は今のまま（`Resync(clock.UtcNow())` を呼ぶ）。

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体、PlayMode 全体（`OnCleanClicked_AfterTheDebugClockRanAhead_IsNotUndoneByTheNextTick`・`OnDebugSimulate12HoursClicked_AppliesExactlyTwelveHoursOfProgress` が引き続き通ること）
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Keep the debug fast-forward across saves with a saved game clock offset"
```

---

### Task 2: 保存データの異常系と確定ヘテロの誤差

**Files:**
- Modify: `Assets/Scripts/Core/KnownGenetics.cs`
- Modify: `Assets/Scripts/Core/MorphNamer.cs`（`FullName` の判定1行）
- Modify: `Assets/Scripts/Core/MarketPrice.cs`（`HetMultiplier` の判定1行）
- Modify: `Assets/Scripts/Core/ColonySaveService.cs`（enum の読み方）
- Test: `Assets/Tests/EditMode/GeneticsTests.cs`・`Assets/Tests/EditMode/ColonySaveServiceTests.cs`（追加）

**Interfaces:**
- Produces: `KnownGenetics.SnapEpsilon`（=1e-9）、`KnownGenetics.IsProvenHet(double) → bool`、`SetHet` の丸め（NaN→0、1−ε以上→1、ε以下→0）。`ColonySaveService` の中の `TryParseDefined<T>`（private）。

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/GeneticsTests.cs` のクラスの末尾に追加：

```csharp
        [Test]
        public void AProbabilityThatIsOneUpToRoundingError_CountsAsAProvenHet()
        {
            var nearlyOne = 0.7d + 0.2d + 0.1d; // 0.9999999999999999 in doubles
            Assert.That(nearlyOne, Is.LessThan(1d), "the test needs a real rounding error");

            var known = new KnownGenetics().SetHet(GeneId.Eclipse, nearlyOne).SetHet(GeneId.Blizzard, 1e-17);

            Assert.That(known.HetProbability(GeneId.Eclipse), Is.EqualTo(1d));
            Assert.That(known.HetProbability(GeneId.Blizzard), Is.EqualTo(0d));
            Assert.That(KnownGenetics.IsProvenHet(nearlyOne), Is.True);
            Assert.That(KnownGenetics.IsProvenHet(0.99d), Is.False);
            Assert.That(MorphNamer.FullName(Genotype.Normal(), known), Is.EqualTo("ノーマル ヘテロエクリプス"));
            Assert.That(MarketPrice.HetMultiplier(Genotype.Normal(), known), Is.EqualTo(1.2d).Within(1e-9));
        }

        [Test]
        public void ANaNProbability_IsTreatedAsNoHet()
        {
            var known = new KnownGenetics().SetHet(GeneId.Eclipse, double.NaN);

            Assert.That(known.HetProbability(GeneId.Eclipse), Is.EqualTo(0d));
        }
```

`Assets/Tests/EditMode/ColonySaveServiceTests.cs` のクラスの末尾に追加：

```csharp
        private const string AnimalBase = "\"id\":1,\"name\":\"レオパ1\",\"sex\":\"Female\",\"weightGrams\":45.0,\"stage\":\"Adult\"," +
            "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100";

        private void WriteSchemaThreeWithAnimal(string animalJson)
        {
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"animals\":[" + animalJson + "]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}],\"rackCount\":1,\"incubatorCount\":1,\"nextAnimalId\":2,\"nextCageId\":2}");
        }

        [Test]
        public void GenomeVersionOne_WithHetsUnknown_KeepsHetsUnknown()
        {
            WriteSchemaThreeWithAnimal("{" + AnimalBase + ",\"genomeVersion\":1,\"genes\":[],\"hets\":[],\"hetsUnknown\":true," +
                "\"personality\":\"Calm\",\"personalityKnown\":true}");

            var pet = service.LoadOrCreate(path, Now, new Random(1)).Animals[0];

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(pet.Known.HetsUnknown, Is.True);
            Assert.That(MorphNamer.FullName(pet.Genotype, pet.Known), Is.EqualTo("ノーマル（ヘテロ不明）"));
        }

        [Test]
        public void UnknownAndNumericGeneNames_AreIgnored()
        {
            WriteSchemaThreeWithAnimal("{" + AnimalBase + ",\"genomeVersion\":1," +
                "\"genes\":[{\"gene\":\"Eclipse\",\"copies\":2},{\"gene\":\"Lemonfrost\",\"copies\":2},{\"gene\":\"99\",\"copies\":2}]," +
                "\"hets\":[{\"gene\":\"Blizzard\",\"probability\":1.0},{\"gene\":\"Lemonfrost\",\"probability\":1.0},{\"gene\":\"42\",\"probability\":0.5}]," +
                "\"personality\":\"Calm\",\"personalityKnown\":true}");

            var pet = service.LoadOrCreate(path, Now, new Random(1)).Animals[0];

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(MorphNamer.FullName(pet.Genotype, pet.Known), Is.EqualTo("エクリプス ヘテロブリザード"));
        }

        [Test]
        public void MissingGeneAndHetLists_LoadAsNormalWithNoHets()
        {
            WriteSchemaThreeWithAnimal("{" + AnimalBase + ",\"genomeVersion\":1,\"hypo\":30,\"tangerine\":20," +
                "\"personality\":\"Shy\",\"personalityKnown\":true}");

            var pet = service.LoadOrCreate(path, Now, new Random(1)).Animals[0];

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(MorphNamer.FullName(pet.Genotype, pet.Known), Is.EqualTo("ノーマル"));
            Assert.That(pet.Personality, Is.EqualTo(Personality.Shy));
        }

        [TestCase("Grumpy")]
        [TestCase("42")]
        [TestCase("")]
        public void AnUnreadablePersonality_IsRolledFromTheDefinedOnes(string personality)
        {
            WriteSchemaThreeWithAnimal("{" + AnimalBase + ",\"genomeVersion\":1,\"personality\":\"" + personality + "\",\"personalityKnown\":true}");

            var pet = service.LoadOrCreate(path, Now, new Random(1)).Animals[0];

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(Enum.IsDefined(typeof(Personality), pet.Personality), Is.True);
        }

        [Test]
        public void NumericOrUnknownEnumNames_FallBackToDefaults()
        {
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"ledger\":[{\"atUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"category\":\"77\",\"amount\":-30,\"note\":\"x\"}]," +
                "\"animals\":[{\"id\":1,\"name\":\"レオパ1\",\"sex\":\"5\",\"weightGrams\":10.0,\"stage\":\"8\"," +
                "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100," +
                "\"genomeVersion\":1,\"personality\":\"Calm\",\"personalityKnown\":true}]," +
                "\"cages\":[{\"id\":1,\"size\":\"12\",\"animalId\":1}],\"rackCount\":1,\"incubators\":[\"9\"],\"nextAnimalId\":2,\"nextCageId\":2}");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(colony.Animals[0].Sex, Is.EqualTo(Sex.Female));
            Assert.That(colony.Animals[0].Stage, Is.EqualTo(GrowthStage.Baby));
            Assert.That(colony.Cages[0].Size, Is.EqualTo(CageSize.Standard));
            Assert.That(colony.Incubators, Is.EqualTo(new[] { IncubatorModel.Simple }));
            Assert.That(colony.Wallet.Ledger[0].Category, Is.EqualTo(LedgerCategory.Other));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter "TerrariumDays.Tests.GeneticsTests|TerrariumDays.Tests.ColonySaveServiceTests"`
Expected: コンパイルエラー（`IsProvenHet` がない）。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/KnownGenetics.cs`：`SetHet` を置き換え、`IsProvenHet` と定数を加える：

```csharp
        /// <summary>Probabilities this close to 0 or 1 are snapped, so float error never reads "99%" for a proven het.</summary>
        public const double SnapEpsilon = 1e-9;

        public KnownGenetics SetHet(GeneId gene, double probability)
        {
            var p = double.IsNaN(probability) ? 0d : Math.Max(0d, Math.Min(1d, probability));
            if (p >= 1d - SnapEpsilon)
            {
                p = 1d;
            }
            else if (p <= SnapEpsilon)
            {
                p = 0d;
            }

            het[(int)gene] = p;
            return this;
        }

        /// <summary>The one place that decides "proven het" (names, prices).</summary>
        public static bool IsProvenHet(double probability) => probability >= 1d - SnapEpsilon;
```

`Assets/Scripts/Core/MorphNamer.cs`（`FullName`）：`if (p >= 1d)` を `if (KnownGenetics.IsProvenHet(p))` にする。

`Assets/Scripts/Core/MarketPrice.cs`（`HetMultiplier`）：`bonus += p >= 1d ? ProvenHetBonus : p * PossibleHetBonusPerProbability;` を `bonus += KnownGenetics.IsProvenHet(p) ? ProvenHetBonus : p * PossibleHetBonusPerProbability;` にする。

`Assets/Scripts/Core/ColonySaveService.cs`：
1. クラスの末尾（`Parse` の後）に追加：

```csharp
        /// <summary>
        /// Enum.TryParse also accepts any number ("99"), which would index past the gene and
        /// personality tables; only defined names/values count.
        /// </summary>
        private static bool TryParseDefined<T>(string value, out T result) where T : struct
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, out result) && Enum.IsDefined(typeof(T), result))
            {
                return true;
            }

            result = default;
            return false;
        }
```

2. ファイルの中の `Enum.TryParse(` をすべて `TryParseDefined(` にする（9か所：`MigrateLegacy` の段階、孵卵器、台帳の区分、ケージの大きさ、性別、段階、遺伝子2か所、性格）。引数と `out` の型はそのまま。

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Snap near-proven het odds and reject undefined enum values in saves"
```

---

### Task 3: 室温（天気の気温を Core へ）

**Files:**
- Create: `Assets/Scripts/Core/RoomTemperature.cs`
- Modify: `Assets/Scripts/UI/WeatherService.cs`
- Modify: `Assets/Scripts/Core/ColonySession.cs`（`Room` を1行）
- Modify: `Assets/Scripts/UI/TerrariumView.cs`（天気の値の受け取り。`Awake` 155行の `StartCoroutine(weatherService.Run(OnWeatherText))`（321行）、`LoadColony` 363行、`OnWeatherText` 1191行）
- Test: `Assets/Tests/EditMode/TimeAndWeatherTests.cs`（追加）

**Interfaces:**
- Produces: `RoomTemperature.MinC/MaxC/FallbackC`・`RoomTemperature.IsUsable(WeatherReport)`・`RoomTemperature.From(WeatherReport?)`・`RoomTemperature.Label(double celsius, bool measured)`、`RoomClimate`（`TemperatureC`・`Measured`・`Update(WeatherReport?)`）、`ColonySession.Room`、`WeatherService.Run(Action<string> onText, Action<WeatherReport?> onReport = null)`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/TimeAndWeatherTests.cs` のクラスの末尾に追加：

```csharp
        [TestCase(12d, 18d)]
        [TestCase(18d, 18d)]
        [TestCase(26.4d, 26.4d)]
        [TestCase(30d, 30d)]
        [TestCase(35d, 30d)]
        public void RoomTemperature_ClampsTheOutdoorTemperature(double outdoor, double room)
        {
            Assert.That(RoomTemperature.From(new WeatherReport(outdoor, 50d, 1)), Is.EqualTo(room).Within(1e-9));
        }

        [Test]
        public void RoomTemperature_WithoutUsableWeather_Is24()
        {
            Assert.That(RoomTemperature.From(null), Is.EqualTo(24d));
            Assert.That(RoomTemperature.From(new WeatherReport(double.NaN, 50d, 1)), Is.EqualTo(24d));
        }

        [Test]
        public void RoomClimate_FollowsTheLatestReportAndFallsBackWhenItIsLost()
        {
            var room = new RoomClimate();
            Assert.That((room.TemperatureC, room.Measured), Is.EqualTo((24d, false)));

            room.Update(new WeatherReport(31d, 60d, 0));
            Assert.That((room.TemperatureC, room.Measured), Is.EqualTo((30d, true)));

            room.Update(null);
            Assert.That((room.TemperatureC, room.Measured), Is.EqualTo((24d, false)));

            room.Update(new WeatherReport(double.NaN, 60d, 0));
            Assert.That((room.TemperatureC, room.Measured), Is.EqualTo((24d, false)));
        }

        [Test]
        public void RoomTemperatureLabel_SaysWhenItIsTheFallback()
        {
            Assert.That(RoomTemperature.Label(27.4d, true), Is.EqualTo("室温27℃"));
            Assert.That(RoomTemperature.Label(24d, false), Is.EqualTo("室温24℃（天気を取得できないため）"));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.TimeAndWeatherTests`
Expected: コンパイルエラー（`RoomTemperature` がない）。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/RoomTemperature.cs`:

```csharp
using System;
using System.Globalization;

namespace TerrariumDays.Core
{
    /// <summary>
    /// The breeding room's temperature (§7.7): the current outdoor temperature at the player's
    /// location clamped to 18–30 ℃, or 24 ℃ when there is no usable weather (location off,
    /// failed, offline). Pure, so eggs and breeding can be tested without Unity.
    /// </summary>
    public static class RoomTemperature
    {
        public const double MinC = 18d;
        public const double MaxC = 30d;
        public const double FallbackC = 24d;

        public static bool IsUsable(WeatherReport report) =>
            !double.IsNaN(report.TemperatureC) && !double.IsInfinity(report.TemperatureC);

        public static double From(WeatherReport? report)
        {
            if (!report.HasValue || !IsUsable(report.Value))
            {
                return FallbackC;
            }

            return Math.Max(MinC, Math.Min(MaxC, report.Value.TemperatureC));
        }

        /// <summary>"室温27℃", or with no weather "室温24℃（天気を取得できないため）".</summary>
        public static string Label(double celsius, bool measured)
        {
            var text = "室温" + Math.Round(celsius, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "℃";
            return measured ? text : text + "（天気を取得できないため）";
        }
    }

    /// <summary>The room temperature the colony uses right now; the UI feeds it each weather result.</summary>
    public sealed class RoomClimate
    {
        public double TemperatureC { get; private set; } = RoomTemperature.FallbackC;

        /// <summary>False while the 24 ℃ fallback is in use.</summary>
        public bool Measured { get; private set; }

        public void Update(WeatherReport? report)
        {
            TemperatureC = RoomTemperature.From(report);
            Measured = report.HasValue && RoomTemperature.IsUsable(report.Value);
        }
    }
}
```

`Assets/Scripts/Core/ColonySession.cs`：`public bool DecorMovedToInventory { get; private set; }` の次に追加：

```csharp
        /// <summary>The room temperature for nest-box eggs (§7.7); the UI updates it from the weather.</summary>
        public RoomClimate Room { get; } = new RoomClimate();
```

`Assets/Scripts/UI/WeatherService.cs`：`Run` と `FetchOnce` を次のように変える（表示の文字は今のまま）：

```csharp
        public IEnumerator Run(Action<string> onText, Action<WeatherReport?> onReport = null)
        {
            onText(Cached() ?? LoadingText);
            var report = onReport ?? (_ => { });
            while (true)
            {
                yield return FetchOnce(onText, report);
                yield return new WaitForSecondsRealtime(RefreshSeconds);
            }
        }

        private IEnumerator FetchOnce(Action<string> onText, Action<WeatherReport?> onReport)
```

`FetchOnce` の中：
- `if (!Input.location.isEnabledByUser)` の枝の最初に `onReport(null);`
- `if (Input.location.status != LocationServiceStatus.Running)` の枝の `Input.location.Stop();` の次に `onReport(null);`
- 成功の枝（`OpenMeteo.TryParse` が true）の最初に `onReport(report);`
- 失敗の枝（`else`）の最初に `onReport(null);`

`Assets/Scripts/UI/TerrariumView.cs`：
- フィールド `private readonly WeatherService weatherService = new WeatherService();` の次に `private WeatherReport? lastWeatherReport;`
- `StartCoroutine(weatherService.Run(OnWeatherText));` を `StartCoroutine(weatherService.Run(OnWeatherText, OnWeatherReport));` に
- `OnWeatherText` の次に：

```csharp
        private void OnWeatherReport(WeatherReport? report)
        {
            lastWeatherReport = report;
            session?.Room.Update(report);
        }
```

- `LoadColony` の `session = new ColonySession(...);` の次の行に `session.Room.Update(lastWeatherReport);`

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体、PlayMode 全体
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts Assets/Tests/EditMode
git commit -m "Feed the clamped outdoor temperature to the colony as room temperature"
```

---

### Task 4: 衰弱（§5.5）の判定と価格

**Files:**
- Modify: `Assets/Scripts/Core/PetState.cs`
- Modify: `Assets/Scripts/Core/CareTuning.cs`
- Modify: `Assets/Scripts/Core/OfflineProgressResult.cs`
- Modify: `Assets/Scripts/Core/OfflineProgressCalculator.cs`
- Modify: `Assets/Scripts/Core/ShopService.cs`（`MarketOf` の1行）
- Test: `Assets/Tests/EditMode/OfflineProgressCalculatorTests.cs`・`Assets/Tests/EditMode/ShopServiceTests.cs`（追加）

**Interfaces:**
- Produces: `PetState.Weak`、`CareTuning.WeakHealthThreshold`（0）・`WeakRecoveryHealth`（30）、`OfflineProgressResult.BecameWeak`・`RecoveredFromWeak`、`ShopService.MarketOf(pet)` が `pet.Weak` を価格に反映。
- 保存（`weak`）とセッションの報告（`ColonyTickReport.WeakStarted`・`WeakRecovered`）は Task 9。

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/OfflineProgressCalculatorTests.cs` のクラスの末尾に追加：

```csharp
        [Test]
        public void HealthReachingZero_MakesTheAnimalWeak()
        {
            var state = new PetState
            {
                Hunger = 0d, Hydration = 0d, Cleanliness = 0d, Health = 4d,
                HatchedAtUtc = Epoch, LastSavedAtUtc = Epoch, NextShedAtUtc = Epoch.AddDays(30),
            };

            var result = calculator.Apply(state, Epoch, Epoch.AddHours(1));

            Assert.That(state.Health, Is.EqualTo(0d));
            Assert.That(state.Weak, Is.True);
            Assert.That(result.BecameWeak, Is.True);
            Assert.That(result.RecoveredFromWeak, Is.False);
        }

        [Test]
        public void AWeakAnimal_StaysWeakUntilHealthIsBackToThirty()
        {
            var state = new PetState
            {
                Hunger = 100d, Hydration = 100d, Cleanliness = 100d, Health = 0d, Weak = true,
                HatchedAtUtc = Epoch, LastSavedAtUtc = Epoch, NextShedAtUtc = Epoch.AddDays(30),
            };

            var first = calculator.Apply(state, Epoch, Epoch.AddHours(12)); // +2 per hour

            Assert.That(state.Health, Is.EqualTo(24d).Within(1e-6));
            Assert.That(state.Weak, Is.True);
            Assert.That(first.BecameWeak, Is.False);
            Assert.That(first.RecoveredFromWeak, Is.False);

            state.Hunger = 100d;
            state.Hydration = 100d;
            state.Cleanliness = 100d;
            var second = calculator.Apply(state, Epoch.AddHours(12), Epoch.AddHours(15).AddMinutes(10));

            Assert.That(state.Health, Is.GreaterThanOrEqualTo(30d));
            Assert.That(state.Weak, Is.False);
            Assert.That(second.RecoveredFromWeak, Is.True);
        }
```

`Assets/Tests/EditMode/ShopServiceTests.cs` のクラスの末尾に追加：

```csharp
        [Test]
        public void AWeakAnimal_IsPricedAtThirtyPercent()
        {
            var colony = Colony.CreateNew(Now, economy, care, new Random(1));
            var pet = colony.Animals[0];
            var healthy = shop.MarketOf(pet);

            pet.Weak = true;

            Assert.That(shop.MarketOf(pet), Is.EqualTo(MarketPrice.For(pet, weak: true)));
            Assert.That(shop.MarketOf(pet), Is.LessThan(healthy));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter "TerrariumDays.Tests.OfflineProgressCalculatorTests|TerrariumDays.Tests.ShopServiceTests"`
Expected: コンパイルエラー（`Weak` がない）。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/PetState.cs`：`Health` プロパティの次に追加：

```csharp
        /// <summary>
        /// §5.5 衰弱: set when health reaches 0, cleared when it is back to 30. A weak animal
        /// cannot breed and sells for ×0.3. Adults never die; this is the failure state.
        /// </summary>
        public bool Weak { get; set; }
```

`Assets/Scripts/Core/CareTuning.cs`：`LowCareThreshold` の次に追加：

```csharp
        /// <summary>§5.5: health at or below this makes the animal weak (衰弱).</summary>
        public double WeakHealthThreshold { get; set; } = 0d;

        /// <summary>§5.5: a weak animal recovers once health is back to this.</summary>
        public double WeakRecoveryHealth { get; set; } = 30d;
```

`Assets/Scripts/Core/OfflineProgressResult.cs`：コンストラクタと2つのプロパティを加える：

```csharp
        public OfflineProgressResult(TimeSpan appliedElapsed, GrowthStage? newGrowthStage, int shedCount = 0, bool sexRevealed = false,
            bool becameWeak = false, bool recoveredFromWeak = false)
        {
            AppliedElapsed = appliedElapsed;
            NewGrowthStage = newGrowthStage;
            ShedCount = shedCount;
            SexRevealed = sexRevealed;
            BecameWeak = becameWeak;
            RecoveredFromWeak = recoveredFromWeak;
        }

        /// <summary>The animal was not weak before this call and is weak after it (§5.5).</summary>
        public bool BecameWeak { get; }

        /// <summary>The animal was weak before this call and has recovered (§5.5).</summary>
        public bool RecoveredFromWeak { get; }
```

`Assets/Scripts/Core/OfflineProgressCalculator.cs`（`Apply`）：
1. `var sexRevealed = false;` の次に `var weakBefore = state.Weak;`
2. ループの中、`ApplyStep(state, refusing);` の直後に：

```csharp
                if (!state.Weak && state.Health <= tuning.WeakHealthThreshold)
                {
                    state.Weak = true;
                }
                else if (state.Weak && state.Health >= tuning.WeakRecoveryHealth)
                {
                    state.Weak = false;
                }
```

3. 最後の `return` を次にする：

```csharp
            return new OfflineProgressResult(appliedElapsed, stageAfter != stageBefore ? stageAfter : (GrowthStage?)null, sheds, sexRevealed,
                !weakBefore && state.Weak, weakBefore && !state.Weak);
```

`Assets/Scripts/Core/ShopService.cs`：

```csharp
        public long MarketOf(PetState pet) => MarketPrice.For(pet, EventDemand.None, pet.Weak);
```

`MarketPrice.For` の説明コメントの「(phase 4)」を消す。

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Make animals weak at zero health until thirty and price them at thirty percent"
```

---

### Task 5: 繁殖の条件・成功率・予測・乱数

**Files:**
- Modify: `Assets/Scripts/Core/CareTuning.cs`
- Create: `Assets/Scripts/Core/BreedingRandom.cs`
- Create: `Assets/Scripts/Core/BreedingRules.cs`
- Test: `Assets/Tests/EditMode/BreedingRulesTests.cs`（新規）

**Interfaces:**
- Consumes: `PetState.Weak`（Task 4）、`GrowthModel.AgeMonths`、`PersonalityTraits.CompatibilityOf/MatingSuccessMultiplier/ClutchDelta`、`GeneticsCalculator.PredictVisualOdds`
- Produces: `CareTuning` の繁殖の数値（下のコード）、`BreedingRandom.PairingStream/ClutchStream/SeedOf(DateTimeOffset)/For(int seed, int stream, int id, int index)/Uniform(Random, double, double)`、`PairingProblem`、`BreedingRules.IsBreedingSeason/SeasonOf/CheckAnimal/HealthFactor/WeightFactor/MatingSuccess/ClutchRange/FertilityFor`、`BreedingForecast.For(male, female, care)`（`CompatibilityKnown`・`Compatibility`・`SuccessChance`・`MinClutches`・`MaxClutches`・`Offspring`）

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/BreedingRulesTests.cs`:

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class BreedingRulesTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();

        private static PetState Adult(Sex sex, double months = 12d, double grams = 55d, Personality personality = Personality.Calm) =>
            new PetState
            {
                Sex = sex,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = grams,
                HatchedAtUtc = Now.AddDays(-months),
                Personality = personality,
                PersonalityKnown = true,
            };

        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(6, true)]
        [TestCase(9, true)]
        [TestCase(10, false)]
        [TestCase(12, false)]
        public void TheBreedingSeasonIsMarchToSeptember(int month, bool inSeason)
        {
            var date = new GameDate(2027, month, 15);

            Assert.That(BreedingRules.IsBreedingSeason(date, care), Is.EqualTo(inSeason));
            Assert.That(BreedingRules.SeasonOf(date, care), Is.EqualTo(inSeason ? 2027 : -1));
        }

        [Test]
        public void AFemaleNeedsTenMonthsAndFortyFiveGrams()
        {
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Female, 10d, 45d), Sex.Female, Now, care), Is.EqualTo(PairingProblem.None));
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Female, 9.9d, 50d), Sex.Female, Now, care), Is.EqualTo(PairingProblem.TooYoung));
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Female, 12d, 44.9d), Sex.Female, Now, care), Is.EqualTo(PairingProblem.TooLight));
        }

        [Test]
        public void AMaleNeedsEightMonthsAndFortyGrams()
        {
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Male, 8d, 40d), Sex.Male, Now, care), Is.EqualTo(PairingProblem.None));
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Male, 7.9d, 50d), Sex.Male, Now, care), Is.EqualTo(PairingProblem.TooYoung));
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Male, 12d, 39.9d), Sex.Male, Now, care), Is.EqualTo(PairingProblem.TooLight));
        }

        [Test]
        public void UnknownSexTheWrongSexAndWeakness_BlockBreeding()
        {
            var unknown = Adult(Sex.Female);
            unknown.SexRevealed = false;
            var weak = Adult(Sex.Female);
            weak.Weak = true;

            Assert.That(BreedingRules.CheckAnimal(unknown, Sex.Female, Now, care), Is.EqualTo(PairingProblem.SexUnknown));
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Male), Sex.Female, Now, care), Is.EqualTo(PairingProblem.NotMaleAndFemale));
            Assert.That(BreedingRules.CheckAnimal(weak, Sex.Female, Now, care), Is.EqualTo(PairingProblem.Weak));
            Assert.That(BreedingRules.CheckAnimal(null, Sex.Female, Now, care), Is.EqualTo(PairingProblem.NotFound));
        }

        [Test]
        public void MatingSuccess_IsSeventyPercentTimesCompatibilityHealthAndWeight()
        {
            var male = Adult(Sex.Male);
            var female = Adult(Sex.Female, grams: 55d);

            Assert.That(BreedingRules.MatingSuccess(Compatibility.Good, male, female, care), Is.EqualTo(0.84d).Within(1e-9));
            Assert.That(BreedingRules.MatingSuccess(Compatibility.Normal, male, female, care), Is.EqualTo(0.7d).Within(1e-9));
            Assert.That(BreedingRules.MatingSuccess(Compatibility.Bad, male, female, care), Is.EqualTo(0.42d).Within(1e-9));

            female.WeightGrams = 45d;
            Assert.That(BreedingRules.MatingSuccess(Compatibility.Normal, male, female, care), Is.EqualTo(0.595d).Within(1e-9));

            female.WeightGrams = 55d;
            male.Health = 50d;
            Assert.That(BreedingRules.MatingSuccess(Compatibility.Normal, male, female, care), Is.EqualTo(0.525d).Within(1e-9));
        }

        [Test]
        public void ClutchRangeAndFertility_FollowCompatibility()
        {
            Assert.That(BreedingRules.ClutchRange(Compatibility.Good, care), Is.EqualTo((5, 9)));
            Assert.That(BreedingRules.ClutchRange(Compatibility.Normal, care), Is.EqualTo((4, 8)));
            Assert.That(BreedingRules.ClutchRange(Compatibility.Bad, care), Is.EqualTo((3, 7)));
            Assert.That(BreedingRules.FertilityFor(Compatibility.Good, care), Is.EqualTo(0.9d));
            Assert.That(BreedingRules.FertilityFor(Compatibility.Normal, care), Is.EqualTo(0.9d));
            Assert.That(BreedingRules.FertilityFor(Compatibility.Bad, care), Is.EqualTo(0.75d));
        }

        [Test]
        public void Forecast_UsesWhatThePlayerKnows()
        {
            var male = Adult(Sex.Male, personality: Personality.Calm);
            male.Known = new KnownGenetics().SetHet(GeneId.Eclipse, 1d);
            var female = Adult(Sex.Female, personality: Personality.Shy);
            female.Known = new KnownGenetics().SetHet(GeneId.Eclipse, 1d);

            var forecast = BreedingForecast.For(male, female, care);

            Assert.That(forecast.CompatibilityKnown, Is.True);
            Assert.That(forecast.Compatibility, Is.EqualTo(Compatibility.Good));
            Assert.That(forecast.SuccessChance, Is.EqualTo(0.84d).Within(1e-9));
            Assert.That((forecast.MinClutches, forecast.MaxClutches), Is.EqualTo((5, 9)));
            Assert.That(forecast.Offspring.Select(o => o.Name), Is.EqualTo(new[] { "ノーマル", "エクリプス" }));
            Assert.That(forecast.Offspring.Select(o => o.Probability), Is.EqualTo(new[] { 0.75d, 0.25d }).Within(1e-9));
        }

        [Test]
        public void Forecast_WithAnUnknownPersonality_AssumesANormalMatch()
        {
            var male = Adult(Sex.Male, personality: Personality.Bold);
            var female = Adult(Sex.Female, personality: Personality.Shy); // Bold x Shy is bad in truth
            female.PersonalityKnown = false;

            var forecast = BreedingForecast.For(male, female, care);

            Assert.That(forecast.CompatibilityKnown, Is.False);
            Assert.That(forecast.Compatibility, Is.EqualTo(Compatibility.Normal));
            Assert.That(forecast.SuccessChance, Is.EqualTo(0.7d).Within(1e-9));
            Assert.That((forecast.MinClutches, forecast.MaxClutches), Is.EqualTo((4, 8)));
        }

        [Test]
        public void BreedingRandom_IsReproducibleAndDiffersByIndex()
        {
            var a = BreedingRandom.For(123, BreedingRandom.ClutchStream, 7, 0).NextDouble();
            var b = BreedingRandom.For(123, BreedingRandom.ClutchStream, 7, 0).NextDouble();
            var c = BreedingRandom.For(123, BreedingRandom.ClutchStream, 7, 1).NextDouble();
            var d = BreedingRandom.For(123, BreedingRandom.PairingStream, 7, 0).NextDouble();

            Assert.That(a, Is.EqualTo(b));
            Assert.That(c, Is.Not.EqualTo(a));
            Assert.That(d, Is.Not.EqualTo(a));
            Assert.That(BreedingRandom.SeedOf(Now), Is.EqualTo(BreedingRandom.SeedOf(Now)));
            Assert.That(BreedingRandom.Uniform(new Random(1), 3d, 5d), Is.InRange(3d, 5d));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.BreedingRulesTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/CareTuning.cs`：`PersonalityRevealGameDays` の次に追加：

```csharp
        // Breeding (§7), in game months / grams / game days.
        public int BreedingSeasonFirstMonth { get; set; } = 3;
        public int BreedingSeasonLastMonth { get; set; } = 9;
        public double FemaleBreedingMinAgeMonths { get; set; } = 10d;
        public double FemaleBreedingMinWeightGrams { get; set; } = 45d;
        public double MaleBreedingMinAgeMonths { get; set; } = 8d;
        public double MaleBreedingMinWeightGrams { get; set; } = 40d;
        public double PairingGameDays { get; set; } = 3d;
        public double BaseMatingSuccess { get; set; } = 0.7d;
        public double MaxMatingSuccess { get; set; } = 0.95d;
        public double FirstClutchMinGameDays { get; set; } = 21d;
        public double FirstClutchMaxGameDays { get; set; } = 28d;
        public double ClutchIntervalMinGameDays { get; set; } = 14d;
        public double ClutchIntervalMaxGameDays { get; set; } = 28d;
        public int MinClutches { get; set; } = 4;
        public int MaxClutches { get; set; } = 8;
        public double SingleEggChance { get; set; } = 0.1d;
        public double ClutchWeightLossMinGrams { get; set; } = 3d;
        public double ClutchWeightLossMaxGrams { get; set; } = 5d;
        public double LayingStopWeightGrams { get; set; } = 40d;
        public double Fertility { get; set; } = 0.9d;
        public double BadMatchFertility { get; set; } = 0.75d;

        /// <summary>§7.7: an egg laid without a nest box dries out after this many game days.</summary>
        public double LooseEggDryGameDays { get; set; } = 2d;

        /// <summary>Game days below 24 ℃ (in total) that kill a nest-box egg (planner's ruling).</summary>
        public double EggColdFailGameDays { get; set; } = 3d;
```

`Assets/Scripts/Core/BreedingRandom.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Reproducible randomness for breeding (§7). Each roll gets its own generator built from
    /// the colony's seed, a stream, an id and an index, so outcomes do not depend on the order
    /// things were processed in (offline catch-up vs live play) or on saving in between.
    /// </summary>
    public static class BreedingRandom
    {
        public const int PairingStream = 1;
        public const int ClutchStream = 2;

        /// <summary>The colony's breeding seed, from its calendar epoch (so it needs no save field).</summary>
        public static int SeedOf(DateTimeOffset epochUtc)
        {
            var ticks = epochUtc.UtcTicks;
            return unchecked((int)ticks ^ (int)(ticks >> 32));
        }

        public static Random For(int seed, int stream, int id, int index)
        {
            unchecked
            {
                var h = (uint)seed;
                h = Mix(h ^ ((uint)stream * 0x9E3779B9u));
                h = Mix(h ^ ((uint)id * 0x85EBCA6Bu));
                h = Mix(h ^ ((uint)index * 0xC2B2AE35u));
                return new Random((int)(h & 0x7FFFFFFFu));
            }
        }

        /// <summary>A uniform value in [min, max).</summary>
        public static double Uniform(Random random, double min, double max) => min + random.NextDouble() * (max - min);

        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
```

`Assets/Scripts/Core/BreedingRules.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Why a pairing cannot start (§7.1). None = it can.</summary>
    public enum PairingProblem
    {
        None,
        NotFound,
        OutOfSeason,
        NotMaleAndFemale,
        SexUnknown,
        Weak,
        TooYoung,
        TooLight,
        Gravid,
        AlreadyPairing
    }

    /// <summary>Breeding conditions and odds (§7.1–7.6). Pure: no Unity, no clock of its own.</summary>
    public static class BreedingRules
    {
        /// <summary>The weight factor reaches 1 this many grams above the female minimum.</summary>
        public const double WeightFactorSpanGrams = 10d;

        public const double WeightFactorFloor = 0.85d;
        public const double HealthFactorFloor = 0.5d;

        public static bool IsBreedingSeason(GameDate date, CareTuning care) =>
            date.Month >= care.BreedingSeasonFirstMonth && date.Month <= care.BreedingSeasonLastMonth;

        /// <summary>The season (game year) a date belongs to, or -1 outside the breeding season.</summary>
        public static int SeasonOf(GameDate date, CareTuning care) => IsBreedingSeason(date, care) ? date.Year : -1;

        /// <summary>
        /// Whether this animal can take the given role now: sex known and right, not weak, old
        /// and heavy enough. The season, gravid state and the room are checked by BreedingService.
        /// </summary>
        public static PairingProblem CheckAnimal(PetState pet, Sex role, DateTimeOffset nowUtc, CareTuning care)
        {
            if (pet == null)
            {
                return PairingProblem.NotFound;
            }

            if (!pet.SexKnown)
            {
                return PairingProblem.SexUnknown;
            }

            if (pet.Sex != role)
            {
                return PairingProblem.NotMaleAndFemale;
            }

            if (pet.Weak)
            {
                return PairingProblem.Weak;
            }

            var female = role == Sex.Female;
            var minAge = female ? care.FemaleBreedingMinAgeMonths : care.MaleBreedingMinAgeMonths;
            if (GrowthModel.AgeMonths(pet, nowUtc) < minAge)
            {
                return PairingProblem.TooYoung;
            }

            var minWeight = female ? care.FemaleBreedingMinWeightGrams : care.MaleBreedingMinWeightGrams;
            return pet.WeightGrams < minWeight ? PairingProblem.TooLight : PairingProblem.None;
        }

        /// <summary>0.5 at health 0 up to 1 at health 100, from the less healthy of the two.</summary>
        public static double HealthFactor(PetState male, PetState female)
        {
            var health = Math.Min(male.Health, female.Health);
            return HealthFactorFloor + (1d - HealthFactorFloor) * health / 100d;
        }

        /// <summary>0.85 at the female minimum weight up to 1 at 10 g above it.</summary>
        public static double WeightFactor(PetState female, CareTuning care)
        {
            var above = (female.WeightGrams - care.FemaleBreedingMinWeightGrams) / WeightFactorSpanGrams;
            return WeightFactorFloor + (1d - WeightFactorFloor) * Math.Max(0d, Math.Min(1d, above));
        }

        /// <summary>§7.3: base 70% × compatibility × health × weight, at most CareTuning.MaxMatingSuccess.</summary>
        public static double MatingSuccess(Compatibility compatibility, PetState male, PetState female, CareTuning care) =>
            Math.Min(care.MaxMatingSuccess, care.BaseMatingSuccess * PersonalityTraits.MatingSuccessMultiplier(compatibility)
                * HealthFactor(male, female) * WeightFactor(female, care));

        /// <summary>§7.4: 4–8 clutches, ±1 by compatibility, at least 1.</summary>
        public static (int Min, int Max) ClutchRange(Compatibility compatibility, CareTuning care)
        {
            var delta = PersonalityTraits.ClutchDelta(compatibility);
            return (Math.Max(1, care.MinClutches + delta), Math.Max(1, care.MaxClutches + delta));
        }

        /// <summary>§7.6: 90%, or 75% for a bad match.</summary>
        public static double FertilityFor(Compatibility compatibility, CareTuning care) =>
            compatibility == Compatibility.Bad ? care.BadMatchFertility : care.Fertility;
    }

    /// <summary>
    /// What the pairing screen shows before starting (§7.2), from what the player knows: an
    /// unknown personality means an unknown match, estimated as a normal one.
    /// </summary>
    public sealed class BreedingForecast
    {
        private BreedingForecast()
        {
        }

        public bool CompatibilityKnown { get; private set; }

        public Compatibility Compatibility { get; private set; }

        public double SuccessChance { get; private set; }

        public int MinClutches { get; private set; }

        public int MaxClutches { get; private set; }

        public List<MorphOdds> Offspring { get; private set; }

        public static BreedingForecast For(PetState male, PetState female, CareTuning care)
        {
            var known = male.PersonalityKnown && female.PersonalityKnown;
            var compatibility = known ? PersonalityTraits.CompatibilityOf(male.Personality, female.Personality) : Compatibility.Normal;
            var clutches = BreedingRules.ClutchRange(compatibility, care);
            return new BreedingForecast
            {
                CompatibilityKnown = known,
                Compatibility = compatibility,
                SuccessChance = BreedingRules.MatingSuccess(compatibility, male, female, care),
                MinClutches = clutches.Min,
                MaxClutches = clutches.Max,
                Offspring = GeneticsCalculator.PredictVisualOdds(female.Genotype, female.Known, male.Genotype, male.Known),
            };
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Add breeding conditions, mating odds, the pairing forecast and breeding randomness"
```

---

### Task 6: ペアリングの記録・同居・産卵床

**Files:**
- Create: `Assets/Scripts/Core/BreedingData.cs`
- Modify: `Assets/Scripts/Core/PetState.cs`（`Gravid`）
- Modify: `Assets/Scripts/Core/Colony.cs`
- Create: `Assets/Scripts/Core/BreedingService.cs`
- Test: `Assets/Tests/EditMode/BreedingServiceTests.cs`（新規）

**Interfaces:**
- Consumes: Task 5
- Produces: `Pairing`・`GravidState`・`EggPlace`・`EggFailure`・`Egg`（下のコード）、`PetState.Gravid`、`Cage.VisitorAnimalId`・`Cage.HasNestBox`、`Colony.Pairings/Eggs/NextPairingId/NextEggId/BreedingSeed/PairingOf/IsVisiting/VisitorIn/CageShowing/ResidentShown/ShownCages/EggsIn`、`NestBoxResult`、`BreedingService(CareTuning)` の `CheckPair/CheckCandidate/StartPairing/CancelPairing/PlaceNestBox/RemoveNestBox`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/BreedingServiceTests.cs`:

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class BreedingServiceTests
    {
        // Calendar day 0 is 1 April 2026 (in season).
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly GameCalendar calendar = new GameCalendar(Epoch);
        private BreedingService breeding;

        [SetUp]
        public void SetUp() => breeding = new BreedingService(care);

        private static DateTimeOffset At(double gameDays) => Epoch + GameCalendar.RealTimeFor(gameDays);

        private static PetState Adult(Sex sex, Personality personality = Personality.Calm, double grams = 55d) =>
            new PetState
            {
                Name = sex == Sex.Male ? "タロウ" : "ハナ",
                Sex = sex,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = grams,
                HatchedAtUtc = Epoch.AddDays(-12),
                Personality = personality,
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

        [Test]
        public void StartPairing_MovesTheFemaleIntoTheMalesCageForThreeGameDays()
        {
            var colony = Room(out var male, out var female);
            var maleCage = colony.CageOf(male);
            var femaleCage = colony.CageOf(female);

            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar), Is.EqualTo(PairingProblem.None));

            var pairing = colony.PairingOf(female);
            Assert.That(pairing, Is.SameAs(colony.PairingOf(male)));
            Assert.That(pairing.EndsAtUtc, Is.EqualTo(At(3d)));
            Assert.That(pairing.SuccessChance, Is.EqualTo(0.84d).Within(1e-9));
            Assert.That(maleCage.VisitorAnimalId, Is.EqualTo(female.Id));
            Assert.That(colony.VisitorIn(maleCage), Is.SameAs(female));
            Assert.That(colony.IsVisiting(female), Is.True);
            Assert.That(colony.IsVisiting(male), Is.False);
            Assert.That(colony.CageShowing(female), Is.SameAs(maleCage));
            Assert.That(colony.CageShowing(male), Is.SameAs(maleCage));
            Assert.That(colony.ResidentShown(femaleCage), Is.Null, "her own cage looks empty while she is away");
            Assert.That(femaleCage.IsEmpty, Is.False, "but it stays hers: the shop cannot put a new animal in it");
            Assert.That(colony.ShownCages(), Is.EqualTo(new[] { maleCage }));
        }

        [Test]
        public void StartPairing_RefusesOutOfSeasonBusyAndWrongPairs()
        {
            var colony = Room(out var male, out var female);
            var october = At(6 * GameCalendar.DaysPerMonth);

            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, october, calendar), Is.EqualTo(PairingProblem.OutOfSeason));
            Assert.That(breeding.StartPairing(colony, female.Id, male.Id, Epoch, calendar), Is.EqualTo(PairingProblem.NotMaleAndFemale));
            Assert.That(breeding.StartPairing(colony, male.Id, 99, Epoch, calendar), Is.EqualTo(PairingProblem.NotFound));
            Assert.That(colony.Pairings, Is.Empty);

            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar), Is.EqualTo(PairingProblem.None));
            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar), Is.EqualTo(PairingProblem.AlreadyPairing));
            Assert.That(breeding.CheckCandidate(colony, male, Epoch, calendar), Is.EqualTo(PairingProblem.AlreadyPairing));
        }

        [Test]
        public void AGravidFemale_CannotPairAgain()
        {
            var colony = Room(out var male, out var female);
            female.Gravid = new GravidState { SeasonYear = 2026 };

            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar), Is.EqualTo(PairingProblem.Gravid));
            Assert.That(breeding.CheckCandidate(colony, female, Epoch, calendar), Is.EqualTo(PairingProblem.Gravid));
            Assert.That(breeding.CheckCandidate(colony, male, Epoch, calendar), Is.EqualTo(PairingProblem.None));
            Assert.That(breeding.CheckCandidate(colony, male, At(200d), calendar), Is.EqualTo(PairingProblem.OutOfSeason));
        }

        [Test]
        public void CancelPairing_SendsTheFemaleHome()
        {
            var colony = Room(out var male, out var female);
            breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar);

            Assert.That(breeding.CancelPairing(colony, male), Is.True);

            Assert.That(colony.Pairings, Is.Empty);
            Assert.That(colony.CageOf(male).VisitorAnimalId, Is.EqualTo(-1));
            Assert.That(colony.ResidentShown(colony.CageOf(female)), Is.SameAs(female));
            Assert.That(breeding.CancelPairing(colony, male), Is.False);
        }

        [Test]
        public void RemovingAPairedAnimal_EndsThePairingAndClearsTheVisitor()
        {
            var colony = Room(out var male, out var female);
            breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar);

            colony.RemoveAnimal(male);

            Assert.That(colony.Pairings, Is.Empty);
            Assert.That(colony.Cages.TrueForAll(c => c.VisitorAnimalId == -1), Is.True);
            Assert.That(colony.ResidentShown(colony.CageOf(female)), Is.SameAs(female));
        }

        [Test]
        public void NestBox_ComesFromTheInventoryAndGoesBack()
        {
            var colony = Room(out _, out var female);
            var cage = colony.CageOf(female);

            Assert.That(breeding.PlaceNestBox(colony, cage), Is.EqualTo(NestBoxResult.NoneInInventory));
            colony.Inventory.Add(ShopCatalog.NestBoxId, 1);
            Assert.That(breeding.PlaceNestBox(colony, cage), Is.EqualTo(NestBoxResult.Ok));
            Assert.That(cage.HasNestBox, Is.True);
            Assert.That(colony.Inventory.Count(ShopCatalog.NestBoxId), Is.EqualTo(0));
            Assert.That(breeding.PlaceNestBox(colony, cage), Is.EqualTo(NestBoxResult.AlreadyPlaced));

            colony.Eggs.Add(new Egg { Id = 1, CageId = cage.Id, Place = EggPlace.NestBox });
            Assert.That(breeding.RemoveNestBox(colony, cage), Is.EqualTo(NestBoxResult.EggsInside));
            Assert.That(colony.EggsIn(cage), Has.Count.EqualTo(1));

            colony.Eggs.Clear();
            Assert.That(breeding.RemoveNestBox(colony, cage), Is.EqualTo(NestBoxResult.Ok));
            Assert.That(cage.HasNestBox, Is.False);
            Assert.That(colony.Inventory.Count(ShopCatalog.NestBoxId), Is.EqualTo(1));
            Assert.That(breeding.RemoveNestBox(colony, cage), Is.EqualTo(NestBoxResult.NotPlaced));
            Assert.That(breeding.PlaceNestBox(colony, null), Is.EqualTo(NestBoxResult.NoCage));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.BreedingServiceTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/BreedingData.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>A female visiting a male's cage for pairing (§7.3); she lives there until it ends (§6.1).</summary>
    public sealed class Pairing
    {
        public int Id { get; set; }
        public int MaleId { get; set; }
        public int FemaleId { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset EndsAtUtc { get; set; }

        /// <summary>Fixed at the start from the true personalities, health and weight (§7.3).</summary>
        public double SuccessChance { get; set; }
    }

    /// <summary>
    /// A female carrying eggs after a successful pairing (§7.4–7.6). The father's genes are
    /// copied in, so the clutch does not depend on him staying in the room.
    /// </summary>
    public sealed class GravidState
    {
        public int PairingId { get; set; }
        public int FatherId { get; set; }
        public string FatherName { get; set; } = string.Empty;
        public Genotype FatherGenotype { get; set; } = Genotype.Normal();
        public KnownGenetics FatherKnown { get; set; } = new KnownGenetics();
        public Compatibility Compatibility { get; set; } = Compatibility.Normal;

        /// <summary>The game year of the breeding season; the state ends with that season (§7.5).</summary>
        public int SeasonYear { get; set; }

        public int ClutchesPlanned { get; set; }
        public int ClutchesLaid { get; set; }
        public DateTimeOffset NextClutchAtUtc { get; set; }
    }

    public enum EggPlace
    {
        /// <summary>In the cage's nest box: develops at room temperature (§7.7).</summary>
        NestBox,

        /// <summary>On the cage floor (no nest box): dries out after two game days (§7.7).</summary>
        Loose
    }

    public enum EggFailure
    {
        None,
        Dried,
        Cold
    }

    /// <summary>
    /// One egg (§7, §8). The child's genes are fixed when it is laid; sex (incubation
    /// temperature), candling and hatching come in phase 5.
    /// </summary>
    public sealed class Egg
    {
        public int Id { get; set; }
        public int MotherId { get; set; }
        public int FatherId { get; set; }
        public int CageId { get; set; } = -1;
        public DateTimeOffset LaidAtUtc { get; set; }
        public EggPlace Place { get; set; } = EggPlace.NestBox;

        /// <summary>Hidden from the player until candling (phase 5).</summary>
        public bool Fertile { get; set; }

        public Genotype ChildGenotype { get; set; } = Genotype.Normal();
        public KnownGenetics ChildKnown { get; set; } = new KnownGenetics();

        /// <summary>0–100. Only a fertile egg at 24 ℃ or warmer develops.</summary>
        public double DevelopmentPercent { get; set; }

        /// <summary>Time-weighted temperature over the middle third of development (§5.1, used in phase 5).</summary>
        public double MiddleThirdTemperatureSum { get; set; }

        public double MiddleThirdGameDays { get; set; }

        /// <summary>Game days spent below 24 ℃ in total; enough of them kill the egg.</summary>
        public double ColdGameDays { get; set; }

        public EggFailure Failure { get; set; } = EggFailure.None;

        public bool Failed => Failure != EggFailure.None;

        /// <summary>How far this egg's development has been applied (game clock).</summary>
        public DateTimeOffset AppliedUntilUtc { get; set; }
    }
}
```

`Assets/Scripts/Core/PetState.cs`：`Weak` の次に追加：

```csharp
        /// <summary>Carrying eggs after a successful pairing (§7.4); null otherwise.</summary>
        public GravidState Gravid { get; set; }
```

`Assets/Scripts/Core/Colony.cs`：
1. `Cage` を次にする（説明コメントの「pairing comes later」も直す）：

```csharp
    /// <summary>One cage on a rack; holds one animal, plus a visiting female while pairing (§6.1).</summary>
    public sealed class Cage
    {
        public int Id { get; set; }
        public CageSize Size { get; set; } = CageSize.Standard;
        public int AnimalId { get; set; } = -1;

        /// <summary>A female visiting for pairing; -1 when none. Only ever set on the male's cage.</summary>
        public int VisitorAnimalId { get; set; } = -1;

        /// <summary>A nest box is placed here (§7.7): eggs laid in this cage stay in it.</summary>
        public bool HasNestBox { get; set; }

        public List<string> DecorIds { get; set; } = new List<string>();
        public bool IsEmpty => AnimalId < 0;
    }
```

2. `Colony` の `public TimeSpan GameClockOffset ...` の次に追加：

```csharp
        public List<Pairing> Pairings { get; set; } = new List<Pairing>();
        public List<Egg> Eggs { get; set; } = new List<Egg>();
        public int NextPairingId { get; set; } = 1;
        public int NextEggId { get; set; } = 1;

        /// <summary>Seed for BreedingRandom, from the calendar epoch (so it needs no save field).</summary>
        public int BreedingSeed => BreedingRandom.SeedOf(CalendarEpochUtc);
```

3. `OccupiedCages()` の次に追加：

```csharp
        public Pairing PairingOf(PetState pet) =>
            pet == null ? null : Pairings.Find(p => p.MaleId == pet.Id || p.FemaleId == pet.Id);

        public bool IsVisiting(PetState pet) => pet != null && Pairings.Exists(p => p.FemaleId == pet.Id);

        public PetState VisitorIn(Cage cage) => cage == null || cage.VisitorAnimalId < 0 ? null : AnimalById(cage.VisitorAnimalId);

        /// <summary>The cage an animal is in right now: the host male's while visiting, otherwise its own.</summary>
        public Cage CageShowing(PetState pet)
        {
            if (pet == null)
            {
                return null;
            }

            return Cages.Find(c => c.VisitorAnimalId == pet.Id) ?? CageOf(pet);
        }

        /// <summary>The resident a cage shows: none while that animal is away visiting (the cage looks empty but stays hers).</summary>
        public PetState ResidentShown(Cage cage)
        {
            var pet = AnimalIn(cage);
            return pet != null && IsVisiting(pet) ? null : pet;
        }

        /// <summary>Cages whose resident is at home: the ones the cage detail opens and swipes through.</summary>
        public List<Cage> ShownCages() => Cages.FindAll(c => ResidentShown(c) != null);

        public List<Egg> EggsIn(Cage cage) => cage == null ? new List<Egg>() : Eggs.FindAll(e => e.CageId == cage.Id);
```

4. `RemoveAnimal` を次にする：

```csharp
        /// <summary>
        /// Takes the animal out of the room (sold): its cage becomes empty and keeps its decor,
        /// and any pairing it was in ends (a visiting female goes home).
        /// </summary>
        public bool RemoveAnimal(PetState pet)
        {
            if (pet == null || !Animals.Remove(pet))
            {
                return false;
            }

            Pairings.RemoveAll(p => p.MaleId == pet.Id || p.FemaleId == pet.Id);
            foreach (var cage in Cages)
            {
                if (cage.AnimalId == pet.Id)
                {
                    cage.AnimalId = -1;
                    cage.VisitorAnimalId = -1;
                }

                if (cage.VisitorAnimalId == pet.Id)
                {
                    cage.VisitorAnimalId = -1;
                }
            }

            return true;
        }
```

`Assets/Scripts/Core/BreedingService.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public enum NestBoxResult
    {
        Ok,
        NoCage,
        AlreadyPlaced,
        NoneInInventory,
        NotPlaced,
        EggsInside
    }

    /// <summary>
    /// Pairing, laying and eggs (§7). Pure: time comes in as arguments and randomness from
    /// BreedingRandom. The session calls Advance on every time step.
    /// </summary>
    public sealed class BreedingService
    {
        private readonly CareTuning care;

        public BreedingService(CareTuning care)
        {
            this.care = care;
        }

        public CareTuning Care => care;

        /// <summary>§7.1 for a concrete pair in this room: both in cages, in season, one male and one female, neither busy.</summary>
        public PairingProblem CheckPair(Colony colony, PetState male, PetState female, DateTimeOffset nowUtc, GameCalendar calendar)
        {
            if (male == null || female == null || colony.CageOf(male) == null || colony.CageOf(female) == null)
            {
                return PairingProblem.NotFound;
            }

            if (!BreedingRules.IsBreedingSeason(calendar.DateAt(nowUtc), care))
            {
                return PairingProblem.OutOfSeason;
            }

            var problem = BreedingRules.CheckAnimal(male, Sex.Male, nowUtc, care);
            if (problem == PairingProblem.None)
            {
                problem = BreedingRules.CheckAnimal(female, Sex.Female, nowUtc, care);
            }

            if (problem != PairingProblem.None)
            {
                return problem;
            }

            if (female.Gravid != null)
            {
                return PairingProblem.Gravid;
            }

            return colony.PairingOf(male) != null || colony.PairingOf(female) != null
                ? PairingProblem.AlreadyPairing
                : PairingProblem.None;
        }

        /// <summary>One animal on its own, for the pairing screen's lists (its own reasons first, the season last).</summary>
        public PairingProblem CheckCandidate(Colony colony, PetState pet, DateTimeOffset nowUtc, GameCalendar calendar)
        {
            if (pet == null || colony.CageOf(pet) == null)
            {
                return PairingProblem.NotFound;
            }

            if (!pet.SexKnown)
            {
                return PairingProblem.SexUnknown;
            }

            var problem = BreedingRules.CheckAnimal(pet, pet.Sex, nowUtc, care);
            if (problem != PairingProblem.None)
            {
                return problem;
            }

            if (pet.Gravid != null)
            {
                return PairingProblem.Gravid;
            }

            if (colony.PairingOf(pet) != null)
            {
                return PairingProblem.AlreadyPairing;
            }

            return BreedingRules.IsBreedingSeason(calendar.DateAt(nowUtc), care) ? PairingProblem.None : PairingProblem.OutOfSeason;
        }

        /// <summary>Puts the female in the male's cage for PairingGameDays (§7.3). The outcome is rolled when it ends.</summary>
        public PairingProblem StartPairing(Colony colony, int maleId, int femaleId, DateTimeOffset nowUtc, GameCalendar calendar)
        {
            var male = colony.AnimalById(maleId);
            var female = colony.AnimalById(femaleId);
            var problem = CheckPair(colony, male, female, nowUtc, calendar);
            if (problem != PairingProblem.None)
            {
                return problem;
            }

            var compatibility = PersonalityTraits.CompatibilityOf(male.Personality, female.Personality);
            colony.Pairings.Add(new Pairing
            {
                Id = colony.NextPairingId++,
                MaleId = male.Id,
                FemaleId = female.Id,
                StartedAtUtc = nowUtc,
                EndsAtUtc = nowUtc + GameCalendar.RealTimeFor(care.PairingGameDays),
                SuccessChance = BreedingRules.MatingSuccess(compatibility, male, female, care),
            });
            colony.CageOf(male).VisitorAnimalId = female.Id;
            return PairingProblem.None;
        }

        /// <summary>Ends a pairing early with no outcome and sends the female home. False when the animal is not pairing.</summary>
        public bool CancelPairing(Colony colony, PetState pet)
        {
            var pairing = colony.PairingOf(pet);
            if (pairing == null)
            {
                return false;
            }

            EndPairing(colony, pairing);
            return true;
        }

        public NestBoxResult PlaceNestBox(Colony colony, Cage cage)
        {
            if (cage == null)
            {
                return NestBoxResult.NoCage;
            }

            if (cage.HasNestBox)
            {
                return NestBoxResult.AlreadyPlaced;
            }

            if (!colony.Inventory.TryTake(ShopCatalog.NestBoxId))
            {
                return NestBoxResult.NoneInInventory;
            }

            cage.HasNestBox = true;
            return NestBoxResult.Ok;
        }

        public NestBoxResult RemoveNestBox(Colony colony, Cage cage)
        {
            if (cage == null)
            {
                return NestBoxResult.NoCage;
            }

            if (!cage.HasNestBox)
            {
                return NestBoxResult.NotPlaced;
            }

            if (colony.Eggs.Exists(e => e.CageId == cage.Id && e.Place == EggPlace.NestBox))
            {
                return NestBoxResult.EggsInside;
            }

            cage.HasNestBox = false;
            colony.Inventory.Add(ShopCatalog.NestBoxId, 1);
            return NestBoxResult.Ok;
        }

        private static void EndPairing(Colony colony, Pairing pairing)
        {
            colony.Pairings.Remove(pairing);
            foreach (var cage in colony.Cages)
            {
                if (cage.VisitorAnimalId == pairing.FemaleId)
                {
                    cage.VisitorAnimalId = -1;
                }
            }
        }
    }
}
```

（`using System.Collections.Generic;` は Task 7 で使う。）

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体（`ColonyTests` の `RemoveAnimal` の既存テストも通ること）
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Add pairing records, visiting females and nest boxes"
```

---

### Task 7: ペアリングの結果・産卵のスケジュール・卵（§14 の完了条件）

**Files:**
- Create: `Assets/Scripts/Core/BreedingReport.cs`
- Create: `Assets/Scripts/Core/EggDevelopment.cs`
- Modify: `Assets/Scripts/Core/BreedingService.cs`（`Advance` と private の関数を加える）
- Test: `Assets/Tests/EditMode/BreedingServiceTests.cs`（追加）

**Interfaces:**
- Consumes: Task 5・6
- Produces: `BreedingEnd`（`Weak, SeasonOver, TooThin, AllClutchesLaid`）、`ClutchReport(Female, EggCount, InNestBox)`、`BreedingReport`（`PairingsSucceeded`・`PairingsFailed`・`PairingsCancelled`・`Clutches`・`GravidEnded`・`EggsDried`・`HasEvents`）、`EggDevelopment.MinDevelopingC/DaysToHatch(double)/Apply(Egg, double gameDays, double celsius, CareTuning)`、`BreedingService.Advance(Colony, DateTimeOffset targetUtc, GameCalendar, double roomTemperatureC) → BreedingReport`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/BreedingServiceTests.cs` のクラスの末尾に追加：

```csharp
        private static void MakeGravid(PetState female, PetState male, int planned, double firstClutchDay, int pairingId = 1)
        {
            female.Gravid = new GravidState
            {
                PairingId = pairingId,
                FatherId = male.Id,
                FatherName = male.Name,
                FatherGenotype = male.Genotype.Clone(),
                FatherKnown = male.Known.Clone(),
                Compatibility = Compatibility.Normal,
                SeasonYear = 2026,
                ClutchesPlanned = planned,
                NextClutchAtUtc = At(firstClutchDay),
            };
        }

        [Test]
        public void ASuccessfulPairing_MakesTheFemaleGravidWithAScheduleInRange()
        {
            var colony = Room(out var male, out var female);
            breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar);
            colony.PairingOf(female).SuccessChance = 1d;

            Assert.That(breeding.Advance(colony, At(2.9d), calendar, 28d).HasEvents, Is.False);
            Assert.That(colony.IsVisiting(female), Is.True);

            var report = breeding.Advance(colony, At(3d), calendar, 28d);

            Assert.That(report.PairingsSucceeded, Is.EqualTo(new[] { (female, male) }));
            Assert.That(colony.Pairings, Is.Empty);
            Assert.That(colony.CageOf(male).VisitorAnimalId, Is.EqualTo(-1));
            var gravid = female.Gravid;
            Assert.That(gravid, Is.Not.Null);
            Assert.That(gravid.FatherId, Is.EqualTo(male.Id));
            Assert.That(gravid.FatherGenotype, Is.Not.SameAs(male.Genotype));
            Assert.That(gravid.Compatibility, Is.EqualTo(Compatibility.Good));
            Assert.That(gravid.SeasonYear, Is.EqualTo(2026));
            Assert.That(gravid.ClutchesPlanned, Is.InRange(5, 9));
            Assert.That(gravid.NextClutchAtUtc, Is.InRange(At(3d + 21d), At(3d + 28d)));
        }

        [Test]
        public void AFailedPairing_LeavesTheFemaleNotGravid()
        {
            var colony = Room(out var male, out var female);
            breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar);
            colony.PairingOf(female).SuccessChance = 0d;

            var report = breeding.Advance(colony, At(3d), calendar, 28d);

            Assert.That(report.PairingsFailed, Is.EqualTo(new[] { (female, male) }));
            Assert.That(female.Gravid, Is.Null);
            Assert.That(colony.IsVisiting(female), Is.False);
        }

        [Test]
        public void APairingEndingAfterTheSeason_BearsNoFruit()
        {
            var colony = Room(out var male, out var female);
            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, At(178d), calendar), Is.EqualTo(PairingProblem.None)); // 29 September
            colony.PairingOf(female).SuccessChance = 1d;

            var report = breeding.Advance(colony, At(181d), calendar, 28d); // 2 October

            Assert.That(report.PairingsCancelled, Is.EqualTo(new[] { (female, male, BreedingEnd.SeasonOver) }));
            Assert.That(female.Gravid, Is.Null);
        }

        [Test]
        public void AWeakAnimal_EndsThePairing()
        {
            var colony = Room(out var male, out var female);
            breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar);
            male.Weak = true;

            var report = breeding.Advance(colony, At(1d), calendar, 28d);

            Assert.That(report.PairingsCancelled, Is.EqualTo(new[] { (female, male, BreedingEnd.Weak) }));
            Assert.That(colony.IsVisiting(female), Is.False);
        }

        [Test]
        public void Clutches_ComeOnScheduleAndStopAfterThePlannedNumber()
        {
            var colony = Room(out var male, out var female);
            colony.CageOf(female).HasNestBox = true;
            female.WeightGrams = 60d;
            MakeGravid(female, male, planned: 3, firstClutchDay: 30d);

            Assert.That(breeding.Advance(colony, At(29.9d), calendar, 28d).Clutches, Is.Empty);

            var first = breeding.Advance(colony, At(30d), calendar, 28d);

            Assert.That(first.Clutches, Has.Count.EqualTo(1));
            Assert.That(first.Clutches[0].Female, Is.SameAs(female));
            Assert.That(first.Clutches[0].EggCount, Is.InRange(1, 2));
            Assert.That(first.Clutches[0].InNestBox, Is.True);
            Assert.That(colony.Eggs, Has.Count.EqualTo(first.Clutches[0].EggCount));
            Assert.That(colony.Eggs.TrueForAll(e => e.Place == EggPlace.NestBox && e.LaidAtUtc == At(30d)
                && e.MotherId == female.Id && e.FatherId == male.Id && e.CageId == colony.CageOf(female).Id), Is.True);
            Assert.That(female.WeightGrams, Is.InRange(55d, 57d));
            Assert.That(female.Gravid.ClutchesLaid, Is.EqualTo(1));
            Assert.That(female.Gravid.NextClutchAtUtc, Is.InRange(At(30d + 14d), At(30d + 28d)));

            var rest = breeding.Advance(colony, At(120d), calendar, 28d); // 1 August, still in season

            Assert.That(rest.Clutches, Has.Count.EqualTo(2));
            Assert.That(rest.GravidEnded, Is.EqualTo(new[] { (female, BreedingEnd.AllClutchesLaid) }));
            Assert.That(female.Gravid, Is.Null);
        }

        [Test]
        public void OverManyClutches_IntervalsEggCountsAndTheSingleEggRateFollowTheSpec()
        {
            var singles = 0;
            var clutches = 0;
            for (var pairingId = 1; pairingId <= 60; pairingId++)
            {
                var colony = Room(out var male, out var female);
                colony.CageOf(female).HasNestBox = true;
                female.WeightGrams = 500d; // never too thin in this test
                MakeGravid(female, male, planned: 8, firstClutchDay: 30d, pairingId: pairingId);

                while (female.Gravid != null)
                {
                    var due = female.Gravid.NextClutchAtUtc;
                    var report = breeding.Advance(colony, due, calendar, 28d);
                    foreach (var clutch in report.Clutches)
                    {
                        clutches++;
                        Assert.That(clutch.EggCount, Is.InRange(1, 2));
                        if (clutch.EggCount == 1)
                        {
                            singles++;
                        }
                    }

                    if (female.Gravid != null)
                    {
                        Assert.That(GameCalendar.GameDaysBetween(due, female.Gravid.NextClutchAtUtc), Is.InRange(14d, 28d));
                    }
                }
            }

            Assert.That(clutches, Is.GreaterThan(150));
            Assert.That((double)singles / clutches, Is.InRange(0.03d, 0.2d));
        }

        [Test]
        public void AFemaleBelowFortyGrams_StopsLayingForTheSeason()
        {
            var colony = Room(out var male, out var female);
            female.WeightGrams = 42d;
            MakeGravid(female, male, planned: 6, firstClutchDay: 30d);

            var report = breeding.Advance(colony, At(30d), calendar, 28d);

            Assert.That(report.Clutches, Has.Count.EqualTo(1), "she still lays this clutch");
            Assert.That(female.WeightGrams, Is.LessThan(40d));
            Assert.That(report.GravidEnded, Is.EqualTo(new[] { (female, BreedingEnd.TooThin) }));
            Assert.That(female.Gravid, Is.Null);

            var thinRoom = Room(out var male2, out var female2);
            female2.WeightGrams = 39d;
            MakeGravid(female2, male2, planned: 6, firstClutchDay: 30d);

            var none = breeding.Advance(thinRoom, At(30d), calendar, 28d);

            Assert.That(none.Clutches, Is.Empty);
            Assert.That(none.GravidEnded, Is.EqualTo(new[] { (female2, BreedingEnd.TooThin) }));
        }

        [Test]
        public void TheEndOfTheSeason_EndsTheGravidState()
        {
            var colony = Room(out var male, out var female);
            MakeGravid(female, male, planned: 8, firstClutchDay: 175d); // 26 September

            var report = breeding.Advance(colony, At(185d), calendar, 28d); // 6 October

            Assert.That(report.Clutches, Has.Count.EqualTo(1));
            Assert.That(report.GravidEnded, Is.EqualTo(new[] { (female, BreedingEnd.SeasonOver) }));
            Assert.That(female.Gravid, Is.Null);
        }

        [Test]
        public void AWeakGravidFemale_StopsBeingGravid()
        {
            var colony = Room(out var male, out var female);
            MakeGravid(female, male, planned: 6, firstClutchDay: 30d);
            female.Weak = true;

            var report = breeding.Advance(colony, At(10d), calendar, 28d);

            Assert.That(report.GravidEnded, Is.EqualTo(new[] { (female, BreedingEnd.Weak) }));
            Assert.That(female.Gravid, Is.Null);
        }

        [Test]
        public void WithoutANestBox_EggsDryUpAfterTwoGameDays()
        {
            var colony = Room(out var male, out var female);
            MakeGravid(female, male, planned: 1, firstClutchDay: 30d);

            var laid = breeding.Advance(colony, At(30d), calendar, 28d);
            var count = colony.Eggs.Count;

            Assert.That(laid.Clutches[0].InNestBox, Is.False);
            Assert.That(colony.Eggs.TrueForAll(e => e.Place == EggPlace.Loose), Is.True);
            Assert.That(breeding.Advance(colony, At(31.9d), calendar, 28d).EggsDried, Is.Empty);

            var dried = breeding.Advance(colony, At(32d), calendar, 28d);

            Assert.That(dried.EggsDried, Has.Count.EqualTo(count));
            Assert.That(dried.EggsDried.TrueForAll(e => e.Failure == EggFailure.Dried), Is.True);
            Assert.That(colony.Eggs, Is.Empty);
        }

        [Test]
        public void NestBoxEggs_DevelopAtRoomTemperatureUpToTheOfflineCap()
        {
            var colony = Room(out var male, out var female);
            colony.CageOf(female).HasNestBox = true;
            MakeGravid(female, male, planned: 1, firstClutchDay: 30d);
            breeding.Advance(colony, At(30d), calendar, 28d);
            colony.Eggs.ForEach(e => e.Fertile = true);

            breeding.Advance(colony, At(40d), calendar, 28d); // 10 game days = 8 real hours

            Assert.That(colony.Eggs[0].DevelopmentPercent, Is.EqualTo(100d * 10d / 60d).Within(1e-6));

            breeding.Advance(colony, At(70d), calendar, 28d); // 30 game days away, but at most 12 real hours (15 game days) apply

            Assert.That(colony.Eggs[0].DevelopmentPercent, Is.EqualTo(100d * 25d / 60d).Within(1e-6));
        }

        [Test]
        public void InfertileEggsDoNotDevelopAndColdKillsFertileOnes()
        {
            var colony = Room(out var male, out var female);
            colony.CageOf(female).HasNestBox = true;
            MakeGravid(female, male, planned: 1, firstClutchDay: 30d);
            breeding.Advance(colony, At(30d), calendar, 28d);
            colony.Eggs.ForEach(e => e.Fertile = false);

            breeding.Advance(colony, At(40d), calendar, 28d);
            Assert.That(colony.Eggs[0].DevelopmentPercent, Is.EqualTo(0d));

            colony.Eggs.ForEach(e => e.Fertile = true);
            breeding.Advance(colony, At(42d), calendar, 20d);
            Assert.That(colony.Eggs[0].ColdGameDays, Is.EqualTo(2d).Within(1e-9));
            Assert.That(colony.Eggs[0].Failed, Is.False);

            breeding.Advance(colony, At(43d), calendar, 20d);
            Assert.That(colony.Eggs[0].Failure, Is.EqualTo(EggFailure.Cold));
            Assert.That(colony.Eggs[0].DevelopmentPercent, Is.EqualTo(0d));
        }

        [Test]
        public void EggsCarryTheChildsGenesAndWhatThePlayerKnows()
        {
            var colony = Room(out var male, out var female);
            colony.CageOf(female).HasNestBox = true;
            female.Genotype = Genotype.Normal().Set(GeneId.Eclipse, 2);
            MakeGravid(female, male, planned: 1, firstClutchDay: 30d);

            breeding.Advance(colony, At(30d), calendar, 28d);

            Assert.That(colony.Eggs, Is.Not.Empty);
            foreach (var egg in colony.Eggs)
            {
                Assert.That(egg.ChildGenotype.Copies(GeneId.Eclipse), Is.EqualTo(1));
                Assert.That(egg.ChildKnown.HetProbability(GeneId.Eclipse), Is.EqualTo(1d));
                Assert.That(MorphNamer.FullName(egg.ChildGenotype, egg.ChildKnown), Does.Contain("ヘテロエクリプス"));
            }
        }

        [TestCase(24d, 76d)]
        [TestCase(28d, 60d)]
        [TestCase(29d, 56d)]
        [TestCase(30d, 52d)]
        [TestCase(32d, 45d)]
        public void DaysToHatch_FollowsTheIncubationTable(double celsius, double days)
        {
            Assert.That(EggDevelopment.DaysToHatch(celsius), Is.EqualTo(days).Within(1e-9));
        }

        [Test]
        public void TheMiddleThirdRecordsItsAverageTemperature()
        {
            var egg = new Egg { Fertile = true, DevelopmentPercent = 30d };

            EggDevelopment.Apply(egg, 6.8d, 26d, care); // 26 ℃: 68 days to hatch, so +10 %

            Assert.That(egg.DevelopmentPercent, Is.EqualTo(40d).Within(1e-9));
            Assert.That(egg.MiddleThirdGameDays, Is.EqualTo((40d - 100d / 3d) * 68d / 100d).Within(1e-9));
            Assert.That(egg.MiddleThirdTemperatureSum / egg.MiddleThirdGameDays, Is.EqualTo(26d).Within(1e-9));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.BreedingServiceTests`
Expected: コンパイルエラー（`Advance`・`BreedingEnd`・`EggDevelopment` がない）。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/BreedingReport.cs`:

```csharp
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Why a pairing or a gravid state ended.</summary>
    public enum BreedingEnd
    {
        Weak,
        SeasonOver,
        TooThin,
        AllClutchesLaid
    }

    public sealed class ClutchReport
    {
        public ClutchReport(PetState female, int eggCount, bool inNestBox)
        {
            Female = female;
            EggCount = eggCount;
            InNestBox = inNestBox;
        }

        public PetState Female { get; }

        public int EggCount { get; }

        public bool InNestBox { get; }
    }

    /// <summary>What happened in breeding during one time step, for notifications.</summary>
    public sealed class BreedingReport
    {
        public List<(PetState Female, PetState Male)> PairingsSucceeded { get; } = new List<(PetState, PetState)>();

        public List<(PetState Female, PetState Male)> PairingsFailed { get; } = new List<(PetState, PetState)>();

        public List<(PetState Female, PetState Male, BreedingEnd Reason)> PairingsCancelled { get; } =
            new List<(PetState, PetState, BreedingEnd)>();

        public List<ClutchReport> Clutches { get; } = new List<ClutchReport>();

        public List<(PetState Female, BreedingEnd Reason)> GravidEnded { get; } = new List<(PetState, BreedingEnd)>();

        public List<Egg> EggsDried { get; } = new List<Egg>();

        public bool HasEvents => PairingsSucceeded.Count > 0 || PairingsFailed.Count > 0 || PairingsCancelled.Count > 0
            || Clutches.Count > 0 || GravidEnded.Count > 0 || EggsDried.Count > 0;
    }
}
```

`Assets/Scripts/Core/EggDevelopment.cs`:

```csharp
using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Egg development at a temperature (§8 days to hatch, §5.1 middle third). Phase 4 uses it
    /// for nest-box eggs at room temperature (§7.7); phase 5 adds the incubator.
    /// </summary>
    public static class EggDevelopment
    {
        /// <summary>Below this an egg does not develop and, given long enough, dies (§5.1).</summary>
        public const double MinDevelopingC = 24d;

        private const double MiddleThirdStart = 100d / 3d;
        private const double MiddleThirdEnd = 200d / 3d;

        /// <summary>§8: 60 days at 28 ℃, 52 at 30 ℃, 45 at 32 ℃; straight lines between, continued beyond.</summary>
        public static double DaysToHatch(double celsius) =>
            celsius <= 30d ? 52d + (30d - celsius) * 4d : 52d - (celsius - 30d) * 3.5d;

        public static void Apply(Egg egg, double gameDays, double celsius, CareTuning care)
        {
            if (gameDays <= 0d || egg.Failed || !egg.Fertile)
            {
                return;
            }

            if (celsius < MinDevelopingC)
            {
                egg.ColdGameDays += gameDays;
                if (egg.ColdGameDays >= care.EggColdFailGameDays)
                {
                    egg.Failure = EggFailure.Cold;
                }

                return;
            }

            var daysToHatch = DaysToHatch(celsius);
            var before = egg.DevelopmentPercent;
            var after = Math.Min(100d, before + gameDays / daysToHatch * 100d);
            egg.DevelopmentPercent = after;

            var overlap = Math.Min(after, MiddleThirdEnd) - Math.Max(before, MiddleThirdStart);
            if (overlap > 0d)
            {
                var days = overlap * daysToHatch / 100d;
                egg.MiddleThirdGameDays += days;
                egg.MiddleThirdTemperatureSum += celsius * days;
            }
        }
    }
}
```

`Assets/Scripts/Core/BreedingService.cs`：`CancelPairing` の次に `Advance` を、`EndPairing` の前に private の関数を加える：

```csharp
        /// <summary>
        /// Brings breeding up to targetUtc (game clock): pairings that ended, clutches that were
        /// due, the season's end, and the eggs. Scheduled events fire at their own times however
        /// long the gap; egg development applies at most MaxOfflineProgressHours at once, like
        /// the animals' care values.
        /// </summary>
        public BreedingReport Advance(Colony colony, DateTimeOffset targetUtc, GameCalendar calendar, double roomTemperatureC)
        {
            var report = new BreedingReport();
            ResolvePairings(colony, targetUtc, calendar, report);
            foreach (var female in colony.Animals)
            {
                LayClutches(colony, female, targetUtc, calendar, report);
            }

            AdvanceEggs(colony, targetUtc, roomTemperatureC, report);
            return report;
        }
```

```csharp
        private void ResolvePairings(Colony colony, DateTimeOffset targetUtc, GameCalendar calendar, BreedingReport report)
        {
            foreach (var pairing in colony.Pairings.ToArray())
            {
                var male = colony.AnimalById(pairing.MaleId);
                var female = colony.AnimalById(pairing.FemaleId);
                if (male == null || female == null)
                {
                    EndPairing(colony, pairing);
                    continue;
                }

                if (male.Weak || female.Weak)
                {
                    EndPairing(colony, pairing);
                    report.PairingsCancelled.Add((female, male, BreedingEnd.Weak));
                    continue;
                }

                if (pairing.EndsAtUtc > targetUtc)
                {
                    continue;
                }

                EndPairing(colony, pairing);
                var season = BreedingRules.SeasonOf(calendar.DateAt(pairing.EndsAtUtc), care);
                if (season < 0)
                {
                    report.PairingsCancelled.Add((female, male, BreedingEnd.SeasonOver));
                    continue;
                }

                var random = BreedingRandom.For(colony.BreedingSeed, BreedingRandom.PairingStream, pairing.Id, 0);
                if (random.NextDouble() >= pairing.SuccessChance)
                {
                    report.PairingsFailed.Add((female, male));
                    continue;
                }

                var compatibility = PersonalityTraits.CompatibilityOf(male.Personality, female.Personality);
                var clutches = BreedingRules.ClutchRange(compatibility, care);
                female.Gravid = new GravidState
                {
                    PairingId = pairing.Id,
                    FatherId = male.Id,
                    FatherName = male.Name,
                    FatherGenotype = male.Genotype.Clone(),
                    FatherKnown = male.Known.Clone(),
                    Compatibility = compatibility,
                    SeasonYear = season,
                    ClutchesPlanned = random.Next(clutches.Min, clutches.Max + 1),
                    NextClutchAtUtc = pairing.EndsAtUtc + GameCalendar.RealTimeFor(
                        BreedingRandom.Uniform(random, care.FirstClutchMinGameDays, care.FirstClutchMaxGameDays)),
                };
                report.PairingsSucceeded.Add((female, male));
            }
        }

        private void LayClutches(Colony colony, PetState female, DateTimeOffset targetUtc, GameCalendar calendar, BreedingReport report)
        {
            while (female.Gravid != null && female.Gravid.NextClutchAtUtc <= targetUtc)
            {
                var gravid = female.Gravid;
                var at = gravid.NextClutchAtUtc;
                if (BreedingRules.SeasonOf(calendar.DateAt(at), care) != gravid.SeasonYear)
                {
                    EndGravid(female, BreedingEnd.SeasonOver, report);
                    return;
                }

                if (female.Weak)
                {
                    EndGravid(female, BreedingEnd.Weak, report);
                    return;
                }

                if (female.WeightGrams < care.LayingStopWeightGrams)
                {
                    EndGravid(female, BreedingEnd.TooThin, report);
                    return;
                }

                Lay(colony, female, gravid, at, report);

                if (gravid.ClutchesLaid >= gravid.ClutchesPlanned)
                {
                    EndGravid(female, BreedingEnd.AllClutchesLaid, report);
                    return;
                }

                if (female.WeightGrams < care.LayingStopWeightGrams)
                {
                    EndGravid(female, BreedingEnd.TooThin, report);
                    return;
                }
            }

            if (female.Gravid == null)
            {
                return;
            }

            if (BreedingRules.SeasonOf(calendar.DateAt(targetUtc), care) != female.Gravid.SeasonYear)
            {
                EndGravid(female, BreedingEnd.SeasonOver, report);
            }
            else if (female.Weak)
            {
                EndGravid(female, BreedingEnd.Weak, report);
            }
        }

        private void Lay(Colony colony, PetState female, GravidState gravid, DateTimeOffset at, BreedingReport report)
        {
            var random = BreedingRandom.For(colony.BreedingSeed, BreedingRandom.ClutchStream, gravid.PairingId, gravid.ClutchesLaid);
            var cage = colony.CageOf(female);
            var inNestBox = cage != null && cage.HasNestBox;
            var count = random.NextDouble() < care.SingleEggChance ? 1 : 2;
            var fertility = BreedingRules.FertilityFor(gravid.Compatibility, care);
            for (var i = 0; i < count; i++)
            {
                var child = GeneticsCalculator.Breed(female.Genotype, gravid.FatherGenotype, random);
                colony.Eggs.Add(new Egg
                {
                    Id = colony.NextEggId++,
                    MotherId = female.Id,
                    FatherId = gravid.FatherId,
                    CageId = cage != null ? cage.Id : -1,
                    LaidAtUtc = at,
                    AppliedUntilUtc = at,
                    Place = inNestBox ? EggPlace.NestBox : EggPlace.Loose,
                    Fertile = random.NextDouble() < fertility,
                    ChildGenotype = child,
                    ChildKnown = KnownGenetics.ForChild(female.Genotype, female.Known, gravid.FatherGenotype, gravid.FatherKnown, child),
                });
            }

            female.WeightGrams -= BreedingRandom.Uniform(random, care.ClutchWeightLossMinGrams, care.ClutchWeightLossMaxGrams);
            gravid.ClutchesLaid++;
            gravid.NextClutchAtUtc = at + GameCalendar.RealTimeFor(
                BreedingRandom.Uniform(random, care.ClutchIntervalMinGameDays, care.ClutchIntervalMaxGameDays));
            report.Clutches.Add(new ClutchReport(female, count, inNestBox));
        }

        private void AdvanceEggs(Colony colony, DateTimeOffset targetUtc, double roomTemperatureC, BreedingReport report)
        {
            var dryAfter = GameCalendar.RealTimeFor(care.LooseEggDryGameDays);
            var cap = TimeSpan.FromHours(care.MaxOfflineProgressHours);
            foreach (var egg in colony.Eggs.ToArray())
            {
                if (egg.Place == EggPlace.Loose)
                {
                    if (targetUtc >= egg.LaidAtUtc + dryAfter)
                    {
                        egg.Failure = EggFailure.Dried;
                        colony.Eggs.Remove(egg);
                        report.EggsDried.Add(egg);
                    }

                    continue;
                }

                var elapsed = targetUtc - egg.AppliedUntilUtc;
                if (elapsed <= TimeSpan.Zero)
                {
                    continue;
                }

                egg.AppliedUntilUtc = targetUtc;
                var applied = elapsed > cap ? cap : elapsed;
                EggDevelopment.Apply(egg, applied.TotalMinutes / GameCalendar.RealMinutesPerGameDay, roomTemperatureC, care);
            }
        }

        private static void EndGravid(PetState female, BreedingEnd reason, BreedingReport report)
        {
            female.Gravid = null;
            report.GravidEnded.Add((female, reason));
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体
Expected: PASS（これで §14「繁殖期の条件と産卵スケジュールがテストで確認できる」を満たす）

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Resolve pairings, lay clutches on schedule and develop nest-box eggs"
```

---

### Task 8: 繁殖の文字（`BreedingText`）

**Files:**
- Create: `Assets/Scripts/UI/BreedingText.cs`
- Test: `Assets/Tests/EditMode/BreedingTextTests.cs`（新規）

**Interfaces:**
- Consumes: Task 3〜7
- Produces: `BreedingText`（下のコード。画面のタスク 10〜14 はここの文字だけを使う）

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/BreedingTextTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-unity-tests.sh EditMode -testFilter TerrariumDays.Tests.BreedingTextTests`
Expected: コンパイルエラー。

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/UI/BreedingText.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>Every breeding and weakness text on screen (§7, §5.5). Pure, so it is EditMode tested.</summary>
    public static class BreedingText
    {
        public const string WeakStatus = "衰弱中（繁殖できません）";
        public const string NestBoxPlacedMessage = "産卵床を置きました";
        public const string NestBoxRemovedMessage = "産卵床を外して所持品に戻しました";
        public const string HatchComesLaterLine = "孵化は段階5（孵卵器）で追加されます";

        public static string ProblemLabel(PairingProblem problem)
        {
            switch (problem)
            {
                case PairingProblem.NotFound:
                    return "個体が見つかりません";
                case PairingProblem.OutOfSeason:
                    return "繁殖期ではありません";
                case PairingProblem.NotMaleAndFemale:
                    return "オスとメスを1匹ずつ選んでください";
                case PairingProblem.SexUnknown:
                    return "性別がまだ分かりません";
                case PairingProblem.Weak:
                    return "衰弱中です";
                case PairingProblem.TooYoung:
                    return "まだ若すぎます";
                case PairingProblem.TooLight:
                    return "体重が足りません";
                case PairingProblem.Gravid:
                    return "抱卵中です";
                case PairingProblem.AlreadyPairing:
                    return "ペアリング中です";
                default:
                    return string.Empty;
            }
        }

        public static string Requirement(Sex role, CareTuning care) => role == Sex.Female
            ? $"メス：生後{Whole(care.FemaleBreedingMinAgeMonths)}か月以上・{Whole(care.FemaleBreedingMinWeightGrams)}g以上"
            : $"オス：生後{Whole(care.MaleBreedingMinAgeMonths)}か月以上・{Whole(care.MaleBreedingMinWeightGrams)}g以上";

        public static string SeasonLine(GameDate today, CareTuning care) => BreedingRules.IsBreedingSeason(today, care)
            ? $"いまは繁殖期です（{care.BreedingSeasonFirstMonth}〜{care.BreedingSeasonLastMonth}月）"
            : $"繁殖期は{care.BreedingSeasonFirstMonth}〜{care.BreedingSeasonLastMonth}月です（いまは{today.Month}月）";

        public static string CompatibilityLabel(BreedingForecast forecast)
        {
            if (!forecast.CompatibilityKnown)
            {
                return "不明（性格がまだ分かりません）";
            }

            switch (forecast.Compatibility)
            {
                case Compatibility.Good:
                    return "良い";
                case Compatibility.Bad:
                    return "悪い";
                default:
                    return "普通";
            }
        }

        /// <summary>A probability as a whole percent; tiny non-zero chances read "1%未満".</summary>
        public static string Percent(double probability)
        {
            if (probability > 0d && probability < 0.005d)
            {
                return "1%未満";
            }

            return Whole(probability * 100d) + "%";
        }

        public static List<string> ForecastLines(BreedingForecast forecast)
        {
            var lines = new List<string>
            {
                $"相性：{CompatibilityLabel(forecast)}",
                $"交尾の成功率：約{Percent(forecast.SuccessChance)}",
                $"産卵：{forecast.MinClutches}〜{forecast.MaxClutches}回（1回に2個、ときどき1個）",
                "子の見込み（分かっている遺伝から）：",
            };
            foreach (var odds in forecast.Offspring)
            {
                lines.Add($"・{odds.Name} {Percent(odds.Probability)}");
            }

            return lines;
        }

        public static string ConfirmPairing(PetState male, PetState female, CareTuning care) =>
            $"{female.Name}を{male.Name}のケージに入れて、ゲーム内{Whole(care.PairingGameDays)}日間ペアリングしますか？";

        public static string CandidateLine(PetState pet, DateTimeOffset nowUtc) =>
            $"{pet.Name}　{pet.WeightGrams.ToString("0.0", CultureInfo.InvariantCulture)}g・生後{(int)Math.Floor(GrowthModel.AgeMonths(pet, nowUtc))}か月";

        /// <summary>Whole game days left, rounded up ("あと3日"); never negative.</summary>
        public static int DaysLeft(DateTimeOffset untilUtc, DateTimeOffset nowUtc) =>
            Math.Max(0, (int)Math.Ceiling(GameCalendar.GameDaysBetween(nowUtc, untilUtc) - 1e-9));

        public static string PairingLine(Pairing pairing, PetState male, PetState female, DateTimeOffset nowUtc) =>
            $"{female.Name}が{male.Name}のケージを訪問中（あと{DaysLeft(pairing.EndsAtUtc, nowUtc)}日）";

        public static string VisitorLabel(PetState visitor, Pairing pairing, DateTimeOffset nowUtc) =>
            $"訪問中：{visitor.Name}（あと{DaysLeft(pairing.EndsAtUtc, nowUtc)}日）";

        public static string GravidStatus(GravidState gravid, GameCalendar calendar)
        {
            var date = calendar.DateAt(gravid.NextClutchAtUtc);
            return $"抱卵中（{gravid.ClutchesLaid + 1}回目の産卵は{date.Month}月{date.Day}日ごろ）";
        }

        public static string GravidLine(PetState female, GameCalendar calendar) =>
            $"{female.Name}：{GravidStatus(female.Gravid, calendar)}";

        /// <summary>The egg lines for one cage (simple status only; candling is phase 5).</summary>
        public static List<string> EggLines(IList<Egg> eggsInCage, RoomClimate room, DateTimeOffset nowUtc, CareTuning care)
        {
            var lines = new List<string>();
            var nest = 0;
            var hatchReady = false;
            var loose = 0;
            var firstLooseLaid = DateTimeOffset.MaxValue;
            foreach (var egg in eggsInCage)
            {
                if (egg.Place == EggPlace.NestBox)
                {
                    nest++;
                    hatchReady |= egg.DevelopmentPercent >= 100d;
                }
                else
                {
                    loose++;
                    if (egg.LaidAtUtc < firstLooseLaid)
                    {
                        firstLooseLaid = egg.LaidAtUtc;
                    }
                }
            }

            if (nest > 0)
            {
                var temperature = RoomTemperature.Label(room.TemperatureC, room.Measured);
                lines.Add(room.TemperatureC < EggDevelopment.MinDevelopingC
                    ? $"産卵床に卵が{nest}個あります。{temperature}では寒くて発生が止まっています"
                    : $"産卵床に卵が{nest}個あります。{temperature}で発生中");
                if (hatchReady)
                {
                    lines.Add(HatchComesLaterLine);
                }
            }

            if (loose > 0)
            {
                var dryAt = firstLooseLaid + GameCalendar.RealTimeFor(care.LooseEggDryGameDays);
                var minutes = Math.Max(0, (int)Math.Ceiling((dryAt - nowUtc).TotalMinutes - 1e-9));
                lines.Add($"産卵床の外に卵が{loose}個（あと約{minutes}分で乾いてしまいます）");
            }

            return lines;
        }

        /// <summary>Breeding alerts for a home/list thumbnail, most important first.</summary>
        public static List<string> Alerts(Colony colony, Cage cage, PetState shown)
        {
            var alerts = new List<string>();
            if (shown != null && shown.Weak)
            {
                alerts.Add("衰弱");
            }

            if (colony.VisitorIn(cage) != null)
            {
                alerts.Add("ペアリング中");
            }

            if (shown != null && shown.Gravid != null)
            {
                alerts.Add("抱卵中");
                if (!cage.HasNestBox)
                {
                    alerts.Add("産卵床なし");
                }
            }

            if (colony.EggsIn(cage).Count > 0)
            {
                alerts.Add("卵あり");
            }

            return alerts;
        }

        /// <summary>The breeding lines on the cage detail screen: weakness, visitor, gravid, eggs.</summary>
        public static List<string> CageDetailLines(Colony colony, Cage cage, PetState shown, GameCalendar calendar, RoomClimate room,
            DateTimeOffset nowUtc, CareTuning care)
        {
            var lines = new List<string>();
            if (shown != null && shown.Weak)
            {
                lines.Add(WeakStatus);
            }

            var visitor = colony.VisitorIn(cage);
            var pairing = colony.PairingOf(visitor);
            if (visitor != null && pairing != null)
            {
                lines.Add(VisitorLabel(visitor, pairing, nowUtc));
            }

            if (shown != null && shown.Gravid != null)
            {
                lines.Add(GravidStatus(shown.Gravid, calendar) + (cage.HasNestBox ? string.Empty : "・産卵床がありません"));
            }

            lines.AddRange(EggLines(colony.EggsIn(cage), room, nowUtc, care));
            return lines;
        }

        public static List<string> Messages(BreedingReport report, CareTuning care)
        {
            var messages = new List<string>();
            foreach (var (female, _) in report.PairingsSucceeded)
            {
                messages.Add($"ペアリング成功！{female.Name}が抱卵しました（産卵はゲーム内3〜4週間後から）");
            }

            foreach (var (female, male) in report.PairingsFailed)
            {
                messages.Add($"{female.Name}と{male.Name}のペアリングはうまくいきませんでした");
            }

            foreach (var (female, male, reason) in report.PairingsCancelled)
            {
                messages.Add(reason == BreedingEnd.Weak
                    ? $"衰弱したため、{female.Name}と{male.Name}のペアリングを中止しました"
                    : $"繁殖期が終わったため、{female.Name}と{male.Name}のペアリングは実りませんでした");
            }

            foreach (var clutch in report.Clutches)
            {
                messages.Add(clutch.InNestBox
                    ? $"{clutch.Female.Name}が産卵床に卵を{clutch.EggCount}個産みました"
                    : $"{clutch.Female.Name}が卵を{clutch.EggCount}個産みました。産卵床がないので、ゲーム内{Whole(care.LooseEggDryGameDays)}日で乾いてしまいます");
            }

            foreach (var (female, reason) in report.GravidEnded)
            {
                messages.Add(GravidEndMessage(female, reason, care));
            }

            if (report.EggsDried.Count > 0)
            {
                messages.Add($"産卵床がなかったため、卵{report.EggsDried.Count}個が乾いてだめになりました");
            }

            return messages;
        }

        public static string GravidEndMessage(PetState female, BreedingEnd reason, CareTuning care)
        {
            switch (reason)
            {
                case BreedingEnd.AllClutchesLaid:
                    return $"{female.Name}の今シーズンの産卵が終わりました";
                case BreedingEnd.TooThin:
                    return $"{female.Name}は体重が{Whole(care.LayingStopWeightGrams)}gを下回ったので、今シーズンの産卵を終えました";
                case BreedingEnd.Weak:
                    return $"{female.Name}は衰弱したので、抱卵が終わりました";
                default:
                    return $"繁殖期が終わり、{female.Name}の抱卵は終わりました";
            }
        }

        public static string WeakStartedMessage(PetState pet, CareTuning care) =>
            $"{pet.Name}が衰弱しました（健康が{Whole(care.WeakRecoveryHealth)}に戻るまで繁殖できません）";

        public static string WeakRecoveredMessage(PetState pet) => $"{pet.Name}の衰弱が治りました";

        /// <summary>One feedback line: the first message, plus how many more there were.</summary>
        public static string Summary(IList<string> messages) =>
            messages.Count == 0 ? string.Empty
            : messages.Count == 1 ? messages[0]
            : $"{messages[0]}（ほか{messages.Count - 1}件）";

        public static string NestBoxButtonLabel(Cage cage, Inventory inventory) =>
            cage.HasNestBox ? "産卵床を外す" : $"産卵床を置く（所持{inventory.Count(ShopCatalog.NestBoxId)}）";

        public static string NestBoxMessage(NestBoxResult result)
        {
            switch (result)
            {
                case NestBoxResult.Ok:
                    return string.Empty;
                case NestBoxResult.NoneInInventory:
                    return "産卵床を持っていません（ショップの「用品」で買えます）";
                case NestBoxResult.EggsInside:
                    return "卵が入っているので外せません";
                case NestBoxResult.AlreadyPlaced:
                    return "産卵床はもう置いてあります";
                case NestBoxResult.NotPlaced:
                    return "産卵床は置いていません";
                default:
                    return "ケージが見つかりません";
            }
        }

        private static string Whole(double value) =>
            Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode 全体
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI Assets/Tests/EditMode
git commit -m "Add the breeding and weakness texts"
```

---

### Task 9: セッションとセーブ（繁殖・衰弱の適用と保存、ペアリング中の卸売り）

**Files:**
- Modify: `Assets/Scripts/Core/ColonySession.cs`（`ColonyTickReport` 6行、`ApplyUntil` 126行）
- Modify: `Assets/Scripts/Core/ColonySaveData.cs`
- Modify: `Assets/Scripts/Core/ColonySaveService.cs`（`FromSaveData` 184行、`FromAnimalSaveData` 296行、`ToSaveData` 373行、`ToAnimalSaveData` 435行）
- Modify: `Assets/Scripts/Core/ShopService.cs`（`ShopResult`・`Wholesale`）・`Assets/Scripts/UI/ShopText.cs`（`FailureMessage`）
- Test: `Assets/Tests/EditMode/ColonySessionTests.cs`・`ColonySaveServiceTests.cs`・`ShopServiceTests.cs`・`ShopAndLedgerTextTests.cs`（追加）

**Interfaces:**
- Consumes: Task 1〜8
- Produces: `ColonyTickReport.WeakStarted`・`WeakRecovered`（`List<PetState>`）・`Breeding`（`BreedingReport`）、`HasEvents` にそれらを含める。`ColonySession.Breeding`（`BreedingService`、UI はこれを使う）。`ShopResult.InPairing`（enum の最後に追加）と `ShopText.FailureMessage(InPairing)` =「ペアリング中の個体は売れません」。

**セッション（`ColonySession`）：**

```csharp
        // フィールドとコンストラクタ
        private readonly BreedingService breeding;
        // ctor の最後:  breeding = new BreedingService(care);
        public BreedingService Breeding => breeding;

        // ApplyUntil(): 各個体のループの中（SexReveals の後）
                if (result.BecameWeak)
                {
                    report.WeakStarted.Add(pet);
                }

                if (result.RecoveredFromWeak)
                {
                    report.WeakRecovered.Add(pet);
                }

        // ApplyUntil(): 個体のループの後、電気代の前
            report.Breeding = breeding.Advance(Colony, targetUtc, Calendar, Room.TemperatureC);
```

`ColonyTickReport` に `public List<PetState> WeakStarted { get; } = new List<PetState>();`・`public List<PetState> WeakRecovered { get; } = new List<PetState>();`・`public BreedingReport Breeding { get; set; } = new BreedingReport();` を加え、`HasEvents` に `|| WeakStarted.Count > 0 || WeakRecovered.Count > 0 || Breeding.HasEvents` を加える。

**保存の形（JsonUtility、すべてフィールド。入れ子のクラスは null を書けないので、抱卵は個体の外の一覧にする）：**

```csharp
    // ColonySaveData に追加
        public int nextPairingId;
        public int nextEggId;
        public List<PairingSaveData> pairings = new List<PairingSaveData>();
        public List<GravidSaveData> gravidStates = new List<GravidSaveData>();
        public List<EggSaveData> eggs = new List<EggSaveData>();

    // AnimalSaveData に追加
        public bool weak;

    // CageSaveData に追加
        public bool hasNestBox;

    [Serializable]
    public sealed class PairingSaveData
    {
        public int id;
        public int maleId;
        public int femaleId;
        public string startedAtUtc;
        public string endsAtUtc;
        public double successChance;
    }

    [Serializable]
    public sealed class GravidSaveData
    {
        public int animalId;
        public int pairingId;
        public int fatherId;
        public string fatherName;
        public List<GeneSaveData> fatherGenes;
        public double fatherHypo;
        public double fatherTangerine;
        public List<HetSaveData> fatherHets;
        public bool fatherHetsUnknown;
        public string compatibility;
        public int seasonYear;
        public int clutchesPlanned;
        public int clutchesLaid;
        public string nextClutchAtUtc;
    }

    [Serializable]
    public sealed class EggSaveData
    {
        public int id;
        public int motherId;
        public int fatherId;
        public int cageId;
        public string laidAtUtc;
        public string place;
        public bool fertile;
        public List<GeneSaveData> genes;
        public double hypo;
        public double tangerine;
        public List<HetSaveData> hets;
        public bool hetsUnknown;
        public double developmentPercent;
        public double middleThirdTemperatureSum;
        public double middleThirdGameDays;
        public double coldGameDays;
        public string failure;
        public string appliedUntilUtc;
    }
```

**読み込み・保存の規則（`ColonySaveService`）：**
1. 遺伝子の変換を4つの private 関数に切り出し、個体・父・卵で共用する：`GenesToSave(Genotype)`・`HetsToSave(KnownGenetics)`（今の `ToAnimalSaveData` のループ）、`GenotypeFrom(List<GeneSaveData> genes, double hypo, double tangerine)`・`KnownFrom(List<HetSaveData> hets, bool hetsUnknown)`（今の `FromAnimalSaveData` の `genomeVersion >= 1` の枝。`TryParseDefined` を使い、null の一覧は空として扱う）。個体の読み書きの結果は変えない（既存テストで確かめる）。
2. `weak` ⇔ `PetState.Weak`、`hasNestBox` ⇔ `Cage.HasNestBox`。
3. ペアリング：オス・メスが個体の一覧にいて、どちらもケージを持ち、同じ個体でなく、どちらもまだ別のペアリングに入っていないときだけ読む（それ以外は捨てる）。読んだら `colony.CageOf(male).VisitorAnimalId = femaleId`（訪問者は保存せず、ペアリングから作り直す）。
4. 抱卵：`animalId` の個体がいてメスのときだけ `Gravid` に入れる。`compatibility` は `TryParseDefined`（読めなければ `Normal`）。
5. 卵：`place` は読めなければ `NestBox`、`failure` は読めなければ `None`。時刻は `Parse(…, nowUtc)`。
6. `NextPairingId = max(1, data.nextPairingId, 最大のペアリング番号＋1)`、`NextEggId` も同じ（段階3までのセーブは0なので1になる）。
7. 保存は上の全部を書く（`nextPairingId`・`nextEggId`・一覧）。

**卸売り：** `ShopResult` の最後に `InPairing` を加え、`ShopService.Wholesale` の `pet == null` の判定の次に `if (colony.PairingOf(pet) != null) { return ShopResult.InPairing; }`。`ShopText.FailureMessage` に `case ShopResult.InPairing: return "ペアリング中の個体は売れません";`。

- [ ] **Step 1: Write the failing tests**

EditMode に加えるテスト（名前と確かめること。コードは既存テストの書き方に合わせる）：
- `ColonySaveServiceTests.RoundTrip_KeepsPairingsGravidEggsNestBoxAndWeakness`：オス・メスを2つのケージに入れ、`new BreedingService(care).StartPairing` で訪問させ、別のメス（3つ目のケージ、産卵床あり）に `Gravid`（父の遺伝子にエクリプス1コピー・確定ヘテロブリザード、`Compatibility.Bad`、`SeasonYear` 2026、予定6回・産んだ2回・次の産卵の時刻）、卵2個（1個は `NestBox`・有精・発生37.5%・中盤の記録・寒さ0.5日、1個は `Loose`・無精・`Failure.None`、子の遺伝子と `ChildKnown` あり）、1匹を `Weak`。保存→読み込みで、ペアリング（番号・時刻・成功率）、`VisitorAnimalId`、`IsVisiting`、抱卵の全フィールド（`FatherGenotype.Copies(Eclipse)==1`、`FatherKnown` の確定ヘテロ）、卵の全フィールド（`MorphNamer.FullName(ChildGenotype, ChildKnown)` が保存前と同じ）、`HasNestBox`、`Weak`、`NextPairingId`・`NextEggId` が保存前と同じ。
- `ColonySaveServiceTests.APhaseThreeSave_LoadsWithNoBreeding`：段階3の形の JSON（新しいフィールドなし、`Task 2` の `WriteSchemaThreeWithAnimal` を使ってよい）で、`Pairings`・`Eggs` が空、`NextPairingId == 1`・`NextEggId == 1`、`Weak == false`、`HasNestBox == false`、`Gravid == null`。
- `ColonySaveServiceTests.APairingWithAMissingAnimal_IsDropped`：`pairings` にいない個体の番号を書いた JSON を読むと `Pairings` が空で、どのケージの `VisitorAnimalId` も -1。
- `ColonySessionTests.ABreedingPair_PairsLaysAndSurvivesReloads`：`Load` の後、大人のオス・メス（性別判明・生後12か月・55g・性格判明・`NextShedAtUtc` は30日後）を新しいケージ2つに入れ、メスのケージに産卵床。`session.Breeding.StartPairing(..., session.GameNowUtc, session.Calendar)` → `SuccessChance = 1` → `SimulateGameTime(GameCalendar.RealTimeFor(3d))` の報告に `Breeding.PairingsSucceeded` があり `HasEvents`。`Save`→新しいセッションで `Load` → メスの `Gravid` が残る。各 `SimulateGameTime` の前に全個体の空腹・水分・清潔を100にする（12時間を何度も進めると健康が落ちて衰弱し、抱卵が終わるため）。次の産卵の時刻まで `SimulateGameTime` → `Breeding.Clutches` が1件、卵が `Eggs` にある。`Save`→`Load` → 卵が残る。
- `ColonySessionTests.HealthReachingZero_IsReportedAsWeak`：個体の世話の値を0・健康1にして `SimulateGameTime(1時間)` → `WeakStarted` にその個体、`HasEvents`。
- `ShopServiceTests.AnAnimalInAPairing_CannotBeWholesaled`：ペアリング中のオスとメスはどちらも `InPairing`、所持金と個体数は変わらない。抱卵中のメスは `Ok`（売れる）。
- `ShopAndLedgerTextTests`：`ShopText.FailureMessage(ShopResult.InPairing)` が「ペアリング中の個体は売れません」。

- [ ] **Step 2: Run the tests to verify they fail**

Run: EditMode 全体
Expected: コンパイルエラーまたは FAIL。

- [ ] **Step 3: Write the implementation**（上の規則どおり）

- [ ] **Step 4: Run the tests to verify they pass**

Run: EditMode・PlayMode 全体
Expected: PASS（段階1〜3のセーブの既存テストもすべて通ること）

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts Assets/Tests/EditMode
git commit -m "Apply and save breeding and weakness in the colony session"
```

---

### Task 10: ホーム・ケージ一覧の印（訪問中の枠・衰弱の注意マーク・繁殖の注意）

**Files:**
- Modify: `Assets/Scripts/UI/CageStatusText.cs`（`TitleFor` 13行・`AlertsFor` 24行）
- Modify: `Assets/Scripts/UI/HomeView.cs`（`CageListSignature` 29行・`CageSlot` 84行・`CageListView.Render` 141行）
- Modify: `Assets/UI/Terrarium.uss`
- Test: `Assets/Tests/EditMode/ShellTests.cs`（追加）、`Assets/Tests/PlayMode/TerrariumViewTests.cs`・`ScreenCaptureTests.cs`（追加）

**Interfaces:**
- Consumes: `Colony.ResidentShown/VisitorIn/PairingOf/EggsIn`、`BreedingText.Alerts`
- Produces: `CageStatusText.AlertsFor(Colony colony, Cage cage, PetState shown, DateTimeOffset nowUtc, CareTuning tuning)`（繁殖の注意 → 世話の注意の順に「・」でつなぐ。`shown == null` なら繁殖の注意だけ）、`CageStatusText.AwayTitle(Cage cage)`（`$"ケージ{cage.Id}"`、訪問で留守のケージ）。USS のクラス `rack-slot-visiting`・`rack-visitor`（「訪問中」の枠の中の小さなサムネイルと名前）・`rack-badge-weak`（衰弱の注意マーク「！」）・`cage-row-visiting`・`cage-row-badge-weak`。

**見た目の決まり（利用者の決定2・3）：**
1. オスのケージ（訪問者あり）：スロット全体に `rack-slot-visiting`（枠線の色を変える）。サムネイルの脇に小さな枠（`rack-visitor`）を置き、訪問中のメスの小さなサムネイル（`MorphSprites.Thumbnail`）と「訪問中」の文字。
2. メスの元のケージ：「空欄」＝サムネイルなし・名前は空・注意も空・押せない（`SetEnabled(false)`）。ケージ一覧の行は `AwayTitle`、詳細の文字は空。
3. 衰弱：サムネイルの右上に赤い丸の「！」（`rack-badge-weak`）。注意の文字の先頭に「衰弱」。ケージ一覧の行にも同じマーク。
4. 注意の文字は `AlertsFor(colony, cage, shown, …)`（「衰弱・抱卵中・産卵床なし・卵あり・空腹」など）。
5. `CageListSignature` に各ケージの `ResidentShown` の番号、`VisitorAnimalId`、`Weak`、`Gravid != null`、`HasNestBox`、卵の数を加える（変わったときだけ作り直す仕組みは今のまま）。

- [ ] **Step 1: Write the failing tests**
  - EditMode（`ShellTests`）：`AlertsFor` の新しい形が「衰弱・抱卵中・産卵床なし・空腹」の順になること。`CageListSignature` が訪問の開始・衰弱・卵の追加でそれぞれ変わること。
  - PlayMode（`TerrariumViewTests`）：ペアリング中の colony を読み込むと、ホームのオスのスロットに `rack-slot-visiting` があり `rack-visitor` の中に「訪問中」、メスの元のスロットは名前が空で押せない。衰弱の個体のスロットに `rack-badge-weak` が表示される（`display` が Flex）。
- [ ] **Step 2:** テストが FAIL することを確かめる。
- [ ] **Step 3:** 実装する（上の決まりどおり。`HomeView` と `CageListView` の両方）。
- [ ] **Step 4:** EditMode・PlayMode 全体 PASS。`ScreenCaptureTests` に `CaptureHomeBreeding`（ペアリング中のペア、衰弱した個体、卵のあるケージを含む colony）を加え、`Logs/Screens/home-breeding.png`・`cage-list-breeding.png` を撮って、訪問中の枠・小さなサムネイル・「！」が 4 列のスロットの中に収まり、文字が切れないことを確かめる。
- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI Assets/UI Assets/Tests
git commit -m "Mark visiting pairs, weak animals and eggs on the home and cage list"
```

---

### Task 11: ケージ詳細の繁殖表示（衰弱の文言・訪問中・卵・産卵床・通知・デバッグ）

**Files:**
- Modify: `Assets/Scripts/UI/TerrariumView.cs`（`LoadColony` 363・`SelectCage` 403・`ShowCageStep` 455・`HandleReport` 468・`OnLedgerAnimalTapped` 562・`BindElements` 888・`Render` 1959・`OnDebugAddPetClicked` 1651 の近く）
- Modify: `Assets/UI/Terrarium.uxml`・`Assets/UI/Terrarium.uss`
- Test: `Assets/Tests/PlayMode/TerrariumViewTests.cs`・`ScreenCaptureTests.cs`（追加）

**Interfaces:**
- Consumes: `ColonySession.Breeding`・`ColonySession.Room`、`Colony.ResidentShown/ShownCages/CageShowing`、`BreedingText.CageDetailLines/Messages/Summary/WeakStartedMessage/WeakRecoveredMessage/NestBoxButtonLabel/NestBoxMessage/NestBoxPlacedMessage/NestBoxRemovedMessage`
- Produces: UXML の `breeding-status-label`（プロフィールの語の下）・`nest-box-button`、デバッグの `debug-add-pair-button`「繁殖ペア追加」、`TerrariumView.OnNestBoxButtonClicked()`・`OnDebugAddPairClicked()`（public、PlayMode テストから呼ぶ）

**やること：**
1. 選択：`SelectCage` は `Colony.ResidentShown(cage)` を使う（留守のケージは開かない）。`ShowCageStep` と `LoadColony` の最初の選択は `Colony.ShownCages()` を使う。`OnLedgerAnimalTapped` は `Colony.CageShowing(pet)` を開く（訪問中のメスならオスのケージ）。ほかに `OccupiedCages()` を画面の選択に使っている所があれば同じく直す（`grep -n OccupiedCages Assets/Scripts/UI`）。
2. `Render`：`breeding-status-label` に `BreedingText.CageDetailLines(colony, currentCage, state, calendar, session.Room, now, tuning)` を改行でつないで出す。空なら `visibility: hidden`（高さは固定）。先頭が `BreedingText.WeakStatus` のときはクラス `breeding-status-weak`（赤）を付ける。`nest-box-button` の文字は `BreedingText.NestBoxButtonLabel(currentCage, colony.Inventory)`。
3. `OnNestBoxButtonClicked`：産卵床があれば `session.Breeding.RemoveNestBox`、なければ `PlaceNestBox`。結果が `Ok` なら `NestBoxPlacedMessage`／`NestBoxRemovedMessage`、それ以外は `NestBoxMessage(result)` を `ShowFeedback`。`Ok` なら保存と `ColonyChanged`。
4. `HandleReport`：`BreedingText.Messages(report.Breeding, tuning)` の前に、`WeakStarted` の各個体の `WeakStartedMessage`、`WeakRecovered` の各個体の `WeakRecoveredMessage` を並べ、`Summary` が空でなければ `ShowFeedback`（開いているケージと関係なく出す。既存の脱皮・性別・性格の通知の後に出して上書きしてよい）。ペアリングの結果で訪問が終わったら、開いているケージの表示を作り直す（`Render`）。今の選択が留守のケージになった（ペアリングが始まった）ときは、そのメスが訪問しているケージを選び直す。
5. デバッグ「繁殖ペア追加」：空きケージが2つなければ `AddCage(CageSize.Standard)` で足す（ラックが満杯なら「ラックがいっぱいです」）。オスとメスを1匹ずつ、`SexRevealed = true`・`Stage = Adult`・`WeightGrams = 55`・`HatchedAtUtc = now - 12日`（生後12か月）・`PersonalityKnown = true`・遺伝子は `StarterGenetics.Showcase` から、名前は「レオパN」。保存と `ColonyChanged`。
6. USS：`breeding-status-label`（小さめの文字、最大3行分の固定の高さ）、`breeding-status-weak`（赤）、`nest-box-button`（プロフィール欄の右か下の小さなボタン）。

- [ ] **Step 1: Write the failing tests**（PlayMode）
  - 衰弱の個体のケージ詳細で `breeding-status-label` が「衰弱中（繁殖できません）」で始まり、表示されている。
  - `session.Breeding.StartPairing` の後にオスのケージを開くと「訪問中：…」が出る。`ShowCageStep` で留守のケージを飛ばす。台帳から訪問中のメスをタップするとオスのケージが開く。
  - 所持品に産卵床があるとき `OnNestBoxButtonClicked` で置かれ、ボタンが「産卵床を外す」になり、所持品が減る。卵があると外せず「卵が入っているので外せません」。
  - `SimulateGameTime` でペアリングが成功すると通知欄に「ペアリング成功！…」が出る。
  - `OnDebugAddPairClicked` で大人のオス・メスが増え、`session.Breeding.CheckCandidate` がどちらも `None`（繁殖期のとき）。
- [ ] **Step 2:** FAIL を確かめる。
- [ ] **Step 3:** 実装する。
- [ ] **Step 4:** EditMode・PlayMode 全体 PASS。`ScreenCaptureTests` に `CaptureCageBreeding`（訪問中のオスのケージ、衰弱＋抱卵＋産卵床の卵があるメスのケージ）を加え、`Logs/Screens/cage-visiting.png`・`cage-eggs.png` を撮る。文字が水槽や世話のボタンに重ならず、3行に収まることを確かめる。
- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI Assets/UI Assets/Tests
git commit -m "Show weakness, visitors, eggs and the nest box on the cage detail"
```

---

### Task 12: 繁殖タブの枠（6つ目のタブ）

**Files:**
- Modify: `Assets/Scripts/UI/ShellNavigator.cs`（`ShellTab` 13行・コンストラクタ 38行・`ShowTab` 100行）
- Modify: `Assets/UI/Terrarium.uxml`・`Assets/UI/Terrarium.uss`
- Test: `Assets/Tests/EditMode/ShellTests.cs`（`BuildShell` に `breeding-panel`・`tab-breeding` を加え、テストを追加）

**Interfaces:**
- Produces: `ShellTab.Breeding`（enum の最後に追加）、UXML の `tab-breeding`「繁殖」（タブバーの「ケージ」の次）、`breeding-panel`（クラス `panel`）とその中の `breeding-season-label`・`breeding-message-label`・`breeding-list`（ScrollView）。USS のクラス（Task 13 が使う）：`breeding-section-title`・`breeding-row`・`breeding-row-selected`・`breeding-row-blocked`・`breeding-row-reason`・`breeding-line`・`breeding-forecast-line`・`breeding-start-button`・`breeding-cancel-button`。

- [ ] **Step 1: Write the failing tests**：`ShowTab(ShellTab.Breeding)` で `breeding-panel` が Flex・`placeholder-panel` が None・`tab-breeding` が選択表示。ホームではどのタブも選択表示にならない（既存の規則）。
- [ ] **Step 2:** FAIL を確かめる。
- [ ] **Step 3:** 実装する（`hasPanel` に繁殖を加える。タブが6つになるので、`tab-button` の文字の大きさと余白を調整して iPhone 15 Pro の幅で1行に収める）。
- [ ] **Step 4:** EditMode・PlayMode 全体 PASS。`scripts/capture-screens.sh` のホームと、繁殖タブを開いた空の画面を撮り（`Logs/Screens/breeding-empty.png`）、6つのタブの文字が切れず、押せる大きさ（高さ44px 以上）であることを確かめる。
- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI Assets/UI Assets/Tests
git commit -m "Add the breeding tab to the tab bar"
```

---

### Task 13: 繁殖タブの中身（進行中・ペアの選択・予測・開始・中止）

**Files:**
- Create: `Assets/Scripts/UI/PairingView.cs`
- Modify: `Assets/Scripts/UI/TerrariumView.cs`（`BindElements` 888・`OnTabChanged` 540・`RefreshShell` 1076・`OnDestroy` 680、確認ダイアログの使い方は `OnShopOfferBuyRequested` 1804／`ConfirmBuyAnimal` 1823 と同じ形）
- Modify: `Assets/UI/Terrarium.uss`（必要なら）
- Test: `Assets/Tests/PlayMode/TerrariumViewTests.cs`・`ScreenCaptureTests.cs`（追加）

**Interfaces:**
- Consumes: `BreedingService.CheckCandidate/CheckPair/StartPairing/CancelPairing`、`BreedingForecast.For`、`BreedingText`、`ConfirmDialog`
- Produces: `PairingView(VisualElement root)`、`Render(Colony colony, BreedingService breeding, GameCalendar calendar, DateTimeOffset nowUtc, RoomClimate room)`、`Invalidate()`、`Dispose()`、`Preselect(PetState pet)`（性別判明なら、メスは `SelectedFemaleId`、オスは `SelectedMaleId` に入れる）、`SelectedMaleId`・`SelectedFemaleId`（`int?`）、イベント `StartRequested(int maleId, int femaleId)`・`CancelRequested(int femaleId)`、`ShowMessage(string)`

**画面の中身（上から）：**
1. `breeding-season-label`：`BreedingText.SeasonLine(calendar.DateAt(now), care)`。
2. 「進行中」（`breeding-section-title`）：各ペアリングの `PairingLine`＋「やめる」ボタン（`breeding-cancel-button`）、各抱卵中のメスの `GravidLine`、卵のある各ケージの「ケージN：」＋ `EggLines`。何もなければ「進行中の繁殖はありません」。
3. 「メスを選ぶ」：性別が判明したメスをケージの順に1行ずつ（`breeding-row`、`CandidateLine`）。`CheckCandidate` が `None` でも `OutOfSeason` でもない行は `breeding-row-blocked`（押せない）で、右に `ProblemLabel`（`breeding-row-reason`）。選んだ行は `breeding-row-selected`。その下に `Requirement(Sex.Female)`。
4. 「オスを選ぶ」：同じ。`Requirement(Sex.Male)`。性別が分かっていない個体がいれば「性別が分かっていない個体（N匹）は選べません」。
5. 予測：両方選ばれていれば `ForecastLines(BreedingForecast.For(male, female, care))`（`breeding-forecast-line`）。
6. 「ペアリングを始める」ボタン（`breeding-start-button`）：`CheckPair` が `None` のときだけ押せる。押せないときはその下に `ProblemLabel`。
7. `breeding-message-label`：結果の文。

**決まり：**
- 作り直しは署名（ペアリング・抱卵・卵・候補の番号と理由・選択・日付の日）が変わったときだけ（`ShopView` 166行の `Signature` と同じ考え方。1秒ごとの更新でボタンを作り直すとタップが消えるため）。
- 選んだ個体がいなくなった・選べなくなったら選択を外す。
- `TerrariumView`：`StartRequested` → `ConfirmDialog` に `BreedingText.ConfirmPairing` → 「はい」のとき、その時点で `CheckPair` をもう一度確かめてから `session.Breeding.StartPairing(…, GameNowUtc(), session.Calendar)`。成功なら「ペアリングを始めました」、保存、`ColonyChanged`、`Invalidate`、選択を外す。今のケージ詳細の選択がそのメスなら、`CageShowing` のケージを選び直す。失敗なら `ProblemLabel`。`CancelRequested` → 確認「○○のペアリングをやめますか？」→ `CancelPairing` →「ペアリングをやめました」。
- `RefreshShell` で繁殖タブが開いているときだけ `Render`。`OnTabChanged` で繁殖なら `Invalidate`。`OnDestroy` で購読を外して `Dispose`。

- [ ] **Step 1: Write the failing tests**（PlayMode）
  - `OnDebugAddPairClicked` の後に繁殖タブを開くと、メスとオスの行が1つずつあり、両方選ぶと予測の行（「相性：…」「交尾の成功率：約…」）が出て、開始ボタンが押せる。はいで `Pairings` が1件になり、メスが訪問中になる。
  - 性別不明のベビーは行に出ず、「性別が分かっていない個体（1匹）は選べません」が出る。
  - 衰弱した個体の行は押せず「衰弱中です」。
  - 繁殖期の外（`SimulateGameTime` で10月へ）では開始ボタンが押せず「繁殖期ではありません」。
  - 「やめる」→ はい で `Pairings` が空、メスが元のケージに戻る。
  - 確認ダイアログを開いたまま、その間にメスが衰弱した場合、「はい」でペアリングは始まらず「衰弱中です」が出る。
- [ ] **Step 2:** FAIL を確かめる。
- [ ] **Step 3:** 実装する。
- [ ] **Step 4:** EditMode・PlayMode 全体 PASS。`ScreenCaptureTests` に `CaptureBreedingTab`（選択と予測が出ている状態、進行中のペアリング・抱卵・卵がある状態）を加え、`Logs/Screens/breeding-forecast.png`・`breeding-ongoing.png` を撮る。行の理由が右に収まり、予測の行が画面からはみ出さないことを確かめる。
- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI Assets/UI Assets/Tests
git commit -m "Add the pairing picker, forecast and ongoing breeding list"
```

---

### Task 14: ペアリングの入口（ケージ詳細のボタン・台帳の「ペアリング」）

**Files:**
- Modify: `Assets/Scripts/UI/TerrariumView.cs`
- Modify: `Assets/Scripts/UI/LedgerView.cs`（`AnimalRow` 190行・`Signature` 149行）
- Modify: `Assets/UI/Terrarium.uxml`・`Assets/UI/Terrarium.uss`
- Test: `Assets/Tests/PlayMode/TerrariumViewTests.cs`（追加）

**Interfaces:**
- Consumes: `PairingView.Preselect`、`ShellNavigator.ShowTab(ShellTab.Breeding)`
- Produces: UXML の `pair-button`「ペアリング」（ケージ詳細の `bottom-bar` の5つ目）、`TerrariumView.OnPairButtonClicked()`・`OnLedgerPairRequested(int animalId)`（public）、`LedgerView` のイベント `PairingRequested(int animalId)`

**やること：**
1. ケージ詳細の「ペアリング」：`OnPairButtonClicked` → `pairingView.Preselect(state)` → `navigator.ShowTab(ShellTab.Breeding)`。性別不明の個体でも押せる（繁殖タブで理由が分かる）。5つのボタンが1行に収まるよう `care-button` の幅を調整する。
2. 台帳：性別が判明した個体の行に小さな「ペアリング」ボタン。今の行は全体が `Button` なので、行を `VisualElement` にして「行の本体（今のタップでケージを開くボタン）」と「ペアリング」ボタンを横に並べる（ボタンの中にボタンを入れると両方のタップが起きるため）。押すと `PairingRequested(pet.Id)` → `TerrariumView.OnLedgerPairRequested` → `Preselect` → 繁殖タブ。
3. `LedgerView.Signature` にペアリングの状態（訪問中・抱卵中）を加える必要はない（行の中身は変わらない）。ボタンの有無が性別の判明で変わるので、署名に `SexKnown` を含めることを確かめる。

- [ ] **Step 1: Write the failing tests**（PlayMode）：ケージ詳細の `pair-button` を押すと繁殖タブが開き、その個体が選ばれている（メスなら `SelectedFemaleId`）。台帳の行の「ペアリング」を押すと繁殖タブが開いてその個体が選ばれ、行の本体を押すと今までどおりケージ詳細が開く。性別不明の個体の行には「ペアリング」がない。
- [ ] **Step 2:** FAIL を確かめる。
- [ ] **Step 3:** 実装する。
- [ ] **Step 4:** EditMode・PlayMode 全体 PASS。`ScreenCaptureTests` の台帳とケージ詳細の撮影で、ボタンが収まっていることを確かめる（`Logs/Screens/ledger-animals.png`・`01-idle.png`）。
- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI Assets/UI Assets/Tests
git commit -m "Open the pairing picker from the cage detail and the ledger"
```

---

### Task 15: 文書の更新と最終確認

**Files:**
- Modify: `GAME.md`（繁殖期と条件、ペアリング（訪問・3日・成功率・予測）、抱卵と産卵のスケジュール、産卵床と卵（室温・乾燥・段階5で孵化）、衰弱、室温を、1節ずつ短く）
- Modify: `CLAUDE.md`（「Pet life model」に衰弱の1行、ゲーム内時刻が `Colony.GameClockOffset` で早送りが保存されること、「Networking」に天気の気温が室温にも使われること（`Core/RoomTemperature.cs`、取れないときは24℃）、繁殖のロジックの場所（`Core/Breeding*.cs`・`Core/EggDevelopment.cs`、数値は `CareTuning`））

- [ ] **Step 1:** 上の文書を更新する（段階1〜3の記述と矛盾しないこと。段階3の計画の「持ち越し」にあった早送りの問題が直ったことを GAME.md のデバッグの説明に反映する）。
- [ ] **Step 2:** EditMode・PlayMode 全体を実行して PASS、`test-summary.py` が「.meta will be ignored」を報告しないことを確かめる。
- [ ] **Step 3:** `scripts/capture-screens.sh` と、Task 10〜14 で加えた撮影を実行し、画像を確かめる。
- [ ] **Step 4:** Commit：

```bash
git add GAME.md CLAUDE.md
git commit -m "Document breeding, eggs, weakness, room temperature and the game clock offset"
```

- [ ] **Step 5:** `scripts/build-ios.sh --run` で実機に入れる。利用者に確かめてもらうこと：
  1. デバッグの 600x で進めてからアプリを閉じて開き直しても、ゲーム内の日付が戻らない。
  2. デバッグ「繁殖ペア追加」→ ケージ詳細の「ペアリング」→ 繁殖タブでオスを選ぶ → 予測が出る → 開始。ホームでオスのケージに「訪問中」の枠、メスの元のケージが空欄。
  3. 「+12時間進める」でペアリングが終わり（成功または失敗の通知）、成功なら抱卵中の表示。ショップで産卵床を買い、メスのケージ詳細で置く。さらに進めると「産卵床に卵が○個あります。室温○℃で発生中」。位置情報をオフにすると「室温24℃（天気を取得できないため）」。
  4. 産卵床のないケージで産むと「乾いてしまいます」の通知と残り時間、2日後に「だめになりました」。
  5. デバッグで健康を0にすると、ホームのサムネイルに「！」、ケージ詳細に「衰弱中（繁殖できません）」、卸値が下がる。健康が30に戻ると解除の通知。
