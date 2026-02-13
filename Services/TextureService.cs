using Blish_HUD;
using Blish_HUD.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CinemaModule.Services
{
    /// <summary>
    /// Access to bundled textures, GW2 asset textures, URL-based images with caching.
    /// </summary>
    public class TextureService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<TextureService>();
        private readonly ImageCacheService _imageCache;
        private readonly HttpClient _httpClient;
        private Texture2D _whitePixel;

        #region Texture Names

        private const string CornerIconTexture = "cinemahudx64.png";
        private const string EmblemTexture = "cinemahudx90.png";
        private const string LogoTexture = "quaggantv_highres.png";
        private const string LogoTextTexture = "cinemahudtext.png";
        private const string SmallWindowBackgroundTexture = "bgwindow3.png";
        private const string TwitchIconTextureName = "twitchicon.png";
        private const string PauseIconTexture = "pause.png";

        private const string TvSideTexture = "tv_side.png";
        private const string TvTopBottomTexture = "tv_topbottom.png";
        private const string TvBackTexture = "tv_back.png";
        private const string TvScreenOffTexture = "tv_screenoff.png";

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
        public AsyncTexture2D GetPauseIcon() => GetTexture(PauseIconTexture);
        public AsyncTexture2D GetDefaultAvatar() => GetTexture(CornerIconTexture);
        public AsyncTexture2D GetTvSide() => GetTexture(TvSideTexture);
        public AsyncTexture2D GetTvTopBottom() => GetTexture(TvTopBottomTexture);
        public AsyncTexture2D GetTvBack() => GetTexture(TvBackTexture);
        public AsyncTexture2D GetTvScreenOff() => GetTexture(TvScreenOffTexture);

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
        public AsyncTexture2D GetResizeCorner() => AsyncTexture2D.FromAssetId(156009);
        public AsyncTexture2D GetResizeCornerActive() => AsyncTexture2D.FromAssetId(156010);
        public AsyncTexture2D GetDisplayIcon() => AsyncTexture2D.FromAssetId(358406);
        public AsyncTexture2D GetSourceIcon() => AsyncTexture2D.FromAssetId(156909);
        public AsyncTexture2D GetCopyIcon() => AsyncTexture2D.FromAssetId(2208347);
        public AsyncTexture2D GetImportIcon() => AsyncTexture2D.FromAssetId(2208351);
        public AsyncTexture2D GetCardBackground() => AsyncTexture2D.FromAssetId(154960);
        public AsyncTexture2D GetWindowTexture() => AsyncTexture2D.FromAssetId(155997);

        #endregion

        #region URL-Based Images

        public async Task<AsyncTexture2D> GetImageFromUrlAsync(string cacheKey, string imageUrl)
        {
            return await _imageCache.GetImageAsync(cacheKey, imageUrl);
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
