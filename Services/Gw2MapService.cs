using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Blish_HUD;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CinemaModule.Services
{

    public class Gw2MapService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2MapService>();

        private const string Gw2MapsApiUrl = "https://api.guildwars2.com/v2/maps";
        private const string CacheFileName = "gw2_map_cache.json";
        private const string DefaultMapNamePrefix = "Map";
        private const string UnknownMapName = "Unknown";
        private const int InvalidMapId = 0;

        private readonly HttpClient _httpClient;
        private readonly Dictionary<int, string> _mapNameCache;
        private readonly string _cacheFilePath;

        public Gw2MapService(string cacheDirectory)
        {
            if (string.IsNullOrWhiteSpace(cacheDirectory))
                throw new ArgumentException("Cache directory cannot be null or empty.", nameof(cacheDirectory));

            _httpClient = new HttpClient();
            _mapNameCache = new Dictionary<int, string>();
            _cacheFilePath = Path.Combine(cacheDirectory, CacheFileName);

            LoadCacheFromFile();
        }

        public async Task<string> GetMapNameAsync(int mapId)
        {
            if (mapId <= InvalidMapId)
                return UnknownMapName;

            if (_mapNameCache.TryGetValue(mapId, out string cachedName))
                return cachedName;

            return await FetchMapNameFromApiAsync(mapId).ConfigureAwait(false);
        }

        private async Task<string> FetchMapNameFromApiAsync(int mapId)
        {
            try
            {
                var url = $"{Gw2MapsApiUrl}/{mapId}";
                var response = await _httpClient.GetAsync(url).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"Failed to fetch map name for ID {mapId}. Status: {response.StatusCode}");
                    return GetFallbackMapName(mapId);
                }

                var mapName = await ParseMapNameFromResponseAsync(response).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(mapName))
                {
                    _mapNameCache[mapId] = mapName;
                    SaveCacheToFile();
                    return mapName;
                }

                return GetFallbackMapName(mapId);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to fetch map name for ID {mapId}");
                return GetFallbackMapName(mapId);
            }
        }

        private static async Task<string> ParseMapNameFromResponseAsync(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var mapData = JObject.Parse(json);
            return mapData["name"]?.ToString();
        }

        private static string GetFallbackMapName(int mapId)
        {
            return $"{DefaultMapNamePrefix} {mapId}";
        }

        private void LoadCacheFromFile()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                {
                    Logger.Info("Map cache file not found, starting with empty cache");
                    return;
                }

                var json = File.ReadAllText(_cacheFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Logger.Info("Map cache file is empty, starting with empty cache");
                    return;
                }

                var cache = JsonConvert.DeserializeObject<Dictionary<int, string>>(json);
                if (cache != null)
                {
                    foreach (var entry in cache)
                    {
                        _mapNameCache[entry.Key] = entry.Value;
                    }
                    Logger.Info($"Loaded {_mapNameCache.Count} map names from cache");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load map cache file, starting with empty cache");
            }
        }

        private void SaveCacheToFile()
        {
            try
            {
                var directory = Path.GetDirectoryName(_cacheFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(_mapNameCache, Formatting.Indented);
                File.WriteAllText(_cacheFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to save map cache to file");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
