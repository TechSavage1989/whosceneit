using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MediaBrowser.Common;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Querying;
using WhoSceneIt.Updates;

namespace WhoSceneIt.Deletion
{
    public sealed class EmbyDeletionBackend : IDeletionBackend
    {
        private readonly IUserManager _users;
        private readonly IUserDataManager _data;
        private readonly ILibraryManager _library;
        private readonly ISessionManager _sessions;
        private readonly IApplicationHost _host;
        public EmbyDeletionBackend(IUserManager users, IUserDataManager data, ILibraryManager library, ISessionManager sessions, IApplicationHost host)
        { _users = users; _data = data; _library = library; _sessions = sessions; _host = host; }

        public void EnsureAvailable()
        {
            if (!_host.IsStartupComplete || _host.IsShuttingDown) throw new InvalidOperationException("Wait for Emby startup or shutdown to finish.");
            if (_library.IsScanRunning) throw new InvalidOperationException("Wait for the library scan to finish before deleting media.");
            var manager = UpdateManager.Instance;
            if (manager != null && (manager.IsInstalling || manager.Status().RestartRequired)) throw new InvalidOperationException("Finish the plugin update or restart before deleting media.");
        }
        public string[] Resolve(string userId, string[] rootIds, bool allowUnwatched = false)
        {
            var user = string.IsNullOrEmpty(userId) ? null : _users.GetUserById(userId) ?? throw new ArgumentException("The selected user no longer exists.");
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var value in rootIds)
            {
                var id = long.Parse(value, CultureInfo.InvariantCulture);
                var item = _library.GetItemById(id);
                if (item is Series)
                {
                    var episodes = _library.GetItemList(new InternalItemsQuery(user) { Recursive = true, IsVirtualItem = false,
                        IncludeItemTypes = new[] { "Episode" }, AncestorIds = new[] { id } });
                    var matching = episodes.Where(e => allowUnwatched || WatchedUsers(user, e).Any()).Select(e => Id(e)).ToArray();
                    foreach (var child in matching) ids.Add(child);
                    if (matching.Length == 0) ids.Add(value); // Show why an empty/non-watched series is blocked.
                }
                else ids.Add(value);
                if (ids.Count > DeletionManager.MaximumItems) throw new ArgumentException("This selection contains more than 500 media items. Select fewer titles or use the TV episodes view.");
            }
            return ids.ToArray();
        }
        public DeletionInspection Inspect(string actorId, string userId, string itemId, bool allowUnwatched = false)
        {
            var entry = new DeletionItem { ItemId = itemId, Name = "Unavailable item " + itemId };
            DeletionInspection Block(string reason) { entry.State = "Blocked"; entry.Message = reason; return new DeletionInspection { Item = entry }; }
            var actor = _users.GetUserById(actorId);
            var policy = actor == null ? null : _users.GetUserPolicy(actor);
            if (policy == null || !policy.IsAdministrator || policy.IsDisabled) return Block("The signed-in administrator no longer has access.");
            var item = _library.GetItemById(long.Parse(itemId, CultureInfo.InvariantCulture));
            if (item == null) return Block("This media item is no longer in the library.");
            entry.Name = item.Name; entry.Type = item is Episode ? "Episode" : item is Movie ? "Movie" : item.GetType().Name;
            if (item is Episode episode) entry.Detail = episode.SeriesName + " - " + ReportBuilder.EpisodeLabel(new TitleSummary
                { SeasonNumber = episode.ParentIndexNumber, EpisodeNumber = episode.IndexNumber, EpisodeNumberEnd = episode.IndexNumberEnd });
            if (!(item is Movie || item is Episode) || item.IsFolder || item.IsVirtualItem || !item.IsFileProtocol)
                return Block("Only local movie and episode media can be deleted. TV shows expand into the eligible episode files, not the show folder.");
            var selectedUser = string.IsNullOrEmpty(userId) ? null : _users.GetUserById(userId);
            if (!string.IsNullOrEmpty(userId) && selectedUser == null) return Block("The selected user no longer exists.");
            var watched = WatchedUsers(selectedUser, item).ToArray();
            entry.WatchedBy = watched.Select(u => u.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
            if (!allowUnwatched && watched.Length == 0) return Block("This item is not marked watched for the selected report. Enable the unfinished/unwatched deletion option and prepare a new preview to include it.");
            var resuming = _users.GetUserList(new UserQuery()).Where(u => WatchProgress.HasResume(_data.GetUserData(u, item)?.PlaybackPositionTicks ?? 0L)).Select(u => u.Name).ToArray();
            entry.WatchNote = (watched.Length == 0 ? "Not marked watched for the selected report. " : "") +
                (resuming.Length > 0 ? "Saved playback positions exist for: " + string.Join(", ", resuming) + ". " : "") +
                (allowUnwatched ? "This preview explicitly allows unfinished and unwatched media." : "Fully watched media only; saved progress is protected.");
            if (!allowUnwatched && resuming.Length > 0) return Block("Saved playback positions exist for: " + string.Join(", ", resuming) + ". This media is preserved, including possible rewatches.");
            // This user-scoped query is available in both 4.8 and current Emby.
            // The old two-argument FilterItemsToIdsForUser no longer exists in newer servers.
            if (selectedUser != null && !_library.GetItemList(new InternalItemsQuery(selectedUser) { ItemIds = new[] { item.InternalId },
                Recursive = true, IsVirtualItem = false, EnforceContentRestriction = true, Limit = 1 }).Any(i => i.InternalId == item.InternalId))
                return Block("This title is no longer available to the selected user.");
            var folders = _library.GetCollectionFolders(item);
            if (!item.CanDelete(actor, folders)) return Block("Emby's content-deletion permissions do not allow this administrator to delete this item.");
            if (_sessions.Sessions.Any(s => s.FullNowPlayingItem?.InternalId == item.InternalId || s.NowPlayingItem?.Id == Id(item) ||
                (Guid.TryParse(s.NowPlayingItem?.Id, out var playingId) && playingId == item.Id)))
                return Block("This media is playing or paused in an Emby session. Stop playback before deleting it.");
            var paths = item.GetDeletePaths(false, folders);
            if (paths == null || paths.Count == 0 || paths.Any(p => string.IsNullOrWhiteSpace(p.FullName))) return Block("Emby did not provide safe deletion paths.");
            entry.Paths = paths.Select(p => Path.GetFullPath(p.FullName)).Distinct(PathComparer).OrderBy(p => p, PathComparer).ToArray();
            foreach (var directory in paths.Where(p => p.IsDirectory))
            {
                var full = Path.GetFullPath(directory.FullName);
                if (PathComparer.Equals(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
                    return Block("Deleting a filesystem root is not allowed.");
                if (_library.GetVirtualFolders().Any(v => (v.Locations ?? Array.Empty<string>()).Any(p => PathComparer.Equals(Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))))
                    return Block("Emby would delete a library root folder. Delete this item through Emby's own media controls instead.");
                var prefix = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var other = _library.GetItemList(new InternalItemsQuery { Recursive = true, PathStartsWith = prefix, AllowOwnedItems = true,
                    IncludeItemTypes = new[] { "Movie", "Episode" }, ExcludeItemIds = new[] { item.InternalId }, Limit = 1 });
                if (other.Length != 0) return Block("Emby would remove a folder containing other library media. Those files are preserved; use Emby's controls to review them separately.");
            }
            var identity = itemId + "\n" + item.GetType().FullName + "\n" + item.Path + "\n" + string.Join("\n", paths.OrderBy(p => p.FullName, PathComparer)
                .Select(p => p.FullName + "|" + p.IsDirectory + "|" + p.Length + "|" + p.LastWriteTimeUtc.ToString("O")));
            using var hash = SHA256.Create();
            return new DeletionInspection { Item = entry, Fingerprint = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", ""),
                MediaPath = item.Path, MediaSize = File.Exists(item.Path) ? new FileInfo(item.Path).Length : (long?)null,
                SeasonNumber = item.ParentIndexNumber, EpisodeNumber = item.IndexNumber, EpisodeNumberEnd = (item as Episode)?.IndexNumberEnd };
        }
        public void Delete(string actorId, string userId, string itemId, string expectedFingerprint, bool allowUnwatched = false)
        {
            EnsureAvailable();
            var current = Inspect(actorId, userId, itemId, allowUnwatched);
            if (current.Item.State != "Ready" || current.Fingerprint != expectedFingerprint) throw new InvalidOperationException(current.Item.Message ?? "The media or deletion paths changed after confirmation.");
            var item = _library.GetItemById(long.Parse(itemId, CultureInfo.InvariantCulture));
            _library.DeleteItem(item, new DeleteOptions { DeleteFileLocation = true, DeleteFromExternalProvider = false, CollectionFolders = _library.GetCollectionFolders(item) }, true);
        }
        private System.Collections.Generic.IEnumerable<User> WatchedUsers(User selected, BaseItem item) =>
            (selected == null ? _users.GetUserList(new UserQuery()) : new[] { selected }).Where(u => _data.GetUserData(u, item)?.Played == true);
        private static string Id(BaseItem item) => item.InternalId.ToString(CultureInfo.InvariantCulture);
        private static StringComparer PathComparer => Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }
}
