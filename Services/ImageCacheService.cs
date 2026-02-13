using Blish_HUD;
using Blish_HUD.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CinemaModule.Services
{
    public class ImageCacheService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<ImageCacheService>();
        private const int MinValidImageSize = 100;
        private const string UrlMetadataExtension = ".url";

        private readonly HttpClient _httpClient;
        private readonly string _cacheDirectory;

        public ImageCacheService(string cacheDirectory, HttpClient httpClient)
        {
            _cacheDirectory = cacheDirectory;
            _httpClient = httpClient;

            if (!string.IsNullOrEmpty(_cacheDirectory))
                Directory.CreateDirectory(_cacheDirectory);
        }

        public async Task<AsyncTexture2D> GetImageAsync(string cacheKey, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(cacheKey) || string.IsNullOrWhiteSpace(imageUrl))
                return null;

            var cachePath = GetCachePath(cacheKey);
            if (cachePath == null)
                return await DownloadImageAsync(imageUrl);

            var cachedTexture = TryLoadFromCache(cachePath, imageUrl);
            if (cachedTexture != null)
                return cachedTexture;

            return await DownloadAndCacheAsync(imageUrl, cachePath);
        }

        private string GetCachePath(string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(_cacheDirectory))
                return null;

            var safeFileName = $"{SanitizeFileName(cacheKey)}.png";
            return Path.Combine(_cacheDirectory, safeFileName);
        }

        private static string GetUrlMetadataPath(string cachePath)
        {
            return cachePath + UrlMetadataExtension;
        }

        private AsyncTexture2D TryLoadFromCache(string cachePath, string currentUrl)
        {
            if (!File.Exists(cachePath))
                return null;

            if (!IsUrlUnchanged(cachePath, currentUrl))
                return null;

            try
            {
                var bytes = File.ReadAllBytes(cachePath);
                if (IsValidImageData(bytes))
                    return CreateTextureFromBytes(bytes);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to read cached image from {cachePath}: {ex.Message}");
            }

            return null;
        }

        private bool IsUrlUnchanged(string cachePath, string currentUrl)
        {
            var urlMetadataPath = GetUrlMetadataPath(cachePath);

            if (!File.Exists(urlMetadataPath))
                return false;

            try
            {
                var cachedUrl = File.ReadAllText(urlMetadataPath).Trim();
                return string.Equals(cachedUrl, currentUrl, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task<AsyncTexture2D> DownloadAndCacheAsync(string imageUrl, string cachePath)
        {
            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(imageUrl);

                if (!IsValidImageData(bytes))
                    return null;

                SaveToCache(cachePath, bytes, imageUrl);
                return CreateTextureFromBytes(bytes);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to download image from {imageUrl}: {ex.Message}");
                return null;
            }
        }

        private async Task<AsyncTexture2D> DownloadImageAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(imageUrl);
                return IsValidImageData(bytes) ? CreateTextureFromBytes(bytes) : null;
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to download image from {imageUrl}: {ex.Message}");
                return null;
            }
        }

        private void SaveToCache(string cachePath, byte[] bytes, string imageUrl)
        {
            try
            {
                File.WriteAllBytes(cachePath, bytes);
                File.WriteAllText(GetUrlMetadataPath(cachePath), imageUrl);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to cache image: {ex.Message}");
            }
        }

        private static bool IsValidImageData(byte[] bytes)
        {
            return bytes != null && bytes.Length > MinValidImageSize;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.ToLowerInvariant().Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

        private static AsyncTexture2D CreateTextureFromBytes(byte[] bytes)
        {
            try
            {
                using (var stream = new MemoryStream(bytes))
                {
                    var graphicsContext = GameService.Graphics.LendGraphicsDeviceContext();
                    try
                    {
                        var texture = Texture2D.FromStream(graphicsContext.GraphicsDevice, stream);
                        return new AsyncTexture2D(texture);
                    }
                    finally
                    {
                        graphicsContext.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to create texture from bytes: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
