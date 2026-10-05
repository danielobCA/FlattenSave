using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FlattenSave
{
    // Palette sampled from Paint.NET's dark and light themes; Apply switches between them.
    internal static class Theme
    {
        public static Color Body, Header, Border, Input, Text, Muted, Button, ButtonHover, Accent, AccentHover, Close, Error;
        public static bool IsDark { get; private set; }
        public static readonly Font Font = new Font("Segoe UI", 9f);

        static Theme() { Apply(DetectDark()); }

        public static void Apply(bool dark)
        {
            IsDark = dark;
            Accent = Color.FromArgb(0x1A, 0x5B, 0xA0); AccentHover = Color.FromArgb(0x24, 0x70, 0xC0);
            Close = Color.FromArgb(0xC7, 0x50, 0x50);
            if (dark)
            {
                Body = Color.FromArgb(0x28, 0x28, 0x28); Header = Color.FromArgb(0x20, 0x20, 0x20); Border = Color.FromArgb(0x4C, 0x4C, 0x4C);
                Input = Color.FromArgb(0x1C, 0x1C, 0x1C); Text = Color.FromArgb(0xE6, 0xE6, 0xE6); Muted = Color.FromArgb(0xA0, 0xA0, 0xA0);
                Button = Color.FromArgb(0x3A, 0x3A, 0x3A); ButtonHover = Color.FromArgb(0x4C, 0x4C, 0x4C); Error = Color.FromArgb(0xE0, 0x6C, 0x6C);
            }
            else
            {
                Body = Color.FromArgb(0xF2, 0xF2, 0xF2); Header = Color.FromArgb(0xE6, 0xE6, 0xE6); Border = Color.FromArgb(0xA0, 0xA0, 0xA0);
                Input = Color.White; Text = Color.FromArgb(0x1E, 0x1E, 0x1E); Muted = Color.FromArgb(0x6A, 0x6A, 0x6A);
                Button = Color.FromArgb(0xE1, 0xE1, 0xE1); ButtonHover = Color.FromArgb(0xD0, 0xD0, 0xD0); Error = Color.FromArgb(0xC0, 0x30, 0x30);
            }
        }

        // Follows Paint.NET's own theme setting, falling back to the Windows app theme when it is set to follow the system.
        public static bool DetectDark()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\paint.net"))
                {
                    string v = k?.GetValue("UI/AeroColorScheme") as string;
                    if (string.Equals(v, "Dark", StringComparison.OrdinalIgnoreCase)) return true;
                    if (string.Equals(v, "Light", StringComparison.OrdinalIgnoreCase)) return false;
                }
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return !(k?.GetValue("AppsUseLightTheme") is int i && i != 0);
            }
            catch { return true; }
        }
    }
    internal class DarkButton : Control
    {
        private bool hover, down;
        public Color BackNormal = Theme.Button, BackHover = Theme.ButtonHover;
        public bool DrawCross;

        public DarkButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.Font; ForeColor = Theme.Text; Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var br = new SolidBrush(hover ? BackHover : BackNormal)) g.FillRectangle(br, ClientRectangle);
            if (down) using (var br = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillRectangle(br, ClientRectangle);
            if (DrawCross)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float cx = Width / 2f, cy = Height / 2f, d = 3.2f;
                using var pen = new Pen(Color.White, 1.4f);
                g.DrawLine(pen, cx - d, cy - d, cx + d, cy + d);
                g.DrawLine(pen, cx - d, cy + d, cx + d, cy - d);
            }
            else
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    internal class DarkTextBox : Panel
    {
        public readonly TextBox Inner = new TextBox();

        public DarkTextBox()
        {
            Height = 26; Padding = new Padding(6, 4, 6, 3); BackColor = Theme.Input;
            Inner.BorderStyle = BorderStyle.None; Inner.BackColor = Theme.Input; Inner.ForeColor = Theme.Text;
            Inner.Font = Theme.Font; Inner.Dock = DockStyle.Fill;
            Inner.Enter += (s, e) => Invalidate();
            Inner.Leave += (s, e) => Invalidate();
            Controls.Add(Inner);
        }

        public override string Text { get => Inner.Text; set => Inner.Text = value; }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var pen = new Pen(Inner.Focused ? Theme.Accent : Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    internal class DarkCheckBox : Control
    {
        private bool hover, isChecked;
        public event EventHandler CheckedChanged;

        public DarkCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = Theme.Font; ForeColor = Theme.Text; Cursor = Cursors.Hand; Height = 22;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => isChecked;
            set { if (isChecked != value) { isChecked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Body);
            var box = new Rectangle(0, (Height - 15) / 2, 15, 15);
            using (var br = new SolidBrush(isChecked ? Theme.Accent : Theme.Input)) g.FillRectangle(br, box);
            using (var pen = new Pen(hover ? Theme.Muted : Theme.Border)) g.DrawRectangle(pen, box.X, box.Y, box.Width - 1, box.Height - 1);
            if (isChecked)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.White, 1.6f);
                g.DrawLines(pen, new[] { new PointF(box.X + 3.5f, box.Y + 7.5f), new PointF(box.X + 6.3f, box.Y + 10.3f), new PointF(box.X + 11.5f, box.Y + 4.5f) });
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(22, 0, Width - 22, Height), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    internal class DarkComboBox : Control
    {
        private bool hover;
        private int selected = -1;
        public readonly List<object> Items = new List<object>();
        public event EventHandler SelectedIndexChanged;

        public DarkComboBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = Theme.Font; ForeColor = Theme.Text; Cursor = Cursors.Hand; Height = 26;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => selected;
            set { if (selected != value) { selected = value; Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); } }
        }

        public object SelectedItem => selected >= 0 && selected < Items.Count ? Items[selected] : null;

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Items.Count == 0) return;
            var menu = new ContextMenuStrip
            {
                Renderer = new DarkRenderer(), BackColor = Theme.Input, ForeColor = Theme.Text, Font = Font,
                ShowImageMargin = false, MinimumSize = new Size(Width, 0)
            };
            for (int i = 0; i < Items.Count; i++)
            {
                int idx = i;
                var item = new ToolStripMenuItem(Items[i].ToString().Replace("&", "&&"))
                {
                    ForeColor = idx == selected ? Color.White : Theme.Text,
                    Font = idx == selected ? new Font(Font, FontStyle.Bold) : Font,
                    Padding = new Padding(2, 3, 2, 3)
                };
                item.Click += (s, a) => SelectedIndex = idx;
                menu.Items.Add(item);
            }
            menu.Closed += (s, a) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(this, new Point(0, Height - 1));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Input);
            using (var pen = new Pen(hover ? Theme.Muted : Theme.Border)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(g, SelectedItem?.ToString() ?? "", Font, new Rectangle(6, 0, Width - 26, Height), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = Width - 13, cy = Height / 2f;
            using var chev = new Pen(Theme.Muted, 1.5f);
            g.DrawLines(chev, new[] { new PointF(cx - 3.5f, cy - 1.5f), new PointF(cx, cy + 2f), new PointF(cx + 3.5f, cy - 1.5f) });
        }
    }

    internal sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            using var br = new SolidBrush(e.Item.Selected ? Theme.Accent : Theme.Input);
            e.Graphics.FillRectangle(br, new Rectangle(1, 0, e.Item.Width - 2, e.Item.Height));
        }
    }

    internal sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Input;
        public override Color MenuBorder => Theme.Border;
        public override Color ImageMarginGradientBegin => Theme.Input;
        public override Color ImageMarginGradientMiddle => Theme.Input;
        public override Color ImageMarginGradientEnd => Theme.Input;
    }

    internal enum IconKind { RotateCw, RotateCcw, Rotate180, MirrorH, MirrorV, Layers }

    internal class IconToggle : Control
    {
        private bool hover, isChecked;
        public IconKind Kind;
        private Image icon;
        public event EventHandler CheckedChanged;

        // Uses Paint.NET's own menu icons so the buttons match the app; falls back to drawn glyphs.
        private static Image LoadPdnIcon(IconKind kind, int dpi)
        {
            string name = kind switch
            {
                IconKind.RotateCw => "MenuImageRotate90CWIcon",
                IconKind.RotateCcw => "MenuImageRotate90CCWIcon",
                IconKind.Rotate180 => "MenuImageRotate180Icon",
                IconKind.MirrorH => "MenuImageFlipHorizontalIcon",
                IconKind.MirrorV => "MenuImageFlipVerticalIcon",
                _ => "MenuLayersDuplicateLayerIcon"
            };
            string size = dpi >= 192 ? "192" : dpi >= 144 ? "144" : "120";
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name != "PaintDotNet.Resources") continue;
                    using var st = a.GetManifestResourceStream("PaintDotNet.Icons." + name + "." + size + ".png");
                    if (st == null) return null;
                    using var bmp = new Bitmap(st);
                    return new Bitmap(bmp);
                }
            }
            catch { }
            return null;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            icon = LoadPdnIcon(Kind, DeviceDpi);
            Invalidate();
        }

        public IconToggle(IconKind kind, string tip, ToolTip tt)
        {
            Kind = kind; Size = new Size(34, 30); Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            tt.SetToolTip(this, tip);
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => isChecked;
            set { if (isChecked != value) { isChecked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(isChecked ? Theme.Accent : hover ? Theme.ButtonHover : Theme.Button);
            using (var pen = new Pen(isChecked ? Theme.AccentHover : Theme.Border)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            if (icon != null)
            {
                g.DrawImage(icon, (Width - icon.Width * 96f / DeviceDpi) / 2f, (Height - icon.Height * 96f / DeviceDpi) / 2f,
                    icon.Width * 96f / DeviceDpi, icon.Height * 96f / DeviceDpi);
                return;
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(Width / 2f, Height / 2f);
            Color c = isChecked ? Color.White : Theme.Text;
            using var pn = new Pen(c, 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var br = new SolidBrush(c);
            switch (Kind)
            {
                case IconKind.RotateCw: DrawRotate(g, pn, br); break;
                case IconKind.RotateCcw: g.ScaleTransform(-1, 1); DrawRotate(g, pn, br); break;
                case IconKind.Rotate180: g.RotateTransform(180); DrawRotate(g, pn, br); break;
                case IconKind.MirrorH: DrawMirror(g, pn, br); break;
                case IconKind.MirrorV: g.RotateTransform(90); DrawMirror(g, pn, br); break;
                case IconKind.Layers: DrawLayers(g, pn, br); break;
            }
        }

        private static void DrawRotate(Graphics g, Pen pn, Brush br)
        {
            const float r = 7f;
            g.DrawArc(pn, -r, -r, 2 * r, 2 * r, 200f, 250f);
            double a = 450 * Math.PI / 180;
            float ex = (float)(r * Math.Cos(a)), ey = (float)(r * Math.Sin(a));
            g.FillPolygon(br, new[] { new PointF(ex + 4.5f, ey - 0.5f), new PointF(ex - 3.5f, ey - 3.5f), new PointF(ex - 3.5f, ey + 3.5f) });
        }

        private static void DrawMirror(Graphics g, Pen pn, Brush br)
        {
            using var dash = new Pen(pn.Color, 1.4f) { DashStyle = DashStyle.Dash };
            g.DrawLine(dash, 0, -9, 0, 9);
            g.FillPolygon(br, new[] { new PointF(-2.5f, -6), new PointF(-9, 6), new PointF(-2.5f, 6) });
            g.DrawPolygon(pn, new[] { new PointF(2.5f, -6), new PointF(9, 6), new PointF(2.5f, 6) });
        }

        private static void DrawLayers(Graphics g, Pen pn, Brush br)
        {
            for (int i = 2; i >= 0; i--)
            {
                float y = -8 + i * 4.5f;
                var pts = new[] { new PointF(0, y), new PointF(9, y + 4.5f), new PointF(0, y + 9), new PointF(-9, y + 4.5f) };
                using (var back = new SolidBrush(Color.FromArgb(0x28, 0x28, 0x28))) { }
                g.DrawPolygon(pn, pts);
            }
            // Strike through the top layer to show it can be left out.
            g.DrawLine(pn, -9, 9, 9, -9);
        }
    }

    internal static class DarkTips
    {
        public static ToolTip Create()
        {
            var tt = new ToolTip { OwnerDraw = true, InitialDelay = 400, ReshowDelay = 100, AutoPopDelay = 8000 };
            tt.Popup += (s, e) =>
            {
                var size = TextRenderer.MeasureText(tt.GetToolTip(e.AssociatedControl), Theme.Font);
                e.ToolTipSize = new Size(size.Width + 12, size.Height + 8);
            };
            tt.Draw += (s, e) =>
            {
                using (var br = new SolidBrush(Theme.Input)) e.Graphics.FillRectangle(br, e.Bounds);
                using (var pen = new Pen(Theme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, Theme.Font, new Rectangle(6, 4, e.Bounds.Width - 6, e.Bounds.Height - 4), Theme.Text, TextFormatFlags.NoPrefix);
            };
            return tt;
        }
    }
}


