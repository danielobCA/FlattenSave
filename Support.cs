using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using PaintDotNet;

namespace FlattenSave
{
    internal sealed class Settings
    {
        public string OutputDirectory { get; set; } = "";
        public string Extension { get; set; } = ".png";
        public string FileName { get; set; } = "";
        public string Rotate { get; set; } = "none";
        public bool MirrorH { get; set; }
        public bool MirrorV { get; set; }
        public bool OmitLayers { get; set; }
        public string OmitNames { get; set; } = "";
        public bool SuppressOverwriteWarning { get; set; }
        public bool NumberExports { get; set; }
        public string[] HotkeyKeys { get; set; } = new string[0];
        public bool MultiExport { get; set; }
        public bool ShowFullPath { get; set; }
        public string[] ExtraExtensions { get; set; } = new string[0];
        public int PanelX { get; set; } = int.MinValue;
        public int PanelY { get; set; } = int.MinValue;
        public int PanelWidth { get; set; }
        public int PanelHeight { get; set; }
        public bool UpdatePromptShown { get; set; }
        public bool CheckForUpdates { get; set; }
        public string LastUpdateCheck { get; set; } = "";

        // Restores every setting except the output directory and the first-run update prompt state.
        public void ResetToDefaults()
        {
            var d = new Settings { OutputDirectory = OutputDirectory, UpdatePromptShown = UpdatePromptShown };
            foreach (var p in typeof(Settings).GetProperties())
                p.SetValue(this, p.GetValue(d));
        }

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlattenSave", "settings.json");

        public static Settings Load()
        {
            try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings(); }
            catch { return new Settings(); }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch { }
        }
    }

    internal sealed class FileTypeChoice
    {
        public IFileTypeInfo Info;
        public string Extension;
        public override string ToString() => Info.Name + " (" + Extension + ")";
    }

    // Paint.NET has no public API for the open document, so reach it through the running app via reflection.
    internal static class PdnAccess
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static Control FindByTypeName(Control root, string name)
        {
            if (root.GetType().Name == name) return root;
            foreach (Control c in root.Controls)
            {
                var r = FindByTypeName(c, name);
                if (r != null) return r;
            }
            return null;
        }

        public static object GetAppWorkspace(Form main)
        {
            return FindByTypeName(main, "AppWorkspace");
        }

        public static object GetActiveWorkspace(Form main)
        {
            var app = GetAppWorkspace(main);
            return app?.GetType().GetProperty("ActiveDocumentWorkspace", All)?.GetValue(app);
        }

        public static Document GetDocument(object workspace)
        {
            return workspace?.GetType().GetProperty("Document", All)?.GetValue(workspace) as Document;
        }

        public static string GetDocumentBaseName(object workspace)
        {
            try
            {
                var m = workspace.GetType().GetMethod("GetDocumentSaveOptions", All);
                var args = new object[] { null, null, null };
                m.Invoke(workspace, args);
                if (args[0] is string path && path.Length > 0)
                    return Path.GetFileNameWithoutExtension(path);
            }
            catch { }
            return null;
        }

        public static IFileTypesService GetFileTypesService(Form main)
        {
            try
            {
                Type t = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = a.GetType("PaintDotNet.Data.FileTypesCollection");
                    if (t != null) break;
                }
                Log("type=" + (t == null ? "null" : t.Assembly.FullName));
                Log("inst=" + t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null));
                var r = t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as IFileTypesService;
                Log("FileTypesCollection=" + (r == null ? "null" : "count=" + r.FileTypes.Count));
                return r;
            }
            catch (Exception ex) { Log("ex " + ex); return null; }
        }

        public static void Log(string s)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "FlattenSave.log"), s + "\r\n"); } catch { }
        }

        public static List<FileTypeChoice> GetSaveTypes(IFileTypesService svc)
        {
            var list = new List<FileTypeChoice>();
            if (svc == null) return list;
            foreach (var info in svc.FileTypes)
            {
                var o = info.Options;
                if (o == null || !o.SupportsSaving || o.SupportsLayers) continue;
                string ext = o.DefaultSaveExtension;
                if (string.IsNullOrEmpty(ext) && o.SaveExtensions.Count > 0) ext = o.SaveExtensions[0];
                if (string.IsNullOrEmpty(ext)) continue;
                if (!ext.StartsWith(".")) ext = "." + ext;
                list.Add(new FileTypeChoice { Info = info, Extension = ext.ToLowerInvariant() });
            }
            list.Sort((a, b) => string.Compare(a.Info.Name, b.Info.Name, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }
    }

    internal static class ImageOps
    {
        // Flattens the visible layers (minus any whose name matches omitTokens) and applies rotate/mirror.
        public static Document BuildExportDocument(Document doc, string rotate, bool mirrorH, bool mirrorV, string[] omitTokens)
        {
            var hiddenByUs = new List<Layer>();
            try
            {
                if (omitTokens != null && omitTokens.Length > 0)
                {
                    for (int i = 0; i < doc.Layers.Count; i++)
                    {
                        Layer l = doc.Layers[i];
                        if (!l.Visible) continue;
                        string name = l.Name ?? "";
                        foreach (var tok in omitTokens)
                            if (name.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0) { l.Visible = false; hiddenByUs.Add(l); break; }
                    }
                }
                using (var flat = new Surface(doc.Width, doc.Height))
                {
                    flat.Fill(new PaintDotNet.Rendering.RectInt32(0, 0, flat.Width, flat.Height), ColorBgra.Transparent);
                    doc.Flatten(flat);
                    using (var result = Transform(flat, rotate, mirrorH, mirrorV))
                    using (var bmp = result.CreateAliasedBitmap())
                        return Document.FromImage(bmp);
                }
            }
            finally
            {
                foreach (var l in hiddenByUs) l.Visible = true;
            }
        }

        private static Surface Transform(Surface src, string rotate, bool mirrorH, bool mirrorV)
        {
            int w = src.Width, h = src.Height;
            bool swap = rotate == "cw" || rotate == "ccw";
            int ow = swap ? h : w, oh = swap ? w : h;
            var dst = new Surface(ow, oh);
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    int sx = mirrorH ? ow - 1 - x : x;
                    int sy = mirrorV ? oh - 1 - y : y;
                    int px, py;
                    if (rotate == "cw") { px = sy; py = h - 1 - sx; }
                    else if (rotate == "ccw") { px = w - 1 - sy; py = sx; }
                    else if (rotate == "180") { px = w - 1 - sx; py = h - 1 - sy; }
                    else { px = sx; py = sy; }
                    dst[x, y] = src[px, py];
                }
            return dst;
        }
    }
}



