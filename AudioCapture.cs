using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FreeWindowsScreenRecorder
{
    internal enum AudioMode { None, System, Microphone, SystemAndMicrophone }

    // One QPC timeline for video and audio. Packets crossing pause/resume boundaries
    // are sliced, so neither buffered microphone data nor silence extends a pause.
    internal sealed class RecordingClock
    {
        internal struct Span { internal double SourceOffset, ActiveStart, Duration; }
        private sealed class Segment { internal double Start, End, Offset; }
        private readonly object gate = new object();
        private readonly List<Segment> segments = new List<Segment>();
        private double total;
        private bool running;
        internal static double Now { get { return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency; } }
        internal TimeSpan Elapsed { get { lock (gate) return TimeSpan.FromSeconds(total + (running ? Now - segments[segments.Count - 1].Start : 0)); } }
        internal void Resume() { ResumeAt(Now); }
        internal void Pause() { PauseAt(Now); }
        internal void ResumeAt(double time)
        {
            lock (gate) { if (running) return; segments.Add(new Segment { Start = time, End = Double.PositiveInfinity, Offset = total }); running = true; }
        }
        internal void PauseAt(double time)
        {
            lock (gate)
            {
                if (!running) return;
                Segment segment = segments[segments.Count - 1]; segment.End = Math.Max(segment.Start, time);
                total += segment.End - segment.Start; running = false;
            }
        }
        internal List<Span> Map(double start, double duration)
        {
            List<Span> result = new List<Span>();
            lock (gate)
            {
                // Most packets only touch the most recent interval.
                for (int i = segments.Count - 1; i >= 0; i--)
                {
                    Segment segment = segments[i];
                    if (segment.End <= start) break;
                    double left = Math.Max(start, segment.Start), right = Math.Min(start + duration, segment.End);
                    if (right > left) result.Add(new Span { SourceOffset = left - start, ActiveStart = segment.Offset + left - segment.Start, Duration = right - left });
                }
            }
            result.Reverse(); return result;
        }
    }

    // Fixed-size ring: silence is represented by empty slots. Loopback devices may
    // send no packets at all when the PC is silent; wall time must still advance.
    internal sealed class AudioMixer
    {
        internal const int Rate = 48000;
        private const int Capacity = Rate * 5;
        private readonly float[] samples = new float[Capacity * 2];
        internal long Written { get; private set; }
        internal void Add(long frame, float left, float right)
        {
            if (frame < Written) return;
            if (frame >= Written + Capacity) throw new IOException("Audio buffer overflow.");
            int offset = (int)(frame % Capacity) * 2;
            samples[offset] += left; samples[offset + 1] += right;
        }
        internal byte[] Read(int frames)
        {
            byte[] bytes = new byte[frames * 4];
            for (int i = 0; i < frames; i++)
            {
                int offset = (int)(Written++ % Capacity) * 2;
                for (int channel = 0; channel < 2; channel++)
                {
                    float value = samples[offset + channel]; samples[offset + channel] = 0;
                    short pcm = (short)Math.Round(Math.Max(-1f, Math.Min(1f, value)) * 32767);
                    bytes[i * 4 + channel * 2] = (byte)pcm;
                    bytes[i * 4 + channel * 2 + 1] = (byte)(pcm >> 8);
                }
            }
            return bytes;
        }
    }

    internal sealed class AudioPacketConverter
    {
        private readonly WaveFormat format;
        private readonly bool floating;
        private long lastFrame = -1;
        internal AudioPacketConverter(WaveFormat format)
        {
            this.format = format;
            WaveFormatExtensible extended = format as WaveFormatExtensible;
            Guid floatId = new Guid("00000003-0000-0010-8000-00aa00389b71");
            Guid pcmId = new Guid("00000001-0000-0010-8000-00aa00389b71");
            floating = format.Encoding == WaveFormatEncoding.IeeeFloat || (extended != null && extended.SubFormat == floatId);
            bool pcm = format.Encoding == WaveFormatEncoding.Pcm || (extended != null && extended.SubFormat == pcmId);
            if ((!floating && !pcm) || (floating && format.BitsPerSample != 32) ||
                (!floating && format.BitsPerSample != 16 && format.BitsPerSample != 24 && format.BitsPerSample != 32) ||
                format.Channels < 1 || format.Channels > 8 || format.SampleRate < 8000)
                throw new NotSupportedException("Unsupported audio device format: " + format);
        }
        private float Sample(byte[] data, int frame, int channel)
        {
            int offset = frame * format.BlockAlign + channel * (format.BitsPerSample / 8);
            if (floating) { float value = BitConverter.ToSingle(data, offset); return Single.IsNaN(value) || Single.IsInfinity(value) ? 0 : value; }
            if (format.BitsPerSample == 16) return BitConverter.ToInt16(data, offset) / 32768f;
            if (format.BitsPerSample == 32) return BitConverter.ToInt32(data, offset) / 2147483648f;
            int sample = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
            if ((sample & 0x800000) != 0) sample |= unchecked((int)0xff000000);
            return sample / 8388608f;
        }
        private float Stereo(byte[] data, int frame, int side)
        {
            if (format.Channels == 1) return Sample(data, frame, 0);
            float value = Sample(data, frame, side);
            // Standard Windows surround order: FL, FR, FC, LFE, BL, BR, SL, SR.
            if (format.Channels > 2) value += Sample(data, frame, 2) * .7071f;
            if (format.Channels > 3) value += Sample(data, frame, 3) * .25f;
            for (int c = 4 + side; c < format.Channels; c += 2) value += Sample(data, frame, c) * .7071f;
            return format.Channels > 2 ? value / 2.5f : value;
        }
        internal void Add(byte[] data, int frames, double timestamp, bool silent, RecordingClock clock, AudioMixer mixer, float gain)
        {
            if (frames <= 0) return;
            foreach (RecordingClock.Span span in clock.Map(timestamp, frames / (double)format.SampleRate))
            {
                long start = Math.Max(lastFrame + 1, (long)Math.Round(span.ActiveStart * AudioMixer.Rate));
                long end = (long)Math.Round((span.ActiveStart + span.Duration) * AudioMixer.Rate);
                for (long target = start; target < end; target++)
                {
                    double position = (span.SourceOffset + target / (double)AudioMixer.Rate - span.ActiveStart) * format.SampleRate;
                    position = Math.Max(0, Math.Min(frames - 1, position));
                    int a = (int)position, b = Math.Min(frames - 1, a + 1); float fraction = (float)(position - a);
                    float left = 0, right = 0;
                    if (!silent)
                    {
                        left = Stereo(data, a, 0) * (1 - fraction) + Stereo(data, b, 0) * fraction;
                        right = Stereo(data, a, 1) * (1 - fraction) + Stereo(data, b, 1) * fraction;
                    }
                    mixer.Add(target, left * gain, right * gain);
                }
                lastFrame = Math.Max(lastFrame, end - 1);
            }
        }
    }

    internal sealed class AudioEncoder : IDisposable
    {
        private readonly Process process;
        private readonly StringBuilder errors = new StringBuilder();
        private bool finished;
        internal AudioEncoder(string path)
        {
            ProcessStartInfo start = new ProcessStartInfo(FfmpegWriter.EncoderPath,
                "-hide_banner -loglevel error -nostats -n -f s16le -ar 48000 -ac 2 -i pipe:0 -vn -c:a flac -compression_level 0 -threads 1 " + FfmpegWriter.Quote(path));
            start.UseShellExecute = false; start.CreateNoWindow = true; start.RedirectStandardInput = true; start.RedirectStandardError = true;
            process = new Process { StartInfo = start };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (errors) { if (errors.Length < 8000) errors.AppendLine(e.Data); } };
            try { process.Start(); process.BeginErrorReadLine(); }
            catch { process.Dispose(); throw; }
        }
        internal void Write(byte[] bytes) { process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length); }
        internal void Finish()
        {
            if (finished) return; finished = true;
            try { process.StandardInput.Close(); } catch (IOException) { }
            if (!process.WaitForExit(60000)) { process.Kill(); process.WaitForExit(); throw new IOException("Audio encoder timed out."); }
            process.WaitForExit(); if (process.ExitCode != 0) throw new IOException("Audio encoder failed: " + errors);
        }
        public void Dispose() { try { Finish(); } finally { process.Dispose(); } }
        internal static void Mux(string video, string audio, string output, VideoQuality quality, double seconds)
        {
            string mux = quality == VideoQuality.HighQuality ? "-movflags +faststart -f mp4 " : "-f matroska ";
            ProcessStartInfo start = new ProcessStartInfo(FfmpegWriter.EncoderPath,
                "-hide_banner -loglevel error -nostats -n -i " + FfmpegWriter.Quote(video) + " -i " + FfmpegWriter.Quote(audio) +
                " -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -ar 48000 -ac 2 -threads 2 -t " + seconds.ToString("0.000000", CultureInfo.InvariantCulture) + " " + mux + FfmpegWriter.Quote(output));
            start.UseShellExecute = false; start.CreateNoWindow = true; start.RedirectStandardError = true;
            using (Process process = new Process { StartInfo = start })
            {
                StringBuilder errors = new StringBuilder();
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (errors) { if (errors.Length < 8000) errors.AppendLine(e.Data); } };
                process.Start(); process.BeginErrorReadLine();
                // Long recordings need longer to mux; this step does not re-encode video.
                if (!process.WaitForExit((int)Math.Min(Int32.MaxValue, Math.Max(120000, seconds * 1000))))
                { process.Kill(); process.WaitForExit(); throw new IOException("Audio/video save timed out. Temporary files were retained."); }
                process.WaitForExit(); if (process.ExitCode != 0) throw new IOException("Audio/video save failed: " + errors);
            }
        }
    }

    internal sealed class AudioRecording : IDisposable
    {
        private sealed class Source : IDisposable
        {
            private MMDevice device;
            private AudioClient client;
            private AudioCaptureClient capture;
            private AudioPacketConverter converter;
            private WaveFormat format;
            private byte[] buffer = new byte[0];
            internal Source(MMDeviceEnumerator enumerator, bool loopback)
            {
                try
                {
                    device = enumerator.GetDefaultAudioEndpoint(loopback ? DataFlow.Render : DataFlow.Capture, Role.Console);
                    client = device.AudioClient; format = client.MixFormat; converter = new AudioPacketConverter(format);
                    client.Initialize(AudioClientShareMode.Shared, loopback ? AudioClientStreamFlags.Loopback : AudioClientStreamFlags.None,
                        1000000, 0, format, Guid.Empty);
                    capture = client.AudioCaptureClient; client.Start();
                }
                catch { Dispose(); throw; }
            }
            internal void Read(RecordingClock clock, AudioMixer mixer, float gain)
            {
                while (capture.GetNextPacketSize() > 0)
                {
                    int frames; long position, qpc; AudioClientBufferFlags flags;
                    IntPtr data = capture.GetBuffer(out frames, out flags, out position, out qpc);
                    try
                    {
                        int count = checked(frames * format.BlockAlign);
                        bool silent = (flags & AudioClientBufferFlags.Silent) != 0;
                        if (buffer.Length < count) buffer = new byte[count];
                        if (!silent && count > 0) Marshal.Copy(data, buffer, 0, count);
                        double timestamp = qpc / 10000000.0;
                        if ((flags & AudioClientBufferFlags.TimestampError) != 0 || qpc <= 0)
                            timestamp = RecordingClock.Now - frames / (double)format.SampleRate;
                        converter.Add(buffer, frames, timestamp, silent, clock, mixer, gain);
                    }
                    finally { capture.ReleaseBuffer(frames); }
                }
            }
            public void Dispose()
            {
                if (client != null) { try { client.Stop(); } catch (COMException) { } }
                if (capture != null) { capture.Dispose(); capture = null; }
                if (client != null) { client.Dispose(); client = null; }
                if (device != null) { device.Dispose(); device = null; }
            }
        }
        private readonly RecordingClock clock;
        private readonly AudioMode mode;
        private readonly string path;
        private readonly string failureMessage;
        private readonly ManualResetEvent ready = new ManualResetEvent(false);
        private readonly AutoResetEvent changed = new AutoResetEvent(false);
        private Thread thread;
        private volatile bool stopping;
        private volatile Exception failure;
        private long finalFrames;
        internal AudioRecording(RecordingClock clock, AudioMode mode, string path)
        {
            this.clock = clock; this.mode = mode; this.path = path;
            failureMessage = Localization.Text("声音录制失败。请检查 Windows 默认播放设备、默认麦克风和麦克风隐私权限，或选择静音后重试。", "Audio recording failed. Check Windows default playback/microphone devices and microphone permissions, or choose No audio and retry.");
        }
        internal void Start()
        {
            thread = new Thread(Run) { IsBackground = true, Name = "CatEye audio" }; thread.SetApartmentState(ApartmentState.STA); thread.Start();
            if (!ready.WaitOne(15000)) throw new IOException("Audio device initialization timed out.");
            CheckFailure();
        }
        internal void CheckFailure() { if (failure != null) throw new IOException(failureMessage, failure); }
        internal void Finish(double seconds)
        {
            Interlocked.Exchange(ref finalFrames, (long)Math.Round(seconds * AudioMixer.Rate));
            stopping = true; changed.Set();
            if (!thread.Join(70000)) throw new IOException("Audio save timed out.");
            CheckFailure();
        }
        private void Run()
        {
            List<Source> sources = new List<Source>();
            try
            {
                using (MMDeviceEnumerator enumerator = new MMDeviceEnumerator())
                {
                    if (mode == AudioMode.System || mode == AudioMode.SystemAndMicrophone) sources.Add(new Source(enumerator, true));
                    if (mode == AudioMode.Microphone || mode == AudioMode.SystemAndMicrophone) sources.Add(new Source(enumerator, false));
                }
                using (AudioEncoder encoder = new AudioEncoder(path))
                {
                    AudioMixer mixer = new AudioMixer(); ready.Set(); bool hidden = false;
                    while (true)
                    {
                        foreach (Source source in sources) source.Read(clock, mixer, sources.Count == 2 ? .5f : 1f);
                        if (!hidden && File.Exists(path)) { File.SetAttributes(path, FileAttributes.Hidden); hidden = true; }
                        bool stop = stopping;
                        long target = stop ? Interlocked.Read(ref finalFrames) : Math.Max(0, (long)((clock.Elapsed.TotalSeconds - .25) * AudioMixer.Rate));
                        while (mixer.Written < target) encoder.Write(mixer.Read((int)Math.Min(4800, target - mixer.Written)));
                        if (stop) break;
                        changed.WaitOne(10);
                    }
                    // Release recording devices before waiting for the encoder to finish.
                    foreach (Source source in sources) source.Dispose(); sources.Clear();
                    encoder.Finish();
                }
            }
            catch (Exception ex) { failure = ex; }
            finally { foreach (Source source in sources) source.Dispose(); ready.Set(); }
        }
        public void Dispose()
        {
            if (thread != null && thread.IsAlive)
            {
                Interlocked.Exchange(ref finalFrames, (long)(clock.Elapsed.TotalSeconds * AudioMixer.Rate));
                stopping = true; changed.Set(); thread.Join(70000);
            }
            if (thread == null || !thread.IsAlive) { ready.Dispose(); changed.Dispose(); }
        }
    }
}
