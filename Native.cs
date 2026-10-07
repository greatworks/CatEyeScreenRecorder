using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FreeWindowsScreenRecorder
{
    internal static class Native
    {
        public static void Capture(Graphics graphics, Rectangle area)
        {
            IntPtr screen = GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr target = IntPtr.Zero;
            try
            {
                target = graphics.GetHdc();
                // GDI+ rejects combined CopyPixelOperation flags; native BitBlt supports layered windows.
                if (!BitBlt(target, 0, 0, area.Width, area.Height, screen, area.X, area.Y, 0x40CC0020))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { if (target != IntPtr.Zero) graphics.ReleaseHdc(target); ReleaseDC(IntPtr.Zero, screen); }
        }

        public static void FlushDesktop()
        {
            try { DwmFlush(); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            catch (SEHException) { }
        }

        public static bool ExcludeFromCapture(IntPtr handle)
        {
            try
            {
                VersionInfo version = new VersionInfo(); version.Size = Marshal.SizeOf(typeof(VersionInfo));
                bool composition;
                if (RtlGetVersion(ref version) != 0 || DwmIsCompositionEnabled(out composition) != 0 || !composition) return false;
                // WDA_EXCLUDEFROMCAPTURE was added in Windows 10 2004. Older
                // systems can still protect a toolbar with WDA_MONITOR, which
                // keeps it visible while preventing its contents from entering a capture.
                uint affinity = version.Build >= 19041 ? 0x11u : 0x1u;
                if (SetWindowDisplayAffinity(handle, affinity)) return true;
                return affinity != 0x1u && SetWindowDisplayAffinity(handle, 0x1u);
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
            catch (SEHException) { return false; }
        }

        // Clear WDA_EXCLUDEFROMCAPTURE while the recorder is idle so ordinary screenshot
        // tools can capture the interface. Recording and selection reapply exclusion first.
        public static bool AllowCapture(IntPtr handle)
        {
            try { return SetWindowDisplayAffinity(handle, 0); }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
            catch (SEHException) { return false; }
        }

        public static void DragWindow(Form form)
        {
            ReleaseCapture(); SendMessage(form.Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
        }

        public static bool SetHotkey(IntPtr handle, int id, Keys key) { return RegisterHotKey(handle, id, 0x4000 | 0x0002 | 0x0004, (uint)key); }
        public static void ClearHotkey(IntPtr handle, int id) { UnregisterHotKey(handle, id); }
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowDisplayAffinity(IntPtr handle, uint affinity);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
        [DllImport("user32.dll")] internal static extern bool GetWindowDisplayAffinity(IntPtr handle, out uint affinity);
        [DllImport("dwmapi.dll")] private static extern int DwmFlush();
        [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled(out bool enabled);
        [DllImport("ntdll.dll", CharSet = CharSet.Unicode)] private static extern int RtlGetVersion(ref VersionInfo version);
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr handle, int id);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct VersionInfo
        {
            public int Size, Major, Minor, Build, Platform;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string ServicePack;
        }
    }
}
