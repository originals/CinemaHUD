using System;
using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.UI.Controls
{
    public class WindowVideoControls : BaseVideoControls
    {
        #region Constants

        private const int DefaultButtonSize = 48;
        private const int ControlMargin = 30;
        private const int IconSliderSpacing = 12;
        private const float HoverScale = 1.15f;
        private new const int SeekBarHeight = 16;
        private const int TimeDisplayWidth = 75;
        private const int BottomBarMargin = 40;
        private const int SeekBarPadding = 80;
        private const int StreamInfoSpacing = 8;

        #endregion

        #region Fields

        private readonly int _buttonSize;
        private bool _isHoveringPlayPause;
        private bool _isHoveringVolume;
        private bool _isHoveringSettings;
        private bool _isHoveringTwitchChat;
        private bool _isHoveringClose;
        private bool _isHoveringPanel;
        private bool _wasHoveringPanel;
        private bool _isHoveringLock;
        private bool _isLocked;
        private Rectangle _panelBounds;
        private Rectangle _playPauseBounds;
        private Rectangle _volumeIconBounds;
        private Rectangle _volumeControlBounds;
        private Rectangle _settingsBounds;
        private Rectangle _twitchChatBounds;
        private Rectangle _closeBounds;
        private Rectangle _lockBounds;
        private Rectangle _streamInfoBounds;
        private Rectangle _seekBarBackgroundBounds;
        private Rectangle _timeDisplayBounds;
        private int _currentSeekBarWidth;
        private bool _isSeekable;

        #endregion

        #region Events

        public event EventHandler TwitchChatClicked;
        public event EventHandler CloseClicked;
        public event EventHandler<bool> LockToggled;

        #endregion

        #region Properties

        public bool IsTwitchStream { get; set; }

        public bool IsLocked
        {
            get => _isLocked;
            set => _isLocked = value;
        }

        public bool IsVisible => _isHoveringPanel || VolumeTrackBar.Dragging || QualityDropdown.MouseOver || IsSeekBarDragging;

        private bool ShouldDraw => Opacity > 0.01f;

        public string CurrentTooltip { get; private set; }

        public bool IsSeekable
        {
            get => _isSeekable;
            set
            {
                _isSeekable = value;
                UpdateSeekBarVisibility();
            }
        }

        public string StreamTitle { get; set; }

        public int? ViewerCount { get; set; }

        public string GameName { get; set; }

        #endregion

        public WindowVideoControls(Container parent, int buttonSize = DefaultButtonSize)
            : base(parent, TrackBarWidth, TrackBarHeight, QualityDropdownWidth, true)
        {
            _buttonSize = buttonSize;
        }

        private void UpdateSeekBarVisibility()
        {
            SeekBar.Visible = ShouldDraw && _isSeekable && !IsTwitchStream;
        }

        #region Public Methods

        public void Update(Rectangle panelBounds)
        {
            _panelBounds = panelBounds;

            var center = new Vector2(panelBounds.X + panelBounds.Width / 2f, panelBounds.Y + panelBounds.Height / 2f);
            var bottomRight = new Vector2(panelBounds.X + panelBounds.Width, panelBounds.Y + panelBounds.Height);
            var bottomLeft = new Vector2(panelBounds.X, panelBounds.Y + panelBounds.Height);
            var topRight = new Vector2(panelBounds.X + panelBounds.Width, panelBounds.Y);
            var topLeft = new Vector2(panelBounds.X, panelBounds.Y);

            UpdatePlayPauseBounds(center);
            UpdateVolumeBounds(bottomRight, bottomLeft);
            UpdateSeekBarBounds(bottomRight, bottomLeft);
            UpdateSettingsBounds(topRight);
            UpdateLockBounds(topLeft);
            UpdateStreamInfoBounds();
            if (IsTwitchStream)
            {
                UpdateTwitchChatBounds();
            }
            UpdateCloseBounds();
            UpdateTrackBarPosition();
            UpdateSeekBarPosition();
            UpdateSeekBarDragState();
            UpdateQualityDropdownPosition();
            UpdateHoverStates();
            UpdateFadeAnimation();
        }

        private void UpdatePlayPauseBounds(Vector2 center)
        {
            _playPauseBounds = new Rectangle(
                (int)(center.X - _buttonSize / 2f),
                (int)(center.Y - _buttonSize / 2f),
                _buttonSize,
                _buttonSize);
        }

        private void UpdateVolumeBounds(Vector2 bottomRight, Vector2 bottomLeft)
        {
            int bottomY = (int)bottomLeft.Y - BottomBarMargin - IconSize;

            _volumeIconBounds = new Rectangle(
                (int)bottomRight.X - BottomBarMargin - TrackBarWidth - IconSliderSpacing - IconSize,
                bottomY,
                IconSize,
                IconSize);

            _volumeControlBounds = new Rectangle(
                _volumeIconBounds.X - 14,
                _volumeIconBounds.Y - 4,
                IconSize + IconSliderSpacing + TrackBarWidth + 34,
                IconSize + 8);
        }

        private void UpdateTrackBarPosition()
        {
            int trackBarX = _volumeIconBounds.X + IconSize + IconSliderSpacing - _panelBounds.X;
            int trackBarY = _volumeIconBounds.Y + (IconSize - TrackBarHeight) / 2 - _panelBounds.Y;

            VolumeTrackBar.Location = new Point(trackBarX, trackBarY);
            VolumeTrackBar.Visible = ShouldDraw;
            VolumeTrackBar.Opacity = Opacity;
        }

        private void UpdateSeekBarBounds(Vector2 bottomRight, Vector2 bottomLeft)
        {
            if (!_isSeekable || IsTwitchStream) return;

            int bottomY = _volumeIconBounds.Y;

            // 1. Calculate the Anchors
            // Left Anchor: Absolute Screen X where the slider area starts
            int sliderLeft = _panelBounds.X + SeekBarPadding;

            // Right Anchor: Absolute Screen X where the background ends (anchored to volume)
            int backgroundRight = _volumeControlBounds.X - SeekBarPadding;

            // 2. Position the Time Display
            // The time display sits 14px inside the right edge of the background
            int timeRight = backgroundRight - 14;
            _timeDisplayBounds = new Rectangle(
                timeRight - TimeDisplayWidth,
                bottomY,
                TimeDisplayWidth,
                IconSize);

            // 3. Calculate Slider Width
            // The slider fills the space between the Left Anchor and the Time Display (with extra spacing)
            int seekBarToTimeSpacing = IconSliderSpacing + 8;
            _currentSeekBarWidth = _timeDisplayBounds.X - seekBarToTimeSpacing - sliderLeft;

            // 4. Define the Background Bounds
            // It must span from 14px before the Slider starts ... to ... the Right Anchor.
            int bgStartX = sliderLeft - 14;
            int bgWidth = backgroundRight - bgStartX + 10;

            _seekBarBackgroundBounds = new Rectangle(
                bgStartX,
                bottomY - 4,
                bgWidth,
                IconSize + 8);
        }

        private void UpdateSeekBarPosition()
        {
            bool shouldShow = ShouldDraw && _isSeekable && !IsTwitchStream;
            SeekBar.Visible = shouldShow;
            SeekBar.Opacity = Opacity;

            if (!shouldShow) return;

            int seekBarX = _seekBarBackgroundBounds.X + 14 - _panelBounds.X;
            int seekBarY = _seekBarBackgroundBounds.Y + 4 + (IconSize - SeekBarHeight) / 2 - _panelBounds.Y;

            SeekBar.Location = new Point(seekBarX, seekBarY);
            SeekBar.Size = new Point(_currentSeekBarWidth, SeekBarHeight);
        }

        private void UpdateSettingsBounds(Vector2 topRight)
        {
            _settingsBounds = new Rectangle(
                (int)(topRight.X - IconSize - 60),
                (int)(topRight.Y + ControlMargin),
                IconSize,
                IconSize);
        }

        private void UpdateTwitchChatBounds()
        {
            _twitchChatBounds = new Rectangle(
                _settingsBounds.X - IconSize - ControlSpacing,
                _settingsBounds.Y + (_settingsBounds.Height - IconSize) / 2,
                IconSize,
                IconSize);
        }

        private void UpdateQualityDropdownPosition()
        {
            // Position to the left of settings or twitch chat if visible
            int leftmostX = IsTwitchStream && _twitchChatBounds.Width > 0 
                ? _twitchChatBounds.X 
                : _settingsBounds.X;
            
            int dropdownX = leftmostX - QualityDropdownWidth - ControlSpacing - _panelBounds.X;
            int dropdownY = _settingsBounds.Y - _panelBounds.Y;

            QualityDropdown.Location = new Point(dropdownX, dropdownY);
            QualityDropdown.Opacity = Opacity;
            
            bool hasQualities = QualityDropdown.Items.Count > 0;
            QualityDropdown.Visible = ShouldDraw && hasQualities;
        }

        private void UpdateCloseBounds()
        {
            _closeBounds = new Rectangle(
                _settingsBounds.X + _settingsBounds.Width + ControlSpacing,
                _settingsBounds.Y + (_settingsBounds.Height - IconSize) / 2,
                IconSize,
                IconSize);
        }

        private void UpdateLockBounds(Vector2 topLeft)
        {
            _lockBounds = new Rectangle(
                (int)(topLeft.X + ControlMargin),
                (int)(topLeft.Y + ControlMargin),
                IconSize,
                IconSize);
        }

        private void UpdateStreamInfoBounds()
        {
            int maxWidth = _settingsBounds.X - _lockBounds.Right - StreamInfoSpacing * 2 - QualityDropdownWidth - ControlSpacing;
            _streamInfoBounds = new Rectangle(
                _lockBounds.Right + StreamInfoSpacing,
                _lockBounds.Y,
                maxWidth,
                IconSize);
        }

        private void UpdateHoverStates()
        {
            var mousePos = GameService.Input.Mouse.Position;
            _isHoveringPanel = _panelBounds.Contains(mousePos);
            _isHoveringPlayPause = _isHoveringPanel && _playPauseBounds.Contains(mousePos);
            _isHoveringVolume = _isHoveringPanel && (_volumeControlBounds.Contains(mousePos) || VolumeTrackBar.MouseOver);
            _isHoveringSettings = _isHoveringPanel && _settingsBounds.Contains(mousePos);
            _isHoveringTwitchChat = IsTwitchStream && _isHoveringPanel && _twitchChatBounds.Contains(mousePos);
            _isHoveringClose = _isHoveringPanel && _closeBounds.Contains(mousePos);
            _isHoveringLock = _isHoveringPanel && _lockBounds.Contains(mousePos);
            UpdateTooltip();
        }

        private void UpdateFadeAnimation()
        {
            if (IsVisible && !_wasHoveringPanel)
            {
                StartFadeIn();
            }
            else if (!IsVisible && _wasHoveringPanel)
            {
                StartFadeOut();
            }

            _wasHoveringPanel = IsVisible;
        }

        private void UpdateTooltip()
        {
            if (!_isHoveringPanel)
            {
                CurrentTooltip = null;
                return;
            }

            if (_isHoveringPlayPause)
            {
                CurrentTooltip = IsPaused ? "Play" : "Pause";
            }
            else if (_isHoveringLock)
            {
                CurrentTooltip = _isLocked ? "Unlock Position" : "Lock Position";
            }
            else if (_isHoveringVolume && _volumeIconBounds.Contains(GameService.Input.Mouse.Position))
            {
                CurrentTooltip = Volume == 0 ? "Unmute" : "Mute";
            }
            else if (_isHoveringSettings)
            {
                CurrentTooltip = "Settings";
            }
            else if (_isHoveringTwitchChat)
            {
                CurrentTooltip = "Toggle Twitch Chat";
            }
            else if (_isHoveringClose)
            {
                CurrentTooltip = "Close";
            }
            else
            {
                CurrentTooltip = null;
            }
        }

        public bool HandleMouseDown(Point mousePosition)
        {
            if (!IsVisible)
            {
                return false;
            }

            if (VolumeTrackBar.AbsoluteBounds.Contains(mousePosition))
            {
                return true;
            }

            if (SeekBar?.AbsoluteBounds.Contains(mousePosition) == true)
            {
                return true;
            }

            if (_playPauseBounds.Contains(mousePosition))
            {
                RaisePlayPauseClicked();
                return true;
            }

            if (_settingsBounds.Contains(mousePosition))
            {
                RaiseSettingsClicked();
                return true;
            }

            if (IsTwitchStream && _twitchChatBounds.Contains(mousePosition))
            {
                TwitchChatClicked?.Invoke(this, EventArgs.Empty);
                return true;
            }

            if (_closeBounds.Contains(mousePosition))
            {
                CloseClicked?.Invoke(this, EventArgs.Empty);
                return true;
            }

            if (_lockBounds.Contains(mousePosition))
            {
                _isLocked = !_isLocked;
                LockToggled?.Invoke(this, _isLocked);
                return true;
            }

            if (_volumeIconBounds.Contains(mousePosition))
            {
                ToggleMuteAndNotify();
                return true;
            }

            if (_volumeControlBounds.Contains(mousePosition))
            {
                return true;
            }

            if (_isSeekable && !IsTwitchStream && _seekBarBackgroundBounds.Contains(mousePosition))
            {
                return true;
            }

            return false;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!ShouldDraw)
            {
                return;
            }

            DrawLockButton(spriteBatch);
            DrawStreamInfo(spriteBatch);
            DrawPlayPauseButton(spriteBatch);
            DrawSeekBarControls(spriteBatch);
            DrawVolumeIcon(spriteBatch);
            if (IsTwitchStream)
            {
                DrawTwitchChatButton(spriteBatch);
            }
            DrawSettingsButton(spriteBatch);
            DrawCloseButton(spriteBatch);
        }

        private void DrawSeekBarControls(SpriteBatch spriteBatch)
        {
            if (!_isSeekable || IsTwitchStream) return;

            Renderer.DrawSeekBarBackground(spriteBatch, _seekBarBackgroundBounds, Opacity);
            Renderer.DrawTimeText(spriteBatch, FormatTimeDisplay(), _timeDisplayBounds, Opacity);
        }

        private void DrawPlayPauseButton(SpriteBatch spriteBatch)
        {
            var drawBounds = _playPauseBounds;
            if (_isHoveringPlayPause)
            {
                int scaledSize = (int)(_playPauseBounds.Width * HoverScale);
                int offset = (scaledSize - _playPauseBounds.Width) / 2;
                drawBounds = new Rectangle(
                    _playPauseBounds.X - offset,
                    _playPauseBounds.Y - offset,
                    scaledSize,
                    scaledSize);
            }

            Renderer.DrawPlayPauseButton(spriteBatch, drawBounds, IsPaused, _isHoveringPlayPause, Opacity);
        }

        private void DrawVolumeIcon(SpriteBatch spriteBatch)
        {
            Renderer.DrawVolumeIconWithBackground(
                spriteBatch,
                _volumeIconBounds,
                _volumeControlBounds,
                Volume,
                _isHoveringVolume,
                Opacity);
        }

        private void DrawSettingsButton(SpriteBatch spriteBatch)
        {
            Renderer.DrawSettingsButtonWithBackground(spriteBatch, _settingsBounds, _isHoveringSettings, Opacity);
        }

        private void DrawTwitchChatButton(SpriteBatch spriteBatch)
        {
            Renderer.DrawTwitchChatButton(spriteBatch, _twitchChatBounds, _isHoveringTwitchChat, Opacity);
        }

        private void DrawCloseButton(SpriteBatch spriteBatch)
        {
            Renderer.DrawCloseButton(spriteBatch, _closeBounds, _isHoveringClose, Opacity);
        }

        private void DrawLockButton(SpriteBatch spriteBatch)
        {
            Renderer.DrawLockButton(spriteBatch, _lockBounds, _isLocked, _isHoveringLock, Opacity);
        }

        private void DrawStreamInfo(SpriteBatch spriteBatch)
        {
            if (string.IsNullOrEmpty(StreamTitle)) return;
            Renderer.DrawStreamInfo(spriteBatch, _streamInfoBounds, StreamTitle, ViewerCount, GameName, Opacity);
        }

        #endregion
    }
}
