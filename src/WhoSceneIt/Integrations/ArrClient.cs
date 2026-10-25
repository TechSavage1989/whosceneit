using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Serialization;

namespace WhoSceneIt.Integrations
{
    public sealed class ArrClient
    {
        private static readonly HttpClient DefaultHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) };
        private readonly HttpClient _http;
        private readonly IJsonSerializer _json;
        private readonly ArrConnection _connection;
        private readonly string _service;
        private string _api;
        private ArrRequestException _readFailure;
        public ArrClient(string service, ArrConnection connection, IJsonSerializer json, HttpClient http = null)
        { _service = service; _connection = connection.Copy(); _json = json; _http = http ?? DefaultHttp; }
        public static Uri ValidateBaseUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https") ||
                string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Use the service's HTTP or HTTPS base URL without credentials, a query, or a fragment.");
            return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
        }
        public ArrTestResult Connect()
        {
            if (string.IsNullOrWhiteSpace(_connection.ApiKey)) throw new InvalidOperationException(_service + " needs an API key.");
            var api = "api/v3/";
            var status = Send<ArrSystemStatus>("GET", api + "system/status", allowMissing: _service == "Sonarr");
            if (status == null && _service == "Sonarr") { api = "api/v5/"; status = Send<ArrSystemStatus>("GET", api + "system/status"); }
            if (status == null || string.IsNullOrWhiteSpace(status.version) || (!string.IsNullOrEmpty(status.appName) && !string.Equals(status.appName, _service, StringComparison.OrdinalIgnoreCase)))
            { _readFailure = new ArrRequestException("The URL did not return a valid " + _service + " server response."); throw _readFailure; }
            _api = api;
            return new ArrTestResult { Success = true, Message = "Connected to " + _service + " " + status.version + ". Read-only connection test passed.", ApiVersion = _api.Contains("v5") ? "v5" : "v3", Version = status.version };
        }
        public T Get<T>(string route, bool allowMissing = false) where T : class { EnsureConnected(); return Send<T>("GET", _api + route, allowMissing: allowMissing); }
        public void Mutate(string method, string route, object body = null) { EnsureConnected(); Send<object>(method, _api + route, body); }
        private void EnsureConnected() { if (_api == null) Connect(); }
        private T Send<T>(string method, string path, object body = null, bool allowMissing = false) where T : class => SendAsync<T>(method, path, body, allowMissing).GetAwaiter().GetResult();
        private async Task<T> SendAsync<T>(string method, string path, object body, bool allowMissing) where T : class
        {
            if (_readFailure != null) throw _readFailure; // One failed read stops this service for the batch.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(ValidateBaseUrl(_connection.BaseUrl), path));
                request.Headers.TryAddWithoutValidation("X-Api-Key", _connection.ApiKey);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                if (body != null) request.Content = new StringContent(_json.SerializeToString(body), Encoding.UTF8, "application/json");
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (allowMissing && response.StatusCode == HttpStatusCode.NotFound) return null;
                if (!response.IsSuccessStatusCode) throw new ArrRequestException(_service + " returned HTTP " + (int)response.StatusCode +
                    ((int)response.StatusCode == 401 || (int)response.StatusCode == 403 ? ". Check its API key and access permissions." : (int)response.StatusCode >= 300 && (int)response.StatusCode < 400 ? ". Redirects are not followed. Use the service's final base URL." : ". Check the service and its logs."));
                if (method != "GET") return null; // Do not parse or replay a mutation receipt.
                const int maximum = 32 * 1024 * 1024;
                if (response.Content.Headers.ContentLength > maximum) throw new ArrRequestException(_service + " returned an oversized response.");
                using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
                while ((count = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                { if (output.Length + count > maximum) throw new ArrRequestException(_service + " returned an oversized response."); output.Write(buffer, 0, count); }
                try { return _json.DeserializeFromString<T>(Encoding.UTF8.GetString(output.ToArray())) ?? throw new Exception(); }
                catch { throw new ArrRequestException(_service + " returned invalid JSON. Check its base URL and version."); }
            }
            catch (ArrRequestException e) { if (method == "GET") _readFailure = e; throw; }
            catch (Exception) { var error = new ArrRequestException(_service + " could not be reached or its response was lost. No request will be automatically replayed."); if (method == "GET") _readFailure = error; throw error; }
        }
    }
    public sealed class ArrRequestException : Exception { public ArrRequestException(string message) : base(message) { } }
    public sealed class ArrTestResult { public bool Success { get; set; } public string Message { get; set; } public string ApiVersion { get; set; } public string Version { get; set; } }
    public sealed class ArrSystemStatus { public string appName { get; set; } public string version { get; set; } }
    public sealed class ArrSeries { public int tvdbId { get; set; } public int id { get; set; } public string path { get; set; } }
    public sealed class ArrMovie { public int id { get; set; } public int tmdbId { get; set; } public string path { get; set; } public bool monitored { get; set; } public ArrFile movieFile { get; set; } }
    public sealed class ArrFile { public int id { get; set; } public int seriesId { get; set; } public long size { get; set; } public string path { get; set; } public string relativePath { get; set; } }
    public sealed class ArrEpisode { public int id { get; set; } public int seriesId { get; set; } public int episodeFileId { get; set; } public int seasonNumber { get; set; } public int episodeNumber { get; set; } public bool monitored { get; set; } }
}
