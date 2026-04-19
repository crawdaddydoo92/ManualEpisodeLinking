using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace ManualEpisodeLinking
{
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        public static Plugin? Instance { get; private set; }

        private readonly PlaybackMonitor _monitor;

        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ISessionManager sessionManager,
            ILogger<PlaybackMonitor> logger)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;

            // 🔥 THIS starts everything
            _monitor = new PlaybackMonitor(sessionManager, logger);
        }

        public override string Name => "Manual Episode Linking";

        public override Guid Id => new Guid("d6b0f5c2-9c0f-4b1a-9f1e-123456789abc");
    }
}