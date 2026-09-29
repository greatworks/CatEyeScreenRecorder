using System;
using System.Drawing;
using System.Windows.Forms;

namespace FreeWindowsScreenRecorder
{
    internal enum AppLanguage
    {
        Chinese,
        English
    }

    internal static class Localization
    {
        internal static AppLanguage Current = AppLanguage.Chinese;
        internal static bool IsEnglish { get { return Current == AppLanguage.English; } }
        internal static string Text(string chinese, string english) { return IsEnglish ? english : chinese; }
        internal static string ProductName { get { return Text("猫眼录屏", "CatEye Screen Recorder"); } }
        internal static string ProductTitle { get { return Text("猫眼录屏 · CatEye Screen Recorder", "CatEye Screen Recorder"); } }
    }

    internal sealed class AboutDialog : Form
    {
        private readonly Label featuresText = new Label();

        internal AboutDialog()
        {
            SuspendLayout();
            Text = Localization.ProductTitle;
            BackColor = Theme.Background; ForeColor = Theme.Text;
            Font = Theme.Font(9, false); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi; FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(720, 580);
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel chrome = new Panel { Bounds = new Rectangle(0, 0, 720, 46), BackColor = Theme.Sidebar }; Controls.Add(chrome);
            Label chromeTitle = Theme.Label(chrome, Localization.ProductTitle, 18, 12, 560, 22, 9, Theme.Muted, false);
            chrome.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Native.DragWindow(this); };
            chromeTitle.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Native.DragWindow(this); };
            DarkButton closeTop = new DarkButton { Text = "×", Bounds = new Rectangle(676, 7, 36, 32), Danger = true, Chrome = true, TabStop = false, BackColor = Theme.Sidebar }; closeTop.Click += delegate { Close(); }; chrome.Controls.Add(closeTop);
            Label title = Theme.Label(this, Localization.Text("猫眼录屏", "CatEye Screen Recorder"), 28, 66, 664, 44, 18, Theme.Text, true);
            Label subtitle = Theme.Label(this, Localization.Text("无水印、轻量、完全免费的 Windows 录屏工具，打开就能录。", "Free, lightweight and watermark-free. Open and record."), 30, 108, 660, 28, 9, Theme.Accent, false);
            CardPanel card = new CardPanel { Bounds = new Rectangle(28, 146, 664, 354), AutoScroll = true, AutoScrollMinSize = new Size(640, 430) }; Controls.Add(card);
            Panel contentHost = new Panel { Location = new Point(0, 0), Size = new Size(640, 430), BackColor = Theme.Card }; card.Controls.Add(contentHost);
            Section(contentHost, Localization.Text("软件简介", "About"), new Rectangle(20, 14, 604, 24), 10, Theme.Accent, true);
            Section(contentHost, Localization.Text("猫眼录屏面向 Windows 用户，打开即可录制，轻量、无水印、完全免费。", "A free Windows recorder with no watermark, low resource use and quick setup. Open the app and start."), new Rectangle(20, 42, 604, 42), 9, Theme.Text, false);
            Section(contentHost, Localization.Text("核心特点", "Key features"), new Rectangle(20, 92, 604, 24), 10, Theme.Accent, true);
            featuresText = Section(contentHost, Features(), new Rectangle(20, 120, 604, 96), 9, Theme.Text, false);
            Section(contentHost, Localization.Text("适用场景", "Use cases"), new Rectangle(20, 224, 604, 24), 10, Theme.Accent, true);
            Section(contentHost, Localization.Text("网课、游戏、会议、教程、日常屏幕记录", "Classes, games, meetings, tutorials and daily screen records"), new Rectangle(20, 252, 604, 32), 9, Theme.Text, false);
            Section(contentHost, Localization.Text("收费模式：完全免费\n运行环境：Windows 7 / 10 / 11", "Pricing: Free\nEnvironment: Windows 7 / 10 / 11"), new Rectangle(20, 302, 604, 42), 9, Theme.Muted, false);
            DarkButton close = new DarkButton { Text = Localization.Text("关闭", "Close"), Bounds = new Rectangle(568, 526, 124, 38), Chrome = true, TabStop = false, BackColor = Theme.Accent, ForeColor = Theme.Background };
            close.Click += delegate { Close(); }; Controls.Add(close);
            Paint += delegate(object sender, PaintEventArgs e) { using (Pen border = new Pen(Theme.Border)) e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1); };
            ResumeLayout(true);
        }

        private Label Section(Control parent, string text, Rectangle bounds, float size, Color color, bool bold)
        {
            Label label = new Label { Text = text, Bounds = bounds, Font = Theme.Font(size, bold), ForeColor = color, BackColor = Color.Transparent, AutoSize = false, UseCompatibleTextRendering = true, TextAlign = ContentAlignment.TopLeft };
            parent.Controls.Add(label); return label;
        }
        private static string Features()
        {
            return Localization.Text("• 无水印，录制完成即可使用\n• 轻量低占用\n• 完全免费\n• 支持 Windows 7 / 10 / 11", "• No watermark\n• Lightweight\n• Completely free\n• Windows 7 / 10 / 11");
        }
    }
}
