using System;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Settings;
using CinemaModule.Models;
using CinemaModule.Player;
using CinemaModule.Services;
using CinemaModule.Settings;
using CinemaModule.UI.Displays;

namespace CinemaModule
{
    public class CinemaController : IDisposable
    {
        #region Members

        private static readonly Logger Logger = Logger.GetLogger<CinemaController>();
        private const float DefaultWorldScreenWidth = 10f;

        private readonly CinemaSettings _moduleSettings;
        private readonly CinemaUserSettings _userSettings;
        private readonly TwitchService _twitchService;

        private VideoPlayer _videoPlayer;
        private WindowVideoDisplay _windowDisplay;
        private WorldVideoDisplay _worldDisplay;
        private bool _isDisposed;
        private bool _isPausedDueToRange;

        #endregion

        #region Events

        public event EventHandler ShowSettingsRequested;

        #endregion

        public CinemaController(CinemaSettings coreSettings, CinemaUserSettings userSettings, TwitchService twitchService)
        {
            _moduleSettings = coreSettings;
            _userSettings = userSettings;
            _twitchService = twitchService;

            SubscribeToSettingsEvents();
            _twitchService.QualitiesChanged += OnTwitchQualitiesChanged;
        }

        #region Public Methods

        public void RegisterPlayer(VideoPlayer player)
        {
            _videoPlayer = player;
            _videoPlayer.QualitiesChanged += OnVideoQualitiesChanged;

            if (_moduleSettings.IsEnabled && !string.IsNullOrEmpty(_userSettings.StreamUrl))
            {
                _videoPlayer.Play(_userSettings.StreamUrl);

                if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel)
                {
                    var channelName = _userSettings.GetCurrentTwitchChannel();
                    _twitchService.FetchAndCacheQualitiesAsync(channelName);
                }
            }
        }

        public void RegisterDisplays(WindowVideoDisplay windowDisplay, WorldVideoDisplay worldDisplay)
        {
            _windowDisplay = windowDisplay;
            _worldDisplay = worldDisplay;

            SubscribeToDisplayEvents(_windowDisplay);
            SubscribeToDisplayEvents(_worldDisplay);

            if (_windowDisplay != null)
            {
                _windowDisplay.PositionChanged += OnWindowPositionChanged;
                _windowDisplay.SizeChanged += OnWindowSizeChanged;
            }

            if (_worldDisplay != null)
            {
                _worldDisplay.InRangeChanged += OnWorldDisplayInRangeChanged;
            }

            UpdateTwitchStreamState();
            UpdateDisplayVisibility();
        }

        private void SubscribeToDisplayEvents(IVideoDisplay display)
        {
            if (display == null) return;

            display.PlayPauseClicked += OnPlayPauseClicked;
            display.VolumeChanged += OnVolumeChangedFromUI;
            display.SettingsClicked += OnDisplaySettingsClicked;
            display.QualityChanged += OnQualityChanged;
            display.TwitchChatClicked += OnTwitchChatClicked;
            display.CloseClicked += OnCloseClicked;
        }


        public void Update()
        {
            if (!_moduleSettings.IsEnabled || _videoPlayer == null)
                return;

            _videoPlayer.Update();
            
            SyncDisplayState(_windowDisplay);
            SyncDisplayState(_worldDisplay);

            GetActiveDisplay()?.UpdateTexture(_videoPlayer.VideoTexture);
        }

        private void SyncDisplayState(IVideoDisplay display)
        {
            if (display == null || _videoPlayer == null) return;

            display.IsPaused = _videoPlayer.IsPaused;
            display.Volume = _videoPlayer.Volume;
        }

        private IVideoDisplay GetActiveDisplay()
        {
            return _userSettings.DisplayMode == CinemaDisplayMode.OnScreen
                ? (IVideoDisplay)_windowDisplay
                : (IVideoDisplay)_worldDisplay;
        }

        public void SelectSavedLocation(string id)
        {
            _userSettings.SelectedPresetLocationId = "";
            _userSettings.SelectedSavedLocationId = id;

            var location = _userSettings.SavedLocations.Locations.Find(l => l.Id == id);
            if (location?.Position != null)
            {
                _userSettings.WorldPosition = location.Position;
                _userSettings.WorldScreenWidth = location.ScreenWidth;
            }
            else
            {
                _userSettings.WorldPosition = null;
                Logger.Warn($"Selected location '{id}' is invalid or not found");
            }
        }

        public void SelectPresetLocation(string presetId, WorldPosition3D position, float screenWidth)
        {
            _userSettings.SelectedSavedLocationId = "";
            _userSettings.SelectedPresetLocationId = presetId;

            if (position != null)
            {
                _userSettings.WorldPosition = position;
                _userSettings.WorldScreenWidth = screenWidth > 0 ? screenWidth : DefaultWorldScreenWidth;
                Logger.Info($"Selected preset location '{presetId}': X={position.X:F2}, Y={position.Y:F2}, Z={position.Z:F2}, Yaw={position.Yaw:F1}, Pitch={position.Pitch:F1}, MapId={position.MapId}, ScreenWidth={_userSettings.WorldScreenWidth:F1}");
            }
        }

        public void SelectSavedStream(string id)
        {
            _userSettings.SelectedSavedStreamId = id;

            var stream = _userSettings.SavedStreams.Streams.Find(s => s.Id == id);
            if (stream != null)
            {
                _userSettings.CurrentStreamSourceType = stream.SourceType;
                _userSettings.CurrentTwitchChannel = stream.SourceType == StreamSourceType.TwitchChannel 
                    ? stream.Value 
                    : "";
            }
            else
            {
                _userSettings.CurrentStreamSourceType = StreamSourceType.Url;
                _userSettings.CurrentTwitchChannel = "";
            }
        }


        #endregion

        #region Private Methods

        private void SubscribeToSettingsEvents()
        {
            _moduleSettings.EnabledSetting.SettingChanged += OnEnabledChanged;
            _userSettings.StreamUrlChanged += OnStreamUrlChanged;
            _userSettings.DisplayModeChanged += OnDisplayModeChanged;
            _userSettings.WorldPositionChanged += OnWorldPositionChanged;
            _userSettings.WorldScreenWidthChanged += OnWorldScreenWidthChanged;
            _userSettings.VolumeChanged += OnVolumeChanged;
            _userSettings.CurrentStreamSourceTypeChanged += OnCurrentStreamSourceTypeChanged;
        }

        private void UnsubscribeFromSettingsEvents()
        {
            _moduleSettings.EnabledSetting.SettingChanged -= OnEnabledChanged;
            _userSettings.StreamUrlChanged -= OnStreamUrlChanged;
            _userSettings.DisplayModeChanged -= OnDisplayModeChanged;
            _userSettings.WorldPositionChanged -= OnWorldPositionChanged;
            _userSettings.WorldScreenWidthChanged -= OnWorldScreenWidthChanged;
            _userSettings.VolumeChanged -= OnVolumeChanged;
            _userSettings.CurrentStreamSourceTypeChanged -= OnCurrentStreamSourceTypeChanged;
        }

        private void UnsubscribeFromDisplayEvents()
        {
            UnsubscribeFromCommonDisplayEvents(_windowDisplay);
            UnsubscribeFromCommonDisplayEvents(_worldDisplay);

            if (_windowDisplay != null)
            {
                _windowDisplay.PositionChanged -= OnWindowPositionChanged;
                _windowDisplay.SizeChanged -= OnWindowSizeChanged;
            }

            if (_worldDisplay != null)
            {
                _worldDisplay.InRangeChanged -= OnWorldDisplayInRangeChanged;
            }
        }

        private void UnsubscribeFromCommonDisplayEvents(IVideoDisplay display)
        {
            if (display == null) return;

            display.PlayPauseClicked -= OnPlayPauseClicked;
            display.VolumeChanged -= OnVolumeChangedFromUI;
            display.SettingsClicked -= OnDisplaySettingsClicked;
            display.QualityChanged -= OnQualityChanged;
            display.TwitchChatClicked -= OnTwitchChatClicked;
            display.CloseClicked -= OnCloseClicked;
        }

        private async void OnEnabledChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            if (_videoPlayer == null)
                return;

            if (e.NewValue)
            {
                // For Twitch streams, refresh the token before playing
                if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel)
                {
                    await RefreshTwitchStreamAndPlayAsync();
                }
                else if (!string.IsNullOrEmpty(_userSettings.StreamUrl))
                {
                    _videoPlayer.Play(_userSettings.StreamUrl);
                }
                UpdateDisplayVisibility();
            }
            else
            {
                _videoPlayer.Stop();
                HideAllDisplays();
            }
        }

        private void OnStreamUrlChanged(object sender, string url)
        {
            if (_videoPlayer == null)
                return;

            _twitchService.ClearCachedQualities();

            if (_moduleSettings.IsEnabled && !string.IsNullOrEmpty(url))
            {
                if (_videoPlayer.CurrentUrl == url)
                    return;

                _videoPlayer.Stop();
                _videoPlayer.Play(url);

                if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel)
                {
                    var channelName = _userSettings.GetCurrentTwitchChannel();
                    _twitchService.FetchAndCacheQualitiesAsync(channelName);
                }
            }
        }

        private void OnDisplayModeChanged(object sender, CinemaDisplayMode mode)
        {
            UpdateRangeBasedPlayback();
            UpdateDisplayVisibility();
            
            if (_moduleSettings.IsEnabled && 
                !string.IsNullOrEmpty(_userSettings.StreamUrl) && 
                _videoPlayer != null && 
                !_videoPlayer.IsPlaying)
            {
                Logger.Info("Restarting playback after display mode change");
                _videoPlayer.Play(_userSettings.StreamUrl);
            }
        }

        private void OnWorldPositionChanged(object sender, WorldPosition3D position)
        {
            if (_worldDisplay != null)
            {
                _worldDisplay.WorldPosition = position;
            }
            UpdateRangeBasedPlayback();
        }

        private void OnWorldScreenWidthChanged(object sender, float width)
        {
            if (_worldDisplay != null)
            {
                _worldDisplay.WorldWidth = width;
            }
        }

        private void OnVolumeChanged(object sender, int volume)
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Volume = volume;
            }
        }

        private void UpdateTwitchStreamState()
        {
            bool isTwitchStream = _userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel;
            
            if (_windowDisplay != null)
            {
                _windowDisplay.IsTwitchStream = isTwitchStream;
            }

            if (_worldDisplay != null)
            {
                _worldDisplay.IsTwitchStream = isTwitchStream;
            }
        }

        private void OnCurrentStreamSourceTypeChanged(object sender, StreamSourceType sourceType)
        {
            UpdateTwitchStreamState();

            if (sourceType == StreamSourceType.TwitchChannel && _videoPlayer?.IsPlaying == true)
            {
                var channelName = _userSettings.GetCurrentTwitchChannel();
                _twitchService.FetchAndCacheQualitiesAsync(channelName);
            }
            else if (sourceType != StreamSourceType.TwitchChannel)
            {
                _twitchService.ClearCachedQualities();
            }
        }

        private void OnVideoQualitiesChanged(object sender, EventArgs e)
        {
            if (_videoPlayer == null)
                return;

            // Twitch streams, quality is handled by TwitchService.QualitiesChanged
            if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel && _twitchService.CachedQualities.Count > 0)
                return;

            var qualityNames = _videoPlayer.AvailableQualities.Select(q => q.Name).ToList();
            
            if (_windowDisplay != null)
            {
                _windowDisplay.UpdateAvailableQualities(qualityNames, _videoPlayer.SelectedQualityIndex);
            }

            if (_worldDisplay != null)
            {
                _worldDisplay.UpdateAvailableQualities(qualityNames, _videoPlayer.SelectedQualityIndex);
            }
        }

        private void OnTwitchQualitiesChanged(object sender, TwitchQualitiesEventArgs e)
        {
            if (_windowDisplay != null)
            {
                _windowDisplay.UpdateAvailableQualities(e.QualityNames.ToList(), e.SelectedIndex);
            }

            if (_worldDisplay != null)
            {
                _worldDisplay.UpdateAvailableQualities(e.QualityNames.ToList(), e.SelectedIndex);
            }
        }

        private void OnWindowPositionChanged(object sender, Microsoft.Xna.Framework.Point position)
        {
            _userSettings.WindowPosition = position;
        }

        private void OnWindowSizeChanged(object sender, Microsoft.Xna.Framework.Point size)
        {
            _userSettings.WindowSize = size;
        }

        private void OnPlayPauseClicked(object sender, EventArgs e)
        {
            _videoPlayer?.TogglePause();
        }

        private void OnVolumeChangedFromUI(object sender, int volume)
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.Volume = volume;
            }
            _userSettings.Volume = volume;
        }

        private void OnDisplaySettingsClicked(object sender, EventArgs e)
        {
            ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnCloseClicked(object sender, EventArgs e)
        {
            _moduleSettings.EnabledSetting.Value = false;
        }

        private void OnTwitchChatClicked(object sender, EventArgs e)
        {
            var channelName = _userSettings.GetCurrentTwitchChannel();
            if (string.IsNullOrEmpty(channelName))
            {
                Logger.Warn("Cannot open Twitch chat - no valid Twitch channel detected");
                return;
            }

            _twitchService.OpenTwitchChat(channelName);
        }

        private void OnQualityChanged(object sender, int qualityIndex)
        {
            if (_videoPlayer == null)
                return;

            if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel && _twitchService.CachedQualities.Count > 0)
            {
                var streamUrl = _twitchService.SelectQuality(qualityIndex);
                if (!string.IsNullOrEmpty(streamUrl))
                {
                    _videoPlayer.Stop();
                    _videoPlayer.Play(streamUrl);
                }
                return;
            }

            _videoPlayer.SetQuality(qualityIndex);
            Logger.Info($"Video quality changed to index {qualityIndex}");
        }

        private void OnWorldDisplayInRangeChanged(object sender, bool isInRange)
        {
            UpdateRangeBasedPlayback();
        }

        private void UpdateRangeBasedPlayback()
        {
            if (_videoPlayer == null)
                return;

            bool shouldBePaused = _userSettings.DisplayMode == CinemaDisplayMode.InGame 
                                  && _worldDisplay != null 
                                  && !_worldDisplay.IsInRange;

            Logger.Debug($"UpdateRangeBasedPlayback - Display mode: {_userSettings.DisplayMode}, IsInRange: {_worldDisplay?.IsInRange ?? false}, Should pause: {shouldBePaused}, Currently paused due to range: {_isPausedDueToRange}");

            if (shouldBePaused && !_isPausedDueToRange && !_videoPlayer.IsPaused)
            {
                _videoPlayer.Pause();
                _isPausedDueToRange = true;
                Logger.Info("World display out of range - paused playback");
            }
            else if (!shouldBePaused && _isPausedDueToRange)
            {
                _videoPlayer.Resume();
                _isPausedDueToRange = false;
                Logger.Info("World display in range or mode changed - resumed playback");
            }
        }

        private void UpdateDisplayVisibility()
        {
            bool isOnScreen = _userSettings.DisplayMode == CinemaDisplayMode.OnScreen;
            bool isEnabled = _moduleSettings.IsEnabled;

            if (_windowDisplay != null)
            {
                _windowDisplay.Visible = isEnabled && isOnScreen;
            }

            if (_worldDisplay != null)
            {
                _worldDisplay.Visible = isEnabled && !isOnScreen;
            }
        }

        private void HideAllDisplays()
        {
            if (_windowDisplay != null)
                _windowDisplay.Visible = false;

            if (_worldDisplay != null)
                _worldDisplay.Visible = false;
        }

        private async Task RefreshTwitchStreamAndPlayAsync()
        {
            var channelName = _userSettings.GetCurrentTwitchChannel();
            if (string.IsNullOrEmpty(channelName))
            {
                Logger.Warn("Cannot refresh Twitch stream - no channel name");
                return;
            }

            Logger.Info($"Refreshing Twitch stream URL for channel: {channelName}");

            try
            {
                var freshUrl = await _twitchService.GetPlayableStreamUrlAsync(channelName);
                if (string.IsNullOrEmpty(freshUrl))
                {
                    Logger.Warn($"Failed to get fresh stream URL for channel: {channelName}");
                    return;
                }

                _userSettings.StreamUrl = freshUrl;

                _videoPlayer.Play(freshUrl);

                _twitchService.FetchAndCacheQualitiesAsync(channelName);

                Logger.Info($"Successfully refreshed Twitch stream for channel: {channelName}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to refresh Twitch stream for channel: {channelName}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            UnsubscribeFromSettingsEvents();
            UnsubscribeFromDisplayEvents();
            
            if (_videoPlayer != null)
            {
                _videoPlayer.QualitiesChanged -= OnVideoQualitiesChanged;
            }

            _twitchService.QualitiesChanged -= OnTwitchQualitiesChanged;
            _isPausedDueToRange = false;

            _isDisposed = true;
        }

        #endregion
       
    }
}
