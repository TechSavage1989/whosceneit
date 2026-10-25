using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;

namespace WhoSceneIt.Retention
{
    public sealed class CleanupEntryPoint : IServerEntryPoint
    {
        private readonly IUserManager _users;
        private readonly IUserDataManager _data;
        private readonly ISessionManager _sessions;
        private readonly CleanupCoordinator _coordinator;
        private Timer _timer;
        private bool _disposed;
        public CleanupEntryPoint(IUserManager users, IUserDataManager data, ILibraryManager library, ISessionManager sessions, IApplicationHost host, IJsonSerializer json, IApplicationPaths paths)
        { _users = users; _data = data; _sessions = sessions; _coordinator = CleanupRuntime.Get(users, data, library, sessions, host, json, paths); }
        public void Run()
        {
            if (_timer != null || _disposed) return;
            _data.UserDataSaved += Saved; _sessions.PlaybackStopped += Stopped;
            try { _coordinator.SignalEvent("Startup"); } catch { }
            _timer = new Timer(_ => { if (!_disposed) _ = Task.Run(() => _coordinator.Tick()); }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }
        private void Saved(object sender, UserDataSaveEventArgs e)
        {
            if (_disposed || e?.Item == null || e.User == null || (e.SaveReason != UserDataSaveReason.TogglePlayed && !(e.SaveReason == UserDataSaveReason.PlaybackFinished && e.UserData?.Played == true))) return;
            Signal("Watched state changed", e.User, e.Item);
        }
        private void Stopped(object sender, PlaybackStopEventArgs e)
        {
            if (_disposed || e?.Item == null) return;
            foreach (var user in e.Users ?? new System.Collections.Generic.List<User>()) Signal("Playback stopped", user, e.Item);
        }
        private void Signal(string trigger, User user, BaseItem item) { try { _coordinator.SignalEvent(trigger, _users.GetUserDto(user).Id, RetentionStore.Id(item)); } catch { } }
        public void Dispose() { _disposed = true; _timer?.Dispose(); _timer = null; _data.UserDataSaved -= Saved; _sessions.PlaybackStopped -= Stopped; }
    }
}
