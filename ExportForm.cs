using System;
using System.Drawing;
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
        private readonly DarkTextBox omitBox = new DarkTextBox();
        private readonly IconToggle cwBtn, ccwBtn, r180Btn, mhBtn, mvBtn, omitBtn;
        private readonly ToolTip tips = DarkTips.Create();
        private readonly Label preview = new Label();
        private readonly System.Windows.Forms.Timer previewTimer = new System.Windows.Forms.Timer { Interval = 700 };
        private readonly Label status = new Label();

        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int round = 2, border = Theme.Border.R | (Theme.Border.G << 8) | (Theme.Border.B << 16);
            int hr = DwmSetWindowAttribute(Handle, 33, ref round, 4);
            if (hr == 0) DwmSetWindowAttribute(Handle, 34, ref border, 4);
            else ApplyFallbackRegion();
        }

        // Windows 10 has no native rounded windows, so clip a region instead.
        private void ApplyFallbackRegion()
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = 12, w = Width, h = Height;
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
            ClientSize = new Size(290, 450);
            var wa = Screen.FromControl(main).WorkingArea;
            Location = new Point(wa.Right - Width - 20, wa.Top + 120);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Body };
            var header = BuildHeader();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10, 8, 10, 10), BackColor = Theme.Body
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));

            layout.Controls.Add(MakeLabel("Output Directory"), 0, 0); layout.SetColumnSpan(layout.GetControlFromPosition(0, 0), 2);

            pathBox.Dock = DockStyle.Fill; pathBox.Text = settings.OutputDirectory;
            pathBox.Inner.Leave += (s, e) => SaveSettings();
            var browse = new DarkButton { Text = "...", Dock = DockStyle.Fill, Margin = new Padding(4, 3, 3, 3) };
            browse.Click += (s, e) => Browse();
            layout.Controls.Add(pathBox, 0, 1); layout.Controls.Add(browse, 1, 1);

            var nameLabel = MakeLabel("Export file name (optional)");
            layout.Controls.Add(nameLabel, 0, 2); layout.SetColumnSpan(nameLabel, 2);
            nameBox.Dock = DockStyle.Fill; nameBox.Text = settings.FileName;
            nameBox.Inner.Leave += (s, e) => SaveSettings();
            layout.Controls.Add(nameBox, 0, 3); layout.SetColumnSpan(nameBox, 2);

            var typeLabel = MakeLabel("File type");
            layout.Controls.Add(typeLabel, 0, 4); layout.SetColumnSpan(typeLabel, 2);
            typeBox.Dock = DockStyle.Fill;
            var choices = PdnAccess.GetSaveTypes(PdnAccess.GetFileTypesService(main));
            foreach (var c in choices) typeBox.Items.Add(c);
            int idx = choices.FindIndex(c => c.Extension == settings.Extension);
            if (idx < 0) idx = choices.FindIndex(c => c.Extension == ".png");
            if (idx < 0 && choices.Count > 0) idx = 0;
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

            var export = new DarkButton
            {
                Text = "Flatten & Save", Dock = DockStyle.Fill, Height = 32, Margin = new Padding(3, 8, 3, 3),
                BackNormal = Theme.Accent, BackHover = Theme.AccentHover, ForeColor = Color.White
            };
            export.Click += (s, e) => DoExport();
            layout.Controls.Add(export, 0, 11); layout.SetColumnSpan(export, 2);

            status.AutoSize = true; status.MaximumSize = new Size(260, 0); status.ForeColor = Theme.Muted;
            status.BackColor = Theme.Body; status.Margin = new Padding(3, 6, 3, 0);
            preview.AutoSize = true; preview.MaximumSize = new Size(260, 0); preview.ForeColor = Theme.Muted; preview.BackColor = Theme.Body; preview.Margin = new Padding(3, 4, 3, 0);
            layout.Controls.Add(preview, 0, 12); layout.SetColumnSpan(preview, 2);
            layout.Controls.Add(status, 0, 13); layout.SetColumnSpan(status, 2);

            tips.SetToolTip(pathBox, "Folder the exported file is saved to. Paste a path here."); tips.SetToolTip(pathBox.Inner, tips.GetToolTip(pathBox));
            tips.SetToolTip(browse, "Browse for a folder");
            tips.SetToolTip(nameBox, "Name for the exported file, without extension. Leave empty to use the open image's file name."); tips.SetToolTip(nameBox.Inner, tips.GetToolTip(nameBox));
            tips.SetToolTip(typeBox, "File format to export as");
            tips.SetToolTip(noWarnBox, "Overwrite existing files without asking");
            tips.SetToolTip(export, "Flatten the image and save it to the output directory");
            tips.SetToolTip(omitBox, "Layer names to omit, comma-separated (only used when the omit-layers button is on)"); tips.SetToolTip(omitBox.Inner, tips.GetToolTip(omitBox));
            pathBox.Inner.TextChanged += (s, e) => UpdatePreview();
            nameBox.Inner.TextChanged += (s, e) => UpdatePreview();
            typeBox.SelectedIndexChanged += (s, e) => UpdatePreview();
            previewTimer.Tick += (s, e) => UpdatePreview();
            previewTimer.Start();
            FormClosed += (s, e) => previewTimer.Dispose();
            UpdatePreview();
            body.Controls.Add(layout);
            Controls.Add(body);
            Controls.Add(header);
            FormClosing += (s, e) => SaveSettings();
        }

        private static Label MakeLabel(string text) => new Label
        {
            Text = text, AutoSize = true, ForeColor = Theme.Text, BackColor = Theme.Body, Margin = new Padding(3, 6, 3, 0)
        };

        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Theme.Header };
            var title = new Label
            {
                Text = "FlattenSave", ForeColor = Theme.Text, BackColor = Theme.Header, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0)
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
                Dock = DockStyle.Right, AutoSize = false, Width = 48, TextAlign = ContentAlignment.MiddleRight
            };
            version.MouseDown += drag;
            header.Controls.Add(title);
            header.Controls.Add(version);
            header.Controls.Add(closeHost);
            return header;
        }

        private void UpdatePreview()
        {
            if (!(typeBox.SelectedItem is FileTypeChoice choice)) { preview.Text = ""; return; }
            string name = nameBox.Text.Trim();
            if (name.Length == 0) name = PdnAccess.GetDocumentBaseName(PdnAccess.GetActiveWorkspace(main));
            if (string.IsNullOrEmpty(name)) { preview.ForeColor = Theme.Muted; preview.Text = "Type a file name above to export"; return; }
            if (name.EndsWith(choice.Extension, StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - choice.Extension.Length);
            preview.ForeColor = Theme.Muted;
            preview.Text = name + choice.Extension;
        }

        private void UpdateOmitState()
        {
            omitBox.Inner.ReadOnly = !omitBtn.Checked;
            omitBox.Inner.ForeColor = omitBtn.Checked ? Color.White : Theme.Muted;
            omitBox.Inner.Text = omitBox.Inner.Text;
        }

        private void SaveSettings()
        {
            if (cwBtn == null) return;
            settings.Rotate = cwBtn.Checked ? "cw" : ccwBtn.Checked ? "ccw" : r180Btn.Checked ? "180" : "none";
            settings.MirrorH = mhBtn.Checked; settings.MirrorV = mvBtn.Checked;
            settings.OmitLayers = omitBtn.Checked; settings.OmitNames = omitBox.Text.Trim();
            settings.OutputDirectory = pathBox.Text.Trim().Trim('"');
            settings.FileName = nameBox.Text.Trim();
            settings.SuppressOverwriteWarning = noWarnBox.Checked;
            if (typeBox.SelectedItem is FileTypeChoice c) settings.Extension = c.Extension;
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
            SaveSettings();
            try
            {
                string dir = settings.OutputDirectory;
                if (dir.Length == 0) { Report("Set an output directory first.", true); return; }
                if (!(typeBox.SelectedItem is FileTypeChoice choice)) { Report("No file type selected.", true); return; }

                var ws = PdnAccess.GetActiveWorkspace(main);
                var doc = PdnAccess.GetDocument(ws);
                if (doc == null) { Report("No image is open.", true); return; }

                string baseName = settings.FileName;
                if (baseName.Length == 0) baseName = PdnAccess.GetDocumentBaseName(ws);
                if (string.IsNullOrEmpty(baseName))
                {
                    Report("Save this file first(or enter a file name).", true);
                    MessageBox.Show(this, "This image hasn't been saved yet, so there is no file name to export with.\r\nSave it first, or type a file name in the panel.",
                        "FlattenSave", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                baseName = baseName.Trim();
                if (baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { Report("File name has invalid characters.", true); return; }
                if (baseName.EndsWith(choice.Extension, StringComparison.OrdinalIgnoreCase))
                    baseName = baseName.Substring(0, baseName.Length - choice.Extension.Length);

                Directory.CreateDirectory(dir);
                string target = Path.Combine(dir, baseName + choice.Extension);

                if (File.Exists(target) && !settings.SuppressOverwriteWarning)
                {
                    var r = MessageBox.Show(this, Path.GetFileName(target) + " already exists. Overwrite it?",
                        "FlattenSave", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes) { Report("Cancelled.", false); return; }
                }

                FileType ft = choice.Info.GetInstance();
                SaveConfigToken token = ft.GetLastSaveConfigToken() ?? ft.CreateDefaultSaveConfigToken();
                string temp = target + ".tmp";
                string[] omit = settings.OmitLayers
                    ? settings.OmitNames.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : null;
                using (var outDoc = ImageOps.BuildExportDocument(doc, settings.Rotate, settings.MirrorH, settings.MirrorV, omit))
                using (var scratch = new Surface(outDoc.Width, outDoc.Height))
                using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write))
                    ft.Save(outDoc, fs, token, scratch, null, false);
                File.Move(temp, target, true);
                Report("Saved " + Path.GetFileName(target) + "  " + DateTime.Now.ToString("HH:mm:ss"), false);
            }
            catch (Exception ex)
            {
                Report("Export failed: " + ex.Message, true);
            }
        }
    }
}














