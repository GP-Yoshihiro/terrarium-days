[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$UnityPath,

    [ValidateSet('EditMode', 'PlayMode', 'All')]
    [string]$Mode = 'EditMode'
)

$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logDirectory = Join-Path $projectPath 'Logs'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity executable was not found: $UnityPath"
}

if (-not (Test-Path -LiteralPath (Join-Path $projectPath 'Assets') -PathType Container)) {
    throw "Unity project not found at $projectPath. Create/open the Unity project here before running tests."
}

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

function Invoke-UnityTestRun {
    param([Parameter(Mandatory = $true)][string]$Platform)

    $logPath = Join-Path $logDirectory "tests-$Platform-$timestamp.log"
    $resultsPath = Join-Path $logDirectory "results-$Platform-$timestamp.xml"
    # Note: -runTests quits the Editor on its own once the run completes.
    # Passing -quit alongside it causes Unity to exit during the initial
    # domain reload, before the NUnit run ever starts.
    $arguments = @(
        '-batchmode',
        '-projectPath', $projectPath,
        '-runTests', '-testPlatform', $Platform,
        '-testResults', $resultsPath,
        '-logFile', $logPath
    )

    # Unity.exe is a GUI application on Windows. Invoke it through Start-Process
    # so PowerShell waits for the NUnit run to finish instead of returning early.
    $unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru

    # This Unity build does not reliably surface $LASTEXITCODE for -runTests
    # (observed null even on a passing run), so treat the NUnit results file
    # Unity itself writes as the source of truth for pass/fail.
    if (-not (Test-Path -LiteralPath $resultsPath -PathType Leaf)) {
        throw "Unity $Platform tests produced no results file (exit code $($unityProcess.ExitCode)). Log: $logPath"
    }

    $resultAttribute = ([xml](Get-Content -LiteralPath $resultsPath -Raw)).'test-run'.result
    if ($resultAttribute -ne 'Passed') {
        throw "Unity $Platform tests reported result '$resultAttribute'. Results: $resultsPath Log: $logPath"
    }

    Write-Host "Unity $Platform tests passed. Results: $resultsPath"
}

if ($Mode -eq 'EditMode' -or $Mode -eq 'All') { Invoke-UnityTestRun -Platform 'editmode' }
if ($Mode -eq 'PlayMode' -or $Mode -eq 'All') { Invoke-UnityTestRun -Platform 'playmode' }
