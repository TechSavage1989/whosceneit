using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using WhoSceneIt.Integrations;

namespace WhoSceneIt.Retention
{
    // Separate, atomic server-owned state. A damaged/unwritable store blocks cleanup.
    public sealed class RetentionStore
    {
        private static readonly object SharedSync = new object();
        private static RetentionStore _shared;
        private readonly object _sync = new object();
        private readonly string _file;
        private readonly IJsonSerializer _json;
        private RetentionState _state;
        private string _fault;
        public RetentionStore(string file, IJsonSerializer json)
        {
            _file = file; _json = json;
            try
            {
                // A backup proves state existed. Never replace a missing primary with empty rules.
                // Restoring an older backup can enable obsolete automatic rules, so require review.
                if (!File.Exists(file) && File.Exists(file + ".bak")) throw new InvalidDataException();
                if (File.Exists(file) && new FileInfo(file).Length > 32 * 1024 * 1024) throw new InvalidDataException();
                _state = File.Exists(file) ? json.DeserializeFromString<RetentionState>(File.ReadAllText(file)) : new RetentionState();
                if (_state == null || _state.SchemaVersion != 1 || _state.Protected == null || _state.Policies == null || _state.Reviews == null || _state.Holds == null || _state.Runs == null) throw new InvalidDataException();
                if (_state.Runs.Any(r => r.State == "Deleting" || r.Token != null)) Change(s => { foreach (var r in s.Runs) { if (r.State == "Deleting") { r.State = "Interrupted"; r.Message = "Emby restarted during cleanup. Stored targets remain on automation hold; no mutations will be replayed."; } r.Token = null; } });
            }
            catch { _fault = "Saved cleanup/protection state is missing, damaged, or incompatible. Existing files and backups were retained. Review and restore retention.json from a known backup, then restart Emby. Deletion is blocked; saved rules were not reset."; }
        }
        public static RetentionStore For(IApplicationPaths paths, IJsonSerializer json)
        {
            lock (SharedSync) return _shared ??= new RetentionStore(Path.Combine(paths.DataPath, "WhoSceneIt", "retention.json"), json);
        }
        public RetentionState Read() { lock (_sync) { Healthy(); return Copy(_state); } }
        private RetentionState Copy(RetentionState state) => _json.DeserializeFromString<RetentionState>(_json.SerializeToString(state));
        private void Healthy() { if (_fault != null) throw new InvalidOperationException(_fault); }
        public void Change(Action<RetentionState> change)
        {
            lock (_sync)
            {
                Healthy(); var next = Copy(_state); change(next);
                if (next.Protected.Length > 10000 || next.Policies.Length > 25 || next.Reviews.Length > 50 || next.Holds.Length > 5000) throw new InvalidOperationException("Cleanup state reached its limit. Review stored protection, prompts, and automation holds before adding more.");
                var data = Encoding.UTF8.GetBytes(_json.SerializeToString(next));
                if (data.Length > 32 * 1024 * 1024) throw new InvalidOperationException("Cleanup state reached its size limit. Review its stored history.");
                var temp = _file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_file));
                    using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(data, 0, data.Length); stream.Flush(true); }
                    if (File.Exists(_file)) File.Replace(temp, _file, _file + ".bak"); else File.Move(temp, _file);
                    _state = Copy(next);
                }
                catch { _fault = "Emby could not persist cleanup/protection state. Deletion is blocked until storage is repaired and Emby restarted."; throw new InvalidOperationException(_fault); }
                finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
            }
        }
        public IDisposable LockProtection() { Monitor.Enter(_sync); try { Healthy(); return new ActionLease(() => Monitor.Exit(_sync)); } catch { Monitor.Exit(_sync); throw; } }
        public string Reason(BaseItem item)
        {
            lock (_sync)
            {
                Healthy();
                var found = _state.Protected.FirstOrDefault(p => p.ItemId == Id(item) || SamePath(p.Path, item.Path) ||
                    (p.Type == "Series" && (p.ItemId == item.SeriesId.ToString(CultureInfo.InvariantCulture) || ChildPath(p.Path, item.Path))));
                return found == null ? null : "Never delete: " + found.Name + (found.Type == "Series" && !(item is Series) ? " (protected show and its episodes)." : ".");
            }
        }
        public ProtectionFlag Flag(BaseItem item)
        {
            var reason = Reason(item);
            lock (_sync)
            {
                var type = item is Series ? "Series" : item is Episode ? "Episode" : "Movie";
                var source = _state.Protected.FirstOrDefault(p => p.ItemId == Id(item) || p.Type == type && SamePath(p.Path, item.Path));
                return new ProtectionFlag { ItemId = Id(item), NeverDelete = reason != null, Explicit = source != null, SourceId = source?.ItemId, Reason = reason };
            }
        }
        public void Protect(string[] ids, bool protect, ILibraryManager library)
        {
            var records = protect ? ids.Select(id => {
                var item = library.GetItemById(long.Parse(id, CultureInfo.InvariantCulture));
                if (!(item is Movie || item is Episode || item is Series)) throw new ArgumentException("Only movies, shows, and episodes can be protected.");
                return new ProtectedMedia { ItemId = Id(item), Name = item.Name, Type = item is Series ? "Series" : item is Episode ? "Episode" : "Movie", Path = item.Path };
            }).ToArray() : Array.Empty<ProtectedMedia>();
            Change(s => s.Protected = s.Protected.Where(p => !ids.Contains(p.ItemId)).Concat(records).ToArray());
        }
        public static string Id(BaseItem item) => item.InternalId.ToString(CultureInfo.InvariantCulture);
        public static bool SamePath(string one, string two) => !string.IsNullOrWhiteSpace(one) && !string.IsNullOrWhiteSpace(two) && ArrPath.Equal(one, two);
        private static bool ChildPath(string one, string two) => !string.IsNullOrWhiteSpace(one) && !string.IsNullOrWhiteSpace(two) && ArrPath.Contains(one, two);
    }
}
