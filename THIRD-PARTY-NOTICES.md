# Third-party components

CatEye Screen Recorder invokes FFmpeg as a separate executable for video encoding. No FFmpeg code is linked into the recorder executable.

- Component: FFmpeg 6.1.1 essentials Windows build, including libx264 and libx264rgb.
- Binary distributor: Gyan Doshi, https://www.gyan.dev/ffmpeg/builds/
- Download: https://github.com/GyanD/codexffmpeg/releases/download/6.1.1/ffmpeg-6.1.1-essentials_build.zip
- Downloaded and verified: 2026-09-30.
- Archive SHA-256: `742E32FC9F92681F9F254B925E1B613FDD8074BA40749D4879AEFDB009B94CC5`.
- Build configuration includes `--enable-gpl --enable-version3`; the build's accompanying license is retained as `tools/LICENSE`.
- Upstream project and source: https://ffmpeg.org/ and https://github.com/FFmpeg/FFmpeg
- Release source information and build details: https://www.gyan.dev/ffmpeg/builds/#release-builds
- x264: https://www.videolan.org/developers/x264.html

The development folder also includes ffprobe for validation. The portable user package only needs ffmpeg.exe and its license. Keep third-party license notices with copied components.

Audio capture uses the unmodified NAudio 1.10.0 .NET Framework 3.5 assembly from NuGet. Copyright Mark Heath and contributors. This version is distributed under the Microsoft Public License (Ms-PL). The complete license, package URL, DLL/package hashes and pinned source link are retained in `vendor/NAudio/LICENSE.txt` and `vendor/NAudio/NOTICE.txt`. Audio capture uses the Windows WASAPI shared-mode and loopback APIs; AAC/FLAC encoding remains in the external FFmpeg process.

The installer is built with Inno Setup 6.7.3 (https://jrsoftware.org/), Copyright Jordan Russell and Martijn Laan. Chinese translations were retrieved from the official source repository under `Files/Languages/ChineseSimplified.isl` and `ChineseTraditional.isl` on 2026-10-09; original contributor notices remain in those files. Installer build dependencies are not required to run CatEye. See `installer/INNO-LICENSE.txt` for Inno Setup's license.

Implementation references:

- FFmpeg libx264/libx264rgb encoder documentation: https://ffmpeg.org/ffmpeg-codecs.html#libx264_002c-libx264rgb
- Microsoft window capture exclusion: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity
- Microsoft process DPI awareness: https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process
