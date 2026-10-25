using System.Reflection;
using System.Runtime.Loader;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using WhoSceneIt;
using WhoSceneIt.Updates;

public static class PluginPathChecks
{
    public static async Task Run(string root, string candidate, IJsonSerializer json, Action<string, Action> test, Func<string, Func<Task>, Task> testAsync, Action<bool, string> assert)
    {
        void Reject(Action action, string expected)
        {
            try { action(); }
            catch (TargetInvocationException exception) when (exception.InnerException?.Message.Contains(expected) == true) { return; }
            catch (InvalidOperationException exception) when (exception.Message.Contains(expected)) { return; }
            throw new Exception("Unsafe plugin-path lookup was accepted or reported the wrong error.");
        }
        test("Memory-loaded Emby entry point reproduces the old empty path and resolves registered custom filenames", () =>
        {
            using var fixture = new MemoryPluginFixture(root, File.ReadAllBytes(typeof(Plugin).Assembly.Location), json);
            assert(fixture.Assembly.Location == "", "The fixture did not load the real DLL from bytes.");
            // Negative control: the exact Assembly.Location/Hash path used by 1.0.0 fails.
            try { UpdateIntegrity.Hash(fixture.Assembly.Location); throw new Exception("An empty assembly path was accepted."); }
            catch (ArgumentException exception) { assert(exception.ParamName == "path", "The old failure did not match the reported path error."); }
            fixture.Register(); fixture.CreateEntryPoint();
            assert(fixture.Resolve() == fixture.Target, "The actual memory-loaded entry point lost the registered filename.");
            assert(Path.GetFileName(fixture.Resolve()) == "CustomWhoSceneIt.dll", "A custom filename was replaced by a guessed default.");
        });
        test("Memory-load fallback finds one matching plugin DLL and rejects duplicate installations", () =>
        {
            using var fixture = new MemoryPluginFixture(root, File.ReadAllBytes(typeof(Plugin).Assembly.Location), json);
            fixture.CreateEntryPoint();
            assert(fixture.Resolve() == fixture.Target, "The unique plugins-directory fallback failed.");
            var duplicate = Path.Combine(fixture.Plugins, "WhoSceneIt.dll"); File.Copy(fixture.Target, duplicate);
            Reject(() => fixture.Resolve(), "More than one");
            File.Delete(duplicate);
            fixture.Register(); fixture.CreateEntryPoint();
            assert(fixture.Resolve() == fixture.Target, "Registered metadata did not restore a safe target.");
        });
        test("Path discovery rejects wrong applications, versions, missing targets, and shadow-cache paths", () =>
        {
            using var fixture = new MemoryPluginFixture(root, File.ReadAllBytes(typeof(Plugin).Assembly.Location), json);
            fixture.Register(); fixture.CreateEntryPoint();
            var original = File.ReadAllBytes(fixture.Target);
            File.Copy(typeof(PluginPathChecks).Assembly.Location, fixture.Target, true);
            Reject(() => fixture.Resolve(), "different application");
            File.Copy(candidate, fixture.Target, true); Reject(() => fixture.Resolve(), "differs from the loaded");
            File.Delete(fixture.Target); Reject(() => fixture.Resolve(), "does not exist");
            Reject(() => PluginPathResolver.Resolve(fixture.Paths, fixture.EmptyHost(), typeof(Plugin).Assembly), "usable installed DLL path");
            File.WriteAllBytes(fixture.Target, original);
            assert(UpdateIntegrity.Hash(fixture.Target) == fixture.OriginalHash, "Rejected path discovery changed installed bytes.");
        });
        test("Emby may register plugin attributes after entry-point construction", () =>
        {
            using var fixture = new MemoryPluginFixture(root, File.ReadAllBytes(typeof(Plugin).Assembly.Location), json);
            fixture.CreateEntryPoint(); fixture.Register();
            assert(fixture.Resolve() == fixture.Target, "The resolver cached an empty startup path.");
            fixture.Plugin.SetAttributes(Path.GetFileName(fixture.Target), fixture.Data, fixture.Assembly.GetName().Version);
            assert(fixture.Resolve() == fixture.Target, "A host-provided plugin filename was not resolved under PluginsPath.");
        });
        await testAsync("Memory-load target discovery survives verified install, backup, new memory load, and cleanup", async () =>
        {
            using var fixture = new MemoryPluginFixture(root, File.ReadAllBytes(typeof(Plugin).Assembly.Location), json);
            fixture.CreateEntryPoint(); fixture.Register();
            var version = AssemblyName.GetAssemblyName(candidate).Version.ToString(3);
            var manifest = new ReleaseManifest { SchemaVersion = 1, AppId = "WhoSceneIt", PluginId = UpdateIntegrity.PluginId, Repository = UpdateIntegrity.Repository,
                Version = version, MinimumEmbyVersion = "4.8.0.0", ReleaseUrl = "https://github.com/TechSavage1989/whosceneit/releases/tag/v" + version,
                Asset = new ReleaseAsset { FileName = "WhoSceneIt.dll", DownloadUrl = "https://github.com/TechSavage1989/whosceneit/releases/download/v" + version + "/WhoSceneIt.dll",
                    Sha256 = UpdateIntegrity.Hash(candidate), SizeBytes = new FileInfo(candidate).Length } };
            var handler = new FakeHttp(request => Task.FromResult(request.RequestUri.Host == "raw.githubusercontent.com" ? FakeHttp.ManifestResponse(manifest) :
                new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.ByteArrayContent(File.ReadAllBytes(candidate)) }));
            var resolutions = 0;
            using var manager = new UpdateManager(fixture.Data, null, json, fixture.Host, fixture.Library, fixture.Sessions, handler, fixture.Tasks, () => { resolutions++; return fixture.Resolve(); });
            assert(resolutions == 0, "The installer resolved a startup-only path too early.");
            await manager.CheckAsync("manual"); manager.BeginDownload();
            var until = DateTimeOffset.UtcNow.AddSeconds(10); while (manager.Status().Busy && DateTimeOffset.UtcNow < until) await Task.Delay(10);
            assert(manager.Status().Downloaded, "The memory-load fixture did not download the candidate.");
            manager.Install(); assert(manager.Status().RestartRequired && resolutions == 1, "Installation did not use the runtime path provider.");
            assert(UpdateIntegrity.Hash(fixture.Target) == manifest.Asset.Sha256 && UpdateIntegrity.Hash(Path.Combine(fixture.Data, "rollback", "WhoSceneIt.dll")) == fixture.OriginalHash, "Installation or the single backup used the wrong path.");
            assert(File.ReadAllText(fixture.Settings) == "preserve configuration", "Installation changed settings.");
            using var next = new MemoryPluginFixture(root, File.ReadAllBytes(candidate), json, fixture.Root);
            next.Register(); next.CreateEntryPoint();
            var status = next.Manager.GetType().GetMethod("Status").Invoke(next.Manager, null);
            assert((bool)next.Manager.GetType().GetProperty("StartupVerified").GetValue(next.Manager), "The new memory-loaded entry point could not verify its pending install.");
            assert((bool)status.GetType().GetProperty("AwaitingDashboardVerification").GetValue(status), "Staging was cleaned before dashboard health.");
            next.Manager.GetType().GetMethod("ConfirmDashboardHealthy").Invoke(next.Manager, null);
            assert(!File.Exists(Path.Combine(fixture.Data, "pending-install.json")) && !File.Exists(Path.Combine(fixture.Data, "downloaded.dll")), "Memory-load health acknowledgement did not clean staging.");
        });
    }
}

public sealed class MemoryPluginFixture : IDisposable
{
    private sealed class Context : AssemblyLoadContext
    {
        public Context() : base(isCollectible: true) { }
        protected override Assembly Load(AssemblyName name) => name.Name == "WhoSceneIt" ? null : Default.Assemblies.FirstOrDefault(a => a.GetName().Name == name.Name);
    }
    private readonly Context _context = new();
    private readonly IJsonSerializer _json;
    private object _entryPoint;
    private IPlugin[] _plugins = Array.Empty<IPlugin>();
    public string Root, Plugins, Target, Data, Settings, OriginalHash;
    public Assembly Assembly;
    public BasePlugin Plugin;
    public IApplicationPaths Paths;
    public IApplicationHost Host;
    public ILibraryManager Library;
    public ISessionManager Sessions;
    public ITaskManager Tasks;
    public object Manager => _entryPoint.GetType().GetField("_manager", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_entryPoint);
    public MemoryPluginFixture(string parent, byte[] assembly, IJsonSerializer json, string existing = null)
    {
        Root = existing ?? Path.Combine(parent, "memory-plugin-" + Guid.NewGuid().ToString("N")); Plugins = Path.Combine(Root, "plugins");
        var dataRoot = Path.Combine(Root, "data"); Data = Path.Combine(dataRoot, "WhoSceneIt");
        Directory.CreateDirectory(Plugins); Directory.CreateDirectory(Data);
        Target = Path.Combine(Plugins, "CustomWhoSceneIt.dll"); Settings = Path.Combine(Root, "configuration.xml");
        if (existing == null) { File.WriteAllBytes(Target, assembly); File.WriteAllText(Settings, "preserve configuration"); }
        OriginalHash = UpdateIntegrity.Hash(Target); _json = json;
        using (var stream = new MemoryStream(assembly)) Assembly = _context.LoadFromStream(stream);
        Paths = Proxy.Create<IApplicationPaths>((m, a) => m.Name switch { "get_PluginsPath" => Plugins, "get_DataPath" => dataRoot, "get_PluginConfigurationsPath" => Root, _ => Root });
        Host = Proxy.Create<IApplicationHost>((m, a) => m.Name switch { "get_Plugins" => _plugins, "get_IsStartupComplete" => true, "get_ApplicationVersion" => new Version(4, 8, 11, 0), _ => null });
        Library = Proxy.Create<ILibraryManager>((m, a) => null);
        Sessions = Proxy.Create<ISessionManager>((m, a) => m.Name == "get_Sessions" ? Array.Empty<SessionInfo>() : null);
        Tasks = Proxy.Create<ITaskManager>((m, a) => m.Name == "get_ScheduledTasks" ? Array.Empty<IScheduledTaskWorker>() : null);
        Plugin = (BasePlugin)Activator.CreateInstance(Assembly.GetType("WhoSceneIt.Plugin"), Paths, Proxy.Create<IXmlSerializer>((m, a) => null));
    }
    public void Register() { Plugin.SetAttributes(Target, Data, Assembly.GetName().Version); _plugins = new IPlugin[] { Plugin }; }
    public void CreateEntryPoint()
    {
        (_entryPoint as IDisposable)?.Dispose();
        _entryPoint = Activator.CreateInstance(Assembly.GetType("WhoSceneIt.Updates.UpdateEntryPoint"), Paths, _json, Host, Library, Sessions, Tasks);
    }
    public string Resolve() => (string)Manager.GetType().GetMethod("GetInstalledTarget", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Manager, null);
    public IApplicationHost EmptyHost() => Proxy.Create<IApplicationHost>((m, a) => m.Name == "get_Plugins" ? Array.Empty<IPlugin>() : null);
    public void Dispose() { (_entryPoint as IDisposable)?.Dispose(); _context.Unload(); }
}
