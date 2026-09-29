param([string[]] $Folder = @(
    (Join-Path ([Environment]::GetFolderPath('MyVideos')) 'ScreenRecordings'),
    (Join-Path ([Environment]::GetFolderPath('MyVideos')) 'FrameboxRecordings'),
    (Join-Path ([Environment]::GetFolderPath('MyVideos')) 'CatEyeRecordings')
))
$ErrorActionPreference = 'Stop'
foreach ($taskFolder in $Folder) {
    if (-not (Test-Path -LiteralPath $taskFolder -PathType Container)) { continue }
    $taskIni = Join-Path $taskFolder 'desktop.ini'
    if (-not (Test-Path -LiteralPath $taskIni)) {
        Set-Content -LiteralPath $taskIni -Value '[.ShellClassInfo]','FolderType=Generic' -Encoding UTF8
        $taskFile = Get-Item -LiteralPath $taskIni -Force
        $taskFile.Attributes = $taskFile.Attributes -bor [IO.FileAttributes]::Hidden -bor [IO.FileAttributes]::System
    }
    Get-ChildItem -LiteralPath $taskFolder -Force -File -Filter '*.recording.*' -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Attributes = $_.Attributes -bor [IO.FileAttributes]::Hidden }
    Write-Output "Prepared: $taskFolder"
}
Write-Output 'Existing recordings were not deleted or re-encoded. Refresh each folder after closing its preview pane.'
