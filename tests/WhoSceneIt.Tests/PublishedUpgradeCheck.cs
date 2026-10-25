using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using MediaBrowser.Common;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Serialization;
using WhoSceneIt;

// Explicit post-publication check only. Never starts Emby or contacts services
// other than the approved GitHub repository and its asset CDN.
public static class PublishedUpgradeCheck
{
    public static async Task Run(IJsonSerializer json)
    {
        const string previousHash = "e2f2e93ee1aa4e3d656bf373cbdc8b5e5fb7bdbca9a6f30dff9f4eb6995ed024";
        var expected = typeof(Plugin).Assembly.GetName().Version.ToString(3);
        var root = Path.Combine(Path.GetTempPath(), "WhoSceneIt-published-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        object Call(object value, string name, params object[] arguments) => value.GetType().GetMethod(name).Invoke(value, arguments);
        object Status(object manager) => Call(manager, "Status");
        T Get<T>(object value, string property) => (T)value.GetType().GetProperty(property).GetValue(value);
        using var previousContext = new IsolatedPlugin();
        using var nextContext = new IsolatedPlugin();
        object previousManager = null, nextManager = null;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WhoSceneIt-Published-Upgrade-Check");
            var previous = await http.GetByteArrayAsync("https://github.com/TechSavage1989/whosceneit/releases/download/v1.0.0/WhoSceneIt.dll");
            Assert(previous.Length < 16 * 1024 * 1024 && Convert.ToHexString(SHA256.HashData(previous)).ToLowerInvariant() == previousHash, "The published previous DLL failed its pinned digest.");
            var target = Path.Combine(root, "CustomWhoSceneIt.dll"); File.WriteAllBytes(target, previous);
            var configuration = Path.Combine(root, "configuration.xml"); File.WriteAllText(configuration, "preserve existing configuration");
            var data = Path.Combine(root, "data"); Directory.CreateDirectory(data);
            var host = Proxy.Create<IApplicationHost>((m, a) => m.Name switch { "get_IsStartupComplete" => true, "get_ApplicationVersion" => new Version(4, 8, 11, 0), _ => null });
            var library = Proxy.Create<ILibraryManager>((m, a) => null);
            var sessions = Proxy.Create<ISessionManager>((m, a) => m.Name == "get_Sessions" ? Array.Empty<SessionInfo>() : null);
            object Manager(Assembly assembly) => Activator.CreateInstance(assembly.GetType("WhoSceneIt.Updates.UpdateManager"), data, target, json, host, library, sessions, null, null);
            var oldAssembly = previousContext.LoadPlugin(previous);
            previousManager = Manager(oldAssembly);
            Assert(Get<string>(Status(previousManager), "InstalledVersion") == "1.0.0", "The previous updater did not load its original version.");
            await (Task)Call(previousManager, "CheckAsync", "manual");
            var checkedStatus = Status(previousManager);
            Assert(Get<bool>(checkedStatus, "UpdateAvailable") && Get<string>(checkedStatus, "LatestVersion") == expected, "The published feed did not offer the expected release: " + Get<string>(checkedStatus, "Message"));
            Call(previousManager, "BeginDownload");
            var timeout = DateTimeOffset.UtcNow.AddSeconds(90);
            while (Get<bool>(Status(previousManager), "Busy") && DateTimeOffset.UtcNow < timeout) await Task.Delay(200);
            Assert(Get<bool>(Status(previousManager), "Downloaded"), "Real asset download did not verify: " + Get<string>(Status(previousManager), "Message"));
            var result = Call(previousManager, "Install");
            Assert(Get<bool>(result, "RestartRequired"), "The previous updater did not stage/install the new release: " + Get<string>(result, "Message"));
            Assert(Hash(Path.Combine(data, "rollback", "WhoSceneIt.dll")) == previousHash, "Rollback did not retain the previous published DLL.");
            Assert(File.Exists(Path.Combine(data, "pending-install.json")) && File.Exists(Path.Combine(data, "downloaded.dll")), "Installation lost its pending health-verification files.");
            ((IDisposable)previousManager).Dispose(); previousManager = null;
            var newAssembly = nextContext.LoadPlugin(File.ReadAllBytes(target));
            Assert(newAssembly.GetName().Version.ToString(3) == expected, "The installed public DLL has the wrong version.");
            nextManager = Manager(newAssembly);
            Assert(Get<bool>(nextManager, "StartupVerified") && Get<bool>(Status(nextManager), "AwaitingDashboardVerification"), "The new assembly did not verify its pending install.");
            Call(nextManager, "ConfirmDashboardHealthy");
            Assert(!File.Exists(Path.Combine(data, "pending-install.json")) && !File.Exists(Path.Combine(data, "downloaded.dll")), "Confirmed reload did not clean owned staging files.");
            Assert(File.ReadAllText(configuration) == "preserve existing configuration" && Directory.GetFiles(Path.Combine(data, "rollback")).Length == 1, "Configuration or rollback retention changed.");
            Console.WriteLine("PASS Published 1.0.0 to " + expected + ": real HTTPS feed/download, digest, staging, installation, custom filename, backup, settings, isolated assembly reload, and cleanup.");
            Console.WriteLine("Installed SHA-256: " + Hash(target));
            Console.WriteLine("This check uses SDK host fixtures; it does not start or restart a live Emby server.");
        }
        finally
        {
            (previousManager as IDisposable)?.Dispose(); (nextManager as IDisposable)?.Dispose();
            try { Directory.Delete(root, true); } catch { }
        }
    }
    private sealed class IsolatedPlugin : AssemblyLoadContext, IDisposable
    {
        public IsolatedPlugin() : base(isCollectible: true) { }
        public Assembly LoadPlugin(byte[] data) { using var stream = new MemoryStream(data); return LoadFromStream(stream); }
        protected override Assembly Load(AssemblyName name) => name.Name == "WhoSceneIt" ? null : Default.Assemblies.FirstOrDefault(assembly => assembly.GetName().Name == name.Name);
        public void Dispose() => Unload();
    }
}
