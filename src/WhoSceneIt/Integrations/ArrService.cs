using System;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace WhoSceneIt.Integrations
{
    [Route("/WhoSceneIt/Integrations", "GET")]
    [Authenticated(Roles = "Admin")]
    public sealed class GetArrSettings : IReturn<ArrSettingsView> { }
    [Route("/WhoSceneIt/Integrations", "POST")]
    [Authenticated(Roles = "Admin")]
    public sealed class SaveArrSettings : IReturn<ArrSettingsView>
    {
        public ArrConnectionUpdate Sonarr { get; set; }
        public ArrConnectionUpdate Radarr { get; set; }
    }
    [Route("/WhoSceneIt/Integrations/Test", "POST")]
    [Authenticated(Roles = "Admin")]
    public sealed class TestArrConnection : IReturn<ArrTestResult>
    {
        public string Service { get; set; }
        public ArrConnectionUpdate Connection { get; set; }
    }
    public sealed class ArrService : IService
    {
        private readonly IJsonSerializer _json;
        public ArrService(IJsonSerializer json) { _json = json; }
        public object Get(GetArrSettings request) => ArrSettingsView.From(ArrSettingsStore.Read());
        public object Post(SaveArrSettings request) => ArrSettingsStore.Save(request?.Sonarr, request?.Radarr);
        public object Post(TestArrConnection request)
        {
            try
            {
                if (request?.Service != "Sonarr" && request?.Service != "Radarr") throw new ArgumentException("Choose Sonarr or Radarr.");
                var saved = ArrSettingsStore.Read();
                var connection = request.Connection?.Merge(request.Service == "Sonarr" ? saved.Sonarr : saved.Radarr) ?? throw new ArgumentException("Supply connection settings.");
                var client = new ArrClient(request.Service, connection, _json);
                var result = client.Connect();
                if (request.Service == "Sonarr") client.Get<ArrSeries[]>("series"); else client.Get<ArrMovie[]>("movie");
                return result;
            }
            catch (Exception e) { return new ArrTestResult { Success = false, Message = e.Message }; }
        }
    }
}
