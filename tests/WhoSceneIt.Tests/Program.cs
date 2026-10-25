using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using WhoSceneIt;
using WhoSceneIt.Updates;
using WhoSceneIt.Deletion;
using MediaBrowser.Common;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

if (args.Contains("--published-upgrade")) { await PublishedUpgradeCheck.Run(new TestJson()); return; }
var candidate = args.FirstOrDefault(x => !x.StartsWith("--"));
if (candidate == null || !File.Exists(candidate)) throw new ArgumentException("Pass the path of a test-only next-patch WhoSceneIt DLL.");
var candidateVersion = AssemblyName.GetAssemblyName(candidate).Version.ToString(3);
var root = Path.Combine(Path.GetTempPath(), "WhoSceneIt-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var passed = new List<string>();
var json = new TestJson();
void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
void Test(string name, Action action) { action(); passed.Add(name); Console.WriteLine("PASS " + name); }
async Task TestAsync(string name, Func<Task> action) { await action(); passed.Add(name); Console.WriteLine("PASS " + name); }
void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid input was accepted."); }
ReleaseManifest Manifest() => new()
{
    SchemaVersion = 1, AppId = "WhoSceneIt", PluginId = UpdateIntegrity.PluginId, Repository = UpdateIntegrity.Repository,
    Version = candidateVersion, MinimumEmbyVersion = "4.8.0.0", ReleaseUrl = "https://github.com/TechSavage1989/whosceneit/releases/tag/v" + candidateVersion,
    Notes = new[] { "Test-only candidate." }, Asset = new()
    {
        FileName = "WhoSceneIt.dll", DownloadUrl = "https://github.com/TechSavage1989/whosceneit/releases/download/v" + candidateVersion + "/WhoSceneIt.dll",
        Sha256 = UpdateIntegrity.Hash(candidate), SizeBytes = new FileInfo(candidate).Length
    }
};
var dt = DateTimeOffset.Parse("2025-01-02T12:00:00+02:00");
WatchRecord Record(string id, string type = "Movie", string series = null, bool played = true, long position = 0, int count = 1, DateTimeOffset? date = null) => new()
{
    Title = new() { Id = id, Name = "Title " + id, Type = type, SeriesName = "Shared title", SeasonNumber = 1, EpisodeNumber = 2 },
    SeriesId = series, Played = played, PlaybackPositionTicks = position, PlayCount = count, LastPlayed = date
};
try
{
    Test("Manual watch flags are retained without inventing play counts or dates", () =>
    {
        var row = ReportBuilder.ForUser(new[] { Record("1", count:0) }, "Titles", false).Single();
        Assert(row.WatchedCount == 1 && row.SavedPlayCount == 0 && row.LastPlayedUtc == null, "Saved state was changed.");
    });
    Test("Play counts and dates alone never imply completed viewing", () =>
    {
        var records = new[] { Record("1",played:false,count:3,date:dt), Record("2",played:false,position:100) };
        Assert(ReportBuilder.ForUser(records,"Titles",false).Count == 0,"Incomplete viewing counted as watched.");
        var row = ReportBuilder.ForUser(records,"Titles",true).Single();
        Assert(row.ItemId == "2" && row.Status == "Partially watched", "Progress filter is incorrect.");
    });
    Test("Shows with identical names remain separate and repeated records do not inflate counts", () =>
    {
        var records = new[] { Record("1","Episode","10",date:dt), Record("1","Episode","10",date:dt), Record("2","Episode","20"), Record("3","Episode",null) };
        var rows = ReportBuilder.ForUser(records,"Shows",false);
        Assert(rows.Count == 3 && rows.All(x=>x.WatchedCount==1),"Show identity or de-duplication failed.");
        Assert(rows.Single(x=>x.ItemId=="10").LastPlayedUtc == "2025-01-02T10:00:00.0000000+00:00","Dates are not UTC.");
    });
    Test("A show viewer needs one qualifying episode, and complete means all available files", () =>
    {
        var title = new TitleSummary { Id="10",Name="Show",Type="Series" };
        var user = new UserSummary { Id="u1",Name="User" };
        var row = ReportBuilder.ForViewer(new[]{ Record("1","Episode","10"), Record("2","Episode","10",false,100) },title,user,false,2);
        Assert(row.WatchedCount==1 && row.Status=="Watched episodes" && row.AvailableEpisodeCount==2,"Partial show incorrectly completed.");
        var progress = ReportBuilder.ForViewer(new[]{Record("2","Episode","10",false,100)},title,user,true,2);
        Assert(progress.Status=="Partially watched" && progress.InProgressCount==1,"Partial viewer missing.");
    });
    Test("Episode ranges, title filtering, and unknown dates are handled", () =>
    {
        var title = new TitleSummary { Type="Episode",SeasonNumber=0,EpisodeNumber=1,EpisodeNumberEnd=3 };
        Assert(ReportBuilder.EpisodeLabel(title)=="S00E01-E03","Episode range lost.");
        var rows = ReportBuilder.ForUser(new[]{Record("2",date:dt),Record("1")},"Titles",false);
        Assert(ReportBuilder.FilterAndSort(rows,null,"LastPlayed").First().ItemId=="2","Unknown date sorted first.");
        Assert(ReportBuilder.FilterAndSort(rows,"TITLE 1","Name").Single().ItemId=="1","Search failed.");
    });
    Test("Every report, update, deletion, and integration route requires the native Admin role", () =>
    {
        var routes = typeof(Plugin).Assembly.GetTypes().Where(t=>t.GetCustomAttribute<RouteAttribute>()!=null).ToArray();
        Assert(routes.Length==17,"Unexpected public endpoint count.");
        Assert(routes.All(t=>t.GetCustomAttribute<AuthenticatedAttribute>()?.Roles=="Admin"),"An endpoint is missing server-side authorization.");
        Assert(routes.Where(t=>t.Namespace=="WhoSceneIt.Updates" && t.Name!="GetUpdates").All(t=>t.GetCustomAttribute<RouteAttribute>().Verbs=="POST"),"Update writes allow GET.");
        Assert(routes.Where(t=>t.Namespace=="WhoSceneIt.Deletion" && t.Name!="GetDeletionStatus").All(t=>t.GetCustomAttribute<RouteAttribute>().Verbs=="POST"),"Deletion writes allow GET.");
        Assert(routes.Where(t=>t.Namespace=="WhoSceneIt.Integrations" && t.Name!="GetArrSettings").All(t=>t.GetCustomAttribute<RouteAttribute>().Verbs=="POST"),"Integration writes allow GET.");
    });
    Test("Manifest rejects other repositories, versions, URLs, hashes, sizes, and app identities", () =>
    {
        UpdateIntegrity.ValidateManifest(Manifest(),new Version(4,8,11,0));
        foreach(var change in new Action<ReleaseManifest>[] {
            m=>m.AppId="Other", m=>m.PluginId=Guid.NewGuid().ToString(), m=>m.Repository="attacker/repo",
            m=>m.Version="1.0.1-beta", m=>m.Asset.DownloadUrl+="?token=abc", m=>m.ReleaseUrl="http://github.com/attacker",
            m=>m.Asset.FileName="../WhoSceneIt.dll", m=>m.Asset.Sha256="bad", m=>m.Asset.SizeBytes=UpdateIntegrity.MaxBinarySize+1,
            m=>m.MinimumEmbyVersion="99.0.0.0" }) { var m=Manifest();change(m);Reject(()=>UpdateIntegrity.ValidateManifest(m,new Version(4,8,11,0))); }
        Assert(!UpdateIntegrity.IsAllowedAssetHop(new Uri("https://github.com.attacker.invalid/test"),false),"Untrusted redirect accepted.");
        Assert(!UpdateIntegrity.IsAllowedAssetHop(new Uri("http://release-assets.githubusercontent.com/test"),false),"HTTP redirect accepted.");
    });
    Test("DLL validation rejects a wrong digest, assembly version, other app, and executable payload", () =>
    {
        var m=Manifest(); UpdateIntegrity.ValidateBinary(candidate,m.Version,m.Asset.Sha256,m.Asset.SizeBytes);
        Reject(()=>UpdateIntegrity.ValidateBinary(candidate,"1.0.2",m.Asset.Sha256,m.Asset.SizeBytes));
        Reject(()=>UpdateIntegrity.ValidateBinary(candidate,m.Version,new string('0',64),m.Asset.SizeBytes));
        var foreign=typeof(TestJson).Assembly.Location;
        Reject(()=>UpdateIntegrity.ValidateBinary(foreign,m.Version,UpdateIntegrity.Hash(foreign),new FileInfo(foreign).Length));
        var bad=Path.Combine(root,"executable.dll");var bytes=File.ReadAllBytes(candidate);var pe=BitConverter.ToInt32(bytes,0x3c);bytes[pe+23] &= 0xdf;File.WriteAllBytes(bad,bytes);
        Reject(()=>UpdateIntegrity.ValidateBinary(bad,m.Version,UpdateIntegrity.Hash(bad),bytes.Length));
    });
    Test("Previous DLL is replaced from another directory, one backup is retained, and settings survive", () =>
    {
        var dir=Path.Combine(root,"replace");Directory.CreateDirectory(dir);
        var target=Path.Combine(dir,"CustomWhoSceneIt.dll");var backup=Path.Combine(root,"rollback");
        File.Copy(typeof(Plugin).Assembly.Location,target);var before=UpdateIntegrity.Hash(target);
        var settings=Path.Combine(dir,"settings.xml");File.WriteAllText(settings,"preserve this");
        UpdateInstaller.Install(candidate,target,backup,Manifest());
        Assert(UpdateIntegrity.Hash(target)==Manifest().Asset.Sha256,"New DLL was not installed.");
        Assert(UpdateIntegrity.Hash(Path.Combine(backup,"WhoSceneIt.dll"))==before,"Backup is wrong.");
        Assert(Directory.GetFiles(backup).Length==1 && !Directory.GetFiles(dir,"*.tmp").Any(),"Cleanup failed.");
        Assert(File.ReadAllText(settings)=="preserve this","Settings were changed.");
        var invalid=Manifest();invalid.Asset.Sha256=new string('0',64);
        Reject(()=>UpdateInstaller.Install(candidate,target,backup,invalid));
        Assert(UpdateIntegrity.Hash(target)==Manifest().Asset.Sha256,"Rejected update changed the installed file.");
    });
    Test("A replacement failure after the old target is removed restores its actual bytes", () =>
    {
        var dir=Path.Combine(root,"failure");Directory.CreateDirectory(dir);
        var target=Path.Combine(dir,"WhoSceneIt.dll");File.Copy(typeof(Plugin).Assembly.Location,target);
        var before=UpdateIntegrity.Hash(target);
        try { UpdateInstaller.Install(candidate,target,Path.Combine(dir,"backup"),Manifest(),(staged,destination)=>{File.Delete(destination);throw new IOException("Injected replacement failure");}); }
        catch(IOException) { Assert(UpdateIntegrity.Hash(target)==before,"Rollback did not restore the original DLL.");return; }
        throw new Exception("The injected failure was not reached.");
    });

    var playback = false; var scanning = false; var runningTasks = false;
    var host = Proxy.Create<IApplicationHost>((m,a)=> m.Name switch { "get_ApplicationVersion"=>new Version(4,8,11,0), "get_IsStartupComplete"=>true, "get_CanSelfRestart"=>false, "get_IsShuttingDown"=>false, _=>null });
    var library = Proxy.Create<ILibraryManager>((m,a)=> m.Name=="get_IsScanRunning" ? scanning : null);
    SessionInfo SampleSession(bool playing)
    {
        var session = new SessionInfo(null, Proxy.Create<MediaBrowser.Model.Logging.ILogger>((m,a)=>null));
        if (playing)
        {
            var progress = new MediaBrowser.Model.Session.PlaybackProgressInfo { PlaySessionId="fixture" };
            session.GetOrAddPlaySessionInfo(progress).NowPlayingItem = new MediaBrowser.Model.Dto.BaseItemDto { Id="1" };
            session.RegisterPlaySession(progress);
        }
        return session;
    }
    var sessions = Proxy.Create<ISessionManager>((m,a)=>m.Name=="get_Sessions" ? new[]{SampleSession(playback)} : null);
    var taskWorker=Proxy.Create<MediaBrowser.Model.Tasks.IScheduledTaskWorker>((m,a)=>m.Name=="get_State"?(runningTasks?MediaBrowser.Model.Tasks.TaskState.Running:MediaBrowser.Model.Tasks.TaskState.Idle):null);
    var tasks=Proxy.Create<MediaBrowser.Model.Tasks.ITaskManager>((m,a)=>m.Name=="get_ScheduledTasks"?new[]{taskWorker}:null);
    var tempSequence=0;
    UpdateManager Manager(FakeHttp handler) {
        var dir=Path.Combine(root,"manager"+(++tempSequence));Directory.CreateDirectory(dir);
        var target=Path.Combine(dir,"WhoSceneIt.dll");File.Copy(typeof(Plugin).Assembly.Location,target);
        return new UpdateManager(Path.Combine(dir,"data"),target,json,host,library,sessions,handler,tasks);
    }
    Test("Successful assembly reload keeps staging until dashboard health is acknowledged",()=>
    {
        var dir=Path.Combine(root,"reload");Directory.CreateDirectory(dir);
        var target=Path.Combine(dir,"WhoSceneIt.dll");File.Copy(typeof(Plugin).Assembly.Location,target);
        var data=Path.Combine(dir,"data");Directory.CreateDirectory(data);
        var pending=new InstallTransaction {Version=typeof(Plugin).Assembly.GetName().Version.ToString(3),Sha256=UpdateIntegrity.Hash(target),SizeBytes=new FileInfo(target).Length,PreviousVersion="1.0.0"};
        File.WriteAllText(Path.Combine(data,"pending-install.json"),json.SerializeToString(pending));
        File.WriteAllText(Path.Combine(data,"downloaded.dll"),"owned staged bytes");
        File.WriteAllText(Path.Combine(data,"settings.xml"),"preserved settings");
        using var manager=new UpdateManager(data,target,json,host,library,sessions,new FakeHttp(_=>throw new Exception("No network expected")),tasks);
        Assert(manager.StartupVerified && manager.Status().AwaitingDashboardVerification,"Loaded version did not verify.");
        Assert(File.Exists(Path.Combine(data,"pending-install.json")) && File.Exists(Path.Combine(data,"downloaded.dll")),"Cleanup ran before dashboard verification.");
        manager.ConfirmDashboardHealthy();
        Assert(!manager.Status().AwaitingDashboardVerification && !File.Exists(Path.Combine(data,"pending-install.json")) && !File.Exists(Path.Combine(data,"downloaded.dll")),"Verified cleanup failed.");
        Assert(File.ReadAllText(Path.Combine(data,"settings.xml"))=="preserved settings","Health cleanup changed settings.");
    });
    await TestAsync("Startup, real concurrent checks, and manual cooldown share one request", async()=>
    {
        var h=new FakeHttp(async(request)=> { await Task.Delay(100);return FakeHttp.ManifestResponse(Manifest()); });
        using var manager=Manager(h);
        var first=manager.CheckAsync("startup");await Task.WhenAll(first,manager.CheckAsync("manual"),manager.CheckAsync("focus"));
        await manager.CheckAsync("manual");
        Assert(h.Count==1 && manager.Status().UpdateAvailable,"Single-flight or cooldown failed.");
    });
    await TestAsync("Offline status is an error, and Retry-After blocks repeated manual checks", async()=>
    {
        var h=new FakeHttp(request=> { var r=new HttpResponseMessage(HttpStatusCode.TooManyRequests);r.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromMinutes(10));return Task.FromResult(r); });
        using var manager=Manager(h);await manager.CheckAsync("manual");await manager.CheckAsync("manual");
        Assert(h.Count==1 && manager.Status().Message.Contains("failed") && manager.Status().NextAllowedCheckUtc!=null,"429 backoff failed.");
        using var offline=Manager(new FakeHttp(request=>throw new HttpRequestException("offline")));
        await offline.CheckAsync("startup");Assert(offline.Status().Message.Contains("offline") && offline.Status().LastSuccessfulCheckUtc==null,"Offline falsely reported current.");
    });
    await TestAsync("Complete update download, digest, staging, playback/scan guards, install, backup, and manual restart state", async()=>
    {
        var h=new FakeHttp(request=> Task.FromResult(request.RequestUri.Host=="raw.githubusercontent.com"?FakeHttp.ManifestResponse(Manifest()):new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(File.ReadAllBytes(candidate))}));
        using var manager=Manager(h);await manager.CheckAsync("startup");manager.BeginDownload();
        var until=DateTimeOffset.UtcNow.AddSeconds(15);while(manager.Status().Busy && DateTimeOffset.UtcNow<until)await Task.Delay(25);
        Assert(manager.Status().Downloaded,"Verified download failed: "+manager.Status().Message);
        manager.ConfirmDashboardHealthy();
        playback=true;manager.Install();Assert(!manager.Status().RestartRequired && manager.Status().Message.Contains("playback"),"Playback guard failed: "+manager.Status().Message);playback=false;
        scanning=true;manager.Install();Assert(!manager.Status().RestartRequired && manager.Status().Message.Contains("scan"),"Scan guard failed.");scanning=false;
        runningTasks=true;manager.Install();Assert(!manager.Status().RestartRequired && manager.Status().Message.Contains("scheduled"),"Scheduled task guard failed.");runningTasks=false;
        manager.Install();Assert(manager.Status().RestartRequired && !manager.Status().Downloaded,"Installation did not reach restart-required state: "+manager.Status().Message);
    });
    await TestAsync("A bad downloaded digest never becomes installable", async()=>
    {
        var h=new FakeHttp(request=>Task.FromResult(request.RequestUri.Host=="raw.githubusercontent.com"?FakeHttp.ManifestResponse(Manifest()):new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[new FileInfo(candidate).Length])}));
        using var manager=Manager(h);await manager.CheckAsync("startup");manager.BeginDownload();
        var until=DateTimeOffset.UtcNow.AddSeconds(15);while(manager.Status().Busy && DateTimeOffset.UtcNow<until)await Task.Delay(25);
        Assert(!manager.Status().Downloaded && manager.Status().Message.Contains("failed"),"Bad download was accepted.");
    });

    ProgressChecks.Run(Test, Assert);
    await PluginPathChecks.Run(root, candidate, json, Test, TestAsync, Assert);
    await DeletionChecks.Run(root, Test, TestAsync, Assert);
    await DeletionServiceChecks.Run(root, Test, TestAsync, Assert);
    await TestAsync("Background update checks do not interrupt media deletion", async () =>
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var manager = Manager(new FakeHttp(async request => { entered.SetResult(true); await release.Task; return FakeHttp.ManifestResponse(Manifest()); }));
        var instance = typeof(UpdateManager).GetProperty("Instance"); var previous = instance.GetValue(null);
        instance.SetValue(null, manager); Task check = null;
        try
        {
            check = manager.CheckAsync("startup"); await entered.Task;
            Assert(manager.Status().Busy && !manager.IsInstalling, "The background check fixture did not become busy.");
            new NativeDeletionFixture(root).Backend.EnsureAvailable();
        }
        finally { release.TrySetResult(true); if (check != null) await check; instance.SetValue(null, previous); }
    });
    await TestAsync("An active media deletion blocks plugin installation and restart", async () =>
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var backend = new DeletionFixture(root) { Entered = entered, Release = release };
        var deletion = new DeletionManager(backend); var preview = deletion.Preview("admin", "user", new[] { "1", "2" });
        using var manager = Manager(new FakeHttp(request => Task.FromResult(request.RequestUri.Host == "raw.githubusercontent.com" ? FakeHttp.ManifestResponse(Manifest()) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(candidate)) })));
        await manager.CheckAsync("startup"); manager.BeginDownload();
        var limit = DateTimeOffset.UtcNow.AddSeconds(10); while (manager.Status().Busy && DateTimeOffset.UtcNow < limit) await Task.Delay(10);
        Assert(manager.Status().Downloaded, "Upgrade fixture did not download.");
        try
        {
            deletion.Execute("admin", preview.Token, "DELETE"); Assert(await Task.Run(() => entered.Wait(3000)), "Deletion did not start.");
            manager.Install(); Assert(!manager.Status().RestartRequired && manager.Status().Message.Contains("media deletion"), "Installation ran during deletion.");
            manager.Restart(); Assert(manager.Status().Message.Contains("media deletion"), "Restart was not blocked during deletion.");
        }
        finally { release.Set(); }
        limit = DateTimeOffset.UtcNow.AddSeconds(5); while (deletion.Status("admin", preview.Token).State == "Deleting" && DateTimeOffset.UtcNow < limit) await Task.Delay(10);
        Assert(deletion.Status("admin", preview.Token).State == "Completed", "Deletion did not finish after the guard check.");
    });
    await ArrChecks.Run(root, Test, TestAsync, Assert);
    if(args.Contains("--real-timer"))
    {
        await TestAsync("Real five-minute timer, two-minute focus gate, manual cooldown expiry, ETag, and HTTP 304", async()=>
        {
            var sawEtag=false;
            var h=new FakeHttp(request=> {
                if(request.Headers.Contains("If-None-Match")){sawEtag=true;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));}
                return Task.FromResult(FakeHttp.ManifestResponse(Manifest()));
            });
            using var manager=Manager(h);manager.Start();
            var focusHttp=new FakeHttp(request=>Task.FromResult(FakeHttp.ManifestResponse(Manifest())));
            var manualHttp=new FakeHttp(request=>Task.FromResult(FakeHttp.ManifestResponse(Manifest())));
            var timerHttp=new FakeHttp(request=>Task.FromResult(FakeHttp.ManifestResponse(Manifest())));
            using var focusManager=Manager(focusHttp);using var manualManager=Manager(manualHttp);using var timerManager=Manager(timerHttp);
            await focusManager.CheckAsync("startup");await focusManager.CheckAsync("focus");
            await manualManager.CheckAsync("startup");
            await timerManager.CheckAsync("startup");
            Assert(focusHttp.Count==1,"Focus check ignored its initial cooldown.");
            var realExpiry=Task.Run(async()=>{
                await Task.Delay(31000);await manualManager.CheckAsync("manual");Assert(manualHttp.Count==2,"Manual cooldown did not expire.");
                await timerManager.CheckAsync("timer");Assert(timerHttp.Count==2,"Timer callback incorrectly applied a second five-minute cooldown.");
                await Task.Delay(91000);await focusManager.CheckAsync("focus");Assert(focusHttp.Count==2,"Focus check did not run after two real minutes.");
            });
            var until=DateTimeOffset.UtcNow.AddMinutes(5).AddSeconds(15);
            while(h.Count<2 && DateTimeOffset.UtcNow<until)await Task.Delay(250);
            Assert(h.Count==2 && sawEtag,"The real timer did not perform a conditional request.");
            Assert(!manager.Status().Message.Contains("failed"),"304 cache handling failed.");
            await realExpiry;
        });
    }
    Console.WriteLine("RESULT " + passed.Count + " checks passed.");
}
finally { try { Directory.Delete(root,true); } catch { } }

public class Proxy : DispatchProxy
{
    public Func<MethodInfo,object[],object> Call;
    protected override object Invoke(MethodInfo method,object[] arguments)
    {
        var value=Call(method,arguments);
        return value ?? (method.ReturnType.IsValueType && method.ReturnType!=typeof(void)?Activator.CreateInstance(method.ReturnType):null);
    }
    public static T Create<T>(Func<MethodInfo,object[],object> call) where T:class { var proxy=Create<T,Proxy>();((Proxy)(object)proxy).Call=call;return proxy; }
}
public sealed class FakeHttp : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage,Task<HttpResponseMessage>> _respond;
    public int Count;
    public FakeHttp(Func<HttpRequestMessage,Task<HttpResponseMessage>> respond) { _respond=respond; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) { Interlocked.Increment(ref Count);return _respond(request); }
    public static HttpResponseMessage ManifestResponse(ReleaseManifest manifest) {
        var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(manifest),Encoding.UTF8,"application/json")};response.Headers.ETag=new EntityTagHeaderValue("\"fixture\"");return response;
    }
}
public sealed class TestJson : IJsonSerializer
{
    public string SerializeToString(object value)=>JsonSerializer.Serialize(value);
    public string SerializeToString(object value,MediaBrowser.Model.Serialization.JsonSerializerOptions options)=>SerializeToString(value);
    public T DeserializeFromString<T>(string value)=>JsonSerializer.Deserialize<T>(value);
    public object DeserializeFromString(string value,Type type)=>JsonSerializer.Deserialize(value,type);
    public T DeserializeFromFile<T>(string path) where T:class =>DeserializeFromString<T>(File.ReadAllText(path));
    public Task<T> DeserializeFromFileAsync<T>(string path) where T:class =>Task.FromResult(DeserializeFromFile<T>(path));
    public Task<object> DeserializeFromFileAsync(Type type,string path)=>Task.FromResult(DeserializeFromString(File.ReadAllText(path),type));
    public void SerializeToFile(object value,string path)=>File.WriteAllText(path,SerializeToString(value));
    public void SerializeToFile(object value,string path,MediaBrowser.Model.Serialization.JsonSerializerOptions options)=>SerializeToFile(value,path);
    public void SerializeToStream(object value,Stream stream)=>JsonSerializer.Serialize(stream,value);
    public void SerializeToStream(object value,Stream stream,MediaBrowser.Model.Serialization.JsonSerializerOptions options)=>SerializeToStream(value,stream);
    public T DeserializeFromStream<T>(Stream stream)=>JsonSerializer.Deserialize<T>(stream);
    public object DeserializeFromStream(Stream stream,Type type)=>JsonSerializer.Deserialize(stream,type);
    public Task<T> DeserializeFromStreamAsync<T>(Stream stream)=>Task.FromResult(DeserializeFromStream<T>(stream));
    public Task<object> DeserializeFromStreamAsync(Stream stream,Type type)=>Task.FromResult(DeserializeFromStream(stream,type));
    public ReadOnlySpan<char> SerializeToSpan(object value)=>SerializeToString(value).AsSpan();
    public T DeserializeFromSpan<T>(ReadOnlySpan<char> value)=>DeserializeFromString<T>(value.ToString());
    public object DeserializeFromSpan(ReadOnlySpan<char> value,Type type)=>DeserializeFromString(value.ToString(),type);
    public T DeserializeFromBytes<T>(ReadOnlySpan<byte> value)=>JsonSerializer.Deserialize<T>(value);
    public object DeserializeFromBytes(ReadOnlySpan<byte> value,Type type)=>JsonSerializer.Deserialize(value,type);
    public void DeserializePartialJsonInto(string value,object target)=>throw new NotSupportedException();
}
