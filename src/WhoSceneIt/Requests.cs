using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace WhoSceneIt
{
    [Route("/WhoSceneIt/Users", "GET")]
    [Authenticated(Roles = "Admin")]
    public sealed class GetWatchInspectorUsers : IReturn<UserSummary[]> { }

    [Route("/WhoSceneIt/UserReport", "GET")]
    [Authenticated(Roles = "Admin")]
    public sealed class GetWatchInspectorUserReport : IReturn<PagedResult<ReportRow>>
    {
        public string UserId { get; set; }
        public string View { get; set; } = "Titles";
        public bool IncludeInProgress { get; set; }
        public string WatchState { get; set; }
        public string SeriesId { get; set; }
        public string SearchTerm { get; set; }
        public string SortBy { get; set; } = "LastPlayed";
        public int StartIndex { get; set; }
        public int Limit { get; set; } = 100;
    }

    [Route("/WhoSceneIt/Titles", "GET")]
    [Authenticated(Roles = "Admin")]
    public sealed class GetWatchInspectorTitles : IReturn<PagedResult<TitleSummary>>
    {
        public string SearchTerm { get; set; }
        public string Type { get; set; } = "All";
        public int StartIndex { get; set; }
        public int Limit { get; set; } = 50;
    }

    [Route("/WhoSceneIt/Viewers", "GET")]
    [Authenticated(Roles = "Admin")]
    public sealed class GetWatchInspectorViewers : IReturn<ViewerReport>
    {
        public string ItemId { get; set; }
        public bool IncludeInProgress { get; set; }
        public string WatchState { get; set; }
        public string SearchTerm { get; set; }
        public string SortBy { get; set; } = "LastPlayed";
        public int StartIndex { get; set; }
        public int Limit { get; set; } = 100;
    }
}
