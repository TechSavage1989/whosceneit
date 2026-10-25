using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;

namespace WhoSceneIt.Updates
{
    public sealed class UpdateManager : IDisposable
    {
        public static UpdateManager Instance { get; internal set; }
        public static readonly TimeSpan AutomaticInterval = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan FocusInterval = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan ManualCooldown = TimeSpan.FromSeconds(30);
        private readonly HttpClient _http;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly IJsonSerializer _json;
        private readonly IApplicationHost _host;
        private readonly ILibraryManager _library;
        private readonly ISessionManager _sessions;
        private readonly ITaskManager _tasks;
        private readonly string _root;
        private readonly string _target;
        private readonly Version _installed;
        private Timer _timer;
        private UpdateCache _cache;
        private volatile bool _busy;
        private volatile bool _downloaded;
        private volatile bool _restartRequired;
        private volatile bool _awaitingDashboard = true;
        private string _message = "Update status has not been checked.";
        private int _progress;
        public bool StartupVerified { get; private set; }

        public UpdateManager(string root, string target, IJsonSerializer json, IApplicationHost host, ILibraryManager library, ISessionManager sessions, HttpMessageHandler handler = null, ITaskManager tasks = null)
        {
            _root = root; _target = target; _json = json; _host = host; _library = library; _sessions = sessions; _tasks = tasks;
            _installed = typeof(Plugin).Assembly.GetName().Version;
            Directory.CreateDirectory(root);
            _cache = Read<UpdateCache>("update-cache.json") ?? new UpdateCache();
            _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
            _http.Timeout = TimeSpan.FromSeconds(30);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("WhoSceneIt/" + _installed.ToString(3));
            if (_cache.Manifest != null)
            {
                try { UpdateIntegrity.ValidateManifest(_cache.Manifest, _host.ApplicationVersion); }
                catch { _cache = new UpdateCache(); }
            }
            VerifyStartup();
        }

        public void Start()
        {
            // Server-owned timer remains alive independently of any browser tab. Startup does not await network I/O.
            _ = CheckAsync("startup");
            _timer = new Timer(_ => { _ = CheckAsync("timer"); }, null, AutomaticInterval, AutomaticInterval);
        }

        private string PathFor(string name) => Path.Combine(_root, name);
        private T Read<T>(string file) where T : class
        {
            try { return File.Exists(PathFor(file)) ? _json.DeserializeFromFile<T>(PathFor(file)) : null; }
            catch { return null; }
        }
        private void Save(string file, object value)
        {
            var temporary = PathFor(file + ".tmp");
            try
            {
                File.WriteAllText(temporary, _json.SerializeToString(value), Encoding.UTF8);
                if (File.Exists(PathFor(file))) File.Replace(temporary, PathFor(file), null);
                else File.Move(temporary, PathFor(file));
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public UpdateStatus Status()
        {
            var release = _cache.Manifest;
            var available = release != null && UpdateIntegrity.ParseVersion(release.Version) > _installed;
            var message = _message;
            if (_busy && _progress > 0) message += " " + _progress + "%";
            if (!_busy && !_restartRequired && _cache.Error != null && message == "Update status has not been checked.") message = "Update check failed: " + _cache.Error + " Cached version information may be out of date.";
            else if (!_busy && !_restartRequired && _cache.LastSuccessfulCheck.HasValue && message == "Update status has not been checked.")
                message = available ? "An update is available." : "No newer release was found at the last successful check.";
            return new UpdateStatus
            {
                InstalledVersion = _installed.ToString(3), LatestVersion = release?.Version, UpdateAvailable = available,
                Downloaded = _downloaded, Busy = _busy, RestartRequired = _restartRequired, CanRestart = _host.CanSelfRestart,
                AwaitingDashboardVerification = _awaitingDashboard && StartupVerified,
                Message = message, LastSuccessfulCheckUtc = _cache.LastSuccessfulCheck?.ToString("O"), NextAllowedCheckUtc = _cache.NextAllowedCheck?.ToString("O"),
                Notes = release?.Notes ?? Array.Empty<string>(), HasMoreNotes = release?.HasMoreNotes == true || release?.Notes?.Length > 3,
                ReleaseUrl = release?.ReleaseUrl
            };
        }

        public async Task<UpdateStatus> CheckAsync(string reason)
        {
            if (!await _gate.WaitAsync(0).ConfigureAwait(false)) return Status();
            try
            {
                var now = DateTimeOffset.UtcNow;
                var wait = reason == "focus" ? FocusInterval : reason == "timer" ? AutomaticInterval : ManualCooldown;
                if (_restartRequired || (_cache.NextAllowedCheck > now) || (_cache.LastAttempt.HasValue && now - _cache.LastAttempt.Value < wait)) return Status();
                _busy = true; _message = "Checking for updates...";
                _cache.LastAttempt = now;
                using (var request = new HttpRequestMessage(HttpMethod.Get, UpdateIntegrity.FeedUrl))
                {
                    if (_cache.Manifest != null && !string.IsNullOrEmpty(_cache.ETag)) request.Headers.TryAddWithoutValidation("If-None-Match", _cache.ETag);
                    using (var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _stop.Token).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.NotModified)
                        {
                            if (_cache.Manifest == null) throw new InvalidDataException("The server returned an empty update cache.");
                        }
                        else
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                _cache.NextAllowedCheck = RetryAfter(response, now);
                                throw new HttpRequestException("The release feed returned HTTP " + (int)response.StatusCode + ".");
                            }
                            var bytes = await ReadLimited(response, UpdateIntegrity.MaxManifestSize, _stop.Token).ConfigureAwait(false);
                            var release = _json.DeserializeFromString<ReleaseManifest>(Encoding.UTF8.GetString(bytes));
                            UpdateIntegrity.ValidateManifest(release, _host.ApplicationVersion);
                            if (_cache.Manifest != null && _cache.Manifest.Version == release.Version &&
                                !string.Equals(_cache.Manifest.Asset.Sha256, release.Asset.Sha256, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("A published version changed its digest. A new version is required.");
                            if (_cache.Manifest == null || _cache.Manifest.Asset.Sha256 != release.Asset.Sha256) _downloaded = false;
                            _cache.Manifest = release; _cache.ETag = response.Headers.ETag?.ToString();
                        }
                        _cache.LastSuccessfulCheck = now; _cache.Failures = 0; _cache.NextAllowedCheck = null; _cache.Error = null;
                        _message = UpdateIntegrity.ParseVersion(_cache.Manifest.Version) > _installed ? "An update is available. Select Download to continue." : "No newer release was found at the last successful check.";
                    }
                }
            }
            catch (Exception exception)
            {
                _cache.Failures = Math.Min(10, _cache.Failures + 1);
                var backoff = DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 60 * Math.Pow(2, _cache.Failures - 1)));
                if (!_cache.NextAllowedCheck.HasValue || _cache.NextAllowedCheck < backoff) _cache.NextAllowedCheck = backoff;
                _cache.Error = exception is OperationCanceledException ? "The server did not respond in time." : exception.Message;
                _message = "Update check failed: " + _cache.Error + " Cached version information may be out of date.";
            }
            finally
            {
                try { Save("update-cache.json", _cache); }
                catch (Exception exception) { _message = "Could not save update status: " + exception.Message; }
                _busy = false; _gate.Release();
            }
            return Status();
        }

        public UpdateStatus BeginDownload()
        {
            if (!_gate.Wait(0)) return Status();
            var release = _cache.Manifest;
            if (release == null || UpdateIntegrity.ParseVersion(release.Version) <= _installed || _restartRequired) { _gate.Release(); return Status(); }
            _busy = true; _progress = 0; _message = "Downloading update...";
            _ = DownloadAsync(release);
            return Status();
        }

        private async Task DownloadAsync(ReleaseManifest release)
        {
            var partial = PathFor("download.partial");
            try
            {
                UpdateIntegrity.ValidateManifest(release, _host.ApplicationVersion);
                var uri = new Uri(release.Asset.DownloadUrl);
                for (var hop = 0; ; hop++)
                {
                    if (hop > 5 || !UpdateIntegrity.IsAllowedAssetHop(uri, hop == 0)) throw new InvalidDataException("The download redirected outside the trusted release hosts.");
                    using (var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, _stop.Token).ConfigureAwait(false))
                    {
                        var code = (int)response.StatusCode;
                        if (code >= 300 && code < 400 && response.Headers.Location != null) { uri = new Uri(uri, response.Headers.Location); continue; }
                        response.EnsureSuccessStatusCode();
                        if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != release.Asset.SizeBytes) throw new InvalidDataException("Unexpected download size.");
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
                        {
                            timeout.CancelAfter(TimeSpan.FromMinutes(3));
                            var buffer = new byte[32768]; long total = 0; int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                            {
                                total += count;
                                if (total > release.Asset.SizeBytes || total > UpdateIntegrity.MaxBinarySize) throw new InvalidDataException("The download exceeded the expected size.");
                                await output.WriteAsync(buffer, 0, count, timeout.Token).ConfigureAwait(false);
                                _progress = (int)(total * 100 / release.Asset.SizeBytes);
                            }
                            output.Flush(true);
                        }
                        break;
                    }
                }
                UpdateIntegrity.ValidateBinary(partial, release.Version, release.Asset.Sha256, release.Asset.SizeBytes);
                UpdateInstaller.AtomicWriteCopy(partial, PathFor("downloaded.dll"));
                _downloaded = true; _message = "Download verified. Ready to install.";
            }
            catch (Exception exception) { _downloaded = false; _message = "Download failed: " + exception.Message; }
            finally
            {
                try { if (File.Exists(partial)) File.Delete(partial); }
                catch (Exception exception) { _message += " Temporary download cleanup could not finish: " + exception.Message; }
                finally { _progress = 0; _busy = false; _gate.Release(); }
            }
        }

        public UpdateStatus Install()
        {
            if (!_gate.Wait(0)) return Status();
            try
            {
                EnsureIdle();
                if (!_downloaded || _cache.Manifest == null) throw new InvalidOperationException("Download and verify an update first.");
                _busy = true; _message = "Installing verified update...";
                var release = _cache.Manifest;
                UpdateIntegrity.ValidateManifest(release, _host.ApplicationVersion);
                var transaction = new InstallTransaction { Version = release.Version, Sha256 = release.Asset.Sha256, SizeBytes = release.Asset.SizeBytes, PreviousVersion = _installed.ToString(3), PreviousSha256 = UpdateIntegrity.Hash(_target) };
                Save("pending-install.json", transaction);
                try { UpdateInstaller.Install(PathFor("downloaded.dll"), _target, PathFor("rollback"), release); }
                catch { File.Delete(PathFor("pending-install.json")); throw; }
                _host.NotifyPendingRestart(); _restartRequired = true; _downloaded = false;
                _message = _host.CanSelfRestart ? "Update installed. Restarting Emby Server..." : "Update installed. Restart Emby Server using its normal controls.";
                if (_host.CanSelfRestart) _ = RestartAfterResponse();
            }
            catch (Exception exception) { _message = "Installation stopped: " + exception.Message; }
            finally { _busy = false; _gate.Release(); }
            return Status();
        }

        public UpdateStatus Restart()
        {
            try { EnsureIdle(); if (_restartRequired && _host.CanSelfRestart) { _message = "Restarting Emby Server..."; _ = RestartAfterResponse(); } }
            catch (Exception exception) { _message = "Restart stopped: " + exception.Message; }
            return Status();
        }
        private async Task RestartAfterResponse()
        {
            await Task.Delay(1500).ConfigureAwait(false);
            try { EnsureIdle(); _host.Restart(); }
            catch (Exception exception) { _message = "The update is installed but Emby could not restart: " + exception.Message + " Restart Emby manually when idle."; }
        }
        private void EnsureIdle()
        {
            if (_host.IsShuttingDown || !_host.IsStartupComplete) throw new InvalidOperationException("Wait for Emby Server startup or shutdown to finish.");
            if (_library.IsScanRunning) throw new InvalidOperationException("Wait for the library scan to finish.");
            if (_tasks != null && _tasks.ScheduledTasks.Any(x => x.State != TaskState.Idle)) throw new InvalidOperationException("Wait for Emby's running scheduled tasks to finish.");
            if (ReportActivity.Active > 0) throw new InvalidOperationException("Wait for active Who Scene It reports to finish.");
            if (_sessions.Sessions.Any(x => x.NowPlayingItem != null)) throw new InvalidOperationException("Stop active and paused playback sessions before restarting Emby Server.");
        }

        private void VerifyStartup()
        {
            var pending = Read<InstallTransaction>("pending-install.json");
            if (pending != null)
            {
                try
                {
                    if (UpdateIntegrity.ParseVersion(pending.Version) != _installed) throw new InvalidDataException("The expected plugin version did not load.");
                    UpdateIntegrity.ValidateBinary(_target, pending.Version, pending.Sha256, pending.SizeBytes);
                    StartupVerified = true;
                    _message = "Who Scene It " + _installed.ToString(3) + " loaded. Waiting for the administrator dashboard to respond before cleanup.";
                }
                catch (Exception exception) { _message = "Update verification needs attention: " + exception.Message + " See the rollback instructions."; }
            }
            else { StartupVerified = true; }
        }
        public UpdateStatus ConfirmDashboardHealthy()
        {
            if (!_gate.Wait(0)) return Status();
            try
            {
                if (!_awaitingDashboard || !StartupVerified || _restartRequired || ReportActivity.Active > 0) return Status();
                var updated = File.Exists(PathFor("pending-install.json"));
                if (!_downloaded) DeleteOwnedDownloadFiles();
                if (updated) File.Delete(PathFor("pending-install.json"));
                _awaitingDashboard = false;
                if (updated) _message = "Who Scene It " + _installed.ToString(3) + " loaded and its dashboard responded. The previous DLL is available for rollback.";
            }
            catch (Exception exception) { _message = "The plugin is responding, but update cleanup failed: " + exception.Message; }
            finally { _gate.Release(); }
            return Status();
        }
        private void DeleteOwnedDownloadFiles()
        {
            foreach (var name in new[] { "download.partial", "downloaded.dll" })
                try { if (File.Exists(PathFor(name))) File.Delete(PathFor(name)); } catch { }
        }
        private static DateTimeOffset? RetryAfter(HttpResponseMessage response, DateTimeOffset now)
        {
            var after = response.Headers.RetryAfter;
            var until = after?.Date ?? (after?.Delta.HasValue == true ? now.Add(after.Delta.Value) : (DateTimeOffset?)null);
            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values) && long.TryParse(values.FirstOrDefault(), out var epoch))
            { var reset = DateTimeOffset.FromUnixTimeSeconds(epoch); if (!until.HasValue || until < reset) until = reset; }
            return until;
        }
        private static async Task<byte[]> ReadLimited(HttpResponseMessage response, int limit, CancellationToken token)
        {
            if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("The update manifest is too large.");
            using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var output = new MemoryStream())
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                var buffer = new byte[8192]; int count;
                while ((count = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                { if (output.Length + count > limit) throw new InvalidDataException("The update manifest is too large."); output.Write(buffer, 0, count); }
                return output.ToArray();
            }
        }
        public void Dispose() { _timer?.Dispose(); _stop.Cancel(); _http.Dispose(); }
    }
}
