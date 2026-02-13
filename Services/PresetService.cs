using Blish_HUD;
using CinemaModule.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CinemaModule.Services
{
    public class PresetService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<PresetService>();
        private const string DefaultApiBaseUrl = "https://www.gw2opus.com/wp-json/cinemahud/v1";
        private const string ImageCacheSubfolder = "presets";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private readonly ImageCacheService _imageCache;

        private PresetsResponse _cachedPresets;
        private bool _isLoaded;


        public IReadOnlyList<WorldLocationPresetData> WorldLocationPresets =>
            _cachedPresets?.WorldLocations ?? new List<WorldLocationPresetData>();

        public IReadOnlyList<StreamPresetData> StreamPresets =>
            _cachedPresets?.Streams ?? new List<StreamPresetData>();

        public IReadOnlyList<string> TwitchChannels =>
            _cachedPresets?.TwitchChannels ?? new List<string>();


        public bool IsLoaded => _isLoaded;

        public event EventHandler PresetsLoaded;


        public PresetService(string cacheDirectory) : this(cacheDirectory, DefaultApiBaseUrl)
        {
        }

        public PresetService(string cacheDirectory, string apiBaseUrl)
        {
            _apiBaseUrl = apiBaseUrl;
            _httpClient = new HttpClient { Timeout = RequestTimeout };

            var imageCacheDir = Path.Combine(cacheDirectory, ImageCacheSubfolder);
            _imageCache = new ImageCacheService(imageCacheDir, _httpClient);
        }

        public async Task LoadPresetsAsync()
        {
            try
            {
                Logger.Info("Loading presets from API...");
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/config");

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"Failed to load presets from API: {response.StatusCode}");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                _cachedPresets = JsonConvert.DeserializeObject<PresetsResponse>(json);
                _isLoaded = true;

                Logger.Info($"Loaded {WorldLocationPresets.Count} world locations, {StreamPresets.Count} streams, {TwitchChannels.Count} Twitch channels");
                
                _ = LoadPresetImagesAsync();
                
                PresetsLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load presets from API");
            }
        }

        private async Task LoadPresetImagesAsync()
        {
            if (_cachedPresets?.WorldLocations == null || _cachedPresets.WorldLocations.Count == 0)
                return;

            var tasks = _cachedPresets.WorldLocations.Select(LoadImagesForPresetAsync);
            await Task.WhenAll(tasks);
            
            Logger.Debug($"Finished loading images for {_cachedPresets.WorldLocations.Count} world location presets");
        }

        private async Task LoadImagesForPresetAsync(WorldLocationPresetData preset)
        {
            var avatarTask = _imageCache.GetImageAsync($"{preset.Id}_avatar", preset.Avatar);
            var pictureTask = _imageCache.GetImageAsync($"{preset.Id}_picture", preset.Picture);

            await Task.WhenAll(avatarTask, pictureTask);

            preset.AvatarTexture = avatarTask.Result;
            preset.PictureTexture = pictureTask.Result;
        }

        public void Dispose()
        {
            _imageCache?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
