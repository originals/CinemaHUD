using System;
using System.Threading.Tasks;
using Blish_HUD;
using CinemaModule.Models.WatchParty;
using CinemaModule.Services.YouTube;

namespace CinemaModule.Controllers.WatchParty
{
    public sealed class WatchPartyVideoLoader
    {
        private static readonly Logger Logger = Logger.GetLogger<WatchPartyVideoLoader>();

        private readonly PlaybackController _playbackController;
        private readonly DisplayController _displayController;
        private readonly YouTubeService _youtubeService;
        private readonly Func<MemberState, Task> _reportMemberState;

        public WatchPartyVideoLoader(
            PlaybackController playbackController,
            DisplayController displayController,
            YouTubeService youtubeService,
            Func<MemberState, Task> reportMemberState)
        {
            _playbackController = playbackController;
            _displayController = displayController;
            _youtubeService = youtubeService;
            _reportMemberState = reportMemberState;
        }

        public async Task<VideoLoadResult> LoadVideoAsync(string videoId, Func<string, bool> validateLoadingState)
        {
            Logger.Debug($"Starting video load {videoId}");
            PrepareForVideoLoad();
            _ = _reportMemberState(MemberState.Loading);
            _ = LoadThumbnailAsync(videoId);

            var (streamUrl, isLiveStream) = await ResolveStreamUrlAsync(videoId, validateLoadingState).ConfigureAwait(false);

            if (string.IsNullOrEmpty(streamUrl))
            {
                Logger.Warn($"Failed to resolve stream URL for {videoId} - video may be unavailable or age-restricted");
                return VideoLoadResult.Failed();
            }

            if (!validateLoadingState(videoId))
                return VideoLoadResult.Cancelled();

            return VideoLoadResult.Success(streamUrl, isLiveStream);
        }

        public void StartPlayback(string streamUrl, bool isLiveStream)
        {
            _playbackController.Play(streamUrl);
            _displayController.UpdateOfflineState(false);
            _displayController.UpdateSeekableState(!isLiveStream, 0);
        }

        public async Task LoadThumbnailAsync(string videoId)
        {
            try
            {
                var textureService = CinemaModule.Instance?.TextureService;
                if (textureService == null) return;

                var texture = await textureService.GetYouTubeThumbnailAsync(videoId).ConfigureAwait(false);
                if (texture?.Texture != null)
                    _displayController.UpdateOfflineTexture(texture.Texture);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to load thumbnail for video: {videoId}");
            }
        }

        public async Task LoadVideoInfoAsync(string videoId)
        {
            try
            {
                var videoInfo = await _youtubeService.GetVideoInfoAsync(videoId).ConfigureAwait(false);
                if (videoInfo != null)
                    _displayController.UpdateStreamInfo(videoInfo.Title, null, videoInfo.Author);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to load video info: {videoId}");
            }
        }

        public void FetchQualities(string videoId)
        {
            _ = _youtubeService.FetchAndCacheQualitiesAsync(videoId);
        }

        #region Private Methods

        private void PrepareForVideoLoad()
        {
            _playbackController.Stop();
            _displayController.ClearVideoTexture();
            _displayController.UpdateOfflineTexture(null);
            _displayController.UpdateOfflineState(true);
            _youtubeService.ClearVideoInfoCache();
        }

        private async Task<(string Url, bool IsLiveStream)> ResolveStreamUrlAsync(string videoId, Func<string, bool> validateLoadingState)
        {
            var videoInfo = await _youtubeService.GetVideoInfoAsync(videoId).ConfigureAwait(false);

            if (!validateLoadingState(videoId))
                return (null, false);

            bool isLiveStream = videoInfo?.IsLiveStream == true;
            var (streamUrl, actualIsLiveStream) = await TryResolveStreamUrlAsync(videoId, isLiveStream).ConfigureAwait(false);

            if (videoInfo != null)
                _displayController.UpdateStreamInfo(videoInfo.Title, null, videoInfo.Author);

            return (streamUrl, actualIsLiveStream);
        }

        private async Task<(string Url, bool IsLiveStream)> TryResolveStreamUrlAsync(string videoId, bool isLiveStream)
        {
            if (isLiveStream)
            {
                Logger.Debug($"Video {videoId} is livestream, fetching HLS URL");
                var hlsUrl = await _youtubeService.GetLiveStreamUrlAsync(videoId).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(hlsUrl))
                    return (hlsUrl, true);

                Logger.Warn($"Failed to get HLS URL for {videoId}, trying regular stream URL");
                var regularUrl = await _youtubeService.GetPlayableStreamUrlAsync(videoId).ConfigureAwait(false);
                return (regularUrl, false);
            }

            var streamUrl = await _youtubeService.GetPlayableStreamUrlAsync(videoId).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(streamUrl))
                return (streamUrl, false);

            Logger.Debug($"Regular stream URL failed for {videoId}, trying livestream URL");
            var fallbackUrl = await _youtubeService.GetLiveStreamUrlAsync(videoId).ConfigureAwait(false);
            return (fallbackUrl, !string.IsNullOrEmpty(fallbackUrl));
        }

        #endregion
    }

    public readonly struct VideoLoadResult
    {
        public bool IsSuccess { get; }
        public bool IsCancelled { get; }
        public string StreamUrl { get; }
        public bool IsLiveStream { get; }

        private VideoLoadResult(bool isSuccess, bool isCancelled, string streamUrl, bool isLiveStream)
        {
            IsSuccess = isSuccess;
            IsCancelled = isCancelled;
            StreamUrl = streamUrl;
            IsLiveStream = isLiveStream;
        }

        public static VideoLoadResult Success(string streamUrl, bool isLiveStream)
            => new VideoLoadResult(true, false, streamUrl, isLiveStream);

        public static VideoLoadResult Failed()
            => new VideoLoadResult(false, false, null, false);

        public static VideoLoadResult Cancelled()
            => new VideoLoadResult(false, true, null, false);
    }
}
