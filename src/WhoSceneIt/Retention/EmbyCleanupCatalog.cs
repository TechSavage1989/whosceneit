using System;
using System.Globalization;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using WhoSceneIt.Deletion;

namespace WhoSceneIt.Retention
{
    public interface ICleanupCatalog
    {
        string OwnerError(CleanupPolicy policy);
        string[] Candidates(CleanupPolicy policy, DateTimeOffset now, string[] eventIds);
        string Ineligible(CleanupPolicy policy, string id, DateTimeOffset now);
        BaseItem Item(string id);
    }
    public sealed class EmbyCleanupCatalog : ICleanupCatalog
    {
        private readonly IUserManager _users;
        private readonly IUserDataManager _data;
        private readonly ILibraryManager _library;
        public EmbyCleanupCatalog(IUserManager users, IUserDataManager data, ILibraryManager library) { _users = users; _data = data; _library = library; }
        public BaseItem Item(string id) => _library.GetItemById(long.Parse(id, CultureInfo.InvariantCulture));
        public string OwnerError(CleanupPolicy policy)
        {
            var owner = _users.GetUserById(policy.OwnerId); var permission = owner == null ? null : _users.GetUserPolicy(owner);
            if (permission == null || !permission.IsAdministrator || permission.IsDisabled) return "The rule owner is no longer an active administrator.";
            if (_users.GetUserById(policy.UserId) == null) return "The rule's watch-history user no longer exists.";
            return null;
        }
        private bool Matches(CleanupPolicy p, BaseItem item, DateTimeOffset now)
        {
            if (!(item is Movie || item is Episode) || item.IsVirtualItem || !item.IsFileProtocol) return false;
            if (p.Scope == "Movies" && !(item is Movie) || (p.Scope == "Episodes" || p.Scope == "CompletedShows") && !(item is Episode)) return false;
            var data = _data.GetUserData(_users.GetUserById(p.UserId), item);
            if (data?.Played != true) return false;
            if (p.WatchMode == "Watched" && WatchProgress.HasResume(data.PlaybackPositionTicks) || p.WatchMode == "Resume" && !WatchProgress.HasResume(data.PlaybackPositionTicks)) return false;
            if (p.MinimumDays > 0 && (!data.LastPlayedDate.HasValue || data.LastPlayedDate.Value > now.AddDays(-p.MinimumDays))) return false;
            return true;
        }
        private bool CompleteShow(CleanupPolicy policy, long seriesId)
        {
            if (seriesId <= 0) return false;
            var episodes = _library.GetItemList(new InternalItemsQuery { Recursive = true, IsVirtualItem = false, IncludeItemTypes = new[] { "Episode" }, AncestorIds = new[] { seriesId } });
            var user = _users.GetUserById(policy.UserId);
            return episodes.Length > 0 && episodes.All(e => _data.GetUserData(user, e)?.Played == true);
        }
        public string[] Candidates(CleanupPolicy policy, DateTimeOffset now, string[] eventIds)
        {
            var error = OwnerError(policy); if (error != null) throw new InvalidOperationException(error);
            var items = _library.GetItemList(new InternalItemsQuery(_users.GetUserById(policy.UserId)) { Recursive = true, IsVirtualItem = false, EnforceContentRestriction = true,
                IncludeItemTypes = policy.Scope == "Movies" ? new[] { "Movie" } : policy.Scope == "Both" ? new[] { "Movie", "Episode" } : new[] { "Episode" }, Limit = 100001 });
            if (items.Length > 100000) throw new InvalidOperationException("This rule exceeds 100,000 media records. Narrow its media scope.");
            var events = (eventIds ?? Array.Empty<string>()).Select(Item).Where(i => i != null).ToArray();
            var matching = items.GroupBy(i => i.InternalId).Select(g => g.First()).Where(i => Matches(policy, i, now));
            if (eventIds != null) matching = matching.Where(i => events.Any(e => e.InternalId == i.InternalId || policy.Scope == "CompletedShows" && e.SeriesId > 0 && e.SeriesId == i.SeriesId));
            if (policy.Scope == "CompletedShows") matching = matching.GroupBy(i => i.SeriesId).Where(g => CompleteShow(policy, g.Key)).SelectMany(g => g);
            return matching.OrderBy(i => _data.GetUserData(_users.GetUserById(policy.UserId), i).LastPlayedDate).ThenBy(i => i.InternalId).Select(RetentionStore.Id).ToArray();
        }
        public string Ineligible(CleanupPolicy policy, string id, DateTimeOffset now)
        {
            var error = OwnerError(policy); if (error != null) return error;
            var item = Item(id);
            if (item == null || !Matches(policy, item, now)) return "This item no longer matches the cleanup rule's completed state, media scope, or waiting period.";
            if (policy.Scope == "CompletedShows" && !CompleteShow(policy, item.SeriesId)) return "This show is no longer fully watched. Its remaining episodes are preserved.";
            return null;
        }
        public bool SeriesHasFiles(DeletionInspection entry)
        {
            if (entry.SeriesId <= 0) return true; // No proof of an empty native series.
            return _library.GetItemList(new InternalItemsQuery { Recursive = true, IsVirtualItem = false, IncludeItemTypes = new[] { "Episode" }, AncestorIds = new[] { entry.SeriesId } })
                .Any(i => !i.IsFileProtocol || string.IsNullOrWhiteSpace(i.Path) || System.IO.File.Exists(i.Path) || System.IO.Directory.Exists(i.Path));
        }
    }
}
