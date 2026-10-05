using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.PropertySystem;

[assembly: PluginSupportInfo(typeof(FlattenSave.PluginInfo))]

namespace FlattenSave
{
    public class PluginInfo : IPluginSupportInfo
    {
        public string DisplayName => "FlattenSave";
        public string Author => "Local";
        public string Copyright => "";
        public Version Version => new Version(1, 0, 3, 0);
        public Uri WebsiteUri => new Uri("https://www.getpaint.net");
    }

    // Menu entry (Effects > Tools > FlattenSave Panel) that re-opens the panel; it never changes the image.
    [PluginSupportInfo<PluginInfo>(DisplayName = "FlattenSave Panel")]
    public sealed class FlattenSaveEffect : PropertyBasedEffect
    {
        public FlattenSaveEffect() : base("FlattenSave Panel", LoadIcon(), "Tools", new EffectOptions { Flags = EffectFlags.None }) { }

        private static Image LoadIcon()
        {
            using (var s = typeof(FlattenSaveEffect).Assembly.GetManifestResourceStream("FlattenSave.icon.png"))
                return s == null ? null : new Bitmap(s);
        }

        protected override PropertyCollection OnCreatePropertyCollection() => PropertyCollection.CreateEmpty();

        protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            PanelHost.ShowPanel();
            base.OnSetRenderInfo(newToken, dstArgs, srcArgs);
        }

        protected override void OnRender(System.Drawing.Rectangle[] renderRects, int startIndex, int length)
        {
            for (int i = startIndex; i < startIndex + length; i++)
                DstArgs.Surface.CopySurface(SrcArgs.Surface, renderRects[i]);
        }
    }

    internal static class PanelHost
    {
        private static ExportForm form;
        private static LauncherButton launcher;
        private static int started;

        [ModuleInitializer]
        internal static void Init()
        {
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            var t = new Thread(() =>
            {
                for (int i = 0; i < 240; i++)
                {
                    Thread.Sleep(500);
                    if (ShowPanel()) return;
                }
            }) { IsBackground = true };
            t.Start();
        }

        internal static Form FindMainForm()
        {
            try
            {
                foreach (Form f in Application.OpenForms)
                    if (f.GetType().Name == "MainForm" && f.IsHandleCreated && f.Visible) return f;
            }
            catch { }
            return null;
        }

        // Re-creates the panel in place when Paint.NET's theme changes.
        internal static void Rebuild(ExportForm old)
        {
            Theme.Apply(Theme.Detect());
            var loc = old.Location;
            old.Persist();
            old.Close();
            Form main = FindMainForm();
            if (main == null) return;
            form = new ExportForm(main) { Location = loc };
            form.Show(main);
        }

        internal static bool IsOpen => form != null && !form.IsDisposed && form.Visible;

        internal static void TogglePanel()
        {
            if (form != null && !form.IsDisposed && form.Visible) form.Close();
            else ShowPanel();
        }

        internal static bool ShowPanel()
        {
            Form main = FindMainForm();
            if (main == null) return false;
            main.BeginInvoke(new Action(() =>
            {
                if (launcher == null || launcher.IsDisposed) { launcher = new LauncherButton(main); launcher.Show(main); }
                if (form == null || form.IsDisposed)
                {
                    form = new ExportForm(main);
                    form.Show(main);
                }
                else
                {
                    form.Visible = true;
                    form.Activate();
                }
            }));
            return true;
        }
    }
}






