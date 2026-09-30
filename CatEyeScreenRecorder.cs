using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

[assembly: AssemblyTitle("猫眼录屏 CatEye Screen Recorder")]
[assembly: AssemblyProduct("猫眼录屏 CatEye Screen Recorder")]
[assembly: AssemblyDescription("无水印、轻量、完全免费的 Windows 录屏工具")]
[assembly: AssemblyVersion("2.1.1.0")]

namespace FreeWindowsScreenRecorder
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new RecorderForm());
        }
    }

    internal sealed class RecorderForm : Form
    {
        private readonly TextBox outputBox = new TextBox();
        private readonly DarkSelect qualityBox = new DarkSelect(), fpsBox = new DarkSelect();
        private readonly DarkButton fullButton = new DarkButton(), regionButton = new DarkButton(), selectButton = new DarkButton(), startButton = new DarkButton();
        private readonly DarkButton languageButton = new DarkButton();
        private readonly List<Action> languageBindings = new List<Action>();
        private readonly CapturePreview preview = new CapturePreview();
        private readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ToolStripMenuItem trayPause = new ToolStripMenuItem("暂停录制"), trayStop = new ToolStripMenuItem("停止并保存");
        private Label statusLabel, qualityHint, latestLabel;
        private bool fullScreen = true, busy, saving, closeAfterSave;
        private bool updateCheckStarted;
        private Rectangle selectedArea;
        private RecordingSession session;
        private RecordingToolbar toolbar;
        private string latestFile;
        private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrameboxRecorder", "settings.xml");

        public RecorderForm()
        {
            SuspendLayout(); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Text = Localization.ProductTitle; BackColor = Theme.Background; ForeColor = Theme.Text;
            Font = Theme.Font(9, false); FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(920, 620); StartPosition = FormStartPosition.CenterScreen; DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BuildUi(); LoadSettings(); ApplyLanguage(); UpdateArea();
            tray.Icon = Icon ?? SystemIcons.Application; tray.Text = Localization.ProductName;
            ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.Add(trayPause); menu.Items.Add(trayStop); tray.ContextMenuStrip = menu;
            trayPause.Click += delegate { TogglePause(); }; trayStop.Click += delegate { StopRecording(); };
            tray.DoubleClick += delegate { if (session != null) TogglePause(); };
            uiTimer.Interval = 200; uiTimer.Tick += delegate { UpdateRecordingState(); }; uiTimer.Start();
            ResumeLayout(true);
            Shown += delegate
            {
                NormalizeSettingsLayout();
                if (IsHandleCreated) BeginInvoke(new Action(NormalizeSettingsLayout));
            };
            Shown += async delegate { await CheckForUpdatesAsync(); };
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            if (IsHandleCreated) BeginInvoke(new Action(NormalizeSettingsLayout));
        }

        private void NormalizeSettingsLayout()
        {
            if (IsDisposed || qualityBox.IsDisposed || fpsBox.IsDisposed || qualityBox.Parent == null) return;
            qualityBox.NormalizeDpiBounds();
            fpsBox.NormalizeDpiBounds();
            qualityBox.Invalidate(); fpsBox.Invalidate();
        }

        private void BuildUi()
        {
            Panel chrome = new Panel { Bounds = new Rectangle(0, 0, 920, 44), BackColor = Theme.Sidebar };
            Controls.Add(chrome); chrome.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Native.DragWindow(this); };
            Label name = LocalLabel(chrome, "猫眼录屏  /  CATEYE", "CatEye Screen Recorder", 22, 13, 330, 22, 9, Theme.Muted, false);
            name.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Native.DragWindow(this); };
            languageButton.Text = "EN"; languageButton.Chrome = true; languageButton.TabStop = false; languageButton.BackColor = Theme.Sidebar; languageButton.Bounds = new Rectangle(750, 5, 68, 32); languageButton.Click += delegate { ToggleLanguage(); }; chrome.Controls.Add(languageButton);
            DarkButton minimize = Button(chrome, "—", 828, 5, 38, 32, delegate { WindowState = FormWindowState.Minimized; }); minimize.Chrome = true; minimize.TabStop = false; minimize.BackColor = Theme.Sidebar;
            DarkButton close = Button(chrome, "×", 873, 5, 36, 32, delegate { Close(); }); close.Danger = true; close.Chrome = true; close.TabStop = false; close.BackColor = Theme.Sidebar;
            Panel sidebar = new Panel { Bounds = new Rectangle(0, 44, 164, 576), BackColor = Theme.Sidebar }; Controls.Add(sidebar);
            Panel mark = new Panel { Bounds = new Rectangle(22, 25, 48, 48) }; mark.Paint += delegate(object sender, PaintEventArgs e) { Theme.Mark(e.Graphics, mark.ClientRectangle); }; sidebar.Controls.Add(mark);
            LocalLabel(sidebar, "猫眼", "CatEye", 22, 83, 118, 39, 23, Theme.Text, true);
            LocalLabel(sidebar, "免费 · 无水印", "Free · No watermark", 24, 127, 136, 22, 8, Theme.Accent, false);
            DarkButton nav = LocalButton(sidebar, "▣   屏幕录制", "▣   Screen recording", 14, 181, 136, 44, delegate { }); nav.TextAlign = ContentAlignment.MiddleLeft; nav.Selected = true;
            DarkButton recordings = LocalButton(sidebar, "▤   录制文件夹", "▤   Recordings", 14, 237, 136, 42, delegate { OpenFolder(); }); recordings.TextAlign = ContentAlignment.MiddleLeft;
            DarkButton about = LocalButton(sidebar, "ⓘ   关于猫眼", "ⓘ   About CatEye", 14, 293, 136, 42, delegate { ShowAbout(); }); about.TextAlign = ContentAlignment.MiddleLeft;
            LocalLabel(sidebar, "本地录制\n清晰记录 · 轻巧留存", "Local recording\nClear & light", 24, 454, 128, 53, 9, Theme.Muted, false);
            LocalLabel(sidebar, "CATEYE  2.0", "CATEYE  2.0", 24, 531, 126, 20, 8, Color.FromArgb(100, 106, 117), false);
            LocalLabel(this, "录制工作台", "Recording workspace", 192, 65, 340, 41, 22, Theme.Text, true);
            LocalLabel(this, "绿色 · 轻量 · 无广告", "Green · Lightweight · Ad-free", 194, 109, 500, 25, 9, Theme.Muted, false);
            LocalLabel(this, "●  高清画面 · 无水印", "●  HD · No watermark", 757, 77, 180, 26, 9, Theme.Accent, false);
            BindButton(fullButton, "▣  全屏", "▣  Full screen"); fullButton.Bounds = new Rectangle(192, 151, 130, 39); Controls.Add(fullButton);
            fullButton.Click += delegate { fullScreen = true; UpdateArea(); };
            BindButton(regionButton, "⌗  自定义区域", "⌗  Custom area"); regionButton.Bounds = new Rectangle(332, 151, 162, 39); Controls.Add(regionButton);
            regionButton.Click += async delegate { await SelectRegionAsync(); };
            BindButton(selectButton, "重新框选", "Select again"); selectButton.Bounds = new Rectangle(768, 300, 120, 38); Controls.Add(selectButton);
            selectButton.Click += async delegate { await SelectRegionAsync(); };
            preview.Bounds = new Rectangle(192, 204, 704, 148); Controls.Add(preview);
            CardPanel settings = new CardPanel { Bounds = new Rectangle(192, 370, 704, 105) }; Controls.Add(settings);
            LocalLabel(settings, "画质与体积", "Quality & size", 18, 13, 160, 23, 9, Theme.Muted, false);
            ConfigureCombo(qualityBox, new Rectangle(18, 40, 290, 32));
            SetComboItems(qualityBox, new string[] { "高清省空间 · MP4（推荐）", "原画无损 · MKV" }, new string[] { "HQ MP4 · Compact", "Lossless MKV" });
            settings.Controls.Add(qualityBox); qualityBox.SelectedIndexChanged += delegate { UpdateQualityHint(); };
            qualityHint = LocalLabel(settings, "原分辨率 · 高质量压缩", "Native resolution · High-quality compression", 18, 77, 456, 20, 8, Theme.Muted, false);
            LocalLabel(settings, "帧率", "Frame rate", 470, 13, 180, 23, 9, Theme.Muted, false);
            ConfigureCombo(fpsBox, new Rectangle(470, 40, 214, 32));
            SetComboItems(fpsBox, new string[] { "15 FPS · 文档演示", "30 FPS · 日常录制", "60 FPS · 流畅动态" }, new string[] { "15 FPS · Docs", "30 FPS · Daily", "60 FPS · Smooth" }); settings.Controls.Add(fpsBox);
            LocalLabel(this, "保存到", "Save to", 194, 493, 72, 26, 9, Theme.Muted, false);
            outputBox.Bounds = new Rectangle(264, 491, 505, 28); outputBox.BackColor = Theme.Card; outputBox.ForeColor = Theme.Text;
            outputBox.BorderStyle = BorderStyle.FixedSingle; outputBox.Font = Theme.Font(9, false);
            outputBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "CatEyeRecordings"); Controls.Add(outputBox);
            LocalButton(this, "更改目录", "Change folder", 784, 484, 112, 36, delegate { BrowseOutput(); });
            statusLabel = LocalLabel(this, "就绪 · 主窗口自动隐藏，控制条不入镜", "Ready · Controls stay hidden", 194, 541, 495, 23, 9, Theme.Text, false);
            latestLabel = LocalLabel(this, "Ctrl + Shift + F9 暂停 / 继续    Ctrl + Shift + F10 停止", "F9 pause / resume    F10 stop", 194, 569, 500, 32, 8, Theme.Muted, false);
            latestLabel.AutoEllipsis = true;
            latestLabel.Cursor = Cursors.Default;
            latestLabel.Click += delegate { if (!string.IsNullOrEmpty(latestFile) && File.Exists(latestFile)) Process.Start(latestFile); };
            BindButton(startButton, "●  开始录制", "●  Start recording"); startButton.Primary = true; startButton.Font = Theme.Font(11, true);
            startButton.Bounds = new Rectangle(710, 545, 186, 49); Controls.Add(startButton);
            startButton.Click += async delegate { await StartRecordingAsync(); };
        }

        private static DarkButton Button(Control parent, string text, int x, int y, int width, int height, Action action)
        {
            DarkButton button = new DarkButton { Text = text, Bounds = new Rectangle(x, y, width, height) };
            button.Click += delegate { action(); }; parent.Controls.Add(button); return button;
        }
        private Label LocalLabel(Control parent, string chinese, string english, int x, int y, int width, int height, float size, Color color, bool bold)
        {
            Label label = Theme.Label(parent, Localization.Text(chinese, english), x, y, width, height, size, color, bold);
            languageBindings.Add(delegate { label.Text = Localization.Text(chinese, english); }); return label;
        }
        private DarkButton LocalButton(Control parent, string chinese, string english, int x, int y, int width, int height, Action action)
        {
            DarkButton button = Button(parent, Localization.Text(chinese, english), x, y, width, height, action);
            languageBindings.Add(delegate { button.Text = Localization.Text(chinese, english); }); return button;
        }
        private void BindButton(DarkButton button, string chinese, string english)
        {
            button.Text = Localization.Text(chinese, english);
            languageBindings.Add(delegate { button.Text = Localization.Text(chinese, english); });
        }
        private static void SetComboItems(DarkSelect combo, string[] chinese, string[] english)
        {
            int selected = combo.SelectedIndex < 0 ? 0 : combo.SelectedIndex;
            combo.Items.Clear();
            string[] values = Localization.IsEnglish ? english : chinese;
            for (int i = 0; i < values.Length; i++) combo.Items.Add(values[i]);
            combo.SelectedIndex = Math.Min(selected, values.Length - 1);
        }
        private void ToggleLanguage()
        {
            Localization.Current = Localization.IsEnglish ? AppLanguage.Chinese : AppLanguage.English;
            ApplyLanguage(); SaveSettings();
        }
        private async Task CheckForUpdatesAsync()
        {
            if (updateCheckStarted || !UpdateChecker.Enabled) return;
            updateCheckStarted = true;
            try
            {
                ReleaseInfo release = await UpdateChecker.CheckLatestAsync();
                if (release == null || IsDisposed || busy || session != null) return;
                string prompt = Localization.Text("发现新版本 " + release.TagName + "，是否下载更新？", "Version " + release.TagName + " is available. Download the update?");
                if (MessageBox.Show(this, prompt, Localization.Text("猫眼录屏更新", "CatEye Screen Recorder update"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
                startButton.Enabled = false; UseWaitCursor = true;
                statusLabel.Text = Localization.Text("正在下载更新…", "Downloading update…"); statusLabel.ForeColor = Theme.Accent;
                string package = await UpdateChecker.DownloadAsync(release);
                UseWaitCursor = false;
                statusLabel.Text = Localization.Text("更新已下载，准备安装", "Update downloaded and ready to install");
                string restartPrompt = Localization.Text("更新包已下载，立即重启安装吗？", "The update is downloaded. Restart now to install it?");
                if (MessageBox.Show(this, restartPrompt, Localization.Text("猫眼录屏更新", "CatEye Screen Recorder update"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    UpdateChecker.LaunchInstaller(release, package); Close();
                }
            }
            catch (Exception) { }
            finally { UseWaitCursor = false; if (!IsDisposed && session == null) startButton.Enabled = true; }
        }
        private void ApplyLanguage()
        {
            for (int i = 0; i < languageBindings.Count; i++) languageBindings[i]();
            languageButton.Text = Localization.IsEnglish ? "中文" : "EN";
            Text = Localization.ProductTitle; tray.Text = Localization.ProductName;
            trayPause.Text = Localization.Text("暂停录制", "Pause recording"); trayStop.Text = Localization.Text("停止并保存", "Stop and save");
            SetComboItems(qualityBox, new string[] { "高清省空间 · MP4（推荐）", "原画无损 · MKV" }, new string[] { "HQ MP4 · Compact", "Lossless MKV" });
            SetComboItems(fpsBox, new string[] { "15 FPS · 文档演示", "30 FPS · 日常录制", "60 FPS · 流畅动态" }, new string[] { "15 FPS · Docs", "30 FPS · Daily", "60 FPS · Smooth" });
            UpdateQualityHint();
            if (!string.IsNullOrEmpty(latestFile) && File.Exists(latestFile)) SetLatestPlaybackText();
            else { latestLabel.Cursor = Cursors.Default; latestLabel.ForeColor = Theme.Muted; latestLabel.Font = Theme.Font(8, false); }
            UpdateArea();
        }
        private void SetLatestPlaybackText()
        {
            latestLabel.Text = Localization.Text("点击播放  ", "Click to play  ") + Path.GetFileName(latestFile);
            latestLabel.ForeColor = Theme.Accent; latestLabel.Cursor = Cursors.Hand; latestLabel.Font = Theme.Font(8, true);
        }
        private void ShowAbout()
        {
            using (AboutDialog dialog = new AboutDialog()) dialog.ShowDialog(this);
        }
        private static void ConfigureCombo(DarkSelect combo, Rectangle bounds)
        {
            combo.LogicalBounds = bounds; combo.Bounds = bounds; combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = Color.FromArgb(41, 44, 50); combo.ForeColor = Theme.Text; combo.Font = Theme.Font(9, false);
        }
        private void UpdateQualityHint()
        {
            if (qualityHint != null) qualityHint.Text = qualityBox.SelectedIndex == 1 ? Localization.Text("逐像素保真 · 文件较大 · 建议用 VLC / 剪辑软件播放", "Pixel-perfect · Larger file") : Localization.Text("原分辨率 · 高质量压缩，兼顾清晰度与体积", "Native resolution · High quality");
        }
        private void UpdateArea()
        {
            fullButton.Selected = fullScreen; regionButton.Selected = !fullScreen;
            fullButton.Invalidate(); regionButton.Invalidate(); selectButton.Visible = !fullScreen;
            Rectangle area = GetCaptureArea(); preview.Dimensions = area.Width + " × " + area.Height;
            preview.Mode = fullScreen ? Localization.Text("全屏录制", "Full-screen recording") : Localization.Text("区域录制  (", "Area recording  (") + area.X + ", " + area.Y + ")"; preview.Invalidate();
        }
        internal Rectangle GetCaptureArea() { return fullScreen ? SystemInformation.VirtualScreen : selectedArea; }
        internal async Task SelectRegionAsync()
        {
            if (busy) return;
            busy = true; Native.ExcludeFromCapture(Handle); Hide();
            try
            {
                await Task.Delay(180); Native.FlushDesktop(); Rectangle desktop = SystemInformation.VirtualScreen;
                using (Bitmap image = new Bitmap(desktop.Width, desktop.Height))
                {
                    using (Graphics g = Graphics.FromImage(image)) Native.Capture(g, desktop);
                    using (RegionSelector selector = new RegionSelector(desktop, image))
                        if (selector.ShowDialog() == DialogResult.OK) { selectedArea = selector.SelectedArea; fullScreen = false; }
                }
            }
            catch (Exception ex) { statusLabel.Text = Localization.Text("框选失败：", "Area selection failed: ") + ex.Message; }
            finally { busy = false; Native.AllowCapture(Handle); Show(); Activate(); UpdateArea(); }
        }
        internal async Task StartRecordingAsync()
        {
            if (busy || session != null) return;
            Rectangle area = GetCaptureArea();
            if (area.Width < 2 || area.Height < 2) { await SelectRegionAsync(); return; }
            if (!File.Exists(FfmpegWriter.EncoderPath)) { MessageBox.Show(this, Localization.Text("找不到编码组件，请将 tools 文件夹与程序保持在一起。", "The encoder is missing. Keep the tools folder beside the program."), Localization.ProductName); return; }
            string directory = outputBox.Text.Trim();
            try { directory = Path.GetFullPath(directory); Directory.CreateDirectory(directory); PrepareOutputFolder(directory); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Localization.Text("无法保存到此目录", "Cannot save to this folder")); return; }
            VideoQuality quality = qualityBox.SelectedIndex == 1 ? VideoQuality.Lossless : VideoQuality.HighQuality;
            int fps = new int[] { 15, 30, 60 }[Math.Max(0, fpsBox.SelectedIndex)];
            string path = Path.Combine(directory, "猫眼-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + (quality == VideoQuality.Lossless ? ".mkv" : ".mp4"));
            busy = true; saving = false; SaveSettings(); startButton.Enabled = false; Native.ExcludeFromCapture(Handle); Hide();
            try
            {
                await Task.Delay(180); Native.FlushDesktop();
                toolbar = new RecordingToolbar(); toolbar.PauseRequested += TogglePause; toolbar.StopRequested += StopRecording;
                bool floating = toolbar.ShowSafely(Screen.PrimaryScreen.WorkingArea);
                // Do not add a notification-area icon to the recorded desktop when the protected toolbar is available.
                tray.Visible = !floating; trayPause.Enabled = trayStop.Enabled = true;
                if (!floating) tray.ShowBalloonTip(4000, Localization.ProductName, Localization.Text("此系统不支持排除悬浮条，已使用托盘控制。右键托盘图标可暂停或停止。", "This system cannot exclude the floating bar, so the tray controls are used. Right-click the tray icon to pause or stop."), ToolTipIcon.Info);
                session = new RecordingSession(area, fps, path, quality);
                session.Completed += delegate(string output, Exception error) { BeginInvoke(new Action(delegate { RecordingCompleted(output, error); })); };
                session.Start();
            }
            catch (Exception ex) { RecordingCompleted(path, ex); }
        }
        internal void TogglePause()
        {
            if (session == null || saving) return; session.TogglePause(); UpdateRecordingState();
        }
        internal void StopRecording()
        {
            if (session == null || saving) return;
            saving = true; session.Stop(); UpdateRecordingState();
        }
        private void UpdateRecordingState()
        {
            if (session == null) return;
            if (toolbar != null) toolbar.UpdateState(session.Elapsed, session.IsPaused, saving);
            trayPause.Text = session.IsPaused ? Localization.Text("继续录制", "Resume recording") : Localization.Text("暂停录制", "Pause recording"); trayStop.Text = Localization.Text("停止并保存", "Stop and save"); trayPause.Enabled = trayStop.Enabled = !saving;
        }
        private void RecordingCompleted(string path, Exception error)
        {
            if (toolbar != null) { toolbar.Dispose(); toolbar = null; }
            tray.Visible = false; session = null; busy = false; saving = false; startButton.Enabled = true;
            Native.AllowCapture(Handle);
            if (error == null)
            {
                latestFile = path; long bytes = new FileInfo(path).Length;
                statusLabel.Text = Localization.Text("已保存  ·  ", "Saved  ·  ") + (bytes / 1048576.0).ToString("0.0") + " MB"; statusLabel.ForeColor = Theme.Accent;
                SetLatestPlaybackText();
            }
            else
            {
                statusLabel.Text = Localization.Text("录制未完成，请查看错误详情", "Recording did not finish. Check the error details."); statusLabel.ForeColor = Theme.Red;
                latestLabel.Text = Localization.Text("可恢复的临时文件保留在录制目录", "A recoverable temporary file remains in the recording folder."); latestLabel.Cursor = Cursors.Default; latestLabel.ForeColor = Theme.Muted; latestLabel.Font = Theme.Font(8, false);
                try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(path), "猫眼-错误-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log"), error.ToString()); } catch { }
                Show(); MessageBox.Show(this, error.Message, Localization.Text("猫眼录屏 · 录制失败", "CatEye Screen Recorder · Recording failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            if (closeAfterSave) { Close(); return; }
            Show(); WindowState = FormWindowState.Normal; Activate(); UpdateArea();
        }
        private void BrowseOutput()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = outputBox.Text; dialog.Description = Localization.Text("选择录制文件保存位置", "Choose where recordings are saved");
                if (dialog.ShowDialog(this) == DialogResult.OK) { outputBox.Text = dialog.SelectedPath; SaveSettings(); }
            }
        }
        private void OpenFolder()
        {
            try { Directory.CreateDirectory(outputBox.Text); Process.Start("explorer.exe", FfmpegWriter.Quote(outputBox.Text)); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Localization.Text("无法打开目录", "Cannot open folder")); }
        }
        private static void PrepareOutputFolder(string directory)
        {
            // Keep Explorer from classifying a media-heavy folder and repeatedly probing every item.
            string desktopIni = Path.Combine(directory, "desktop.ini");
            if (!File.Exists(desktopIni))
            {
                File.WriteAllText(desktopIni, "[.ShellClassInfo]" + Environment.NewLine + "FolderType=Generic" + Environment.NewLine);
                File.SetAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);
                DirectoryInfo folder = new DirectoryInfo(directory); folder.Attributes |= FileAttributes.System;
            }
        }
        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                XmlDocument doc = new XmlDocument(); XmlElement root = doc.CreateElement("settings"); doc.AppendChild(root);
                root.SetAttribute("output", outputBox.Text); root.SetAttribute("quality", qualityBox.SelectedIndex.ToString()); root.SetAttribute("fps", fpsBox.SelectedIndex.ToString()); root.SetAttribute("language", Localization.IsEnglish ? "en" : "zh"); doc.Save(settingsPath);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(settingsPath)) return;
                XmlDocument doc = new XmlDocument(); doc.XmlResolver = null; doc.Load(settingsPath); XmlElement root = doc.DocumentElement;
                if (!string.IsNullOrEmpty(root.GetAttribute("output"))) outputBox.Text = root.GetAttribute("output");
                Localization.Current = root.GetAttribute("language") == "en" ? AppLanguage.English : AppLanguage.Chinese;
                int value; if (int.TryParse(root.GetAttribute("quality"), out value) && value >= 0 && value < 2) qualityBox.SelectedIndex = value;
                if (int.TryParse(root.GetAttribute("fps"), out value) && value >= 0 && value < 3) fpsBox.SelectedIndex = value;
            }
            catch (Exception) { }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); if (busy || session != null) Native.ExcludeFromCapture(Handle); else Native.AllowCapture(Handle);
            bool pause = Native.SetHotkey(Handle, 1, Keys.F9), stop = Native.SetHotkey(Handle, 2, Keys.F10);
            if ((!pause || !stop) && latestLabel != null) latestLabel.Text = Localization.Text("快捷键被占用时，请使用悬浮条或托盘控制", "If a shortcut is busy, use the floating bar or tray controls.");
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            Native.ClearHotkey(Handle, 1); Native.ClearHotkey(Handle, 2); base.OnHandleDestroyed(e);
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0312) { if (message.WParam.ToInt32() == 1) TogglePause(); else if (message.WParam.ToInt32() == 2) StopRecording(); }
            base.WndProc(ref message);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (session != null) { e.Cancel = true; closeAfterSave = true; StopRecording(); return; }
            if (IsHandleCreated) Native.AllowCapture(Handle);
            SaveSettings(); base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { uiTimer.Dispose(); tray.Dispose(); if (toolbar != null) toolbar.Dispose(); }
            base.Dispose(disposing);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }
}
