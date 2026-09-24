# Claude Code setup and extension list

## Objective
Use a deliberately small Claude Code setup. The purpose is not to avoid every token limit mechanically; it is to minimize repeated project explanation, broad code searches, and unfocused debug loops so the 5-hour allowance is spent on implementation.

## Required local tools
| Item | Purpose | Status in this workspace |
| --- | --- | --- |
| Git | Diff, history, and safe rollback | Available |
| Claude Code | Coding agent | Not found on PATH; install and sign in required |
| Unity Editor 6000.5.3f1 with Android Build Support | Editor, CLI tests, Android builds | Available at `C:\\Program Files\\Unity\\Hub\\Editor\\6000.5.3f1\\Editor\\Unity.exe` |
| Android SDK / JDK / NDK | Unity Android builds | Installed with the selected Unity Editor; verify on the first build |
| Android device with USB debugging | Real-device development build check | User setup required |

Install Claude Code on Windows from its official installer or run `winget install Anthropic.ClaudeCode`, then confirm with `claude --version`. Unity 6000.5.3f1 is already installed with Android Build Support, so do not change Editor versions during this prototype. Do not install packages, MCP servers, or marketplace plugins merely to begin this prototype.

Unity Personalライセンスは有効化済みである。CLIテストの成功はまだ確認できていないため、JUnit XMLの結果が生成・成功するまでは、テストまたはビルドが成功したと報告しない。

## Project files installed now
| File or directory | Role | Token-saving effect |
| --- | --- | --- |
| `CLAUDE.md` | Always-loaded project rules and common commands | Eliminates repeated explanations each session |
| `GAME.md` | Frozen prototype scope and acceptance criteria | Prevents scope drift and vague implementation prompts |
| `.claude/skills/unity-feature/` | Workflow for a single small feature | Forces focused, reviewable requests |
| `.claude/skills/unity-verify/` | CLI test/build workflow | Separates validation from implementation chat |
| `.claude/skills/unity-debug/` | Narrow log-to-fix debugging workflow | Avoids speculative multi-file fixes |
| `scripts/` | Repeatable Unity CLI commands | Makes test evidence easy to request and rerun |

## Skills to use
These are local Claude Code skills included in this project, not third-party dependencies.

| Skill | Invoke when | Do not use when |
| --- | --- | --- |
| `/unity-feature` | Implementing one small, specified behavior | Planning an entire game or refactoring unrelated code |
| `/unity-verify` | Running EditMode, PlayMode, or Android build checks | The requested work has no runnable change yet |
| `/unity-debug` | A reproducible compiler/test/runtime failure exists | You are guessing what might be wrong |

## Plugin and MCP policy
**Required plugins: none.** Claude Code already provides file editing, shell commands, search, and git operations needed for this offline Unity prototype. Every extra MCP/plugin increases configuration, tool description, and troubleshooting overhead.

Consider a plugin only when a concrete need appears:

| Need that appears later | Suitable extension type | Decision rule |
| --- | --- | --- |
| Repeat the same Unity workflow across multiple projects | Claude Code plugin that bundles these local skills | Create/adopt after this prototype succeeds |
| Visual UI handoff from a designer | Figma MCP/plugin | Add only if a Figma file is actually the source of truth |
| Issue tracking with other people | GitHub/Linear MCP/plugin | Add only when issues are maintained externally |
| Automated Unity editor control | Unity-specific MCP, if a trusted maintained integration is selected | Review permissions and maintenance status first |

Use Claude Code's `/plugin` command to search its configured marketplace at the time of need. Plugin availability changes, so do not rely on a copied marketplace list.

## Session protocol (the main token-control mechanism)
1. Start each session in this project directory; Claude Code loads `CLAUDE.md` automatically.
2. Select exactly one unchecked item from `GAME.md`.
3. Invoke `/unity-feature`, provide the file paths and acceptance criteria, and implement only that item.
4. Invoke `/unity-verify` in a fresh, short request.
5. If it fails, invoke `/unity-debug` with only the failing command and relevant log excerpt.
6. Commit after a verified vertical slice. Start a new conversation after a completed slice rather than retaining a long debug history.

## Prompt template
```text
/unity-feature

Task: [one behavior only]
Read: GAME.md and [specific source files].
Change only: [paths].
Acceptance criteria:
- [observable result]
- [test result]
Do not refactor unrelated code or add packages.
```

## Initial verification commands
The Unity project is already created in this repository:

```powershell
claude --version
powershell -ExecutionPolicy Bypass -File scripts/Run-UnityTests.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe'
powershell -ExecutionPolicy Bypass -File scripts/Build-Android.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe'
```
