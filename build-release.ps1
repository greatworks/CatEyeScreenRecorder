param(
    [string]$IsccPath = (Join-Path $env:LOCALAPPDATA 'CatEyeBuildTools\InnoSetup\ISCC.exe'),
    [switch]$TestInstaller
)
$ErrorActionPreference = 'Stop'
$taskVersion = '2.3.0'
$taskRoot = $PSScriptRoot
$taskRelease = Join-Path $taskRoot 'release'
$taskPayload = Join-Path $taskRelease ('CatEyeScreenRecorder-v' + $taskVersion)
if (-not (Test-Path -LiteralPath $IsccPath)) { throw 'Inno Setup 6.7.3 is required. Pass -IsccPath with the path to ISCC.exe.' }
& (Join-Path $taskRoot 'build.ps1') -OutputDirectory $taskPayload
foreach ($taskFolder in @('tools','vendor\NAudio','installer')) { New-Item -ItemType Directory -Force -Path (Join-Path $taskPayload $taskFolder) | Out-Null }
foreach ($taskFile in @('CatEye.ico','CatEyeLogo.png','LICENSE','README.md','README.en.md','THIRD-PARTY-NOTICES.md','update.config','run.bat','tools\ffmpeg.exe','tools\LICENSE','vendor\NAudio\LICENSE.txt','vendor\NAudio\NOTICE.txt','installer\INNO-LICENSE.txt')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot $taskFile) -Destination (Join-Path $taskPayload $taskFile)
}
$taskArgs = @('/Qp', "/DPayloadDir=$taskPayload", "/DOutputDir=$taskRelease", "/DAppVersion=$taskVersion")
if ($TestInstaller) { $taskArgs += '/DTestInstall=1' }
& $IsccPath @taskArgs (Join-Path $taskRoot 'installer\CatEye.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
if (-not $TestInstaller) {
    $taskZip = Join-Path $taskRelease ("CatEyeScreenRecorder-v$taskVersion-Windows-x64.zip")
    Compress-Archive -Path (Join-Path $taskPayload '*') -DestinationPath $taskZip -Force
    $taskSetup = Join-Path $taskRelease ("CatEyeScreenRecorder-Setup-v$taskVersion-Windows-x64.exe")
    $taskHashes = foreach ($taskArtifact in @($taskZip,$taskSetup)) {
        $taskHash = Get-FileHash -LiteralPath $taskArtifact -Algorithm SHA256
        "$($taskHash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($taskArtifact))"
    }
    $taskHashes | Set-Content -Encoding ascii -LiteralPath (Join-Path $taskRelease 'SHA256SUMS-v2.3.0.txt')
    Get-Item -LiteralPath $taskSetup,$taskZip | Select-Object FullName,Length
}
