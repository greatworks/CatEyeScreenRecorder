# Third-party components

CatEye Screen Recorder invokes FFmpeg as a separate executable for video encoding. No FFmpeg code is linked into the recorder executable.

- Component: FFmpeg 9.0.1 essentials Windows build, including libx264.
- Binary distributor: Gyan Doshi, https://www.gyan.dev/ffmpeg/builds/
- Download: https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip
- Downloaded and verified: 2026-09-09.
- Archive SHA-256: `FEC81AE03971D9DD4BE3EBE02E263BD2EC1D789483F931BDBA5F5715E65DA2E9`.
- Build configuration includes `--enable-gpl --enable-version3`; the build's accompanying license is retained as `tools/LICENSE`.
- Upstream project and source: https://ffmpeg.org/ and https://github.com/FFmpeg/FFmpeg
- Release source information and build details: https://www.gyan.dev/ffmpeg/builds/#release-builds
- x264: https://www.videolan.org/developers/x264.html

The development folder also includes ffprobe for validation. The portable user package only needs ffmpeg.exe and its license. Keep third-party license notices with copied components.

Implementation references:

- FFmpeg libx264/libx264rgb encoder documentation: https://ffmpeg.org/ffmpeg-codecs.html#libx264_002c-libx264rgb
- Microsoft window capture exclusion: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity
- Microsoft process DPI awareness: https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process
