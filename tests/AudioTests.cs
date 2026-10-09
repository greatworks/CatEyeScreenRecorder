using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using FreeWindowsScreenRecorder;
using NAudio.Wave;
using NAudio.CoreAudioApi;

internal static class AudioTests
{
    private static void Check(bool condition, string description)
    { if (!condition) throw new Exception(description); Console.WriteLine("PASS: " + description); }
    private static int Main(string[] args)
    {
        try
        {
            string output = args[0]; Directory.CreateDirectory(output);
            CheckTimeline(); CheckFormats(); CheckMix(output); CheckNoAudioAndDevices(output);
            Console.WriteLine("PASS: all available audio checks completed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static RecordingClock Clock()
    {
        RecordingClock clock = new RecordingClock(); clock.ResumeAt(100); clock.PauseAt(101);
        clock.ResumeAt(101.5); clock.PauseAt(103.5); return clock;
    }
    private static void CheckTimeline()
    {
        RecordingClock clock = Clock();
        Check(Math.Abs(clock.Elapsed.TotalSeconds - 3) < .00001, "Pause removes exactly 500ms from the shared timeline");
        var spans = clock.Map(100.9, .7);
        Check(spans.Count == 2 && Math.Abs(spans[0].Duration - .1) < .00001 && Math.Abs(spans[1].ActiveStart - 1) < .00001,
            "A packet crossing pause and resume is sliced into active segments");
        Check(clock.Map(101.1, .3).Count == 0 && clock.Map(99, .5).Count == 0 && clock.Map(103.6, .5).Count == 0,
            "Audio before start, during pause, and after stop is discarded");
    }
    private static void CheckFormats()
    {
        foreach (int bits in new int[] { 16, 24, 32 })
        {
            WaveFormat format = new WaveFormat(44100, bits, 1);
            int frames = 4410; byte[] data = new byte[frames * format.BlockAlign];
            for (int i = 0; i < frames; i++) data[(i + 1) * format.BlockAlign - 1] = 0x40;
            RecordingClock clock = new RecordingClock(); clock.ResumeAt(10); clock.PauseAt(10.1);
            AudioMixer mixer = new AudioMixer(); new AudioPacketConverter(format).Add(data, frames, 10, false, clock, mixer, 1f);
            byte[] pcm = mixer.Read(4800);
            Check(Math.Abs(BitConverter.ToInt16(pcm, 2000) - 16384) <= 1 && BitConverter.ToInt16(pcm, 2000) == BitConverter.ToInt16(pcm, 2002),
                bits + "-bit mono PCM at 44.1kHz converts to 48kHz stereo");
        }
        AudioMixer clipped = new AudioMixer(); clipped.Add(0, 8, -8); byte[] result = clipped.Read(1);
        Check(BitConverter.ToInt16(result, 0) == 32767 && BitConverter.ToInt16(result, 2) == -32767, "Mix saturates without integer wraparound");
        AudioMixer silence = new AudioMixer(); byte[] quiet = silence.Read(48000);
        Check(Array.TrueForAll(quiet, delegate(byte b) { return b == 0; }), "Absent loopback packets still produce timed silence");
    }
    private static void AddTone(AudioMixer mixer, int rate, bool floating, int frequency, float gain)
    {
        WaveFormat format = floating ? WaveFormat.CreateIeeeFloatWaveFormat(rate, 2) : new WaveFormat(rate, 16, 1);
        AudioPacketConverter converter = new AudioPacketConverter(format); RecordingClock clock = Clock();
        int packetFrames = rate / 100;
        // One 10ms packet at a time, including packets captured while paused.
        for (int packet = 25; packet < 350; packet++)
        {
            byte[] bytes = new byte[packetFrames * format.BlockAlign];
            for (int i = 0; i < packetFrames; i++)
            {
                double wall = packet / 100.0 + i / (double)rate;
                int hz = wall >= 1 && wall < 1.5 ? 1760 : frequency;
                double value = .4 * Math.Sin(2 * Math.PI * hz * wall);
                // Quiet interval after resume, from active time 2.0 to 2.25.
                if (wall >= 2.5 && wall < 2.75) value = 0;
                if (floating)
                {
                    byte[] sample = BitConverter.GetBytes((float)value);
                    Buffer.BlockCopy(sample, 0, bytes, i * 8, 4); Buffer.BlockCopy(sample, 0, bytes, i * 8 + 4, 4);
                }
                else { short sample = (short)(value * 32767); bytes[i * 2] = (byte)sample; bytes[i * 2 + 1] = (byte)(sample >> 8); }
            }
            converter.Add(bytes, packetFrames, 100 + packet / 100.0, false, clock, mixer, gain);
        }
    }
    private static void CheckMix(string output)
    {
        AudioMixer mixer = new AudioMixer(); AddTone(mixer, 48000, true, 440, .5f); AddTone(mixer, 44100, false, 660, .5f);
        string audio = Path.Combine(output, "mixed.flac");
        using (AudioEncoder encoder = new AudioEncoder(audio))
        {
            for (int i = 0; i < 300; i++) encoder.Write(mixer.Read(480));
            encoder.Finish();
        }
        foreach (VideoQuality quality in new VideoQuality[] { VideoQuality.HighQuality, VideoQuality.Lossless })
        {
            string ext = quality == VideoQuality.HighQuality ? ".mp4" : ".mkv";
            string video = Path.Combine(output, "video" + ext);
            using (Bitmap bitmap = new Bitmap(320, 180))
            using (Graphics g = Graphics.FromImage(bitmap))
            using (FfmpegWriter writer = new FfmpegWriter(video, 320, 180, 30, quality))
            {
                g.Clear(Color.FromArgb(23, 140, 86)); g.FillRectangle(Brushes.White, 30, 30, 40, 30);
                for (int i = 0; i < 90; i++) writer.AddFrame(bitmap); writer.Finish();
            }
            AudioEncoder.Mux(video, audio, Path.Combine(output, "mixed" + ext), quality, 3);
            Check(File.Exists(Path.Combine(output, "mixed" + ext)), "Muxed video with mixed audio: " + ext);
        }
    }
    private static void CheckNoAudioAndDevices(string output)
    {
        using (ManualResetEvent done = new ManualResetEvent(false))
        {
            Exception failure = null;
            RecordingSession session = new RecordingSession(new Rectangle(0, 0, 320, 180), 15, Path.Combine(output, "silent.mp4"), VideoQuality.HighQuality, AudioMode.None);
            session.CaptureFrame = delegate(Graphics g, Rectangle area) { g.Clear(Color.CadetBlue); };
            session.Completed += delegate(string path, Exception ex) { failure = ex; done.Set(); };
            session.Start(); Thread.Sleep(650); session.Stop();
            Check(done.WaitOne(20000) && failure == null, "Video-only mode records without audio hardware");
        }
        foreach (AudioMode mode in new AudioMode[] { AudioMode.System, AudioMode.Microphone, AudioMode.SystemAndMicrophone })
        {
            bool available = true;
            using (MMDeviceEnumerator devices = new MMDeviceEnumerator())
            {
                try
                {
                    if (mode != AudioMode.Microphone) using (MMDevice d = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console)) { }
                    if (mode != AudioMode.System) using (MMDevice d = devices.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console)) { }
                }
                catch { available = false; }
            }
            // Never capture the operator's microphone automatically in a test run.
            if (available) { Console.WriteLine("MANUAL: endpoint available for " + mode + "; use a consented test tone/voice for hardware acceptance."); continue; }
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                Exception failure = null;
                RecordingSession session = new RecordingSession(new Rectangle(0, 0, 320, 180), 15, Path.Combine(output, "unavailable-" + mode + ".mp4"), VideoQuality.HighQuality, mode);
                session.Completed += delegate(string path, Exception ex) { failure = ex; done.Set(); };
                session.Start();
                Check(done.WaitOne(20000) && failure != null && failure.InnerException != null,
                    mode + " reports unavailable devices, cleans up, and completes without hanging");
            }
        }
    }
}
