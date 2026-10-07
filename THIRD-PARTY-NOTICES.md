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

Implementation references:

- FFmpeg libx264/libx264rgb encoder documentation: https://ffmpeg.org/ffmpeg-codecs.html#libx264_002c-libx264rgb
- Microsoft window capture exclusion: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity
- Microsoft process DPI awareness: https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process
