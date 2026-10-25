using System;
using System.Linq;
using System.Threading;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using WhoSceneIt.Retention;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Services;
using MediaBrowser.Model.Serialization;
using WhoSceneIt.Integrations;

namespace WhoSceneIt.Deletion
{
    [Route("/WhoSceneIt/Delete/Preview", "POST")]
    [Authenticated(Roles = "Admin")]
    public sealed class PreviewDeletion : IReturn<DeletionStatus>
    {
        public string UserId { get; set; }
        public string ItemIds { get; set; }
        public bool AllowUnwatched { get; set; }
    }
    [Route("/WhoSceneIt/Delete/Execute", "POST")]
    [Authenticated(Roles = "Admin")]
    public sealed class ExecuteDeletion : IReturn<DeletionStatus>
    {
        public string Token { get; set; }
        public bool Confirmed { get; set; }
    }
    [Route("/WhoSceneIt/Delete/Status", "GET")]
    [Authenticated(Roles = "Admin")]
    public sealed class GetDeletionStatus : IReturn<DeletionStatus> { public string Token { get; set; } }
    [Route("/WhoSceneIt/Delete/Cancel", "POST")]
    [Authenticated(Roles = "Admin")]
    public sealed class CancelDeletion : IReturn<DeletionStatus> { public string Token { get; set; } }

    public sealed class DeletionService : IService, IRequiresRequest
    {
        private static DeletionManager _manager;
        private readonly IAuthorizationContext _authorization;
        private readonly IUserManager _users;
        public IRequest Request { get; set; }
        public DeletionService(IAuthorizationContext authorization, IUserManager users, IUserDataManager data, ILibraryManager library, ISessionManager sessions, IApplicationHost host, IJsonSerializer json, IApplicationPaths paths)
        {
            _authorization = authorization; _users = users;
            Shared(users, data, library, sessions, host, json, paths);
        }
        public static DeletionManager Shared(IUserManager users, IUserDataManager data, ILibraryManager library, ISessionManager sessions, IApplicationHost host, IJsonSerializer json, IApplicationPaths paths)
        {
            var protection = RetentionStore.For(paths, json);
            return LazyInitializer.EnsureInitialized(ref _manager, () => new DeletionManager(new EmbyDeletionBackend(users, data, library, sessions, host, protection.Reason, protection.LockProtection),
                synchronization: new ArrSynchronization(ArrSettingsStore.Read, json, beginOperation: ArrSettingsStore.BeginOperation, hasNativeSeriesFiles: new EmbyCleanupCatalog(users, data, library).SeriesHasFiles)));
        }
        private string Actor()
        {
            if (Request == null) throw new UnauthorizedAccessException("Emby did not provide the signed-in request context. Reload this page and sign in again.");
            var user = _authorization.GetAuthorizationInfo(Request)?.User;
            var policy = user == null ? null : _users.GetUserPolicy(user);
            if (policy == null || !policy.IsAdministrator || policy.IsDisabled) throw new UnauthorizedAccessException("Sign in with an Emby administrator account to delete media.");
            return _users.GetUserDto(user).Id;
        }
        public object Post(PreviewDeletion request)
        {
            try
            {
                var actor = Actor();
                if (request == null || string.IsNullOrEmpty(request.ItemIds) || request.ItemIds.Length > 11000) throw new ArgumentException("Select between 1 and 500 titles.");
                return _manager.Preview(actor, request.UserId, request.ItemIds.Split(',').Select(s => s.Trim()).ToArray(), request.AllowUnwatched);
            }
            catch (Exception exception)
            {
                return new DeletionStatus { State = "PreviewFailed", Message = "No deletion was started for this selection. " + exception.Message };
            }
        }
        public object Post(ExecuteDeletion request) => _manager.Execute(Actor(), request.Token, request.Confirmed);
        public object Get(GetDeletionStatus request) => _manager.Status(Actor(), request.Token);
        public object Post(CancelDeletion request) => _manager.Cancel(Actor(), request.Token);
    }
}
