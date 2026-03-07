using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Glide;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.UI.Controls
{
    public class WorldVideoControls : Container
    {
        #region Constants

        private const int PanelHeight = 50;
        private const int ScreenMargin = 60;
        private const int TimeDisplayWidth = 90;
        private const int SeekBarWidth = 150;
        private const int VideoControlsOffset = 30;

        #endregion

        #region Fields

        private readonly BaseVideoControls _base;

        private Rectangle _playPauseBounds;
        private Rectangle _volumeIconBounds;
        private Rectangle _settingsBounds;
        private Rectangle _twitchChatBounds;
        private Rectangle _closeBounds;
        private Rectangle _timeDisplayBounds;

        private bool _isHoveringPlayPause;
        private bool _isHoveringVolume;
        private bool _isHoveringSettings;
        private bool _isHoveringTwitchChat;
        private bool _isHoveringClose;

        private Tween _fadeAnimation;
        private bool _shouldBeVisible;
        private bool _isSeekable;

        #endregion

        #region Events

        public event EventHandler PlayPauseClicked
        {
            add => _base.PlayPauseClicked += value;
            remove => _base.PlayPauseClicked -= value;
        }

        public event EventHandler<int> VolumeChanged
        {
            add => _base.VolumeChanged += value;
            remove => _base.VolumeChanged -= value;
        }

        public event EventHandler SettingsClicked
        {
            add => _base.SettingsClicked += value;
            remove => _base.SettingsClicked -= value;
        }

        public event EventHandler<int> QualityChanged
        {
            add => _base.QualityChanged += value;
            remove => _base.QualityChanged -= value;
        }

        public event EventHandler<float> SeekRequested
        {
            add => _base.SeekRequested += value;
            remove => _base.SeekRequested -= value;
        }

        public event EventHandler TwitchChatClicked;

        public event EventHandler CloseClicked;

        #endregion

        #region Properties

        public bool IsPaused
        {
            get => _base.IsPaused;
            set => _base.IsPaused = value;
        }

        public int Volume
        {
            get => _base.Volume;
            set => _base.Volume = value;
        }

        public bool IsTrackBarDragging => _base.VolumeTrackBar?.Dragging ?? false || _base.IsSeekBarDragging;

        public bool IsDropdownOpen => _base.QualityDropdown?.PanelOpen ?? false;

        public bool IsSeekable
        {
            get => _isSeekable;
            set
            {
                if (_isSeekable == value) return;
                _isSeekable = value;
                UpdateLayout();
            }
        }

        public float CurrentPosition
        {
            get => _base.CurrentPosition;
            set => _base.CurrentPosition = value;
        }

        public long Duration
        {
            get => _base.Duration;
            set => _base.Duration = value;
        }

        private bool ShowSeekBar => _isSeekable && !_isTwitchStream;

        private bool _isTwitchStream;
        public bool IsTwitchStream
        {
            get => _isTwitchStream;
            set
            {
                if (_isTwitchStream == value) return;
                _isTwitchStream = value;
                UpdateLayout();
            }
        }

        private bool _isWatchPartyViewer;
        public bool IsWatchPartyViewer
        {
            get => _isWatchPartyViewer;
            set
            {
                if (_isWatchPartyViewer == value) return;
                _isWatchPartyViewer = value;
                _base.IsWatchPartyViewer = value;
                UpdateLayout();
            }
        }

        #endregion

        public WorldVideoControls()
        {
            BackgroundColor = new Color(20, 20, 20, 200);
            Opacity = 0f;
            Visible = false;

            _base = new BaseVideoControls(this, BaseVideoControls.TrackBarWidth, BaseVideoControls.TrackBarHeight, BaseVideoControls.QualityDropdownWidth, true);

            UpdateLayout();
        }

        private void UpdateLayout()
        {
            UpdateControlBounds();
            UpdateSeekBarBounds();
            UpdateTrackBarBounds();
            UpdateQualityDropdownBounds();
        }

        protected override CaptureType CapturesInput()
        {
            // Don't capture input when not visible or fully faded out
            if (!Visible || !_shouldBeVisible || Opacity < 0.01f)
            {
                return CaptureType.None;
            }

            return base.CapturesInput();
        }

        #region Private Methods

        private void UpdateControlBounds()
        {
            int centerY = PanelHeight / 2;

            if (_isWatchPartyViewer)
            {
                _playPauseBounds = Rectangle.Empty;
                return;
            }

            _playPauseBounds = new Rectangle(
                BaseVideoControls.ControlSpacing,
                centerY - BaseVideoControls.IconSize / 2,
                BaseVideoControls.IconSize,
                BaseVideoControls.IconSize);
        }

        private void UpdateSeekBarBounds()
        {
            int centerY = PanelHeight / 2;
            int nextX = _isWatchPartyViewer
                ? BaseVideoControls.ControlSpacing
                : _playPauseBounds.Right + BaseVideoControls.ControlSpacing;

            if (ShowSeekBar)
            {
                _base.SeekBar.Visible = Visible;
                _base.SeekBar.Location = new Point(nextX, centerY - BaseVideoControls.SeekBarHeight / 2);
                _base.SeekBar.Size = new Point(SeekBarWidth, BaseVideoControls.SeekBarHeight);

                nextX = _base.SeekBar.Location.X + SeekBarWidth + BaseVideoControls.ControlSpacing;

                _timeDisplayBounds = new Rectangle(
                    nextX,
                    centerY - BaseVideoControls.IconSize / 2,
                    TimeDisplayWidth,
                    BaseVideoControls.IconSize);

                nextX += TimeDisplayWidth + BaseVideoControls.ControlSpacing;
            }
            else
            {
                _base.SeekBar.Visible = false;
            }

            _volumeIconBounds = new Rectangle(
                nextX,
                centerY - BaseVideoControls.IconSize / 2,
                BaseVideoControls.IconSize,
                BaseVideoControls.IconSize);
        }

        private void UpdateTrackBarBounds()
        {
            int centerY = PanelHeight / 2;
            int trackBarX = _volumeIconBounds.Right + BaseVideoControls.ControlSpacing;
            _base.VolumeTrackBar.Location = new Point(trackBarX, centerY - BaseVideoControls.TrackBarHeight / 2);
            _base.VolumeTrackBar.Size = new Point(BaseVideoControls.TrackBarWidth, BaseVideoControls.TrackBarHeight);
        }

        private void UpdateQualityDropdownBounds()
        {
            int centerY = PanelHeight / 2;
            int dropdownX = _base.VolumeTrackBar.Location.X + BaseVideoControls.TrackBarWidth + BaseVideoControls.ControlSpacing;

            int dropdownY = centerY - _base.QualityDropdown.Height / 2;
            _base.QualityDropdown.Location = new Point(dropdownX, dropdownY);

            bool hasQualities = _base.QualityDropdown.Items.Count > 0;
            _base.QualityDropdown.Visible = hasQualities;

            int nextX = hasQualities 
                ? dropdownX + BaseVideoControls.QualityDropdownWidth + BaseVideoControls.ControlSpacing 
                : dropdownX;

            _twitchChatBounds = new Rectangle(
                nextX,
                centerY - BaseVideoControls.IconSize / 2,
                BaseVideoControls.IconSize,
                BaseVideoControls.IconSize);

            int settingsX = IsTwitchStream 
                ? _twitchChatBounds.Right + BaseVideoControls.ControlSpacing 
                : nextX;
            _settingsBounds = new Rectangle(
                settingsX,
                centerY - BaseVideoControls.IconSize / 2,
                BaseVideoControls.IconSize,
                BaseVideoControls.IconSize);

            int closeX = _settingsBounds.Right + BaseVideoControls.ControlSpacing;
            _closeBounds = new Rectangle(
                closeX,
                centerY - BaseVideoControls.IconSize / 2,
                BaseVideoControls.IconSize,
                BaseVideoControls.IconSize);

            int calculatedWidth = _closeBounds.Right + BaseVideoControls.ControlSpacing;
            Size = new Point(calculatedWidth, PanelHeight);
        }

        #endregion

        #region Public Methods

        public void UpdatePosition(Rectangle videoBounds)
        {
            var screenWidth = GameService.Graphics.SpriteScreen.Width;
            var screenHeight = GameService.Graphics.SpriteScreen.Height;

            int x = videoBounds.X + (videoBounds.Width - Width) / 2;
            int y = videoBounds.Bottom + VideoControlsOffset;

            if (y + PanelHeight > screenHeight - ScreenMargin)
            {
                y = videoBounds.Y - PanelHeight - VideoControlsOffset;
            }

            if (y < ScreenMargin)
            {
                y = Math.Min(videoBounds.Bottom - PanelHeight - ScreenMargin, screenHeight - PanelHeight - ScreenMargin);
                y = Math.Max(ScreenMargin, y);
            }

            x = Math.Max(ScreenMargin, Math.Min(x, screenWidth - Width - ScreenMargin));
            y = Math.Max(ScreenMargin, Math.Min(y, screenHeight - PanelHeight - ScreenMargin));

            Location = new Point(x, y);
        }

        public new void Show()
        {
            if (_shouldBeVisible) return;
            _shouldBeVisible = true;
            Visible = true;

            if (ShowSeekBar)
            {
                _base.SeekBar.Visible = true;
            }

            _fadeAnimation?.Cancel();
            _fadeAnimation = GameService.Animation.Tweener
                .Tween(this, new { Opacity = 1f }, BaseVideoControls.FadeDuration)
                .Ease(Ease.QuadOut);
        }

        public new void Hide()
        {
            if (!_shouldBeVisible) return;
            if (IsTrackBarDragging || IsDropdownOpen) return;

            _shouldBeVisible = false;

            _fadeAnimation?.Cancel();
            _fadeAnimation = GameService.Animation.Tweener
                .Tween(this, new { Opacity = 0f }, BaseVideoControls.FadeDuration)
                .Ease(Ease.QuadIn)
                .OnComplete(() =>
                {
                    Visible = false;
                    _base.SeekBar.Visible = false;
                });
        }

        public void Reset()
        {
            _fadeAnimation?.Cancel();
            _shouldBeVisible = false;
            Opacity = 0f;
            Visible = false;
            Location = Point.Zero;

            _isHoveringPlayPause = false;
            _isHoveringVolume = false;
            _isHoveringSettings = false;
            _isHoveringTwitchChat = false;
            _isHoveringClose = false;

            _base.CurrentPosition = 0f;
            _base.Duration = 0;
            _isSeekable = false;
            _base.SeekBar.Value = 0;
            _base.SeekBar.Visible = false;
            UpdateLayout();
        }

        public void UpdateAvailableQualities(IReadOnlyList<string> qualityNames, int selectedIndex)
        {
            _base.UpdateAvailableQualities(qualityNames, selectedIndex);
            UpdateQualityDropdownBounds();
        }

        protected override void OnLeftMouseButtonPressed(MouseEventArgs e)
        {
            if (_base.VolumeTrackBar.AbsoluteBounds.Contains(e.MousePosition))
            {
                return;
            }

            if (ShowSeekBar && _base.SeekBar.AbsoluteBounds.Contains(e.MousePosition))
            {
                return;
            }

            base.OnLeftMouseButtonPressed(e);

            var localPos = new Point(e.MousePosition.X - AbsoluteBounds.X, e.MousePosition.Y - AbsoluteBounds.Y);

            if (!_isWatchPartyViewer && _playPauseBounds.Contains(localPos))
            {
                _base.RaisePlayPauseClicked();
                return;
            }

            if (_volumeIconBounds.Contains(localPos))
            {
                _base.ToggleMuteAndNotify();
                return;
            }

            if (_settingsBounds.Contains(localPos))
            {
                _base.RaiseSettingsClicked();
                return;
            }

            if (IsTwitchStream && _twitchChatBounds.Contains(localPos))
            {
                TwitchChatClicked?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (_closeBounds.Contains(localPos))
            {
                CloseClicked?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            var mousePos = GameService.Input.Mouse.Position;
            var localPos = new Point(mousePos.X - AbsoluteBounds.X, mousePos.Y - AbsoluteBounds.Y);

            _isHoveringPlayPause = !_isWatchPartyViewer && _playPauseBounds.Contains(localPos);
            _isHoveringVolume = _volumeIconBounds.Contains(localPos) || _base.VolumeTrackBar.MouseOver;
            _isHoveringSettings = _settingsBounds.Contains(localPos);
            _isHoveringTwitchChat = IsTwitchStream && _twitchChatBounds.Contains(localPos);
            _isHoveringClose = _closeBounds.Contains(localPos);

            _base.UpdateSeekBarDragState();
            UpdateTooltip(localPos);
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            base.PaintBeforeChildren(spriteBatch, bounds);

            if (Opacity < 0.01f) return;

            if (!_isWatchPartyViewer)
            {
                var playPauseRect = new Rectangle(
                    AbsoluteBounds.X + _playPauseBounds.X,
                    AbsoluteBounds.Y + _playPauseBounds.Y,
                    _playPauseBounds.Width,
                    _playPauseBounds.Height);
                _base.Renderer.DrawPlayPauseButton(spriteBatch, playPauseRect, IsPaused, _isHoveringPlayPause, Opacity, false);
            }

            if (ShowSeekBar)
            {
                DrawSeekBarSection(spriteBatch);
            }

            var volumeRect = new Rectangle(
                AbsoluteBounds.X + _volumeIconBounds.X,
                AbsoluteBounds.Y + _volumeIconBounds.Y,
                _volumeIconBounds.Width,
                _volumeIconBounds.Height);
            _base.Renderer.DrawVolumeIcon(spriteBatch, volumeRect, Volume, _isHoveringVolume, Opacity);

            if (IsTwitchStream)
            {
                var twitchChatRect = new Rectangle(
                    AbsoluteBounds.X + _twitchChatBounds.X,
                    AbsoluteBounds.Y + _twitchChatBounds.Y,
                    _twitchChatBounds.Width,
                    _twitchChatBounds.Height);
                _base.Renderer.DrawTwitchChatIconOnly(spriteBatch, twitchChatRect, _isHoveringTwitchChat, Opacity);
            }

            var settingsRect = new Rectangle(
                AbsoluteBounds.X + _settingsBounds.X,
                AbsoluteBounds.Y + _settingsBounds.Y,
                _settingsBounds.Width,
                _settingsBounds.Height);
            _base.Renderer.DrawSettingsIconOnly(spriteBatch, settingsRect, _isHoveringSettings, Opacity);

            var closeRect = new Rectangle(
                AbsoluteBounds.X + _closeBounds.X,
                AbsoluteBounds.Y + _closeBounds.Y,
                _closeBounds.Width,
                _closeBounds.Height);
            _base.Renderer.DrawCloseButton(spriteBatch, closeRect, _isHoveringClose, Opacity);
        }

        private void UpdateTooltip(Point localPos)
        {
            if (!_isWatchPartyViewer && _playPauseBounds.Contains(localPos))
            {
                BasicTooltipText = IsPaused ? "Play" : "Pause";
            }
            else if (_volumeIconBounds.Contains(localPos))
            {
                BasicTooltipText = Volume == 0 ? "Unmute" : "Mute";
            }
            else if (_settingsBounds.Contains(localPos))
            {
                BasicTooltipText = "Settings";
            }
            else if (IsTwitchStream && _twitchChatBounds.Contains(localPos))
            {
                BasicTooltipText = "Toggle Twitch Chat";
            }
            else if (_closeBounds.Contains(localPos))
            {
                BasicTooltipText = "Close";
            }
            else
            {
                BasicTooltipText = null;
            }
        }

        private void DrawSeekBarSection(SpriteBatch spriteBatch)
        {
            var timeRect = new Rectangle(
                AbsoluteBounds.X + _timeDisplayBounds.X,
                AbsoluteBounds.Y + _timeDisplayBounds.Y,
                _timeDisplayBounds.Width,
                _timeDisplayBounds.Height);
            _base.Renderer.DrawTimeText(spriteBatch, _base.FormatTimeDisplay(), timeRect, Opacity);
        }

        #endregion

        #region Cleanup

        protected override void DisposeControl()
        {
            _base.Dispose();
            base.DisposeControl();
        }

        #endregion
    }
}

