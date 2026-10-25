using System;

namespace WhoSceneIt.Updates
{
    public sealed class ReleaseManifest
    {
        public int SchemaVersion { get; set; }
        public string AppId { get; set; }
        public string PluginId { get; set; }
        public string Repository { get; set; }
        public string Version { get; set; }
        public string MinimumEmbyVersion { get; set; }
        public string ReleaseUrl { get; set; }
        public string[] Notes { get; set; } = Array.Empty<string>();
        public bool HasMoreNotes { get; set; }
        public ReleaseAsset Asset { get; set; }
    }
    public sealed class ReleaseAsset
    {
        public string FileName { get; set; }
        public string DownloadUrl { get; set; }
        public string Sha256 { get; set; }
        public long SizeBytes { get; set; }
    }
    public sealed class UpdateStatus
    {
        public string InstalledVersion { get; set; }
        public string LatestVersion { get; set; }
        public bool UpdateAvailable { get; set; }
        public bool Downloaded { get; set; }
        public bool Busy { get; set; }
        public bool RestartRequired { get; set; }
        public bool CanRestart { get; set; }
        public bool AwaitingDashboardVerification { get; set; }
        public string Message { get; set; }
        public string LastSuccessfulCheckUtc { get; set; }
        public string NextAllowedCheckUtc { get; set; }
        public string[] Notes { get; set; } = Array.Empty<string>();
        public bool HasMoreNotes { get; set; }
        public string ReleaseUrl { get; set; }
    }
    public sealed class UpdateCache
    {
        public string ETag { get; set; }
        public ReleaseManifest Manifest { get; set; }
        public DateTimeOffset? LastSuccessfulCheck { get; set; }
        public DateTimeOffset? LastAttempt { get; set; }
        public DateTimeOffset? NextAllowedCheck { get; set; }
        public int Failures { get; set; }
        public string Error { get; set; }
    }
    public sealed class InstallTransaction
    {
        public string Version { get; set; }
        public string Sha256 { get; set; }
        public long SizeBytes { get; set; }
        public string PreviousVersion { get; set; }
        public string PreviousSha256 { get; set; }
    }
}
