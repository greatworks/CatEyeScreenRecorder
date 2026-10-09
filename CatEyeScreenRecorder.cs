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
[assembly: AssemblyVersion("2.3.0.0")]

namespace FreeWindowsScreenRecorder
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            // Installer checks this named mutex instead of terminating an active recording.
            using (System.Threading.Mutex appMutex = new System.Threading.Mutex(false, "CatEyeScreenRecorder.Running"))
                Application.Run(new RecorderForm());
        }
    }

    internal sealed class RecorderForm : Form
    {
        private readonly TextBox outputBox = new TextBox();
        private readonly DarkSelect qualityBox = new DarkSelect(), fpsBox = new DarkSelect(), audioBox = new DarkSelect();
        private readonly DarkButton fullButton = new DarkButton(), regionButton = new DarkButton(), selectButton = new DarkButton(), startButton = new DarkButton();
        private readonly DarkButton languageButton = new DarkButton();
        private ContextMenuStrip languageMenu;
        private readonly List<Action> languageBindings = new List<Action>();
        private readonly CapturePreview preview = new CapturePreview();
        private readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ToolStripMenuItem trayPause = new ToolStripMenuItem("暂停录制"), trayStop = new ToolStripMenuItem("停止并保存");
        private CardPanel settingsPanel;
        private DarkButton changeFolderButton;
        private Label saveLabel, statusLabel, qualityTitle, fpsTitle, audioTitle, qualityHint, latestLabel;
        private bool fullScreen = true, busy, saving, closeAfterSave;
        private bool updateCheckStarted;
        private bool layingOutSettings;
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

        private void NormalizeSettingsLayout()
        {
            if (IsDisposed || qualityBox.IsDisposed || fpsBox.IsDisposed || qualityBox.Parent == null) return;
            LayoutSettingsPanel();
            qualityBox.Invalidate(); fpsBox.Invalidate(); audioBox.Invalidate();
        }

        private void LayoutSettingsPanel()
        {
            if (settingsPanel == null || settingsPanel.IsDisposed || settingsPanel.Width <= 0) return;
            qualityBox.NormalizeDpiBounds(); fpsBox.NormalizeDpiBounds(); audioBox.NormalizeDpiBounds();
            float scale = settingsPanel.Width / 704f;
            if (scale <= 0f) scale = 1f;
            int padding = Math.Max(18, (int)Math.Round(18f * scale));
            int top = Math.Max(12, (int)Math.Round(13f * scale));
            int rowTop = Math.Max(36, (int)Math.Round(40f * scale));
            int rowHeight = Math.Max(24, Math.Max(qualityBox.Font.Height + 8, fpsBox.Font.Height + 8));
            int hintTop = rowTop + rowHeight + Math.Max(6, (int)Math.Round(8f * scale));
            int leftWidth = (int)Math.Round(250f * scale);
            int rightX = (int)Math.Round(284f * scale);
            int rightWidth = (int)Math.Round(180f * scale);
            int audioX = (int)Math.Round(480f * scale);
            int audioWidth = settingsPanel.ClientSize.Width - audioX - padding;
            int titleHeight = Math.Max(23, (int)Math.Round(23f * scale));
            int hintHeight = Math.Max(20, Math.Max((int)Math.Round(22f * scale), qualityHint == null ? 20 : qualityHint.Font.Height + 4));
            rowHeight = Math.Max(rowHeight, Math.Max(24, Math.Max(qualityBox.Font.Height + 8, fpsBox.Font.Height + 8)));
            hintTop = rowTop + rowHeight + Math.Max(6, (int)Math.Round(8f * scale));
            if (qualityTitle != null) qualityTitle.Bounds = new Rectangle(padding, top, leftWidth, titleHeight);
            if (fpsTitle != null) fpsTitle.Bounds = new Rectangle(rightX, top, rightWidth, titleHeight);
            if (audioTitle != null) audioTitle.Bounds = new Rectangle(audioX, top, audioWidth, titleHeight);
            qualityBox.LogicalBounds = new Rectangle(18, 40, 250, 28); qualityBox.Bounds = new Rectangle(padding, rowTop, leftWidth, rowHeight);
            fpsBox.LogicalBounds = new Rectangle(284, 40, 180, 28); fpsBox.Bounds = new Rectangle(rightX, rowTop, rightWidth, rowHeight);
            audioBox.LogicalBounds = new Rectangle(480, 40, 206, 28); audioBox.Bounds = new Rectangle(audioX, rowTop, audioWidth, rowHeight);
            if (qualityHint != null) qualityHint.Bounds = new Rectangle(padding, hintTop, settingsPanel.Width - padding * 2, hintHeight);
            int requiredHeight = Math.Max((int)Math.Round(102f * scale), hintTop + hintHeight + Math.Max(8, (int)Math.Round(8f * scale)));
            if (settingsPanel.Height != requiredHeight) settingsPanel.Height = requiredHeight;
            int delta = settingsPanel.Height - Math.Max(118, (int)Math.Round(118f * scale));
            SetLowerLayout(scale, delta);
        }

        private void SetLowerLayout(float scale, int delta)
        {
            int ySave = (int)Math.Round(493f * scale) + delta;
            if (saveLabel != null) saveLabel.Bounds = new Rectangle((int)Math.Round(194f * scale), ySave, (int)Math.Round(72f * scale), Math.Max(26, (int)Math.Round(26f * scale)));
            outputBox.Bounds = new Rectangle((int)Math.Round(264f * scale), (int)Math.Round(491f * scale) + delta, (int)Math.Round(505f * scale), Math.Max(28, outputBox.Font.Height + 8));
            if (changeFolderButton != null) changeFolderButton.Bounds = new Rectangle((int)Math.Round(784f * scale), (int)Math.Round(484f * scale) + delta, Math.Max(112, (int)Math.Round(112f * scale)), Math.Max(36, (int)Math.Round(36f * scale)));
            if (statusLabel != null) statusLabel.Bounds = new Rectangle((int)Math.Round(194f * scale), (int)Math.Round(541f * scale) + delta, (int)Math.Round(495f * scale), Math.Max(23, (int)Math.Round(23f * scale)));
            if (latestLabel != null) latestLabel.Bounds = new Rectangle((int)Math.Round(194f * scale), (int)Math.Round(569f * scale) + delta, (int)Math.Round(500f * scale), Math.Max(32, (int)Math.Round(32f * scale)));
            startButton.Bounds = new Rectangle((int)Math.Round(710f * scale), (int)Math.Round(545f * scale) + delta, Math.Max(186, (int)Math.Round(186f * scale)), Math.Max(49, (int)Math.Round(49f * scale)));
        }

        private void BuildUi()
        {
            Panel chrome = new Panel { Bounds = new Rectangle(0, 0, 920, 44), BackColor = Theme.Sidebar };
            Controls.Add(chrome); chrome.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Native.DragWindow(this); };
            Label name = LocalLabel(chrome, "猫眼录屏  /  CATEYE", "CatEye Screen Recorder", 22, 13, 330, 22, 9, Theme.Muted, false);
            name.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Native.DragWindow(this); };
            languageButton.Text = Localization.ShortCode; languageButton.Chrome = true; languageButton.TabStop = false; languageButton.BackColor = Theme.Sidebar; languageButton.Bounds = new Rectangle(750, 5, 68, 32); languageButton.Click += delegate { ShowLanguageMenu(); }; chrome.Controls.Add(languageButton);
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
            LocalLabel(sidebar, "CATEYE  2.3", "CATEYE  2.3", 24, 531, 126, 20, 8, Color.FromArgb(100, 106, 117), false);
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
            settingsPanel = new CardPanel { Bounds = new Rectangle(192, 370, 704, 118) }; settingsPanel.Resize += delegate { LayoutSettingsPanel(); }; Controls.Add(settingsPanel);
            qualityTitle = LocalLabel(settingsPanel, "画质与体积", "Quality & size", 18, 13, 250, 23, 9, Theme.Muted, false);
            ConfigureCombo(qualityBox, new Rectangle(18, 40, 250, 36));
            SetComboItems(qualityBox, new string[] { "高清省空间 · MP4（推荐）", "原画无损 · MKV" }, new string[] { "HQ MP4 · Compact", "Lossless MKV" });
            settingsPanel.Controls.Add(qualityBox); qualityBox.SelectedIndexChanged += delegate { UpdateQualityHint(); };
            qualityHint = LocalLabel(settingsPanel, "原分辨率 · 高质量压缩", "Native resolution · High-quality compression", 18, 83, 456, 22, 8, Theme.Muted, false);
            fpsTitle = LocalLabel(settingsPanel, "帧率", "Frame rate", 430, 13, 180, 23, 9, Theme.Muted, false);
            ConfigureCombo(fpsBox, new Rectangle(430, 40, 180, 36));
            SetComboItems(fpsBox, new string[] { "15 FPS · 文档演示", "30 FPS · 日常录制", "60 FPS · 流畅动态" }, new string[] { "15 FPS · Docs", "30 FPS · Daily", "60 FPS · Smooth" }); settingsPanel.Controls.Add(fpsBox);
            audioTitle = LocalLabel(settingsPanel, "录制声音", "Audio", 480, 13, 206, 23, 9, Theme.Muted, false);
            ConfigureCombo(audioBox, new Rectangle(480, 40, 206, 28));
            SetAudioItems(); audioBox.SelectedIndex = (int)AudioMode.System;
            settingsPanel.Controls.Add(audioBox);
            LayoutSettingsPanel();
            saveLabel = LocalLabel(this, "保存到", "Save to", 194, 493, 72, 26, 9, Theme.Muted, false);
            outputBox.Bounds = new Rectangle(264, 491, 505, 28); outputBox.BackColor = Theme.Card; outputBox.ForeColor = Theme.Text;
            outputBox.BorderStyle = BorderStyle.FixedSingle; outputBox.Font = Theme.Font(9, false);
            outputBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "CatEyeRecordings"); Controls.Add(outputBox);
            changeFolderButton = LocalButton(this, "更改目录", "Change folder", 784, 484, 112, 36, delegate { BrowseOutput(); });
            statusLabel = LocalLabel(this, "就绪 · 主窗口自动隐藏，控制条不入镜", "Ready · Controls stay hidden", 194, 541, 495, 23, 9, Theme.Text, false);
            latestLabel = LocalLabel(this, "Ctrl + Shift + F9 暂停 / 继续    Ctrl + Shift + F10 停止", "F9 pause / resume    F10 stop", 194, 569, 500, 32, 8, Theme.Muted, false);
            latestLabel.AutoEllipsis = true;
            latestLabel.Cursor = Cursors.Default;
            latestLabel.Click += delegate { if (!string.IsNullOrEmpty(latestFile) && File.Exists(latestFile)) Process.Start(latestFile); };
            BindButton(startButton, "●  开始录制", "●  Start recording"); startButton.Primary = true; startButton.Font = Theme.Font(11, true);
            startButton.Bounds = new Rectangle(710, 545, 186, 49); Controls.Add(startButton);
            startButton.Click += StartButton_Click;
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            // Keep the click boundary safe even if a platform-specific startup API
            // throws before the recording session has been created.  Without this
            // guard an async event exception can leave the form busy with no visible
            // feedback, making a second click appear to do nothing.
            try { await StartRecordingAsync(); }
            catch (Exception ex)
            {
                busy = false; saving = false; startButton.Enabled = true;
                Native.AllowCapture(Handle); Show(); WindowState = FormWindowState.Normal; Activate();
                statusLabel.Text = Localization.Text("录制启动失败", "Recording could not start"); statusLabel.ForeColor = Theme.Red;
                try
                {
                    string directory = outputBox.Text.Trim();
                    if (!string.IsNullOrEmpty(directory))
                    {
                        directory = Path.GetFullPath(directory); Directory.CreateDirectory(directory);
                        File.WriteAllText(Path.Combine(directory, "猫眼-启动错误-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log"), ex.ToString());
                    }
                }
                catch { }
                MessageBox.Show(this, ex.Message, Localization.Text("猫眼录屏 · 录制启动失败", "CatEye Screen Recorder · Start failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            for (int i = 0; i < chinese.Length; i++) combo.Items.Add(Localization.Text(chinese[i], english[i]));
            combo.SelectedIndex = Math.Min(selected, chinese.Length - 1);
        }
        private void ShowLanguageMenu()
        {
            if (languageMenu == null || languageMenu.IsDisposed)
            {
                languageMenu = new ContextMenuStrip { BackColor = Theme.Card, ForeColor = Theme.Text, Font = Theme.Font(9, false), ShowImageMargin = false };
                languageMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            }
            languageMenu.Items.Clear();
            for (int i = 0; i < Localization.Languages.Length; i++)
            {
                LanguageOption option = Localization.Languages[i];
                ToolStripMenuItem item = new ToolStripMenuItem(option.NativeName) { Tag = option.Language, Checked = option.Language == Localization.Current, ForeColor = Theme.Text };
                item.Click += delegate(object sender, EventArgs e)
                {
                    ToolStripMenuItem clicked = (ToolStripMenuItem)sender;
                    Localization.Current = (AppLanguage)clicked.Tag;
                    ApplyLanguage(); SaveSettings();
                };
                languageMenu.Items.Add(item);
            }
            languageMenu.Show(languageButton, new Point(0, languageButton.Height));
        }
        private async Task CheckForUpdatesAsync()
        {
            if (updateCheckStarted || !UpdateChecker.Enabled) return;
            updateCheckStarted = true;
            try
            {
                ReleaseInfo release = await UpdateChecker.CheckLatestAsync();
                if (release == null || IsDisposed || busy || session != null) return;
                string prompt = Localization.Format("发现新版本 {0}，是否下载更新？", "Version {0} is available. Download the update?", release.TagName);
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
            languageButton.Text = Localization.ShortCode;
            Text = Localization.ProductTitle; tray.Text = Localization.ProductName;
            trayPause.Text = Localization.Text("暂停录制", "Pause recording"); trayStop.Text = Localization.Text("停止并保存", "Stop and save");
            SetComboItems(qualityBox, new string[] { "高清省空间 · MP4（推荐）", "原画无损 · MKV" }, new string[] { "HQ MP4 · Compact", "Lossless MKV" });
            SetComboItems(fpsBox, new string[] { "15 FPS · 文档演示", "30 FPS · 日常录制", "60 FPS · 流畅动态" }, new string[] { "15 FPS · Docs", "30 FPS · Daily", "60 FPS · Smooth" });
            SetAudioItems();
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
        private void SetAudioItems()
        {
            SetComboItems(audioBox, new string[] { "静音（仅画面）", "电脑声音", "麦克风", "电脑声音 + 麦克风" },
                new string[] { "No audio", "System audio", "Microphone", "System + mic" });
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
            busy = true; saving = false;
            try
            {
                SaveSettings(); startButton.Enabled = false;
                statusLabel.Text = Localization.Text("正在准备录制…", "Preparing recording…"); statusLabel.ForeColor = Theme.Accent;
                Native.ExcludeFromCapture(Handle); Hide();
                await Task.Delay(180); Native.FlushDesktop();
                toolbar = new RecordingToolbar(); toolbar.PauseRequested += TogglePause; toolbar.StopRequested += StopRecording;
                bool floating = toolbar.ShowSafely(Screen.PrimaryScreen.WorkingArea);
                // Do not add a notification-area icon to the recorded desktop when the protected toolbar is available.
                tray.Visible = !floating; trayPause.Enabled = trayStop.Enabled = true;
                if (!floating) tray.ShowBalloonTip(4000, Localization.ProductName, Localization.Text("此系统不支持排除悬浮条，已使用托盘控制。右键托盘图标可暂停或停止。", "This system cannot exclude the floating bar, so the tray controls are used. Right-click the tray icon to pause or stop."), ToolTipIcon.Info);
                session = new RecordingSession(area, fps, path, quality, (AudioMode)Math.Max(0, audioBox.SelectedIndex));
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
                root.SetAttribute("output", outputBox.Text); root.SetAttribute("quality", qualityBox.SelectedIndex.ToString()); root.SetAttribute("fps", fpsBox.SelectedIndex.ToString()); root.SetAttribute("audio", audioBox.SelectedIndex.ToString()); root.SetAttribute("language", Localization.Code); doc.Save(settingsPath);
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
                Localization.SetFromCode(root.GetAttribute("language"));
                int value; if (int.TryParse(root.GetAttribute("quality"), out value) && value >= 0 && value < 2) qualityBox.SelectedIndex = value;
                if (int.TryParse(root.GetAttribute("fps"), out value) && value >= 0 && value < 3) fpsBox.SelectedIndex = value;
                if (int.TryParse(root.GetAttribute("audio"), out value) && value >= 0 && value < 4) audioBox.SelectedIndex = value;
            }
            catch (Exception) { }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); if (busy || session != null) Native.ExcludeFromCapture(Handle); else Native.AllowCapture(Handle);
            bool pause = Native.SetHotkey(Handle, 1, Keys.F9), stop = Native.SetHotkey(Handle, 2, Keys.F10);
            if ((!pause || !stop) && latestLabel != null) latestLabel.Text = Localization.Text("快捷键被占用时，请使用悬浮条或托盘控制", "If a shortcut is busy, use the floating bar or tray controls.");
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (layingOutSettings || settingsPanel == null || settingsPanel.IsDisposed || settingsPanel.Width <= 0) return;
            layingOutSettings = true;
            try { LayoutSettingsPanel(); }
            finally { layingOutSettings = false; }
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            Native.ClearHotkey(Handle, 1); Native.ClearHotkey(Handle, 2); base.OnHandleDestroyed(e);
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0312) { if (message.WParam.ToInt32() == 1) TogglePause(); else if (message.WParam.ToInt32() == 2) StopRecording(); }
            if (message.Msg == 0x02E0 && IsHandleCreated)
            {
                try { BeginInvoke(new Action(NormalizeSettingsLayout)); } catch (InvalidOperationException) { }
            }
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
            if (disposing) { uiTimer.Dispose(); tray.Dispose(); if (languageMenu != null) languageMenu.Dispose(); if (toolbar != null) toolbar.Dispose(); }
            base.Dispose(disposing);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }
}
