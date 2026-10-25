using System;
using System.Threading.Tasks;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace WhoSceneIt.Updates
{
    [Route("/WhoSceneIt/Updates", "GET")]
    [Authenticated(Roles = "Admin")]
    public sealed class GetUpdates : IReturn<UpdateStatus> { }
    [Route("/WhoSceneIt/Updates/Check", "POST")]
    [Authenticated(Roles = "Admin")]
    public sealed class CheckUpdates : IReturn<UpdateStatus> { public string Reason { get; set; } = "manual"; }
    [Route("/WhoSceneIt/Updates/Download", "POST")]
    [Authenticated(Roles = "Admin")]
    public sealed class DownloadUpdate : IReturn<UpdateStatus> { }
    [Route("/WhoSceneIt/Updates/Install", "POST")]
    [Authenticated(Roles = "Admin")]
    public sealed class InstallUpdate : IReturn<UpdateStatus> { }
    [Route("/WhoSceneIt/Updates/Restart", "POST")]
    [Authenticated(Roles = "Admin")]
    public sealed class RestartUpdate : IReturn<UpdateStatus> { }
    [Route("/WhoSceneIt/Updates/Healthy", "POST")]
    [Authenticated(Roles = "Admin")]
    public sealed class ConfirmDashboardHealth : IReturn<UpdateStatus> { }

    public sealed class UpdateService : IService
    {
        private static UpdateManager Manager => UpdateManager.Instance ?? throw new InvalidOperationException("The plugin is still starting. Try again shortly.");
        public object Get(GetUpdates request) => Manager.Status();
        public Task<UpdateStatus> Post(CheckUpdates request) => Manager.CheckAsync(request.Reason == "focus" ? "focus" : "manual");
        public object Post(DownloadUpdate request) => Manager.BeginDownload();
        public object Post(InstallUpdate request) => Manager.Install();
        public object Post(RestartUpdate request) => Manager.Restart();
        public object Post(ConfirmDashboardHealth request) => Manager.ConfirmDashboardHealthy();
    }
}
