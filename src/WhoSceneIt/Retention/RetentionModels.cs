using System;
using System.Globalization;

namespace WhoSceneIt.Retention
{
    public sealed class ProtectedMedia
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
    }
    public sealed class ProtectionFlag
    {
        public string ItemId { get; set; }
        public bool Explicit { get; set; }
        public string SourceId { get; set; }
        public bool NeverDelete { get; set; }
        public string Reason { get; set; }
    }
    public sealed class CleanupPolicy
    {
        public string Id { get; set; }
        public string Revision { get; set; }
        public string OwnerId { get; set; }
        public string Name { get; set; } = "Watched media cleanup";
        public bool Enabled { get; set; }
        public string UserId { get; set; }
        public string Scope { get; set; } = "Both";
        public string WatchMode { get; set; } = "Watched";
        public int MinimumDays { get; set; } = 7;
        public int MaximumItems { get; set; } = 100;
        public string Action { get; set; } = "Review";
        public string Schedule { get; set; } = "Off";
        public int IntervalHours { get; set; } = 24;
        public string TimeUtc { get; set; } = "03:00";
        public int Weekday { get; set; }
        public bool OnPlaybackStopped { get; set; }
        public bool OnWatchedChanged { get; set; }
        public bool OnStartup { get; set; }
        public bool OnDashboard { get; set; }
        public string NextRunUtc { get; set; }
        public string LastEvaluatedUtc { get; set; }
    }
    public sealed class CleanupReview
    {
        public string Id { get; set; }
        public string PolicyId { get; set; }
        public string Revision { get; set; }
        public string Name { get; set; }
        public string OwnerId { get; set; }
        public string Trigger { get; set; }
        public string CreatedUtc { get; set; }
        public string[] ItemIds { get; set; } = Array.Empty<string>();
    }
    public sealed class CleanupHold
    {
        public string RunId { get; set; }
        public string ItemId { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public string Fingerprint { get; set; }
    }
    public sealed class CleanupRun
    {
        public string Id { get; set; }
        public string OwnerId { get; set; }
        public string PolicyId { get; set; }
        public string Name { get; set; }
        public string Trigger { get; set; }
        public string StartedUtc { get; set; }
        public string FinishedUtc { get; set; }
        public string State { get; set; }
        public string Message { get; set; }
        public string Token { get; set; }
        public int Total { get; set; }
        public int Deleted { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public int SyncFailed { get; set; }
    }
    public sealed class RetentionState
    {
        public int SchemaVersion { get; set; } = 1;
        public ProtectedMedia[] Protected { get; set; } = Array.Empty<ProtectedMedia>();
        public CleanupPolicy[] Policies { get; set; } = Array.Empty<CleanupPolicy>();
        public CleanupReview[] Reviews { get; set; } = Array.Empty<CleanupReview>();
        public CleanupHold[] Holds { get; set; } = Array.Empty<CleanupHold>();
        public CleanupRun[] Runs { get; set; } = Array.Empty<CleanupRun>();
    }
    public sealed class CleanupStatus
    {
        public RetentionState Settings { get; set; }
        public string ActorId { get; set; }
        public bool Busy { get; set; }
        public string Message { get; set; }
    }
    public static class CleanupSchedule
    {
        public static DateTimeOffset? Next(CleanupPolicy policy, DateTimeOffset now)
        {
            now = now.ToUniversalTime();
            if (!policy.Enabled || policy.Schedule == "Off") return null;
            if (policy.Schedule == "Interval") return now.AddHours(policy.IntervalHours);
            var time = TimeSpan.ParseExact(policy.TimeUtc, @"hh\:mm", CultureInfo.InvariantCulture);
            var next = new DateTimeOffset(now.UtcDateTime.Date.Add(time), TimeSpan.Zero);
            if (next <= now) next = next.AddDays(1);
            if (policy.Schedule == "Weekly") while ((int)next.DayOfWeek != policy.Weekday) next = next.AddDays(1);
            return next;
        }
        public static DateTimeOffset? Parse(string value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result) ? result : (DateTimeOffset?)null;
    }
}
