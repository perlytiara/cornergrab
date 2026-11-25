using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace CornerGrab
{
    internal sealed class Thumbnail : Form
    {
        private readonly Bitmap thumbnail;
        private readonly string file;
        private readonly Timer expiry = new Timer { Interval = 12000 };
        private readonly ContextMenuStrip actions;
        private readonly Action<string> copyPath;
        private readonly float scale;
        private readonly int padding, footer;
        private Point? pressed;
        private bool dragging, menuOpen, boardHint;
        internal bool Busy { get { return dragging || menuOpen || pressed.HasValue; } }

        internal Thumbnail(string path, Point at, ContextMenuStrip menu, Action<string> copyFilePath)
        {
            file = path;
            actions = menu;
            copyPath = copyFilePath;
            scale = Native.ScreenScale(at);
            padding = Px(5);
            footer = Px(23);
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(34, 36, 40);
            Text = "CornerGrab thumbnail";
            AccessibleName = "Screenshot thumbnail. Drag to attach, or click for options.";
            Cursor = Cursors.Hand;

            using (var image = Image.FromFile(path))
            {
                double fit = Math.Min(Px(208) / (double)image.Width, Px(128) / (double)image.Height);
                var imageSize = new Size(Math.Max(1, (int)Math.Round(image.Width * fit)), Math.Max(1, (int)Math.Round(image.Height * fit)));
                thumbnail = new Bitmap(imageSize.Width, imageSize.Height);
                using (var g = Graphics.FromImage(thumbnail))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, new Rectangle(Point.Empty, imageSize));
                }
            }
            ClientSize = new Size(Math.Max(Px(157), thumbnail.Width + 2 * padding), Math.Max(Px(48), thumbnail.Height) + 2 * padding + footer);
            var area = Screen.FromPoint(at).WorkingArea;
            Location = new Point(area.Right - Width - Px(16), area.Bottom - Height - Px(16));
            using (var frame = Rounded(new Rectangle(0, 0, Width, Height), Px(12))) Region = new Region(frame);
            expiry.Tick += delegate { if (!Busy && !Bounds.Contains(Control.MousePosition)) Hide(); };
            actions.Closed += MenuClosed;
        }

        private int Px(int value) { return (int)Math.Round(value * scale); }

        private static GraphicsPath Rounded(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            int d = 2 * r;
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080; return p; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(215, 222, 225), Math.Max(1, Px(2))))
            using (var frame = Rounded(new Rectangle(1, 1, Width - 2, Height - 2), Px(11))) g.DrawPath(pen, frame);
            int imageHeight = Height - footer - 2 * padding;
            g.DrawImageUnscaled(thumbnail, (Width - thumbnail.Width) / 2, padding + (imageHeight - thumbnail.Height) / 2);
            var caption = new Rectangle(padding + Px(4), Height - footer - padding, Width - 2 * padding - Px(8), footer);
            TextRenderer.DrawText(g, boardHint ? "Image +  ·  Copy path" : "Drag PNG     ···", SystemFonts.MessageBoxFont, caption,
                Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void RestartTimer()
        {
            expiry.Stop();
            if (Visible && !Busy) expiry.Start();
        }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); RestartTimer(); }
        protected override void OnMouseEnter(EventArgs e) { expiry.Stop(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); RestartTimer(); }

        private void MenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            menuOpen = false;
            RestartTimer();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { ShowActions(e.Location); return; }
            if (e.Button == MouseButtons.Left) { pressed = e.Location; Capture = true; expiry.Stop(); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!pressed.HasValue || dragging || (Control.MouseButtons & MouseButtons.Left) == 0) return;
            var delta = SystemInformation.DragSize;
            if (Math.Abs(e.X - pressed.Value.X) < delta.Width / 2 && Math.Abs(e.Y - pressed.Value.Y) < delta.Height / 2) return;
            pressed = null;
            dragging = true;
            Capture = false;
            try
            {
                if (!File.Exists(file)) { Hide(); return; }
                var effect = DoDragDrop(CaptureFiles.AsFile(file), DragDropEffects.Copy);
                if (effect == DragDropEffects.Copy) Hide();
                else if (Native.OverDiscord()) { boardHint = true; Invalidate(); }
            }
            finally { dragging = false; RestartTimer(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !pressed.HasValue) return;
            pressed = null;
            Capture = false;
            if (boardHint && e.Y >= Height - footer - padding) { copyPath(file); boardHint = false; Invalidate(); RestartTimer(); }
            else ShowActions(e.Location);
        }

        private void ShowActions(Point location)
        {
            expiry.Stop();
            menuOpen = true;
            actions.Show(this, location);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                expiry.Dispose();
                actions.Closed -= MenuClosed;
                actions.Dispose();
                thumbnail.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
