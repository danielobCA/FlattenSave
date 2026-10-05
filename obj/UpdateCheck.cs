using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace FlattenSave
{
    internal sealed class UpdateInfo
    {
        public Version Latest;
        public string Url;
        public bool IsNewer;
    }

    // Opt-in check against the latest GitHub release. Only reads release metadata; never downloads or installs anything.
    internal static class UpdateCheck
    {
        private const string Api = "https://api.github.com/repos/donajello/FlattenSave/releases/latest";
        private const string ReleasesPage = "https://github.com/donajello/FlattenSave/releases";
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("FlattenSave-UpdateCheck");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        public static Version Current
        {
            get { var v = typeof(UpdateCheck).Assembly.GetName().Version; return new Version(v.Major, v.Minor, Math.Max(v.Build, 0)); }
        }

        // Returns null when the check fails (offline, rate limited, bad data).
        public static async Task<UpdateInfo> CheckAsync()
        {
            try
            {
                string json = await Http.GetStringAsync(Api).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                string tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
                if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
                string url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
                if (!IsSafeUrl(url)) url = ReleasesPage;
                latest = new Version(latest.Major, latest.Minor, Math.Max(latest.Build, 0));
                return new UpdateInfo { Latest = latest, Url = url, IsNewer = latest > Current };
            }
            catch { return null; }
        }

        private static bool IsSafeUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == "github.com";

        public static void OpenReleasePage(string url)
        {
            OpenPage(IsSafeUrl(url) ? url : ReleasesPage);
        }

        public static void OpenPage(string url)
        {
            try { if (IsSafeUrl(url)) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }
    }
}
