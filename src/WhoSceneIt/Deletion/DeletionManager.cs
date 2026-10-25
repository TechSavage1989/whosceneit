using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace WhoSceneIt.Deletion
{
    public sealed class DeletionItem
    {
        public string ItemId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Detail { get; set; }
        public string[] Paths { get; set; } = Array.Empty<string>();
        public string[] WatchedBy { get; set; } = Array.Empty<string>();
        public string State { get; set; } = "Ready";
        public string Message { get; set; }
    }

    public sealed class DeletionInspection
    {
        public DeletionItem Item { get; set; }
        public string Fingerprint { get; set; }
    }

    public sealed class DeletionStatus
    {
        public string Token { get; set; }
        public string State { get; set; }
        public string ExpiresUtc { get; set; }
        public string Message { get; set; }
        public int Total { get; set; }
        public int Deleted { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public int Ready { get; set; }
        public DeletionItem[] Items { get; set; } = Array.Empty<DeletionItem>();
    }

    public interface IDeletionBackend
    {
        string[] Resolve(string userId, string[] rootIds);
        DeletionInspection Inspect(string actorId, string userId, string itemId);
        void Delete(string actorId, string userId, string itemId, string expectedFingerprint);
        void EnsureAvailable();
    }

    // Jobs exist only after an administrator explicitly confirms a frozen preview.
    // They run inside Emby, keep no worker/service outside it, and never auto-start.
    public sealed class DeletionManager
    {
        public const int MaximumItems = 500;
        private readonly object _sync = new object();
        private readonly Dictionary<string, Plan> _plans = new Dictionary<string, Plan>(StringComparer.Ordinal);
        private readonly IDeletionBackend _backend;
        private readonly Func<DateTimeOffset> _now;
        private bool _running;
        private sealed class Plan
        {
            public string Token, ActorId, UserId, State = "Ready", Message;
            public DateTimeOffset Expires;
            public DeletionInspection[] Entries;
            public bool Cancel;
        }
        public DeletionManager(IDeletionBackend backend, Func<DateTimeOffset> now = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _now = now ?? (() => DateTimeOffset.UtcNow);
        }
        public DeletionStatus Preview(string actorId, string userId, string[] rootIds)
        {
            if (string.IsNullOrWhiteSpace(actorId)) throw new UnauthorizedAccessException("An administrator sign-in is required.");
            if (rootIds == null || rootIds.Length == 0 || rootIds.Length > MaximumItems)
                throw new ArgumentException("Select between 1 and 500 titles. Split larger selections into smaller batches.");
            if (rootIds.Any(id => !long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)) throw new ArgumentException("Select valid library items.");
            _backend.EnsureAvailable();
            var ids = _backend.Resolve(userId, rootIds.Distinct(StringComparer.Ordinal).ToArray()).Distinct(StringComparer.Ordinal).Take(MaximumItems + 1).ToArray();
            if (ids.Length > MaximumItems) throw new ArgumentException("This selection contains more than 500 media items. Use TV episodes or a smaller selection.");
            var entries = ids.Select(id => _backend.Inspect(actorId, userId, id)).ToArray();
            // Never let two different library IDs cause the same physical target to be removed twice.
            var paths = new HashSet<string>(System.IO.Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var entry in entries.Where(e => e.Item.State == "Ready"))
            {
                if (entry.Item.Paths.Any(p => paths.Contains(p))) { entry.Item.State = "Blocked"; entry.Item.Message = "This file is already included by another selected item."; }
                else foreach (var path in entry.Item.Paths) paths.Add(path);
            }
            lock (_sync)
            {
                foreach (var key in _plans.Where(p => p.Value.State != "Deleting" && p.Value.Expires <= _now()).Select(p => p.Key).ToArray()) _plans.Remove(key);
                foreach (var key in _plans.Where(p => p.Value.State != "Deleting" && p.Value.State != "Ready").OrderBy(p => p.Value.Expires).Take(Math.Max(0, _plans.Count - 63)).Select(p => p.Key).ToArray()) _plans.Remove(key);
                if (_plans.Count >= 64 || _plans.Values.Count(p => p.ActorId == actorId && (p.State == "Ready" || p.State == "Deleting")) >= 8)
                    throw new InvalidOperationException("Close or cancel older deletion previews, or wait ten minutes before trying again.");
                var plan = new Plan { Token = Guid.NewGuid().ToString("N"), ActorId = actorId, UserId = userId, Expires = _now().AddMinutes(10), Entries = entries,
                    Message = "Review the exact media and paths below. Deletion removes them from the server for every user." };
                _plans.Add(plan.Token, plan);
                return Snapshot(plan);
            }
        }
        public DeletionStatus Status(string actorId, string token) { lock (_sync) return Snapshot(Find(actorId, token)); }
        public DeletionStatus Cancel(string actorId, string token)
        {
            lock (_sync)
            {
                var plan = Find(actorId, token); plan.Cancel = true;
                if (plan.State == "Ready")
                {
                    plan.State = "Cancelled"; plan.Message = "Deletion cancelled. No files were removed.";
                    foreach (var entry in plan.Entries.Where(e => e.Item.State == "Ready")) { entry.Item.State = "Skipped"; entry.Item.Message = "Cancelled before deletion."; }
                }
                else if (plan.State == "Deleting") plan.Message = "Stopping after the current media item finishes.";
                return Snapshot(plan);
            }
        }
        public DeletionStatus Execute(string actorId, string token, string confirmation)
        {
            lock (_sync)
            {
                var plan = Find(actorId, token);
                if (confirmation != "DELETE") throw new ArgumentException("Type DELETE to confirm permanent media deletion.");
                if (plan.State != "Ready") return Snapshot(plan); // Replayed POSTs cannot launch a second deletion.
                if (_running) throw new InvalidOperationException("Another deletion is running. Wait for it to finish.");
                _backend.EnsureAvailable();
                if (!plan.Entries.Any(e => e.Item.State == "Ready")) throw new InvalidOperationException("No items in this preview can be deleted.");
                // Acquire before returning or scheduling work, so update/restart guards see the operation.
                var activity = new ReportActivity();
                _running = true; plan.State = "Deleting"; plan.Message = "Deleting confirmed media...";
                _ = Task.Run(() => Run(plan, activity));
                return Snapshot(plan);
            }
        }
        private void Run(Plan plan, IDisposable activity)
        {
            try
            {
                foreach (var entry in plan.Entries)
                {
                    lock (_sync)
                    {
                        if (entry.Item.State != "Ready") continue;
                        if (plan.Cancel) { entry.Item.State = "Skipped"; entry.Item.Message = "Cancelled before this item was deleted."; continue; }
                    }
                    try
                    {
                        _backend.EnsureAvailable();
                        var current = _backend.Inspect(plan.ActorId, plan.UserId, entry.Item.ItemId);
                        if (current.Item.State != "Ready" || current.Fingerprint != entry.Fingerprint)
                        {
                            lock (_sync) { entry.Item.State = "Skipped"; entry.Item.Message = current.Item.Message ?? "This item or its deletion paths changed. Review it again."; }
                            continue;
                        }
                        _backend.Delete(plan.ActorId, plan.UserId, entry.Item.ItemId, entry.Fingerprint);
                        lock (_sync) { entry.Item.State = "Deleted"; entry.Item.Message = "Deleted through Emby."; }
                    }
                    catch (Exception exception)
                    {
                        lock (_sync) { entry.Item.State = "Failed"; entry.Item.Message = "Emby could not finish deleting this item: " + exception.Message + " Refresh the library before retrying; some files may already be removed."; }
                    }
                }
            }
            finally
            {
                activity.Dispose();
                lock (_sync)
                {
                    _running = false; plan.Expires = _now().AddMinutes(10);
                    plan.State = plan.Cancel ? "Cancelled" : "Completed";
                    plan.Message = plan.Cancel ? "Deletion stopped. Completed deletions cannot be undone." : "Deletion finished. Review any blocked, skipped, or failed items below.";
                }
            }
        }
        private Plan Find(string actorId, string token)
        {
            if (string.IsNullOrEmpty(token) || !_plans.TryGetValue(token, out var plan) || plan.ActorId != actorId)
                throw new ArgumentException("This deletion preview is unavailable. Review your selection again.");
            if (plan.State != "Deleting" && plan.Expires <= _now()) throw new ArgumentException("This deletion preview expired. Review your selection again.");
            return plan;
        }
        private static DeletionStatus Snapshot(Plan plan)
        {
            var items = plan.Entries.Select(e => new DeletionItem { ItemId = e.Item.ItemId, Name = e.Item.Name, Type = e.Item.Type, Detail = e.Item.Detail,
                Paths = e.Item.Paths.ToArray(), WatchedBy = e.Item.WatchedBy.ToArray(), State = e.Item.State, Message = e.Item.Message }).ToArray();
            return new DeletionStatus { Token = plan.Token, State = plan.State, Message = plan.Message, ExpiresUtc = plan.Expires.ToString("O"), Items = items,
                Total = items.Length, Ready = items.Count(i => i.State == "Ready"), Deleted = items.Count(i => i.State == "Deleted"), Failed = items.Count(i => i.State == "Failed"),
                Skipped = items.Count(i => i.State == "Blocked" || i.State == "Skipped") };
        }
    }
}
