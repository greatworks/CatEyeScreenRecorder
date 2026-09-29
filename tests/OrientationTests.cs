using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

// Exercises the shipped EXE's writer, then reads frames through Windows AVI/GDI.
internal static class OrientationTests
{
    private static Type writerType;

    private static int Main(string[] args)
    {
        try
        {
            writerType = Assembly.LoadFrom(Path.GetFullPath(args[0]))
                .GetType("FreeWindowsScreenRecorder.AviWriter", true);
            Directory.CreateDirectory(args[1]);
            for (int width = 4; width <= 7; width++)
            {
                CheckVideo(args[1], width, 5, false);
                CheckVideo(args[1], width, 6, true);
            }
            Console.WriteLine("PASS: 8 AVI files, 24 frames; upright pixels, left/right order, row padding, signed strides.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static Color ExpectedPixel(int x, int y, int frame)
    {
        return Color.FromArgb(20 + y * 30, 10 + x * 25, 30 + frame * 70);
    }

    private static void CheckVideo(string directory, int width, int height, bool negativeStride)
    {
        string path = Path.Combine(directory, width + "x" + height + (negativeStride ? "-negative" : "-positive") + ".avi");
        int stride = ((width * 3 + 3) / 4) * 4;
        // Extra zeroed space makes the old, incorrect negative-stride read fail deterministically.
        IntPtr memory = Marshal.AllocHGlobal(stride * height * 2);
        try
        {
            Marshal.Copy(new byte[stride * height * 2], 0, memory, stride * height * 2);
            using (Bitmap bitmap = negativeStride
                ? new Bitmap(width, height, -stride, PixelFormat.Format24bppRgb,
                    IntPtr.Add(memory, (height - 1) * stride))
                : new Bitmap(width, height, PixelFormat.Format24bppRgb))
            using (IDisposable writer = (IDisposable)Activator.CreateInstance(writerType,
                new object[] { path, width, height, 10 }))
            {
                for (int frame = 0; frame < 3; frame++)
                {
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            bitmap.SetPixel(x, y, ExpectedPixel(x, y, frame));

                    BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                        ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    int actualStride = data.Stride;
                    bitmap.UnlockBits(data);
                    if ((actualStride < 0) != negativeStride)
                        throw new Exception("Fixture did not exercise the requested stride sign.");
                    writerType.GetMethod("AddFrame").Invoke(writer, new object[] { bitmap });
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }

        AVIFileInit();
        IntPtr file = IntPtr.Zero;
        IntPtr stream = IntPtr.Zero;
        IntPtr decoder = IntPtr.Zero;
        try
        {
            Check(AVIFileOpenW(out file, path, 0, IntPtr.Zero), "Open AVI");
            Check(AVIFileGetStream(file, out stream, 0x73646976, 0), "Get video stream");
            if (AVIStreamLength(stream) != 3)
                throw new Exception("Wrong frame count in " + path);
            decoder = AVIStreamGetFrameOpen(stream, IntPtr.Zero);
            if (decoder == IntPtr.Zero)
                throw new Exception("Windows could not decode " + path);

            for (int frame = 0; frame < 3; frame++)
            {
                IntPtr dib = AVIStreamGetFrame(decoder, frame);
                if (dib == IntPtr.Zero)
                    throw new Exception("Could not decode frame " + frame);
                int headerSize = Marshal.ReadInt32(dib, 0);
                int decodedWidth = Marshal.ReadInt32(dib, 4);
                int decodedHeight = Marshal.ReadInt32(dib, 8);
                if (decodedWidth != width || Math.Abs(decodedHeight) != height ||
                    Marshal.ReadInt16(dib, 14) != 24 || Marshal.ReadInt32(dib, 16) != 0)
                    throw new Exception("Unexpected decoded format in " + path);

                // Add a BMP file header so GDI+ independently interprets the DIB orientation.
                byte[] bytes = new byte[headerSize + stride * height];
                Marshal.Copy(dib, bytes, 0, bytes.Length);
                using (MemoryStream bmp = new MemoryStream())
                {
                    BinaryWriter output = new BinaryWriter(bmp);
                    output.Write((ushort)0x4d42);
                    output.Write(14 + bytes.Length);
                    output.Write(0);
                    output.Write(14 + headerSize);
                    output.Write(bytes);
                    bmp.Position = 0;
                    using (Bitmap decoded = new Bitmap(bmp))
                        for (int y = 0; y < height; y++)
                            for (int x = 0; x < width; x++)
                                if (decoded.GetPixel(x, y).ToArgb() != ExpectedPixel(x, y, frame).ToArgb())
                                    throw new Exception("Pixel mismatch: " + path + ", frame " + frame + ", (" + x + ", " + y + ")");
                }
            }
            Console.WriteLine("PASS: " + Path.GetFileName(path));
        }
        finally
        {
            if (decoder != IntPtr.Zero) AVIStreamGetFrameClose(decoder);
            if (stream != IntPtr.Zero) AVIStreamRelease(stream);
            if (file != IntPtr.Zero) AVIFileRelease(file);
            AVIFileExit();
        }
    }

    private static void Check(int result, string action)
    {
        if (result != 0) throw new Exception(action + " failed: " + result);
    }

    [DllImport("avifil32.dll")] private static extern void AVIFileInit();
    [DllImport("avifil32.dll")] private static extern void AVIFileExit();
    [DllImport("avifil32.dll", CharSet = CharSet.Unicode)]
    private static extern int AVIFileOpenW(out IntPtr file, string path, int mode, IntPtr handler);
    [DllImport("avifil32.dll")] private static extern int AVIFileGetStream(IntPtr file, out IntPtr stream, int type, int index);
    [DllImport("avifil32.dll")] private static extern int AVIStreamLength(IntPtr stream);
    [DllImport("avifil32.dll")] private static extern IntPtr AVIStreamGetFrameOpen(IntPtr stream, IntPtr format);
    [DllImport("avifil32.dll")] private static extern IntPtr AVIStreamGetFrame(IntPtr decoder, int position);
    [DllImport("avifil32.dll")] private static extern int AVIStreamGetFrameClose(IntPtr decoder);
    [DllImport("avifil32.dll")] private static extern int AVIStreamRelease(IntPtr stream);
    [DllImport("avifil32.dll")] private static extern int AVIFileRelease(IntPtr file);
}
