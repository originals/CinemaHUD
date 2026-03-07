using System;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Settings;
using CinemaModule.Controllers.WatchParty;
using CinemaModule.Models;
using CinemaModule.Models.Location;
using CinemaModule.Models.Twitch;
using CinemaModule.VideoPlayer;
using VideoPlayerClass = CinemaModule.VideoPlayer.VideoPlayer;
using CinemaModule.Services;
using CinemaModule.Services.Twitch;
using CinemaModule.Services.YouTube;
using CinemaModule.Settings;
using CinemaModule.UI.VideoDisplays;

namespace CinemaModule.Controllers
{
    public class CinemaController : IDisposable
    {
        #region Members

        private static readonly Logger Logger = Logger.GetLogger<CinemaController>();
        private const float DefaultWorldScreenWidth = 10f;

        private readonly CinemaSettings _moduleSettings;
        private readonly CinemaUserSettings _userSettings;
        private readonly TwitchService _twitchService;
        private readonly YouTubeService _youtubeService;
        private readonly RadioMetadataService _radioMetadataService;

        private readonly PlaybackController _playbackController;
        private readonly DisplayController _displayController;
        private readonly TwitchIntegrationHandler _twitchHandler;

        private WatchPartyController _watchPartyController;
        private WatchPartyPlaybackHandler _watchPartyPlaybackHandler;
        private bool _isDisposed;

        #endregion

        #region Events

        public event EventHandler ShowSettingsRequested;
        public event EventHandler<string> ShowChatRequested;
        public event EventHandler<string> ToggleChatRequested;
        public event EventHandler<string> ChatChannelChangeRequested;

        #endregion

        public CinemaController(CinemaSettings coreSettings, CinemaUserSettings userSettings, TwitchService twitchService, YouTubeService youtubeService)
        {
            _moduleSettings = coreSettings;
            _userSettings = userSettings;
            _twitchService = twitchService;
            _youtubeService = youtubeService;
            _radioMetadataService = new RadioMetadataService();

            _playbackController = new PlaybackController(coreSettings, userSettings, twitchService, youtubeService);
            _displayController = new DisplayController(coreSettings, userSettings);
            _twitchHandler = new TwitchIntegrationHandler(userSettings, twitchService, youtubeService);

            _radioMetadataService.TrackInfoUpdated += OnRadioTrackInfoUpdated;

            SubscribeToHandlerEvents();
            SubscribeToSettingsEvents();
        }

        #region Public Methods

        public void RegisterPlayer(VideoPlayerClass player)
        {
            _playbackController.RegisterPlayer(player);
            _displayController.RegisterPlayer(player);
            _twitchHandler.RegisterPlayer(player);
        }

        public void RegisterWatchParty(WatchPartyController watchPartyController)
        {
            _watchPartyController = watchPartyController;
            _watchPartyPlaybackHandler = new WatchPartyPlaybackHandler(
                watchPartyController,
                _playbackController,
                _displayController,
                _youtubeService,
                _userSettings);

            _twitchHandler.SetWatchPartyCheck(() => _watchPartyController?.IsInRoom == true);
            _twitchHandler.QualityChangeCompleted += OnQualityChangeCompleted;
        }

        public void StartInitialPlaybackIfEnabled()
        {
            _playbackController.StartInitialPlaybackIfEnabled();
        }

        public void RegisterDisplays(WindowVideoDisplay windowDisplay, WorldVideoDisplay worldDisplay)
        {
            _displayController.RegisterDisplays(windowDisplay, worldDisplay);
            _displayController.UpdateTwitchStreamState(IsTwitchStream);
            _displayController.UpdateDisplayVisibility();
            _twitchHandler.InitializeStreamInfo();
            UpdateRadioMetadataPolling();
        }

        public void Update()
        {
            if (!_moduleSettings.IsEnabled)
                return;

            _playbackController.Update();
            _watchPartyPlaybackHandler?.Update();
            _displayController.SyncDisplayState();
            _displayController.UpdateActiveDisplayTexture();
            UpdateSeekState();
        }

        private void UpdateSeekState()
        {
            bool isSeekable = !IsTwitchStream && _playbackController.IsSeekable;

            _displayController.UpdateSeekableState(isSeekable, _playbackController.Duration);
            _displayController.UpdateCurrentPosition(_playbackController.Position);
        }

        private bool IsTwitchStream => _userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel;

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
            }
        }

        public void SelectSavedStream(string id)
        {
            var stream = _userSettings.SavedStreams.Streams.Find(s => s.Id == id);
            if (stream != null)
            {
                _userSettings.SelectSavedStream(stream);
            }
            else
            {
                Logger.Warn($"Saved stream '{id}' not found");
            }
        }

        public void PrepareForStreamChange()
        {
            _playbackController.Stop();
            _displayController.UpdateOfflineState(true);
            _ = LoadOfflineTextureAsync();
        }

        public void RequestShowChat(string channelName)
        {
            if (string.IsNullOrEmpty(channelName))
            {
                Logger.Warn("Cannot open Twitch chat - channel name is empty");
                return;
            }

            ShowChatRequested?.Invoke(this, channelName);
        }

        public void ForceWatchPartyResync()
        {
            _watchPartyPlaybackHandler?.ForceResync();
        }

        public void ApplyLocation(SavedLocation location)
        {
            if (location?.Position == null)
                return;

            _userSettings.WorldPosition = location.Position;
            _userSettings.WorldScreenWidth = location.ScreenWidth > 0 ? location.ScreenWidth : DefaultWorldScreenWidth;
            Logger.Info($"Applied shared location: {location.Name}");
        }

        #endregion

        #region Private Methods

        private void SubscribeToHandlerEvents()
        {
            _displayController.WindowPositionChanged += (s, pos) => _userSettings.WindowPosition = pos;
            _displayController.WindowSizeChanged += (s, size) => _userSettings.WindowSize = size;
            _displayController.WindowLockToggled += (s, locked) => _userSettings.WindowLocked = locked;
            _displayController.WorldDisplayInRangeChanged += OnWorldDisplayInRangeChanged;
            _displayController.PlayPauseClicked += (s, e) => _playbackController.TogglePause();
            _displayController.VolumeChangedFromUI += OnVolumeChangedFromUI;
            _displayController.SettingsClicked += (s, e) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
            _displayController.QualityChanged += (s, index) => _twitchHandler.HandleQualityChange(index);
            _displayController.TwitchChatClicked += OnTwitchChatClicked;
            _displayController.CloseClicked += (s, e) => _moduleSettings.EnabledSetting.Value = false;
            _displayController.SeekRequested += OnSeekRequested;

            _twitchHandler.ChatChannelChangeRequested += (s, channel) => ChatChannelChangeRequested?.Invoke(this, channel);
            _twitchHandler.QualitiesUpdated += OnQualitiesUpdated;
            _twitchHandler.StreamInfoUpdated += OnStreamInfoUpdated;

            _playbackController.StreamUrlRefreshed += OnStreamUrlRefreshed;
            _playbackController.PlaybackStateChanged += OnPlaybackStateChanged;
        }

        private void SubscribeToSettingsEvents()
        {
            _moduleSettings.EnabledSetting.SettingChanged += OnEnabledChanged;
            _userSettings.StreamUrlChanged += OnStreamUrlChanged;
            _userSettings.DisplayModeChanged += OnDisplayModeChanged;
            _userSettings.WorldPositionChanged += OnWorldPositionChanged;
            _userSettings.WorldScreenWidthChanged += OnWorldScreenWidthChanged;
            _userSettings.VolumeChanged += OnVolumeChanged;
            _userSettings.CurrentStreamSourceTypeChanged += OnCurrentStreamSourceTypeChanged;
            _userSettings.CurrentStreamPresetChanged += OnCurrentStreamPresetChanged;
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
            _userSettings.CurrentStreamPresetChanged -= OnCurrentStreamPresetChanged;
        }

        private void OnEnabledChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            _playbackController.HandleEnabledChanged(e.NewValue);

            if (e.NewValue)
            {
                _displayController.UpdateDisplayVisibility();
                UpdateRadioMetadataPolling();
            }
            else
            {
                _displayController.HideAllDisplays();
                _radioMetadataService.StopPolling();
            }
        }

        private void OnStreamUrlChanged(object sender, string url)
        {
            CinemaModule.Instance.TextureService.ClearCachedStreamInfo();

            if (string.IsNullOrEmpty(url))
            {
                _playbackController.HandleStreamUrlChanged(url);
                _displayController.UpdateOfflineState(true);
                _ = LoadOfflineTextureAsync();
            }
            else
            {
                _displayController.UpdateOfflineState(false);
                _displayController.UpdateOfflineTexture(null);
                _playbackController.HandleStreamUrlChanged(url);
            }

            _twitchHandler.HandleStreamUrlChanged(IsTwitchStream);
            UpdateRadioMetadataPolling();
        }

        private void OnDisplayModeChanged(object sender, CinemaDisplayMode mode)
        {
            UpdateRangeBasedPlayback();
            _displayController.UpdateDisplayVisibility();
            _playbackController.RestartPlaybackIfNeeded();
        }

        private void OnWorldPositionChanged(object sender, WorldPosition3D position)
        {
            _displayController.UpdateWorldPosition(position);
            UpdateRangeBasedPlayback();
        }

        private void OnWorldScreenWidthChanged(object sender, float width)
        {
            _displayController.UpdateWorldScreenWidth(width);
        }

        private void OnVolumeChanged(object sender, int volume)
        {
            _playbackController.SetVolume(volume);
        }

        private void OnCurrentStreamSourceTypeChanged(object sender, StreamSourceType sourceType)
        {
            _displayController.UpdateTwitchStreamState(IsTwitchStream);
            _twitchHandler.HandleStreamSourceTypeChanged(sourceType);
            UpdateRadioMetadataPolling();
        }

        private void OnCurrentStreamPresetChanged(object sender, StreamPresetData preset)
        {
            UpdateRadioMetadataPolling();

            if (_playbackController.IsOffline)
            {
                _ = LoadOfflineTextureAsync();
            }
        }

        private void OnWorldDisplayInRangeChanged(object sender, bool isInRange)
        {
            UpdateRangeBasedPlayback();
        }

        private void OnVolumeChangedFromUI(object sender, int volume)
        {
            _playbackController.SetVolume(volume);
            _userSettings.Volume = volume;
        }

        private void OnTwitchChatClicked(object sender, EventArgs e)
        {
            var channelName = _twitchHandler.GetCurrentTwitchChannel();
            if (string.IsNullOrEmpty(channelName))
            {
                Logger.Warn("Cannot open Twitch chat - no valid Twitch channel detected");
                return;
            }

            ToggleChatRequested?.Invoke(this, channelName);
        }

        private void OnQualitiesUpdated(object sender, TwitchQualitiesEventArgs e)
        {
            _displayController.UpdateAvailableQualities(e.QualityNames.ToList(), e.SelectedIndex);
        }

        private void OnStreamInfoUpdated(object sender, TwitchStreamInfo streamInfo)
        {
            CinemaModule.Instance.TextureService.UpdateCachedStreamInfo(streamInfo);

            if (streamInfo != null)
                _displayController.UpdateStreamInfo(streamInfo.ChannelName, streamInfo.ViewerCount, streamInfo.GameName);
            else
                _displayController.UpdateStreamInfo(null, null, null);
        }

        private void OnPlaybackStateChanged(object sender, PlaybackState state)
        {
            bool isOffline = state == PlaybackState.Stopped || state == PlaybackState.Error || state == PlaybackState.Ended;
            _displayController.UpdateOfflineState(isOffline);

            if (isOffline)
                _ = LoadOfflineTextureAsync();
            else if (state == PlaybackState.Playing)
                _displayController.UpdateOfflineTexture(null);
        }

        private async Task LoadOfflineTextureAsync()
        {
            var offlineTexture = await CinemaModule.Instance.TextureService.LoadOfflineTextureAsync(_userSettings, _twitchService);
            if (offlineTexture != null)
                _displayController.UpdateOfflineTexture(offlineTexture);
        }

        private void OnQualityChangeCompleted(object sender, EventArgs e)
        {
            _watchPartyPlaybackHandler?.HandleQualityChanged();
        }

        private void OnSeekRequested(object sender, float position)
        {
            _playbackController.Seek(position);
        }

        private void OnStreamUrlRefreshed(object sender, TwitchStreamRefreshedEventArgs e)
        {
            _userSettings.StreamUrl = e.StreamUrl;
            ChatChannelChangeRequested?.Invoke(this, e.ChannelName);
        }

        private void UpdateRangeBasedPlayback()
        {
            _playbackController.UpdateRangeBasedPlayback(
                _displayController.IsWorldDisplayInRange,
                _userSettings.DisplayMode);
        }

        private void OnRadioTrackInfoUpdated(object sender, RadioTrackInfo trackInfo)
        {
            string trackName = trackInfo?.TrackName;
            _displayController.UpdateRadioTrackInfo(trackName);
        }

        private void UpdateRadioMetadataPolling()
        {
            var preset = _userSettings.CurrentStreamPreset;
            bool shouldPoll = preset?.IsRadio == true && preset?.AsylumInfo == true && !IsTwitchStream;

            if (!shouldPoll)
            {
                _radioMetadataService.StopPolling();
                _displayController.UpdateRadioTrackInfo(null);
                return;
            }

            _radioMetadataService.StartPolling(_userSettings.StreamUrl, preset.InfoUrl);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            UnsubscribeFromSettingsEvents();

            _radioMetadataService.TrackInfoUpdated -= OnRadioTrackInfoUpdated;
            _radioMetadataService.Dispose();
            _playbackController?.Dispose();
            _displayController?.Dispose();
            _twitchHandler?.Dispose();
            _watchPartyPlaybackHandler?.Dispose();

            _isDisposed = true;
        }

        #endregion
    }
}
