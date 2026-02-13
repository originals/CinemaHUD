using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using CinemaModule.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.UI.Controls
{
    public class VideoControlsRenderer
    {
        #region Fields

        private readonly TextureService _textureService;
        private readonly AsyncTexture2D _playTexture;
        private readonly AsyncTexture2D _pauseTexture;
        private readonly AsyncTexture2D _volumeNotMutedTexture;
        private readonly AsyncTexture2D _volumeMutedTexture;
        private readonly AsyncTexture2D _volumeBgTexture;
        private readonly AsyncTexture2D _settingsIconTexture;
        private readonly AsyncTexture2D _settingsBgTexture;
        private readonly AsyncTexture2D _twitchChatIconTexture;
        private readonly AsyncTexture2D _closeIconTexture;
        private readonly AsyncTexture2D _qualityIconTexture;

        #endregion

        public VideoControlsRenderer(TextureService textureService)
        {
            _textureService = textureService;
            _pauseTexture = _textureService.GetPauseIcon();
            _playTexture = _textureService.GetPlayIcon();
            _volumeNotMutedTexture = _textureService.GetVolumeNotMutedIcon();
            _volumeMutedTexture = _textureService.GetVolumeMutedIcon();
            _settingsIconTexture = _textureService.GetSettingsIcon();
            _settingsBgTexture = _textureService.GetSettingsBackground();
            _twitchChatIconTexture = _textureService.GetTwitchChatIcon();
            _closeIconTexture = _textureService.GetCloseIcon();
            _qualityIconTexture = _textureService.GetQualityIcon();
            _volumeBgTexture = _textureService.GetVolumeBackground();
        }

        #region Public Methods

        public AsyncTexture2D GetVolumeTexture(int volume)
        {
            return volume == 0 ? _volumeMutedTexture : _volumeNotMutedTexture;
        }

        public void DrawPlayPauseButton(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isPaused,
            bool isHovering,
            float opacity,
            bool drawBackground = true)
        {
            Rectangle iconBounds;

            if (drawBackground)
            {
                if (_textureService.IsTextureReady(_settingsBgTexture))
                {
                    var bgColor = ApplyOpacity(Color.White, opacity);
                    spriteBatch.Draw(_settingsBgTexture, bounds, bgColor);
                }

                int iconPadding = 4;
                iconBounds = new Rectangle(
                    bounds.X + iconPadding,
                    bounds.Y + iconPadding,
                    bounds.Width - iconPadding * 2,
                    bounds.Height - iconPadding * 2);
            }
            else
            {
                iconBounds = bounds;
            }

            var iconColor = GetIconColor(isHovering, opacity);

            if (isPaused)
            {
                if (_textureService.IsTextureReady(_playTexture))
                {
                    spriteBatch.Draw(_playTexture, iconBounds, iconColor);
                }
            }
            else
            {
                if (_textureService.IsTextureReady(_pauseTexture))
                {
                    spriteBatch.Draw(_pauseTexture, iconBounds, iconColor);
                }
            }
        }

        public void DrawVolumeIcon(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            int volume,
            bool isHovering,
            float opacity)
        {
            var texture = GetVolumeTexture(volume);
            if (!_textureService.IsTextureReady(texture)) return;

            var iconColor = GetIconColor(isHovering, opacity);
            spriteBatch.Draw(texture, bounds, iconColor);
        }

        public void DrawVolumeIconWithBackground(
            SpriteBatch spriteBatch,
            Rectangle iconBounds,
            Rectangle backgroundBounds,
            int volume,
            bool isHovering,
            float opacity)
        {
            if (_textureService.IsTextureReady(_volumeBgTexture))
            {
                var bgColor = ApplyOpacity(Color.White, opacity);
                spriteBatch.Draw(_volumeBgTexture, backgroundBounds, bgColor);
            }

            DrawVolumeIcon(spriteBatch, iconBounds, volume, isHovering, opacity);
        }

        public void DrawSettingsButtonWithBackground(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isHovering,
            float opacity)
        {
            if (_textureService.IsTextureReady(_settingsBgTexture))
            {
                var bgColor = ApplyOpacity(Color.White, opacity);
                spriteBatch.Draw(_settingsBgTexture, bounds, bgColor);
            }

            if (_textureService.IsTextureReady(_settingsIconTexture))
            {
                int iconPadding = 4;
                var iconBounds = new Rectangle(
                    bounds.X + iconPadding,
                    bounds.Y + iconPadding,
                    bounds.Width - iconPadding * 2,
                    bounds.Height - iconPadding * 2);

                var iconColor = GetIconColor(isHovering, opacity);
                spriteBatch.Draw(_settingsIconTexture, iconBounds, iconColor);
            }
        }

        public void DrawSettingsIconOnly(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isHovering,
            float opacity)
        {
            if (_textureService.IsTextureReady(_settingsIconTexture))
            {
                var iconColor = GetIconColor(isHovering, opacity);
                spriteBatch.Draw(_settingsIconTexture, bounds, iconColor);
            }
        }

        public void DrawTwitchChatIconOnly(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isHovering,
            float opacity)
        {
            if (_textureService.IsTextureReady(_twitchChatIconTexture))
            {
                var iconColor = GetIconColor(isHovering, opacity);
                spriteBatch.Draw(_twitchChatIconTexture, bounds, iconColor);
            }
        }

        public void DrawTwitchChatButton(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isHovering,
            float opacity)
        {
            if (_textureService.IsTextureReady(_settingsBgTexture))
            {
                var bgColor = ApplyOpacity(Color.White, opacity);
                spriteBatch.Draw(_settingsBgTexture, bounds, bgColor);
            }

            if (_textureService.IsTextureReady(_twitchChatIconTexture))
            {
                int iconPadding = 4;
                var iconBounds = new Rectangle(
                    bounds.X + iconPadding,
                    bounds.Y + iconPadding,
                    bounds.Width - iconPadding * 2,
                    bounds.Height - iconPadding * 2);

                var iconColor = GetIconColor(isHovering, opacity);
                spriteBatch.Draw(_twitchChatIconTexture, iconBounds, iconColor);
            }
        }

        public void DrawCloseButton(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isHovering,
            float opacity)
        {
            if (_textureService.IsTextureReady(_settingsBgTexture))
            {
                var bgColor = ApplyOpacity(Color.White, opacity);
                spriteBatch.Draw(_settingsBgTexture, bounds, bgColor);
            }

            if (_textureService.IsTextureReady(_closeIconTexture))
            {
                int iconPadding = 4;
                var iconBounds = new Rectangle(
                    bounds.X + iconPadding -3,
                    bounds.Y + iconPadding - 3,
                    bounds.Width - (iconPadding - 3) * 2,
                    bounds.Height - (iconPadding - 3) * 2);

                var iconColor = GetIconColor(isHovering, opacity);

                //  plus icon so, rotate by 45 degrees to get a cross
                float rotation = MathHelper.ToRadians(45);
                var origin = new Vector2(_closeIconTexture.Width / 2f, _closeIconTexture.Height / 2f);
                var position = new Vector2(
                    iconBounds.X + iconBounds.Width / 2f,
                    iconBounds.Y + iconBounds.Height / 2f);
                var scale = new Vector2(
                    iconBounds.Width / (float)_closeIconTexture.Width,
                    iconBounds.Height / (float)_closeIconTexture.Height);

                spriteBatch.Draw(
                    _closeIconTexture,
                    position,
                    null,
                    iconColor,
                    rotation,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0f);

            }
        }

        #endregion

        private Color ApplyOpacity(Color color, float opacity)
        {
            return new Color(color.R, color.G, color.B, (int)(color.A * opacity));
        }

        private Color GetIconColor(bool isHovering, float opacity)
        {
            var baseColor = isHovering ? Color.White : new Color(220, 220, 220);
            return ApplyOpacity(baseColor, opacity);
        }
    }
}

