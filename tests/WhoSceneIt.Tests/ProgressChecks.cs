using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Users;
using WhoSceneIt;

public static class ProgressChecks
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert)
    {
        WatchRecord Record(string id, bool played = false, long position = 0, long? duration = 6000000000L) => new()
        {
            Title = new() { Id = id, Name = "Episode " + id, Type = "Episode", SeriesName = "Show", SeasonNumber = 1, EpisodeNumber = int.Parse(id) },
            SeriesId = "10", Played = played, PlaybackPositionTicks = position, RunTimeTicks = duration
        };
        ReportRow Single(WatchRecord r) => ReportBuilder.ForUser(new[] { r }, "Episodes", true, "All").Single();
        test("Episode progress keeps completion, partial playback, and rewatches separate", () =>
        {
            var partial = Single(Record("1", position: 1500000000L));
            assert(partial.Status == "Partially watched" && partial.PlaybackPercent == 25 && partial.RemainingTicks == 4500000000L && partial.DeletableCount == 0, "Partial time or completion was wrong.");
            var complete = Single(Record("2", true));
            assert(complete.Status == "Watched" && complete.PlaybackPercent == 100 && complete.RemainingTicks == 0 && complete.DeletableCount == 1, "The saved completion flag was ignored.");
            var rewatch = Single(Record("3", true, 1500000000L));
            assert(rewatch.WatchedCount == 1 && rewatch.WatchedWithResumeCount == 1 && rewatch.PlaybackPercent == 25 && rewatch.DeletableCount == 0, "A rewatch lost historical completion or became deletable.");
        });
        test("Missing, negative, and overrun durations never fabricate completed viewing", () =>
        {
            var unknown = Single(Record("1", position: 100, duration: null));
            assert(unknown.PlaybackPercent == null && unknown.RemainingTicks == null && unknown.PlaybackPositionTicks == 100, "An unknown duration was invented.");
            var negative = Single(Record("2", position: -50, duration: -10));
            assert(negative.Status == "Not started" && negative.PlaybackPositionTicks == 0 && negative.RunTimeTicks == null, "Negative values leaked into progress.");
            var overrun = Single(Record("3", position: long.MaxValue, duration: 100));
            assert(overrun.PlaybackPercent == 100 && overrun.RemainingTicks == 0 && overrun.Status == "Partially watched" && overrun.WatchedCount == 0, "A position at the end fabricated a saved completion flag.");
            var manual = Record("4", true, duration: null); manual.PlayCount = 0;
            assert(Single(manual).WatchedCount == 1 && Single(manual).SavedPlayCount == 0 && Single(manual).PlaybackPercent == null, "Manual marks invented playback history.");
        });
        test("Watch-state filters distinguish started, watched, partial, rewatching, and not started", () =>
        {
            var records = new[] { Record("1", true), Record("2", position: 50), Record("3"), Record("4", true, 50) };
            string Ids(string state) => string.Join(",", ReportBuilder.ForUser(records, "Episodes", true, state).Select(r => r.ItemId));
            assert(Ids("Started") == "1,2,4" && Ids("Watched") == "1,4" && Ids("Partial") == "2" && Ids("Resuming") == "4" && Ids("NotStarted") == "3" && Ids("All") == "1,2,3,4", "A watch-state filter returned the wrong episodes.");
        });
        test("Show completion counts all episodes while preserving partial and rewatch counts", () =>
        {
            var records = new[] { Record("1", true), Record("2", true, 50), Record("3", position: 50), Record("4"), Record("5"), Record("1", true) };
            var row = ReportBuilder.ForUser(records, "Shows", true, "All").Single();
            assert(row.AvailableEpisodeCount == 5 && row.WatchedCount == 2 && row.InProgressCount == 1 && row.WatchedWithResumeCount == 1 && row.NotStartedCount == 2 && row.CompletionPercent == 40 && row.DeletableCount == 1, "Series counts or denominator were wrong.");
            assert(!ReportBuilder.ForUser(records, "Shows", true, "Watched").Any() && ReportBuilder.ForUser(records, "Shows", true, "Partial").Count == 1, "A partly completed show passed the fully-watched filter.");
        });
        test("Viewer filtering includes not-started users only when requested", () =>
        {
            var title = new TitleSummary { Id = "1", Name = "Episode", Type = "Episode" };
            var user = new UserSummary { Id = "u", Name = "User" };
            assert(ReportBuilder.ForViewer(new[] { Record("1") }, title, user, true, null, "Started") == null, "An unstarted viewer was counted as started.");
            var row = ReportBuilder.ForViewer(new[] { Record("1") }, title, user, true, null, "NotStarted");
            assert(row.Status == "Not started" && row.UserId == "u", "The not-started viewer filter lost user identity.");
        });
        test("Episode ordering is numeric and progress sorting places unknown durations last", () =>
        {
            var rows = ReportBuilder.ForUser(new[] { Record("100", position: 3000000000L), Record("20", position: 100, duration: null), Record("2", true) }, "Episodes", true, "All");
            assert(string.Join(",", ReportBuilder.FilterAndSort(rows, null, "Episode").Select(r => r.ItemId)) == "2,20,100", "Episode 100 was sorted before episode 20.");
            assert(string.Join(",", ReportBuilder.FilterAndSort(rows, null, "Progress").Select(r => r.ItemId)) == "2,100,20", "Progress sorting mishandled missing durations.");
        });
        test("Unscoped reports retain the SDK's empty ancestor list for every report view", () =>
        {
            var defaults = new InternalItemsQuery();
            assert(defaults.AncestorIds != null && defaults.AncestorIds.Length == 0, "The SDK's native ancestor default changed.");
            foreach (var view in new[] { "Titles", "Movies", "Shows", "Episodes" })
            {
                var fixture = new ProgressServiceFixture();
                fixture.Service.Get(new GetWatchInspectorUserReport { UserId = "alice", View = view, WatchState = "Started" });
                assert(fixture.LastQuery.AncestorIds != null && fixture.LastQuery.AncestorIds.SequenceEqual(defaults.AncestorIds), "An unscoped report replaced Emby's empty ancestor array with null.");
            }
        });
        test("Native reports read below-threshold saved progress and keep the full series inventory", () =>
        {
            var fixture = new ProgressServiceFixture();
            var partial = (PagedResult<ReportRow>)fixture.Service.Get(new GetWatchInspectorUserReport { UserId = "alice", View = "Episodes", WatchState = "Partial", SeriesId = "10" });
            assert(partial.Items.Single().ItemId == "2" && partial.Items[0].PlaybackPositionTicks == TimeSpan.FromSeconds(1).Ticks, "Saved below-threshold playback was omitted.");
            assert(fixture.LastQuery.IsPlayed == null && fixture.LastQuery.IsResumable == null && fixture.LastQuery.AncestorIds.Single() == 10, "The query still uses the resumable threshold or lost its series scope.");
            var show = (PagedResult<ReportRow>)fixture.Service.Get(new GetWatchInspectorUserReport { UserId = "alice", View = "Shows", WatchState = "Started" });
            assert(show.Items.Single().AvailableEpisodeCount == 3 && show.Items[0].NotStartedCount == 1 && show.Items[0].CompletionPercent == 33.3, "Native show inventory inflated completion.");
            var viewers = (ViewerReport)fixture.Service.Get(new GetWatchInspectorViewers { ItemId = "2", WatchState = "Partial" });
            assert(viewers.Items.Single().UserName == "Alice" && viewers.Items[0].RunTimeTicks == TimeSpan.FromMinutes(30).Ticks, "The native viewer path omitted partial progress or duration.");
        });
        test("Native progress reports reject invalid status and episode scopes", () =>
        {
            var fixture = new ProgressServiceFixture();
            void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid report options were accepted."); }
            Reject(() => fixture.Service.Get(new GetWatchInspectorUserReport { UserId = "alice", WatchState = "invalid" }));
            Reject(() => fixture.Service.Get(new GetWatchInspectorViewers { ItemId = "2", WatchState = "invalid" }));
            Reject(() => fixture.Service.Get(new GetWatchInspectorUserReport { UserId = "alice", SeriesId = "10", View = "Titles" }));
            Reject(() => fixture.Service.Get(new GetWatchInspectorUserReport { UserId = "alice", SeriesId = "99", View = "Episodes" }));
        });
    }
}

public sealed class ProgressServiceFixture
{
    public InternalItemsQuery LastQuery;
    public WatchInspectorService Service;
    public ProgressServiceFixture()
    {
        var user = new User { InternalId = 4, Name = "Alice", Policy = new UserPolicy() };
        var series = new Series { InternalId = 10, Name = "Show" };
        var items = Enumerable.Range(1, 3).Select(i => (BaseItem)new Episode { InternalId = i, Name = "Episode " + i, SeriesId = 10, ParentIndexNumber = 1, IndexNumber = i, RunTimeTicks = TimeSpan.FromMinutes(30).Ticks }).ToArray();
        var users = Proxy.Create<IUserManager>((m, a) => m.Name switch
        {
            "GetUserById" => user, "GetUserPolicy" => user.Policy, "GetUserList" => new[] { user },
            "GetUserDto" => new UserDto { Id = "alice", Name = "Alice" }, _ => null
        });
        var data = Proxy.Create<IUserDataManager>((m, a) => m.Name == "GetUserData" ? new UserItemData { Played = ((BaseItem)a[1]).InternalId == 1, PlaybackPositionTicks = ((BaseItem)a[1]).InternalId == 2 ? TimeSpan.FromSeconds(1).Ticks : 0L } : null);
        var library = Proxy.Create<ILibraryManager>((m, a) =>
        {
            if (m.Name == "GetItemById") { var id = (long)a[0]; return id == 10 ? series : items.FirstOrDefault(i => i.InternalId == id); }
            if (m.Name == "GetItemList") { LastQuery = (InternalItemsQuery)a[0]; return items; }
            return null;
        });
        BaseItem.LibraryManager = library; BaseItem.UserManager = users;
        Service = new WatchInspectorService(users, library, data);
    }
}
