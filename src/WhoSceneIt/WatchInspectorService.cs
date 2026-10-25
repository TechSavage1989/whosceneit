using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Services;

namespace WhoSceneIt
{
    public sealed class WatchInspectorService : IService
    {
        private readonly IUserManager _users;
        private readonly ILibraryManager _library;
        private readonly IUserDataManager _data;

        public WatchInspectorService(IUserManager users, ILibraryManager library, IUserDataManager data)
        {
            _users = users;
            _library = library;
            _data = data;
        }

        public object Get(GetWatchInspectorUsers request)
        {
            return AllUsers().Select(Summarize).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public object Get(GetWatchInspectorUserReport request)
        {
            using var activity = new ReportActivity();
            CheckPage(request.StartIndex, request.Limit);
            var view = Choice(request.View, "Titles", "Movies", "Shows", "Episodes");
            CheckText(request.SearchTerm);
            var sort = Choice(request.SortBy, "LastPlayed", "Name", "PlayCount", "WatchedCount", "Progress", "Episode");
            var watchState = WatchState(request.WatchState);
            if (string.IsNullOrWhiteSpace(request.UserId)) throw new ArgumentException("Select a user.");
            var user = _users.GetUserById(request.UserId);
            if (user == null) throw new ArgumentException("The selected user no longer exists.");
            var types = view == "Movies" ? new[] { "Movie" } :
                (view == "Shows" || view == "Episodes" ? new[] { "Episode" } : new[] { "Movie", "Episode" });
            long? seriesId = null;
            if (!string.IsNullOrEmpty(request.SeriesId))
            {
                if (view != "Episodes" || !long.TryParse(request.SeriesId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0 || !(_library.GetItemById(parsed) is Series))
                    throw new ArgumentException("Select a valid TV show for the episode list.");
                seriesId = parsed;
            }
            // Read saved positions directly, including positions below Emby's resumable threshold.
            // Full episode inventories also keep series completion denominators accurate.
            var query = new InternalItemsQuery(user)
            {
                Recursive = true, IsVirtualItem = false, IncludeItemTypes = types
            };
            // Preserve the SDK's empty ancestor array for an unscoped report.
            if (seriesId.HasValue) query.AncestorIds = new[] { seriesId.Value };
            var candidates = _library.GetItemList(query);
            var records = candidates.GroupBy(x => x.InternalId).Select(x => x.First()).Select(item => Read(user, item));
            var rows = ReportBuilder.FilterAndSort(ReportBuilder.ForUser(records, view, request.IncludeInProgress, watchState), request.SearchTerm, sort).ToArray();
            var summary = Summarize(user);
            foreach (var row in rows) { row.UserId = summary.Id; row.UserName = summary.Name; row.UserDisabled = summary.IsDisabled; }
            return Page(rows, request.StartIndex, request.Limit);
        }

        public object Get(GetWatchInspectorTitles request)
        {
            using var activity = new ReportActivity();
            CheckPage(request.StartIndex, request.Limit);
            CheckText(request.SearchTerm);
            var type = Choice(request.Type, "All", "Movie", "Series", "Episode");
            if (string.IsNullOrWhiteSpace(request.SearchTerm)) return new PagedResult<TitleSummary>();
            var query = new InternalItemsQuery
            {
                Recursive = true,
                IsVirtualItem = false,
                IncludeItemTypes = type == "All" ? new[] { "Movie", "Series" } : new[] { type },
                SearchTerm = request.SearchTerm.Trim(),
                StartIndex = request.StartIndex,
                Limit = request.Limit,
                OrderBy = new[] { ("SortName", SortOrder.Ascending) },
                EnableTotalRecordCount = true
            };
            var result = _library.GetItemsResult(query);
            return new PagedResult<TitleSummary>
            {
                Items = result.Items.Select(Title).ToArray(), TotalRecordCount = result.TotalRecordCount, StartIndex = request.StartIndex
            };
        }

        public object Get(GetWatchInspectorViewers request)
        {
            using var activity = new ReportActivity();
            CheckPage(request.StartIndex, request.Limit);
            CheckText(request.SearchTerm);
            var sort = Choice(request.SortBy, "LastPlayed", "Name", "PlayCount", "WatchedCount", "Progress", "Episode");
            var watchState = WatchState(request.WatchState);
            if (!long.TryParse(request.ItemId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                throw new ArgumentException("Select a valid movie, TV show, or episode.");
            var selected = _library.GetItemById(id);
            if (selected == null || !(selected is Series || selected is Episode || selected.GetType().Name == "Movie"))
                throw new ArgumentException("The selected title is unavailable or is not a movie, TV show, or episode.");
            var title = Title(selected);
            var items = selected is Series ? _library.GetItemList(new InternalItemsQuery
            {
                Recursive = true, IsVirtualItem = false, IncludeItemTypes = new[] { "Episode" }, AncestorIds = new[] { id }
            }).GroupBy(x => x.InternalId).Select(g => g.First()).ToArray() : new[] { selected };
            int? available = selected is Series ? items.Length : (int?)null;
            var rows = new List<ReportRow>();
            foreach (var user in AllUsers())
            {
                var row = ReportBuilder.ForViewer(items.Select(item => Read(user, item)), title, Summarize(user), request.IncludeInProgress, available, watchState);
                if (row != null) rows.Add(row);
            }
            var sorted = ReportBuilder.FilterAndSort(rows, request.SearchTerm, sort).ToArray();
            return new ViewerReport
            {
                Title = title, Items = sorted.Skip(request.StartIndex).Take(request.Limit).ToArray(),
                TotalRecordCount = sorted.Length, StartIndex = request.StartIndex, AvailableEpisodeCount = available
            };
        }

        private User[] AllUsers() => _users.GetUserList(new UserQuery());
        private UserSummary Summarize(User user)
        {
            var dto = _users.GetUserDto(user);
            return new UserSummary { Id = dto.Id, Name = user.Name, IsDisabled = _users.GetUserPolicy(user)?.IsDisabled ?? false };
        }

        private WatchRecord Read(User user, BaseItem item)
        {
            var data = _data.GetUserData(user, item) ?? new UserItemData();
            var episode = item as Episode;
            var series = episode?.Series;
            return new WatchRecord
            {
                Title = Title(item), SeriesId = series != null ? Id(series) : (item.SeriesId > 0 ? item.SeriesId.ToString(CultureInfo.InvariantCulture) : null),
                SeriesYear = series?.ProductionYear,
                Played = data.Played, PlayCount = data.PlayCount, PlaybackPositionTicks = data.PlaybackPositionTicks, RunTimeTicks = item.RunTimeTicks, LastPlayed = data.LastPlayedDate
            };
        }

        private static TitleSummary Title(BaseItem item)
        {
            var episode = item as Episode;
            return new TitleSummary
            {
                Id = Id(item), Name = item.Name, Type = item is Series ? "Series" : (episode != null ? "Episode" : "Movie"),
                Year = item.ProductionYear, SeriesName = episode?.SeriesName,
                SeasonNumber = item.ParentIndexNumber, EpisodeNumber = item.IndexNumber, EpisodeNumberEnd = episode?.IndexNumberEnd
            };
        }

        private static string Id(BaseItem item) => item.InternalId.ToString(CultureInfo.InvariantCulture);
        private static string WatchState(string value) => string.IsNullOrEmpty(value) ? null : Choice(value, "Started", "Watched", "Partial", "Resuming", "NotStarted", "All");
        private static PagedResult<T> Page<T>(T[] rows, int start, int limit) => new PagedResult<T>
        {
            Items = rows.Skip(start).Take(limit).ToArray(), TotalRecordCount = rows.Length, StartIndex = start
        };
        private static void CheckPage(int start, int limit)
        {
            if (start < 0 || limit < 1 || limit > 500) throw new ArgumentException("StartIndex must be non-negative and Limit must be between 1 and 500.");
        }
        private static void CheckText(string value)
        {
            if (value != null && value.Length > 200) throw new ArgumentException("Search text must be 200 characters or fewer.");
        }
        private static string Choice(string value, params string[] choices)
        {
            if (string.IsNullOrEmpty(value)) return choices[0];
            var match = choices.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
            if (match == null) throw new ArgumentException("Unsupported report option.");
            return match;
        }
    }
}
