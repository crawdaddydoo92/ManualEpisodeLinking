using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace ManualEpisodeLinking
{
    public class PlaybackMonitor
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<PlaybackMonitor> _logger;

        private Dictionary<string, string> _links = new();
        private readonly Dictionary<string, string> _lastItemId = new();
        private readonly Dictionary<string, string> _lastItemName = new();
        private readonly HashSet<string> _triggered = new();
        private readonly Dictionary<string, DateTime> _lastSkip = new();

        // 🔥 NEW (ONLY addition)
        private readonly Dictionary<string, string> _pendingNext = new();

        private double _triggerPercent = 0.99;
        private double _endBufferSeconds = 3;

        public PlaybackMonitor(ISessionManager sessionManager, ILogger<PlaybackMonitor> logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;

            _logger.LogInformation("[ManualEpisodeLinking] Monitor started");

            LoadLinks();
            _ = Task.Run(MonitorLoop);
        }

        private void LoadLinks()
        {
            var path = "/etc/jellyfin/plugins/ManualEpisodeLinking/links.json";

            try
            {
                if (!File.Exists(path))
                {
                    _logger.LogWarning("[ManualEpisodeLinking] links.json not found");
                    return;
                }

                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);

                var root = doc.RootElement;

                if (root.TryGetProperty("Settings", out var settings))
                {
                    if (settings.TryGetProperty("TriggerPercent", out var tp))
                        _triggerPercent = tp.GetDouble();

                    if (settings.TryGetProperty("EndBufferSeconds", out var eb))
                        _endBufferSeconds = eb.GetDouble();
                }

                var linksElement = root.GetProperty("Links");

                _links = new Dictionary<string, string>();

                foreach (var prop in linksElement.EnumerateObject())
                {
                    var key = NormalizeId(prop.Name);
                    var value = NormalizeId(prop.Value.GetString() ?? "");
                    _links[key] = value;
                }

                _logger.LogInformation($"[ManualEpisodeLinking] Loaded {_links.Count} links");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ManualEpisodeLinking] Failed to load links.json");
            }
        }

        private string NormalizeId(string id)
        {
            return id.Replace("-", "").ToLowerInvariant();
        }

        private async Task MonitorLoop()
        {
            while (true)
            {
                try
                {
                    var sessions = _sessionManager.Sessions;

                    foreach (var session in sessions)
                    {
                        var sessionId = session.Id;

                        if (session.NowPlayingItem != null)
                        {
                            var currentId = NormalizeId(session.NowPlayingItem.Id.ToString());

                            if (_lastItemId.TryGetValue(sessionId, out var previousId))
                            {
                                if (previousId != currentId)
                                {
                                    if (_lastSkip.TryGetValue(sessionId, out var lastTime))
                                    {
                                        if ((DateTime.UtcNow - lastTime).TotalSeconds < 2)
                                            continue;
                                    }

                                    _lastSkip[sessionId] = DateTime.UtcNow;

                                    if (_links.TryGetValue(previousId, out var nextId))
                                    {
                                        _logger.LogInformation("[ManualEpisodeLinking] SKIP DETECTED");

                                        await _sessionManager.SendPlayCommand(
                                            session.Id,
                                            session.Id,
                                            new PlayRequest
                                            {
                                                ItemIds = new[] { Guid.ParseExact(nextId, "N") },
                                                StartPositionTicks = 0
                                            },
                                            default
                                        );

                                        _logger.LogInformation("[ManualEpisodeLinking] Skip override sent");
                                    }

                                    _triggered.Remove(sessionId);
                                }
                            }

                            _lastItemId[sessionId] = currentId;
                            _lastItemName[sessionId] = session.NowPlayingItem.Name;
                        }

                        if (session.PlayState?.PositionTicks != null &&
                            session.NowPlayingItem?.RunTimeTicks != null)
                        {
                            var position = session.PlayState.PositionTicks.Value;
                            var duration = session.NowPlayingItem.RunTimeTicks.Value;

                            if (_triggered.Contains(sessionId) && position < duration * 0.1)
                            {
                                _triggered.Remove(sessionId);
                                _logger.LogInformation("[ManualEpisodeLinking] Trigger reset for new playback");
                            }

                            if (position > duration * _triggerPercent &&
                                (duration - position) > TimeSpan.FromSeconds(_endBufferSeconds).Ticks)
                            {
                                if (_triggered.Contains(sessionId))
                                    continue;

                                _triggered.Add(sessionId);

                                if (_lastItemId.TryGetValue(sessionId, out var lastId))
                                {
                                    var lastName = _lastItemName.GetValueOrDefault(sessionId, "UNKNOWN");

                                    _logger.LogInformation($"[ManualEpisodeLinking] NEAR END: {lastName}");

                                    if (_links.TryGetValue(lastId, out var nextId))
                                    {
                                        _logger.LogInformation($"[ManualEpisodeLinking] LINK FOUND: {lastId} → {nextId}");

                                        // 🔥 CHANGED: queue instead of immediate jump
                                        _pendingNext[sessionId] = nextId;
                                        _logger.LogInformation("[ManualEpisodeLinking] Queued next episode");
                                    }
                                }
                            }

                            // 🔥 NEW: execute after natural stop (position reset near 0)
                            if (_pendingNext.TryGetValue(sessionId, out var pendingId) &&
                                position < duration * 0.1)
                            {
                                _pendingNext.Remove(sessionId);

                                await Task.Delay(500);

                                await _sessionManager.SendPlayCommand(
                                    session.Id,
                                    session.Id,
                                    new PlayRequest
                                    {
                                        ItemIds = new[] { Guid.ParseExact(pendingId, "N") },
                                        StartPositionTicks = 0
                                    },
                                    default
                                );

                                _logger.LogInformation("[ManualEpisodeLinking] Played queued episode");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[ManualEpisodeLinking] ERROR: {ex}");
                }

                await Task.Delay(500);
            }
        }
    }
}
