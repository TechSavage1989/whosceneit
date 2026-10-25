using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace WhoSceneIt
{
    public sealed class PluginConfiguration : BasePluginConfiguration { public Integrations.ArrSettings Arr { get; set; } = new Integrations.ArrSettings(); }

    public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public static Plugin Instance { get; private set; }
        // This identifier belongs to this plugin and must stay unchanged across upgrades.
        public override Guid Id => new Guid("b6405a2e-52e6-4218-a014-00a46a65f1f8");
        public override string Name => "Who Scene It";
        public override string Description => "Find watched movies and TV shows by user, or find viewers of a title. Administrator access required.";

        public Plugin(IApplicationPaths paths, IXmlSerializer serializer) : base(paths, serializer) { Instance = this; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "whosceneit",
                    DisplayName = "Who Scene It",
                    EmbeddedResourcePath = "WhoSceneIt.Web.whosceneit.html",
                    EnableInMainMenu = true,
                    MenuSection = "server",
                    MenuIcon = "assessment",
                    IsMainConfigPage = true
                },
                new PluginPageInfo
                {
                    Name = "whosceneit-1.4.0.js",
                    EmbeddedResourcePath = "WhoSceneIt.Web.whosceneit.js"
                },
                new PluginPageInfo
                {
                    Name = "whosceneit-1.3.0.js",
                    EmbeddedResourcePath = "WhoSceneIt.Web.whosceneit.js"
                },
                new PluginPageInfo
                {
                    Name = "whosceneit.js",
                    EmbeddedResourcePath = "WhoSceneIt.Web.whosceneit.js"
                }
            };
        }
    }
}
