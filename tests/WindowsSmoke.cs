using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal class WindowsSmoke : Form
{
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int x, int y, uint buttons, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    static readonly Color Upper = Color.FromArgb(25, 104, 171), Lower = Color.FromArgb(230, 145, 54);
    readonly string executable, folder;
    volatile string dropped;
    Process grab;
    int result = 1;

    WindowsSmoke(string app, string output)
    {
        executable = app; folder = output;
        StartPosition = FormStartPosition.Manual;
        var area = Screen.PrimaryScreen.WorkingArea;
        Bounds = new Rectangle(area.Left + 60, area.Top + 55, 480, 360);
        Text = "CornerGrab local drag receiver";
        AllowDrop = true;
        DragEnter += delegate(object s, DragEventArgs e) { if(e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        DragDrop += delegate(object s, DragEventArgs e) { dropped = ((string[])e.Data.GetData(DataFormats.FileDrop))[0]; };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using(var top = new SolidBrush(Upper)) e.Graphics.FillRectangle(top, ClientRectangle);
        using(var bottom = new SolidBrush(Lower)) e.Graphics.FillRectangle(bottom, 0, ClientSize.Height / 2, Width, Height);
    }

    static IntPtr VisibleWindow(string title)
    {
        var w = FindWindow(null, title);
        return IsWindowVisible(w) ? w : IntPtr.Zero;
    }

    static void Wait(Func<bool> condition, string label, int limit = 8000)
    {
        var time = Stopwatch.StartNew();
        while (!condition() && time.ElapsedMilliseconds < limit) Thread.Sleep(50);
        if (!condition()) throw new Exception(label);
    }

    static void Key(byte value, bool up) { keybd_event(value, 0, up ? 2u : 0u, UIntPtr.Zero); }
    static void Shortcut() { Key(17,false); Key(16,false); Key(0x34,false); Key(0x34,true); Key(16,true); Key(17,true); }
    static void Escape() { Key(27,false); Key(27,true); }
    static void Drag(Point from, Point to)
    {
        Cursor.Position = from; Thread.Sleep(120);
        mouse_event(2,0,0,0,UIntPtr.Zero); Thread.Sleep(100);
        for (int i = 1; i <= 22; i++)
        {
            Cursor.Position = new Point(from.X + (to.X-from.X)*i/22, from.Y + (to.Y-from.Y)*i/22);
            Thread.Sleep(30);
        }
        Thread.Sleep(180); mouse_event(4,0,0,0,UIntPtr.Zero);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Point center = PointToScreen(new Point(180, ClientSize.Height / 2));
        var worker = new Thread(delegate() { Run(center); });
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    void Run(Point center)
    {
        try
        {
            grab = Process.Start(executable, "--tray");
            Wait(delegate { return FindWindow(null, "CornerGrab shortcut receiver") != IntPtr.Zero; }, "Tray startup");
            Shortcut();
            Wait(delegate { return VisibleWindow("CornerGrab selection") != IntPtr.Zero; }, "Ctrl+Shift+4 did not open selection");
            Thread.Sleep(260);
            Drag(new Point(center.X + 80, center.Y + 40), new Point(center.X - 80, center.Y - 40));
            Wait(delegate { return Directory.GetFiles(folder,"*.png").Length == 1; }, "No saved PNG");
            string file = Directory.GetFiles(folder,"*.png")[0];
            using(var image = new Bitmap(file))
            {
                if (image.Width != 160 || image.Height != 80 || image.GetPixel(35,15).ToArgb()!=Upper.ToArgb() || image.GetPixel(35,65).ToArgb()!=Lower.ToArgb())
                    throw new Exception("Crop size or pixel colors incorrect: " + image.Size + " " + image.GetPixel(35,15) + " " + image.GetPixel(35,65));
            }
            Console.WriteLine("PASS Ctrl+Shift+4, reverse region selection, exact PNG dimensions and pixels, chosen folder");
            Wait(delegate { return VisibleWindow("CornerGrab thumbnail") != IntPtr.Zero; }, "Thumbnail did not appear");
            var clipboard = Clipboard.GetDataObject();
            using(var png = (Stream)clipboard.GetData("PNG", false))
            using(var image = new Bitmap(png))
                if(image.Width != 160 || image.GetPixel(35,65).ToArgb()!=Lower.ToArgb()) throw new Exception("Clipboard PNG incorrect");
            using(var dib = (Stream)clipboard.GetData(DataFormats.Dib, false))
            using(var read = new BinaryReader(dib))
                if(read.ReadInt32() < 40 || read.ReadInt32() != 160 || read.ReadInt32() != 80) throw new Exception("Clipboard DIB incorrect");
            Console.WriteLine("PASS clipboard publishes a decodable PNG and a standard bitmap");
            Rect thumb;
            GetWindowRect(VisibleWindow("CornerGrab thumbnail"),out thumb);
            var work = Screen.FromPoint(center).WorkingArea;
            if(Math.Abs(work.Right - thumb.Right) > 60 || Math.Abs(work.Bottom - thumb.Bottom)>60) throw new Exception("Thumbnail not in lower right");
            Drag(new Point((thumb.Left+thumb.Right)/2,(thumb.Top+thumb.Bottom)/2),center);
            Wait(delegate { return dropped != null; }, "Other application did not receive a native file drop");
            if (!String.Equals(Path.GetFullPath(dropped),Path.GetFullPath(file),StringComparison.OrdinalIgnoreCase)) throw new Exception("Wrong dropped file");
            Console.WriteLine("PASS lower-right thumbnail dragged into an independent Windows application as the saved file");
            Shortcut();
            Wait(delegate { return VisibleWindow("CornerGrab selection") != IntPtr.Zero; }, "Cannot capture twice");
            Escape();
            Wait(delegate { return VisibleWindow("CornerGrab selection") == IntPtr.Zero; }, "Escape did not cancel");
            if(Directory.GetFiles(folder,"*.png").Length != 1) throw new Exception("Unexpected second capture");
            Key(0x2c,false); Key(0x2c,true);
            Thread.Sleep(550);
            bool print = VisibleWindow("CornerGrab selection") != IntPtr.Zero;
            Console.WriteLine(print ? "PASS Print Screen opens CornerGrab" : "INFO Windows or another app owns Print Screen on this desktop");
            Escape();
            result = 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e.ToString()); }
        finally
        {
            if(grab != null && !grab.HasExited) { grab.Kill(); grab.WaitForExit(2500); }
            BeginInvoke(new Action(Close));
        }
    }

    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        using(var test = new WindowsSmoke(args[0], args[1])) { Application.Run(test); return test.result; }
    }
}
