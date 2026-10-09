using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FreeWindowsScreenRecorder
{
    internal enum VideoQuality { HighQuality, Lossless }

    internal sealed class FfmpegWriter : IDisposable
    {
        private readonly Process process;
        private readonly Stream input;
        private readonly byte[] pixels;
        private readonly int width, height;
        private readonly StringBuilder errors = new StringBuilder();
        private bool finished;
        internal static string EncoderPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg.exe"); } }

        public FfmpegWriter(string path, int width, int height, int fps, VideoQuality quality)
        {
            if (!File.Exists(EncoderPath)) throw new FileNotFoundException("缺少视频编码组件，请保持 tools 文件夹与录屏软件在一起。", EncoderPath);
            this.width = width; this.height = height;
            pixels = new byte[checked(width * height * 3)];
            string codec = quality == VideoQuality.Lossless
                ? "-c:v libx264rgb -crf 0 -preset veryfast -pix_fmt bgr24 "
                : "-c:v libx264 -crf 18 -preset veryfast -pix_fmt " + ((width % 2 == 0 && height % 2 == 0) ? "yuv420p " : "yuv444p ");
            string mux = quality == VideoQuality.Lossless ? "-f matroska " : "-movflags +faststart -f mp4 ";
            ProcessStartInfo start = new ProcessStartInfo(EncoderPath,
                "-hide_banner -loglevel error -nostats -n -f rawvideo -pixel_format bgr24 -video_size " + width + "x" + height +
                " -framerate " + fps + " -i pipe:0 -an " + codec + "-threads 4 " + mux + Quote(path));
            start.UseShellExecute = false; start.CreateNoWindow = true;
            start.RedirectStandardInput = true; start.RedirectStandardError = true;
            process = new Process(); process.StartInfo = start;
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null) lock (errors) { if (errors.Length < 12000) errors.AppendLine(e.Data); }
            };
            try { process.Start(); process.BeginErrorReadLine(); input = process.StandardInput.BaseStream; }
            catch { process.Dispose(); throw; }
            HidePartialWhenCreated(path);
        }

        private static void HidePartialWhenCreated(string path)
        {
            // Explorer should not try to generate thumbnails for an actively growing file.
            for (int attempt = 0; attempt < 20 && !File.Exists(path); attempt++) Thread.Sleep(25);
            if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Hidden);
        }

        internal static string Quote(string value)
        {
            // Windows argv quoting, including paths that end in a backslash.
            StringBuilder result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                slashes = 0; result.Append(c);
            }
            result.Append('\\', slashes * 2); return result.Append('"').ToString();
        }

        public void AddFrame(Bitmap bitmap)
        {
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                // Packed top-down RGB, signed stride also handles bottom-up source bitmaps.
                for (int y = 0; y < height; y++)
                    Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), pixels, y * width * 3, width * 3);
            }
            finally { bitmap.UnlockBits(data); }
            try { input.Write(pixels, 0, pixels.Length); }
            catch (IOException ex) { throw new IOException("视频编码失败。" + ErrorText(), ex); }
        }

        private string ErrorText() { lock (errors) return errors.ToString(); }

        public void Finish()
        {
            if (finished) return;
            finished = true;
            try { input.Close(); }
            catch (IOException) { }
            if (!process.WaitForExit(60000))
            {
                process.Kill(); process.WaitForExit();
                throw new IOException("视频保存超时，临时文件已保留。" + ErrorText());
            }
            process.WaitForExit(); // Drain asynchronous stderr callbacks.
            if (process.ExitCode != 0) throw new IOException("视频保存失败。" + ErrorText());
        }

        public void Dispose()
        {
            try { if (!finished) Finish(); }
            finally { process.Dispose(); }
        }
    }

    internal sealed class RecordingSession
    {
        private readonly object gate = new object();
        private readonly RecordingClock activeClock = new RecordingClock();
        private readonly AutoResetEvent changed = new AutoResetEvent(false);
        private readonly Rectangle area;
        private readonly int fps;
        private readonly string outputPath;
        private readonly VideoQuality quality;
        private readonly AudioMode audioMode;
        private bool recordingReady;
        private bool stopping, paused;
        private Thread worker;
        private long frameCount;
        internal Action<Graphics, Rectangle> CaptureFrame = Native.Capture;
        public event Action<string, Exception> Completed;
        public bool IsPaused { get { lock (gate) return paused; } }
        public TimeSpan Elapsed { get { lock (gate) return activeClock.Elapsed; } }
        public long FrameCount { get { return Interlocked.Read(ref frameCount); } }
        public string OutputPath { get { return outputPath; } }

        public RecordingSession(Rectangle area, int fps, string path, VideoQuality quality)
            : this(area, fps, path, quality, AudioMode.None) { }

        public RecordingSession(Rectangle area, int fps, string path, VideoQuality quality, AudioMode audioMode)
        {
            if (area.Width < 2 || area.Height < 2) throw new ArgumentException("录制区域至少为 2 × 2 像素。");
            if (fps < 1 || fps > 120) throw new ArgumentOutOfRangeException("fps");
            if (!Enum.IsDefined(typeof(AudioMode), audioMode)) throw new ArgumentOutOfRangeException("audioMode");
            this.area = area; this.fps = fps; outputPath = path; this.quality = quality;
            this.audioMode = audioMode;
        }

        public void Start()
        {
            lock (gate)
            {
                if (worker != null) throw new InvalidOperationException("Already started.");
                worker = new Thread(Run); worker.IsBackground = true; worker.Name = "CatEye capture";
                worker.Start();
            }
        }

        public void TogglePause()
        {
            lock (gate)
            {
                if (stopping) return;
                paused = !paused;
                if (paused) activeClock.Pause(); else if (recordingReady) activeClock.Resume();
                changed.Set();
            }
        }

        public void Stop()
        {
            lock (gate) { if (stopping) return; stopping = true; activeClock.Pause(); changed.Set(); }
        }

        private void Run()
        {
            string partial = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + ".recording" + Path.GetExtension(outputPath));
            // A private working folder also keeps Explorer away from unfinished media.
            string work = audioMode == AudioMode.None ? null : Path.Combine(Path.GetDirectoryName(outputPath), ".cateye-" + Guid.NewGuid().ToString("N"));
            string video = work == null ? partial : Path.Combine(work, "video" + Path.GetExtension(outputPath));
            string sound = work == null ? null : Path.Combine(work, "audio.flac");
            string combined = work == null ? null : Path.Combine(work, "combined" + Path.GetExtension(outputPath));
            AudioRecording audio = null;
            Exception failure = null;
            try
            {
                if (work != null)
                {
                    Directory.CreateDirectory(work); File.SetAttributes(work, FileAttributes.Hidden | FileAttributes.Directory);
                    audio = new AudioRecording(activeClock, audioMode, sound); audio.Start();
                }
                using (FfmpegWriter writer = new FfmpegWriter(video, area.Width, area.Height, fps, quality))
                using (Bitmap frame = new Bitmap(area.Width, area.Height, PixelFormat.Format24bppRgb))
                using (Graphics graphics = Graphics.FromImage(frame))
                {
                    lock (gate) { recordingReady = true; if (!paused && !stopping) activeClock.Resume(); }
                    bool captured = false;
                    while (true)
                    {
                        bool stop, pause; double seconds;
                        lock (gate) { stop = stopping; pause = paused; seconds = activeClock.Elapsed.TotalSeconds; }
                        if (audio != null) audio.CheckFailure();
                        if (stop) break;
                        if (pause) { changed.WaitOne(100); continue; }
                        long due = (long)Math.Floor(seconds * fps);
                        if (FrameCount > due) { changed.WaitOne(Math.Max(1, (int)((FrameCount / (double)fps - seconds) * 1000))); continue; }
                        CaptureFrame(graphics, area); captured = true;
                        // Repeat the latest frame when encoding falls behind, keeping duration correct.
                        do { writer.AddFrame(frame); Interlocked.Increment(ref frameCount); }
                        while (FrameCount <= due);
                    }
                    // Include only active time; a pause never becomes a frozen segment in the video.
                    if (!captured) { CaptureFrame(graphics, area); }
                    long total = Math.Max(1, (long)Math.Ceiling(Elapsed.TotalSeconds * fps));
                    while (FrameCount < total) { writer.AddFrame(frame); Interlocked.Increment(ref frameCount); }
                    if (audio != null) audio.Finish(FrameCount / (double)fps);
                    writer.Finish();
                }
                if (audio != null)
                {
                    AudioEncoder.Mux(video, sound, combined, quality, FrameCount / (double)fps);
                    File.Move(combined, outputPath);
                    // Delete only the two files created by this session after successful muxing.
                    try { File.Delete(video); File.Delete(sound); Directory.Delete(work); }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
                else File.Move(partial, outputPath);
                File.SetAttributes(outputPath, FileAttributes.Normal);
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                lock (gate) { stopping = true; activeClock.Pause(); }
                if (audio != null) audio.Dispose();
                if (failure != null && work != null && Directory.Exists(work))
                {
                    // Make recoverable media discoverable on device/encoder failure.
                    try
                    {
                        File.SetAttributes(work, FileAttributes.Directory);
                        foreach (string item in Directory.GetFiles(work)) File.SetAttributes(item, FileAttributes.Normal);
                    }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
                changed.Dispose();
                Action<string, Exception> done = Completed;
                if (done != null) done(failure == null ? outputPath : (work ?? partial), failure);
            }
        }
    }
}
