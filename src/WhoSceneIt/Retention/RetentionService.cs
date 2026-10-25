using System;
using System.Globalization;
using System.Linq;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using WhoSceneIt.Deletion;

namespace WhoSceneIt.Retention
{
    [Route("/WhoSceneIt/Retention", "GET"), Authenticated(Roles = "Admin")]
    public sealed class GetRetention : IReturn<CleanupStatus> { }
    [Route("/WhoSceneIt/Retention/Protection", "GET"), Authenticated(Roles = "Admin")]
    public sealed class GetProtection : IReturn<ProtectionFlag[]> { public string ItemIds { get; set; } }
    [Route("/WhoSceneIt/Retention/Protection", "POST"), Authenticated(Roles = "Admin")]
    public sealed class SaveProtection : IReturn<CleanupStatus> { public string ItemIds { get; set; } public bool Protected { get; set; } }
    [Route("/WhoSceneIt/Retention/Policy", "POST"), Authenticated(Roles = "Admin")]
    public sealed class SaveCleanupPolicy : IReturn<CleanupPolicy> { public CleanupPolicy Policy { get; set; } public bool Confirmed { get; set; } }
    [Route("/WhoSceneIt/Retention/Policy/Remove", "POST"), Authenticated(Roles = "Admin")]
    public sealed class RemoveCleanupPolicy : IReturn<CleanupStatus> { public string Id { get; set; } }
    [Route("/WhoSceneIt/Retention/Evaluate", "POST"), Authenticated(Roles = "Admin")]
    public sealed class EvaluateCleanup : IReturn<CleanupStatus> { public string Id { get; set; } }
    [Route("/WhoSceneIt/Retention/Review", "POST"), Authenticated(Roles = "Admin")]
    public sealed class OpenCleanupReview : IReturn<DeletionStatus> { public string Id { get; set; } }
    [Route("/WhoSceneIt/Retention/Dismiss", "POST"), Authenticated(Roles = "Admin")]
    public sealed class DismissCleanupReview : IReturn<CleanupStatus> { public string Id { get; set; } }
    [Route("/WhoSceneIt/Retention/Dashboard", "POST"), Authenticated(Roles = "Admin")]
    public sealed class CleanupDashboardOpened : IReturn<CleanupStatus> { }
    [Route("/WhoSceneIt/Retention/Hold/Clear", "POST"), Authenticated(Roles = "Admin")]
    public sealed class ClearCleanupHold : IReturn<CleanupStatus> { public string RunId { get; set; } public string ItemId { get; set; } public bool Confirmed { get; set; } }
    public sealed class RetentionService : IService, IRequiresRequest
    {
        private readonly IAuthorizationContext _authorization;
        private readonly IUserManager _users;
        private readonly ILibraryManager _library;
        private readonly RetentionStore _store;
        private readonly CleanupCoordinator _coordinator;
        public IRequest Request { get; set; }
        public RetentionService(IAuthorizationContext authorization, IUserManager users, IUserDataManager data, ILibraryManager library, ISessionManager sessions, IApplicationHost host, IJsonSerializer json, IApplicationPaths paths)
        { _authorization = authorization; _users = users; _library = library; _store = RetentionStore.For(paths, json); _coordinator = CleanupRuntime.Get(users, data, library, sessions, host, json, paths); }
        private string Actor()
        {
            if (Request == null) throw new UnauthorizedAccessException("A signed-in Emby administrator request is required.");
            var user = _authorization.GetAuthorizationInfo(Request)?.User; var policy = user == null ? null : _users.GetUserPolicy(user);
            if (policy == null || !policy.IsAdministrator || policy.IsDisabled) throw new UnauthorizedAccessException("Sign in with an active Emby administrator account.");
            return _users.GetUserDto(user).Id;
        }
        public object Get(GetRetention request) { var actor = Actor(); var status = _coordinator.Status(); status.ActorId = actor; return status; }
        public object Get(GetProtection request) { Actor(); return Ids(request.ItemIds).Select(id => _library.GetItemById(long.Parse(id, CultureInfo.InvariantCulture))).Where(i => i != null).Select(_store.Flag).ToArray(); }
        public object Post(SaveProtection request) { Actor(); _store.Protect(Ids(request.ItemIds), request.Protected, _library); return Get(new GetRetention()); }
        public object Post(SaveCleanupPolicy request) => _coordinator.Save(Actor(), request.Policy, request.Confirmed);
        public object Post(RemoveCleanupPolicy request) { _coordinator.Remove(Actor(), request.Id); return Get(new GetRetention()); }
        public object Post(EvaluateCleanup request) => _coordinator.EvaluateNow(Actor(), request.Id);
        public object Post(OpenCleanupReview request) => _coordinator.OpenReview(Actor(), request.Id);
        public object Post(DismissCleanupReview request) { _coordinator.Dismiss(Actor(), request.Id); return Get(new GetRetention()); }
        public object Post(CleanupDashboardOpened request) { _coordinator.SignalEvent("Dashboard opened", actor: Actor()); return Get(new GetRetention()); }
        public object Post(ClearCleanupHold request)
        {
            Actor();
            if (!request.Confirmed) throw new ArgumentException("Confirm that this held item was reviewed before allowing future automatic cleanup attempts.");
            _store.Change(s => { if (!s.Holds.Any(h => h.RunId == request.RunId && h.ItemId == request.ItemId)) throw new ArgumentException("This automation hold is no longer present."); s.Holds = s.Holds.Where(h => h.RunId != request.RunId || h.ItemId != request.ItemId).ToArray(); });
            return Get(new GetRetention());
        }
        private static string[] Ids(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 11000) throw new ArgumentException("Select between 1 and 500 library items.");
            var ids = value.Split(',').Select(s => s.Trim()).Distinct().ToArray();
            if (ids.Length > 500 || ids.Any(s => !long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)) throw new ArgumentException("Select between 1 and 500 valid library items.");
            return ids;
        }
    }
    public static class CleanupRuntime
    {
        private static readonly object Sync = new object();
        private static CleanupCoordinator _coordinator;
        public static CleanupCoordinator Get(IUserManager users, IUserDataManager data, ILibraryManager library, ISessionManager sessions, IApplicationHost host, IJsonSerializer json, IApplicationPaths paths)
        {
            lock (Sync) return _coordinator ??= new CleanupCoordinator(RetentionStore.For(paths, json), new EmbyCleanupCatalog(users, data, library), DeletionService.Shared(users, data, library, sessions, host, json, paths));
        }
    }
}
