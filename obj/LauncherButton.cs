using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FlattenSave
{
    // Small borderless button that floats over Paint.NET's title-bar button strip (in the empty area under the window buttons) and opens the panel.
    // Paint.NET has no API for toolbar buttons, so it follows the main window and snapshots the pixels behind it.
    internal sealed class LauncherButton : Form
    {
        private const int Size96 = 32, RightOffset96 = 53, Top96 = 62;
        private readonly Form main;
        private readonly Timer follow = new Timer { Interval = 100 };
        private readonly ToolTip tips = DarkTips.Create();
        private readonly Timer tipTimer = new Timer { Interval = 400 };
        private Bitmap backdrop, icon;
        private bool hover, down, open;
        private Rectangle lastBounds;

        public LauncherButton(Form main)
        {
            this.main = main;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            // Automatic tooltips never fire on a non-activating window, so show it manually after a short hover delay.
            tipTimer.Tick += (s, e) => { tipTimer.Stop(); if (hover) tips.Show("Open / close FlattenSave", this, 0, Height + 4, 4000); };
            follow.Tick += (s, e) =>
            {
                Reposition(false);
                if (PanelHost.IsOpen != open) { open = PanelHost.IsOpen; Invalidate(); }
            };
            FormClosed += (s, e) => { follow.Dispose(); tipTimer.Dispose(); tips.Dispose(); backdrop?.Dispose(); icon?.Dispose(); };
            main.FormClosed += (s, e) => Close();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080; return cp; } // NOACTIVATE | TOOLWINDOW
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadIcon();
            Reposition(true);
            follow.Start();
        }

        private void LoadIcon()
        {
            using (var s = typeof(LauncherButton).Assembly.GetManifestResourceStream("FlattenSave.icon.png"))
            {
                if (s == null) return;
                using (var src = new Bitmap(s))
                {
                    int px = (int)Math.Round(20 * DeviceDpi / 96.0);
                    icon = new Bitmap(px, px);
                    using (var g = Graphics.FromImage(icon))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, px, px);
                    }
                }
            }
        }

        private void Reposition(bool force)
        {
            bool visible = main.WindowState != FormWindowState.Minimized && main.Visible;
            if (!visible) { if (Visible) Hide(); return; }
            double k = main.DeviceDpi / 96.0;
            int size = (int)Math.Round(Size96 * k);
            var mb = main.Bounds;
            int inset = main.WindowState == FormWindowState.Maximized ? (int)Math.Round(8 * k) : 0;
            var r = new Rectangle(mb.Right - inset - (int)Math.Round(RightOffset96 * k), mb.Top + inset + (int)Math.Round(Top96 * k), size, size);
            if (!force && r == lastBounds && Visible) return;
            lastBounds = r;
            CaptureBackdrop(r);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        // Renders Paint.NET's own window (not the screen) so other windows never bleed into the button's background.
        private void CaptureBackdrop(Rectangle r)
        {
            Bounds = r;
            if (!Visible) Show();
            Bitmap result = null;
            try
            {
                var mb = main.Bounds;
                using (var full = new Bitmap(mb.Width, mb.Height))
                {
                    using (var g = Graphics.FromImage(full))
                    {
                        IntPtr hdc = g.GetHdc();
                        try { PrintWindow(main.Handle, hdc, 2); } finally { g.ReleaseHdc(hdc); }
                    }
                    var src = new Rectangle(r.X - mb.X, r.Y - mb.Y, r.Width, r.Height);
                    if (new Rectangle(0, 0, full.Width, full.Height).Contains(src))
                        result = full.Clone(src, full.PixelFormat);
                }
            }
            catch { }
            if (result == null) { result = new Bitmap(r.Width, r.Height); using (var g = Graphics.FromImage(result)) g.Clear(Theme.Header); }
            backdrop?.Dispose(); backdrop = result;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (backdrop != null) g.DrawImage(backdrop, 0, 0);
            if (open)
            {
                // Same look as Paint.NET's selected toolbar buttons: blue fill with a lighter blue outline.
                using (var br = new SolidBrush(Color.FromArgb(0x1A, 0x4F, 0x8A)))
                using (var pen = new Pen(Color.FromArgb(0x3C, 0x8C, 0xD8)))
                {
                    g.FillRectangle(br, 0, 0, Width, Height);
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }
            if (hover || down)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(Color.FromArgb(down ? 70 : 40, 255, 255, 255)))
                using (var path = Rounded(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 4))
                    g.FillPath(br, path);
            }
            if (icon != null) g.DrawImage(icon, (Width - icon.Width) / 2, (Height - icon.Height) / 2, icon.Width, icon.Height);
        }

        private static GraphicsPath Rounded(RectangleF r, float d)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); tipTimer.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); tipTimer.Stop(); tips.Hide(this); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool click = down && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location);
            down = false; Invalidate();
            if (click) { tipTimer.Stop(); tips.Hide(this); PanelHost.TogglePanel(); }
            base.OnMouseUp(e);
        }
    }
}
