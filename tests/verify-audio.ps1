$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$taskOutput = Join-Path $taskRoot ('validation\audio-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$taskExe = Join-Path $taskRoot 'AudioTests.exe'
Push-Location $taskRoot
try {
    & $taskCompiler /nologo /utf8output /nowarn:0649 /target:exe /platform:x64 /main:AudioTests /reference:NAudio.dll /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$taskExe" CaptureEngine.cs AudioCapture.cs Native.cs Localization.cs Theme.cs tests\AudioTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Audio checks did not compile.' }
    & $taskExe $taskOutput
    if ($LASTEXITCODE -ne 0) { throw "Audio checks failed: $taskOutput" }
    Write-Output "Audio validation artifacts: $taskOutput"
} finally { Pop-Location }
