using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CornerGrab
{
    internal static class CaptureFiles
    {
        private const string Settings = @"Software\CornerGrab";

        internal static string Read(string key, string fallback)
        {
            using (var settings = Registry.CurrentUser.OpenSubKey(Settings))
                return settings == null ? fallback : settings.GetValue(key, fallback) as string ?? fallback;
        }

        internal static void Write(string key, string value)
        {
            using (var settings = Registry.CurrentUser.CreateSubKey(Settings)) settings.SetValue(key, value);
        }

        internal static string Folder
        {
            get { return Read("Folder", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CornerGrab")); }
        }

        internal static string Save(Image image, string folder)
        {
            Directory.CreateDirectory(folder);
            string stem = "Screenshot " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss.fff", CultureInfo.InvariantCulture);
            string pending = Path.Combine(folder, ".cornergrab-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    image.Save(stream, ImageFormat.Png);
                for (int index = 0; ; index++)
                {
                    string suffix = index == 0 ? "" : " (" + index + ")";
                    string path = Path.Combine(folder, stem + suffix + ".png");
                    try { File.Move(pending, path); return path; }
                    catch (IOException) { if (!File.Exists(path)) throw; }
                }
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }

        internal static DataObject AsFile(string path)
        {
            var data = new DataObject();
            data.SetFileDropList(new StringCollection { path });
            return data;
        }

        internal static void CopyImage(string path)
        {
            var data = AsFile(path);
            using (var image = new Bitmap(path))
            using (var bmpStream = new MemoryStream())
            using (var png = new MemoryStream())
            {
                image.Save(png, ImageFormat.Png);
                png.Position = 0;
                data.SetData("PNG", false, png);
                using (var opaque = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb))
                {
                    using (Graphics graphics = Graphics.FromImage(opaque)) graphics.DrawImageUnscaled(image, 0, 0);
                    opaque.Save(bmpStream, ImageFormat.Bmp);
                }
                // CF_DIB starts with BITMAPINFOHEADER. A BMP file has a 14-byte file header before it.
                byte[] bmp = bmpStream.ToArray();
                using (var dib = new MemoryStream(bmp, 14, bmp.Length - 14))
                {
                    data.SetData(DataFormats.Dib, false, dib);
                    Clipboard.SetDataObject(data, true, 10, 35);
                }
            }
        }

        internal static void CopyPath(string path)
        {
            Clipboard.SetDataObject(path, true, 10, 35);
        }
    }

    internal static class Native
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr hwnd, int message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
        [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

        internal static float ScreenScale(Point point)
        {
            uint x, y;
            return GetDpiForMonitor(MonitorFromPoint(point, 2), 0, out x, out y) == 0 ? x / 96f : 1f;
        }

        internal static bool OverDiscord()
        {
            int pid;
            GetWindowThreadProcessId(WindowFromPoint(Cursor.Position), out pid);
            try
            {
                using (var process = System.Diagnostics.Process.GetProcessById(pid))
                    return process.ProcessName.StartsWith("Discord", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
            catch (InvalidOperationException) { return false; }
        }
    }
}
