using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PaintDotNet;

namespace FlattenSave
{
    internal sealed class ExportForm : Form
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly Form main;
        private readonly Settings settings = Settings.Load();
        private readonly DarkTextBox pathBox = new DarkTextBox();
        private readonly DarkTextBox nameBox = new DarkTextBox();
        private readonly DarkComboBox typeBox = new DarkComboBox();
        private readonly DarkCheckBox noWarnBox = new DarkCheckBox();
        private readonly DarkCheckBox numberBox = new DarkCheckBox();
        private readonly DarkTextBox omitBox = new DarkTextBox();
        private readonly IconToggle cwBtn, ccwBtn, r180Btn, mhBtn, mvBtn, omitBtn;
        private readonly ToolTip tips = DarkTips.Create();
        private readonly Label preview = new Label();
        private readonly System.Windows.Forms.Timer previewTimer = new System.Windows.Forms.Timer { Interval = 700 };
        private readonly Label status = new Label();
        private SettingsForm settingsForm;
        private HotkeyFilter hotkeyFilter;
        private bool exporting, loading, usingFallbackRegion;
        private List<FileTypeChoice> allChoices;
        private readonly Label updateLabel = new Label();

        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int round = 3, border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
            int hr = DwmSetWindowAttribute(Handle, 33, ref round, 4);
            if (hr == 0) DwmSetWindowAttribute(Handle, 34, ref border, 4);
            else { usingFallbackRegion = true; ApplyFallbackRegion(); }
        }

        // Windows 10 has no native rounded windows, so clip a region instead.
        private void ApplyFallbackRegion()
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = 6, w = Width, h = Height;
            path.AddArc(0, 0, d, d, 180, 90); path.AddArc(w - d, 0, d, d, 270, 90);
            path.AddArc(w - d, h - d, d, d, 0, 90); path.AddArc(0, h - d, d, d, 90, 90);
            path.CloseFigure();
            Region = new Region(path);
        }

        public ExportForm(Form main)
        {
            this.main = main;
            Text = "FlattenSave";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Border;
            Padding = new Padding(1);
            Font = Theme.Font;
            MinimumSize = new Size(250, 230);
            ClientSize = new Size(290, 450);
            if (settings.PanelWidth >= MinimumSize.Width && settings.PanelHeight >= MinimumSize.Height)
                Size = new Size(settings.PanelWidth, settings.PanelHeight);
            var wa = Screen.FromControl(main).WorkingArea;
            var loc = new Point(wa.Right - Width - 20, wa.Top + 120);
            if (settings.PanelX != int.MinValue)
            {
                var saved = new Rectangle(settings.PanelX, settings.PanelY, Width, 26);
                if (Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(saved))) loc = new Point(settings.PanelX, settings.PanelY);
            }
            Location = loc;

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Body };
            var header = BuildHeader();

            var scroll = new DarkScrollPanel { Dock = DockStyle.Fill, BackColor = Theme.Body };
            var layout = new TableLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Location = Point.Empty,
                ColumnCount = 2, Padding = new Padding(10, 8, 10, 10), BackColor = Theme.Body
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));

            layout.Controls.Add(MakeLabel("Output Directory"), 0, 0); layout.SetColumnSpan(layout.GetControlFromPosition(0, 0), 2);

            pathBox.Dock = DockStyle.Fill; pathBox.Text = settings.OutputDirectory;
            pathBox.Inner.Leave += (s, e) => SaveSettings();
            var browse = new DarkButton { Dock = DockStyle.Fill, Margin = new Padding(4, 3, 3, 3), Icon = LoadResIcon("FlattenSave.open.png") };
            browse.Click += (s, e) => Browse();
            layout.Controls.Add(pathBox, 0, 1); layout.Controls.Add(browse, 1, 1);

            var nameLabel = MakeLabel("File name template (optional)");
            layout.Controls.Add(nameLabel, 0, 2); layout.SetColumnSpan(nameLabel, 2);
            nameBox.Dock = DockStyle.Fill; nameBox.Text = settings.FileName;
            nameBox.Inner.Leave += (s, e) => SaveSettings();
            layout.Controls.Add(nameBox, 0, 3); layout.SetColumnSpan(nameBox, 2);

            var typeLabel = MakeLabel("File type");
            layout.Controls.Add(typeLabel, 0, 4); layout.SetColumnSpan(typeLabel, 2);
            typeBox.Dock = DockStyle.Fill;
            allChoices = PdnAccess.GetSaveTypes(PdnAccess.GetFileTypesService(main));
            foreach (var c in allChoices) typeBox.Items.Add(c);
            int idx = allChoices.FindIndex(c => c.Extension == settings.Extension);
            if (idx < 0) idx = allChoices.FindIndex(c => c.Extension == ".png");
            if (idx < 0 && allChoices.Count > 0) idx = 0;
            typeBox.SelectedIndex = idx;
            typeBox.SelectedIndexChanged += (s, e) => SaveSettings();
            layout.Controls.Add(typeBox, 0, 5); layout.SetColumnSpan(typeBox, 2);

            layout.Controls.Add(MakeLabel("Export options"), 0, 6); layout.SetColumnSpan(layout.GetControlFromPosition(0, 6), 2);
            cwBtn = new IconToggle(IconKind.RotateCw, "Rotate 90 degrees clockwise", tips) { Checked = settings.Rotate == "cw" };
            ccwBtn = new IconToggle(IconKind.RotateCcw, "Rotate 90 degrees counter-clockwise", tips) { Checked = settings.Rotate == "ccw" };
            mhBtn = new IconToggle(IconKind.MirrorH, "Mirror horizontally", tips) { Checked = settings.MirrorH };
            mvBtn = new IconToggle(IconKind.MirrorV, "Mirror vertically", tips) { Checked = settings.MirrorV };
            omitBtn = new IconToggle(IconKind.Layers, "Omit layers: skip any layer whose name contains one of the words below", tips) { Checked = settings.OmitLayers };
            cwBtn.CheckedChanged += (s, e) => { if (cwBtn.Checked) { ccwBtn.Checked = false; r180Btn.Checked = false; } SaveSettings(); };
            r180Btn = new IconToggle(IconKind.Rotate180, "Rotate 180 degrees", tips) { Checked = settings.Rotate == "180" };
            r180Btn.CheckedChanged += (s, e) => { if (r180Btn.Checked) { cwBtn.Checked = false; ccwBtn.Checked = false; } SaveSettings(); };
            ccwBtn.CheckedChanged += (s, e) => { if (ccwBtn.Checked) { cwBtn.Checked = false; r180Btn.Checked = false; } SaveSettings(); };
            mhBtn.CheckedChanged += (s, e) => SaveSettings();
            mvBtn.CheckedChanged += (s, e) => SaveSettings();
            omitBtn.CheckedChanged += (s, e) => { UpdateOmitState(); SaveSettings(); };
            var row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, BackColor = Theme.Body, Margin = new Padding(0, 2, 0, 0), WrapContents = false };
            foreach (var b in new[] { cwBtn, ccwBtn, r180Btn, mhBtn, mvBtn, omitBtn }) { b.Margin = new Padding(3, 3, 3, 3); row.Controls.Add(b); }
            layout.Controls.Add(row, 0, 7); layout.SetColumnSpan(row, 2);

            var omitLabel = MakeLabel("Omit layers named (comma-separated)");
            layout.Controls.Add(omitLabel, 0, 8); layout.SetColumnSpan(omitLabel, 2);
            omitBox.Dock = DockStyle.Fill; omitBox.Text = settings.OmitNames;
            omitBox.Inner.Leave += (s, e) => SaveSettings();
            layout.Controls.Add(omitBox, 0, 9); layout.SetColumnSpan(omitBox, 2);
            UpdateOmitState();

            noWarnBox.Text = "Don't warn when overwriting";
            noWarnBox.Dock = DockStyle.Fill; noWarnBox.Margin = new Padding(3, 8, 3, 3);
            noWarnBox.Checked = settings.SuppressOverwriteWarning;
            noWarnBox.CheckedChanged += (s, e) => SaveSettings();
            layout.Controls.Add(noWarnBox, 0, 10); layout.SetColumnSpan(noWarnBox, 2);

            numberBox.Text = "Number exports (name_1, name_2...)";
            numberBox.Dock = DockStyle.Fill; numberBox.Margin = new Padding(3, 3, 3, 3);
            numberBox.Checked = settings.NumberExports;
            numberBox.CheckedChanged += (s, e) => { SaveSettings(); UpdatePreview(); };
            tips.SetToolTip(numberBox, "Never overwrite: each export gets the next free _N number in the output folder");
            layout.Controls.Add(numberBox, 0, 11); layout.SetColumnSpan(numberBox, 2);
            scroll.SetContent(layout);

            // The export button and its messages live outside the scroll area so they stay visible when the panel is shrunk.
            var export = new DarkButton
            {
                Text = "Flatten & Save", Dock = DockStyle.Fill, Height = 32, Margin = new Padding(0, 8, 0, 3),
                BackNormal = Theme.Accent, BackHover = Theme.AccentHover, ForeColor = Color.White
            };
            export.Click += (s, e) => DoExport();
            var separator = new Panel { Dock = DockStyle.Fill, Height = 1, BackColor = Theme.Border, Margin = Padding.Empty };
            foreach (var l in new[] { status, preview, updateLabel })
            { l.AutoSize = true; l.BackColor = Theme.Body; l.ForeColor = Theme.Muted; }
            status.Margin = new Padding(0, 4, 0, 0); preview.Margin = new Padding(0, 4, 0, 0); updateLabel.Margin = new Padding(0, 4, 0, 0);
            updateLabel.ForeColor = Theme.AccentHover; updateLabel.Cursor = Cursors.Hand; updateLabel.Visible = false;
            updateLabel.Click += (s, e) => { if (LastUpdate != null) UpdateCheck.OpenReleasePage(LastUpdate.Url); };
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 1, BackColor = Theme.Body, Padding = new Padding(10, 0, 10, 8)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.Controls.Add(separator, 0, 0);
            footer.Controls.Add(export, 0, 1);
            footer.Controls.Add(preview, 0, 2);
            footer.Controls.Add(status, 0, 3);
            footer.Controls.Add(updateLabel, 0, 4);
            footer.Resize += (s, e) =>
            {
                int w = Math.Max(40, footer.ClientSize.Width - 20);
                preview.MaximumSize = new Size(w, 0); status.MaximumSize = new Size(w, 0); updateLabel.MaximumSize = new Size(w, 0);
            };

            tips.SetToolTip(pathBox, "Folder the exported file is saved to. Paste a path here."); tips.SetToolTip(pathBox.Inner, tips.GetToolTip(pathBox));
            tips.SetToolTip(browse, "Browse for a folder");
            tips.SetToolTip(nameBox, "File name without extension. Leave empty to use the open image's name.\nTokens: {name} image name, {date}, {time}, {n} or {n:3} auto-counter (zero-padded)."); tips.SetToolTip(nameBox.Inner, tips.GetToolTip(nameBox));
            tips.SetToolTip(typeBox, "File format to export as");
            tips.SetToolTip(noWarnBox, "Overwrite existing files without asking");
            tips.SetToolTip(export, "Flatten the image and save it to the output directory");
            tips.SetToolTip(updateLabel, "Open the release page");
            tips.SetToolTip(omitBox, "Layer names to omit, comma-separated (only used when the omit-layers button is on)"); tips.SetToolTip(omitBox.Inner, tips.GetToolTip(omitBox));
            pathBox.Inner.TextChanged += (s, e) => UpdatePreview();
            nameBox.Inner.TextChanged += (s, e) => UpdatePreview();
            typeBox.SelectedIndexChanged += (s, e) => UpdatePreview();
            previewTimer.Tick += (s, e) =>
            {
                UpdatePreview();
                if (Theme.Detect() != Theme.Current) PanelHost.Rebuild(this);
            };
            previewTimer.Start();
            FormClosed += (s, e) => previewTimer.Dispose();

            body.Controls.Add(scroll);
            body.Controls.Add(footer);
            Controls.Add(body);
            Controls.Add(header);
            Controls.Add(new ResizeStrip(ResizeStrip.Edge.Bottom));
            Controls.Add(new ResizeStrip(ResizeStrip.Edge.Right));
            Controls.Add(new ResizeStrip(ResizeStrip.Edge.Left));
            Controls.Add(new ResizeStrip(ResizeStrip.Edge.Top));
            Resize += (s, e) => { if (usingFallbackRegion) ApplyFallbackRegion(); };
            FormClosing += (s, e) => SaveSettings();
            hotkeyFilter = new HotkeyFilter(() => Hotkey.Parse(settings.HotkeyKeys), () => BeginInvoke(new Action(DoExport)));
            Application.AddMessageFilter(hotkeyFilter);
            FormClosed += (s, e) => Application.RemoveMessageFilter(hotkeyFilter);
            UpdatePreview();
        }

        private static Label MakeLabel(string text) => new Label
        {
            Text = text, AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Body, Margin = new Padding(3, 6, 3, 0)
        };

        private Image LoadLogo()
        {
            try
            {
                using (var s = typeof(ExportForm).Assembly.GetManifestResourceStream("FlattenSave.icon.png"))
                {
                    if (s == null) return null;
                    using (var src = new Bitmap(s))
                    {
                        int px = (int)Math.Round(16 * DeviceDpi / 96.0);
                        var bmp = new Bitmap(px, px);
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            g.DrawImage(src, 0, 0, px, px);
                        }
                        return bmp;
                    }
                }
            }
            catch { return null; }
        }

        internal void Persist() => SaveSettings();

        internal static UpdateInfo LastUpdate;
        private static bool checkedThisSession;

        private Image LoadResIcon(string resource)
        {
            try
            {
                using (var s = typeof(ExportForm).Assembly.GetManifestResourceStream(resource))
                {
                    if (s == null) return null;
                    using (var src = new Bitmap(s))
                    {
                        int px = (int)Math.Round(16 * DeviceDpi / 96.0);
                        var bmp = new Bitmap(px, px);
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            g.DrawImage(src, 0, 0, px, px);
                        }
                        return bmp;
                    }
                }
            }
            catch { return null; }
        }

        private void OpenSettings()
        {
            if (settingsForm != null && !settingsForm.IsDisposed) { settingsForm.Activate(); return; }
            settingsForm = new SettingsForm(this, settings, allChoices, UpdatePreview, ApplySettingsToControls);
            settingsForm.Show(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            BeginInvoke(new Action(StartupUpdateLogic));
        }

        // Asks once whether to enable update checks, then checks at most once per Paint.NET session.
        private void StartupUpdateLogic()
        {
            if (IsDisposed) return;
            if (!settings.UpdatePromptShown)
            {
                settings.UpdatePromptShown = true;
                var r = MessageBox.Show(this,
                    "Check for FlattenSave updates?\n\nWhen the panel opens, FlattenSave can ask GitHub whether a newer release exists. " +
                    "Nothing is downloaded or installed, and no personal data is sent. You can change this any time in Settings.",
                    "FlattenSave", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                settings.CheckForUpdates = r == DialogResult.Yes;
                settings.Save();
            }
            if (settings.CheckForUpdates && !checkedThisSession) RunUpdateCheck();
        }

        private async void RunUpdateCheck()
        {
            checkedThisSession = true;
            var info = await UpdateCheck.CheckAsync();
            if (info != null) LastUpdate = info;
            if (!IsDisposed) UpdatePreview();
        }

        // Sets every control from the current settings (used after "Reset defaults").
        private void ApplySettingsToControls()
        {
            loading = true;
            nameBox.Text = settings.FileName;
            int idx = allChoices.FindIndex(c => c.Extension == settings.Extension);
            if (idx < 0) idx = allChoices.FindIndex(c => c.Extension == ".png");
            if (idx >= 0) typeBox.SelectedIndex = idx;
            cwBtn.Checked = settings.Rotate == "cw"; ccwBtn.Checked = settings.Rotate == "ccw"; r180Btn.Checked = settings.Rotate == "180";
            mhBtn.Checked = settings.MirrorH; mvBtn.Checked = settings.MirrorV;
            omitBtn.Checked = settings.OmitLayers; omitBox.Text = settings.OmitNames;
            noWarnBox.Checked = settings.SuppressOverwriteWarning;
            numberBox.Checked = settings.NumberExports;
            UpdateOmitState();
            UpdatePreview();
        }

        // The selected type first, then any extra formats when multi-export is on.
        private List<FileTypeChoice> ExportChoices(FileTypeChoice primary)
        {
            var list = new List<FileTypeChoice> { primary };
            if (settings.MultiExport)
                foreach (var ext in settings.ExtraExtensions)
                {
                    var c = allChoices.Find(x => x.Extension == ext);
                    if (c != null && !list.Contains(c)) list.Add(c);
                }
            return list;
        }

        private static string StripExtension(string template, List<FileTypeChoice> exts)
        {
            foreach (var c in exts)
                if (template.EndsWith(c.Extension, StringComparison.OrdinalIgnoreCase))
                    return template.Substring(0, template.Length - c.Extension.Length);
            return template;
        }

        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Theme.Header };
            var title = new Label
            {
                Text = "FlattenSave", ForeColor = Theme.Text, BackColor = Theme.Header, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0)
            };
            var close = new DarkButton
            {
                Dock = DockStyle.Right, Width = 18, DrawCross = true, BackNormal = Theme.Close,
                BackHover = Color.FromArgb(0xE0, 0x60, 0x60), Margin = Padding.Empty
            };
            close.Click += (s, e) => Close();
            var closeHost = new Panel { Dock = DockStyle.Right, Width = 28, BackColor = Theme.Header, Padding = new Padding(2, 4, 8, 4) };
            close.Dock = DockStyle.Fill;
            closeHost.Controls.Add(close);
            MouseEventHandler drag = (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            };
            title.MouseDown += drag; header.MouseDown += drag;
            var ver = typeof(ExportForm).Assembly.GetName().Version;
            var version = new Label
            {
                Text = "v" + ver.Major + "." + ver.Minor + "." + ver.Build, ForeColor = Theme.Muted, BackColor = Theme.Header,
                Dock = DockStyle.Right, AutoSize = false, Width = 48, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand
            };
            version.MouseEnter += (s, e) => { version.BackColor = Theme.ButtonHover; version.ForeColor = Theme.Text; };
            version.MouseLeave += (s, e) => { version.BackColor = Theme.Header; version.ForeColor = Theme.Muted; };
            version.Click += (s, e) => UpdateCheck.OpenPage("https://github.com/donajello/FlattenSave");
            tips.SetToolTip(version, "Open the FlattenSave GitHub page");
            header.Controls.Add(title);
            header.Controls.Add(version);
            var cogImg = LoadResIcon("FlattenSave.settings.png");
            var cog = new IconButton { Dock = DockStyle.Right, Width = 24, Icon = cogImg };
            tips.SetToolTip(cog, "Settings");
            cog.Click += (s, e) => OpenSettings();
            header.Controls.Add(cog);
            header.Controls.Add(closeHost);
            var logo = LoadLogo();
            if (logo != null)
            {
                var pic = new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.CenterImage, Dock = DockStyle.Left, Width = logo.Width + 12, BackColor = Theme.Header, Padding = new Padding(6, 0, 0, 0) };
                pic.MouseDown += drag;
                header.Controls.Add(pic);
            }
            return header;
        }

        private void UpdatePreview()
        {
            if (updateLabel != null)
            {
                bool newer = LastUpdate != null && LastUpdate.IsNewer;
                updateLabel.Visible = newer;
                if (newer) updateLabel.Text = "Update available: v" + LastUpdate.Latest + " (click to open)";
            }
            if (!(typeBox.SelectedItem is FileTypeChoice choice)) { preview.Text = ""; return; }
            var exts = ExportChoices(choice);
            string template = StripExtension(nameBox.Text.Trim(), exts);
            if (template.Length == 0) template = "{name}";
            string docName = PdnAccess.GetDocumentBaseName(PdnAccess.GetActiveWorkspace(main));
            preview.ForeColor = Theme.Muted;
            if (NameTemplate.UsesName(template) && string.IsNullOrEmpty(docName)) { preview.Text = "Type a file name above to export"; return; }
            string dir = pathBox.Text.Trim().Trim('"');
            string name = NameTemplate.Resolve(dir, template, docName, exts.Select(c => c.Extension).ToList(), settings.NumberExports, DateTime.Now);
            preview.Text = string.Join(", ", exts.Select(c => settings.ShowFullPath && dir.Length > 0 ? System.IO.Path.Combine(dir, name + c.Extension) : name + c.Extension));
        }

        private void UpdateOmitState()
        {
            omitBox.Inner.ReadOnly = !omitBtn.Checked;
            omitBox.Inner.ForeColor = omitBtn.Checked ? Theme.Text : Theme.Muted;
            omitBox.Inner.Text = omitBox.Inner.Text;
        }

        private void SaveSettings()
        {
            if (cwBtn == null || loading) return;
            settings.Rotate = cwBtn.Checked ? "cw" : ccwBtn.Checked ? "ccw" : r180Btn.Checked ? "180" : "none";
            settings.MirrorH = mhBtn.Checked; settings.MirrorV = mvBtn.Checked;
            settings.OmitLayers = omitBtn.Checked; settings.OmitNames = omitBox.Text.Trim();
            settings.OutputDirectory = pathBox.Text.Trim().Trim('"');
            settings.FileName = nameBox.Text.Trim();
            settings.SuppressOverwriteWarning = noWarnBox.Checked;
            settings.NumberExports = numberBox.Checked;
            if (typeBox.SelectedItem is FileTypeChoice c) settings.Extension = c.Extension;
            if (IsHandleCreated && WindowState == FormWindowState.Normal)
            {
                settings.PanelX = Left; settings.PanelY = Top; settings.PanelWidth = Width; settings.PanelHeight = Height;
            }
            settings.Save();
        }

        private void Browse()
        {
            using var dlg = new FolderBrowserDialog { Description = "Output Directory", UseDescriptionForTitle = true };
            if (Directory.Exists(pathBox.Text.Trim().Trim('"'))) dlg.SelectedPath = pathBox.Text.Trim().Trim('"');
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                pathBox.Text = dlg.SelectedPath;
                SaveSettings();
            }
        }

        private void Report(string msg, bool error)
        {
            status.ForeColor = error ? Theme.Error : Theme.Muted;
            status.Text = msg;
        }

        private void DoExport()
        {
            if (exporting) return;
            exporting = true;
            try { ExportCore(); }
            finally { exporting = false; }
            UpdatePreview();
        }

        private void ExportCore()
        {
            SaveSettings();
            try
            {
                string dir = settings.OutputDirectory;
                if (dir.Length == 0) { Report("Set an output directory first.", true); return; }
                if (!(typeBox.SelectedItem is FileTypeChoice choice)) { Report("No file type selected.", true); return; }

                var ws = PdnAccess.GetActiveWorkspace(main);
                var doc = PdnAccess.GetDocument(ws);
                if (doc == null) { Report("No image is open.", true); return; }

                var exts = ExportChoices(choice);
                string template = StripExtension(settings.FileName.Trim(), exts);
                if (template.Length == 0) template = "{name}";
                string docName = PdnAccess.GetDocumentBaseName(ws);
                if (NameTemplate.UsesName(template) && string.IsNullOrEmpty(docName))
                {
                    Report("Save this file first, or enter a file name.", true);
                    return;
                }
                string baseName = NameTemplate.Resolve(dir, template, docName, exts.Select(c => c.Extension).ToList(), settings.NumberExports, DateTime.Now).Trim();
                if (baseName.Length == 0 || baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { Report("File name has invalid characters.", true); return; }

                Directory.CreateDirectory(dir);
                var targets = exts.Select(c => Path.Combine(dir, baseName + c.Extension)).ToList();
                var existing = targets.Where(File.Exists).Select(Path.GetFileName).ToList();
                if (existing.Count > 0 && !settings.SuppressOverwriteWarning)
                {
                    var r = MessageBox.Show(this, string.Join(", ", existing) + (existing.Count == 1 ? " already exists. Overwrite it?" : " already exist. Overwrite them?"),
                        "FlattenSave", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes) { Report("Cancelled.", false); return; }
                }

                string[] omit = settings.OmitLayers
                    ? settings.OmitNames.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : null;
                using (var outDoc = ImageOps.BuildExportDocument(doc, settings.Rotate, settings.MirrorH, settings.MirrorV, omit))
                {
                    for (int i = 0; i < exts.Count; i++)
                    {
                        FileType ft = exts[i].Info.GetInstance();
                        SaveConfigToken token = ft.GetLastSaveConfigToken() ?? ft.CreateDefaultSaveConfigToken();
                        string temp = targets[i] + ".tmp";
                        using (var scratch = new Surface(outDoc.Width, outDoc.Height))
                        using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write))
                            ft.Save(outDoc, fs, token, scratch, null, false);
                        File.Move(temp, targets[i], true);
                    }
                }
                string saved = targets.Count == 1 ? Path.GetFileName(targets[0]) : baseName + " (" + string.Join(", ", exts.Select(c => c.Extension)) + ")";
                Report("Saved " + saved + "  " + DateTime.Now.ToString("HH:mm:ss"), false);
            }
            catch (Exception ex)
            {
                Report("Export failed: " + ex.Message, true);
            }
        }
    }
}
