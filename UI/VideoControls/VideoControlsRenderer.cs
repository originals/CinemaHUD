using Blish_HUD;
using Blish_HUD.Content;
using CinemaModule.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace CinemaModule.UI.Controls
{
    public class VideoControlsRenderer
    {
        #region Fields

        private const int IconPadding = 4;
        private const int CloseIconOffset = 3;

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
        private readonly AsyncTexture2D _seekBarBgTexture;
        private readonly AsyncTexture2D _lockIconTexture;
        private readonly AsyncTexture2D _lockActiveIconTexture;

        private RenderTarget2D _trackNameTexture;
        private string _cachedTrackName;

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
            _volumeBgTexture = _textureService.GetVolumeBackground();
            _seekBarBgTexture = _textureService.GetSeekBarBackground();
            _lockIconTexture = _textureService.GetLockIcon();
            _lockActiveIconTexture = _textureService.GetLockActiveIcon();
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
                DrawBackground(spriteBatch, _settingsBgTexture, bounds, opacity);
                iconBounds = GetPaddedIconBounds(bounds, IconPadding);
            }
            else
            {
                iconBounds = bounds;
            }

            var texture = isPaused ? _playTexture : _pauseTexture;
            DrawIcon(spriteBatch, texture, iconBounds, isHovering, opacity);
        }

        public void DrawVolumeIcon(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            int volume,
            bool isHovering,
            float opacity)
        {
            DrawIcon(spriteBatch, GetVolumeTexture(volume), bounds, isHovering, opacity);
        }

        public void DrawVolumeIconWithBackground(
            SpriteBatch spriteBatch,
            Rectangle iconBounds,
            Rectangle backgroundBounds,
            int volume,
            bool isHovering,
            float opacity)
        {
            DrawBackground(spriteBatch, _volumeBgTexture, backgroundBounds, opacity);
            DrawVolumeIcon(spriteBatch, iconBounds, volume, isHovering, opacity);
        }

        public void DrawSettingsButtonWithBackground(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isHovering,
            float opacity)
        {
            DrawIconWithBackground(spriteBatch, _settingsIconTexture, bounds, isHovering, opacity);
        }

        public void DrawSettingsIconOnly(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isHovering,
            float opacity)
        {
            DrawIcon(spriteBatch, _settingsIconTexture, bounds, isHovering, opacity);
        }

        public void DrawTwitchChatIconOnly(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isHovering,
            float opacity)
        {
            DrawIcon(spriteBatch, _twitchChatIconTexture, bounds, isHovering, opacity);
        }

        public void DrawTwitchChatButton(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isHovering,
            float opacity)
        {
            DrawIconWithBackground(spriteBatch, _twitchChatIconTexture, bounds, isHovering, opacity);
        }

        public void DrawCloseButton(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isHovering,
            float opacity)
        {
            DrawBackground(spriteBatch, _settingsBgTexture, bounds, opacity);

            if (!_textureService.IsTextureReady(_closeIconTexture)) return;

            int adjustedPadding = IconPadding - CloseIconOffset;
            var iconBounds = GetPaddedIconBounds(bounds, adjustedPadding);
            var iconColor = GetIconColor(isHovering, opacity);

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

        public void DrawTimeText(
            SpriteBatch spriteBatch,
            string timeText,
            Rectangle bounds,
            float opacity)
        {
            var font = GameService.Content.DefaultFont14;
            if (font == null) return;

            var textColor = ApplyOpacity(Color.White, opacity);
            var textSize = font.MeasureString(timeText);
            var textPos = new Vector2(
                bounds.X + (bounds.Width - textSize.Width) / 2,
                bounds.Y + (bounds.Height - textSize.Height) / 2);

            spriteBatch.DrawString(font, timeText, textPos, textColor);
        }

        public void DrawSeekBarBackground(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            float opacity)
        {
            DrawBackground(spriteBatch, _seekBarBgTexture, bounds, opacity);
        }

        public void DrawLockButton(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            bool isLocked,
            bool isHovering,
            float opacity)
        {
            var texture = isLocked ? _lockActiveIconTexture : _lockIconTexture;
            DrawIconWithBackground(spriteBatch, texture, bounds, isHovering, opacity);
        }

        public void DrawStreamInfo(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            string streamTitle,
            int? viewerCount,
            string gameName,
            float opacity)
        {
            var font = GameService.Content.DefaultFont16;
            if (font == null || string.IsNullOrEmpty(streamTitle)) return;

            string displayText = BuildStreamDisplayText(streamTitle, viewerCount, gameName);
            var textColor = ApplyOpacity(new Color(220, 220, 220), opacity);
            var textPos = new Vector2(bounds.X, bounds.Y + (bounds.Height - font.LineHeight) / 2f);

            spriteBatch.DrawString(font, displayText, textPos, textColor);
        }

        public void DrawRadioTrackInfo(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            string trackName,
            float opacity)
        {
            if (string.IsNullOrEmpty(trackName)) return;

            var graphicsDevice = spriteBatch.GraphicsDevice;
            UpdateTrackNameTexture(graphicsDevice, trackName);

            if (_trackNameTexture == null || _trackNameTexture.IsDisposed) return;

            float boxLeftRatio = 0.42f;
            float boxRightRatio = 0.92f;
            float boxTopRatio = 0.07f;
            float boxBottomRatio = 0.27f;

            int boxX = bounds.X + (int)(bounds.Width * boxLeftRatio);
            int boxY = bounds.Y + (int)(bounds.Height * boxTopRatio);
            int boxWidth = (int)(bounds.Width * (boxRightRatio - boxLeftRatio));
            int boxHeight = (int)(bounds.Height * (boxBottomRatio - boxTopRatio));

            float textureAspect = (float)_trackNameTexture.Width / _trackNameTexture.Height;
            int drawWidth = boxWidth;
            int drawHeight = (int)(drawWidth / textureAspect);

            if (drawHeight > boxHeight)
            {
                drawHeight = boxHeight;
                drawWidth = (int)(drawHeight * textureAspect);
            }

            int drawX = boxX + (boxWidth - drawWidth) / 2;
            int drawY = boxY + (boxHeight - drawHeight) / 2;

            var destRect = new Rectangle(drawX, drawY, drawWidth, drawHeight);
            var color = ApplyOpacity(Color.White, opacity);

            spriteBatch.Draw(_trackNameTexture, destRect, color);
        }

        private void UpdateTrackNameTexture(GraphicsDevice graphicsDevice, string trackName)
        {
            if (_cachedTrackName == trackName && _trackNameTexture != null && !_trackNameTexture.IsDisposed)
                return;

            _cachedTrackName = trackName;
            _trackNameTexture?.Dispose();

            var font = GameService.Content.DefaultFont32;
            if (font == null) return;

            string displayText = "Now Playing: " + trackName;
            var textSize = font.MeasureString(displayText);

            int padding = 8;
            int width = (int)textSize.Width + padding * 2;
            int height = (int)textSize.Height + padding * 2;

            if (width <= 0 || height <= 0) return;

            _trackNameTexture = new RenderTarget2D(graphicsDevice, width, height, false, 
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

            var previousTargets = graphicsDevice.GetRenderTargets();
            graphicsDevice.SetRenderTarget(_trackNameTexture);
            graphicsDevice.Clear(Color.Transparent);

            var spriteBatch = new SpriteBatch(graphicsDevice);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            var textColor = new Color(200, 225, 255);
            var textPos = new Vector2(padding, padding);
            spriteBatch.DrawString(font, displayText, textPos, textColor);

            spriteBatch.End();
            spriteBatch.Dispose();

            graphicsDevice.SetRenderTargets(previousTargets);
        }

        public Texture2D GetOrCreateTrackNameTexture(GraphicsDevice graphicsDevice, string trackName)
        {
            if (string.IsNullOrEmpty(trackName)) return null;

            UpdateTrackNameTexture(graphicsDevice, trackName);
            return _trackNameTexture;
        }

        public void DisposeTrackNameTexture()
        {
            _trackNameTexture?.Dispose();
            _trackNameTexture = null;
            _cachedTrackName = null;
        }

        #endregion

        #region Private Methods

        private void DrawBackground(SpriteBatch spriteBatch, AsyncTexture2D texture, Rectangle bounds, float opacity)
        {
            if (!_textureService.IsTextureReady(texture)) return;

            var bgColor = ApplyOpacity(Color.White, opacity);
            spriteBatch.Draw(texture, bounds, bgColor);
        }

        private void DrawIcon(SpriteBatch spriteBatch, AsyncTexture2D texture, Rectangle bounds, bool isHovering, float opacity)
        {
            if (!_textureService.IsTextureReady(texture)) return;

            var iconColor = GetIconColor(isHovering, opacity);
            spriteBatch.Draw(texture, bounds, iconColor);
        }

        private void DrawIconWithBackground(SpriteBatch spriteBatch, AsyncTexture2D iconTexture, Rectangle bounds, bool isHovering, float opacity)
        {
            DrawBackground(spriteBatch, _settingsBgTexture, bounds, opacity);
            var iconBounds = GetPaddedIconBounds(bounds, IconPadding);
            DrawIcon(spriteBatch, iconTexture, iconBounds, isHovering, opacity);
        }

        private Rectangle GetPaddedIconBounds(Rectangle bounds, int padding)
        {
            return new Rectangle(
                bounds.X + padding,
                bounds.Y + padding,
                bounds.Width - padding * 2,
                bounds.Height - padding * 2);
        }

        private string BuildStreamDisplayText(string streamTitle, int? viewerCount, string gameName)
        {
            string displayText = streamTitle;

            if (!string.IsNullOrEmpty(gameName))
            {
                displayText += $" • {gameName}";
            }

            if (viewerCount.HasValue)
            {
                displayText += $" • {viewerCount.Value:N0} viewers";
            }

            return displayText;
        }

        private Color ApplyOpacity(Color color, float opacity)
        {
            return new Color(color.R, color.G, color.B, (int)(color.A * opacity));
        }

        private Color GetIconColor(bool isHovering, float opacity)
        {
            var baseColor = isHovering ? Color.White : new Color(220, 220, 220);
            return ApplyOpacity(baseColor, opacity);
        }

        #endregion
    }
}

