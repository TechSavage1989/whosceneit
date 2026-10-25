using System;
using System.Collections.Generic;

namespace WhoSceneIt
{
    public sealed class UserSummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsDisabled { get; set; }
    }

    public sealed class TitleSummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int? Year { get; set; }
        public string SeriesName { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
        public int? EpisodeNumberEnd { get; set; }
    }

    public sealed class WatchRecord
    {
        public TitleSummary Title { get; set; }
        public string SeriesId { get; set; }
        public int? SeriesYear { get; set; }
        public bool Played { get; set; }
        public long PlaybackPositionTicks { get; set; }
        public long PlayCount { get; set; }
        public DateTimeOffset? LastPlayed { get; set; }
    }

    public sealed class ReportRow
    {
        public string ItemId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public bool UserDisabled { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int? Year { get; set; }
        public string SeriesName { get; set; }
        public string Episode { get; set; }
        public string Status { get; set; }
        public int WatchedCount { get; set; }
        public int InProgressCount { get; set; }
        public int? AvailableEpisodeCount { get; set; }
        public long SavedPlayCount { get; set; }
        public string LastPlayedUtc { get; set; }
    }

    public sealed class PagedResult<T>
    {
        public T[] Items { get; set; } = Array.Empty<T>();
        public int TotalRecordCount { get; set; }
        public int StartIndex { get; set; }
        public string GeneratedUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    }

    public sealed class ViewerReport
    {
        public TitleSummary Title { get; set; }
        public ReportRow[] Items { get; set; } = Array.Empty<ReportRow>();
        public int TotalRecordCount { get; set; }
        public int StartIndex { get; set; }
        public int? AvailableEpisodeCount { get; set; }
        public string GeneratedUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    }
}
