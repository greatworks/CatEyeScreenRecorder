using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FreeWindowsScreenRecorder
{
    internal static class Theme
    {
        internal static readonly Color Background = Color.FromArgb(20, 21, 24);
        internal static readonly Color Sidebar = Color.FromArgb(16, 17, 20);
        internal static readonly Color Card = Color.FromArgb(30, 32, 37);
        internal static readonly Color Border = Color.FromArgb(48, 51, 58);
        internal static readonly Color Text = Color.FromArgb(237, 240, 245);
        internal static readonly Color Muted = Color.FromArgb(146, 152, 163);
        internal static readonly Color Accent = Color.FromArgb(82, 225, 194);
        internal static readonly Color Red = Color.FromArgb(255, 99, 111);
        internal static Font Font(float size, bool bold) { return new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point); }
        internal static GraphicsPath Round(RectangleF box, float radius)
        {
            GraphicsPath path = new GraphicsPath(); float d = radius * 2;
            path.AddArc(box.X, box.Y, d, d, 180, 90); path.AddArc(box.Right - d, box.Y, d, d, 270, 90);
            path.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90); path.AddArc(box.X, box.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        internal static Label Label(Control parent, string text, int x, int y, int width, int height, float size, Color color, bool bold)
        {
            Label label = new Label { Text = text, Bounds = new Rectangle(x, y, width, height), Font = Font(size, bold), ForeColor = color, BackColor = Color.Transparent, UseMnemonic = false };
            parent.Controls.Add(label); return label;
        }
        internal static void Mark(Graphics g, Rectangle box)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float left = box.Left + box.Width * .12f, right = box.Right - box.Width * .12f;
            float centerY = box.Top + box.Height * .52f, top = box.Top + box.Height * .26f, bottom = box.Top + box.Height * .78f;
            using (GraphicsPath eye = new GraphicsPath())
            using (Pen outline = new Pen(Accent, Math.Max(2, box.Width / 12f)))
            {
                eye.AddBezier(left, centerY, box.Left + box.Width * .30f, top, box.Right - box.Width * .30f, top, right, centerY);
                eye.AddBezier(right, centerY, box.Right - box.Width * .30f, bottom, box.Left + box.Width * .30f, bottom, left, centerY);
                using (Brush fill = new SolidBrush(Color.FromArgb(35, 77, 71))) g.FillPath(fill, eye);
                g.DrawPath(outline, eye);
            }
            RectangleF iris = new RectangleF(box.Left + box.Width * .37f, box.Top + box.Height * .32f, box.Width * .26f, box.Height * .40f);
            using (Brush brush = new SolidBrush(Accent)) g.FillEllipse(brush, iris);
            using (Brush pupil = new SolidBrush(Background)) g.FillEllipse(pupil, new RectangleF(iris.X + iris.Width * .34f, iris.Y + iris.Height * .10f, iris.Width * .32f, iris.Height * .80f));
            using (Brush highlight = new SolidBrush(Text)) g.FillEllipse(highlight, new RectangleF(iris.X + iris.Width * .63f, iris.Y + iris.Height * .17f, Math.Max(1.5f, box.Width * .045f), Math.Max(1.5f, box.Width * .045f)));
        }
    }

    internal class DarkButton : Button
    {
        internal bool Selected, Primary, Danger, Chrome;
        protected bool Hovered { get; private set; }
        public DarkButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Default;
            Font = Theme.Font(9, false); ForeColor = Theme.Text; BackColor = Theme.Card;
            // Use the normal Windows arrow pointer for buttons; selection tools keep their own standard cursor.
            Cursor = Cursors.Default;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e) { Hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Chrome)
            {
                using (Brush brush = new SolidBrush(Hovered ? Color.FromArgb(30, 32, 37) : BackColor)) g.FillRectangle(brush, ClientRectangle);
                Color chromeInk = !Enabled ? Theme.Muted : Danger ? Theme.Red : ForeColor;
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, chromeInk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            Color fill = Primary ? Theme.Accent : Selected ? Color.FromArgb(30, 60, 56) : Hovered ? Color.FromArgb(47, 50, 58) : BackColor;
            Color ink = !Enabled ? Theme.Muted : Primary ? Theme.Background : Danger ? Theme.Red : Selected ? Theme.Accent : ForeColor;
            RectangleF box = new RectangleF(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
            float radius = Math.Min(Math.Max(6, Height / 7f), Math.Min(box.Width, box.Height) / 2f);
            using (GraphicsPath path = Theme.Round(box, radius))
            using (Brush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(Selected || Focused ? Theme.Accent : Theme.Border)) { g.FillPath(brush, path); g.DrawPath(pen, path); }
            Rectangle textBounds = TextAlign == ContentAlignment.MiddleLeft
                ? new Rectangle(14, 0, Math.Max(1, Width - 20), Height)
                : ClientRectangle;
            TextFormatFlags textFlags = TextFormatFlags.VerticalCenter |
                (TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, Text, Font, textBounds, ink, textFlags);
        }
    }

    internal class CardPanel : Panel
    {
        public CardPanel() { BackColor = Theme.Card; DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    internal sealed class DarkSelect : DarkButton
    {
        internal readonly List<object> Items = new List<object>();
        internal Rectangle LogicalBounds { get; set; }
        private int selectedIndex = -1;
        private ContextMenuStrip menu;
        public event EventHandler SelectedIndexChanged;
        internal int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value < 0 || value >= Items.Count) return;
                selectedIndex = value; Text = Items[value].ToString(); Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }
        public DarkSelect()
        {
            AccessibleRole = AccessibleRole.ComboBox;
            AutoSize = false;
            // Keep text and the arrow inside the control when a display uses a larger DPI.
            MinimumSize = new Size(0, 36);
        }
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            EnsureReadableHeight();
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            EnsureReadableHeight();
        }
        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            NormalizeDpiBounds();
            EnsureReadableHeight();
        }
        internal void NormalizeDpiBounds()
        {
            if (Parent == null || Parent.Width <= 0 || LogicalBounds.Width <= 0) return;
            float scale = Parent.Width / 704f;
            if (scale <= 0f) scale = 1f;
            int height = Math.Max((int)Math.Ceiling(36f * scale), Font.Height + 16);
            Rectangle expected = new Rectangle(
                (int)Math.Round(LogicalBounds.X * scale),
                (int)Math.Round(LogicalBounds.Y * scale),
                (int)Math.Round(LogicalBounds.Width * scale),
                height);
            if (Bounds != expected) Bounds = expected;
        }
        private void EnsureReadableHeight()
        {
            float parentScale = Parent == null || Parent.Width <= 0 ? 1f : Parent.Width / 704f;
            int scaledMinimum = (int)Math.Ceiling(36f * parentScale);
            int minimumHeight = Math.Max(36, Math.Max(Font.Height + 16, scaledMinimum));
            if (Height < minimumHeight) Height = minimumHeight;
        }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (menu == null || menu.IsDisposed)
            {
                menu = new ContextMenuStrip { BackColor = Theme.Card, ForeColor = Theme.Text, Font = Font, ShowImageMargin = false };
                menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            }
            menu.Items.Clear();
            for (int i = 0; i < Items.Count; i++)
            {
                int index = i; ToolStripMenuItem item = new ToolStripMenuItem(Items[i].ToString()) { Checked = i == selectedIndex, ForeColor = Theme.Text };
                item.Click += delegate { SelectedIndex = index; }; menu.Items.Add(item);
            }
            // Do not dispose here. WinForms is still inside OnItemClicked when Closed fires;
            // disposing at that point causes the ObjectDisposedException reported by users.
            menu.Show(this, new Point(0, Height));
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                SelectedIndex = Math.Max(0, Math.Min(Items.Count - 1, selectedIndex + (e.KeyCode == Keys.Down ? 1 : -1)));
                e.Handled = true; return;
            }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            NormalizeDpiBounds();
            EnsureReadableHeight();
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = Primary ? Theme.Accent : Selected ? Color.FromArgb(30, 60, 56) : Hovered ? Color.FromArgb(47, 50, 58) : BackColor;
            Color ink = !Enabled ? Theme.Muted : Primary ? Theme.Background : Danger ? Theme.Red : Selected ? Theme.Accent : ForeColor;
            RectangleF box = new RectangleF(1, 1, Math.Max(1, Width - 2), Math.Max(1, Height - 2));
            float radius = Math.Min(Math.Max(6, Height / 7f), Math.Min(box.Width, box.Height) / 2f);
            using (GraphicsPath path = Theme.Round(box, radius))
            using (Brush brush = new SolidBrush(fill))
            using (Pen border = new Pen(Selected || Focused ? Theme.Accent : Theme.Border)) { g.FillPath(brush, path); g.DrawPath(border, path); }

            int arrowInset = Math.Max(14, Font.Height);
            int arrowCenter = Math.Max(8, Width - arrowInset - 2);
            int half = Math.Max(3, Font.Height / 6);
            using (Pen arrow = new Pen(Theme.Muted, Math.Max(1f, Font.Height / 12f)))
                g.DrawLines(arrow, new Point[] { new Point(arrowCenter - half, Height / 2 - half / 2), new Point(arrowCenter, Height / 2 + half / 2), new Point(arrowCenter + half, Height / 2 - half / 2) });

            Rectangle textBounds = new Rectangle(14, 0, Math.Max(1, Width - arrowInset - 18), Height);
            TextRenderer.DrawText(g, Text, Font, textBounds, ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && menu != null) { menu.Dispose(); menu = null; }
            base.Dispose(disposing);
        }
    }

    internal sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(43, 64, 61); } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
    }

    internal sealed class CapturePreview : Panel
    {
        internal string Dimensions = "1920 × 1080";
        internal string Mode = "全屏录制";
        public CapturePreview() { DoubleBuffered = true; BackColor = Theme.Card; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; float scale = Width / 704f;
            g.ScaleTransform(scale, scale); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen grid = new Pen(Color.FromArgb(40, 44, 51)))
                for (int x = 12; x < 704; x += 24) for (int y = 12; y < 148; y += 24) g.DrawLine(grid, x, y, x + 1, y);
            Rectangle screen = new Rectangle(36, 25, 168, 92);
            using (Pen pen = new Pen(Color.FromArgb(85, 104, 112), 2))
            using (GraphicsPath shape = Theme.Round(screen, 8)) { g.DrawPath(pen, shape); g.DrawLine(pen, 104, 126, 136, 126); g.DrawLine(pen, 120, 117, 120, 126); }
            Theme.Mark(g, new Rectangle(96, 47, 48, 48));
            using (Font heading = new Font("Microsoft YaHei UI", 30, FontStyle.Bold, GraphicsUnit.Pixel)) using (Font body = new Font("Microsoft YaHei UI", 13, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush bright = new SolidBrush(Theme.Text)) using (Brush muted = new SolidBrush(Theme.Muted))
            {
                g.DrawString(Dimensions, heading, bright, 238, 41);
                g.DrawString(Mode + "  ·  " + Localization.Text("原始分辨率", "Native resolution"), body, muted, 241, 85);
            }
            using (Pen border = new Pen(Theme.Border)) g.DrawRectangle(border, 0, 0, 703, 147);
        }
    }
}
