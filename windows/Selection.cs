using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CornerGrab
{
    internal sealed class Selection : Form
    {
        private readonly Bitmap screen;
        private readonly Rectangle desktop;
        private Point anchor, tip;
        private bool selecting;
        internal Bitmap Result { get; private set; }
        internal Point ScreenTip { get { return new Point(desktop.Left + tip.X, desktop.Top + tip.Y); } }

        internal Selection()
        {
            desktop = SystemInformation.VirtualScreen;
            screen = new Bitmap(desktop.Width, desktop.Height, PixelFormat.Format24bppRgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(screen))
                    graphics.CopyFromScreen(desktop.Location, Point.Empty, desktop.Size, CopyPixelOperation.SourceCopy);
            }
            catch { screen.Dispose(); throw; }
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.Manual;
            Bounds = desktop;
            Cursor = Cursors.Cross;
            Text = "CornerGrab selection";
            KeyPreview = true;
        }

        private Rectangle RegionBox
        {
            get { return Rectangle.FromLTRB(Math.Min(anchor.X, tip.X), Math.Min(anchor.Y, tip.Y), Math.Max(anchor.X, tip.X), Math.Max(anchor.Y, tip.Y)); }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Bounds = desktop;
            Native.SetForegroundWindow(Handle);
            Activate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RegisterHotKey(Handle, 7, 0x4000, (uint)Keys.Escape);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Native.UnregisterHotKey(Handle, 7);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 7) { Close(); return; }
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(screen, Point.Empty);
            using (var shade = new SolidBrush(Color.FromArgb(95, 0, 0, 0))) g.FillRectangle(shade, ClientRectangle);
            Rectangle box = RegionBox;
            if (selecting && box.Width > 0 && box.Height > 0)
            {
                g.DrawImage(screen, box, box, GraphicsUnit.Pixel);
                g.DrawRectangle(Pens.White, box.Left, box.Top, box.Width - 1, box.Height - 1);
                string size = box.Width + " × " + box.Height;
                var textSize = TextRenderer.MeasureText(size, SystemFonts.MessageBoxFont);
                Rectangle label = new Rectangle(Math.Min(box.Left, Width - textSize.Width - 12),
                    Math.Max(0, box.Top - textSize.Height - 8), textSize.Width + 12, textSize.Height + 6);
                g.FillRectangle(Brushes.Black, label);
                TextRenderer.DrawText(g, size, SystemFonts.MessageBoxFont, label, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                var monitor = Screen.FromPoint(Control.MousePosition).WorkingArea;
                string help = "Drag to capture     ·     Esc to cancel";
                Size size = TextRenderer.MeasureText(help, SystemFonts.MessageBoxFont);
                var label = new Rectangle(monitor.Left - desktop.Left + (monitor.Width - size.Width - 24) / 2,
                    monitor.Top - desktop.Top + 28, size.Width + 24, size.Height + 14);
                g.FillRectangle(Brushes.Black, label);
                TextRenderer.DrawText(g, help, SystemFonts.MessageBoxFont, label, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { Close(); return; }
            if (e.Button != MouseButtons.Left) return;
            selecting = true;
            anchor = tip = e.Location;
            Capture = true;
            Invalidate();
        }

        private Point Clamped(Point p)
        {
            return new Point(Math.Max(0, Math.Min(screen.Width, p.X)), Math.Max(0, Math.Min(screen.Height, p.Y)));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!selecting) return;
            tip = Clamped(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!selecting || e.Button != MouseButtons.Left) return;
            tip = Clamped(e.Location);
            selecting = false;
            Capture = false;
            Rectangle box = RegionBox;
            if (box.Width > 1 && box.Height > 1) Result = screen.Clone(box, PixelFormat.Format24bppRgb);
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (Visible) Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) screen.Dispose();
            base.Dispose(disposing);
        }
    }
}
