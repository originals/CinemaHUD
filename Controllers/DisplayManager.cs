using System;
using System.Collections.Generic;
using Blish_HUD;
using CinemaModule.Models;
using CinemaModule.VideoPlayer;
using VideoPlayerClass = CinemaModule.VideoPlayer.VideoPlayer;
using CinemaModule.Settings;
using CinemaModule.UI.VideoDisplays;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.Controllers
{
    public class DisplayManager : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<DisplayManager>();

        private readonly CinemaSettings _moduleSettings;
        private readonly CinemaUserSettings _userSettings;

        private WindowVideoDisplay _windowDisplay;
        private WorldVideoDisplay _worldDisplay;
        private VideoPlayerClass _videoPlayer;
        private bool _isDisposed;

        public event EventHandler<Point> WindowPositionChanged;
        public event EventHandler<Point> WindowSizeChanged;
        public event EventHandler<bool> WorldDisplayInRangeChanged;
        public event EventHandler<bool> WindowLockToggled;

        public event EventHandler PlayPauseClicked;
        public event EventHandler<int> VolumeChangedFromUI;
        public event EventHandler SettingsClicked;
        public event EventHandler<int> QualityChanged;
        public event EventHandler TwitchChatClicked;
        public event EventHandler CloseClicked;
        public event EventHandler<float> SeekRequested;

        public bool IsWorldDisplayInRange => _worldDisplay?.IsInRange ?? false;

        public DisplayManager(CinemaSettings moduleSettings, CinemaUserSettings userSettings)
        {
            _moduleSettings = moduleSettings;
            _userSettings = userSettings;
        }

        public void RegisterPlayer(VideoPlayerClass player)
        {
            _videoPlayer = player;
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
                _windowDisplay.LockToggled += OnWindowLockToggled;
            }

            if (_worldDisplay != null)
            {
                _worldDisplay.InRangeChanged += OnWorldDisplayInRangeChanged;
            }
        }

        public void SyncDisplayState()
        {
            SyncDisplayState(_windowDisplay);
            SyncDisplayState(_worldDisplay);
        }

        public void UpdateActiveDisplayTexture()
        {
            var activeDisplay = GetActiveDisplay();
            if (activeDisplay == null)
                return;

            var preset = _userSettings.CurrentStreamPreset;
            if (preset?.IsRadio == true && preset.StaticImageTexture != null)
            {
                activeDisplay.UpdateTexture(preset.StaticImageTexture);
                return;
            }

            if (_videoPlayer != null)
                activeDisplay.UpdateTexture(_videoPlayer.VideoTexture);
        }

        public void UpdateDisplayVisibility()
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

        public void HideAllDisplays()
        {
            if (_windowDisplay != null)
                _windowDisplay.Visible = false;

            if (_worldDisplay != null)
                _worldDisplay.Visible = false;
        }

        public void UpdateTwitchStreamState(bool isTwitchStream)
        {
            ForEachDisplay(d => d.IsTwitchStream = isTwitchStream);
        }

        public void UpdateAvailableQualities(IReadOnlyList<string> qualityNames, int selectedIndex)
        {
            ForEachDisplay(d => d.UpdateAvailableQualities(qualityNames, selectedIndex));
        }

        public void UpdateWorldPosition(WorldPosition3D position)
        {
            if (_worldDisplay != null)
            {
                _worldDisplay.WorldPosition = position;
            }
        }

        public void UpdateWorldScreenWidth(float width)
        {
            if (_worldDisplay != null)
            {
                _worldDisplay.WorldWidth = width;
            }
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
            display.SeekRequested += OnSeekRequested;
        }

        private void UnsubscribeFromDisplayEvents()
        {
            UnsubscribeFromCommonDisplayEvents(_windowDisplay);
            UnsubscribeFromCommonDisplayEvents(_worldDisplay);

            if (_windowDisplay != null)
            {
                _windowDisplay.PositionChanged -= OnWindowPositionChanged;
                _windowDisplay.SizeChanged -= OnWindowSizeChanged;
                _windowDisplay.LockToggled -= OnWindowLockToggled;
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
            display.SeekRequested -= OnSeekRequested;
        }

        private void SyncDisplayState(IVideoDisplay display)
        {
            if (display == null || _videoPlayer == null) 
                return;

            display.IsPaused = _videoPlayer.IsPaused;
            display.Volume = _videoPlayer.Volume;
        }

        private IVideoDisplay GetActiveDisplay()
        {
            return _userSettings.DisplayMode == CinemaDisplayMode.OnScreen
                ? (IVideoDisplay)_windowDisplay
                : _worldDisplay;
        }

        private void OnWindowPositionChanged(object sender, Point position)
        {
            WindowPositionChanged?.Invoke(this, position);
        }

        private void OnWindowSizeChanged(object sender, Point size)
        {
            WindowSizeChanged?.Invoke(this, size);
        }

        private void OnWindowLockToggled(object sender, bool isLocked)
        {
            WindowLockToggled?.Invoke(this, isLocked);
        }

        private void OnWorldDisplayInRangeChanged(object sender, bool isInRange)
        {
            WorldDisplayInRangeChanged?.Invoke(this, isInRange);
        }

        private void OnPlayPauseClicked(object sender, EventArgs e)
        {
            PlayPauseClicked?.Invoke(this, e);
        }

        private void OnVolumeChangedFromUI(object sender, int volume)
        {
            VolumeChangedFromUI?.Invoke(this, volume);
        }

        private void OnDisplaySettingsClicked(object sender, EventArgs e)
        {
            SettingsClicked?.Invoke(this, e);
        }

        private void OnQualityChanged(object sender, int qualityIndex)
        {
            QualityChanged?.Invoke(this, qualityIndex);
        }

        private void OnTwitchChatClicked(object sender, EventArgs e)
        {
            TwitchChatClicked?.Invoke(this, e);
        }

        private void OnCloseClicked(object sender, EventArgs e)
        {
            CloseClicked?.Invoke(this, e);
        }

        private void OnSeekRequested(object sender, float position)
        {
            SeekRequested?.Invoke(this, position);
        }

        private void ForEachDisplay(Action<IVideoDisplay> action)
        {
            if (_windowDisplay != null)
                action(_windowDisplay);

            if (_worldDisplay != null)
                action(_worldDisplay);
        }

        public void UpdateSeekableState(bool isSeekable, long duration)
        {
            ForEachDisplay(d =>
            {
                d.IsSeekable = isSeekable;
                d.Duration = duration;
            });
        }

        public void UpdateCurrentPosition(float position)
        {
            ForEachDisplay(d => d.CurrentPosition = position);
        }

        public void UpdateBufferingState(bool isBuffering)
        {
            ForEachDisplay(d => d.IsBuffering = isBuffering);
        }

        public void UpdateStreamInfo(string title, int? viewerCount, string gameName)
        {
            if (_windowDisplay == null)
                return;

            _windowDisplay.StreamTitle = title;
            _windowDisplay.ViewerCount = viewerCount;
            _windowDisplay.GameName = gameName;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            UnsubscribeFromDisplayEvents();
            _isDisposed = true;
        }
    }
}
