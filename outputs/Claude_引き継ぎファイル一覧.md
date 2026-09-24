# Claude Code 引き継ぎファイル一覧

## 方針

- このプロジェクトでは**ファイルを削除しない**。
- Claude Codeは次の作業ディレクトリを開く。

```text
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5
```

- 最初に「必ず読むファイル」を順番に読み、その後は対象機能に関係するファイルだけを読む。
- Unityが自動生成するフォルダは削除しない。ただし、トークン節約のため通常は読ませない。

## 必ず読むファイル

| 順序 | 絶対パス | 用途 |
| ---: | --- | --- |
| 1 | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\AGENTS.md` | Codex用のプロジェクト規約、範囲、作業規則 |
| 2 | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\CLAUDE.md` | Claude Code用の常時ルールとCLIコマンド |
| 3 | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\GAME.md` | ゲームのMVP仕様と完了条件 |
| 4 | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\outputs\Terrarium_Days_仕様書.md` | 状態値、放置進行、保存、UI、受け入れ条件の詳細仕様 |
| 5 | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\docs\CLAUDE_CODE_SETUP.md` | Claude Code、スキル、CLI検証の運用方法 |

## Claude Codeスキル

Claude Codeのプロジェクトスキルとして、以下を引き継ぐ。

| スキル | 絶対パス | 使用場面 |
| --- | --- | --- |
| `unity-feature` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-feature\SKILL.md` | 30〜90分で終わる単一機能の実装 |
| `unity-verify` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-verify\SKILL.md` | EditMode、PlayMode、Androidビルドの確認 |
| `unity-debug` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-debug\SKILL.md` | 再現可能なUnityエラーの限定的な調査 |

以下は、Claude Codeが夜間の検証・研究モードで実践知見を元に追加した補助スキルである(2026-09-24)。

| スキル | 絶対パス | 使用場面 |
| --- | --- | --- |
| `unity-cli-batchmode` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-cli-batchmode\SKILL.md` | Unity CLIバッチモードの引数・既知の落とし穴(`-quit`/`-runTests`競合、終了コードの信頼性など)を確認するとき |
| `unity-scriptableobject-data` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-scriptableobject-data\SKILL.md` | ScriptableObjectチューニングデータを新規追加、またはCLI/エディタスクリプトでのアセット生成を行うとき |
| `unity-project-scaffold` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-project-scaffold\SKILL.md` | 新規Unityプロジェクト作成、asmdef追加、フォルダ再編成のとき |
| `unity-playmode-integration-test` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-playmode-integration-test\SKILL.md` | `Assets/Tests/PlayMode/`配下に何か追加する前 |
| `unity-android-build-troubleshoot` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-android-build-troubleshoot\SKILL.md` | Android CLIビルドが失敗、またはAPKが生成されないとき(Scripting BackendがMonoのままである既知の問題を含む) |
| `unity-editor-scripting-pitfalls` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-editor-scripting-pitfalls\SKILL.md` | アセット参照の読み込みとシーン切り替えを同じEditorスクリプト内で行うとき |
| `unity-uitoolkit-runtime-styling` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-uitoolkit-runtime-styling\SKILL.md` | `UIDocument`のビジュアルツリーを状態に応じて動的に変更するMonoBehaviourを書く前 |
| `ai-generated-placeholder-art` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\ai-generated-placeholder-art\SKILL.md` | 適切な無料/CC0素材が見つからず、スプライトやアイコン素材が必要なとき |
| `unity-jsonutility-save-data` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\unity-jsonutility-save-data\SKILL.md` | JsonUtilityベースのセーブデータ形式を新規設計、または変更するとき(DateTimeOffsetの文字列変換、フィールドのみ直列化される制約など) |
| `nunit-float-assertions` | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.claude\skills\nunit-float-assertions\SKILL.md` | double/floatを検証する新規EditMode/PlayModeテストを書くとき(`.Within()`が必要な場合と厳密等価が安全な場合の判断基準) |

`.agents\skills\` 配下の同名ファイルはCodex側の読み込み用コピーである。どちらも削除しない。上記10件も含め、`.claude\skills\`と`.agents\skills\`は常に同一内容に保つ。

## Unityプロジェクトとして必ず渡すパス

| 区分 | 絶対パス | 備考 |
| --- | --- | --- |
| Unityパッケージ定義 | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Packages\manifest.json` | Test Frameworkを含む依存関係 |
| パッケージロック | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Packages\packages-lock.json` | 依存関係の再現性のため必須 |
| Unity設定 | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\ProjectSettings\` | ディレクトリ全体を渡す。特に`ProjectVersion.txt`は必須 |
| 実装 | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Assets\Scripts\` | 現在のゲームロジック |
| エディタ自動化 | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Assets\Editor\` | 初期化・Androidビルド用のCLIエントリポイント |
| テスト | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Assets\Tests\` | EditMode／PlayModeテスト |
| Unityメタファイル | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Assets\**\*.meta` | GUID維持のため必須。個別削除禁止 |
| CLIスクリプト | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\scripts\Run-UnityTests.ps1` | Unity CLIテスト実行 |
| CLIスクリプト | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\scripts\Build-iOS.ps1` | iOS開発ビルド(Xcodeプロジェクト生成まで。実機ビルド・署名・インストールはMac上のXcodeが必要) |
| Git設定 | `C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\.gitignore` | Unity生成物と秘密情報を除外 |

## 現在の実装ファイル

```text
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Assets\Scripts\Core\StatusValue.cs
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Assets\Scripts\TerrariumDays.Runtime.asmdef
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Assets\Editor\BuildAutomation.cs
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Assets\Editor\ProjectBootstrap.cs
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Assets\Tests\EditMode\StatusValueTests.cs
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Assets\Tests\EditMode\TerrariumDays.EditModeTests.asmdef
```

## 現在の検証状況

- Unity 6000.5.3f1とiOS Build Supportを使用する(2026-09-24にAndroidから対象プラットフォームを変更)。
- Unity Personalライセンスの認識はログ上で確認済み。
- EditModeテストはまだ成功確認していない。Unityプロセスを中止したため、次回は`Run-UnityTests.ps1`の実行方式を確認してから再実行する。
- 問題調査が必要な場合だけ、次のログを参照する。ログは削除しない。

```text
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Logs\tests-editmode-20260921-001456.log
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Logs\tests-editmode-20260921-001842.log
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Logs\tests-editmode-20260921-002052.log
```

## 削除せず、通常はClaudeへ読ませないパス

次のフォルダはUnityが生成する。引き継ぎ元には残すが、内容は再生成できるため、Claude Codeの通常のコンテキストへ入れない。

```text
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Library\
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Temp\
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\Logs\
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\UserSettings\
C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5\work\
```

## Claude Codeへの最初の依頼文

```text
作業ディレクトリは C:\Users\yoshi\Documents\Codex\2026-09-20\unity-cli-5-1-2-5 です。
AGENTS.md、CLAUDE.md、GAME.md、outputs/Terrarium_Days_仕様書.md をこの順に読んでください。
Library、Temp、Logs、UserSettings、work は読まず、削除もしないでください。
現在はEditModeテスト未検証です。コード変更はせず、次に確認すべき1つの作業だけを5行以内で報告してください。
```
