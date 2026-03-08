using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace CinemaModule.Services.YouTube
{
    public class YouTubeStreamQuality
    {
        public string DisplayName { get; set; }
        public string StreamUrl { get; set; }
        public string AudioUrl { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Bitrate { get; set; }
    }

    public class YouTubeVideoInfo
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string ThumbnailUrl { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsLiveStream { get; set; }
    }

    public class YouTubeQualitiesEventArgs : EventArgs
    {
        public IReadOnlyList<string> QualityNames { get; }
        public int SelectedIndex { get; }

        public YouTubeQualitiesEventArgs(IReadOnlyList<string> qualityNames, int selectedIndex)
        {
            QualityNames = qualityNames;
            SelectedIndex = selectedIndex;
        }
    }

    public readonly struct YouTubeStreamUrls
    {
        public string VideoUrl { get; }
        public string AudioUrl { get; }
        public bool HasSeparateAudio => !string.IsNullOrEmpty(AudioUrl);

        public YouTubeStreamUrls(string videoUrl, string audioUrl = null)
        {
            VideoUrl = videoUrl;
            AudioUrl = audioUrl;
        }
    }

    public class YouTubeService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<YouTubeService>();
        private const int MaxCachedVideoInfos = 50;

        private readonly YoutubeClient _youtubeClient;
        private readonly SemaphoreSlim _apiSemaphore = new SemaphoreSlim(1, 1);
        private readonly object _qualitiesLock = new object();
        private readonly Dictionary<string, YouTubeVideoInfo> _videoInfoCache = new Dictionary<string, YouTubeVideoInfo>();
        private readonly List<string> _videoInfoCacheOrder = new List<string>();

        private List<YouTubeStreamQuality> _cachedQualities = new List<YouTubeStreamQuality>();
        private int _selectedQualityIndex;
        private int _isFetchingQualities;
        private bool _isDisposed;

        public event EventHandler<YouTubeQualitiesEventArgs> QualitiesChanged;

        public IReadOnlyList<YouTubeStreamQuality> CachedQualities
        {
            get
            {
                lock (_qualitiesLock)
                {
                    return _cachedQualities.ToList();
                }
            }
        }

        public YouTubeService()
        {
            _youtubeClient = new YoutubeClient();
        }

        public static bool IsYouTubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return url.Contains("youtube.com") || url.Contains("youtu.be");
        }

        public static string ExtractVideoId(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                var videoId = VideoId.TryParse(url);
                return videoId?.Value;
            }
            catch
            {
                return null;
            }
        }

        public async Task<YouTubeVideoInfo> GetVideoInfoAsync(string videoIdOrUrl)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(videoIdOrUrl))
                return null;

            var videoId = ExtractVideoId(videoIdOrUrl) ?? videoIdOrUrl;

            if (_videoInfoCache.TryGetValue(videoId, out var cachedInfo))
                return cachedInfo;

            if (string.IsNullOrWhiteSpace(videoId) || videoId.Length < 5)
                return null;

            await _apiSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isDisposed)
                    return null;

                if (_videoInfoCache.TryGetValue(videoId, out cachedInfo))
                    return cachedInfo;

                var video = await _youtubeClient.Videos.GetAsync(videoId).ConfigureAwait(false);
                if (video == null)
                    return null;

                var info = new YouTubeVideoInfo
                {
                    VideoId = video.Id.Value,
                    Title = video.Title ?? "Unknown",
                    Author = video.Author?.ChannelTitle ?? "Unknown",
                    ThumbnailUrl = video.Thumbnails?.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url,
                    Duration = video.Duration ?? TimeSpan.Zero,
                    IsLiveStream = video.Duration == null
                };

                CacheVideoInfo(videoId, info);
                return info;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to get video info for: {videoIdOrUrl}");
                return null;
            }
            finally
            {
                _apiSemaphore.Release();
            }
        }

        private void CacheVideoInfo(string videoId, YouTubeVideoInfo info)
        {
            if (_videoInfoCache.ContainsKey(videoId))
                return;

            if (_videoInfoCacheOrder.Count >= MaxCachedVideoInfos)
            {
                var oldestId = _videoInfoCacheOrder[0];
                _videoInfoCacheOrder.RemoveAt(0);
                _videoInfoCache.Remove(oldestId);
            }

            _videoInfoCache[videoId] = info;
            _videoInfoCacheOrder.Add(videoId);
        }

        public async Task<string> GetPlayableStreamUrlAsync(string videoIdOrUrl)
        {
            var result = await GetBestQualityStreamUrlsAsync(videoIdOrUrl).ConfigureAwait(false);
            return result.VideoUrl;
        }

        public async Task<YouTubeStreamUrls> GetBestQualityStreamUrlsAsync(string videoIdOrUrl)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(videoIdOrUrl))
                return default;

            try
            {
                var videoId = ExtractVideoId(videoIdOrUrl) ?? videoIdOrUrl;
                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId).ConfigureAwait(false);

                var videoOnlyStreams = streamManifest.GetVideoOnlyStreams()
                    .OrderByDescending(s => s.VideoQuality.MaxHeight)
                    .ThenByDescending(s => s.Bitrate.BitsPerSecond)
                    .ToList();

                var preferredVideo = videoOnlyStreams.FirstOrDefault(s => Is1080p(s.VideoQuality.MaxHeight, s.VideoQuality.Label))
                    ?? videoOnlyStreams.FirstOrDefault();

                if (preferredVideo != null)
                {
                    var bestAudio = streamManifest.GetAudioOnlyStreams()
                        .OrderByDescending(a => a.Bitrate.BitsPerSecond)
                        .FirstOrDefault();
                    return new YouTubeStreamUrls(preferredVideo.Url, bestAudio?.Url);
                }

                var muxedStreams = streamManifest.GetMuxedStreams()
                    .OrderByDescending(s => s.VideoQuality.MaxHeight)
                    .ToList();

                var preferredMuxed = muxedStreams.FirstOrDefault(s => Is1080p(s.VideoQuality.MaxHeight, s.VideoQuality.Label))
                    ?? muxedStreams.FirstOrDefault();

                return new YouTubeStreamUrls(preferredMuxed?.Url);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to get playable stream URL for: {videoIdOrUrl}");
                return default;
            }
        }

        public async Task<string> GetLiveStreamUrlAsync(string videoIdOrUrl)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(videoIdOrUrl))
                return null;

            try
            {
                var videoId = ExtractVideoId(videoIdOrUrl) ?? videoIdOrUrl;
                return await _youtubeClient.Videos.Streams.GetHttpLiveStreamUrlAsync(videoId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to get live stream URL for: {videoIdOrUrl}");
                return null;
            }
        }

        public async Task<YouTubeStreamUrls> GetBestStreamUrlsAsync(string videoIdOrUrl)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(videoIdOrUrl))
                return default;

            var videoInfo = await GetVideoInfoAsync(videoIdOrUrl).ConfigureAwait(false);
            if (videoInfo?.IsLiveStream == true)
            {
                var liveUrl = await GetLiveStreamUrlAsync(videoIdOrUrl).ConfigureAwait(false);
                return new YouTubeStreamUrls(liveUrl);
            }

            var streamUrls = await GetBestQualityStreamUrlsAsync(videoIdOrUrl).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(streamUrls.VideoUrl))
                return streamUrls;

            var fallbackUrl = await GetLiveStreamUrlAsync(videoIdOrUrl).ConfigureAwait(false);
            return new YouTubeStreamUrls(fallbackUrl);
        }

        public async Task<string> GetBestStreamUrlAsync(string videoIdOrUrl)
        {
            var result = await GetBestStreamUrlsAsync(videoIdOrUrl).ConfigureAwait(false);
            return result.VideoUrl;
        }

        public async Task<List<YouTubeStreamQuality>> GetStreamQualitiesAsync(string videoIdOrUrl)
        {
            var qualities = new List<YouTubeStreamQuality>();

            if (_isDisposed || string.IsNullOrWhiteSpace(videoIdOrUrl))
                return qualities;

            try
            {
                var videoId = ExtractVideoId(videoIdOrUrl) ?? videoIdOrUrl;
                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId).ConfigureAwait(false);

                var bestAudioUrl = streamManifest.GetAudioOnlyStreams()
                    .OrderByDescending(a => a.Bitrate.BitsPerSecond)
                    .FirstOrDefault()?.Url;

                foreach (var stream in streamManifest.GetMuxedStreams())
                {
                    qualities.Add(new YouTubeStreamQuality
                    {
                        DisplayName = stream.VideoQuality.Label,
                        StreamUrl = stream.Url,
                        Width = stream.VideoResolution.Width,
                        Height = stream.VideoResolution.Height,
                        Bitrate = stream.Bitrate.BitsPerSecond
                    });
                }

                foreach (var stream in streamManifest.GetVideoOnlyStreams())
                {
                    qualities.Add(new YouTubeStreamQuality
                    {
                        DisplayName = stream.VideoQuality.Label,
                        StreamUrl = stream.Url,
                        AudioUrl = bestAudioUrl,
                        Width = stream.VideoResolution.Width,
                        Height = stream.VideoResolution.Height,
                        Bitrate = stream.Bitrate.BitsPerSecond
                    });
                }

                return qualities
                    .OrderByDescending(q => q.Height)
                    .ThenByDescending(q => q.Bitrate)
                    .GroupBy(q => q.Height)
                    .Select(g => g.First())
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to get stream qualities for: {videoIdOrUrl}");
                return qualities;
            }
        }

        public async Task FetchAndCacheQualitiesAsync(string videoIdOrUrl)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(videoIdOrUrl))
                return;

            if (Interlocked.CompareExchange(ref _isFetchingQualities, 1, 0) != 0)
                return;

            try
            {
                var qualities = await GetStreamQualitiesAsync(videoIdOrUrl).ConfigureAwait(false);
                if (qualities.Count == 0)
                    return;

                IReadOnlyList<string> qualityNames;
                int selectedIndex;

                lock (_qualitiesLock)
                {
                    _cachedQualities = qualities;
                    _selectedQualityIndex = FindPreferredQualityIndex(qualities);
                    qualityNames = _cachedQualities.Select(q => q.DisplayName).ToList();
                    selectedIndex = _selectedQualityIndex;
                }

                QualitiesChanged?.Invoke(this, new YouTubeQualitiesEventArgs(qualityNames, selectedIndex));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to fetch YouTube qualities for: {videoIdOrUrl}");
            }
            finally
            {
                Interlocked.Exchange(ref _isFetchingQualities, 0);
            }
        }

        private static int FindPreferredQualityIndex(List<YouTubeStreamQuality> qualities)
        {
            int index1080 = qualities.FindIndex(q => Is1080p(q.Height, q.DisplayName));
            if (index1080 >= 0)
                return index1080;

            return 0;
        }

        private static bool Is1080p(int height, string displayName)
        {
            if (height >= 1070 && height <= 1090)
                return true;

            if (!string.IsNullOrEmpty(displayName) && displayName.Contains("1080"))
                return true;

            return false;
        }

        public YouTubeStreamQuality SelectQuality(int qualityIndex)
        {
            lock (_qualitiesLock)
            {
                if (qualityIndex < 0 || qualityIndex >= _cachedQualities.Count)
                    return null;

                if (_selectedQualityIndex == qualityIndex)
                    return _cachedQualities[qualityIndex];

                _selectedQualityIndex = qualityIndex;
                var qualityNames = _cachedQualities.Select(q => q.DisplayName).ToList();
                var selectedIndex = _selectedQualityIndex;
                var selectedQuality = _cachedQualities[qualityIndex];

                QualitiesChanged?.Invoke(this, new YouTubeQualitiesEventArgs(qualityNames, selectedIndex));
                return selectedQuality;
            }
        }

        public void ClearCachedQualities()
        {
            lock (_qualitiesLock)
            {
                _cachedQualities.Clear();
                _selectedQualityIndex = 0;
            }
        }

        public void ClearVideoInfoCache()
        {
            _videoInfoCache.Clear();
            _videoInfoCacheOrder.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _cachedQualities.Clear();
            _videoInfoCache.Clear();
            _videoInfoCacheOrder.Clear();
            _apiSemaphore.Dispose();
        }
    }
}
