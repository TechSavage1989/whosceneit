using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using MediaBrowser.Common;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using WhoSceneIt;
using WhoSceneIt.Deletion;

public static class DeletionChecks
{
    public static async Task Run(string root, Action<string, Action> test, Func<string, Func<Task>, Task> testAsync, Action<bool, string> assert)
    {
        void Reject(Action action)
        {
            try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; }
            throw new Exception("Unsafe deletion request was accepted.");
        }
        async Task<DeletionStatus> Done(DeletionManager manager, string token)
        {
            var end = DateTimeOffset.UtcNow.AddSeconds(5);
            while (DateTimeOffset.UtcNow < end)
            {
                var state = manager.Status("admin", token);
                if (state.State != "Deleting") return state;
                await Task.Delay(10);
            }
            throw new Exception("Deletion did not finish.");
        }
        test("Deletion previews are non-destructive and identify blocked media", () =>
        {
            var backend = new DeletionFixture(root); backend.Blocked.Add("2");
            var manager = new DeletionManager(backend); var preview = manager.Preview("admin", "user", new[] { "1", "1", "2" });
            assert(preview.Ready == 1 && preview.Skipped == 1 && preview.Total == 2, "Preview did not deduplicate or show blocked media.");
            assert(backend.Deletes == 0 && File.Exists(backend.FileFor("1")), "A preview removed a file.");
            // Returned DTOs cannot alter the frozen operation stored by the server.
            preview.Items[0].Paths[0] = "another-file"; preview.Items[0].State = "Deleted";
            assert(manager.Status("admin", preview.Token).Items[0].State == "Ready", "The preview leaked mutable internal state.");
        });
        test("Deletion rejects invalid selections, wrong administrators, and missing confirmation", () =>
        {
            var backend = new DeletionFixture(root); var manager = new DeletionManager(backend);
            Reject(() => manager.Preview("admin", "user", Array.Empty<string>()));
            Reject(() => manager.Preview("admin", "user", new[] { "../1" }));
            Reject(() => manager.Preview("admin", "user", Enumerable.Range(1, 501).Select(x => x.ToString()).ToArray()));
            var preview = manager.Preview("admin", "user", new[] { "1" });
            Reject(() => manager.Execute("other-admin", preview.Token, "DELETE"));
            Reject(() => manager.Status("other-admin", preview.Token));
            Reject(() => manager.Execute("admin", preview.Token, "delete"));
            Reject(() => manager.Execute("admin", "unknown", "DELETE"));
            assert(backend.Deletes == 0, "An unconfirmed request deleted media.");
        });
        await testAsync("Confirmed single and bulk deletions use real files and replay only once", async () =>
        {
            var backend = new DeletionFixture(root); var manager = new DeletionManager(backend);
            var preview = manager.Preview("admin", "user", new[] { "1", "2" });
            manager.Execute("admin", preview.Token, "DELETE"); manager.Execute("admin", preview.Token, "DELETE");
            var done = await Done(manager, preview.Token); manager.Execute("admin", preview.Token, "DELETE");
            assert(done.Deleted == 2 && backend.Deletes == 2 && !File.Exists(backend.FileFor("1")) && !File.Exists(backend.FileFor("2")), "Confirmed bulk deletion or replay protection failed.");
            assert(File.ReadAllText(backend.Settings) == "keep settings", "Media deletion changed settings.");
        });
        await testAsync("Deletion rechecks changed files, watch status, and playback before removing media", async () =>
        {
            var backend = new DeletionFixture(root); var manager = new DeletionManager(backend);
            var preview = manager.Preview("admin", "user", new[] { "1", "2", "3" });
            File.AppendAllText(backend.FileFor("1"), "changed after preview"); backend.Blocked.Add("2");
            manager.Execute("admin", preview.Token, "DELETE"); var done = await Done(manager, preview.Token);
            assert(done.Deleted == 1 && done.Skipped == 2 && backend.Deletes == 1, "A changed or newly blocked file was deleted.");
            assert(File.Exists(backend.FileFor("1")) && File.Exists(backend.FileFor("2")), "Skipped files did not survive.");
        });
        await testAsync("Deletion reports partial failures without claiming a rollback", async () =>
        {
            var backend = new DeletionFixture(root) { FailAfterDelete = "2" }; var manager = new DeletionManager(backend);
            var preview = manager.Preview("admin", "user", new[] { "1", "2", "3" });
            manager.Execute("admin", preview.Token, "DELETE"); var done = await Done(manager, preview.Token);
            assert(done.Deleted == 2 && done.Failed == 1 && done.Items.Single(i => i.ItemId == "2").Message.Contains("some files may already be removed"), "Partial native deletion was misreported.");
        });
        await testAsync("Cancellation stops remaining deletions and concurrent jobs cannot start", async () =>
        {
            using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
            var backend = new DeletionFixture(root) { Entered = entered, Release = release }; var manager = new DeletionManager(backend);
            var preview = manager.Preview("admin", "user", new[] { "1", "2", "3" });
            var other = manager.Preview("admin", "user", new[] { "3" });
            try
            {
                manager.Execute("admin", preview.Token, "DELETE");
                assert(await Task.Run(() => entered.Wait(3000)), "The first deletion did not start.");
                Reject(() => manager.Execute("admin", other.Token, "DELETE"));
                manager.Cancel("admin", preview.Token);
            }
            finally { release.Set(); }
            var done = await Done(manager, preview.Token);
            assert(done.State == "Cancelled" && done.Deleted == 1 && done.Skipped == 2, "Cancellation removed later files.");
            assert(File.Exists(backend.FileFor("2")) && File.Exists(backend.FileFor("3")), "Cancelled files were removed.");
        });
        test("Expired and cancelled previews never become destructive requests", () =>
        {
            var now = DateTimeOffset.UtcNow; var backend = new DeletionFixture(root); var manager = new DeletionManager(backend, () => now);
            var preview = manager.Preview("admin", "user", new[] { "1" }); now = now.AddMinutes(11);
            Reject(() => manager.Execute("admin", preview.Token, "DELETE"));
            for (var i = 0; i < 12; i++) { var next = manager.Preview("admin", "user", new[] { "1" }); manager.Cancel("admin", next.Token); manager.Execute("admin", next.Token, "DELETE"); }
            assert(backend.Deletes == 0, "A cancelled or expired preview removed media.");
        });
        test("Native Emby series selection expands only watched episodes and removes duplicate IDs", () =>
        {
            var user = new User { InternalId = 4, Name = "Alice", Policy = new MediaBrowser.Model.Users.UserPolicy() };
            var series = new Series { InternalId = 10, Name = "Show" };
            var one = new Episode { InternalId = 11, Name = "Watched episode" };
            var two = new Episode { InternalId = 12, Name = "Unwatched episode" };
            var users = Proxy.Create<IUserManager>((m, a) => m.Name == "GetUserById" ? user : null);
            var data = Proxy.Create<IUserDataManager>((m, a) => m.Name == "GetUserData" ? new UserItemData { Played = ((BaseItem)a[1]).InternalId == 11 } : null);
            var library = Proxy.Create<ILibraryManager>((m, a) => m.Name == "GetItemById" ? series : m.Name == "GetItemList" ? new BaseItem[] { one, one, two } : null);
            var backend = new EmbyDeletionBackend(users, data, library, null, null);
            assert(backend.Resolve("alice", new[] { "10" }).SequenceEqual(new[] { "11" }), "A series included unwatched or duplicate episodes.");
        });
        test("Native deletion respects revoked admin access and rejects non-media folders", () =>
        {
            var user = new User { InternalId = 4, Name = "Admin" };
            var allow = false;
            var users = Proxy.Create<IUserManager>((m, a) => m.Name == "GetUserById" ? user : m.Name == "GetUserPolicy" ? new MediaBrowser.Model.Users.UserPolicy { IsAdministrator = allow } : null);
            var library = Proxy.Create<ILibraryManager>((m, a) => m.Name == "GetItemById" ? new Series { InternalId = 10, Name = "Show folder" } : null);
            var backend = new EmbyDeletionBackend(users, null, library, null, null);
            assert(backend.Inspect("admin", null, "10").Item.State == "Blocked", "A non-admin deletion was allowed.");
            allow = true;
            assert(backend.Inspect("admin", null, "10").Item.State == "Blocked", "A series folder was treated as deletable media.");
        });
        test("Native Emby adapter rechecks watch status, playback, deletion support, and shared folders", () =>
        {
            var fixture = new NativeDeletionFixture(root);
            assert(fixture.Backend.Inspect("admin", "alice", "1").Item.State == "Ready", "An eligible native movie was blocked.");
            fixture.Played = false;
            assert(fixture.Backend.Inspect("admin", "alice", "1").Item.State == "Blocked", "Unwatched media was eligible for deletion.");
            fixture.Played = true; fixture.Playing = true;
            assert(fixture.Backend.Inspect("admin", "alice", "1").Item.State == "Blocked", "Playing media was eligible for deletion.");
            fixture.SessionItemId = fixture.Movie.Id.ToString("N");
            assert(fixture.Backend.Inspect("admin", "alice", "1").Item.State == "Blocked", "A GUID-based playback session was not recognized.");
            fixture.Playing = false; fixture.Movie.AllowDelete = false;
            assert(fixture.Backend.Inspect("admin", "alice", "1").Item.State == "Blocked", "Native deletion support was ignored.");
            fixture.Movie.AllowDelete = true; fixture.Admin.Policy.EnableContentDeletion = false;
            assert(fixture.Backend.Inspect("admin", "alice", "1").Item.State == "Blocked", "Emby's content-deletion permission was ignored.");
            fixture.Admin.Policy.EnableContentDeletion = true; fixture.Movie.DeleteDirectory = true; fixture.SiblingMedia = true;
            assert(fixture.Backend.Inspect("admin", "alice", "1").Item.State == "Blocked", "A folder containing other library media was eligible for deletion.");
            fixture.SiblingMedia = false; fixture.LibraryRoot = true;
            assert(fixture.Backend.Inspect("admin", "alice", "1").Item.State == "Blocked", "A configured library root was eligible for deletion.");
            assert(File.Exists(fixture.Movie.Path) && fixture.NativeDeletes == 0, "A blocked preview changed media.");
        });
        test("Saved partial playback and rewatches for any user block native media deletion", () =>
        {
            var fixture = new NativeDeletionFixture(root);
            fixture.Position = 100;
            assert(fixture.Backend.Inspect("admin", "alice", "1").Item.State == "Blocked", "A watched item being rewatched was deletable.");
            fixture.Position = 0; fixture.OtherPosition = 100;
            assert(fixture.Backend.Inspect("admin", "alice", "1").Item.State == "Blocked", "Another user's partial playback was ignored.");
            fixture.OtherPosition = 0;
            var preview = fixture.Backend.Inspect("admin", "alice", "1");
            fixture.OtherPosition = 100;
            Reject(() => fixture.Backend.Delete("admin", "alice", "1", preview.Fingerprint));
            assert(fixture.NativeDeletes == 0 && File.Exists(fixture.Movie.Path), "New progress after preview did not preserve the media.");
        });
        test("Native Emby delete uses file-deletion options only after a matching path preview", () =>
        {
            var fixture = new NativeDeletionFixture(root);
            var preview = fixture.Backend.Inspect("admin", "alice", "1");
            File.AppendAllText(fixture.Movie.Path, "changed");
            Reject(() => fixture.Backend.Delete("admin", "alice", "1", preview.Fingerprint));
            assert(fixture.NativeDeletes == 0, "A changed native media target was deleted.");
            preview = fixture.Backend.Inspect("admin", "alice", "1");
            fixture.Backend.Delete("admin", "alice", "1", preview.Fingerprint);
            assert(fixture.NativeDeletes == 1 && fixture.Options.DeleteFileLocation && !fixture.Options.DeleteFromExternalProvider && !File.Exists(fixture.Movie.Path), "The native media deletion contract was not followed.");
        });
    }
}

public sealed class NativeDeletionFixture
{
    public bool Played = true, Playing, SiblingMedia, LibraryRoot;
    public long Position, OtherPosition;
    public int NativeDeletes;
    public DeleteOptions Options;
    public NativeDeletionMovie Movie;
    public User Admin;
    public string SessionItemId = "1";
    public EmbyDeletionBackend Backend;
    public IUserManager Users;
    public IUserDataManager Data;
    public ILibraryManager Library;
    public ISessionManager Sessions;
    public IApplicationHost Host;
    public NativeDeletionFixture(string root)
    {
        // DispatchProxy cannot box ReadOnlySpan<char>. Provide the SDK's protocol
        // boundary with a minimal typed stub; all deletion policy/path code remains real.
        BaseItem.MediaSourceManager = FileProtocolStub();
        var directory = Path.Combine(root, "native-media-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "movie.mkv"); File.WriteAllText(file, "synthetic media");
        Movie = new NativeDeletionMovie { InternalId = 1, Id = Guid.NewGuid(), Name = "Native movie", Path = file };
        var admin = Admin = new User { InternalId = 5, Name = "Admin", Policy = new MediaBrowser.Model.Users.UserPolicy { IsAdministrator = true, EnableContentDeletion = true } };
        var user = new User { InternalId = 4, Name = "Alice", Policy = new MediaBrowser.Model.Users.UserPolicy() };
        var users = Proxy.Create<IUserManager>((m, a) => m.Name switch {
            "GetUserById" => a[0] is long id ? (id == 5 ? admin : user) : (string)a[0] == "admin" ? admin : user,
            "GetUserPolicy" => ((User)a[0]).Policy,
            "GetUserDto" => new MediaBrowser.Model.Dto.UserDto { Id = ((User)a[0]).InternalId == 5 ? "admin" : ((User)a[0]).InternalId == 4 ? "alice" : "other" },
            "GetUserList" => new[] { user, admin }, _ => m.ReturnType == typeof(MediaBrowser.Model.Dto.UserItemShareLevel) ? Enum.GetValues<MediaBrowser.Model.Dto.UserItemShareLevel>().Max() : null });
        var data = Proxy.Create<IUserDataManager>((m, a) => m.Name == "GetUserData" ? new UserItemData { Played = Played, PlaybackPositionTicks = ((User)a[0]).InternalId == 4 ? Position : OtherPosition } : null);
        var library = Proxy.Create<ILibraryManager>((m, a) => {
            if (m.Name == "GetItemById") return Movie;
            if (m.Name == "GetShareLevels") return new MediaBrowser.Model.Querying.QueryResult<MediaBrowser.Model.Dto.UserItemShareLevel?> { Items = new MediaBrowser.Model.Dto.UserItemShareLevel?[] { Enum.GetValues<MediaBrowser.Model.Dto.UserItemShareLevel>().Max() }, TotalRecordCount = 1 };
            if (m.Name == "GetCollectionFolders") return Array.Empty<Folder>();
            if (m.Name == "FilterItemsToIdsForUser") return new[] { 1L };
            if (m.Name == "GetVirtualFolders") return new List<MediaBrowser.Model.Entities.VirtualFolderInfo> { new() { Locations = LibraryRoot ? new[] { directory } : Array.Empty<string>() } };
            if (m.Name == "GetItemList") return SiblingMedia ? new BaseItem[] { new Movie { InternalId = 2 } } : Array.Empty<BaseItem>();
            if (m.Name == "DeleteItem") { Options = (DeleteOptions)a[1]; NativeDeletes++; File.Delete(Movie.Path); }
            if (m.ReturnType == typeof(MediaBrowser.Model.Dto.UserItemShareLevel)) return Enum.GetValues<MediaBrowser.Model.Dto.UserItemShareLevel>().Max();
            return null;
        });
        BaseItem.LibraryManager = library; BaseItem.UserManager = users;
        var sessions = Proxy.Create<ISessionManager>((m, a) => {
            if (m.Name != "get_Sessions") return null;
            if (!Playing) return Array.Empty<SessionInfo>();
            var session = new SessionInfo(null, Proxy.Create<MediaBrowser.Model.Logging.ILogger>((n, b) => null));
            var progress = new MediaBrowser.Model.Session.PlaybackProgressInfo { PlaySessionId = "deletion-fixture" };
            session.GetOrAddPlaySessionInfo(progress).NowPlayingItem = new MediaBrowser.Model.Dto.BaseItemDto { Id = SessionItemId }; session.RegisterPlaySession(progress);
            return new[] { session };
        });
        var host = Proxy.Create<IApplicationHost>((m, a) => m.Name == "get_IsStartupComplete" ? true : null);
        Users = users; Data = data; Library = library; Sessions = sessions; Host = host;
        Backend = new EmbyDeletionBackend(users, data, library, sessions, host);
    }
    private static IMediaSourceManager FileProtocolStub()
    {
        var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("FileProtocolFixture" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run).DefineDynamicModule("Fixture");
        var type = module.DefineType("FileProtocolSource", TypeAttributes.Public | TypeAttributes.Class);
        type.AddInterfaceImplementation(typeof(IMediaSourceManager)); type.DefineDefaultConstructor(MethodAttributes.Public);
        foreach (var method in typeof(IMediaSourceManager).GetMethods())
        {
            var stub = type.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                method.ReturnType, method.GetParameters().Select(p => p.ParameterType).ToArray());
            var il = stub.GetILGenerator();
            if (method.Name == "GetPathProtocol") il.Emit(OpCodes.Ldc_I4, (int)MediaBrowser.Model.MediaInfo.MediaProtocol.File);
            else if (method.ReturnType != typeof(void))
            {
                if (method.ReturnType.IsValueType) { var value = il.DeclareLocal(method.ReturnType); il.Emit(OpCodes.Ldloca, value); il.Emit(OpCodes.Initobj, method.ReturnType); il.Emit(OpCodes.Ldloc, value); }
                else il.Emit(OpCodes.Ldnull);
            }
            il.Emit(OpCodes.Ret); type.DefineMethodOverride(stub, method);
        }
        return (IMediaSourceManager)Activator.CreateInstance(type.CreateType());
    }
}
public sealed class NativeDeletionMovie : Movie
{
    public bool AllowDelete = true, DeleteDirectory;
    public override bool CanDelete() => AllowDelete;
    public override List<FileSystemMetadata> GetDeletePaths(bool mediaFilesOnly, BaseItem[] collectionFolders)
    {
        var info = new FileInfo(Path);
        return new List<FileSystemMetadata> { new() { FullName = DeleteDirectory ? System.IO.Path.GetDirectoryName(Path) : Path,
            IsDirectory = DeleteDirectory, Exists = info.Exists, Length = info.Exists ? info.Length : 0, LastWriteTimeUtc = info.Exists ? info.LastWriteTimeUtc : DateTimeOffset.MinValue } };
    }
}

public sealed class DeletionFixture : IDeletionBackend
{
    private readonly string _root;
    private int _deletes;
    public HashSet<string> Blocked = new();
    public string FailAfterDelete, Settings;
    public ManualResetEventSlim Entered, Release;
    public int Deletes => Volatile.Read(ref _deletes);
    public DeletionFixture(string root)
    {
        _root = Path.Combine(root, "media-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root);
        foreach (var id in new[] { "1", "2", "3" }) File.WriteAllText(FileFor(id), "synthetic test media " + id);
        Settings = Path.Combine(_root, "settings.xml"); File.WriteAllText(Settings, "keep settings");
    }
    public string FileFor(string id) => Path.Combine(_root, "movie-" + id + ".mkv");
    public string[] Resolve(string userId, string[] roots) => roots;
    public void EnsureAvailable() { }
    public DeletionInspection Inspect(string actorId, string userId, string id)
    {
        var path = FileFor(id);
        return new DeletionInspection { Item = new DeletionItem { ItemId = id, Name = "Movie " + id, Type = "Movie", Paths = new[] { path }, WatchedBy = new[] { "Alice" }, State = Blocked.Contains(id) ? "Blocked" : "Ready", Message = Blocked.Contains(id) ? "Watch status, permission, or playback changed." : null },
            Fingerprint = File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "missing" };
    }
    public void Delete(string actorId, string userId, string id, string expected)
    {
        Entered?.Set(); if (Release != null && !Release.Wait(5000)) throw new Exception("Test did not release deletion.");
        if (Inspect(actorId, userId, id).Fingerprint != expected) throw new IOException("Fixture changed.");
        Interlocked.Increment(ref _deletes); File.Delete(FileFor(id));
        if (FailAfterDelete == id) throw new IOException("Injected partial native deletion failure.");
    }
}
