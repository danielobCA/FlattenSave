using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FlattenSave
{
    internal static class Hotkey
    {
        public const int MaxKeys = 3;

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] private static extern IntPtr GetFocus();

        public static Keys Normalize(Keys k)
        {
            switch (k)
            {
                case Keys.LControlKey: case Keys.RControlKey: return Keys.ControlKey;
                case Keys.LShiftKey: case Keys.RShiftKey: return Keys.ShiftKey;
                case Keys.LMenu: case Keys.RMenu: return Keys.Menu;
                default: return k;
            }
        }

        public static bool IsModifier(Keys k) => k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu;

        public static Keys[] Parse(string[] names)
        {
            var list = new List<Keys>();
            if (names != null)
                foreach (var n in names)
                    if (Enum.TryParse(n, out Keys k) && !list.Contains(k)) list.Add(k);
            return list.Take(MaxKeys).ToArray();
        }

        public static string[] ToNames(IEnumerable<Keys> keys) => keys.Select(k => k.ToString()).ToArray();

        public static string Format(IEnumerable<Keys> keys)
        {
            var parts = keys.Select(k =>
            {
                switch (k)
                {
                    case Keys.ControlKey: return "Ctrl";
                    case Keys.ShiftKey: return "Shift";
                    case Keys.Menu: return "Alt";
                    case Keys.Oemtilde: return "`";
                    case Keys.OemMinus: return "-";
                    case Keys.Oemplus: return "=";
                    case Keys.OemOpenBrackets: return "[";
                    case Keys.Oem6: return "]";
                    case Keys.Oem5: return "\\";
                    case Keys.Oem1: return ";";
                    case Keys.Oem7: return "'";
                    case Keys.Oemcomma: return ",";
                    case Keys.OemPeriod: return ".";
                    case Keys.OemQuestion: return "/";
                    default:
                        string s = k.ToString();
                        return s.Length == 2 && s[0] == 'D' && char.IsDigit(s[1]) ? s.Substring(1) : s;
                }
            }).ToArray();
            return parts.Length == 0 ? "None" : string.Join(" + ", parts);
        }

        private static bool IsDown(Keys k) => (GetAsyncKeyState((int)k) & 0x8000) != 0;

        // True when the pressed key completes the bound combination and no unbound modifier is held.
        public static bool Matches(Keys[] bound, Keys pressed)
        {
            if (bound.Length == 0 || !bound.Contains(pressed)) return false;
            foreach (var k in bound) if (!IsDown(k)) return false;
            foreach (var m in new[] { Keys.ControlKey, Keys.ShiftKey, Keys.Menu })
                if (IsDown(m) != bound.Contains(m)) return false;
            return true;
        }

        public static bool TypingInTextField()
        {
            var c = Control.FromHandle(GetFocus());
            return c is TextBoxBase || c is HotkeyBox;
        }
    }

    // Listens for the bound key combination while Paint.NET is the active application.
    internal sealed class HotkeyFilter : IMessageFilter
    {
        private readonly Func<Keys[]> keys;
        private readonly Action fire;

        public HotkeyFilter(Func<Keys[]> keys, Action fire) { this.keys = keys; this.fire = fire; }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != 0x100 && m.Msg != 0x104) return false;
            if (((m.LParam.ToInt64() >> 30) & 1) == 1) return false;
            var bound = keys();
            if (bound.Length == 0) return false;
            var pressed = Hotkey.Normalize((Keys)(int)(m.WParam.ToInt64() & 0xFF));
            if (Hotkey.TypingInTextField() || !Hotkey.Matches(bound, pressed)) return false;
            fire();
            return true;
        }
    }

    internal sealed class HotkeyBox : Control
    {
        private readonly List<Keys> held = new List<Keys>();
        private readonly List<Keys> captured = new List<Keys>();
        public event EventHandler KeysChanged;

        public HotkeyBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            TabStop = true; Font = Theme.Font; Height = 26; Cursor = Cursors.IBeam;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Keys[] Value
        {
            get => captured.ToArray();
            set { captured.Clear(); captured.AddRange(value); Invalidate(); }
        }

        protected override bool IsInputKey(Keys keyData) => true;
        protected override bool IsInputChar(char charCode) => true;
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => Focused ? false : base.ProcessCmdKey(ref msg, keyData);
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
        protected override void OnGotFocus(EventArgs e) { held.Clear(); Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { held.Clear(); Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true; e.Handled = true;
            var k = Hotkey.Normalize(e.KeyCode);
            if (k == Keys.None) return;
            if (held.Count == 0) captured.Clear();
            if (!held.Contains(k)) held.Add(k);
            if (!captured.Contains(k) && captured.Count < Hotkey.MaxKeys) captured.Add(k);
            Invalidate();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            e.Handled = true;
            held.Remove(Hotkey.Normalize(e.KeyCode));
            if (held.Count == 0)
            {
                // Modifiers first, then the remaining keys, for a stable display order.
                var sorted = captured.OrderBy(k => k == Keys.ControlKey ? 0 : k == Keys.ShiftKey ? 1 : k == Keys.Menu ? 2 : 3).ToList();
                captured.Clear(); captured.AddRange(sorted);
                KeysChanged?.Invoke(this, EventArgs.Empty);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Input);
            using (var pen = new Pen(Focused ? Theme.Accent : Theme.Border)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            string text = Focused && held.Count == 0 && captured.Count == 0 ? "Press keys..." : Hotkey.Format(captured);
            bool placeholder = captured.Count == 0;
            TextRenderer.DrawText(g, text, Font, new Rectangle(6, 0, Width - 12, Height), placeholder ? Theme.Muted : Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class IconButton : Control
    {
        private bool hover;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Image Icon;

        public IconButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var br = new SolidBrush(hover ? Theme.ButtonHover : Theme.Header)) g.FillRectangle(br, ClientRectangle);
            if (Icon != null) g.DrawImage(Icon, (Width - Icon.Width) / 2, (Height - Icon.Height) / 2, Icon.Width, Icon.Height);
        }
    }

    internal static class HotkeyConflicts
    {
        private static readonly string[] Known =
        {
            "Ctrl+S", "Ctrl+Shift+S", "Ctrl+O", "Ctrl+N", "Ctrl+Z", "Ctrl+Y", "Ctrl+X", "Ctrl+C", "Ctrl+V", "Ctrl+A",
            "Ctrl+D", "Ctrl+W", "Ctrl+P", "Ctrl+I", "Ctrl+Shift+A", "Ctrl+Shift+Z", "Ctrl+Shift+V", "Ctrl+Shift+N",
            "Ctrl+Shift+X", "Ctrl+Shift+I", "Ctrl+Shift+D", "Ctrl+Shift+F", "Alt+F4", "F1", "F4"
        };

        // Returns a warning for combinations that clash with a Paint.NET shortcut, or null.
        public static string Check(Keys[] keys)
        {
            if (keys.Length == 0) return null;
            bool ctrl = keys.Contains(Keys.ControlKey), shift = keys.Contains(Keys.ShiftKey), alt = keys.Contains(Keys.Menu);
            var main = keys.Where(k => !Hotkey.IsModifier(k)).ToArray();
            if (main.Length == 0) return "Modifier-only hotkeys can trigger while you use other shortcuts.";
            string name = Hotkey.Format(main).Replace(" + ", "+");
            string combo = (ctrl ? "Ctrl+" : "") + (shift ? "Shift+" : "") + (alt ? "Alt+" : "") + name;
            if (Known.Contains(combo, StringComparer.OrdinalIgnoreCase))
                return combo + " is a Paint.NET shortcut; the hotkey will override it while the panel is open.";
            if (!ctrl && !alt && main.Length == 1 && (main[0] >= Keys.A && main[0] <= Keys.Z))
                return "Single letters are Paint.NET tool shortcuts; the hotkey will override them.";
            return null;
        }
    }

    // Thin strip along a panel edge; dragging it (or the last few pixels at its ends, which resize diagonally) resizes the owning form.
    internal sealed class ResizeStrip : Control
    {
        public enum Edge { Left, Right, Top, Bottom }
        private readonly Edge edge;
        private Point start; private Rectangle startBounds; private bool dragging, left, right, top, bottom;

        public ResizeStrip(Edge edge)
        {
            this.edge = edge;
            SetStyle(ControlStyles.Selectable, false);
            switch (edge)
            {
                case Edge.Left: Dock = DockStyle.Left; Width = 4; break;
                case Edge.Right: Dock = DockStyle.Right; Width = 4; break;
                case Edge.Top: Dock = DockStyle.Top; Height = 4; break;
                default: Dock = DockStyle.Bottom; Height = 4; break;
            }
        }

        // Paints the same colors as the panel behind it (header on top, body below) so the strip is invisible.
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Body);
            if (edge == Edge.Top) e.Graphics.Clear(Theme.Header);
            else if (edge == Edge.Left || edge == Edge.Right)
                using (var br = new SolidBrush(Theme.Header)) e.Graphics.FillRectangle(br, 0, 0, Width, 26);
        }

        private void Hit(Point p, out bool l, out bool r, out bool t, out bool b)
        {
            const int c = 14;
            l = edge == Edge.Left; r = edge == Edge.Right; t = edge == Edge.Top; b = edge == Edge.Bottom;
            if (edge == Edge.Top || edge == Edge.Bottom) { if (p.X < c) l = true; else if (p.X >= Width - c) r = true; }
            else { if (p.Y < c) t = true; else if (p.Y >= Height - c) b = true; }
        }

        private static Cursor CursorFor(bool l, bool r, bool t, bool b)
        {
            if ((l && t) || (r && b)) return Cursors.SizeNWSE;
            if ((r && t) || (l && b)) return Cursors.SizeNESW;
            return l || r ? Cursors.SizeWE : Cursors.SizeNS;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging)
            {
                bool l, r, t, b; Hit(e.Location, out l, out r, out t, out b);
                Cursor = CursorFor(l, r, t, b);
            }
            else
            {
                var f = FindForm();
                int dx = Cursor.Position.X - start.X, dy = Cursor.Position.Y - start.Y;
                int x = startBounds.X, y = startBounds.Y, w = startBounds.Width, h = startBounds.Height;
                int minW = f.MinimumSize.Width, minH = f.MinimumSize.Height;
                if (right) w = Math.Max(minW, w + dx);
                if (left) { int nw = Math.Max(minW, w - dx); x += w - nw; w = nw; }
                if (bottom) h = Math.Max(minH, h + dy);
                if (top) { int nh = Math.Max(minH, h - dy); y += h - nh; h = nh; }
                f.Bounds = new Rectangle(x, y, w, h);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true; Hit(e.Location, out left, out right, out top, out bottom);
                start = Cursor.Position; startBounds = FindForm().Bounds;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }
    }
    internal sealed class SettingsForm : Form
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private readonly Settings settings;
        private readonly List<FileTypeChoice> choices;
        private readonly ToolTip tips = DarkTips.Create();
        private readonly Action onChanged, onReset;
        private readonly DarkCheckBox multi = new DarkCheckBox { Text = "Multi-export (also save other formats)", Dock = DockStyle.Top };
        private readonly DarkCheckBox fullPath = new DarkCheckBox { Text = "Show full file path in export preview", Dock = DockStyle.Top };
        private readonly DarkCheckBox updates = new DarkCheckBox { Text = "Check for updates", Dock = DockStyle.Top };
        private readonly HotkeyBox hotkey = new HotkeyBox { Dock = DockStyle.Fill };
        private readonly Label conflict = new Label { AutoSize = false, Height = 32, Dock = DockStyle.Top, ForeColor = Theme.Error, BackColor = Theme.Body };
        private readonly Label updateStatus = new Label { AutoSize = false, Height = 20, Dock = DockStyle.Top, ForeColor = Theme.Muted, BackColor = Theme.Body };
        private readonly DarkScrollPanel formatList = new DarkScrollPanel { Dock = DockStyle.Top, Height = 96, BackColor = Theme.Body };
        private readonly Panel listContent = new Panel { BackColor = Theme.Body };
        private bool loading;
        private UpdateInfo found;

        public SettingsForm(Form owner, Settings settings, List<FileTypeChoice> choices, Action onChanged, Action onReset)
        {
            this.settings = settings; this.choices = choices; this.onChanged = onChanged; this.onReset = onReset;
            Text = "FlattenSave Settings";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Border;
            Padding = new Padding(1);
            Font = Theme.Font;
            ClientSize = new Size(290, 400);
            Location = new Point(owner.Left + 10, owner.Top + 34);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Body, Padding = new Padding(10, 8, 10, 10) };

            multi.CheckedChanged += (s, e) => { formatList.Visible = multi.Checked; if (!loading) { settings.MultiExport = multi.Checked; Commit(); } };
            tips.SetToolTip(multi, "Each export also saves the formats ticked below, with the same name");
            foreach (var c in choices)
            {
                var cb = new DarkCheckBox { Text = c.Info.Name + " (" + c.Extension + ")", Dock = DockStyle.Top, Tag = c.Extension };
                cb.CheckedChanged += (s, e) => { if (!loading) { settings.ExtraExtensions = ExtraFromUi(); Commit(); } };
                listContent.Controls.Add(cb);
            }
            listContent.Height = choices.Count * 22;
            // Docked-top controls stack in reverse add order; flip so the list reads in sorted order.
            for (int i = 0; i < listContent.Controls.Count; i++) listContent.Controls.SetChildIndex(listContent.Controls[i], 0);
            formatList.SetContent(listContent);

            var hkLabel = MakeLabel("Export hotkey (up to 3 keys)");
            hotkey.KeysChanged += (s, e) =>
            {
                if (loading) return;
                settings.HotkeyKeys = Hotkey.ToNames(hotkey.Value); Commit(); ShowConflict();
            };
            tips.SetToolTip(hotkey, "Click, then hold the keys you want (one to three). Works while Paint.NET is focused and the panel is open.");
            var clear = new DarkButton { Text = "Clear", Dock = DockStyle.Right, Width = 54, Margin = Padding.Empty };
            clear.Click += (s, e) => { hotkey.Value = new Keys[0]; settings.HotkeyKeys = new string[0]; Commit(); ShowConflict(); };
            var row = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Theme.Body };
            row.Controls.Add(hotkey);
            row.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 6, BackColor = Theme.Body });
            row.Controls.Add(clear);

            fullPath.CheckedChanged += (s, e) => { if (!loading) { settings.ShowFullPath = fullPath.Checked; Commit(); } };
            tips.SetToolTip(fullPath, "Show the whole output path, not just the file name, in the preview at the bottom of the panel");
            updates.CheckedChanged += (s, e) =>
            {
                if (loading) return;
                settings.CheckForUpdates = updates.Checked; Commit();
                if (updates.Checked) RunCheck(); else { updateStatus.Text = ""; found = null; }
            };
            tips.SetToolTip(updates, "Asks GitHub for the latest FlattenSave release when the panel opens (at most once a day). Nothing is downloaded or installed.");
            var checkNow = new DarkButton { Text = "Check now", Dock = DockStyle.Top, Height = 26, Margin = Padding.Empty };
            checkNow.Click += (s, e) => RunCheck();
            updateStatus.Click += (s, e) => { if (found != null && found.IsNewer) UpdateCheck.OpenReleasePage(found.Url); };

            var reset = new DarkButton { Text = "Reset defaults", Dock = DockStyle.Top, Height = 28 };
            reset.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Reset all FlattenSave settings to their defaults? (The output directory is kept.)", "FlattenSave",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                settings.ResetToDefaults(); settings.Save(); LoadFromSettings(); onReset?.Invoke();
            };

            // Hotkey, full-path and multi-export options sit at the top; the Updates section and Reset defaults are anchored to the bottom.
            var bottom = new Panel { Dock = DockStyle.Bottom, BackColor = Theme.Body };
            // Added in reverse because every control docks to the top.
            bottom.Controls.Add(reset);
            bottom.Controls.Add(Spacer(10));
            bottom.Controls.Add(updateStatus);
            bottom.Controls.Add(checkNow);
            bottom.Controls.Add(updates);
            bottom.Controls.Add(MakeLabel("Updates"));
            bottom.Height = bottom.Controls.Cast<Control>().Sum(c => c.Height);
            body.Controls.Add(formatList);
            body.Controls.Add(multi);
            body.Controls.Add(fullPath);
            body.Controls.Add(conflict);
            body.Controls.Add(row);
            body.Controls.Add(hkLabel);
            body.Controls.Add(bottom);
            Controls.Add(body);
            Controls.Add(BuildHeader());
            LoadFromSettings();
        }

        private static Label MakeLabel(string text) => new Label
        {
            Text = text, AutoSize = false, Height = 24, Dock = DockStyle.Top, ForeColor = Theme.Text, BackColor = Theme.Body,
            TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(0, 0, 0, 2)
        };

        private static Panel Spacer(int h) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Theme.Body };

        private string[] ExtraFromUi() =>
            listContent.Controls.OfType<DarkCheckBox>().Where(c => c.Checked).Select(c => (string)c.Tag).ToArray();

        private void LoadFromSettings()
        {
            loading = true;
            fullPath.Checked = settings.ShowFullPath;
            multi.Checked = settings.MultiExport; formatList.Visible = multi.Checked;
            foreach (var cb in listContent.Controls.OfType<DarkCheckBox>())
                cb.Checked = settings.ExtraExtensions.Contains((string)cb.Tag);
            hotkey.Value = Hotkey.Parse(settings.HotkeyKeys);
            updates.Checked = settings.CheckForUpdates;
            loading = false;
            ShowConflict();
        }

        private void ShowConflict() => conflict.Text = HotkeyConflicts.Check(Hotkey.Parse(settings.HotkeyKeys)) ?? "";

        private async void RunCheck()
        {
            updateStatus.ForeColor = Theme.Muted; updateStatus.Text = "Checking...";
            var info = await UpdateCheck.CheckAsync();
            if (IsDisposed) return;
            found = info;
            ExportForm.LastUpdate = info ?? ExportForm.LastUpdate;
            if (info == null) { updateStatus.ForeColor = Theme.Error; updateStatus.Text = "Couldn't reach GitHub."; }
            else if (info.IsNewer) { updateStatus.ForeColor = Theme.AccentHover; updateStatus.Cursor = Cursors.Hand; updateStatus.Text = "v" + info.Latest + " available - click to open"; }
            else { updateStatus.ForeColor = Theme.Muted; updateStatus.Text = "You're up to date (v" + UpdateCheck.Current + ")."; }
            onChanged?.Invoke();
        }

        private void Commit() { settings.Save(); onChanged?.Invoke(); }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int round = 3, border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
            if (DwmSetWindowAttribute(Handle, 33, ref round, 4) == 0) DwmSetWindowAttribute(Handle, 34, ref border, 4);
        }

        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Theme.Header };
            var title = new Label
            {
                Text = "Settings", ForeColor = Theme.Text, BackColor = Theme.Header, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0)
            };
            var close = new DarkButton { DrawCross = true, BackNormal = Theme.Close, BackHover = Color.FromArgb(0xE0, 0x60, 0x60), Dock = DockStyle.Fill };
            close.Click += (s, e) => Close();
            var closeHost = new Panel { Dock = DockStyle.Right, Width = 28, BackColor = Theme.Header, Padding = new Padding(2, 4, 8, 4) };
            closeHost.Controls.Add(close);
            MouseEventHandler drag = (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            };
            title.MouseDown += drag; header.MouseDown += drag;
            header.Controls.Add(title);
            header.Controls.Add(closeHost);
            return header;
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape && !(ActiveControl is HotkeyBox)) { Close(); return true; }
            return base.ProcessDialogKey(keyData);
        }
    }
}
