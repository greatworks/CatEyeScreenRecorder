# 猫眼录屏 · CatEye Screen Recorder 2.3.0

[English README](README.en.md)

一款无水印、轻量、完全免费的 Windows 录屏工具，打开就能录。

猫眼录屏（CatEye Screen Recorder）面向 Windows 用户，主打无水印、轻量不占资源、操作极简。无需复杂设置，打开软件即可开始录制，适合学生录网课、游戏玩家录精彩片段、职场人录会议和教程等日常场景。软件完全免费，无隐藏收费。

核心特点：无水印；安装包小、运行时占用低；所有功能免费；打开即可录制；支持 Windows 7 SP1 / 8 / 8.1 / 10 / 11。

适用场景：网课录制、游戏片段录制、会议记录、教程制作、日常屏幕记录。

## 开始使用

1. 推荐双击 `CatEyeScreenRecorder-Setup-v2.3.0-Windows-x64.exe` 完成安装，再从开始菜单或桌面打开。便携版完整解压后双击 `CatEyeScreenRecorder.exe`，或运行 `run.bat`。点击右上角语言按钮可选择简体中文、繁體中文、English、日本語、한국어、Deutsch、Français、Español、Português 或 Русский。
2. 选择全屏，或点击“自定义区域”拖动鼠标框选矩形。框选时主窗口自动隐藏，松开完成；Esc 或右键取消。
3. 选择画质、帧率、录制声音和保存目录，点击“开始录制”。主窗口自动隐藏，右下角显示暂停和停止控制条。
4. 点击“停止”后，软件完成视频保存，再自动恢复主窗口。点击保存结果可以播放视频。

`NAudio.dll` 和 `tools` 文件夹分别是音频采集、视频编码组件，请始终与 EXE 放在一起。便携 ZIP 请完整解压后运行，不要只提取 EXE。

## 录制声音（2.3 新增）

“录制声音”提供四个选项，选择会自动保存：

| 选项 | 录制内容 |
| --- | --- |
| 电脑声音（默认） | Windows 默认播放设备的声音，包括视频、会议和游戏声音。 |
| 麦克风 | Windows 默认录音设备的声音。 |
| 电脑声音 + 麦克风 | 同时录制两者并混合到一个立体声音轨；每路降低 6 dB 左右，预留混音余量。 |
| 静音（仅画面） | 不访问声音设备，只录画面。 |

- 使用 Windows WASAPI，不安装虚拟声卡；开始录制时打开设备，停止或出错后释放。开始前请在 Windows 中设置默认播放/录音设备。
- 声音和画面共用计时，暂停时两者一起暂停，电脑没有声音的时段也会保留为静音，避免音画错位。
- 两种视频模式都输出 AAC 48 kHz 立体声音轨，目标码率 192 kbps；“原画无损”指视频像素无损，声音采用高质量有损压缩。混音后不会再次压缩视频。
- 保存期间会暂存压缩音频和视频，然后合并；请为中间文件留出额外磁盘空间。成功后清理临时文件；设备断开或保存失败时保留可恢复文件。
- 麦克风需要 Windows 允许桌面应用访问。选择不可用的设备时会提示错误，可更改系统默认设备或选择“静音”重试。录制过程中不会自动切换到刚插入的新设备。
- 同时录制时建议使用耳机，避免扬声器声音再次进入麦克风产生回声。通过远程桌面运行时，可用音频设备由远程会话的音频重定向设置决定。

## 安装和卸载

安装包支持 Windows 7 SP1 及以上的 64 位 Windows，要求 .NET Framework 4.5.2 或更新版本。安装器会检查系统和运行环境。安装包和软件都支持现有的 10 种语言。

- 默认安装到 `%LOCALAPPDATA%\Programs\CatEye Screen Recorder`，仅供当前 Windows 用户使用，正常安装不需要管理员权限。
- 创建开始菜单入口，可选创建桌面快捷方式，并注册到 Windows“已安装的应用 / 应用和功能”；Windows 7 可通过“程序和功能”卸载。
- 再次运行安装包可覆盖升级。录屏软件正在运行时会提示先关闭，避免中断录制。
- 卸载保留录制文件和用户设置，不删除用户视频。
- 当前交付的安装包尚未做发布者代码签名，Windows 可能显示“未知发布者”；正式公开分发前可按项目发布流程加签。

## 自动更新

软件支持通过 GitHub Releases 检查更新。发布包中的 `update.config` 默认指向 `greatworks/CatEyeScreenRecorder`；如需关闭检查，可将 `repository=` 留空。该仓库需要创建带有 `CatEyeScreenRecorder` ZIP 资产的 Release，例如 `猫眼录屏-CatEyeScreenRecorder-v2.2-Windows-x64.zip`。程序启动后每天最多检查一次；发现更高版本会提示，确认后下载并校验 Release 资产，再由独立的 `CatEyeUpdater.exe` 等待主程序退出、替换文件并重启。

更新包应通过 HTTPS 发布，建议同时保留 GitHub Release 的 SHA-256 摘要和版本说明。更新程序不会上传任何数据，也不会修改录制目录或用户设置；网络不可用时继续正常录制。

## 画质与文件大小

| 模式 | 输出 | 用途与取舍 |
| --- | --- | --- |
| 高清省空间（默认） | MP4 / H.264，CRF 18 | 保持原始像素尺寸，高质量有损压缩，适合日常播放与分享；不宣称逐像素无损。 |
| 原画无损 | MKV / H.264 RGB，CRF 0 | 保持原始分辨率及 RGB 像素，适合细小文字、图形和后期素材。文件通常比高清 MP4 大，建议用支持 H.264 RGB 的播放器或剪辑软件。 |

两个模式都直接压缩写入，不再生成巨大的无压缩 AVI 中间文件。实际文件大小取决于画面复杂度、运动量、录制时长和帧率，无固定压缩比例。

框选边界按 2 像素对齐，选择界面显示的矩形就是最终录制区域，没有后续缩放。整屏录制保留整个虚拟桌面的物理分辨率；极少数奇数像素的虚拟桌面会使用 H.264 4:4:4 保留尺寸，需要播放器支持。

## 暂停、停止和隐藏界面

- 右下角悬浮条：暂停 / 继续、停止、有效录制时长。
- `Ctrl + Shift + F9`：暂停 / 继续；`Ctrl + Shift + F10`：停止并保存。
- 暂停期间不录入新内容，计时停止，最终视频不插入暂停时长。
- 主窗口在框选和录制期间隐藏。悬浮条通过 Windows 的 `WDA_EXCLUDEFROMCAPTURE` 排除在录制之外，输出保留后方画面，不用黑块遮挡。
- 空闲时主窗口保持可被系统截图工具捕获；进入框选或录制前才临时启用窗口保护，完成后恢复，因此截图不需要额外按钮，也不会影响录制内容。
- 只有保存完成后才恢复主窗口，避免最后几帧录入软件界面。
- 不支持窗口排除的系统不显示悬浮条，改用托盘控制；完整悬浮功能需要 Windows 10 2004 或更新版本且启用桌面合成。快捷键冲突时可使用悬浮条。

默认保存目录为 `Videos\CatEyeRecordings`，可在界面更改，画质、帧率、声音、语言和目录会保存在 `%LOCALAPPDATA%\FrameboxRecorder\settings.xml`。旧的 `Videos\ScreenRecordings` 或 `Videos\FrameboxRecordings` 目录不会自动删除或重编码。

如果旧录制目录在资源管理器中持续占用 CPU，可先关闭预览窗格，再运行 `repair-explorer-folders.ps1`。脚本只把目录设为通用文件夹并隐藏未完成的 `.recording.*` 临时文件，不删除任何录制内容。

## 系统与打包

兼容目标为 Windows 7 SP1、Windows 8、Windows 8.1、Windows 10 22H2，以及 Windows 11 24H2 / 25H2 / 26H1 / 26H2 和后续兼容版本，64 位系统需要 .NET Framework 4.5.2 或更高版本。当前发布包已在 Windows 10 x64 完成回归；Windows 7、8、8.1 及 Windows 11 各版本的最终运行验收仍需对应实体机或虚拟机。Windows 7、Windows 8、Windows 8.1 和 Windows 10 22H2 已不在微软常规支持期内，生产环境建议使用当前受支持的 Windows 11 版本。本说明按 2026-10-07 的微软发布信息更新，Windows 11 26H2 为当前最新正式版本。内置 FFmpeg 编码组件，通过独立进程运行，不修改系统 PATH，不需要联网录制。Windows 7 使用旧版系统时，若系统缺少 Universal CRT，请先安装对应的 Microsoft 更新包。

双击 `build-exe.bat` 重新生成带图标和 DPI manifest 的 `CatEyeScreenRecorder.exe`。编码组件位于 `tools` 文件夹，升级组件为 `CatEyeUpdater.exe`。项目只保留当前猫眼录屏版本的源代码和测试入口。

构建安装包和便携 ZIP：安装 Inno Setup 6.7.3 后运行 `build-release.ps1 -IsccPath "C:\路径\ISCC.exe"`。产物在 `release` 中，附 SHA-256 校验文件。脚本在独立目录编译，不覆盖正在使用的开发程序。音频库为随项目提供的 NAudio 1.10.0，无需 NuGet 恢复或更高版本的 .NET。

## 验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify-upgrade.ps1
python .\tests\verify-video.py .\validation\对应的时间目录
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify-audio.ps1
python .\tests\verify-audio-output.py .\validation\audio-对应的时间目录
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-release.ps1 -TestInstaller
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\verify-installer.ps1
```

第二步需要 Pillow 和 NumPy；普通用户运行录屏软件不需要这些依赖。验证会短暂显示测试窗口并录制测试画面，结果位于 `validation`。完整测试运行使用真实的 WinForms 消息循环。

已验证：150% 缩放下 1920×1080 全屏范围、框选反向拖动和负坐标换算、实际 UI 隐藏/恢复流程、悬浮按钮、窗口排除及其反向对照、暂停时间剔除、停止保存、原画无损逐像素一致、正负步幅和奇数尺寸编码。多显示器的实机组合与其他缩放比例尚未实机验收。

同一组 1920×1080、30 帧静态文字测试画面：未压缩 RGB 数据约 178 MiB，高清 MP4 约 216 KiB，原画无损 MKV 约 411 KiB。高清样本 SSIM 为 0.993969；无损样本解码后 RGB 像素完全一致。这只是静态文字样本，不代表游戏或动态视频的压缩比例。

FFmpeg 版本、许可证与来源见 `THIRD-PARTY-NOTICES.md`。

2.3 的音频验证覆盖合成双音源混音、重采样、静音区间、暂停边界、AAC 输出、音视频时长和视频码流不变，以及无设备时的错误回收。当前测试环境没有可用的播放/麦克风端点，因此真实声卡和麦克风，以及 Windows 7/8/8.1/11 实机音频仍待验收，不能视作已完成硬件兼容测试。安装流程已使用隔离测试标识完成安装、覆盖升级、启动、运行中保护、卸载和用户数据保留验证。
