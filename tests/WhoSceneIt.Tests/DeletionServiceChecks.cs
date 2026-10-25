using System.Reflection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using WhoSceneIt.Deletion;

public static class DeletionServiceChecks
{
    public static async Task Run(string root, Action<string, Action> test, Func<string, Func<Task>, Task> testAsync, Action<bool, string> assert)
    {
        (DeletionService Service, NativeDeletionFixture Native, Action<AuthorizationInfo> SetAuth) Fixture()
        {
            typeof(DeletionService).GetField("_manager", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            var native = new NativeDeletionFixture(root);
            var request = Proxy.Create<IRequest>((m, a) => null);
            var info = new AuthorizationInfo { User = native.Admin };
            var auth = Proxy.Create<IAuthorizationContext>((m, a) => { assert(ReferenceEquals(a[0], request), "Authorization was not bound to the actual request."); return info; });
            var service = new DeletionService(auth, native.Users, native.Data, native.Library, native.Sessions, native.Host, new TestJson(), Proxy.Create<MediaBrowser.Common.Configuration.IApplicationPaths>((m, a) => m.Name == "get_DataPath" ? Path.Combine(root, "service-protection") : null)) { Request = request };
            return (service, native, next => info = next);
        }
        test("Native deletion service binds the authenticated administrator request before read-only preview", () =>
        {
            var f = Fixture();
            var preview = (DeletionStatus)f.Service.Post(new PreviewDeletion { UserId = "alice", ItemIds = "1" });
            assert(preview.State == "Ready" && preview.Ready == 1 && !preview.Started && preview.Token != null, "The authenticated service could not preview: " + preview.Message);
            assert(f.Native.NativeDeletes == 0 && File.Exists(f.Native.Movie.Path), "The preview deleted media.");
            f.Service.Post(new CancelDeletion { Token = preview.Token });
        });
        test("Deletion preview rejects missing, API-key-only, and non-admin request identities without mutation", () =>
        {
            var f = Fixture();
            foreach (var auth in new[] { new AuthorizationInfo(), new AuthorizationInfo { User = new User { InternalId = 4, Policy = new MediaBrowser.Model.Users.UserPolicy() } } })
            {
                f.SetAuth(auth);
                var result = (DeletionStatus)f.Service.Post(new PreviewDeletion { UserId = "admin", ItemIds = "1" });
                assert(result.State == "PreviewFailed" && result.Token == null && !result.Started, "A report user ID supplied actor privileges.");
            }
            f.Service.Request = null;
            var missing = (DeletionStatus)f.Service.Post(new PreviewDeletion { ItemIds = "1" });
            assert(missing.State == "PreviewFailed" && missing.Message.Contains("request context"), "A missing request became a deletion plan.");
            assert(f.Native.NativeDeletes == 0 && File.Exists(f.Native.Movie.Path), "Rejected authorization changed media.");
        });
        await testAsync("Real deletion service preview, confirmation, polling, and replay preserve the native contract", async () =>
        {
            var f = Fixture();
            f.Native.Played = false; f.Native.Position = 100;
            var guarded = (DeletionStatus)f.Service.Post(new PreviewDeletion { UserId = "alice", ItemIds = "1" });
            assert(guarded.Ready == 0 && !guarded.AllowUnwatched, "The service implicitly enabled unfinished deletion.");
            var preview = (DeletionStatus)f.Service.Post(new PreviewDeletion { UserId = "alice", ItemIds = "1", AllowUnwatched = true });
            assert(preview.Ready == 1 && preview.AllowUnwatched, "The service did not pass the explicit watch-state option to the native adapter.");
            try { f.Service.Post(new ExecuteDeletion { Token = preview.Token, Confirmed = false }); throw new Exception("Missing checkbox confirmation was accepted."); } catch (ArgumentException) { }
            var started = (DeletionStatus)f.Service.Post(new ExecuteDeletion { Token = preview.Token, Confirmed = true });
            assert(started.Started, "Confirmation was not acknowledged.");
            var limit = DateTimeOffset.UtcNow.AddSeconds(5); DeletionStatus done;
            do { await Task.Delay(10); done = (DeletionStatus)f.Service.Get(new GetDeletionStatus { Token = preview.Token }); } while (done.State == "Deleting" && DateTimeOffset.UtcNow < limit);
            assert(done.State == "Completed" && done.Deleted == 1 && done.Processed == 1 && done.Processing == 0 && f.Native.NativeDeletes == 1 && !File.Exists(f.Native.Movie.Path), "The service did not report actual native deletion completion.");
            f.Service.Post(new ExecuteDeletion { Token = preview.Token, Confirmed = true });
            assert(f.Native.NativeDeletes == 1, "A replay deleted again.");
            f.SetAuth(new AuthorizationInfo { User = new User { InternalId = 6, Policy = new MediaBrowser.Model.Users.UserPolicy { IsAdministrator = true } } });
            try { f.Service.Get(new GetDeletionStatus { Token = preview.Token }); throw new Exception("Another actor could read the plan."); } catch (ArgumentException) { }
        });
        await testAsync("Deletion status names the in-flight item and counts actual completion", async () =>
        {
            using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
            var backend = new DeletionFixture(root) { Entered = entered, Release = release };
            var manager = new DeletionManager(backend); var preview = manager.Preview("admin", "user", new[] { "1", "2" });
            try
            {
                manager.Execute("admin", preview.Token, true);
                assert(await Task.Run(() => entered.Wait(3000)), "The worker did not start.");
                var running = manager.Status("admin", preview.Token);
                assert(running.Started && running.Processing == 1 && running.CurrentItemName == "Movie 1" && running.Ready == 1 && running.Processed == 0, "In-flight progress was inaccurate.");
                manager.Cancel("admin", preview.Token);
            }
            finally { release.Set(); }
            var until = DateTimeOffset.UtcNow.AddSeconds(5); DeletionStatus done;
            do { await Task.Delay(10); done = manager.Status("admin", preview.Token); } while (done.State == "Deleting" && DateTimeOffset.UtcNow < until);
            assert(done.Deleted == 1 && done.Skipped == 1 && done.Processed == 2 && done.Processing == 0 && done.CurrentItemName == null, "Cancellation progress was inaccurate.");
        });
        test("A preview inspection error blocks that item and preserves the rest of the review", () =>
        {
            var manager = new DeletionManager(new FailingInspectionBackend());
            var plan = manager.Preview("admin", "user", new[] { "1", "2" });
            assert(plan.Ready == 1 && plan.Skipped == 1 && !plan.Started && plan.Items.Single(i => i.ItemId == "2").Message.Contains("Fixture path unavailable"), "A single bad inspection removed the whole preview.");
            manager.Cancel("admin", plan.Token);
        });
    }
    private sealed class FailingInspectionBackend : IDeletionBackend
    {
        public void EnsureAvailable() { }
        public string[] Resolve(string user, string[] ids, bool allowUnwatched = false) => ids;
        public DeletionInspection Inspect(string actor, string user, string id, bool allowUnwatched = false, bool allowWatchedResume = false) => id == "2" ? throw new IOException("Fixture path unavailable") : new() { Item = new() { ItemId = id, Name = "Fixture media", Paths = new[] { "/synthetic/one.mkv" } }, Fingerprint = "fixture" };
        public void Delete(string actor, string user, string id, string fingerprint, bool allowUnwatched = false, bool allowWatchedResume = false) => throw new Exception("A preview must never delete.");
    }
}
