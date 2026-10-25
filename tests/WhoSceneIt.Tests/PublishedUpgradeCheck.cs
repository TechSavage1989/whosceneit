using System.Reflection;
using System.Security.Cryptography;
using MediaBrowser.Model.Serialization;
using WhoSceneIt;
using WhoSceneIt.Updates;

// Explicit post-publication check. Uses real GitHub transport and real plugin
// entry points loaded from bytes, without starting an Emby server.
public static class PublishedUpgradeCheck
{
    public static async Task Run(IJsonSerializer json)
    {
        const string previousHash = "e2f2e93ee1aa4e3d656bf373cbdc8b5e5fb7bdbca9a6f30dff9f4eb6995ed024";
        var expected = typeof(Plugin).Assembly.GetName().Version.ToString(3);
        var root = Path.Combine(Path.GetTempPath(), "WhoSceneIt-published-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        object Call(object value, string name, params object[] arguments) => value.GetType().GetMethod(name).Invoke(value, arguments);
        object Status(object manager) => Call(manager, "Status");
        T Get<T>(object value, string property) => (T)value.GetType().GetProperty(property).GetValue(value);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WhoSceneIt-Published-MemoryLoad-Check");
            var previous = await http.GetByteArrayAsync("https://github.com/TechSavage1989/whosceneit/releases/download/v1.0.0/WhoSceneIt.dll");
            Assert(Convert.ToHexString(SHA256.HashData(previous)).ToLowerInvariant() == previousHash, "The previous published DLL failed its pinned digest.");
            string target, data, settings, installationRoot;
            byte[] published;
            using (var broken = new MemoryPluginFixture(root, previous, json))
            {
                broken.Register(); broken.CreateEntryPoint();
                Assert(broken.Assembly.Location == "", "The old plugin was not loaded from bytes.");
                var manager = broken.Manager;
                await (Task)Call(manager, "CheckAsync", "manual");
                Assert(Get<bool>(Status(manager), "UpdateAvailable") && Get<string>(Status(manager), "LatestVersion") == expected,
                    "The public feed did not offer the expected patch: " + Get<string>(Status(manager), "Message"));
                Call(manager, "BeginDownload");
                var timeout = DateTimeOffset.UtcNow.AddSeconds(90);
                while (Get<bool>(Status(manager), "Busy") && DateTimeOffset.UtcNow < timeout) await Task.Delay(200);
                Assert(Get<bool>(Status(manager), "Downloaded"), "The real public patch download failed: " + Get<string>(Status(manager), "Message"));
                var stopped = Call(manager, "Install");
                Assert(!Get<bool>(stopped, "RestartRequired") && Get<string>(stopped, "Message").Contains("path", StringComparison.OrdinalIgnoreCase), "The old entry point did not reproduce the reported install failure.");
                Assert(Hash(broken.Target) == previousHash, "The failed old install changed the target.");
                Console.WriteLine("PASS Negative control: public 1.0.0 loaded from bytes reproduces the empty-path installation failure after a verified real download.");
                published = File.ReadAllBytes(Path.Combine(broken.Data, "downloaded.dll"));
                Assert(AssemblyName.GetAssemblyName(Path.Combine(broken.Data, "downloaded.dll")).Version.ToString(3) == expected, "The downloaded patch has the wrong version.");
                target = broken.Target; data = broken.Data; settings = broken.Settings; installationRoot = broken.Root;
            }
            // Simulates the documented one-time manual replacement with Emby stopped.
            // The broken old plugin cannot execute the new path resolver itself.
            Directory.CreateDirectory(Path.Combine(data, "rollback"));
            File.Copy(target, Path.Combine(data, "rollback", "WhoSceneIt.dll"), true);
            File.WriteAllBytes(target, published);
            using var fixedPlugin = new MemoryPluginFixture(root, published, json, installationRoot);
            fixedPlugin.Register(); fixedPlugin.CreateEntryPoint();
            Assert(fixedPlugin.Resolve() == target, "The public patch entry point did not resolve its registered custom filename.");
            Assert(Get<bool>(fixedPlugin.Manager, "StartupVerified"), "The new memory-loaded entry point did not start.");
            Call(fixedPlugin.Manager, "ConfirmDashboardHealthy");
            Assert(!File.Exists(Path.Combine(data, "downloaded.dll")) && !File.Exists(Path.Combine(data, "pending-install.json")), "Bootstrap health acknowledgement left owned staging files.");
            Assert(File.ReadAllText(settings) == "preserve configuration" && Hash(Path.Combine(data, "rollback", "WhoSceneIt.dll")) == previousHash, "Bootstrap changed settings or the single backup.");
            Console.WriteLine("PASS Public " + expected + " manual bootstrap: actual memory-loaded entry point resolves the installed DLL, preserves settings/custom filename/backup, and cleans staging.");
            Console.WriteLine("Installed SHA-256: " + Hash(target));
            const string patchedHash = "2383314165e2ae609d9ff3a2ffe48482141bc16edf1e88246de015fbf387aaa0";
            var patchedPrevious = await http.GetByteArrayAsync("https://github.com/TechSavage1989/whosceneit/releases/download/v1.3.0/WhoSceneIt.dll");
            Assert(Convert.ToHexString(SHA256.HashData(patchedPrevious)).ToLowerInvariant() == patchedHash, "The previous patched updater failed its pinned digest.");
            string upgradeRoot, upgradeTarget, upgradeData, upgradeSettings;
            using (var upgradable = new MemoryPluginFixture(root, patchedPrevious, json))
            {
                upgradable.Register(); upgradable.CreateEntryPoint();
                var manager = upgradable.Manager;
                Call(manager, "ConfirmDashboardHealthy");
                await (Task)Call(manager, "CheckAsync", "manual");
                Assert(Get<string>(Status(manager), "LatestVersion") == expected, "The patched public updater did not see the release.");
                Call(manager, "BeginDownload");
                var timeout = DateTimeOffset.UtcNow.AddSeconds(90);
                while (Get<bool>(Status(manager), "Busy") && DateTimeOffset.UtcNow < timeout) await Task.Delay(200);
                Assert(Get<bool>(Status(manager), "Downloaded"), "The patched previous updater did not verify the public download.");
                var installed = Call(manager, "Install");
                Assert(Get<bool>(installed, "RestartRequired"), "Public 1.3.0 could not install the new DLL: " + Get<string>(installed, "Message"));
                Assert(Hash(upgradable.Target) == Hash(target), "The previous updater installed different public bytes.");
                upgradeRoot = upgradable.Root; upgradeTarget = upgradable.Target; upgradeData = upgradable.Data; upgradeSettings = upgradable.Settings;
            }
            using var upgraded = new MemoryPluginFixture(root, File.ReadAllBytes(upgradeTarget), json, upgradeRoot);
            upgraded.Register(); upgraded.CreateEntryPoint();
            Assert(Get<bool>(upgraded.Manager, "StartupVerified") && Get<bool>(Status(upgraded.Manager), "AwaitingDashboardVerification"), "The upgraded entry point did not verify its pending install.");
            Call(upgraded.Manager, "ConfirmDashboardHealthy");
            Assert(!File.Exists(Path.Combine(upgradeData, "pending-install.json")) && !File.Exists(Path.Combine(upgradeData, "downloaded.dll")), "Verified upgrade staging was not cleaned.");
            Assert(Hash(Path.Combine(upgradeData, "rollback", "WhoSceneIt.dll")) == patchedHash && File.ReadAllText(upgradeSettings) == "preserve configuration", "The public upgrade changed settings or rollback bytes.");
            Console.WriteLine("PASS Published 1.3.0 to " + expected + ": actual memory-loaded entry points, real HTTPS feed/download, verified custom-target installation, backup, settings, pending reload verification, and cleanup.");
            Console.WriteLine("Versions 1.0.0 and 1.1.0 need manual replacement once. No live Emby process was started or restarted.");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
