using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WhoSceneIt
{
    public static class ReportBuilder
    {
        public static bool IsIncluded(WatchRecord record, bool includeInProgress)
        {
            // A play count or a date alone does not prove completion. Played is Emby's saved watch flag.
            return record.Played || (includeInProgress && record.PlaybackPositionTicks > 0);
        }

        public static List<ReportRow> ForUser(IEnumerable<WatchRecord> input, string view, bool includeInProgress)
        {
            var records = input.GroupBy(x => x.Title.Id, StringComparer.Ordinal).Select(g => g.First())
                .Where(x => IsIncluded(x, includeInProgress)).ToList();
            var rows = new List<ReportRow>();
            if (view == "Titles" || view == "Movies")
                rows.AddRange(records.Where(x => x.Title.Type == "Movie").Select(Single));
            if (view == "Episodes")
                rows.AddRange(records.Where(x => x.Title.Type == "Episode").Select(Single));
            if (view == "Titles" || view == "Shows")
            {
                // Never merge separate shows just because they share a name. Orphans remain separate episodes.
                var episodes = records.Where(x => x.Title.Type == "Episode");
                rows.AddRange(episodes.Where(x => string.IsNullOrEmpty(x.SeriesId)).Select(Single));
                foreach (var group in episodes.Where(x => !string.IsNullOrEmpty(x.SeriesId)).GroupBy(x => x.SeriesId, StringComparer.Ordinal))
                {
                    var first = group.First();
                    rows.Add(Aggregate(group, new TitleSummary
                    {
                        Id = group.Key, Name = first.Title.SeriesName ?? "Unnamed TV show", Type = "Series", Year = first.SeriesYear
                    }, null));
                }
            }
            return rows;
        }

        public static ReportRow ForViewer(IEnumerable<WatchRecord> input, TitleSummary title, UserSummary user, bool includeInProgress, int? availableEpisodes)
        {
            var included = input.GroupBy(x => x.Title.Id, StringComparer.Ordinal).Select(g => g.First())
                .Where(x => IsIncluded(x, includeInProgress)).ToArray();
            if (included.Length == 0) return null;
            var row = title.Type == "Series" ? Aggregate(included, title, availableEpisodes) : Single(included[0]);
            row.UserId = user.Id;
            row.UserName = user.Name;
            row.UserDisabled = user.IsDisabled;
            return row;
        }

        private static ReportRow Single(WatchRecord record)
        {
            var title = record.Title;
            return new ReportRow
            {
                ItemId = title.Id, Name = title.Name, Type = title.Type, Year = title.Year,
                SeriesName = title.SeriesName, Episode = EpisodeLabel(title),
                Status = record.Played ? "Watched" : "In progress",
                WatchedCount = record.Played ? 1 : 0,
                InProgressCount = !record.Played && record.PlaybackPositionTicks > 0 ? 1 : 0,
                SavedPlayCount = Math.Max(0L, record.PlayCount),
                LastPlayedUtc = Iso(record.LastPlayed)
            };
        }

        private static ReportRow Aggregate(IEnumerable<WatchRecord> input, TitleSummary title, int? availableEpisodes)
        {
            var records = input.ToArray();
            var watched = records.Count(x => x.Played);
            var progress = records.Count(x => !x.Played && x.PlaybackPositionTicks > 0);
            var last = records.Where(x => x.LastPlayed.HasValue).Select(x => x.LastPlayed).DefaultIfEmpty(null).Max();
            return new ReportRow
            {
                ItemId = title.Id, Name = title.Name, Type = "Series", Year = title.Year,
                WatchedCount = watched, InProgressCount = progress, AvailableEpisodeCount = availableEpisodes,
                Status = watched == 0 ? "In progress" :
                    (availableEpisodes.HasValue && availableEpisodes > 0 && watched >= availableEpisodes.Value ? "All available episodes watched" : "Watched episodes"),
                SavedPlayCount = records.Sum(x => Math.Max(0L, x.PlayCount)), LastPlayedUtc = Iso(last)
            };
        }

        public static string EpisodeLabel(TitleSummary title)
        {
            if (title.Type != "Episode") return "";
            var season = title.SeasonNumber.HasValue ? "S" + title.SeasonNumber.Value.ToString("00", CultureInfo.InvariantCulture) : "S?";
            var episode = title.EpisodeNumber.HasValue ? "E" + title.EpisodeNumber.Value.ToString("00", CultureInfo.InvariantCulture) : "E?";
            if (title.EpisodeNumberEnd > title.EpisodeNumber) episode += "-E" + title.EpisodeNumberEnd.Value.ToString("00", CultureInfo.InvariantCulture);
            return season + episode;
        }

        public static IEnumerable<ReportRow> FilterAndSort(IEnumerable<ReportRow> rows, string search, string sort)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var needle = search.Trim();
                rows = rows.Where(x => Contains(x.Name, needle) || Contains(x.SeriesName, needle) || Contains(x.UserName, needle) || Contains(x.Episode, needle));
            }
            switch (sort)
            {
                case "Name": return rows.OrderBy(x => x.UserName ?? x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ItemId, StringComparer.Ordinal);
                case "PlayCount": return rows.OrderByDescending(x => x.SavedPlayCount).ThenBy(x => x.UserName ?? x.Name, StringComparer.OrdinalIgnoreCase);
                case "WatchedCount": return rows.OrderByDescending(x => x.WatchedCount).ThenBy(x => x.UserName ?? x.Name, StringComparer.OrdinalIgnoreCase);
                default: return rows.OrderByDescending(x => x.LastPlayedUtc != null).ThenByDescending(x => x.LastPlayedUtc, StringComparer.Ordinal).ThenBy(x => x.UserName ?? x.Name, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool Contains(string value, string search) => value != null && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        private static string Iso(DateTimeOffset? date) => date?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }
}
