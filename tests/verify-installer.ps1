$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskExe = Join-Path $taskRoot 'release\CatEye-InstallTest.exe'
$taskVersionSource = Get-Content -Raw -LiteralPath (Join-Path $taskRoot 'UpdateChecker.cs')
if ($taskVersionSource -notmatch 'CurrentVersionText\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"') { throw 'Version constant missing.' }
$taskExpectedVersion = $Matches[1]
$taskResults = Join-Path $taskRoot ('validation\installer-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$taskInstallDir = Join-Path $taskResults 'installed 猫眼'
$taskKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CatEyeScreenRecorder.InstallTest_is1'
$taskStartLink = Join-Path ([Environment]::GetFolderPath('Programs')) 'CatEye Install Test\CatEye Install Test.lnk'
$taskDesktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'CatEye Install Test.lnk'
if (-not (Test-Path -LiteralPath $taskExe)) { throw 'Build the isolated installer first: .\build-release.ps1 -TestInstaller' }
if (Test-Path -LiteralPath $taskKey) { throw 'An earlier isolated installation exists. Inspect/uninstall that test installation first.' }
New-Item -ItemType Directory -Force -Path $taskResults | Out-Null
$taskPreferences = Join-Path $env:LOCALAPPDATA 'FrameboxRecorder\settings.xml'
$taskOriginal = if (Test-Path -LiteralPath $taskPreferences) { [IO.File]::ReadAllBytes($taskPreferences) } else { $null }
$taskSentinel = Join-Path $taskResults 'user-recording-preserve.mp4'
Set-Content -LiteralPath $taskSentinel -Value 'User recording must survive uninstall.'
$taskRun = $null
try {
    foreach ($taskPhase in @('install','upgrade')) {
        $taskLog = Join-Path $taskResults ($taskPhase + '.log')
        $taskProcess = Start-Process -FilePath $taskExe -WindowStyle Hidden -PassThru -Wait -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="'+$taskInstallDir+'"'),'/LANG=english','/TASKS=desktopicon',('/LOG="'+$taskLog+'"'))
        if ($taskProcess.ExitCode -ne 0) { throw "$taskPhase failed: $($taskProcess.ExitCode). See $taskLog" }
        $taskEntry = Get-ItemProperty -LiteralPath $taskKey
        if ($taskEntry.DisplayVersion -ne $taskExpectedVersion -or $taskEntry.InstallLocation.TrimEnd('\') -ne $taskInstallDir) { throw 'App registration incorrect.' }
        if (-not (Test-Path -LiteralPath $taskStartLink) -or -not (Test-Path -LiteralPath $taskDesktopLink)) { throw 'Shortcuts not created.' }
        foreach ($taskFile in @('CatEyeScreenRecorder.exe','NAudio.dll','tools\ffmpeg.exe','CatEyeUpdater.exe','unins000.exe')) {
            if (-not (Test-Path -LiteralPath (Join-Path $taskInstallDir $taskFile))) { throw "Missing installed component: $taskFile" }
        }
        Write-Output "PASS: $taskPhase, Windows app registration, Unicode install path, and shortcuts."
    }
    $taskRun = Start-Process -FilePath (Join-Path $taskInstallDir 'CatEyeScreenRecorder.exe') -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 1500
    if ($taskRun.HasExited) { throw 'Installed app failed startup.' }
    Write-Output 'PASS: installed application starts.'
    # Silent upgrade must refuse to modify an application that might be recording.
    $taskBlocked = Start-Process -FilePath $taskExe -WindowStyle Hidden -PassThru -Wait -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="'+$taskInstallDir+'"'),('/LOG="'+(Join-Path $taskResults 'running-app.log')+'"'))
    if ($taskBlocked.ExitCode -eq 0 -or $taskRun.HasExited) { throw 'Installer did not protect a running recorder.' }
    Write-Output 'PASS: installer refuses upgrade while recorder is running, without terminating it.'
    $null = $taskRun.CloseMainWindow()
    if (-not $taskRun.WaitForExit(10000)) { throw 'Test application did not exit cleanly.' }
    $taskRun = $null
    $taskUninstaller = Join-Path $taskInstallDir 'unins000.exe'
    $taskUninstall = Start-Process -FilePath $taskUninstaller -WindowStyle Hidden -Wait -PassThru -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $taskResults 'uninstall.log')+'"'))
    if ($taskUninstall.ExitCode -ne 0) { throw 'Uninstall failed.' }
    Start-Sleep -Milliseconds 800
    if ((Test-Path -LiteralPath $taskKey) -or (Test-Path -LiteralPath $taskStartLink) -or (Test-Path -LiteralPath $taskDesktopLink)) { throw 'Uninstall left registration or shortcuts.' }
    if (Test-Path -LiteralPath (Join-Path $taskInstallDir 'CatEyeScreenRecorder.exe')) { throw 'Uninstall left application binaries.' }
    if (-not (Test-Path -LiteralPath $taskSentinel)) { throw 'Uninstall removed user recording.' }
    Write-Output 'PASS: uninstall removes application/shortcuts/registration and preserves recordings.'
    'Install, upgrade, launch, active-app protection, uninstall: PASS' | Set-Content -LiteralPath (Join-Path $taskResults 'result.txt')
    Write-Output "Installer validation: $taskResults"
} finally {
    if ($taskRun -and -not $taskRun.HasExited) { $null=$taskRun.CloseMainWindow(); $null=$taskRun.WaitForExit(5000) }
    if ($null -ne $taskOriginal) { [IO.File]::WriteAllBytes($taskPreferences, $taskOriginal) }
}
