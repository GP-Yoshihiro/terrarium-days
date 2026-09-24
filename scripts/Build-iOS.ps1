[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$UnityPath,

    [string]$OutputPath = 'Builds/iOS/TerrariumDays'
)

$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logDirectory = Join-Path $projectPath 'Logs'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logPath = Join-Path $logDirectory "ios-build-$timestamp.log"
$absoluteOutputPath = [System.IO.Path]::GetFullPath((Join-Path $projectPath $OutputPath))

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity executable was not found: $UnityPath"
}

if (-not (Test-Path -LiteralPath (Join-Path $projectPath 'Assets') -PathType Container)) {
    throw "Unity project not found at $projectPath. Create/open the Unity project here before building."
}

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

$arguments = @(
    '-batchmode',
    '-quit',
    '-projectPath', $projectPath,
    '-executeMethod', 'TerrariumDays.Editor.BuildAutomation.BuildIosDevelopment',
    '-buildOutput', $absoluteOutputPath,
    '-logFile', $logPath
)

# Unity.exe is a GUI application on Windows. Invoke it through Start-Process
# so PowerShell waits for the build to finish instead of returning early.
$unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru

# BuildTarget.iOS produces an Xcode PROJECT DIRECTORY, not a runnable binary — this
# CLI step can only get you that far from Windows. Compiling, code-signing, and
# installing onto an iPhone/iPad requires opening that project in Xcode on a Mac.
if (-not (Test-Path -LiteralPath (Join-Path $absoluteOutputPath 'Unity-iPhone.xcodeproj') -PathType Container)) {
    throw "Unity produced no Xcode project (exit code $($unityProcess.ExitCode)): $absoluteOutputPath. Log: $logPath"
}

if ($unityProcess.ExitCode -ne 0) {
    Write-Warning "Unity process exited with code $($unityProcess.ExitCode), but the Xcode project was created. Log: $logPath"
}

Write-Host "Xcode project created: $absoluteOutputPath"
Write-Host "Next step (on a Mac): open '$absoluteOutputPath/Unity-iPhone.xcodeproj' in Xcode, select a signing team, then build/run on a connected iPhone/iPad."
