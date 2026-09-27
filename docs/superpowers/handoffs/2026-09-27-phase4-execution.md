# 段階4 実装への引き継ぎ（設計承認済み）

前のセッションの会話は引き継がない。必要なことはこのファイル・計画書・git の履歴にある。

## 現在の状態（2026-09-27）
- ブランチ `phase4-breeding`（コミット 62335f7、`docs/superpowers/plans/2026-09-27-phase4-breeding.md`、main の ada72fe から分岐）。
- 設計セッションで確認した決定（計画にすでに反映済み）：
  1. ペアリング入口は3つすべて（ケージ詳細ボタン・繁殖タブ・台帳）
  2. オスのケージに「訪問中」の枠、メスの元のケージは完全な空欄（押せない、文言なし）
  3. 衰弱：ケージサムネイルの注意マーク＋世話タブに文言
  4. 産卵床の卵は簡易な状態表示のみ（キャンドリングは段階5）
  5. WeatherService を拡張し構造化した気温を Core へ渡す
  6. 両親・家系図の実装は段階5へ（子が生まれるのが段階5のため）
  7. スプライトキャッシュ上限の修正も段階5へ（新しいモルフが増えるのが段階5のため）
- planner が計画者の判断として決めた数値（交尾成功率の健康・体重係数、24℃未満で卵が3日で死ぬ、ゲーム時刻の差分が1x復帰後も残る）は、各タスクのレビューで妥当性を検証すること。

## タスク一覧（15、担当作業者付き）
1 ゲーム内時刻の差分（transcriber）／2 保存データの異常系・確定ヘテロの誤差（transcriber）／3 室温（transcriber）／4 衰弱の判定と価格（transcriber）／5 繁殖の条件・成功率・予測・乱数（transcriber）／6 ペアリングの記録・同居・産卵床（transcriber）／7 ペアリングの結果・産卵スケジュール・卵（transcriber）／8 BreedingText（transcriber）／9 セッションとセーブ（programmer）／10 ホーム・ケージ一覧の印（graphics）／11 ケージ詳細の繁殖表示（programmer）／12 繁殖タブの枠（graphics）／13 繁殖タブの中身（programmer）／14 ペアリングの入口（programmer）／15 文書と最終確認（programmer）

## 新しいセッションでの最初の依頼文（例）
> 段階4（繁殖）を実装します。ブランチ phase4-breeding の docs/superpowers/plans/2026-09-27-phase4-breeding.md を、superpowers:subagent-driven-development と hq-section-orchestration に従って実行してください。
