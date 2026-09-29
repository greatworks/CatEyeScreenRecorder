using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FreeWindowsScreenRecorder
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new RecorderForm());
        }
    }

    internal sealed class RecorderForm : Form
    {
        private readonly TextBox outputBox = new TextBox();
        private readonly Button browseButton = new Button();
        private readonly RadioButton fullScreenRadio = new RadioButton();
        private readonly RadioButton customAreaRadio = new RadioButton();
        private readonly NumericUpDown xBox = new NumericUpDown();
        private readonly NumericUpDown yBox = new NumericUpDown();
        private readonly NumericUpDown widthBox = new NumericUpDown();
        private readonly NumericUpDown heightBox = new NumericUpDown();
        private readonly NumericUpDown fpsBox = new NumericUpDown();
        private readonly Button startButton = new Button();
        private readonly Button stopButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly Label timerLabel = new Label();
        private readonly TextBox logBox = new TextBox();

        private volatile bool recording;
        private Thread captureThread;
        private Stopwatch stopwatch;
        private string currentFile;

        public RecorderForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "免费 Windows 屏幕录像";
            Width = 760;
            Height = 560;
            MinimumSize = new Size(720, 520);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();
            UpdateAreaState();
            ResumeLayout(true);
        }

        private void BuildUi()
        {
            Label title = new Label();
            title.Text = "免费 Windows 屏幕录像";
            title.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(18, 16);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "单文件 EXE，无需 Python/FFmpeg。输出无压缩 AVI，文件会比较大。";
            subtitle.AutoSize = true;
            subtitle.ForeColor = Color.FromArgb(80, 80, 80);
            subtitle.Location = new Point(21, 54);
            Controls.Add(subtitle);

            Label outputLabel = new Label();
            outputLabel.Text = "保存到";
            outputLabel.AutoSize = true;
            outputLabel.Location = new Point(21, 94);
            Controls.Add(outputLabel);

            outputBox.Location = new Point(92, 90);
            outputBox.Width = 520;
            outputBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            outputBox.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "ScreenRecordings");
            Controls.Add(outputBox);

            browseButton.Text = "浏览";
            browseButton.Location = new Point(625, 88);
            browseButton.Width = 92;
            browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseButton.Click += BrowseButton_Click;
            Controls.Add(browseButton);

            Label captureLabel = new Label();
            captureLabel.Text = "录制范围";
            captureLabel.AutoSize = true;
            captureLabel.Location = new Point(21, 136);
            Controls.Add(captureLabel);

            fullScreenRadio.Text = "全屏";
            fullScreenRadio.Checked = true;
            fullScreenRadio.AutoSize = true;
            fullScreenRadio.Location = new Point(92, 132);
            fullScreenRadio.CheckedChanged += delegate { UpdateAreaState(); };
            Controls.Add(fullScreenRadio);

            customAreaRadio.Text = "自定义区域";
            customAreaRadio.AutoSize = true;
            customAreaRadio.Location = new Point(165, 132);
            customAreaRadio.CheckedChanged += delegate { UpdateAreaState(); };
            Controls.Add(customAreaRadio);

            GroupBox areaGroup = new GroupBox();
            areaGroup.Text = "自定义区域";
            areaGroup.Location = new Point(21, 170);
            areaGroup.Size = new Size(696, 76);
            areaGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(areaGroup);

            AddNumber(areaGroup, "X", xBox, 18, 32, 0, 99999, 0);
            AddNumber(areaGroup, "Y", yBox, 174, 32, 0, 99999, 0);
            AddNumber(areaGroup, "宽度", widthBox, 330, 32, 2, 99999, Screen.PrimaryScreen.Bounds.Width);
            AddNumber(areaGroup, "高度", heightBox, 522, 32, 2, 99999, Screen.PrimaryScreen.Bounds.Height);

            Label fpsLabel = new Label();
            fpsLabel.Text = "FPS";
            fpsLabel.AutoSize = true;
            fpsLabel.Location = new Point(21, 266);
            Controls.Add(fpsLabel);

            fpsBox.Location = new Point(92, 262);
            fpsBox.Minimum = 1;
            fpsBox.Maximum = 30;
            fpsBox.Value = 10;
            fpsBox.Width = 80;
            Controls.Add(fpsBox);

            Label hint = new Label();
            hint.Text = "建议 10 FPS 左右；无压缩 AVI 适合短录制，后续可用剪映/HandBrake 压成 MP4。";
            hint.AutoSize = true;
            hint.ForeColor = Color.FromArgb(90, 90, 90);
            hint.Location = new Point(190, 266);
            Controls.Add(hint);

            startButton.Text = "开始录制";
            startButton.Location = new Point(21, 310);
            startButton.Size = new Size(120, 36);
            startButton.Click += StartButton_Click;
            Controls.Add(startButton);

            stopButton.Text = "停止";
            stopButton.Location = new Point(153, 310);
            stopButton.Size = new Size(92, 36);
            stopButton.Enabled = false;
            stopButton.Click += StopButton_Click;
            Controls.Add(stopButton);

            timerLabel.Text = "00:00:00";
            timerLabel.Font = new Font("Consolas", 15F, FontStyle.Bold);
            timerLabel.AutoSize = true;
            timerLabel.Location = new Point(610, 316);
            timerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(timerLabel);

            statusLabel.Text = "就绪";
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(21, 365);
            Controls.Add(statusLabel);

            logBox.Location = new Point(21, 392);
            logBox.Size = new Size(696, 116);
            logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            logBox.Multiline = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.ReadOnly = true;
            logBox.Font = new Font("Consolas", 9F);
            Controls.Add(logBox);

            System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 500;
            uiTimer.Tick += delegate { UpdateTimer(); };
            uiTimer.Start();
        }

        private static void AddNumber(Control parent, string labelText, NumericUpDown box, int x, int y, int min, int max, int value)
        {
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Location = new Point(x, y + 4);
            parent.Controls.Add(label);

            box.Location = new Point(x + 52, y);
            box.Width = 82;
            box.Minimum = min;
            box.Maximum = max;
            box.Value = Math.Min(Math.Max(value, min), max);
            parent.Controls.Add(box);
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择录屏保存目录";
                dialog.SelectedPath = outputBox.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    outputBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (recording)
            {
                return;
            }

            Rectangle area = GetCaptureArea();
            if (area.Width < 2 || area.Height < 2)
            {
                MessageBox.Show(this, "录制区域太小。", "无法开始录制", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputDir = outputBox.Text.Trim();
            if (outputDir.Length == 0)
            {
                MessageBox.Show(this, "请选择保存目录。", "无法开始录制", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "保存目录错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            currentFile = Path.Combine(outputDir, "recording-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".avi");
            int fps = (int)fpsBox.Value;

            recording = true;
            stopwatch = Stopwatch.StartNew();
            startButton.Enabled = false;
            stopButton.Enabled = true;
            statusLabel.Text = "正在录制到 " + currentFile;
            Log("开始录制：" + currentFile);
            Log(string.Format("录制范围：{0} × {1} 像素，起点 ({2}, {3})", area.Width, area.Height, area.Left, area.Top));

            captureThread = new Thread(new ThreadStart(delegate { CaptureLoop(area, fps, currentFile); }));
            captureThread.IsBackground = true;
            captureThread.Start();
        }

        private Rectangle GetCaptureArea()
        {
            if (fullScreenRadio.Checked)
            {
                // app.manifest disables DPI virtualization, so these are physical pixels
                // for the entire virtual desktop (including negative monitor origins).
                return SystemInformation.VirtualScreen;
            }

            return new Rectangle(
                (int)xBox.Value,
                (int)yBox.Value,
                (int)widthBox.Value,
                (int)heightBox.Value);
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            StopRecording();
        }

        private void StopRecording()
        {
            if (!recording)
            {
                return;
            }

            recording = false;
            stopButton.Enabled = false;
            statusLabel.Text = "正在停止，请稍等...";
        }

        private void CaptureLoop(Rectangle area, int fps, string path)
        {
            int frameCount = 0;
            long frameTicks = Stopwatch.Frequency / Math.Max(1, fps);
            Stopwatch clock = Stopwatch.StartNew();

            try
            {
                using (AviWriter writer = new AviWriter(path, area.Width, area.Height, fps))
                {
                    while (recording)
                    {
                        long targetTicks = frameCount * frameTicks;
                        using (Bitmap frame = new Bitmap(area.Width, area.Height, PixelFormat.Format24bppRgb))
                        {
                            using (Graphics g = Graphics.FromImage(frame))
                            {
                                g.CopyFromScreen(area.Left, area.Top, 0, 0, area.Size, CopyPixelOperation.SourceCopy);
                            }
                            writer.AddFrame(frame);
                        }

                        frameCount++;
                        if (frameCount % Math.Max(1, fps) == 0)
                        {
                            BeginInvoke(new Action(delegate { Log("已写入帧数：" + frameCount); }));
                        }

                        long nextTicks = targetTicks + frameTicks;
                        int sleepMs = (int)((nextTicks - clock.ElapsedTicks) * 1000 / Stopwatch.Frequency);
                        if (sleepMs > 1)
                        {
                            Thread.Sleep(sleepMs);
                        }
                    }
                }

                BeginInvoke(new Action(delegate
                {
                    statusLabel.Text = "录制已保存：" + path;
                    Log("录制完成：" + path);
                    ResetRecordingUi();
                }));
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(delegate
                {
                    statusLabel.Text = "录制失败：" + ex.Message;
                    Log("错误：" + ex);
                    ResetRecordingUi();
                }));
            }
        }

        private void ResetRecordingUi()
        {
            recording = false;
            stopwatch = null;
            startButton.Enabled = true;
            stopButton.Enabled = false;
            timerLabel.Text = "00:00:00";
        }

        private void UpdateAreaState()
        {
            bool enabled = customAreaRadio.Checked;
            xBox.Enabled = enabled;
            yBox.Enabled = enabled;
            widthBox.Enabled = enabled;
            heightBox.Enabled = enabled;
        }

        private void UpdateTimer()
        {
            if (stopwatch == null)
            {
                return;
            }

            TimeSpan elapsed = stopwatch.Elapsed;
            timerLabel.Text = string.Format("{0:00}:{1:00}:{2:00}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);
        }

        private void Log(string message)
        {
            logBox.AppendText(message + Environment.NewLine);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (recording)
            {
                DialogResult result = MessageBox.Show(
                    this,
                    "正在录制，是否停止录制并退出？",
                    "正在录制",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }

                StopRecording();
                Thread.Sleep(300);
            }

            base.OnFormClosing(e);
        }
    }

    internal sealed class AviWriter : IDisposable
    {
        private const int OF_WRITE = 0x00000001;
        private const int OF_CREATE = 0x00001000;
        private const int AVIIF_KEYFRAME = 0x00000010;
        private const int BI_RGB = 0;

        private readonly int width;
        private readonly int height;
        private readonly int stride;
        private readonly int imageSize;
        private readonly byte[] buffer;
        private IntPtr aviFile = IntPtr.Zero;
        private IntPtr aviStream = IntPtr.Zero;
        private int frameIndex;
        private bool disposed;

        public AviWriter(string path, int width, int height, int fps)
        {
            this.width = width;
            this.height = height;
            stride = ((width * 24 + 31) / 32) * 4;
            imageSize = stride * height;
            buffer = new byte[imageSize];

            AVIFileInit();
            Check(AVIFileOpenW(out aviFile, path, OF_WRITE | OF_CREATE, IntPtr.Zero), "AVIFileOpen");

            AVISTREAMINFO streamInfo = new AVISTREAMINFO();
            streamInfo.fccType = MakeFourCC('v', 'i', 'd', 's');
            streamInfo.fccHandler = 0;
            streamInfo.dwScale = 1;
            streamInfo.dwRate = fps;
            streamInfo.dwSuggestedBufferSize = imageSize;
            streamInfo.dwQuality = -1;
            streamInfo.rcFrame.right = width;
            streamInfo.rcFrame.bottom = height;
            streamInfo.szName = "Screen Capture";

            Check(AVIFileCreateStream(aviFile, out aviStream, ref streamInfo), "AVIFileCreateStream");

            BITMAPINFOHEADER bitmapInfo = new BITMAPINFOHEADER();
            bitmapInfo.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bitmapInfo.biWidth = width;
            // Store a conventional bottom-up RGB DIB for AVI player compatibility.
            bitmapInfo.biHeight = height;
            bitmapInfo.biPlanes = 1;
            bitmapInfo.biBitCount = 24;
            bitmapInfo.biCompression = BI_RGB;
            bitmapInfo.biSizeImage = (uint)imageSize;

            Check(AVIStreamSetFormat(aviStream, 0, ref bitmapInfo, Marshal.SizeOf(typeof(BITMAPINFOHEADER))), "AVIStreamSetFormat");
        }

        public void AddFrame(Bitmap bitmap)
        {
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int copyBytes = width * 3;

                for (int y = 0; y < height; y++)
                {
                    // Scan0 is the logical top row; the signed stride locates each next row.
                    IntPtr sourceRow = AddToPointer(data.Scan0, y * data.Stride);
                    // Positive biHeight requires the bottom row first in the AVI buffer.
                    // Copy only pixels so the buffer's DWORD-alignment padding stays zero.
                    Marshal.Copy(sourceRow, buffer, (height - 1 - y) * stride, copyBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            Check(AVIStreamWrite(aviStream, frameIndex, 1, buffer, imageSize, AVIIF_KEYFRAME, IntPtr.Zero, IntPtr.Zero), "AVIStreamWrite");
            frameIndex++;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (aviStream != IntPtr.Zero)
            {
                AVIStreamRelease(aviStream);
                aviStream = IntPtr.Zero;
            }
            if (aviFile != IntPtr.Zero)
            {
                AVIFileRelease(aviFile);
                aviFile = IntPtr.Zero;
            }
            AVIFileExit();
        }

        private static IntPtr AddToPointer(IntPtr pointer, int offset)
        {
            return new IntPtr(pointer.ToInt64() + offset);
        }

        private static int MakeFourCC(char a, char b, char c, char d)
        {
            return ((byte)a) | ((byte)b << 8) | ((byte)c << 16) | ((byte)d << 24);
        }

        private static void Check(int result, string action)
        {
            if (result != 0)
            {
                throw new InvalidOperationException(action + " failed with code " + result);
            }
        }

        [DllImport("avifil32.dll")]
        private static extern void AVIFileInit();

        [DllImport("avifil32.dll")]
        private static extern void AVIFileExit();

        [DllImport("avifil32.dll", EntryPoint = "AVIFileOpenW", CharSet = CharSet.Unicode)]
        private static extern int AVIFileOpenW(out IntPtr ppfile, string szFile, int uMode, IntPtr lpHandler);

        [DllImport("avifil32.dll")]
        private static extern int AVIFileCreateStream(IntPtr pfile, out IntPtr ppavi, ref AVISTREAMINFO psi);

        [DllImport("avifil32.dll")]
        private static extern int AVIStreamSetFormat(IntPtr pavi, int lPos, ref BITMAPINFOHEADER lpFormat, int cbFormat);

        [DllImport("avifil32.dll")]
        private static extern int AVIStreamWrite(
            IntPtr pavi,
            int lStart,
            int lSamples,
            byte[] lpBuffer,
            int cbBuffer,
            int dwFlags,
            IntPtr plSampWritten,
            IntPtr plBytesWritten);

        [DllImport("avifil32.dll")]
        private static extern int AVIStreamRelease(IntPtr aviStream);

        [DllImport("avifil32.dll")]
        private static extern int AVIFileRelease(IntPtr aviFile);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct AVISTREAMINFO
        {
            public int fccType;
            public int fccHandler;
            public int dwFlags;
            public int dwCaps;
            public short wPriority;
            public short wLanguage;
            public int dwScale;
            public int dwRate;
            public int dwStart;
            public int dwLength;
            public int dwInitialFrames;
            public int dwSuggestedBufferSize;
            public int dwQuality;
            public int dwSampleSize;
            public RECT rcFrame;
            public int dwEditCount;
            public int dwFormatChangeCount;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public int biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }
    }
}
