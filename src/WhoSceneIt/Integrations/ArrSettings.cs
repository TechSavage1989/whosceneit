using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WhoSceneIt.Integrations
{
    public sealed class ArrConnection
    {
        public bool Enabled { get; set; }
        public string BaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string EmbyPathRoot { get; set; } = "";
        public string ArrPathRoot { get; set; } = "";
        public bool AddImportExclusion { get; set; } = true;
        public bool RemoveEmptySeries { get; set; }
        public ArrConnection Copy() => (ArrConnection)MemberwiseClone();
    }
    public sealed class ArrSettings
    {
        public ArrConnection Sonarr { get; set; } = new ArrConnection();
        public ArrConnection Radarr { get; set; } = new ArrConnection();
        public ArrSettings Copy() => new ArrSettings { Sonarr = (Sonarr ?? new ArrConnection()).Copy(), Radarr = (Radarr ?? new ArrConnection()).Copy() };
        public string Key()
        {
            using var sha = SHA256.Create();
            string Part(ArrConnection c) => string.Join("\n", c.Enabled, c.BaseUrl, c.ApiKey, c.EmbyPathRoot, c.ArrPathRoot, c.AddImportExclusion, c.RemoveEmptySeries);
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(Part(Sonarr) + "\n---\n" + Part(Radarr))));
        }
    }
    // API keys remain in Emby's server configuration. This public view never contains them.
    public sealed class ArrConnectionView
    {
        public bool Enabled { get; set; }
        public string BaseUrl { get; set; }
        public bool HasApiKey { get; set; }
        public string EmbyPathRoot { get; set; }
        public string ArrPathRoot { get; set; }
        public bool AddImportExclusion { get; set; }
        public bool RemoveEmptySeries { get; set; }
        public static ArrConnectionView From(ArrConnection c) => new ArrConnectionView { Enabled = c.Enabled, BaseUrl = c.BaseUrl,
            HasApiKey = !string.IsNullOrEmpty(c.ApiKey), EmbyPathRoot = c.EmbyPathRoot, ArrPathRoot = c.ArrPathRoot, AddImportExclusion = c.AddImportExclusion, RemoveEmptySeries = c.RemoveEmptySeries };
    }
    public sealed class ArrSettingsView
    {
        public ArrConnectionView Sonarr { get; set; }
        public ArrConnectionView Radarr { get; set; }
        public static ArrSettingsView From(ArrSettings s) => new ArrSettingsView { Sonarr = ArrConnectionView.From(s.Sonarr), Radarr = ArrConnectionView.From(s.Radarr) };
    }
    public sealed class ArrConnectionUpdate
    {
        public bool Enabled { get; set; }
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
        public bool ClearApiKey { get; set; }
        public string EmbyPathRoot { get; set; }
        public string ArrPathRoot { get; set; }
        public bool AddImportExclusion { get; set; } = true;
        public bool RemoveEmptySeries { get; set; }
        public ArrConnection Merge(ArrConnection saved)
        {
            var c = new ArrConnection { Enabled = Enabled, BaseUrl = (BaseUrl ?? "").Trim(),
                ApiKey = ClearApiKey ? "" : string.IsNullOrWhiteSpace(ApiKey) ? saved.ApiKey ?? "" : ApiKey.Trim(),
                EmbyPathRoot = (EmbyPathRoot ?? "").Trim(), ArrPathRoot = (ArrPathRoot ?? "").Trim(), AddImportExclusion = AddImportExclusion, RemoveEmptySeries = RemoveEmptySeries };
            if (c.BaseUrl.Length > 2048 || c.ApiKey.Length > 256 || c.ApiKey.Any(char.IsControl)) throw new ArgumentException("Check the connection URL and API key.");
            if (c.BaseUrl.Length > 0) c.BaseUrl = ArrClient.ValidateBaseUrl(c.BaseUrl).AbsoluteUri;
            if (c.Enabled && (c.BaseUrl.Length == 0 || c.ApiKey.Length == 0)) throw new ArgumentException("Enabled connections need a server URL and API key.");
            if ((c.EmbyPathRoot.Length == 0) != (c.ArrPathRoot.Length == 0)) throw new ArgumentException("Supply both path prefixes, or leave both blank when paths are identical.");
            if (c.EmbyPathRoot.Length > 0) { ArrPath.Normalize(c.EmbyPathRoot); ArrPath.Normalize(c.ArrPathRoot); }
            return c;
        }
    }
    public static class ArrSettingsStore
    {
        private static readonly object Sync = new object();
        private static int _active;
        public static ArrSettings Read() { lock (Sync) return (Plugin.Instance?.Configuration?.Arr ?? new ArrSettings()).Copy(); }
        public static ArrSettingsView Save(ArrConnectionUpdate sonarr, ArrConnectionUpdate radarr)
        {
            lock (Sync)
            {
                if (_active != 0) throw new InvalidOperationException("Wait for the current deletion to finish before changing synchronization settings.");
                if (sonarr == null || radarr == null) throw new ArgumentException("Both connection settings are required.");
                var plugin = Plugin.Instance ?? throw new InvalidOperationException("The plugin is still starting.");
                var old = plugin.Configuration.Arr;
                var current = Read();
                var next = new ArrSettings { Sonarr = sonarr.Merge(current.Sonarr), Radarr = radarr.Merge(current.Radarr) };
                plugin.Configuration.Arr = next;
                try { plugin.SaveConfiguration(); }
                catch { plugin.Configuration.Arr = old; throw new InvalidOperationException("Emby could not save synchronization settings. Check its configuration directory permissions."); }
                return ArrSettingsView.From(next);
            }
        }
        public static IDisposable BeginOperation(string expected)
        {
            lock (Sync)
            {
                if (Read().Key() != expected) throw new InvalidOperationException("Synchronization settings changed. Review the deletion selection again.");
                ++_active;
                return new ActionLease(() => { lock (Sync) --_active; });
            }
        }
    }
    public sealed class ActionLease : IDisposable
    {
        private Action _release;
        public ActionLease(Action release) { _release = release; }
        public void Dispose() { Interlocked.Exchange(ref _release, null)?.Invoke(); }
    }
    public static class ArrPath
    {
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || path.Any(char.IsControl)) throw new ArgumentException("A media path or path prefix is invalid.");
            var p = path.Replace('\\', '/');
            if (!(p.StartsWith("/", StringComparison.Ordinal) || (p.Length > 2 && char.IsLetter(p[0]) && p[1] == ':' && p[2] == '/')) || p.Contains("://"))
                throw new ArgumentException("Media path prefixes must be absolute filesystem paths.");
            var parts = p.Split('/').Where(s => s.Length > 0).ToArray();
            if (parts.Any(s => s == "." || s == "..")) throw new ArgumentException("Media paths cannot contain dot segments.");
            var prefix = p.StartsWith("//", StringComparison.Ordinal) ? "//" : p.StartsWith("/", StringComparison.Ordinal) ? "/" : "";
            var normalized = prefix + string.Join("/", parts);
            return normalized.Length == 2 && normalized[1] == ':' ? normalized + "/" : normalized;
        }
        private static StringComparison Comparison(string p) => p.StartsWith("//", StringComparison.Ordinal) || (p.Length > 1 && p[1] == ':') ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        public static bool Equal(string one, string two) => string.Equals(Normalize(one), Normalize(two), Comparison(Normalize(one)));
        public static bool Contains(string root, string file)
        {
            var r = Normalize(root).TrimEnd('/'); var f = Normalize(file);
            return f.StartsWith(r + "/", Comparison(r));
        }
        public static string Translate(string file, ArrConnection c)
        {
            var f = Normalize(file);
            if (string.IsNullOrEmpty(c.EmbyPathRoot) && string.IsNullOrEmpty(c.ArrPathRoot)) return f;
            var from = Normalize(c.EmbyPathRoot).TrimEnd('/'); var to = Normalize(c.ArrPathRoot).TrimEnd('/');
            if (!Contains(c.EmbyPathRoot, f)) throw new InvalidOperationException("This media path is outside the configured Emby path prefix.");
            return to + f.Substring(from.Length);
        }
        public static string Combine(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith("/", StringComparison.Ordinal) || relative.StartsWith("\\", StringComparison.Ordinal) || relative.Contains(":"))
                throw new InvalidOperationException("The service returned an invalid relative media path.");
            return Normalize(Normalize(root).TrimEnd('/') + "/" + relative);
        }
    }
}
