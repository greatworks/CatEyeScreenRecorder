using System;
using System.Drawing;
using System.Windows.Forms;

namespace FreeWindowsScreenRecorder
{
    internal sealed class RegionSelector : Form
    {
        private readonly Bitmap desktop;
        private Point start;
        private Rectangle selected;
        private bool dragging;
        public Rectangle SelectedArea { get; private set; }
        public RegionSelector(Rectangle bounds, Bitmap desktop)
        {
            this.desktop = desktop; FormBorderStyle = FormBorderStyle.None; AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual; Bounds = bounds; TopMost = true; ShowInTaskbar = false;
            Cursor = Cursors.Cross; DoubleBuffered = true; KeyPreview = true; Text = Localization.Text("猫眼录屏 · 框选区域", "CatEye Screen Recorder · Select area");
        }
        internal static Rectangle Normalize(Point a, Point b, Rectangle bounds)
        {
            return Rectangle.Intersect(bounds, Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
        }
        private Rectangle DragBounds(Point point)
        {
            Rectangle bounds = Normalize(start, point, ClientRectangle);
            // Show the exact capture boundary, aligned to 2 pixels for broadly playable 4:2:0 MP4.
            bounds.Width &= ~1; bounds.Height &= ~1; return bounds;
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; return; }
            if (e.Button != MouseButtons.Left) return;
            start = e.Location; selected = Rectangle.Empty; dragging = true; Capture = true; Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); if (!dragging) return;
            selected = DragBounds(e.Location); Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e); if (!dragging || e.Button != MouseButtons.Left) return;
            selected = DragBounds(e.Location); dragging = false; Capture = false;
            if (selected.Width < 2 || selected.Height < 2) { selected = Rectangle.Empty; Invalidate(); return; }
            SelectedArea = new Rectangle(Left + selected.X, Top + selected.Y, selected.Width, selected.Height);
            DialogResult = DialogResult.OK;
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.DrawImageUnscaled(desktop, 0, 0);
            using (Brush dim = new SolidBrush(Color.FromArgb(140, 10, 12, 16)))
            {
                if (selected.IsEmpty) g.FillRectangle(dim, ClientRectangle);
                else
                {
                    g.FillRectangle(dim, 0, 0, Width, selected.Top);
                    g.FillRectangle(dim, 0, selected.Bottom, Width, Height - selected.Bottom);
                    g.FillRectangle(dim, 0, selected.Top, selected.Left, selected.Height);
                    g.FillRectangle(dim, selected.Right, selected.Top, Width - selected.Right, selected.Height);
                }
            }
            if (!selected.IsEmpty) using (Pen pen = new Pen(Theme.Accent, 2)) g.DrawRectangle(pen, selected);
            string text = selected.IsEmpty ? Localization.Text("拖动鼠标框选矩形区域   ·   Esc 或右键取消", "Drag to select a rectangle   ·   Esc or right-click to cancel") : selected.Width + " × " + selected.Height + Localization.Text(" 像素   ·   松开鼠标完成", " pixels   ·   release to finish");
            using (Font font = Theme.Font(11, true))
            {
                Size size = TextRenderer.MeasureText(text, font); int x = Math.Max(12, Math.Min(Width - size.Width - 28, selected.IsEmpty ? (Width - size.Width) / 2 : selected.Left));
                int y = selected.IsEmpty ? 36 : Math.Max(12, selected.Top - size.Height - 24);
                using (Brush background = new SolidBrush(Theme.Background)) g.FillRectangle(background, x - 10, y - 8, size.Width + 20, size.Height + 16);
                TextRenderer.DrawText(g, text, font, new Point(x, y), Theme.Accent);
            }
        }
    }

    internal sealed class RecordingToolbar : Form
    {
        internal readonly DarkButton PauseButton = new DarkButton();
        internal readonly DarkButton StopButton = new DarkButton();
        private readonly Label time;
        private readonly Label state;
        public event Action PauseRequested;
        public event Action StopRequested;
        internal bool CaptureExcluded { get; private set; }
        protected override bool ShowWithoutActivation { get { return true; } }
        public RecordingToolbar()
        {
            SuspendLayout(); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false; TopMost = true; ClientSize = new Size(360, 76); BackColor = Theme.Card;
            Text = Localization.Text("猫眼录屏 · 录制控制", "CatEye Screen Recorder · Recording controls");
            state = Theme.Label(this, Localization.Text("● 正在录制", "● Recording"), 16, 12, 122, 18, 8, Theme.Red, false);
            time = Theme.Label(this, "00:00:00", 15, 32, 128, 32, 17, Theme.Text, true);
            PauseButton.Text = Localization.Text("Ⅱ  暂停", "Ⅱ  Pause"); PauseButton.Bounds = new Rectangle(154, 18, 90, 40);
            StopButton.Text = Localization.Text("■  停止", "■  Stop"); StopButton.Danger = true; StopButton.Bounds = new Rectangle(254, 18, 90, 40);
            Controls.Add(PauseButton); Controls.Add(StopButton);
            PauseButton.Click += delegate { if (PauseRequested != null) PauseRequested(); };
            StopButton.Click += delegate { if (StopRequested != null) StopRequested(); };
            ResumeLayout(true);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); CaptureExcluded = Native.ExcludeFromCapture(Handle);
        }
        internal bool ShowSafely(Rectangle workingArea)
        {
            IntPtr handle = Handle;
            if (!CaptureExcluded) return false;
            Location = new Point(workingArea.Right - Width - 18, workingArea.Bottom - Height - 18);
            Show(); Native.FlushDesktop(); return true;
        }
        internal void UpdateState(TimeSpan elapsed, bool paused, bool saving)
        {
            time.Text = string.Format("{0:00}:{1:00}:{2:00}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);
            state.Text = saving ? Localization.Text("正在保存…", "Saving…") : paused ? Localization.Text("● 已暂停", "● Paused") : Localization.Text("● 正在录制", "● Recording");
            state.ForeColor = paused ? Color.FromArgb(242, 195, 106) : Theme.Red;
            PauseButton.Text = paused ? Localization.Text("▶  继续", "▶  Resume") : Localization.Text("Ⅱ  暂停", "Ⅱ  Pause");
            PauseButton.Enabled = StopButton.Enabled = !saving;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; if (StopRequested != null) StopRequested(); }
            base.OnFormClosing(e);
        }
    }
}
