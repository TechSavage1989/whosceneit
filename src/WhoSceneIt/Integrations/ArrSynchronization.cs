using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using MediaBrowser.Model.Serialization;
using WhoSceneIt.Deletion;

namespace WhoSceneIt.Integrations
{
    public interface IDeletionSynchronization { IExternalDeletionSession BeginPreview(); }
    public interface IExternalDeletionSession
    {
        void Preview(DeletionInspection item);
        void CompletePreview();
        IDisposable BeginOperation();
        void BeforeDelete(DeletionInspection item);
        void AfterDelete(DeletionInspection item);
    }
    public sealed class ArrSynchronization : IDeletionSynchronization
    {
        private readonly Func<ArrSettings> _settings;
        private readonly IJsonSerializer _json;
        private readonly HttpClient _http;
        private readonly Func<string, IDisposable> _begin;
        private readonly Func<DeletionInspection, bool> _hasNativeSeriesFiles;
        public ArrSynchronization(Func<ArrSettings> settings, IJsonSerializer json, HttpClient http = null, Func<string, IDisposable> beginOperation = null, Func<DeletionInspection, bool> hasNativeSeriesFiles = null)
        { _settings = settings; _json = json; _http = http; _begin = beginOperation; _hasNativeSeriesFiles = hasNativeSeriesFiles ?? (_ => true); }
        public IExternalDeletionSession BeginPreview() => new Session(_settings, _json, _http, _begin, _hasNativeSeriesFiles);
        private sealed class Target
        {
            public string Service, Path, ParentPath;
            public int Id, FileId, SeriesId, TmdbId, TvdbId;
            public long Size;
            public int[] Episodes;
            public int Season, EpisodeStart, EpisodeEnd;
        }
        private sealed class Session : IExternalDeletionSession
        {
            private readonly ArrSettings _saved;
            private readonly string _key;
            private readonly Func<ArrSettings> _current;
            private readonly Func<string, IDisposable> _begin;
            private readonly ArrClient _sonarr, _radarr;
            private readonly Func<DeletionInspection, bool> _hasNativeSeriesFiles;
            private ArrMovie[] _movies;
            private ArrSeries[] _series;
            private readonly Dictionary<int, ArrFile[]> _files = new Dictionary<int, ArrFile[]>();
            private readonly Dictionary<int, ArrEpisode[]> _episodes = new Dictionary<int, ArrEpisode[]>();
            public Session(Func<ArrSettings> settings, IJsonSerializer json, HttpClient http, Func<string, IDisposable> begin, Func<DeletionInspection, bool> hasNativeSeriesFiles)
            {
                _hasNativeSeriesFiles = hasNativeSeriesFiles;
                _current = settings; _saved = settings().Copy(); _key = _saved.Key(); _begin = begin;
                _sonarr = new ArrClient("Sonarr", _saved.Sonarr, json, http); _radarr = new ArrClient("Radarr", _saved.Radarr, json, http);
            }
            private void Current() { if (_current().Copy().Key() != _key) throw new InvalidOperationException("Synchronization settings changed. Review the deletion selection again."); }
            public IDisposable BeginOperation() { Current(); return _begin?.Invoke(_key) ?? new ActionLease(() => { }); }
            public void CompletePreview() { _movies = null; _series = null; _files.Clear(); _episodes.Clear(); }
            public void Preview(DeletionInspection item)
            {
                var movie = item.Item.Type == "Movie";
                var connection = movie ? _saved.Radarr : _saved.Sonarr;
                item.Item.ExternalService = movie ? "Radarr" : "Sonarr";
                item.Item.ExternalState = connection.Enabled ? "Checking" : "Disabled";
                if (!connection.Enabled) { item.Item.ExternalMessage = "Not enabled. This item will be deleted through Emby only."; return; }
                try
                {
                    Current();
                    var path = ArrPath.Translate(item.MediaPath, connection);
                    Target target;
                    if (movie)
                    {
                        _movies ??= _radarr.Get<ArrMovie[]>("movie");
                        var matches = _movies.Where(m => m != null && m.movieFile != null && m.movieFile.id > 0 && ArrPath.Equal(MoviePath(m), path)).ToArray();
                        if (matches.Length != 1) throw new InvalidOperationException("Radarr did not provide one exact movie-file match. Check the path mapping; title-only matches are not used.");
                        var m = matches[0];
                        target = new Target { Service = "Radarr", Id = m.id, FileId = m.movieFile.id, TmdbId = m.tmdbId, Size = m.movieFile.size, Path = path, ParentPath = m.path };
                        item.Item.ExternalMessage = "Unmonitor the matched movie, then remove its Radarr entry after Emby deletes the media." + (connection.AddImportExclusion ? " Request an import-list exclusion." : " No import-list exclusion requested.");
                    }
                    else
                    {
                        _series ??= _sonarr.Get<ArrSeries[]>("series");
                        var shows = _series.Where(s => s != null && !string.IsNullOrEmpty(s.path) && ArrPath.Contains(s.path, path)).ToArray();
                        if (shows.Length != 1) throw new InvalidOperationException("Sonarr did not provide one exact series-path match. Check the path mapping.");
                        var s = shows[0];
                        if (!_files.TryGetValue(s.id, out var files)) _files[s.id] = files = _sonarr.Get<ArrFile[]>("episodefile?seriesId=" + s.id);
                        var matches = files.Where(f => f != null && ArrPath.Equal(FilePath(f, s.path), path)).ToArray();
                        if (matches.Length != 1) throw new InvalidOperationException("Sonarr did not provide one exact episode-file match. No media will be deleted for this item.");
                        var file = matches[0];
                        if (!_episodes.TryGetValue(s.id, out var episodes)) _episodes[s.id] = episodes = _sonarr.Get<ArrEpisode[]>("episode?seriesId=" + s.id);
                        var linked = episodes.Where(e => e != null && e.episodeFileId == file.id).ToArray();
                        if (!item.SeasonNumber.HasValue || !item.EpisodeNumber.HasValue || linked.Length == 0 || linked.Any(e => e.id <= 0 || e.seriesId != s.id || e.seasonNumber != item.SeasonNumber || e.episodeNumber < item.EpisodeNumber || e.episodeNumber > (item.EpisodeNumberEnd ?? item.EpisodeNumber)))
                            throw new InvalidOperationException("This Sonarr file includes episodes outside the selected Emby episode or its numbered range. Review shared episode files in Sonarr.");
                        target = new Target { Service = "Sonarr", Id = file.id, FileId = file.id, SeriesId = s.id, TvdbId = s.tvdbId, Size = file.size, ParentPath = s.path, Path = path,
                            Season = item.SeasonNumber.Value, EpisodeStart = item.EpisodeNumber.Value, EpisodeEnd = item.EpisodeNumberEnd ?? item.EpisodeNumber.Value,
                            Episodes = linked.Select(e => e.id).Distinct().OrderBy(id => id).ToArray() };
                        item.Item.ExternalMessage = "Unmonitor " + target.Episodes.Length + " matched episode(s), then remove the Sonarr file record after Emby deletes the media. " + (connection.RemoveEmptySeries ? "Remove the Sonarr show entry only if no episode files remain in Sonarr or Emby; do not delete additional files." : "Keep the Sonarr show entry and unmonitor removed episodes.");
                    }
                    if (target.Id <= 0 || target.FileId <= 0 || target.Size < 0) throw new InvalidOperationException("The service returned an invalid media identity.");
                    if (!item.MediaSize.HasValue || item.MediaSize.Value != target.Size) throw new InvalidOperationException(target.Service + " and Emby report different file sizes. Check the exact file and path mapping before deletion.");
                    item.ExternalPlan = target; item.ExternalIdentity = target.Service + ":" + target.Id;
                    item.Item.ExternalPath = target.Path; item.Item.ExternalState = "Ready";
                }
                catch (Exception e) { item.Item.ExternalState = "Blocked"; item.Item.ExternalMessage = e.Message; throw; }
            }
            private static string FilePath(ArrFile file, string parent) => !string.IsNullOrWhiteSpace(file.path) ? file.path : ArrPath.Combine(parent, file.relativePath);
            private static string MoviePath(ArrMovie movie) => FilePath(movie.movieFile, movie.path);
            private void Validate(Target target, bool afterDeletion = false)
            {
                Current();
                if (target.Service == "Radarr")
                {
                    var m = _radarr.Get<ArrMovie>("movie/" + target.Id);
                    if (m.id != target.Id || m.tmdbId != target.TmdbId || !ArrPath.Equal(m.path, target.ParentPath) ||
                        (m.movieFile == null ? !afterDeletion : m.movieFile.id != target.FileId || m.movieFile.size != target.Size || !ArrPath.Equal(MoviePath(m), target.Path)))
                        throw new InvalidOperationException("The matched Radarr movie or file changed. Review the selection again.");
                }
                else
                {
                    var s = _sonarr.Get<ArrSeries>("series/" + target.SeriesId);
                    var f = _sonarr.Get<ArrFile>("episodefile/" + target.Id);
                    var episodes = _sonarr.Get<ArrEpisode[]>("episode?episodeFileId=" + target.Id);
                    if (s.id != target.SeriesId || s.tvdbId != target.TvdbId || !ArrPath.Equal(s.path, target.ParentPath) || f.id != target.Id || f.seriesId != target.SeriesId || f.size != target.Size || !ArrPath.Equal(FilePath(f, s.path), target.Path) ||
                        episodes.Any(e => e.seriesId != target.SeriesId || e.seasonNumber != target.Season || e.episodeNumber < target.EpisodeStart || e.episodeNumber > target.EpisodeEnd) ||
                        (afterDeletion ? episodes.Any(e => !target.Episodes.Contains(e.id)) : !episodes.Select(e => e.id).OrderBy(id => id).SequenceEqual(target.Episodes)))
                        throw new InvalidOperationException("The matched Sonarr file, series, or linked episodes changed. Review the selection again.");
                }
            }
            public void BeforeDelete(DeletionInspection item)
            {
                if (!(item.ExternalPlan is Target target)) { Current(); return; }
                Validate(target);
                Exception error = null;
                try
                {
                    if (target.Service == "Sonarr") _sonarr.Mutate("PUT", "episode/monitor", new { episodeIds = target.Episodes, monitored = false });
                    else _radarr.Mutate("PUT", "movie/editor", new { movieIds = new[] { target.Id }, monitored = false });
                }
                catch (ArrRequestException e) { error = e; }
                // Read back after either a receipt or a lost response. Never replay a mutation.
                if (target.Service == "Sonarr")
                {
                    var episodes = _sonarr.Get<ArrEpisode[]>("episode?seriesId=" + target.SeriesId).Where(e => target.Episodes.Contains(e.id)).ToArray();
                    if (episodes.Length != target.Episodes.Length || episodes.Any(e => e.monitored)) throw error ?? new InvalidOperationException("Sonarr did not confirm that every matched episode is unmonitored. Media was preserved.");
                }
                else if (_radarr.Get<ArrMovie>("movie/" + target.Id).monitored) throw error ?? new InvalidOperationException("Radarr did not confirm the matched movie is unmonitored. Media was preserved.");
                Validate(target);
            }
            public void AfterDelete(DeletionInspection item)
            {
                if (!(item.ExternalPlan is Target target)) { Current(); return; }
                Current();
                var client = target.Service == "Sonarr" ? _sonarr : _radarr;
                var route = (target.Service == "Sonarr" ? "episodefile/" : "movie/") + target.Id;
                var absent = target.Service == "Sonarr" ? client.Get<ArrFile>(route, true) == null : client.Get<ArrMovie>(route, true) == null;
                if (!absent)
                {
                    Validate(target, afterDeletion: true);
                    if (target.Service == "Sonarr" && (File.Exists(item.MediaPath) || Directory.Exists(item.MediaPath)))
                        throw new InvalidOperationException("A file is present again at the Emby media path. Sonarr cleanup was stopped to preserve it.");
                    Exception error = null;
                    try { client.Mutate("DELETE", route + (target.Service == "Radarr" ? "?deleteFiles=false&addImportExclusion=" + (_saved.Radarr.AddImportExclusion ? "true" : "false") : "")); }
                    catch (ArrRequestException e) { error = e; }
                    absent = target.Service == "Sonarr" ? client.Get<ArrFile>(route, true) == null : client.Get<ArrMovie>(route, true) == null;
                    if (!absent) throw error ?? new InvalidOperationException(target.Service + " did not confirm removal of the matched record. Check the service before retrying anything.");
                }
                if (target.Service == "Sonarr")
                {
                    var episodes = client.Get<ArrEpisode[]>("episode?seriesId=" + target.SeriesId).Where(e => target.Episodes.Contains(e.id)).ToArray();
                    if (episodes.Length != target.Episodes.Length || episodes.Any(e => e.monitored || e.episodeFileId != 0)) throw new InvalidOperationException("Sonarr removed the original file record, but episode monitoring or file links need review.");
                    item.Item.ExternalMessage = "File record removed; affected episodes are unmonitored. The Sonarr show entry remains.";
                    if (_saved.Sonarr.RemoveEmptySeries)
                    {
                        var remaining = client.Get<ArrFile[]>("episodefile?seriesId=" + target.SeriesId);
                        if (remaining.Length == 0 && !_hasNativeSeriesFiles(item))
                        {
                            var series = client.Get<ArrSeries>("series/" + target.SeriesId, true);
                            if (series != null)
                            {
                                if (series.id != target.SeriesId || series.tvdbId != target.TvdbId || !ArrPath.Equal(series.path, target.ParentPath)) throw new InvalidOperationException("The empty Sonarr series identity changed. Its entry was retained.");
                                // Recheck at the last point before the single, non-replayed mutation.
                                if (client.Get<ArrFile[]>("episodefile?seriesId=" + target.SeriesId).Length != 0 || _hasNativeSeriesFiles(item)) { item.Item.ExternalMessage += " New or remaining episode files preserve the show entry."; return; }
                                Exception error = null;
                                try { client.Mutate("DELETE", "series/" + target.SeriesId + "?deleteFiles=false&addImportListExclusion=false"); }
                                catch (ArrRequestException e) { error = e; }
                                if (client.Get<ArrSeries>("series/" + target.SeriesId, true) != null) throw error ?? new InvalidOperationException("Sonarr did not confirm removal of the empty show entry. Review it before retrying anything.");
                            }
                            item.Item.ExternalMessage = "Episode file removed and unmonitored; verified empty Sonarr show entry removed with deleteFiles=false.";
                        }
                        else item.Item.ExternalMessage += " Remaining episode files preserve the show entry.";
                    }
                }
            }
        }
    }
}
