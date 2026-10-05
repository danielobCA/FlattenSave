using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FlattenSave
{
    // Vertical scroll host with a slim themed scrollbar instead of the native one.
    internal sealed class DarkScrollPanel : Panel
    {
        private const int BarWidth = 8, Gutter = 4;
        private readonly ScrollThumbBar bar;
        private Control content;
        private int offset;

        public DarkScrollPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            bar = new ScrollThumbBar(this) { Width = BarWidth };
            Controls.Add(bar);
            MouseWheel += OnWheel;
        }

        internal int ContentHeight => content?.Height ?? 0;
        internal int Offset => offset;
        internal int MaxOffset => Math.Max(0, ContentHeight - Height);

        public void SetContent(Control c)
        {
            content = c;
            Controls.Add(c);
            HookChildren(c);
            bar.BringToFront();
            DoLayoutNow();
        }

        private void HookChildren(Control c)
        {
            c.MouseWheel += OnWheel;
            c.Enter += (s, e) => EnsureVisible((Control)s);
            foreach (Control child in c.Controls) HookChildren(child);
        }

        private void EnsureVisible(Control c)
        {
            if (content == null || c == content) return;
            var r = RectangleToClient(c.Parent.RectangleToScreen(c.Bounds));
            if (r.Top < 0) ScrollTo(offset + r.Top - 4);
            else if (r.Bottom > Height) ScrollTo(offset + r.Bottom - Height + 4);
        }

        private void OnWheel(object sender, MouseEventArgs e)
        {
            if (e is HandledMouseEventArgs h) h.Handled = true;
            if (MaxOffset > 0) ScrollTo(offset - Math.Sign(e.Delta) * 48);
        }

        internal void ScrollTo(int value)
        {
            offset = Math.Max(0, Math.Min(MaxOffset, value));
            if (content != null) content.Location = new Point(0, -offset);
            bar.Invalidate();
        }

        private void DoLayoutNow()
        {
            if (content == null) return;
            int w = Math.Max(0, Width - BarWidth - Gutter);
            content.MinimumSize = new Size(w, 0);
            content.MaximumSize = new Size(w, 0);
            bar.SetBounds(Width - BarWidth - 2, 2, BarWidth, Math.Max(0, Height - 4));
            bar.Visible = MaxOffset > 0;
            ScrollTo(offset);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); DoLayoutNow(); }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (content != null)
            {
                bar.Visible = MaxOffset > 0;
                if (offset > MaxOffset) ScrollTo(MaxOffset);
            }
        }
    }

    internal sealed class ScrollThumbBar : Control
    {
        private readonly DarkScrollPanel owner;
        private bool hover, dragging;
        private int grabY, grabOffset;

        public ScrollThumbBar(DarkScrollPanel owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Default;
        }

        private Rectangle Thumb
        {
            get
            {
                int total = owner.ContentHeight, view = owner.Height;
                if (total <= view || Height <= 0) return Rectangle.Empty;
                int h = Math.Max(24, (int)((long)Height * view / total));
                int travel = Height - h;
                int y = owner.MaxOffset == 0 ? 0 : (int)((long)travel * owner.Offset / owner.MaxOffset);
                return new Rectangle(0, y, Width, h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Body);
            var t = Thumb;
            if (t.IsEmpty) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundRect(new Rectangle(1, t.Y, Width - 2, t.Height), (Width - 2) / 2))
            using (var br = new SolidBrush(dragging || hover ? Theme.Muted : Theme.Border))
                g.FillPath(br, path);
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var t = Thumb;
            if (t.IsEmpty) return;
            if (t.Contains(e.Location)) { dragging = true; grabY = e.Y; grabOffset = owner.Offset; }
            else owner.ScrollTo(owner.Offset + (e.Y < t.Y ? -owner.Height : owner.Height) * 9 / 10);
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                int travel = Height - Thumb.Height;
                if (travel > 0) owner.ScrollTo(grabOffset + (int)((long)(e.Y - grabY) * owner.MaxOffset / travel));
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Invalidate(); base.OnMouseUp(e); }
    }
}
