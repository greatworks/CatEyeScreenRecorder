# 猫眼录屏兼容性测试报告

测试日期：2026-09-28
测试目录：`D:\WSCodex\Video Recorder - Tools\windows-screen-recorder`

## 测试规划

| 类别 | 检查内容 | 结果 |
| --- | --- | --- |
| 操作系统与运行时 | Windows 7 / 10 / 11 的 API、框架、DPI 清单与降级路径 | 当前主机实测 Windows 10；Win7/Win11 完成静态审计，缺少对应实机 |
| 架构与启动 | x64 PE、GUI 子系统、.NET Framework、所有启动别名 | 通过 |
| DPI 与显示器 | PerMonitorV2 清单、150% 缩放界面、全屏物理范围、负坐标框选 | 通过；当前为单显示器实测，多显示器仅完成边界单测 |
| 录制流程 | 全屏、区域框选、暂停、停止、悬浮条排除、方向、压缩输出 | 通过 |
| 截图行为 | 空闲可截图；录制期间主窗口隐藏、悬浮条受保护；结束后恢复 | 通过 |
| 文件系统 | 中文目录、空格目录、默认视频目录、编码器相对路径 | 通过 |
| 视频结果 | FFmpeg 解码、分辨率、无损像素、HQ SSIM、暂停帧颜色 | 通过 |
| 发布包 | 文件清单、FFmpeg、Logo、SHA256、启动 | 通过 |

## 实际环境

- Windows 10 Pro 22H2，Build 19045，64 位。
- .NET Framework 4.8（Release 533325）。
- 主程序与 FFmpeg 均为 x64 PE；主程序为 GUI 子系统，FFmpeg 为控制台子系统。
- 当前测试应用在 PerMonitor DPI 清单下报告 1920×1080 全屏捕获范围，界面在当前缩放下为 1380×930。
- FFmpeg 9.0.1 essentials 可正常启动并完成编码/解码。

## 已执行的测试

1. `build-exe.bat`：重新编译主程序、图标和兼容别名，成功。
2. `tests\verify-upgrade.ps1`：通过菜单切换、全屏范围、反向/负坐标框选、窗口排除、压缩、暂停/恢复/停止、中英文 UI、弹窗滚动、截图保护状态和完整录制流程。
3. 在 `validation\compat-20260928-中文 空格` 下重复完整测试：中文目录和空格目录可正常创建、编码、停止保存和恢复。
4. 以 DPI-aware GDI `CopyFromScreen` 实测空闲主窗口截图：得到 1380×930 的完整界面，没有黑屏。
5. FFmpeg/PowerShell 视频复核：
   - `sample-lossless.mkv`：1920×1080，无损 RGB 原始帧 SHA256 一致。
   - `odd-lossless.mkv`：321×181，无损 RGB 原始帧 SHA256 一致。
   - `sample-hq.mp4`：1920×1080，30 帧，SSIM 0.993969。
   - `odd-hq.mp4`：321×181，分辨率保持不变。
   - `pause.mkv`：暂停期间中心像素只出现录制前后的红/蓝帧，没有录入暂停内容。
6. 启动与关闭测试：`CatEyeScreenRecorder.exe`、`帧匣录屏.exe`、`Framebox.exe`、`FreeScreenRecorder.exe`，以及发布包中的 `猫眼录屏.exe` 和 `CatEyeScreenRecorder.exe` 均能启动并正常关闭。
7. 发布包审计：主程序、中文别名、Logo、`tools/ffmpeg.exe`、SHA256 文件均存在；包内主程序 SHA256 与清单一致：
   `fcff039d93e2b513d22edf0401e29a9c8ec7722083bcb87d6c21a190669069be`

## Windows 版本兼容性结论

- **Windows 10 22H2：已实测通过。**
- **Windows 10 2004 及以上：代码路径兼容。** `WDA_EXCLUDEFROMCAPTURE` 仅在 Build 19041 及以上且桌面合成可用时启用；不满足条件时使用托盘控制，避免因新 API 缺失启动失败。
- **Windows 7 SP1：完成静态审计，未完成实机运行。** 程序使用 .NET Framework 4.8、GDI、WinForms、DWM 和 Windows 7 可用的 `SetWindowDisplayAffinity`；悬浮条排除功能会按设计降级为托盘控制。需要 Win7 SP1 + .NET Framework 4.8 实机或虚拟机确认 FFmpeg 9.0.1 运行库行为。
- **Windows 11：完成静态审计，未完成实机运行。** 使用标准 Win32/WinForms、PerMonitorV2 清单和 x64 程序结构，没有发现 Windows 10 专用依赖；仍建议在实际 Win11 版本上做一次启动、DPI、多屏和录制验证。

## 当前限制与建议

- 当前环境没有 Win7/Win11 虚拟机，不能宣称这两个系统已经真实通过。
- 当前只有一个可见显示器；负坐标、虚拟桌面和边界换算已通过单元/模拟测试，建议补做双显示器和不同主副屏缩放实机测试。
- 环境没有可用的 Python/Pillow/NumPy，原 `tests\verify-video.py` 未执行；已用 FFmpeg 解码、原始 RGB SHA256、SSIM 和 PowerShell 像素检查完成等价验证。
- 发布程序和 FFmpeg 当前未签名；这不影响本次功能兼容性，但在部分企业策略或 SmartScreen 环境中可能出现首次运行拦截。
