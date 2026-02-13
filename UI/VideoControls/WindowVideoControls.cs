using System;
using System.Collections.Generic;
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

        #endregion

        #region Fields

        private readonly int _buttonSize;
        private bool _isHoveringTwitchChat;
        private bool _isHoveringClose;
        private bool _isHoveringPanel;
        private bool _wasHoveringPanel;
        private Rectangle _panelBounds;
        private Rectangle _playPauseBounds;
        private Rectangle _volumeIconBounds;
        private Rectangle _volumeControlBounds;
        private Rectangle _settingsBounds;
        private Rectangle _twitchChatBounds;
        private Rectangle _closeBounds;

        #endregion

        #region Events

        public event EventHandler TwitchChatClicked;
        public event EventHandler CloseClicked;

        #endregion

        #region Properties

        public bool IsTwitchStream { get; set; }

        public bool IsVisible => _isHoveringPanel || (VolumeTrackBar?.Dragging ?? false) || (QualityDropdown?.MouseOver ?? false);

        private bool ShouldDraw => Opacity > 0.01f;

        #endregion

        public WindowVideoControls(Container parent, int buttonSize = DefaultButtonSize)
            : base(parent, TrackBarWidth, TrackBarHeight, QualityDropdownWidth)
        {
            _buttonSize = buttonSize;
        }

        #region Public Methods

        public void Update(Rectangle panelBounds)
        {
            _panelBounds = panelBounds;

            var center = new Vector2(panelBounds.X + panelBounds.Width / 2f, panelBounds.Y + panelBounds.Height / 2f);
            var bottomRight = new Vector2(panelBounds.X + panelBounds.Width, panelBounds.Y + panelBounds.Height);
            var bottomLeft = new Vector2(panelBounds.X, panelBounds.Y + panelBounds.Height);
            var topRight = new Vector2(panelBounds.X + panelBounds.Width, panelBounds.Y);

            UpdatePlayPauseBounds(center);
            UpdateVolumeBounds(bottomRight, bottomLeft);
            UpdateSettingsBounds(topRight);
            if (IsTwitchStream)
            {
                UpdateTwitchChatBounds();
            }
            UpdateCloseBounds();
            UpdateTrackBarPosition();
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
            Vector2 bottomEdgeDir = Vector2.Normalize(bottomRight - bottomLeft);
            
            Vector2 volumePos = bottomRight - bottomEdgeDir * (TrackBarWidth + IconSliderSpacing + IconSize + ControlMargin);
            volumePos.Y -= ControlMargin + IconSize / 2f;

            _volumeIconBounds = new Rectangle(
                (int)volumePos.X,
                (int)(volumePos.Y - IconSize / 2f),
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
            if (VolumeTrackBar == null) return;

            int trackBarX = _volumeIconBounds.X + IconSize + IconSliderSpacing - _panelBounds.X;
            int trackBarY = _volumeIconBounds.Y + (IconSize - TrackBarHeight) / 2 - _panelBounds.Y;
            
            VolumeTrackBar.Location = new Point(trackBarX, trackBarY);
            VolumeTrackBar.Visible = ShouldDraw;
            VolumeTrackBar.Opacity = Opacity;
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
            if (QualityDropdown == null) return;

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

        private void UpdateHoverStates()
        {
            var mousePos = GameService.Input.Mouse.Position;
            _isHoveringPanel = _panelBounds.Contains(mousePos);
            IsHoveringPlayPause = _isHoveringPanel && _playPauseBounds.Contains(mousePos);
            IsHoveringVolume = _isHoveringPanel && (_volumeControlBounds.Contains(mousePos) || (VolumeTrackBar?.MouseOver ?? false));
            IsHoveringSettings = _isHoveringPanel && _settingsBounds.Contains(mousePos);
            _isHoveringTwitchChat = IsTwitchStream && _isHoveringPanel && _twitchChatBounds.Contains(mousePos);
            _isHoveringClose = _isHoveringPanel && _closeBounds.Contains(mousePos);
        }

        private void UpdateFadeAnimation()
        {
            bool shouldBeVisible = _isHoveringPanel || (VolumeTrackBar?.Dragging ?? false) || (QualityDropdown?.MouseOver ?? false);

            if (shouldBeVisible && !_wasHoveringPanel)
            {
                StartFadeIn();
            }
            else if (!shouldBeVisible && _wasHoveringPanel)
            {
                StartFadeOut();
            }

            _wasHoveringPanel = shouldBeVisible;
        }

        public bool HandleMouseDown(Point mousePosition)
        {
            if (!IsVisible)
            {
                return false;
            }

            if (VolumeTrackBar?.AbsoluteBounds.Contains(mousePosition) == true)
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

            if (_volumeIconBounds.Contains(mousePosition))
            {
                ToggleMuteAndNotify();
                return true;
            }

            if (_volumeControlBounds.Contains(mousePosition))
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

            DrawPlayPauseButton(spriteBatch);
            DrawVolumeIcon(spriteBatch);
            if (IsTwitchStream)
            {
                DrawTwitchChatButton(spriteBatch);
            }
            DrawSettingsButton(spriteBatch);
            DrawCloseButton(spriteBatch);
        }

        private void DrawPlayPauseButton(SpriteBatch spriteBatch)
        {
            var drawBounds = _playPauseBounds;
            if (IsHoveringPlayPause)
            {
                int scaledSize = (int)(_playPauseBounds.Width * HoverScale);
                int offset = (scaledSize - _playPauseBounds.Width) / 2;
                drawBounds = new Rectangle(
                    _playPauseBounds.X - offset,
                    _playPauseBounds.Y - offset,
                    scaledSize,
                    scaledSize);
            }

            Renderer.DrawPlayPauseButton(spriteBatch, drawBounds, IsPaused, IsHoveringPlayPause, Opacity);
        }

        private void DrawVolumeIcon(SpriteBatch spriteBatch)
        {
            Renderer.DrawVolumeIconWithBackground(
                spriteBatch,
                _volumeIconBounds,
                _volumeControlBounds,
                Volume,
                IsHoveringVolume,
                Opacity);
        }

        private void DrawSettingsButton(SpriteBatch spriteBatch)
        {
            Renderer.DrawSettingsButtonWithBackground(spriteBatch, _settingsBounds, IsHoveringSettings, Opacity);
        }

        private void DrawTwitchChatButton(SpriteBatch spriteBatch)
        {
            Renderer.DrawTwitchChatButton(spriteBatch, _twitchChatBounds, _isHoveringTwitchChat, Opacity);
        }

        private void DrawCloseButton(SpriteBatch spriteBatch)
        {
            Renderer.DrawCloseButton(spriteBatch, _closeBounds, _isHoveringClose, Opacity);
        }

        #endregion

        #region Cleanup

        public override void Dispose()
        {
            base.Dispose();
        }

        #endregion
    }
}
