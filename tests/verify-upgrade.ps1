$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$taskResult = Join-Path $taskRoot ('validation\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$taskExe = Join-Path $taskRoot 'UpgradeTests.exe'
Push-Location $taskRoot
try {
    & $taskCompiler /nologo /target:exe /platform:x64 /main:UpgradeTests /win32manifest:app.manifest /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Runtime.Serialization.dll "/out:$taskExe" FreeScreenRecorder.cs CaptureEngine.cs Native.cs Theme.cs Localization.cs RecordingWindows.cs UpdateChecker.cs tests\UpgradeTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Could not compile upgrade checks.' }
    & $taskExe $taskResult
    if ($LASTEXITCODE -ne 0) { throw "Upgrade checks failed: $taskResult" }
    Write-Output "Validation artifacts: $taskResult"
} finally { Pop-Location }
