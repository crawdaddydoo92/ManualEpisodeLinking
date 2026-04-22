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
        private readonly Dictionary<string, bool> _hasLinked = new();
        private readonly Dictionary<string, bool> _skipTriggered = new();
        
        // 🔥 Session state tracking
        private readonly HashSet<string> _blockedLastHop = new();
        private readonly HashSet<string> _nearEndHandled = new();
        private readonly Dictionary<string, DateTime> _lastPlaybackStart = new();
        private readonly Dictionary<string, DateTime> _lastNearEnd = new();
        private readonly Dictionary<string, string> _expectedNext = new();

        // Tracks sessions where this plugin initiated playback (SendPlayCommand)
        // so we can ignore those transitions in skip detection and prevent
        // infinite relinking / playback loops
        private readonly HashSet<string> _pluginInitiated = new();

        private double _triggerPercent = 0.99;
        private double _endBufferSeconds = 3;
        
        //Following is needed for links.json auto-reload
        private FileSystemWatcher _watcher;
        private DateTime _lastReload = DateTime.MinValue;

        public PlaybackMonitor(ISessionManager sessionManager, ILogger<PlaybackMonitor> logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;

            _logger.LogInformation("[ManualEpisodeLinking] Monitor started");

            LoadLinks();
           
            var path = "/etc/jellyfin/plugins/ManualEpisodeLinking/links.json";

            _watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(path) ?? "/etc/jellyfin/plugins/ManualEpisodeLinking",
                Filter = Path.GetFileName(path),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _watcher.Changed += OnLinksFileChanged;
            _watcher.Created += OnLinksFileChanged;
            _watcher.Renamed += OnLinksFileChanged;

            _watcher.EnableRaisingEvents = true;

            _logger.LogInformation("[ManualEpisodeLinking] File watcher initialized");
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

        private void OnLinksFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                var now = DateTime.UtcNow;

                // 🔥 debounce multiple rapid triggers
                if ((now - _lastReload).TotalMilliseconds < 500)
                    return;

                _lastReload = now;

                // 🔥 allow file write to complete
                Thread.Sleep(300);

                LoadLinks();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ManualEpisodeLinking] Failed to reload links.json");
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
                        var position = session.PlayState?.PositionTicks ?? 0;
                        var duration = session.NowPlayingItem?.RunTimeTicks ?? 0;
                        var sessionId = session.Id;

                        if (session.NowPlayingItem != null)
                        {
                            var currentId = NormalizeId(session.NowPlayingItem.Id.ToString());

                            if (_lastItemId.TryGetValue(sessionId, out var previousId))
                            {
                                if (previousId != currentId
                                    && !_blockedLastHop.Contains(sessionId)
                                    && !_pluginInitiated.Contains(sessionId))
                                {
                                    if (_lastSkip.TryGetValue(sessionId, out var lastTime))
                                    {
                                        if ((DateTime.UtcNow - lastTime).TotalSeconds < 2)
                                            continue;
                                    }

                                    _lastSkip[sessionId] = DateTime.UtcNow;

                                    // 🛑 Ignore false skip right after manual playback start
                                    if (_lastPlaybackStart.TryGetValue(sessionId, out var startTime))
                                    {
                                        if ((DateTime.UtcNow - startTime).TotalSeconds < 5)
                                        {
                                            continue;
                                        }
                                    }


                                    if (_links.TryGetValue(previousId, out var nextId))
                                    {
                                        if (session.PlayState?.PositionTicks == null ||
                                            session.NowPlayingItem?.RunTimeTicks == null)
                                        {
                                            continue;
                                        }

                                        if (position > duration * 0.90)
                                            continue;

                                        _logger.LogInformation("[ManualEpisodeLinking] SKIP DETECTED");

                                        // 🔥 mark this as a skip (not plugin-driven)
                                        _skipTriggered[sessionId] = true;

                                        // 🔥 CRITICAL — mark this as plugin-driven
                                        _pluginInitiated.Add(sessionId);

                                        bool isLastHop = !_links.ContainsKey(nextId);
                                        bool hasLinked = _hasLinked.GetValueOrDefault(sessionId, false);

                                        _logger.LogInformation($"[ManualEpisodeLinking] DEBUG link: isLastHop={isLastHop}, hasLinked={hasLinked}");
                                        if (isLastHop && !hasLinked)
                                        {
                                            _logger.LogInformation("[ManualEpisodeLinking] Blocking final hop (manual entry into chain end)");
                                            continue;
                                        }

                                        
                                        // 🔥 cancel any pending auto transition
                                        _pendingNext.Remove(sessionId);
                                        _expectedNext[sessionId] = nextId;

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
                                        _hasLinked[sessionId] = true;
                                    }

                                    
                                }
                            }


                            if (_lastItemId.TryGetValue(sessionId, out var lastId) && lastId != currentId)
                            {
                               
                                _triggered.Remove(sessionId);
                                _nearEndHandled.Remove(sessionId);
                                _blockedLastHop.Remove(sessionId);

                               _logger.LogInformation($"[ManualEpisodeLinking] DEBUG pluginInitiated={_pluginInitiated.Contains(sessionId)}");
                               
                                // ✅ Capture state BEFORE clearing
                                bool isPluginTransition = _pluginInitiated.Contains(sessionId);

                                if (isPluginTransition)
                                {
                                    _ = Task.Run(async () =>
                                    {
                                        await Task.Delay(3000);
                                        _pluginInitiated.Remove(sessionId);
                                    });
                                }

                                if (!isPluginTransition &&
                                    !_pendingNext.ContainsKey(sessionId) &&
                                    session.PlayState?.PositionTicks is long pos &&
                                    pos < 1_000_000)
                                {
                                    _hasLinked[sessionId] = false;
                                    _logger.LogInformation("[ManualEpisodeLinking] Reset link state (fresh playback start)");
                                }

                                if (isPluginTransition)
                                {
                                    _logger.LogInformation("[ManualEpisodeLinking] Preserving link state (plugin transition)");
                                }
                            }
                            
                            _lastItemId[sessionId] = currentId;
                            _lastItemName[sessionId] = session.NowPlayingItem.Name;
                        }

                        if (session.PlayState?.PositionTicks != null &&
                            session.NowPlayingItem?.RunTimeTicks != null)
                        {

                            if (position < duration * 0.1 && !_triggered.Contains(sessionId))
                            {
                                _triggered.Add(sessionId);
                                
                                // ALWAYS track playback start
                                _lastPlaybackStart[sessionId] = DateTime.UtcNow;

                                if (_pluginInitiated.Contains(sessionId))
                                {
                                    _pluginInitiated.Remove(sessionId);
                                    _logger.LogInformation("[ManualEpisodeLinking] Cleared pluginInitiated (playback stabilized)");
                                }

                                // 🔥 clear plugin-initiated state after playback stabilizes
                                _logger.LogInformation("[ManualEpisodeLinking] Skipping redundant reset (handled earlier)");

                                // 🔥 clear pluginInitiated after playback stabilizes
                                _logger.LogInformation("[ManualEpisodeLinking] Trigger reset for new playback");


                            }

                            if (position > duration * 0.80)
                            {
                                if (_nearEndHandled.Contains(sessionId))
                                    continue;

                                if (_blockedLastHop.Contains(sessionId)) // 🔥 prevent near-end override after skip
                                    continue;

                                _nearEndHandled.Add(sessionId);


                                if (_lastItemId.TryGetValue(sessionId, out var lastId))
                                {
                                    var currentId = session.NowPlayingItem.Id.ToString("N");
                                    var lastName = _lastItemName.GetValueOrDefault(sessionId, "UNKNOWN");

                                    _logger.LogInformation($"[ManualEpisodeLinking] NEAR END: {lastName}");

                                    // 🔍 DEBUG (always runs)
                                    _logger.LogInformation($"[ManualEpisodeLinking] DEBUG currentId={currentId}");
                                    _logger.LogInformation($"[ManualEpisodeLinking] DEBUG lastId={lastId}");

                                    bool hasLinked = _hasLinked.GetValueOrDefault(sessionId, false);

                                    if (hasLinked)
                                    {
                                        _logger.LogInformation("[ManualEpisodeLinking] Skipping near-end (already linked)");
                                        continue;
                                    }

                                        _nearEndHandled.Add(sessionId);

                                        

                                        if (_links.TryGetValue(currentId, out var nextId))
                                        {
                                            bool isLastHop = !_links.ContainsKey(nextId);
                                            _logger.LogInformation($"[ManualEpisodeLinking] LINK FOUND: {currentId} → {nextId}");
                                        

                                            // Block final hop when user did NOT arrive via link
                                            if (isLastHop && !hasLinked)
                                            {
                                                _logger.LogInformation("[ManualEpisodeLinking] Blocked final hop (no prior link)");

                                                _pendingNext.Remove(sessionId);
                                                _blockedLastHop.Add(sessionId);

                                                continue;
                                            }
                                        
                                            // Only queue once per playback
                                            if (!_pendingNext.ContainsKey(sessionId))
                                            {
                                                // 🛑 Prevent instant trigger from scrubbing into near-end
                                                if (_lastNearEnd.TryGetValue(sessionId, out var lastNearEndTime))
                                                {
                                                    if ((DateTime.UtcNow - lastNearEndTime).TotalSeconds < 2)
                                                    {
                                                        continue;
                                                    }
                                                }
                                            
                                                _lastNearEnd[sessionId] = DateTime.UtcNow;

                                                _hasLinked[sessionId] = true;

                                                _pendingNext[sessionId] = nextId;

                                                _logger.LogInformation("[ManualEpisodeLinking] Queued next episode (near-end)");

                                            }
                                        }
                                    }
                                }
                            }
                
                            
                            // 🔥 NEW: execute after natural stop (position reset near 0)
                            if (_pendingNext.TryGetValue(sessionId, out var pendingId) &&
                                position < 1_000_000 && // 🔥 ONLY trigger after true reset
                                !_skipTriggered.GetValueOrDefault(sessionId, false) // 🔥 CRITICAL
                                && !_blockedLastHop.Contains(sessionId)) // 🔥 prevent delayed auto-transition after final-hop skip
                                
                            {
                                _pendingNext.Remove(sessionId);

                                // Clear final-hop block on new playback
                                _blockedLastHop.Remove(sessionId);

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
                            

                                // 🔥 ONLY mark as plugin-initiated if this was NOT triggered by skip
                                if (!_skipTriggered.GetValueOrDefault(sessionId, false))
                                {
                                   _logger.LogInformation("[ManualEpisodeLinking] DEBUG setting pluginInitiated TRUE");
                                    _pluginInitiated.Add(sessionId);
                                }

                                // ✅ Mark link usage only if transition came from a link (not skip)
                                _logger.LogInformation("[ManualEpisodeLinking] Played queued episode");

                                if (!_skipTriggered.GetValueOrDefault(sessionId, false))
                                {
                                    _hasLinked[sessionId] = true;
                                    _logger.LogInformation("[ManualEpisodeLinking] Link state set: hasLinked = true");
                                }

                                else
                                {
                                    _logger.LogInformation("[ManualEpisodeLinking] Skip transition - not marking as linked");
                                }

                                _skipTriggered.Remove(sessionId);
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
