using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using FreeWindowsScreenRecorder;

internal static class UpgradeTests
{
    private static string output;
    [STAThread]
    private static int Main(string[] args)
    {
        output = args[0]; Directory.CreateDirectory(output);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        int result = 0;
        string preferences = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrameboxRecorder", "settings.xml");
        byte[] originalPreferences = File.Exists(preferences) ? File.ReadAllBytes(preferences) : null;
        try
        {
        using (Form harness = new Form { ShowInTaskbar = false, Opacity = 0 })
        {
            harness.Shown += delegate
            {
                harness.Hide();
                harness.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        RenderUi(); CheckSelector(); CheckExclusion(); WriteSamples(); CheckPause(); CheckMainWorkflow();
                        Console.WriteLine("PASS: UI, region selection, window exclusion, encoding, pause/resume.");
                    }
                    catch (Exception ex) { Console.Error.WriteLine(ex); result = 1; }
                    finally { harness.Close(); }
                }));
            };
            Application.Run(harness);
        }
        }
        finally
        {
            if (originalPreferences != null) File.WriteAllBytes(preferences, originalPreferences);
            else if (File.Exists(preferences)) File.Delete(preferences);
        }
        return result;
    }
    private static void RenderUi()
    {
        using (RecorderForm form = new RecorderForm())
        {
            IntPtr handle = form.Handle; form.Show(); Pump(150); form.PerformLayout();
            using (Bitmap image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, form.ClientRectangle); image.Save(Path.Combine(output, "main-ui.png")); }
            Rectangle capture = form.GetCaptureArea(); Console.WriteLine("Capture bounds: " + capture + "; UI: " + form.Size);
            Check(capture == SystemInformation.VirtualScreen, "Full-screen coverage");
            DarkSelect quality = (DarkSelect)Field(form, "qualityBox");
            DarkSelect fps = (DarkSelect)Field(form, "fpsBox");
            DarkSelect audio = (DarkSelect)Field(form, "audioBox");
            CardPanel settings = (CardPanel)Field(form, "settingsPanel");
            Label qualityHint = (Label)Field(form, "qualityHint");
            DarkButton language = (DarkButton)Field(form, "languageButton");
            DarkButton reselect = (DarkButton)Field(form, "selectButton");
            Check(quality.Height >= quality.Font.Height + 8 && fps.Height >= fps.Font.Height + 8, "High-DPI combo boxes keep a readable height");
            Check(quality.VisualHeight <= quality.Font.Height + 12 && fps.VisualHeight <= fps.Font.Height + 12, "Combo box frames stay close to the text height");
            Check(quality.Bottom <= settings.ClientSize.Height && fps.Bottom <= settings.ClientSize.Height && qualityHint.Top >= Math.Max(quality.Top + quality.VisualHeight, fps.Top + fps.VisualHeight), "Quality and frame-rate rows do not overlap");
            Check(quality.Cursor == Cursors.Default && fps.Cursor == Cursors.Default, "System default pointer on clickable controls");
            Check(quality.Right < fps.Left && fps.Right < audio.Left && audio.Right < settings.ClientSize.Width,
                "Quality, frame rate, and audio fit in one row");
            Check(audio.VisualHeight <= audio.Font.Height + 12 && audio.Items.Count == 4, "Four audio modes have a compact dropdown");
            Check(reselect.Bounds.X > 700 && reselect.Bounds.Y > 250, "Reselect button is beside the resolution preview");
            ReleaseInfo fixture = UpdateChecker.ParseReleaseForTest("{\"tag_name\":\"v9.9.0\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://github.com/example/cateye/releases/tag/v9.9.0\",\"assets\":[{\"name\":\"猫眼录屏-CatEyeScreenRecorder-v9.9-Windows-x64.zip\",\"browser_download_url\":\"https://github.com/example/cateye/releases/download/v9.9.0/update.zip\",\"digest\":\"sha256:abc\"}]}");
            Check(fixture != null && fixture.Version.Major == 9 && fixture.Version.Minor == 9 && fixture.AssetUrl.EndsWith("update.zip", StringComparison.Ordinal), "GitHub release response parsing");
            uint idleAffinity;
            Check(Native.GetWindowDisplayAffinity(form.Handle, out idleAffinity) && idleAffinity == 0, "Idle main window remains screenshot-capturable");
            MethodInfo showLanguage = typeof(RecorderForm).GetMethod("ShowLanguageMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            showLanguage.Invoke(form, null); Pump(30);
            ContextMenuStrip languageMenu = (ContextMenuStrip)Field(form, "languageMenu");
            Check(languageMenu != null && languageMenu.Items.Count == Localization.Languages.Length, "All language choices are available");
            for (int languageIndex = 0; languageIndex < Localization.Languages.Length; languageIndex++)
            {
                languageMenu.Items[languageIndex].PerformClick(); Pump(20);
                Check(form.Text.Length > 0 && language.Text == Localization.ShortCode, "Language switch " + Localization.Languages[languageIndex].Code);
                if (Localization.Current != AppLanguage.English && Localization.Current != AppLanguage.SimplifiedChinese)
                    Check(audio.Items[1].ToString() != "System audio", "Localized audio choices " + Localization.Code);
                if (Localization.Languages[languageIndex].Language == AppLanguage.TraditionalChinese) Check(form.Text.IndexOf("猫眼录屏", StringComparison.Ordinal) >= 0, "Traditional Chinese keeps the CatEye brand in Chinese");
                if (Localization.Languages[languageIndex].Language != AppLanguage.SimplifiedChinese && Localization.Languages[languageIndex].Language != AppLanguage.TraditionalChinese) Check(form.Text.IndexOf("CatEye", StringComparison.Ordinal) >= 0, "Non-Chinese languages keep the CatEye brand in English");
                if (Localization.Languages[languageIndex].Language == AppLanguage.TraditionalChinese)
                {
                    Check(quality.Items[0].ToString().IndexOf("HQ MP4", StringComparison.Ordinal) < 0 && fps.Items[1].ToString().IndexOf("Daily", StringComparison.Ordinal) < 0, "Traditional Chinese translates quality and frame-rate options");
                    using (AboutDialog aboutTraditional = new AboutDialog()) { Label featuresTraditional = (Label)Field(aboutTraditional, "featuresText"); Check(featuresTraditional.Text.IndexOf("No watermark", StringComparison.Ordinal) < 0, "Traditional Chinese translates the about dialog"); }
                }
                if (Localization.Languages[languageIndex].Language != AppLanguage.SimplifiedChinese && Localization.Languages[languageIndex].Language != AppLanguage.English) Check(quality.Items[0].ToString() != "HQ MP4 · Compact" && fps.Items[1].ToString() != "30 FPS · Daily", "Localized quality and frame-rate options " + Localization.Languages[languageIndex].Code);
                showLanguage.Invoke(form, null); Pump(20);
                languageMenu = (ContextMenuStrip)Field(form, "languageMenu");
            }
            Localization.Current = AppLanguage.SimplifiedChinese; MethodInfo applyLanguage = typeof(RecorderForm).GetMethod("ApplyLanguage", BindingFlags.Instance | BindingFlags.NonPublic); applyLanguage.Invoke(form, null); Pump(20);
            using (Bitmap english = new Bitmap(form.Width, form.Height)) { Localization.Current = AppLanguage.English; applyLanguage.Invoke(form, null); form.DrawToBitmap(english, form.ClientRectangle); english.Save(Path.Combine(output, "english-ui.png")); Localization.Current = AppLanguage.SimplifiedChinese; applyLanguage.Invoke(form, null); }
            using (AboutDialog about = new AboutDialog())
            {
                Label aboutText = (Label)Field(about, "featuresText");
                Check(about.Text == Localization.ProductTitle && aboutText.Text.IndexOf(Localization.IsEnglish ? "No watermark" : "无水印", StringComparison.Ordinal) >= 0, "Localized product introduction");
            }
            AppLanguage savedLanguage = Localization.Current;
            Localization.Current = AppLanguage.SimplifiedChinese;
            using (AboutDialog aboutZh = new AboutDialog()) using (Bitmap image = new Bitmap(aboutZh.Width, aboutZh.Height)) { aboutZh.Show(form); Pump(40); aboutZh.DrawToBitmap(image, aboutZh.ClientRectangle); image.Save(Path.Combine(output, "about-zh.png")); aboutZh.Close(); }
            Localization.Current = AppLanguage.English;
            using (AboutDialog aboutEn = new AboutDialog()) using (Bitmap image = new Bitmap(aboutEn.Width, aboutEn.Height)) { aboutEn.Show(form); Pump(40); aboutEn.DrawToBitmap(image, aboutEn.ClientRectangle); image.Save(Path.Combine(output, "about-en.png")); aboutEn.Close(); }
            Localization.Current = savedLanguage;
            quality.PerformClick(); Pump(50);
            ContextMenuStrip qualityMenu = (ContextMenuStrip)typeof(DarkSelect).GetField("menu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(quality);
            qualityMenu.Items[1].PerformClick(); Pump(50);
            fps.PerformClick(); Pump(50);
            ContextMenuStrip fpsMenu = (ContextMenuStrip)typeof(DarkSelect).GetField("menu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fps);
            fpsMenu.Items[2].PerformClick(); Pump(50);
            Check(quality.SelectedIndex == 1 && fps.SelectedIndex == 2, "Dropdown selection completes without disposing its menu");
            Console.WriteLine("PASS: normal system pointer and safe frame-rate/quality menu clicks.");
        }
    }
    private static void CheckSelector()
    {
        Check(RegionSelector.Normalize(new Point(90, 80), new Point(10, 15), new Rectangle(0, 0, 100, 100)) == new Rectangle(10, 15, 80, 65), "Reverse drag");
        Check(RegionSelector.Normalize(new Point(-10, -15), new Point(80, 90), new Rectangle(0, 0, 100, 100)) == new Rectangle(0, 0, 80, 90), "Clamp drag");
        using (Bitmap image = new Bitmap(320, 200))
        using (RegionSelector selector = new RegionSelector(new Rectangle(-1920, -200, 320, 200), image))
        {
            MethodInfo down = typeof(RegionSelector).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo up = typeof(RegionSelector).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic);
            down.Invoke(selector, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 210, 140, 0) });
            up.Invoke(selector, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 20, 30, 0) });
            Check(selector.SelectedArea == new Rectangle(-1900, -170, 190, 110), "Negative monitor origin translation");
        }
        Console.WriteLine("PASS: reverse drag, clamping, negative screen origins.");
    }
    private static void CheckExclusion()
    {
        using (Form background = new Form())
        using (RecordingToolbar toolbar = new RecordingToolbar())
        {
            background.FormBorderStyle = FormBorderStyle.None; background.StartPosition = FormStartPosition.Manual;
            background.Bounds = new Rectangle(90, 90, 820, 350); background.BackColor = Color.Magenta; background.TopMost = true; background.ShowInTaskbar = false;
            background.Show(); Pump(200);
            Check(toolbar.ShowSafely(background.Bounds), "WDA_EXCLUDEFROMCAPTURE enabled"); Pump(200);
            using (Bitmap ui = new Bitmap(toolbar.Width, toolbar.Height)) { toolbar.DrawToBitmap(ui, toolbar.ClientRectangle); ui.Save(Path.Combine(output, "toolbar-ui.png")); }
            using (Bitmap captured = Snapshot(toolbar.Bounds)) Check(IsMagenta(captured), "Excluded toolbar must reveal underlying pixels, without black rectangles");
            Native.SetWindowDisplayAffinity(toolbar.Handle, 0); Pump(200);
            using (Bitmap captured = Snapshot(toolbar.Bounds)) Check(!IsMagenta(captured), "Negative control: visible toolbar must be detectable");
            Check(Native.ExcludeFromCapture(toolbar.Handle), "Restore exclusion"); Pump(120);
            using (Bitmap captured = Snapshot(toolbar.Bounds)) Check(IsMagenta(captured), "Restored exclusion");
            bool paused = false, stopped = false;
            toolbar.PauseRequested += delegate { paused = true; }; toolbar.StopRequested += delegate { stopped = true; };
            toolbar.PauseButton.PerformClick(); toolbar.StopButton.PerformClick(); Check(paused && stopped, "Floating control actions");
            toolbar.UpdateState(TimeSpan.FromSeconds(12), true, false);
            using (Bitmap ui = new Bitmap(toolbar.Width, toolbar.Height)) { toolbar.DrawToBitmap(ui, toolbar.ClientRectangle); ui.Save(Path.Combine(output, "toolbar-paused-ui.png")); }
        }
        Console.WriteLine("PASS: floating controls excluded from real screen capture; underlying pixels preserved.");
    }
    private static Bitmap Snapshot(Rectangle bounds)
    {
        Native.FlushDesktop(); Bitmap result = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (Graphics graphics = Graphics.FromImage(result)) Native.Capture(graphics, bounds); return result;
    }
    private static bool IsMagenta(Bitmap image)
    {
        for (int y = 10; y < image.Height - 10; y += 9)
            for (int x = 10; x < image.Width - 10; x += 9)
                if (image.GetPixel(x, y).ToArgb() != Color.Magenta.ToArgb()) return false;
        return true;
    }
    private static void WriteSamples()
    {
        using (Bitmap frame = new Bitmap(1920, 1080, PixelFormat.Format24bppRgb))
        using (Graphics g = Graphics.FromImage(frame))
        using (Font font = new Font("Microsoft YaHei UI", 25))
        {
            g.Clear(Color.FromArgb(242, 244, 247));
            g.FillRectangle(Brushes.DarkSlateBlue, 0, 0, 1920, 100);
            g.DrawString("猫眼录屏 / TOP · 1920 × 1080 / 清晰文字", font, Brushes.White, 30, 28);
            for (int row = 0; row < 12; row++) g.DrawString("Screen recording 0123456789 · 屏幕文字清晰度测试 " + row, font, Brushes.Black, 40, 140 + row * 58);
            g.FillRectangle(Brushes.Red, 0, 1000, 120, 80); g.FillRectangle(Brushes.Blue, 1800, 1000, 120, 80);
            frame.Save(Path.Combine(output, "sample-source.png"));
            foreach (VideoQuality quality in new VideoQuality[] { VideoQuality.HighQuality, VideoQuality.Lossless })
            {
                string path = Path.Combine(output, quality == VideoQuality.Lossless ? "sample-lossless.mkv" : "sample-hq.mp4");
                using (FfmpegWriter writer = new FfmpegWriter(path, 1920, 1080, 30, quality))
                    for (int i = 0; i < 30; i++) writer.AddFrame(frame);
                Console.WriteLine(Path.GetFileName(path) + ": " + new FileInfo(path).Length + " bytes; raw RGB: " + (1920L * 1080 * 3 * 30));
            }
        }
        // Odd dimensions must remain exact, with no crop or rescale to satisfy a codec.
        using (Bitmap odd = new Bitmap(321, 181, PixelFormat.Format24bppRgb))
        {
            for (int y = 0; y < odd.Height; y++) for (int x = 0; x < odd.Width; x++) odd.SetPixel(x, y, Color.FromArgb(x % 256, y % 256, (x + y) % 256));
            odd.Save(Path.Combine(output, "odd-source.png"));
            using (FfmpegWriter writer = new FfmpegWriter(Path.Combine(output, "odd-lossless.mkv"), 321, 181, 15, VideoQuality.Lossless)) writer.AddFrame(odd);
            using (FfmpegWriter writer = new FfmpegWriter(Path.Combine(output, "odd-hq.mp4"), 321, 181, 15, VideoQuality.HighQuality)) writer.AddFrame(odd);
        }
        int stride = ((321 * 3 + 3) / 4) * 4;
        IntPtr memory = Marshal.AllocHGlobal(stride * 181);
        try
        {
            using (Bitmap negative = new Bitmap(321, 181, -stride, PixelFormat.Format24bppRgb, IntPtr.Add(memory, stride * 180)))
            {
                for (int y = 0; y < 181; y++) for (int x = 0; x < 321; x++) negative.SetPixel(x, y, Color.FromArgb(x % 256, y % 256, (x + y) % 256));
                BitmapData data = negative.LockBits(new Rectangle(0, 0, 321, 181), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                int actual = data.Stride; negative.UnlockBits(data); Check(actual < 0, "Negative stride fixture");
                using (FfmpegWriter writer = new FfmpegWriter(Path.Combine(output, "negative-lossless.mkv"), 321, 181, 15, VideoQuality.Lossless)) writer.AddFrame(negative);
            }
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    private static void CheckPause()
    {
        RecordingSession session = new RecordingSession(new Rectangle(0, 0, 320, 180), 15, Path.Combine(output, "pause.mkv"), VideoQuality.Lossless);
        int phase = 0; Exception error = null; using (ManualResetEvent done = new ManualResetEvent(false))
        {
            session.CaptureFrame = delegate(Graphics g, Rectangle area) { g.Clear(phase == 0 ? Color.Red : phase == 1 ? Color.Lime : Color.Blue); };
            session.Completed += delegate(string file, Exception ex) { error = ex; done.Set(); };
            session.Start(); Thread.Sleep(700); session.TogglePause(); Thread.Sleep(160);
            long before = session.FrameCount; double elapsed = session.Elapsed.TotalSeconds; phase = 1;
            Thread.Sleep(850); Check(session.FrameCount == before, "No frames while paused"); Check(Math.Abs(session.Elapsed.TotalSeconds - elapsed) < .001, "Timer frozen while paused");
            phase = 2; session.TogglePause(); Thread.Sleep(650); session.Stop(); Check(done.WaitOne(20000), "Recording completion");
            if (error != null) throw error;
            File.WriteAllText(Path.Combine(output, "pause-duration.txt"), session.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Console.WriteLine("PASS: pause excluded; active duration " + session.Elapsed.TotalSeconds.ToString("0.000") + "s, " + session.FrameCount + " frames.");
        }
    }
    private static void Pump(int ms)
    {
        Stopwatch clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); } Native.FlushDesktop();
    }
    private static object Field(object instance, string name) { return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance); }
    private static void CheckMainWorkflow()
    {
        string preferences = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrameboxRecorder", "settings.xml");
        byte[] original = File.Exists(preferences) ? File.ReadAllBytes(preferences) : null;
        try
        {
            using (RecorderForm form = new RecorderForm())
            using (System.Windows.Forms.Timer select = new System.Windows.Forms.Timer())
            {
                form.Show(); Pump(100); select.Interval = 60;
                bool selectionHidden = false;
                select.Tick += delegate
                {
                    RegionSelector selector = null;
                    foreach (Form open in Application.OpenForms) if (open is RegionSelector) selector = (RegionSelector)open;
                    if (selector == null) return;
                    select.Stop(); selectionHidden = !form.Visible;
                    typeof(RegionSelector).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(selector, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 50, 80, 0) });
                    typeof(RegionSelector).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(selector, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 451, 321, 0) });
                };
                Console.WriteLine("Workflow: select region");
                select.Start(); var selecting = form.SelectRegionAsync(); WaitUntil(delegate { return selecting.IsCompleted; }, 10000);
                Check(selectionHidden && form.Visible, "Main window hidden for selection and restored afterwards");
                Check(form.GetCaptureArea().Size == new Size(400, 240), "Selection boundary aligned to 2 pixels for standard MP4");
                ((TextBox)Field(form, "outputBox")).Text = output;
                ((DarkSelect)Field(form, "qualityBox")).SelectedIndex = 1;
                ((DarkSelect)Field(form, "audioBox")).SelectedIndex = 0;
                Console.WriteLine("Workflow: start recording");
                ((DarkButton)Field(form, "startButton")).PerformClick();
                WaitUntil(delegate { return Field(form, "session") != null; }, 10000);
                Console.WriteLine("Workflow: started");
                Check(!form.Visible, "Main window hidden throughout recording");
                RecordingSession active = (RecordingSession)Field(form, "session");
                RecordingToolbar toolbar = (RecordingToolbar)Field(form, "toolbar");
                Check(active != null && toolbar.Visible && toolbar.CaptureExcluded, "Protected floating toolbar visible");
                Pump(600); Console.WriteLine("Workflow: pause"); toolbar.PauseButton.PerformClick(); Pump(160); double paused = active.Elapsed.TotalSeconds;
                Pump(250); Check(active.IsPaused && active.Elapsed.TotalSeconds == paused, "Floating pause freezes active timer");
                Console.WriteLine("Workflow: resume"); toolbar.PauseButton.PerformClick(); Pump(350); Console.WriteLine("Workflow: stop"); toolbar.StopButton.PerformClick();
                WaitUntil(delegate { return Field(form, "session") == null; }, 20000);
                Check(form.Visible && File.Exists(active.OutputPath), "Stop saves video before restoring main window");
                uint restoredAffinity;
                Check(Native.GetWindowDisplayAffinity(form.Handle, out restoredAffinity) && restoredAffinity == 0, "Idle screenshot capture restored after recording");
                Check(toolbar.IsDisposed, "Toolbar closed after recording");
                Label latest = (Label)Field(form, "latestLabel");
                Check(latest.Cursor == Cursors.Hand && latest.ForeColor == Theme.Accent, "Saved recording link is visibly clickable");
                Console.WriteLine("PASS: actual UI flow: hide, select, record, floating pause/resume, stop, save, restore.");
            }
        }
        finally
        {
            if (original != null) File.WriteAllBytes(preferences, original);
            else if (File.Exists(preferences)) File.Delete(preferences);
        }
    }
    private static void WaitUntil(Func<bool> condition, int timeout)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (!condition()) { if (timer.ElapsedMilliseconds > timeout) throw new Exception("UI workflow timed out."); Pump(20); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
