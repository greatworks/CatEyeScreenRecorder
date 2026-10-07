$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $manifest = Get-Content -Raw app.manifest
    $config = Get-Content -Raw CatEyeScreenRecorder.exe.config
    $updaterConfig = Get-Content -Raw CatEyeUpdater.exe.config
    $source = (Get-Content -Raw CatEyeScreenRecorder.cs) + (Get-Content -Raw Native.cs)
    $requiredGuids = @(
        '35138b9a-5d96-4fbd-8e2d-a2440225f93a',
        '4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38',
        '1f676c76-80e1-4239-95bb-83d0f6d0da78',
        '8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a'
    )
    foreach ($guid in $requiredGuids) {
        if ($manifest -notmatch [regex]::Escape($guid)) { throw "Missing supportedOS GUID: $guid" }
    }
    if ($config -notmatch 'Version=v4\.5\.2') { throw 'The .NET Framework 4.5.2 startup target is missing.' }
    if ($updaterConfig -notmatch 'Version=v4\.5\.2') { throw 'The updater .NET Framework 4.5.2 startup target is missing.' }
    if ($source -match 'OnDpiChanged|DpiChangedEventArgs') { throw 'The source still requires the .NET 4.7-only DPI override.' }
    if ($source -notmatch 'EntryPointNotFoundException' -or $source -notmatch 'SEHException') { throw 'Win7 native API fallback guards are missing.' }
    if (-not (Test-Path -LiteralPath tools\ffmpeg.exe)) { throw 'tools\ffmpeg.exe is missing.' }
    $ffmpegVersion = (& (Join-Path $root 'tools\ffmpeg.exe') -version | Select-Object -First 1)
    if ($ffmpegVersion -notmatch 'ffmpeg version 6\.1\.1') { throw "Unexpected FFmpeg build: $ffmpegVersion" }
    $pe = [BitConverter]::ToUInt16([IO.File]::ReadAllBytes((Join-Path $root 'CatEyeScreenRecorder.exe')), 0)
    if ($pe -ne 0x5A4D) { throw 'Recorder executable is not a Windows PE file.' }
    if (-not (Test-Path -LiteralPath CatEyeScreenRecorder.exe)) { throw 'Recorder executable is missing.' }
    $process = Start-Process -FilePath (Join-Path $root 'CatEyeScreenRecorder.exe') -PassThru
    Start-Sleep -Milliseconds 900
    if ($process.HasExited) { throw "Recorder exited during startup smoke test with code $($process.ExitCode)." }
    Stop-Process -Id $process.Id -Force
    Write-Output "PASS: manifest advertises Windows 7/8/8.1/10+, .NET 4.5.2 startup, native fallback guards, FFmpeg 6.1.1, and startup smoke."
    Write-Output "Host OS: $([Environment]::OSVersion.VersionString)"
    Write-Output 'NOTE: Win7 runtime behavior still requires a physical or virtual Windows 7 SP1 test machine.'
} finally { Pop-Location }
