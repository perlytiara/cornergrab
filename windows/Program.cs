using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("CornerGrab")]
[assembly: AssemblyProduct("CornerGrab")]
[assembly: AssemblyDescription("Capture a region and drag the PNG into another app")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace CornerGrab
{
    internal sealed class ShortcutWindow : NativeWindow, IDisposable
    {
        internal const string Title = "CornerGrab shortcut receiver";
        internal const int CaptureCommand = 0x8000 + 41;
        internal bool RegionKey, PrintKey;
        internal event Action Requested;

        internal ShortcutWindow()
        {
            CreateHandle(new CreateParams { Caption = Title });
            Register();
        }

        internal void Register()
        {
            if (!RegionKey) RegionKey = Native.RegisterHotKey(Handle, 1, 0x4000 | 0x0002 | 0x0004, 0x34);
            if (!PrintKey) PrintKey = Native.RegisterHotKey(Handle, 2, 0x4000, 0x2c);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 || m.Msg == CaptureCommand)
            {
                var action = Requested;
                if (action != null) action();
            }
            else base.WndProc(ref m);
        }

        public void Dispose()
        {
            Native.UnregisterHotKey(Handle, 1);
            Native.UnregisterHotKey(Handle, 2);
            DestroyHandle();
        }
    }

    internal sealed class GrabApp : ApplicationContext
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private readonly ShortcutWindow shortcuts = new ShortcutWindow();
        private readonly NotifyIcon tray;
        private readonly Icon icon;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private Thumbnail preview;
        private string latest = CaptureFiles.Read("Latest", "");
        private bool busy;

        internal GrabApp(bool quiet)
        {
            icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            tray = new NotifyIcon { Text = "CornerGrab · Ctrl+Shift+4", Icon = icon, Visible = true, ContextMenuStrip = menu };
            shortcuts.Requested += Capture;
            tray.MouseClick += delegate(object o, MouseEventArgs e) { if (e.Button == MouseButtons.Left) RequestCapture(); };
            menu.Opening += delegate
            {
                shortcuts.Register();
                while (menu.Items.Count > 0) menu.Items[0].Dispose();
                Add(menu, "Capture region     Ctrl+Shift+4", RequestCapture);
                Add(menu, "Show latest thumbnail", delegate { Guard(delegate { ShowPreview(Cursor.Position); }); }).Enabled = File.Exists(latest);
                Add(menu, "Copy latest file path for Whiteboard", delegate { CopyPath(latest); }).Enabled = File.Exists(latest);
                Add(menu, "Use an existing image…", OpenImage);
                menu.Items.Add(new ToolStripSeparator());
                Add(menu, "Open screenshot folder", delegate { OpenFolder(CaptureFiles.Folder); });
                Add(menu, "Choose default folder…", ChooseFolder);
                Add(menu, "Start at sign-in", ToggleStartup).Checked = StartsAtLogin();
                if (!shortcuts.RegionKey || !shortcuts.PrintKey)
                    Add(menu, "Shortcut availability…", ShowKeys);
                menu.Items.Add(new ToolStripSeparator());
                Add(menu, "Quit CornerGrab", ExitThread);
            };
            if (!quiet) RequestCapture();
        }

        private static ToolStripMenuItem Add(ContextMenuStrip target, string title, Action action)
        {
            var item = new ToolStripMenuItem(title);
            item.Click += delegate { action(); };
            target.Items.Add(item);
            return item;
        }

        private void RequestCapture()
        {
            Native.PostMessage(shortcuts.Handle, ShortcutWindow.CaptureCommand, IntPtr.Zero, IntPtr.Zero);
        }

        private void Note(string message)
        {
            tray.ShowBalloonTip(4500, "CornerGrab", message, ToolTipIcon.Info);
        }

        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception e) { MessageBox.Show(e.Message, "CornerGrab", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void Capture()
        {
            if (busy || (preview != null && preview.Busy)) return;
            busy = true;
            try
            {
                Guard(delegate
                {
                    if (preview != null) preview.Hide();
                    Native.DwmFlush();
                    Point at;
                    Bitmap image;
                    using (var region = new Selection())
                    {
                        region.ShowDialog();
                        image = region.Result;
                        at = region.ScreenTip;
                    }
                    if (image == null) return;
                    using (image)
                    {
                        try { latest = CaptureFiles.Save(image, CaptureFiles.Folder); }
                        catch (Exception e)
                        {
                            if (!(e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)) throw;
                            string recovery = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CornerGrab", "Recovered");
                            latest = CaptureFiles.Save(image, recovery);
                            Note("Your save folder is unavailable. The PNG was saved in " + recovery);
                        }
                    }
                    Present(at);
                });
            }
            finally { busy = false; }
        }

        private void Present(Point at)
        {
            CaptureFiles.Write("Latest", latest);
            try { CaptureFiles.CopyImage(latest); }
            catch (System.Runtime.InteropServices.ExternalException) { Note("PNG saved. The clipboard is busy. Click the thumbnail and choose Copy image to retry."); }
            ShowPreview(at);
        }

        private void OpenImage()
        {
            if (busy) return;
            busy = true;
            try
            {
                using (var picker = new OpenFileDialog { Title = "Use an image in CornerGrab", Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp" })
                {
                    if (picker.ShowDialog() != DialogResult.OK) return;
                    Guard(delegate
                    {
                        using (var image = Image.FromFile(picker.FileName)) latest = CaptureFiles.Save(image, CaptureFiles.Folder);
                        Present(Cursor.Position);
                    });
                }
            }
            finally { busy = false; }
        }

        private void ShowPreview(Point at)
        {
            if (preview != null) { preview.Dispose(); preview = null; }
            if (!File.Exists(latest)) return;
            string file = latest;
            var actions = new ContextMenuStrip();
            Add(actions, "Copy image", delegate { Guard(delegate { CaptureFiles.CopyImage(file); }); });
            Add(actions, "Copy file path for Whiteboard", delegate { CopyPath(file); });
            Add(actions, "Save as…", delegate { SaveAs(file); });
            Add(actions, "Show in folder", delegate { Guard(delegate { Process.Start("explorer.exe", "/select,\"" + file + "\""); }); });
            actions.Items.Add(new ToolStripSeparator());
            Add(actions, "Dismiss", delegate { preview.Hide(); });
            preview = new Thumbnail(file, at, actions, CopyPath);
            preview.Show();
        }

        private void CopyPath(string file)
        {
            Guard(delegate { CaptureFiles.CopyPath(file); Note("Path copied. In Whiteboard choose Image +, paste in File name, then choose Open."); });
        }

        private void SaveAs(string file)
        {
            if (busy) return;
            busy = true;
            try
            {
                using (var dialog = new SaveFileDialog { Filter = "PNG image|*.png", FileName = Path.GetFileName(file), DefaultExt = "png",
                    InitialDirectory = Path.GetDirectoryName(file), AddExtension = true, OverwritePrompt = true })
                    if (dialog.ShowDialog() == DialogResult.OK && !String.Equals(file, dialog.FileName, StringComparison.OrdinalIgnoreCase))
                        Guard(delegate { File.Copy(file, dialog.FileName, true); });
            }
            finally { busy = false; }
        }

        private static void OpenFolder(string path)
        {
            try { Directory.CreateDirectory(path); Process.Start("explorer.exe", "\"" + path + "\""); }
            catch (Exception e) { MessageBox.Show(e.Message, "CornerGrab"); }
        }

        private void ChooseFolder()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Save future screenshots in this folder", SelectedPath = CaptureFiles.Folder })
                if (dialog.ShowDialog() == DialogResult.OK) Guard(delegate { CaptureFiles.Write("Folder", dialog.SelectedPath); });
        }

        private static bool StartsAtLogin()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) return key != null && key.GetValue("CornerGrab") != null;
        }

        private void ToggleStartup()
        {
            Guard(delegate
            {
                bool enabled = StartsAtLogin();
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                    if (enabled) key.DeleteValue("CornerGrab", false);
                    else key.SetValue("CornerGrab", "\"" + Application.ExecutablePath + "\" --tray");
            });
        }

        private void ShowKeys()
        {
            string region = shortcuts.RegionKey ? "Ctrl+Shift+4 is ready." : "Another app has Ctrl+Shift+4. Click the CornerGrab tray icon to capture.";
            string print = shortcuts.PrintKey ? "Print Screen is ready." : "For Print Screen, turn off Windows’ built-in screen capture shortcut in Accessibility → Keyboard, then reopen the CornerGrab tray menu to retry.";
            if (MessageBox.Show(region + "\n\n" + print + "\n\nOpen Windows keyboard settings?", "CornerGrab shortcuts", MessageBoxButtons.YesNo) == DialogResult.Yes)
                Guard(delegate { Process.Start("ms-settings:easeofaccess-keyboard"); });
        }

        protected override void ExitThreadCore()
        {
            if (preview != null) preview.Dispose();
            tray.Visible = false;
            tray.Dispose();
            icon.Dispose();
            menu.Dispose();
            shortcuts.Dispose();
            base.ExitThreadCore();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool first;
            using (var mutex = new Mutex(true, @"Local\CornerGrab.126852ed", out first))
            {
                if (!first)
                {
                    var receiver = Native.FindWindow(null, ShortcutWindow.Title);
                    if (receiver != IntPtr.Zero && Array.IndexOf(args, "--tray") < 0)
                        Native.PostMessage(receiver, ShortcutWindow.CaptureCommand, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new GrabApp(Array.IndexOf(args, "--tray") >= 0));
            }
        }
    }
}
