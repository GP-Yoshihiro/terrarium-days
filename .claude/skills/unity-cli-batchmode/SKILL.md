---
name: unity-cli-batchmode
description: Reference for Unity Editor CLI batchmode arguments and known pitfalls. Consult before writing or debugging any Unity batchmode script (CLI tests, CLI builds, custom -executeMethod automation).
---

# Unity CLI batchmode reference

Core arguments and documented behavior (Unity 6000.5 Editor CLI reference):

| Argument | Behavior |
| --- | --- |
| `-batchmode` | Runs without dialogs/human interaction. On exception or failed operation, Unity exits with return code `1`. Only one Unity instance may have a given project open at a time — a second batchmode run against the same `-projectPath` will fail or hang on the project lock. |
| `-quit` | Quits the Editor after other commands finish. |
| `-quitTimeout <seconds>` | Timeout for pending async tasks when using `-quit`. Default 300s. |
| `-nographics` | Skips graphics device init (useful headless/CI). Turns off output logs by default — but the official docs' own fix is to pass `-logFile <path>` alongside it: "To enable the creation of output logs, specify a file location using the command `-logFile`." So the safe combination is `-nographics` **with** an explicit `-logFile`, not `-nographics` alone. |
| `-logFile <path>` | Writes the batchmode log here. Use `-` to stream to console instead of a file. |
| `-projectPath <path>` | Project to open. |
| `-executeMethod <Class.Method>` | Runs one static method after the project opens. Must live under an `Editor/` folder. Use this as the general-purpose hook for custom CLI automation (builds, data validation, asset pipelines) beyond testing. |
| `-buildTarget <name>` | Sets the active build target before the project loads. |
| `-accept-apiupdate` | Required in batchmode for the API Updater to actually run; omitting it skips API updates and, per the official docs, "might lead to compiler errors" — not just a silently-missed update, a plausible root cause for an otherwise-confusing compile failure after a package/API version bump. |

## Known conflict: `-quit` + `-runTests`

**Do not pass both.** Unity's own docs state that when running tests with `-runTests`, `-quit` causes the Editor to quit immediately, before in-progress tests have a chance to complete — the run gets killed mid-flight instead of finishing and exiting cleanly. `-runTests` already quits the Editor itself once the run completes; adding `-quit` races it during the initial domain reload and can abort before NUnit ever starts. This repo's `scripts/Run-UnityTests.ps1` already encodes this (see its comment above the argument list) — do not "fix" it by re-adding `-quit`.

## Exit code is not a reliable pass/fail signal

Unity does not document a common exit-code contract across its subsystems for test runs; `-batchmode` itself only guarantees code `1` on an Editor-level exception/failure, not on NUnit test failures specifically. Some installs also fail to propagate a distinguishing exit code for `-runTests` at all. **Treat the `-testResults` XML file as the source of truth**, not the process exit code:

1. Always pass `-testResults <path>`.
2. After the process exits, check the file exists — its absence (regardless of exit code) means the run aborted before producing results (compile error, missing package, license failure, crash, or the `-quit` conflict above). Read the log for the actual reason.
3. If it exists, parse the root `<test-run result="...">` attribute (`Passed`/`Failed`/etc.) rather than trusting the shell exit code.

## macOS (current setup)

The executable is `/Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/MacOS/Unity`; it blocks the shell until batchmode finishes (no `Start-Process` needed). Use `scripts/run-unity-tests.sh [EditMode|PlayMode|All]`, then `scripts/test-summary.py` to print only totals and failures. Explicit (`[Explicit]`) tests run only when named with `-testFilter <FullClassName>`. Unity Hub CLI: `"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless install-modules --version <ver> -m ios`.

## Windows: Unity.exe does not block the shell

On Windows, `Unity.exe` is a GUI application; invoking it directly from PowerShell returns immediately instead of waiting for batchmode to finish. Launch it via `Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru` (as `scripts/Run-UnityTests.ps1` does) so the calling script actually blocks until the run completes, then inspect `$process.ExitCode` only as a secondary signal alongside the results file.

## Token-efficient verification workflow

Practices that kept a long multi-feature session's context usage low while still verifying every change with real Unity CLI evidence:

- **Run `-Mode All` (EditMode + PlayMode) in one invocation** rather than two separate CLI calls. Each Unity batchmode launch pays a fixed cost (license check, domain reload, asset database load) regardless of how many tests it runs; combining both platforms into one process amortizes that cost instead of paying it twice, and halves the number of background-task round-trips you have to wait on.
- **Always pass an absolute path to the test-runner script**, not a relative one. A relative `scripts/Run-UnityTests.ps1` breaks the moment the shell's working directory drifts (e.g. after an unrelated `cd` into a subfolder for a `ls`/directory-listing check earlier in the session) — the failure mode is an unhelpful "argument to the -File parameter does not exist" rather than a Unity error, costing a full extra run to diagnose. Absolute paths make this class of mistake structurally impossible.
- **Never dump the full JUnit XML into context.** After a run, extract only the root summary line — e.g. `grep -o '<test-run [^>]*result="[^"]*"[^>]*>' results.xml` — which gives pass/fail/count in one short line. Only read further into the file (or the `.log`) when that line shows a failure and you need the specific failing `<test-case>`/`<failure>` block.
- **Delete throwaway diagnostic artifacts once they've served their purpose.** A temporary extra `-logFile` run added purely to bisect a silent bug (see the `unity-editor-scripting-pitfalls` skill for an example) leaves a log file behind that has no further value — remove it rather than letting ad-hoc debug logs accumulate alongside the real per-feature logs in `Logs/`.
- **Launch the CLI run in the background and wait for the completion notification** instead of polling. A batchmode run with a domain reload commonly takes 1-4 minutes; blocking synchronously on it (or worse, sleep-polling for it) wastes turns. Fire it as a background task and let the harness's completion notification resume the session — see this project's session history for the established pattern (`run_in_background: true`, then react to the `<task-notification>`).
- **Don't re-run an idempotent scene-wiring `-executeMethod` step (e.g. this project's `TerrariumSceneSetup.Configure`) after every UI change — only after a change that actually affects what it wires.** Its job is to bind named `VisualElement`s to a `UIDocument`/component and save the scene; a content-only edit to a `.uxml`/`.uss` file (new text, a color tweak, a reflowed layout that doesn't add/rename/remove a queried element name) changes nothing the Editor script touches, because the UXML/USS files are loaded as assets at runtime, not baked into the scene — the scene only stores *references* to those asset files, not their contents. Re-running it is only actually necessary when: a brand-new named element was added that C# code now queries for, a `MonoBehaviour`'s serialized field shape changed, or the `PanelSettings`/`VisualTreeAsset` reference itself needs re-pointing. Treat "did I add/rename an element name that `BindElements` queries for, or change a component's fields" as the trigger question before spending a CLI round-trip on scene re-verification — most UI-content iterations don't need it, only most UI-*wiring* iterations do.

## Stale project locks

If a prior batchmode run was killed (Ctrl+C, task kill, crash) rather than exiting cleanly, the project's lock file can persist and cause the next `-batchmode` invocation to hang waiting for it. If a run hangs immediately on startup with no log growth, check for a leftover Unity process for this project before assuming a script bug.

## Sources

- [Unity Editor command line arguments reference (6000.5)](https://docs.unity3d.com/6000.5/Documentation/Manual/EditorCommandLineArguments.html)
- [Test Framework command line arguments (2.0)](https://docs.unity3d.com/Packages/com.unity.test-framework@2.0/manual/reference-command-line.html)
