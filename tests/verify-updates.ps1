param([string]$OldExecutable)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$taskOutput = Join-Path $taskRoot ('validation\updates-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$taskExe = Join-Path $taskRoot 'UpdateTests.exe'
Push-Location $taskRoot
try {
    & $taskCompiler /nologo /utf8output /nowarn:0649 /target:exe /platform:x64 /main:UpdateTests /reference:NAudio.dll /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll "/out:$taskExe" UpdateChecker.cs CaptureEngine.cs AudioCapture.cs Native.cs Localization.cs Theme.cs tests\UpdateTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Update checks did not compile.' }
    & $taskExe $taskOutput $OldExecutable
    if ($LASTEXITCODE -ne 0) { throw "Update checks failed: $taskOutput" }
    # Run the real ZIP update helper against an isolated application directory.
    # The tiny replacement app only writes a startup marker; no recorder is closed.
    $taskPayload = Join-Path $taskOutput 'payload'
    $taskTarget = Join-Path $taskOutput 'target with spaces 猫眼'
    New-Item -ItemType Directory -Force -Path $taskPayload,$taskTarget,(Join-Path $taskPayload 'tools') | Out-Null
    $taskStubSource = Join-Path $taskOutput 'Stub.cs'
    @'
using System; using System.IO;
internal static class Stub {
    private static void Main() { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "restarted.txt"), "updated"); }
}
'@ | Set-Content -LiteralPath $taskStubSource -Encoding utf8
    & $taskCompiler /nologo /target:winexe /platform:x64 "/out:$taskPayload\CatEyeScreenRecorder.exe" $taskStubSource
    if ($LASTEXITCODE -ne 0) { throw 'Restart fixture compilation failed.' }
    'old executable placeholder' | Set-Content -LiteralPath (Join-Path $taskTarget 'CatEyeScreenRecorder.exe')
    'nested component' | Set-Content -LiteralPath (Join-Path $taskPayload 'tools\component.txt')
    'user settings preserved' | Set-Content -LiteralPath (Join-Path $taskTarget 'user-settings.txt')
    $taskPackage = Join-Path $taskOutput 'update.zip'
    Compress-Archive -Path (Join-Path $taskPayload '*') -DestinationPath $taskPackage
    $taskHelper = Join-Path $taskOutput 'CatEyeUpdater.exe'
    & $taskCompiler /nologo /target:exe /platform:x64 "/out:$taskHelper" /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll tools\UpdateInstaller.cs
    if ($LASTEXITCODE -ne 0) { throw 'Updater compilation failed.' }
    # Mirror the corrected command-line quoting. Target ends with a backslash.
    $taskArgs = '--pid -1 --package "' + $taskPackage + '" --target "' + $taskTarget + '\\" --restart "' + (Join-Path $taskTarget 'CatEyeScreenRecorder.exe') + '"'
    $taskProcess = Start-Process -FilePath $taskHelper -ArgumentList $taskArgs -WindowStyle Hidden -PassThru -Wait
    if ($taskProcess.ExitCode -ne 0) { throw 'ZIP update helper failed.' }
    $taskDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath (Join-Path $taskTarget 'restarted.txt')) -and [DateTime]::UtcNow -lt $taskDeadline) { Start-Sleep -Milliseconds 100 }
    foreach ($taskExpected in @('restarted.txt','tools\component.txt','user-settings.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $taskTarget $taskExpected))) { throw "Update omitted $taskExpected" }
    }
    Write-Output 'PASS: real update helper extracts ZIP, replaces files, preserves user data, and restarts from a Unicode/spaced path.'
    Write-Output "Update validation artifacts: $taskOutput"
} finally { Pop-Location }
