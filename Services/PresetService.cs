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
        private const string DefaultApiBaseUrl = "https://www.gw2opus.com/wp-json/cinemahud/v2";
        private const string ImageCacheSubfolder = "presets";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private readonly ImageCacheService _imageCache;

        private PresetsResponse _cachedPresets;
        private bool _isLoaded;


        public IReadOnlyList<WorldLocationPresetData> WorldLocationPresets =>
            _cachedPresets?.WorldLocations ?? new List<WorldLocationPresetData>();

        public IReadOnlyList<StreamCategory> StreamCategories =>
            _cachedPresets?.StreamCategories ?? new List<StreamCategory>();

        public IReadOnlyList<string> TwitchChannels =>
            StreamCategories
                .Where(c => c.IsTwitch)
                .SelectMany(c => c.TwitchChannelNames)
                .ToList();


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
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/config");

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warn($"Failed to load presets from API: {response.StatusCode}");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                _cachedPresets = JsonConvert.DeserializeObject<PresetsResponse>(json);

                ParseStreamCategories();

                _isLoaded = true;

                _ = LoadPresetImagesAsync();

                PresetsLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load presets from API");
            }
        }

        private void ParseStreamCategories()
        {
            if (_cachedPresets?.StreamCategories == null) return;

            foreach (var category in _cachedPresets.StreamCategories)
            {
                category.ParseChannels();
            }
        }

        private async Task LoadPresetImagesAsync()
        {
            var tasks = new List<Task>();

            if (_cachedPresets?.WorldLocations != null && _cachedPresets.WorldLocations.Count > 0)
            {
                tasks.AddRange(_cachedPresets.WorldLocations.Select(LoadImagesForWorldLocationAsync));
            }

            if (_cachedPresets?.StreamCategories != null)
            {
                foreach (var category in _cachedPresets.StreamCategories)
                {
                    if (!string.IsNullOrEmpty(category.Icon))
                    {
                        tasks.Add(LoadCategoryIconAsync(category));
                    }

                    foreach (var channel in category.Channels)
                    {
                        tasks.Add(LoadImagesForChannelAsync(channel));
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        private async Task LoadImagesForWorldLocationAsync(WorldLocationPresetData preset)
        {
            var avatarTask = _imageCache.GetImageAsync($"{preset.Id}_avatar", preset.Avatar);
            var pictureTask = _imageCache.GetImageAsync($"{preset.Id}_picture", preset.Picture);

            await Task.WhenAll(avatarTask, pictureTask);

            preset.AvatarTexture = avatarTask.Result;
            preset.PictureTexture = pictureTask.Result;
        }

        private async Task LoadCategoryIconAsync(StreamCategory category)
        {
            if (string.IsNullOrEmpty(category.Icon))
                return;

            category.IconTexture = await _imageCache.GetImageAsync($"cat_{category.Id}_icon", category.Icon);
        }

        private async Task LoadImagesForChannelAsync(ChannelData channel)
        {
            var tasks = new List<Task>();

            if (!string.IsNullOrEmpty(channel.Avatar))
            {
                tasks.Add(_imageCache.GetImageAsync($"ch_{channel.Id}_avatar", channel.Avatar)
                    .ContinueWith(t => channel.AvatarTexture = t.Result, TaskContinuationOptions.OnlyOnRanToCompletion));
            }

            if (!string.IsNullOrEmpty(channel.StaticImage))
            {
                tasks.Add(_imageCache.GetImageAsync($"ch_{channel.Id}_static", channel.StaticImage)
                    .ContinueWith(t => channel.StaticImageTexture = t.Result, TaskContinuationOptions.OnlyOnRanToCompletion));
            }

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        public void Dispose()
        {
            _imageCache?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
