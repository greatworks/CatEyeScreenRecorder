$ErrorActionPreference = 'Stop'
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
    & $taskCompiler /nologo /target:exe /platform:x64 /optimize+ /out:CatEyeUpdater.exe /reference:System.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll tools\UpdateInstaller.cs
    if ($LASTEXITCODE -ne 0) { throw 'Update helper compilation failed.' }
    & $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ /win32icon:CatEye.ico /win32manifest:app.manifest /out:CatEyeScreenRecorder.exe /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Runtime.Serialization.dll CatEyeScreenRecorder.cs CaptureEngine.cs Native.cs Theme.cs Localization.cs RecordingWindows.cs UpdateChecker.cs
    if ($LASTEXITCODE -ne 0) { throw 'Recorder compilation failed.' }
    Write-Output "Built: CatEyeScreenRecorder.exe and CatEyeUpdater.exe (CatEye Screen Recorder 2.1.1)"
} finally { Pop-Location }
