using Blish_HUD;
using Blish_HUD.Content;
using CinemaModule.Models;
using CinemaModule.Models.Twitch;
using CinemaModule.Services.Twitch;
using CinemaModule.Services.YouTube;
using CinemaModule.Settings;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CinemaModule.Services
{
    public class TextureService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<TextureService>();
        private readonly ImageCacheService _imageCache;
        private readonly HttpClient _httpClient;
        private Texture2D _whitePixel;
        private TwitchStreamInfo _cachedTwitchStreamInfo;

        #region Texture Names

        private const string CornerIconTexture = "logo_64.png";
        private const string EmblemTexture = "logo_90.png";
        private const string LogoTexture = "logo_highres.png";
        private const string LogoTextTexture = "logo_text.png";
        private const string SmallWindowBackgroundTexture = "window_background.png";
        private const string TwitchIconTextureName = "icon_twitch.png";
        private const string TwitchBigTexture = "icon_twitch_large.png";
        private const string PauseIconTexture = "icon_pause.png";
        private const string WaypointIconTexture = "icon_waypoint.png";
        private const string DeleteIconTexture = "icon_delete.png";
        private const string ExportIconTexture = "icon_export.png";
        private const string ImportIconTexture = "icon_import.png";

        private const string YoutubeIconTexture = "icon_youtube.png";
        private const string VlcIconTexture = "vlc-icon.png";
        private const string TvSideTexture = "tv_frame_side.png";
        private const string TvTopBottomTexture = "tv_frame_topbottom.png";
        private const string TvBackTexture = "tv_frame_back.png";
        private const string TvScreenOffTexture = "tv_screen_off.png";
        private const string SeekBarBackgroundTexture = "155208_background.png";
        private const string ChatBackgroundTexture = "window_background_chat.png";

        #endregion

        public TextureService(string cacheDirectory)
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var imageCacheDir = Path.Combine(cacheDirectory, "images");
            _imageCache = new ImageCacheService(imageCacheDir, _httpClient);
        }

        #region Bundled Textures

        public AsyncTexture2D GetCornerIcon() => GetTexture(CornerIconTexture);
        public AsyncTexture2D GetEmblem() => GetTexture(EmblemTexture);
        public AsyncTexture2D GetLogo() => GetTexture(LogoTexture);
        public AsyncTexture2D GetLogoText() => GetTexture(LogoTextTexture);
        public AsyncTexture2D GetSmallWindowBackground() => GetTexture(SmallWindowBackgroundTexture);
        public AsyncTexture2D GetTwitchIcon() => GetTexture(TwitchIconTextureName);
        public AsyncTexture2D GetTwitchBigIcon() => GetTexture(TwitchBigTexture);
        public AsyncTexture2D GetPauseIcon() => GetTexture(PauseIconTexture);
        public AsyncTexture2D GetDeleteIcon() => GetTexture(DeleteIconTexture);
        public AsyncTexture2D GetExportIcon() => GetTexture(ExportIconTexture);
        public AsyncTexture2D GetImportIcon() => GetTexture(ImportIconTexture);
        public AsyncTexture2D GetYoutubeIcon() => GetTexture(YoutubeIconTexture);
        public AsyncTexture2D GetVlcIcon() => GetTexture(VlcIconTexture);
        public AsyncTexture2D GetDefaultAvatar() => GetTexture(CornerIconTexture);
        public AsyncTexture2D GetTvSide() => GetTexture(TvSideTexture);
        public AsyncTexture2D GetTvTopBottom() => GetTexture(TvTopBottomTexture);
        public AsyncTexture2D GetTvBack() => GetTexture(TvBackTexture);
        public AsyncTexture2D GetTvScreenOff() => GetTexture(TvScreenOffTexture);
        public AsyncTexture2D GetChatBackground() => GetTexture(ChatBackgroundTexture);

        #endregion

        #region GW2 Asset Textures

        public AsyncTexture2D GetPlayIcon() => AsyncTexture2D.FromAssetId(156998);
        public AsyncTexture2D GetVolumeNotMutedIcon() => AsyncTexture2D.FromAssetId(156738);
        public AsyncTexture2D GetVolumeMutedIcon() => AsyncTexture2D.FromAssetId(156739);
        public AsyncTexture2D GetSettingsIcon() => AsyncTexture2D.FromAssetId(155052);
        public AsyncTexture2D GetSettingsBackground() => AsyncTexture2D.FromAssetId(965776);
        public AsyncTexture2D GetTwitchChatIcon() => AsyncTexture2D.FromAssetId(155156);
        public AsyncTexture2D GetCloseIcon() => AsyncTexture2D.FromAssetId(255443);
        public AsyncTexture2D GetQualityIcon() => AsyncTexture2D.FromAssetId(440023);
        public AsyncTexture2D GetVolumeBackground() => AsyncTexture2D.FromAssetId(155208);
        public AsyncTexture2D GetSeekBarBackground() => GetTexture(SeekBarBackgroundTexture);
        public AsyncTexture2D GetResizeCorner() => AsyncTexture2D.FromAssetId(156009);
        public AsyncTexture2D GetResizeCornerActive() => AsyncTexture2D.FromAssetId(156010);
        public AsyncTexture2D GetLockIcon() => AsyncTexture2D.FromAssetId(733265);
        public AsyncTexture2D GetLockActiveIcon() => AsyncTexture2D.FromAssetId(733266);
        public AsyncTexture2D GetDisplayIcon() => AsyncTexture2D.FromAssetId(358406);
        public AsyncTexture2D GetSourceIcon() => AsyncTexture2D.FromAssetId(156909);
        public AsyncTexture2D GetCopyIcon() => AsyncTexture2D.FromAssetId(2208347);
        public AsyncTexture2D GetCardBackground() => AsyncTexture2D.FromAssetId(154960);
        public AsyncTexture2D GetWindowTexture() => AsyncTexture2D.FromAssetId(155997);
        public AsyncTexture2D GetSetScreenIcon() => AsyncTexture2D.FromAssetId(528726);
        public AsyncTexture2D GetWaypointIcon() => AsyncTexture2D.FromAssetId(156628);
        public AsyncTexture2D GetInfoIcon() => AsyncTexture2D.FromAssetId(1508665);
        public AsyncTexture2D GetRefreshIcon() => AsyncTexture2D.FromAssetId(156749);
        public AsyncTexture2D GetWatchPartyIcon() => AsyncTexture2D.FromAssetId(156694);
        public AsyncTexture2D GetArrowUpIcon() => AsyncTexture2D.FromAssetId(102617);
        public AsyncTexture2D GetArrowDownIcon() => AsyncTexture2D.FromAssetId(102618);
        public AsyncTexture2D GetTabbedWindowBackground() => AsyncTexture2D.FromAssetId(155985);
        public AsyncTexture2D GetMenuItemFade() => AsyncTexture2D.FromAssetId(156044);

        #endregion

        #region URL Based Images

        public async Task<AsyncTexture2D> GetImageFromUrlAsync(string cacheKey, string imageUrl)
        {
            return await _imageCache.GetImageAsync(cacheKey, imageUrl);
        }

        public async Task<AsyncTexture2D> GetYouTubeThumbnailAsync(string videoIdOrUrl)
        {
            if (string.IsNullOrWhiteSpace(videoIdOrUrl))
                return null;

            var videoId = YouTubeService.ExtractVideoId(videoIdOrUrl) ?? videoIdOrUrl;
            var thumbnailUrl = $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
            return await GetImageFromUrlAsync($"youtube_thumb_{videoId}", thumbnailUrl);
        }

        public async Task<AsyncTexture2D> GetTwitchAvatarAsync(string channelName, string avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(channelName) || string.IsNullOrWhiteSpace(avatarUrl))
                return null;

            return await GetImageFromUrlAsync($"twitch_avatar_{channelName}", avatarUrl);
        }

        public async Task<AsyncTexture2D> GetPresetImageAsync(string cacheKey, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(cacheKey) || string.IsNullOrWhiteSpace(imageUrl))
                return null;

            return await GetImageFromUrlAsync($"preset_{cacheKey}", imageUrl);
        }

        #endregion

        #region Offline Textures

        public void UpdateCachedStreamInfo(TwitchStreamInfo streamInfo)
        {
            _cachedTwitchStreamInfo = streamInfo;
        }

        public void ClearCachedStreamInfo()
        {
            _cachedTwitchStreamInfo = null;
        }

        public async Task<Texture2D> LoadOfflineTextureAsync(CinemaUserSettings userSettings, TwitchService twitchService)
        {
            if (userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel)
                return await LoadTwitchAvatarTextureAsync(userSettings, twitchService);

            if (userSettings.CurrentStreamSourceType == StreamSourceType.YouTubeVideo)
                return await LoadYouTubeThumbnailTextureAsync(userSettings);

            return await LoadStaticImageTextureAsync(userSettings);
        }

        private async Task<Texture2D> LoadTwitchAvatarTextureAsync(CinemaUserSettings userSettings, TwitchService twitchService)
        {
            var channelName = userSettings.CurrentTwitchChannel;
            if (string.IsNullOrEmpty(channelName))
                return null;

            var streamInfo = _cachedTwitchStreamInfo ?? await twitchService.GetStreamInfoAsync(channelName);
            if (streamInfo == null || string.IsNullOrEmpty(streamInfo.AvatarUrl))
                return null;

            var avatarTexture = await GetTwitchAvatarAsync(channelName, streamInfo.AvatarUrl);
            return avatarTexture?.Texture;
        }

        private async Task<Texture2D> LoadYouTubeThumbnailTextureAsync(CinemaUserSettings userSettings)
        {
            var videoId = userSettings.CurrentYouTubeVideo;
            if (string.IsNullOrEmpty(videoId))
                return null;

            var asyncTexture = await GetYouTubeThumbnailAsync(videoId);
            return asyncTexture?.Texture;
        }

        private async Task<Texture2D> LoadStaticImageTextureAsync(CinemaUserSettings userSettings)
        {
            var preset = userSettings.CurrentStreamPreset;
            if (preset == null || string.IsNullOrEmpty(preset.StaticImage))
                return null;

            var asyncTexture = await GetImageFromUrlAsync($"offline_static_{preset.Id}", preset.StaticImage);

            if (asyncTexture != null)
                preset.StaticImageTexture = asyncTexture;

            return asyncTexture?.Texture;
        }

        #endregion

        public Texture2D GetWhitePixel()
        {
            if (_whitePixel == null || _whitePixel.IsDisposed)
            {
                var graphicsContext = GameService.Graphics.LendGraphicsDeviceContext();
                try
                {
                    _whitePixel = new Texture2D(graphicsContext.GraphicsDevice, 1, 1);
                    _whitePixel.SetData(new[] { Microsoft.Xna.Framework.Color.White });
                }
                finally
                {
                    graphicsContext.Dispose();
                }
            }
            return _whitePixel;
        }

        public bool IsTextureReady(AsyncTexture2D texture) => texture != null && !texture.IsDisposed;
        public bool IsTextureReady(Texture2D texture) => texture != null && !texture.IsDisposed;

        public void Dispose()
        {
            _whitePixel?.Dispose();
            _whitePixel = null;
            _imageCache?.Dispose();
            _httpClient?.Dispose();
        }

        private AsyncTexture2D GetTexture(string textureName)
        {
            try
            {
                return CinemaModule.Instance.ContentsManager.GetTexture(textureName);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to load texture '{textureName}': {ex.Message}");
                return null;
            }
        }
    }
}
