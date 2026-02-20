using System;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Settings;
using CinemaModule.Controllers;
using CinemaModule.Models;
using CinemaModule.VideoPlayer;
using VideoPlayerClass = CinemaModule.VideoPlayer.VideoPlayer;
using CinemaModule.Services;
using CinemaModule.Settings;
using CinemaModule.UI.VideoDisplays;
using Microsoft.Xna.Framework.Graphics;

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

        private readonly PlaybackController _playbackController;
        private readonly DisplayManager _displayManager;
        private readonly TwitchIntegrationHandler _twitchHandler;

        private TwitchStreamInfo _currentTwitchStreamInfo;
        private bool _isDisposed;

        #endregion

        #region Events

        public event EventHandler ShowSettingsRequested;
        public event EventHandler<string> ShowChatRequested;
        public event EventHandler<string> ToggleChatRequested;
        public event EventHandler<string> ChatChannelChangeRequested;

        #endregion

        public CinemaController(CinemaSettings coreSettings, CinemaUserSettings userSettings, TwitchService twitchService)
        {
            _moduleSettings = coreSettings;
            _userSettings = userSettings;
            _twitchService = twitchService;

            _playbackController = new PlaybackController(coreSettings, userSettings, twitchService);
            _displayManager = new DisplayManager(coreSettings, userSettings);
            _twitchHandler = new TwitchIntegrationHandler(userSettings, twitchService);

            SubscribeToHandlerEvents();
            SubscribeToSettingsEvents();
        }

        #region Public Methods

        public void RegisterPlayer(VideoPlayerClass player)
        {
            _playbackController.RegisterPlayer(player);
            _displayManager.RegisterPlayer(player);
            _twitchHandler.RegisterPlayer(player);
        }

        public void StartInitialPlaybackIfEnabled()
        {
            _playbackController.StartInitialPlaybackIfEnabled();
        }

        public void RegisterDisplays(WindowVideoDisplay windowDisplay, WorldVideoDisplay worldDisplay)
        {
            _displayManager.RegisterDisplays(windowDisplay, worldDisplay);
            _displayManager.UpdateTwitchStreamState(IsTwitchStream);
            _displayManager.UpdateDisplayVisibility();
            _twitchHandler.InitializeStreamInfo();
        }

        public void Update()
        {
            if (!_moduleSettings.IsEnabled)
                return;

            _playbackController.Update();
            _displayManager.SyncDisplayState();
            _displayManager.UpdateActiveDisplayTexture();
            UpdateSeekState();
        }

        private void UpdateSeekState()
        {
            bool isSeekable = !IsTwitchStream && _playbackController.IsSeekable;

            _displayManager.UpdateSeekableState(isSeekable, _playbackController.Duration);
            _displayManager.UpdateCurrentPosition(_playbackController.Position);
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

        public void RequestShowChat(string channelName)
        {
            if (string.IsNullOrEmpty(channelName))
            {
                Logger.Warn("Cannot open Twitch chat - channel name is empty");
                return;
            }

            ShowChatRequested?.Invoke(this, channelName);
        }

        #endregion

        #region Private Methods

        private void SubscribeToHandlerEvents()
        {
            _displayManager.WindowPositionChanged += (s, pos) => _userSettings.WindowPosition = pos;
            _displayManager.WindowSizeChanged += (s, size) => _userSettings.WindowSize = size;
            _displayManager.WindowLockToggled += (s, locked) => _userSettings.WindowLocked = locked;
            _displayManager.WorldDisplayInRangeChanged += OnWorldDisplayInRangeChanged;
            _displayManager.PlayPauseClicked += (s, e) => _playbackController.TogglePause();
            _displayManager.VolumeChangedFromUI += OnVolumeChangedFromUI;
            _displayManager.SettingsClicked += (s, e) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
            _displayManager.QualityChanged += (s, index) => _twitchHandler.HandleQualityChange(index);
            _displayManager.TwitchChatClicked += OnTwitchChatClicked;
            _displayManager.CloseClicked += (s, e) => _moduleSettings.EnabledSetting.Value = false;
            _displayManager.SeekRequested += OnSeekRequested;

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

        private void OnEnabledChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            _playbackController.HandleEnabledChanged(e.NewValue);

            if (e.NewValue)
            {
                _displayManager.UpdateDisplayVisibility();
            }
            else
            {
                _displayManager.HideAllDisplays();
            }
        }

        private void OnStreamUrlChanged(object sender, string url)
        {
            _displayManager.UpdateOfflineState(false);
            _displayManager.UpdateOfflineTexture(null);
            _currentTwitchStreamInfo = null;
            _playbackController.HandleStreamUrlChanged(url);
            _twitchHandler.HandleStreamUrlChanged(IsTwitchStream);
        }

        private void OnDisplayModeChanged(object sender, CinemaDisplayMode mode)
        {
            UpdateRangeBasedPlayback();
            _displayManager.UpdateDisplayVisibility();
            _playbackController.RestartPlaybackIfNeeded();
        }

        private void OnWorldPositionChanged(object sender, WorldPosition3D position)
        {
            _displayManager.UpdateWorldPosition(position);
            UpdateRangeBasedPlayback();
        }

        private void OnWorldScreenWidthChanged(object sender, float width)
        {
            _displayManager.UpdateWorldScreenWidth(width);
        }

        private void OnVolumeChanged(object sender, int volume)
        {
            _playbackController.SetVolume(volume);
        }

        private void OnCurrentStreamSourceTypeChanged(object sender, StreamSourceType sourceType)
        {
            _displayManager.UpdateTwitchStreamState(IsTwitchStream);
            _twitchHandler.HandleStreamSourceTypeChanged(sourceType);
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
            _displayManager.UpdateAvailableQualities(e.QualityNames.ToList(), e.SelectedIndex);
        }

        private void OnStreamInfoUpdated(object sender, TwitchStreamInfo streamInfo)
        {
            _currentTwitchStreamInfo = streamInfo;
            if (streamInfo != null)
            {
                _displayManager.UpdateStreamInfo(streamInfo.ChannelName, streamInfo.ViewerCount, streamInfo.GameName);
            }
            else
            {
                _displayManager.UpdateStreamInfo(null, null, null);
            }
        }

        private void OnPlaybackStateChanged(object sender, PlaybackState state)
        {
            bool isOffline = state == PlaybackState.Stopped || state == PlaybackState.Error || state == PlaybackState.Ended;
            _displayManager.UpdateOfflineState(isOffline);

            if (isOffline)
            {
                _ = LoadOfflineTextureAsync();
            }
            else if (state == PlaybackState.Playing)
            {
                _displayManager.UpdateOfflineTexture(null);
            }
        }

        private async Task LoadOfflineTextureAsync()
        {
            Texture2D offlineTexture = IsTwitchStream
                ? await LoadTwitchAvatarTextureAsync()
                : await LoadUrlStaticImageTextureAsync();

            if (offlineTexture != null)
            {
                _displayManager.UpdateOfflineTexture(offlineTexture);
            }
        }

        private async Task<Texture2D> LoadTwitchAvatarTextureAsync()
        {
            var channelName = _userSettings.CurrentTwitchChannel;
            if (string.IsNullOrEmpty(channelName))
                return null;

            var streamInfo = _currentTwitchStreamInfo ?? await _twitchService.GetStreamInfoAsync(channelName);
            if (streamInfo == null || string.IsNullOrEmpty(streamInfo.AvatarUrl))
                return null;

            var avatarTexture = await _twitchService.GetAvatarTextureAsync($"offline_{channelName}", streamInfo.AvatarUrl);
            return avatarTexture?.Texture;
        }

        private async Task<Texture2D> LoadUrlStaticImageTextureAsync()
        {
            var preset = _userSettings.CurrentStreamPreset;
            if (preset == null)
                return null;

            if (preset.StaticImageTexture?.Texture != null && !preset.StaticImageTexture.Texture.IsDisposed)
                return preset.StaticImageTexture.Texture;

            if (string.IsNullOrEmpty(preset.StaticImage))
                return null;

            var textureService = CinemaModule.Instance.TextureService;
            var asyncTexture = await textureService.GetImageFromUrlAsync($"offline_static_{preset.Id}", preset.StaticImage);
            return asyncTexture?.Texture;
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
                _displayManager.IsWorldDisplayInRange,
                _userSettings.DisplayMode);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            UnsubscribeFromSettingsEvents();

            _playbackController?.Dispose();
            _displayManager?.Dispose();
            _twitchHandler?.Dispose();

            _isDisposed = true;
        }

        #endregion
    }
}
