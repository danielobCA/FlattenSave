using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FlattenSave
{
    // Expands file name templates: {name}, {date}, {time}, and {n} / {n:3} for a zero-padded counter.
    internal static class NameTemplate
    {
        private static readonly Regex Number = new Regex(@"\{n(?::(\d{1,2}))?\}", RegexOptions.IgnoreCase);

        public static bool UsesName(string template) => template.IndexOf("{name}", StringComparison.OrdinalIgnoreCase) >= 0;

        private static string ExpandFixed(string t, string docName, DateTime now)
        {
            t = Regex.Replace(t, @"\{name\}", m => docName ?? "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\{date\}", m => now.ToString("yyyy-MM-dd"), RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\{time\}", m => now.ToString("HH-mm-ss"), RegexOptions.IgnoreCase);
            return t;
        }

        private static IEnumerable<string> FileNames(string dir)
        {
            try { return Directory.Exists(dir) ? Directory.EnumerateFiles(dir).Select(Path.GetFileName).ToList() : new List<string>(); }
            catch { return new List<string>(); }
        }

        // Returns the final file name (no extension). The number is chosen so it is free for every extension in exts.
        public static string Resolve(string dir, string template, string docName, IList<string> exts, bool numberExports, DateTime now)
        {
            string pre = ExpandFixed(template, docName, now);
            string extAlt = "(?:" + string.Join("|", exts.Select(Regex.Escape)) + ")$";
            var names = FileNames(dir);

            if (Number.IsMatch(pre))
            {
                var sb = new StringBuilder("^");
                int pos = 0;
                bool first = true;
                foreach (Match m in Number.Matches(pre))
                {
                    sb.Append(Regex.Escape(pre.Substring(pos, m.Index - pos)));
                    sb.Append(first ? "(?<n>\\d+)" : "\\k<n>");
                    first = false;
                    pos = m.Index + m.Length;
                }
                sb.Append(Regex.Escape(pre.Substring(pos))).Append(extAlt);
                var rx = new Regex(sb.ToString(), RegexOptions.IgnoreCase);
                int max = 0;
                foreach (var f in names)
                {
                    var m = rx.Match(f);
                    if (m.Success && int.TryParse(m.Groups["n"].Value, out int v) && v > max) max = v;
                }
                int next = max + 1;
                return Number.Replace(pre, m => next.ToString("D" + (m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 1)));
            }

            if (!numberExports) return pre;

            var suffix = new Regex("^" + Regex.Escape(pre) + "_(\\d+)" + extAlt, RegexOptions.IgnoreCase);
            int top = 0;
            foreach (var f in names)
            {
                var m = suffix.Match(f);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int v) && v > top) top = v;
            }
            bool plainExists = exts.Any(e => names.Contains(pre + e, StringComparer.OrdinalIgnoreCase));
            return top == 0 && !plainExists ? pre : pre + "_" + (top + 1);
        }
    }
}
