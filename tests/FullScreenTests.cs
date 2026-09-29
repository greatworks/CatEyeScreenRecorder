using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class FullScreenTests
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args[0] == "--manifest")
            {
                ExtractManifest(args[1], args[2]);
                return 0;
            }

            Rectangle physical = Rectangle.Empty;
            foreach (Screen screen in Screen.AllScreens)
            {
                // EnumDisplaySettings returns physical pixels independently of DPI virtualization.
                byte[] mode = new byte[220]; // DEVMODEW
                BitConverter.GetBytes((ushort)mode.Length).CopyTo(mode, 68); // dmSize
                if (!EnumDisplaySettingsW(screen.DeviceName, -1, mode))
                    throw new Exception("Could not read the physical display mode.");
                Rectangle bounds = new Rectangle(BitConverter.ToInt32(mode, 76), BitConverter.ToInt32(mode, 80),
                    BitConverter.ToInt32(mode, 172), BitConverter.ToInt32(mode, 176));
                physical = physical.IsEmpty ? bounds : Rectangle.Union(physical, bounds);
            }

            Type formType = Assembly.LoadFrom(Path.GetFullPath(args[0]))
                .GetType("FreeWindowsScreenRecorder.RecorderForm", true);
            using (Form form = (Form)Activator.CreateInstance(formType))
            {
                Rectangle area = (Rectangle)formType.GetMethod("GetCaptureArea", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, null);
                Console.WriteLine("Physical desktop: " + physical);
                Console.WriteLine("Full-screen capture: " + area);
                if (area != physical)
                    throw new Exception("Full-screen capture does not cover all physical display pixels.");
                if (GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) != 2)
                    throw new Exception("The embedded EXE manifest did not enable per-monitor DPI awareness.");

                // Create a hidden handle so the actual capture loop can post completion to the UI.
                IntPtr handle = form.Handle;
                FieldInfo recording = formType.GetField("recording", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo loop = formType.GetMethod("CaptureLoop", BindingFlags.Instance | BindingFlags.NonPublic);
                string video = Path.Combine(args[1], "full-screen.avi");
                Exception workerError = null;
                recording.SetValue(form, true);
                Thread worker = new Thread(delegate()
                {
                    try
                    {
                        if (GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) != 2)
                            throw new Exception("Capture worker did not inherit per-monitor DPI awareness.");
                        loop.Invoke(form, new object[] { area, 1, video });
                    }
                    catch (Exception ex) { workerError = ex; }
                });
                worker.IsBackground = true;
                worker.Start();
                Thread.Sleep(500);
                recording.SetValue(form, false);
                if (!worker.Join(10000)) throw new Exception("Capture worker did not stop.");
                Application.DoEvents();
                if (workerError != null) throw workerError;
                string log = ((TextBox)formType.GetField("logBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form)).Text;
                if (log.Contains("错误")) throw new Exception(log);

                byte[] avi = File.ReadAllBytes(video);
                int format = FindChunk(avi, "strf") + 8;
                int recordedWidth = BitConverter.ToInt32(avi, format + 4);
                int recordedHeight = BitConverter.ToInt32(avi, format + 8);
                int header = FindChunk(avi, "avih") + 8;
                int frames = BitConverter.ToInt32(avi, header + 16);
                if (recordedWidth != physical.Width || recordedHeight != physical.Height || frames < 1)
                    throw new Exception("Recorded AVI dimensions/frame count are incorrect.");
                Console.WriteLine("PASS: actual capture loop wrote " + frames + " frame(s) at " + recordedWidth + " x " + recordedHeight + ".");
                Console.WriteLine("PASS: physical desktop bounds and capture worker DPI awareness.");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int FindChunk(byte[] data, string name)
    {
        for (int i = 12; i < Math.Min(data.Length - 4, 4096); i++)
            if (data[i] == name[0] && data[i + 1] == name[1] && data[i + 2] == name[2] && data[i + 3] == name[3])
                return i;
        throw new Exception("Missing AVI chunk: " + name);
    }

    private static void ExtractManifest(string exe, string output)
    {
        IntPtr module = LoadLibraryExW(Path.GetFullPath(exe), IntPtr.Zero, 2);
        if (module == IntPtr.Zero) throw new Exception("Could not load the EXE resources.");
        try
        {
            IntPtr resource = FindResourceW(module, new IntPtr(1), new IntPtr(24));
            if (resource == IntPtr.Zero) throw new Exception("The EXE has no manifest.");
            byte[] bytes = new byte[SizeofResource(module, resource)];
            Marshal.Copy(LockResource(LoadResource(module, resource)), bytes, 0, bytes.Length);
            File.WriteAllBytes(output, bytes);
        }
        finally { FreeLibrary(module); }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettingsW(string device, int mode, [In, Out] byte[] settings);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryExW(string name, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindResourceW(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll")] private static extern uint SizeofResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")] private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")] private static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
}
