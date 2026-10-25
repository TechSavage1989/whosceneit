using System;

namespace WhoSceneIt
{
    public static class WatchProgress
    {
        public static bool HasResume(long position) => position > 0;
        public static long? Duration(long? ticks) => ticks > 0 ? ticks : null;
        public static long Position(long ticks, long? duration) => duration.HasValue ? Math.Min(Math.Max(0L, ticks), duration.Value) : Math.Max(0L, ticks);
        public static bool Matches(WatchRecord record, string state, bool includeInProgress)
        {
            var resume = HasResume(record.PlaybackPositionTicks);
            switch (state)
            {
                case "All": return true;
                case "Started": return record.Played || resume;
                case "Watched": return record.Played;
                case "Partial": return !record.Played && resume;
                case "Resuming": return record.Played && resume;
                case "NotStarted": return !record.Played && !resume;
                default: return record.Played || (includeInProgress && resume);
            }
        }
        public static bool MatchesSeries(ReportRow row, string state, bool includeInProgress)
        {
            switch (state)
            {
                case "All": return true;
                case "Watched": return row.AvailableEpisodeCount > 0 && row.WatchedCount == row.AvailableEpisodeCount;
                case "Partial": return row.InProgressCount > 0 || (row.WatchedCount > 0 && row.WatchedCount < row.AvailableEpisodeCount);
                case "Resuming": return row.WatchedWithResumeCount > 0;
                case "NotStarted": return row.WatchedCount == 0 && row.InProgressCount == 0;
                case "Started": return row.WatchedCount > 0 || row.InProgressCount > 0;
                default: return row.WatchedCount > 0 || (includeInProgress && row.InProgressCount > 0);
            }
        }
    }
}
