using Blish_HUD;
using Blish_HUD.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
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
        private const int MaxMemoryCacheEntries = 100;

        private readonly HttpClient _httpClient;
        private readonly string _cacheDirectory;
        private readonly Dictionary<string, AsyncTexture2D> _memoryCache = new Dictionary<string, AsyncTexture2D>();
        private readonly List<string> _memoryCacheOrder = new List<string>();

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

            if (TryGetFromMemoryCache(cacheKey, out var memCached))
                return memCached;

            var cachePath = GetCachePath(cacheKey);
            if (cachePath == null)
                return await DownloadImageAsync(imageUrl);

            var cachedTexture = TryLoadFromCache(cachePath, imageUrl);
            if (cachedTexture != null)
            {
                AddToMemoryCache(cacheKey, cachedTexture);
                return cachedTexture;
            }

            var downloaded = await DownloadAndCacheAsync(imageUrl, cachePath);
            if (downloaded != null)
                AddToMemoryCache(cacheKey, downloaded);

            return downloaded;
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
            catch (Exception)
            {
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
            catch (Exception)
            {
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
            catch (Exception)
            {
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
            catch (Exception)
            {
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

        private bool TryGetFromMemoryCache(string cacheKey, out AsyncTexture2D texture)
        {
            if (_memoryCache.TryGetValue(cacheKey, out texture))
            {
                if (texture != null && !texture.IsDisposed)
                    return true;

                _memoryCache.Remove(cacheKey);
                _memoryCacheOrder.Remove(cacheKey);
            }
            texture = null;
            return false;
        }

        private void AddToMemoryCache(string cacheKey, AsyncTexture2D texture)
        {
            if (_memoryCache.ContainsKey(cacheKey))
                return;

            if (_memoryCacheOrder.Count >= MaxMemoryCacheEntries)
            {
                var oldest = _memoryCacheOrder[0];
                _memoryCacheOrder.RemoveAt(0);
                _memoryCache.Remove(oldest);
            }

            _memoryCache[cacheKey] = texture;
            _memoryCacheOrder.Add(cacheKey);
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
            catch (Exception)
            {
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
