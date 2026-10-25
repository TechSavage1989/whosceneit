using System;
using System.IO;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;

namespace WhoSceneIt.Updates
{
    public sealed class UpdateEntryPoint : IServerEntryPoint
    {
        private readonly UpdateManager _manager;
        public UpdateEntryPoint(IApplicationPaths paths, IJsonSerializer json, IApplicationHost host, ILibraryManager library, ISessionManager sessions, ITaskManager tasks)
        {
            _manager = new UpdateManager(Path.Combine(paths.DataPath, "WhoSceneIt"), typeof(Plugin).Assembly.Location, json, host, library, sessions, tasks: tasks);
            UpdateManager.Instance = _manager;
        }
        public void Run() => _manager.Start();
        public void Dispose() => _manager.Dispose();
    }
}
