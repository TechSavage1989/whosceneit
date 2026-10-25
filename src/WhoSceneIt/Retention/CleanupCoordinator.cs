using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using WhoSceneIt.Deletion;

namespace WhoSceneIt.Retention
{
    public sealed class CleanupCoordinator
    {
        private readonly RetentionStore _store;
        private readonly ICleanupCatalog _catalog;
        private readonly DeletionManager _deletion;
        private readonly Func<DateTimeOffset> _now;
        private readonly object _sync = new object();
        private readonly Dictionary<string, Signal> _signals = new Dictionary<string, Signal>();
        private int _busy;
        private string _message;
        private sealed class Signal { public DateTimeOffset Due; public bool Full; public HashSet<string> Items = new HashSet<string>(); public string Trigger; }
        public CleanupCoordinator(RetentionStore store, ICleanupCatalog catalog, DeletionManager deletion, Func<DateTimeOffset> now = null)
        { _store = store; _catalog = catalog; _deletion = deletion; _now = now ?? (() => DateTimeOffset.UtcNow); }
        public CleanupStatus Status()
        {
            var state = _store.Read();
            foreach (var run in state.Runs.Where(r => r.State == "Deleting" && r.Token != null))
            {
                try { var live = _deletion.Status(run.OwnerId, run.Token); run.State = live.State; run.Message = live.Message; run.Total = live.Total; run.Deleted = live.Deleted; run.Failed = live.Failed; run.Skipped = live.Skipped; run.SyncFailed = live.SyncFailed; }
                catch (ArgumentException) { run.State = "Needs review"; run.Token = null; run.Message = "The in-memory operation is unavailable. Stored automation holds remain in place."; }
            }
            var enabled = state.Policies.Count(p => p.Enabled);
            var message = _message ?? (state.Policies.Length == 0 ? "No cleanup rules are saved. Create a rule to configure cleanup." :
                state.Policies.Length + " saved cleanup rule(s). " + enabled + " enabled; " + (state.Policies.Length - enabled) + " disabled. " +
                (enabled > 0 ? "Configured schedules and triggers remain active." : "No rules are enabled. Evaluate now still prepares a review."));
            return new CleanupStatus { Settings = state, Busy = Volatile.Read(ref _busy) != 0 || _deletion.IsRunning, Message = message };
        }
        public CleanupPolicy Save(string actor, CleanupPolicy policy, bool confirmed)
        {
            if (policy == null || string.IsNullOrWhiteSpace(policy.Name) || policy.Name.Length > 100 || string.IsNullOrWhiteSpace(policy.UserId) || policy.UserId.Length > 100) throw new ArgumentException("Provide a rule name and a watch-history user.");
            Choice(policy.Scope, "Both", "Movies", "Episodes", "CompletedShows"); Choice(policy.WatchMode, "Watched", "Resume", "Both"); Choice(policy.Action, "Review", "Automatic"); Choice(policy.Schedule, "Off", "Interval", "Daily", "Weekly");
            if (policy.MinimumDays < 0 || policy.MinimumDays > 3650 || policy.MaximumItems < 1 || policy.MaximumItems > 500 || policy.IntervalHours < 1 || policy.IntervalHours > 168 || policy.Weekday < 0 || policy.Weekday > 6 || !TimeSpan.TryParseExact(policy.TimeUtc, @"hh\:mm", CultureInfo.InvariantCulture, out _)) throw new ArgumentException("Check the waiting period, batch limit, and UTC schedule.");
            if (policy.Enabled && policy.Action == "Automatic" && !confirmed) throw new ArgumentException("Check the authorization box to enable unattended permanent deletion for this rule.");
            policy.OwnerId = actor; policy.Revision = Guid.NewGuid().ToString("N"); policy.Name = policy.Name.Trim();
            var ownerError = _catalog.OwnerError(policy); if (ownerError != null) throw new ArgumentException(ownerError);
            policy.NextRunUtc = CleanupSchedule.Next(policy, _now())?.ToString("O"); policy.LastEvaluatedUtc = null;
            _store.Change(s => {
                var old = string.IsNullOrEmpty(policy.Id) ? null : s.Policies.SingleOrDefault(p => p.Id == policy.Id) ?? throw new ArgumentException("This cleanup rule no longer exists.");
                if (old != null && old.OwnerId != actor) throw new UnauthorizedAccessException("Only the rule owner can change it.");
                policy.Id = old?.Id ?? Guid.NewGuid().ToString("N");
                s.Policies = s.Policies.Where(p => p.Id != policy.Id).Concat(new[] { policy }).ToArray(); s.Reviews = s.Reviews.Where(r => r.PolicyId != policy.Id).ToArray();
            });
            StopPolicy(policy.Id); _message = policy.Name + (policy.Enabled ? ": rule saved and enabled. Its configured schedules and triggers are active." : ": rule saved and disabled. Evaluate now still prepares a review.");
            return _store.Read().Policies.Single(p => p.Id == policy.Id);
        }
        public void Remove(string actor, string id)
        {
            _store.Change(s => { var p = Owned(s, actor, id); s.Policies = s.Policies.Where(x => x.Id != p.Id).ToArray(); s.Reviews = s.Reviews.Where(x => x.PolicyId != p.Id).ToArray(); });
            StopPolicy(id); _message = "Cleanup rule removed. Any in-flight item may finish; remaining queued items are stopped.";
        }
        private void StopPolicy(string id)
        {
            lock (_sync) _signals.Remove(id);
            foreach (var run in _store.Read().Runs.Where(r => r.PolicyId == id && r.State == "Deleting")) try { _deletion.Cancel(run.OwnerId, run.Token); } catch (ArgumentException) { }
        }
        public CleanupStatus EvaluateNow(string actor, string id)
        { var policy = Owned(_store.Read(), actor, id); Evaluate(policy, "On demand", null, false); return Status(); }
        public void Dismiss(string actor, string id) => _store.Change(s => { var review = s.Reviews.SingleOrDefault(r => r.Id == id && r.OwnerId == actor) ?? throw new ArgumentException("This cleanup prompt is unavailable."); s.Reviews = s.Reviews.Where(r => r.Id != review.Id).ToArray(); });
        public DeletionStatus OpenReview(string actor, string id)
        {
            var state = _store.Read(); var review = state.Reviews.SingleOrDefault(r => r.Id == id && r.OwnerId == actor) ?? throw new ArgumentException("This cleanup prompt is unavailable.");
            var policy = Owned(state, actor, review.PolicyId);
            if (policy.Revision != review.Revision) throw new InvalidOperationException("This rule changed. Evaluate it again before deleting anything.");
            return Plan(policy, review.ItemIds, review.Trigger, false, review.Id);
        }
        public void SignalEvent(string trigger, string userId = null, string itemId = null, string actor = null)
        {
            var now = _now();
            foreach (var p in _store.Read().Policies.Where(p => p.Enabled && (actor == null || p.OwnerId == actor) && (userId == null || p.UserId == userId)))
            {
                var enabled = trigger == "Playback stopped" ? p.OnPlaybackStopped : trigger == "Watched state changed" ? p.OnWatchedChanged : trigger == "Startup" ? p.OnStartup : trigger == "Dashboard opened" && p.OnDashboard;
                if (!enabled) continue;
                var due = now.AddSeconds(30); var cooldown = CleanupSchedule.Parse(p.LastEvaluatedUtc)?.AddMinutes(15); if (cooldown > due) due = cooldown.Value;
                lock (_sync) { if (!_signals.TryGetValue(p.Id, out var signal)) _signals[p.Id] = signal = new Signal { Due = due, Trigger = trigger }; if (itemId == null) signal.Full = true; else if (signal.Items.Count < 500) signal.Items.Add(itemId); }
            }
        }
        public void Tick()
        {
            if (Volatile.Read(ref _busy) != 0 || _deletion.IsRunning) return;
            try
            {
                var now = _now();
                foreach (var policy in _store.Read().Policies.Where(p => p.Enabled).OrderBy(p => CleanupSchedule.Parse(p.LastEvaluatedUtc) ?? DateTimeOffset.MinValue))
                {
                    Signal signal; lock (_sync) _signals.TryGetValue(policy.Id, out signal);
                    var scheduled = CleanupSchedule.Parse(policy.NextRunUtc) <= now;
                    if (!scheduled && (signal == null || signal.Due > now)) continue;
                    if (signal != null) lock (_sync) _signals.Remove(policy.Id);
                    Evaluate(policy, scheduled ? "Schedule" : signal.Trigger, scheduled || signal.Full ? null : signal.Items.ToArray(), true); break;
                }
            }
            catch (Exception e) { _message = "Cleanup paused: " + e.Message; }
        }
        private void Evaluate(CleanupPolicy policy, string trigger, string[] events, bool automated)
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) throw new InvalidOperationException("Another cleanup evaluation is running.");
            try
            {
                var now = _now();
                _store.Change(s => { var current = Owned(s, policy.OwnerId, policy.Id); if (current.Revision != policy.Revision || automated && !current.Enabled) throw new InvalidOperationException("The cleanup rule changed."); current.LastEvaluatedUtc = now.ToString("O"); if (trigger == "Schedule" || !CleanupSchedule.Parse(current.NextRunUtc).HasValue) current.NextRunUtc = CleanupSchedule.Next(current, now)?.ToString("O"); });
                var state = _store.Read();
                var ids = _catalog.Candidates(policy, now, events).Where(id => {
                    var item = _catalog.Item(id); return item != null && _store.Reason(item) == null && !state.Holds.Any(h => h.ItemId == id || RetentionStore.SamePath(h.Path, item.Path));
                }).Take(policy.MaximumItems).ToArray();
                if (ids.Length == 0) { _message = policy.Name + ": no eligible, unprotected media. Automation holds are excluded."; Record(policy, trigger, "No matches", _message, 0); return; }
                if (!automated || policy.Action == "Review")
                {
                    _store.Change(s => s.Reviews = s.Reviews.Where(r => r.PolicyId != policy.Id).Concat(new[] { new CleanupReview { Id = Guid.NewGuid().ToString("N"), PolicyId = policy.Id, Revision = policy.Revision, Name = policy.Name, OwnerId = policy.OwnerId, Trigger = trigger, CreatedUtc = now.ToString("O"), ItemIds = ids } }).ToArray());
                    _message = policy.Name + ": " + ids.Length + " item(s) await review. No deletion has started."; Record(policy, trigger, "Review queued", _message, ids.Length); return;
                }
                var preview = Plan(policy, ids, trigger, true, null);
                if (preview.Ready == 0) { _deletion.Cancel(policy.OwnerId, preview.Token); _message = "Cleanup found no deletable items after native/service checks."; Record(policy, trigger, "Blocked", _message, preview.Total); return; }
                var started = _deletion.Execute(policy.OwnerId, preview.Token, true);
                if (!started.Started) { _message = policy.Name + ": no deletion started. " + started.Message; Record(policy, trigger, "Not started", _message, preview.Total); return; }

            }
            catch (Exception e) { _message = policy.Name + ": " + e.Message; Record(policy, trigger, "Needs review", _message, 0); }
            finally { Volatile.Write(ref _busy, 0); }
        }
        private DeletionStatus Plan(CleanupPolicy policy, string[] ids, string trigger, bool automated, string reviewId)
        {
            var runId = Guid.NewGuid().ToString("N");
            string Check(string id)
            {
                var state = _store.Read(); var current = state.Policies.SingleOrDefault(p => p.Id == policy.Id);
                if (current == null || current.Revision != policy.Revision || automated && !current.Enabled) return "The cleanup rule changed or was disabled. Prepare a new review.";
                var item = _catalog.Item(id);
                if (item == null || state.Holds.Any(h => h.RunId != runId && (h.ItemId == id || RetentionStore.SamePath(h.Path, item.Path)))) return "A previous cleanup attempt needs review. Automatic mutations will not be replayed.";
                return _catalog.Ineligible(policy, id, _now());
            }
            var hooks = new DeletionPlanHooks { RuleName = policy.Name, Validate = Check,
                Begin = (status, entries) => {
                    _store.Change(s => {
                        var current = Owned(s, policy.OwnerId, policy.Id); if (current.Revision != policy.Revision || automated && !current.Enabled) throw new InvalidOperationException("This cleanup rule changed.");
                        if (entries.Any(e => s.Holds.Any(h => h.ItemId == e.Item.ItemId || RetentionStore.SamePath(h.Path, e.MediaPath)))) throw new InvalidOperationException("A previous cleanup attempt needs review.");
                        s.Holds = s.Holds.Concat(entries.Select(e => new CleanupHold { RunId = runId, ItemId = e.Item.ItemId, Path = e.MediaPath, Name = e.Item.Name, Fingerprint = e.Fingerprint })).ToArray();
                        s.Reviews = s.Reviews.Where(r => r.Id != reviewId).ToArray();
                        s.Runs = s.Runs.Concat(new[] { new CleanupRun { Id = runId, OwnerId = policy.OwnerId, PolicyId = policy.Id, Name = policy.Name, Trigger = trigger, StartedUtc = _now().ToString("O"), State = "Deleting", Token = status.Token, Total = status.Total } }).Reverse().Take(100).Reverse().ToArray();
                    });
                    _message = policy.Name + ": cleanup started. Open its progress to monitor or stop remaining items.";
                },
                Complete = status => { _store.Change(s => {
                    var r = s.Runs.Single(x => x.Id == runId); r.State = status.State; r.Message = status.Message; r.FinishedUtc = _now().ToString("O"); r.Deleted = status.Deleted; r.Skipped = status.Skipped; r.Failed = status.Failed; r.SyncFailed = status.SyncFailed;
                    var safe = status.Items.Where(i => i.State == "Deleted" && i.ExternalState != "Failed" || (i.State == "Skipped" || i.State == "Blocked") && (i.ExternalState == "Not run" || i.ExternalService == null || i.ExternalState == "Disabled")).Select(i => i.ItemId).ToArray();
                    s.Holds = s.Holds.Where(h => h.RunId != runId || !safe.Contains(h.ItemId)).ToArray();
                }); _message = policy.Name + ": " + status.Message; } };
            return _deletion.Preview(policy.OwnerId, policy.UserId, ids, false, policy.WatchMode != "Watched", hooks);
        }
        private void Record(CleanupPolicy p, string trigger, string state, string message, int count) => _store.Change(s => s.Runs = s.Runs.Concat(new[] { new CleanupRun { Id = Guid.NewGuid().ToString("N"), OwnerId = p.OwnerId, PolicyId = p.Id, Name = p.Name, Trigger = trigger, State = state, Message = message, StartedUtc = _now().ToString("O"), FinishedUtc = _now().ToString("O"), Total = count } }).Reverse().Take(100).Reverse().ToArray());
        private static CleanupPolicy Owned(RetentionState s, string actor, string id) => s.Policies.SingleOrDefault(p => p.Id == id && p.OwnerId == actor) ?? throw new ArgumentException("This cleanup rule is unavailable to this administrator.");
        private static void Choice(string value, params string[] choices) { if (!choices.Contains(value)) throw new ArgumentException("Select a supported cleanup option."); }
    }
}
