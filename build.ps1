param([string]$OutputDirectory = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) {
    $taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $taskCompiler)) { throw 'The .NET Framework compiler was not found.' }
$taskTemp = Join-Path ([IO.Path]::GetTempPath()) ('cateye-build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskTemp | Out-Null
Push-Location $PSScriptRoot
try {
    & $taskCompiler /nologo /utf8output /nowarn:0649 /target:exe /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$taskTemp\MakeIcon.exe" tools\MakeIcon.cs Theme.cs Localization.cs Native.cs
    if ($LASTEXITCODE -ne 0) { throw 'Icon compilation failed.' }
    & "$taskTemp\MakeIcon.exe" CatEye.ico
    if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
    & $taskCompiler /nologo /target:exe /platform:x64 /optimize+ "/out:$taskOutput\CatEyeUpdater.exe" /reference:System.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll tools\UpdateInstaller.cs
    if ($LASTEXITCODE -ne 0) { throw 'Update helper compilation failed.' }
    & $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ /win32icon:CatEye.ico /win32manifest:app.manifest "/out:$taskOutput\CatEyeScreenRecorder.exe" /reference:NAudio.dll /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Runtime.Serialization.dll CatEyeScreenRecorder.cs CaptureEngine.cs AudioCapture.cs Native.cs Theme.cs Localization.cs RecordingWindows.cs UpdateChecker.cs
    if ($LASTEXITCODE -ne 0) { throw 'Recorder compilation failed.' }
    if ($taskOutput -ne $PSScriptRoot) {
        Copy-Item -LiteralPath NAudio.dll,CatEyeScreenRecorder.exe.config,CatEyeUpdater.exe.config -Destination $taskOutput
    }
    Write-Output "Built: $taskOutput (CatEye Screen Recorder 2.3.0)"
} finally { Pop-Location }
