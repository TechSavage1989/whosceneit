using System.Net;
using System.Text;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using WhoSceneIt;
using WhoSceneIt.Deletion;
using WhoSceneIt.Integrations;

public static class ArrChecks
{
    public static async Task Run(string root, Action<string, Action> test, Func<string, Func<Task>, Task> testAsync, Action<bool, string> assert)
    {
        void Reject(Action action) { try { action(); } catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is ArrRequestException) { return; } throw new Exception("Invalid synchronization input was accepted."); }
        async Task<DeletionStatus> Done(ArrFixture f, DeletionStatus preview)
        {
            f.Manager.Execute("admin", preview.Token, true); var until = DateTimeOffset.UtcNow.AddSeconds(5); DeletionStatus result;
            do { await Task.Delay(10); result = f.Manager.Status("admin", preview.Token); } while (result.State == "Deleting" && DateTimeOffset.UtcNow < until);
            assert(result.State == "Completed", "Synchronization did not finish."); return result;
        }
        test("Disabled integrations use no external requests and keep the preview read-only", () =>
        {
            using var f = new ArrFixture(root); f.Settings.Radarr.Enabled = false;
            var p = f.Preview(); assert(p.Ready == 1 && p.Items[0].ExternalState == "Disabled" && f.Calls.Count == 0 && f.Backend.Deletes == 0 && File.Exists(f.File), "Disabled integrations contacted a service or changed media.");
            using var configured = new ArrFixture(root); var pending = configured.Preview();
            var cancelled = configured.Manager.Cancel("admin", pending.Token);
            assert(cancelled.Items[0].ExternalState == "Not run" && configured.Mutations.Count == 0 && File.Exists(configured.File), "Cancelling a preview left a misleading external outcome or changed media.");
        });
        await testAsync("Radarr synchronization verifies exact files, unmonitors first, and removes metadata without extra file deletion", async () =>
        {
            using var f = new ArrFixture(root);
            var p = f.Preview(); assert(p.Ready == 1 && p.Items[0].ExternalPath == f.RemotePath && f.Mutations.Count == 0 && f.Backend.Deletes == 0, "Preview was not a frozen read-only file match.");
            var done = await Done(f, p);
            assert(done.Deleted == 1 && done.Synchronized == 1 && done.SyncFailed == 0 && !File.Exists(f.File) && !f.RecordPresent, "Radarr synchronization was not completed.");
            assert(f.Mutations.Count == 2 && f.Mutations[0].method == "PUT" && f.Mutations[0].path.EndsWith("/movie/editor") && f.Mutations[1].path.Contains("deleteFiles=false&addImportExclusion=true"), "Radarr was asked to delete additional files or changed unrelated fields.");
            assert(f.FilePresentWhenUnmonitored && !f.FilePresentWhenRemoteDeleted && File.Exists(f.Backend.FileFor("2")), "Deletion order or unrelated file preservation failed.");
            f.Manager.Execute("admin", p.Token, true); assert(f.Backend.Deletes == 1 && f.Mutations.Count == 2, "A replay repeated deletion.");
        });
        await testAsync("Sonarr synchronization unmonitors every covered episode and removes only the matched file record", async () =>
        {
            using var f = new ArrFixture(root, sonarr: true); f.SharedFile = true; f.Backend.EpisodeEnd = 2;
            var p = f.Preview(); var done = await Done(f, p);
            assert(done.Deleted == 1 && done.Synchronized == 1 && f.SeriesPresent && !f.RecordPresent && !f.Monitored, "Sonarr did not retain the series while removing the file record.");
            assert(f.Mutations.Count == 2 && f.Mutations[0].body.Contains("\"episodeIds\":[301,302]") && f.Mutations[1].path.EndsWith("/episodefile/201") && !f.Mutations.Any(c => c.path.Contains("/series/")), "Sonarr deleted a series or failed to cover all episodes in the selected range.");
        });
        test("Sonarr blocks a shared file when it includes an episode outside the selected Emby range", () =>
        {
            using var f = new ArrFixture(root, sonarr: true); f.SharedFile = true;
            var p = f.Preview(); assert(p.Ready == 0 && p.Items[0].Message.Contains("outside the selected") && f.Mutations.Count == 0 && File.Exists(f.File), "An unselected episode shared by the file was exposed to deletion.");
        });
        test("Unavailable, ambiguous, unmatched, and different-size service records block media deletion", () =>
        {
            foreach (var condition in new[] { "unavailable", "wrong-app", "unmatched", "ambiguous", "same-record", "size" })
            {
                using var f = new ArrFixture(root); f.Condition = condition;
                var p = f.Preview("1", "2"); assert(p.Ready == 0 && f.Mutations.Count == 0 && f.Backend.Deletes == 0 && File.Exists(f.File), "A bad service match allowed deletion: " + condition);
                if (condition == "unavailable" || condition == "wrong-app") assert(f.Calls.Count == 1, "An unavailable or incorrect service was repeatedly contacted for every item in the batch.");
            }
        });
        await testAsync("Changed remote targets, native guards, and saved settings invalidate a confirmed selection", async () =>
        {
            using (var f = new ArrFixture(root)) { var p = f.Preview(); f.FileId = 999; var done = await Done(f, p); assert(done.Failed == 1 && f.Backend.Deletes == 0 && f.Mutations.Count == 0, "A changed service target was deleted."); }
            using (var f = new ArrFixture(root)) { var p = f.Preview(); f.Backend.Blocked.Add("1"); var done = await Done(f, p); assert(done.Skipped == 1 && f.Backend.Deletes == 0 && f.Mutations.Count == 0, "A newly blocked native item changed external monitoring."); }
            using (var f = new ArrFixture(root)) { var p = f.Preview(); f.OnUnmonitor = () => File.AppendAllText(f.File, "changed during the remote call"); var done = await Done(f, p); assert(done.Failed == 1 && f.Backend.Deletes == 0 && File.Exists(f.File) && f.RecordPresent && f.Mutations.Count == 1, "Native fingerprint revalidation was skipped after the remote call."); }
            using (var f = new ArrFixture(root)) { var p = f.Preview(); f.Settings.Radarr.ArrPathRoot = "/different"; var stopped = f.Manager.Execute("admin", p.Token, true); assert(stopped.State == "Cancelled" && !stopped.Started && stopped.Message.Contains("settings changed") && f.Backend.Deletes == 0 && f.Mutations.Count == 0, "A stale configuration started deletion or reported uncertain progress."); }
        });
        await testAsync("Lost monitoring and deletion receipts are verified by reads without replaying mutations", async () =>
        {
            foreach (var sonarr in new[] { false, true })
            {
                using var f = new ArrFixture(root, sonarr); f.LostPut = true; f.LostDelete = true;
                var done = await Done(f, f.Preview()); assert(done.Deleted == 1 && done.Synchronized == 1 && f.Mutations.Count == 2 && f.Backend.Deletes == 1, "A lost receipt caused replay or a false failure.");
            }
            using (var f = new ArrFixture(root)) { f.Backend.AfterDelete = () => f.MovieFileMissing = true; var done = await Done(f, f.Preview()); assert(done.Synchronized == 1 && !f.RecordPresent, "Radarr could not remove metadata after its file record had already been refreshed."); }
        });
        await testAsync("External failure after native deletion reports partial success without a second media deletion", async () =>
        {
            using var f = new ArrFixture(root); f.FailDelete = true;
            var done = await Done(f, f.Preview());
            assert(done.Deleted == 1 && done.Failed == 0 && done.SyncFailed == 1 && done.Synchronized == 0 && done.Items[0].EmbyState == "Deleted" && done.Items[0].ExternalState == "Failed" && !File.Exists(f.File) && f.RecordPresent, "Partial success was falsely reported as complete or media was restored.");
            assert(f.Mutations.Count(c => c.method == "DELETE") == 1, "A failed synchronization was automatically replayed.");
        });
        await testAsync("A partial native deletion never triggers external cleanup or claims confirmed Emby success", async () =>
        {
            using var f = new ArrFixture(root); f.Backend.FailAfterDelete = "1";
            var done = await Done(f, f.Preview()); assert(done.Failed == 1 && done.Deleted == 0 && done.Items[0].EmbyState == "Unknown" && f.RecordPresent && f.Mutations.Count == 1, "Native uncertainty was hidden or external deletion ran afterward.");
        });
        await testAsync("Sonarr cleanup preserves a replacement file appearing after native deletion", async () =>
        {
            using var f = new ArrFixture(root, sonarr: true); f.Backend.AfterDelete = () => File.WriteAllText(f.File, "replacement file");
            var done = await Done(f, f.Preview()); assert(done.SyncFailed == 1 && File.ReadAllText(f.File) == "replacement file" && f.Mutations.All(c => c.method != "DELETE"), "A replacement file was submitted to Sonarr for deletion.");
        });
        test("Connection checks support Sonarr v5, preserve URL bases, reject redirects, and never disclose keys", () =>
        {
            using var f = new ArrFixture(root, sonarr: true); f.V5 = true;
            var c = new ArrClient("Sonarr", f.Settings.Sonarr, new TestJson(), f.Http); var result = c.Connect();
            assert(result.ApiVersion == "v5" && f.Calls.Count == 2 && f.Calls.All(x => x.path.StartsWith("/sonarr/api/")) && f.Mutations.Count == 0, "API discovery changed data or ignored the URL base.");
            using var redirect = new HttpClient(new FakeHttp(r => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://other.invalid/") } })));
            try { new ArrClient("Sonarr", f.Settings.Sonarr, new TestJson(), redirect).Connect(); throw new Exception("A redirect was followed."); }
            catch (ArrRequestException e) { assert(e.Message.Contains("Redirects are not followed") && !e.Message.Contains(ArrFixture.Key), "An unsafe redirect or secret disclosure occurred."); }
            Reject(() => ArrClient.ValidateBaseUrl("https://user:password@fixture.invalid/")); Reject(() => ArrClient.ValidateBaseUrl("https://fixture.invalid/?apikey=secret"));
            assert(!JsonSerializer.Serialize(ArrSettingsView.From(f.Settings)).Contains(ArrFixture.Key), "The settings view exposed a stored API key.");
        });
        test("Path mapping respects Windows, UNC, POSIX case, path boundaries, and traversal rejection", () =>
        {
            var c = new ArrConnection { EmbyPathRoot = @"D:\Media\Movies", ArrPathRoot = "/movies" };
            assert(ArrPath.Translate(@"d:\media\movies\Title\film.mkv", c) == "/movies/Title/film.mkv", "Windows mapping failed.");
            c.EmbyPathRoot = @"\\NAS\Media"; assert(ArrPath.Translate(@"\\nas\media\TV\a.mkv", c) == "/movies/TV/a.mkv", "UNC mapping failed.");
            assert(!ArrPath.Equal("/Movies/a.mkv", "/movies/a.mkv"), "POSIX paths ignored case.");
            c.EmbyPathRoot = "/media/tv"; Reject(() => ArrPath.Translate("/media/tv-other/a.mkv", c)); Reject(() => ArrPath.Normalize("/media/tv/../other/file.mkv"));
            c.EmbyPathRoot = "C:/"; assert(ArrPath.Translate("C:/TV/a.mkv", c) == "/movies/TV/a.mkv", "Drive-root mapping failed.");
            c.EmbyPathRoot = "/"; assert(ArrPath.Translate("/TV/a.mkv", c) == "/movies/TV/a.mkv", "POSIX-root mapping failed.");
        });
        test("Native plugin settings persist keys, preserve blank keys, clear explicitly, and block edits during deletion", () =>
        {
            var folder = Path.Combine(root, "arr-settings"); Directory.CreateDirectory(folder);
            var paths = Proxy.Create<IApplicationPaths>((m, a) => folder);
            var serializer = Proxy.Create<IXmlSerializer>((m, a) => {
                if (m.Name == "SerializeToFile") { using var stream = File.Create((string)a[1]); new System.Xml.Serialization.XmlSerializer(a[0].GetType()).Serialize(stream, a[0]); }
                if (m.Name == "DeserializeFromFile") { using var stream = File.OpenRead((string)a[1]); return new System.Xml.Serialization.XmlSerializer((Type)a[0]).Deserialize(stream); }
                return null;
            });
            Plugin Create() { var p = new Plugin(paths, serializer); p.SetStartupInfo(path => { Directory.CreateDirectory(path); }); p.SetAttributes(typeof(Plugin).Assembly.Location, folder, typeof(Plugin).Assembly.GetName().Version); return p; }
            var plugin = Create();
            var enabled = new ArrConnectionUpdate { Enabled = true, BaseUrl = "https://fixture.invalid/radarr", ApiKey = ArrFixture.Key };
            var disabled = new ArrConnectionUpdate();
            try
            {
                ArrSettingsStore.Save(disabled, enabled); var stored = ArrSettingsStore.Read(); assert(stored.Radarr.ApiKey == ArrFixture.Key, "The key was not stored.");
                assert(!JsonSerializer.Serialize(new ArrService(new TestJson()).Get(new GetArrSettings())).Contains(ArrFixture.Key), "The native settings endpoint exposed the stored key.");
                plugin = Create(); assert(ArrSettingsStore.Read().Radarr.ApiKey == ArrFixture.Key, "The key did not survive native XML reload.");
                enabled.ApiKey = ""; ArrSettingsStore.Save(disabled, enabled); assert(ArrSettingsStore.Read().Radarr.ApiKey == ArrFixture.Key, "A blank edit erased the key.");
                using (ArrSettingsStore.BeginOperation(ArrSettingsStore.Read().Key())) Reject(() => ArrSettingsStore.Save(disabled, disabled));
                enabled.ClearApiKey = true; Reject(() => ArrSettingsStore.Save(disabled, enabled));
                enabled.Enabled = false; ArrSettingsStore.Save(disabled, enabled); assert(ArrSettingsStore.Read().Radarr.ApiKey == "", "Explicit clearing retained the key.");
                Reject(() => new ArrConnectionUpdate { Enabled = true, BaseUrl = "https://fixture.invalid", ApiKey = ArrFixture.Key, EmbyPathRoot = "/media" }.Merge(new ArrConnection()));
            }
            finally { plugin.Configuration.Arr = new ArrSettings(); plugin.SaveConfiguration(); }
        });
    }
}

public sealed class ArrFixture : IDisposable
{
    public const string Key = "fixture-only-api-key";
    public ArrSettings Settings = new();
    public ArrBackend Backend;
    public DeletionManager Manager;
    public HttpClient Http;
    public List<(string method, string path, string body)> Calls = new();
    public List<(string method, string path, string body)> Mutations => Calls.Where(c => c.method != "GET").ToList();
    public bool Sonarr, SharedFile, V5, LostPut, LostDelete, FailDelete, MovieFileMissing;
    public Action OnUnmonitor;
    public bool Monitored = true, RecordPresent = true, SeriesPresent = true;
    public bool FilePresentWhenUnmonitored, FilePresentWhenRemoteDeleted;
    public int FileId = 201;
    public string Condition;
    public string File => Backend.FileFor("1");
    public string RemotePath => (Sonarr ? "/tv/Show/" : "/movies/") + Path.GetFileName(File);
    private long Size => new FileInfo(File).Exists ? new FileInfo(File).Length : _size;
    private readonly long _size;
    public ArrFixture(string root, bool sonarr = false)
    {
        Sonarr = sonarr; Backend = new ArrBackend(root, sonarr); _size = new FileInfo(File).Length;
        var connection = new ArrConnection { Enabled = true, BaseUrl = "https://fixture.invalid/" + (sonarr ? "sonarr/" : "radarr/"), ApiKey = Key,
            EmbyPathRoot = Path.GetDirectoryName(File), ArrPathRoot = sonarr ? "/tv/Show" : "/movies", AddImportExclusion = true };
        if (sonarr) Settings.Sonarr = connection; else Settings.Radarr = connection;
        Http = new HttpClient(new FakeHttp(Respond));
        Manager = new DeletionManager(Backend, synchronization: new ArrSynchronization(() => Settings, new TestJson(), Http));
    }
    public DeletionStatus Preview(params string[] ids) => Manager.Preview("admin", "alice", ids.Length == 0 ? new[] { "1" } : ids);
    private async Task<HttpResponseMessage> Respond(HttpRequestMessage request)
    {
        var method = request.Method.Method; var path = request.RequestUri.PathAndQuery;
        var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(); Calls.Add((method, path, body));
        if (!request.Headers.TryGetValues("X-Api-Key", out var keys) || keys.Single() != Key) throw new Exception("Missing fixture API header.");
        HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
        HttpResponseMessage Missing() => new(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
        var route = path[(path.IndexOf("/api/", StringComparison.Ordinal) + 8)..];
        if (path.EndsWith("/system/status"))
        {
            if (Condition == "unavailable") return new(HttpStatusCode.ServiceUnavailable);
            if (V5 && path.Contains("/v3/")) return Missing();
            return Json(new { appName = Condition == "wrong-app" ? "Unexpected application" : Sonarr ? "Sonarr" : "Radarr", version = V5 ? "5.0.0" : "4.0.0" });
        }
        ArrFile EpisodeFile() => new() { id = FileId, seriesId = 10, path = RemotePath, size = _size };
        ArrMovie Movie() => new() { id = 101, tmdbId = 1001, path = "/movies", monitored = Monitored, movieFile = MovieFileMissing ? null : new ArrFile { id = FileId, path = RemotePath, size = Condition == "size" ? _size + 1 : _size } };
        ArrEpisode[] Episodes() => (SharedFile ? new[] { 1, 2 } : new[] { 1 }).Select(n => new ArrEpisode { id = 300 + n, seriesId = 10, episodeFileId = RecordPresent ? FileId : 0, seasonNumber = 1, episodeNumber = n, monitored = Monitored }).ToArray();
        if (method == "GET")
        {
            if (route == "movie") return Json(Condition == "unmatched" ? Array.Empty<ArrMovie>() : Condition == "ambiguous" ? new[] { Movie(), Movie() } : Condition == "same-record" ? new[] { Movie(), new ArrMovie { id = 101, tmdbId = 1001, path = "/movies", monitored = true, movieFile = new ArrFile { id = 202, path = "/movies/" + Path.GetFileName(Backend.FileFor("2")), size = _size } } } : new[] { Movie() });
            if (route == "movie/101") return RecordPresent ? Json(Movie()) : Missing();
            if (route == "series") return Json(new[] { new ArrSeries { id = 10, path = "/tv/Show" } });
            if (route == "series/10") return Json(new ArrSeries { id = 10, path = "/tv/Show" });
            if (route == "episodefile?seriesId=10") return Json(new[] { EpisodeFile() });
            if (route == "episodefile/201") return RecordPresent ? Json(EpisodeFile()) : Missing();
            if (route.StartsWith("episode?")) return Json(Episodes());
        }
        if (method == "PUT" && (route == "movie/editor" || route == "episode/monitor"))
        {
            FilePresentWhenUnmonitored = System.IO.File.Exists(File); Monitored = false;
            OnUnmonitor?.Invoke();
            if (LostPut) { LostPut = false; throw new HttpRequestException("Lost fixture receipt"); }
            return Json(new { });
        }
        if (method == "DELETE" && (route.StartsWith("movie/101?") || route == "episodefile/201"))
        {
            FilePresentWhenRemoteDeleted = System.IO.File.Exists(File);
            if (FailDelete) return new(HttpStatusCode.ServiceUnavailable);
            RecordPresent = false;
            if (LostDelete) { LostDelete = false; return new(HttpStatusCode.ServiceUnavailable); }
            return Json(new { });
        }
        throw new Exception("Unexpected fixture request: " + method + " " + path + " parsed as " + route);
    }
    public void Dispose() { Http.Dispose(); }
}

public sealed class ArrBackend : IDeletionBackend
{
    private readonly DeletionFixture _native;
    private readonly bool _episode;
    public int? EpisodeEnd;
    public Action AfterDelete;
    public string FailAfterDelete { set => _native.FailAfterDelete = value; }
    public HashSet<string> Blocked => _native.Blocked;
    public int Deletes => _native.Deletes;
    public ArrBackend(string root, bool episode) { _native = new DeletionFixture(root); _episode = episode; }
    public string FileFor(string id) => _native.FileFor(id);
    public string[] Resolve(string user, string[] ids, bool allowUnwatched = false) => ids;
    public void EnsureAvailable() { }
    public DeletionInspection Inspect(string actor, string user, string id, bool allowUnwatched = false)
    {
        var item = _native.Inspect(actor, user, id, allowUnwatched); item.MediaPath = FileFor(id); item.MediaSize = new FileInfo(FileFor(id)).Length;
        item.Item.Type = _episode ? "Episode" : "Movie"; item.SeasonNumber = 1; item.EpisodeNumber = 1; item.EpisodeNumberEnd = EpisodeEnd; return item;
    }
    public void Delete(string actor, string user, string id, string expected, bool allowUnwatched = false) { _native.Delete(actor, user, id, expected, allowUnwatched); AfterDelete?.Invoke(); }
}
