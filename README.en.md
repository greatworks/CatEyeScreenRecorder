# CatEye Screen Recorder 2.2.0

[中文说明](README.md)

A watermark-free, lightweight and completely free Windows screen recorder. Open it and start recording.

## Overview

CatEye Screen Recorder is designed for simple, local screen recording on Windows. It keeps the workflow short: choose the screen or a rectangular area, select quality and frame rate, and start recording. The recorder does not add a watermark and does not require an online account.

## Features

- **No watermark**: recorded videos are ready to use.
- **Lightweight**: the encoder runs as a separate process and does not modify the system `PATH`.
- **Completely free**: there are no hidden charges or recording limits.
- **Simple controls**: the main window hides during area selection and recording.
- **Multiple languages**: Simplified Chinese, Traditional Chinese, English, Japanese, Korean, German, French, Spanish, Portuguese and Russian.
- **Local recording**: recordings stay on the selected local folder.

## Use cases

Online classes, game clips, meeting notes, software tutorials and everyday screen capture.

## Quick start

1. Run `CatEyeScreenRecorder.exe`, or launch `run.bat`.
2. Choose **Full screen** or **Custom area**, then drag a rectangle to select the capture region. Press `Esc` or right-click to cancel area selection.
3. Select quality, frame rate and the output folder, then click **Start recording**.
4. Use the bottom-right floating controls or the hotkeys to pause, resume or stop. The main window returns after the file has been saved.

The `tools` folder contains the video encoder and must stay beside the executable. Extract the complete portable ZIP before running it.

## Quality and file size

| Mode | Output | Trade-off |
| --- | --- | --- |
| High quality / smaller file (default) | MP4 / H.264, CRF 18 | Keeps the original pixel dimensions with high-quality lossy compression for normal playback and sharing. |
| Original quality / lossless | MKV / H.264 RGB, CRF 0 | Keeps the original resolution and RGB pixels for small text, graphics and post-production. Files are usually larger. |

Both modes encode directly while recording and do not create a huge uncompressed AVI intermediate file. Actual size depends on the content, motion, duration and frame rate.

## Pause, stop and hidden controls

- `Ctrl + Shift + F9`: pause or resume.
- `Ctrl + Shift + F10`: stop and save.
- Paused time is not added to the final video.
- The main window is hidden during area selection and recording.
- The floating control bar is excluded from supported screen-capture paths, so it is not recorded into the output.
- On systems that cannot exclude a window from capture, the recorder uses tray and hotkey controls instead. Full floating-control support requires Windows 10 version 2004 or later with desktop composition enabled.

When the recorder is idle, the main window remains available to normal system screenshots. Capture protection is enabled only while selecting an area or recording, so no extra screenshot permission button is needed.

## Compatibility and system requirements

- **Compatibility target**: 64-bit Windows 7 SP1, Windows 8, Windows 8.1, Windows 10 22H2, and Windows 11 24H2, 25H2, 26H1, 26H2 and later compatible builds.
- **Runtime**: .NET Framework 4.5.2 or later.
- **Validation status**: the current package has completed regression testing on Windows 10 x64. Final runtime acceptance on Windows 7, Windows 8, Windows 8.1 and each Windows 11 branch still requires a matching physical machine or virtual machine.
- **Lifecycle note**: Windows 7, Windows 8, Windows 8.1 and Windows 10 22H2 are outside Microsoft's regular support lifecycle. Prefer a currently supported Windows 11 release for production use.
- **Release information date**: this compatibility description was updated on 2026-10-07. Windows 11 26H2 is the latest generally available Windows 11 release on that date.
- **Legacy Windows 7**: if Universal CRT is missing, install the corresponding Microsoft update package before running the recorder.

The application manifest advertises the Windows 7 SP1 through Windows 11 compatibility range. Windows 11 uses the Windows 10 compatibility identifier provided by Microsoft; this does not change the stated Windows 11 target.

## Automatic updates

The application can check GitHub Releases for updates. `update.config` points to `greatworks/CatEyeScreenRecorder` by default. A release should publish a `CatEyeScreenRecorder` ZIP asset, such as `猫眼录屏-CatEyeScreenRecorder-v2.2-Windows-x64.zip`. The application checks at most once per day, asks before downloading, verifies the release asset, and lets the separate `CatEyeUpdater.exe` replace files after the main process exits.

Updates use HTTPS and do not upload recordings or change the recording folder and user settings. If the network is unavailable, local recording continues normally.

## Validation

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify-compatibility.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify-upgrade.ps1
python .\tests\verify-video.py .\validation\<timestamp>
```

The video verification script needs Pillow and NumPy. Normal users do not need these development dependencies. Validation covers high-DPI full-screen capture, custom-area coordinates, UI hide and restore, floating controls, capture exclusion, pause timing, stop and save, lossless pixel equality, and odd-sized frame encoding.

## License and third-party components

See `LICENSE` and `THIRD-PARTY-NOTICES.md` for the project license, FFmpeg license and source information.
