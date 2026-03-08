using System;
using System.Threading.Tasks;
using Blish_HUD;
using CinemaModule.Models;
using CinemaModule.VideoPlayer;
using VideoPlayerClass = CinemaModule.VideoPlayer.VideoPlayer;
using CinemaModule.Services.Twitch;
using CinemaModule.Services.YouTube;
using CinemaModule.Settings;

namespace CinemaModule.Controllers
{
    public class PlaybackController : IDisposable
    {
        #region Members

        private static readonly Logger Logger = Logger.GetLogger<PlaybackController>();

        private readonly CinemaSettings _moduleSettings;
        private readonly CinemaUserSettings _userSettings;
        private readonly TwitchService _twitchService;
        private readonly YouTubeService _youtubeService;

        private VideoPlayerClass _videoPlayer;
        private bool _isPausedDueToRange;
        private bool _isDisposed;

        #endregion

        #region Events

        public event EventHandler<TwitchStreamRefreshedEventArgs> StreamUrlRefreshed;
        public event EventHandler<YouTubeStreamRefreshedEventArgs> YouTubeStreamUrlRefreshed;
        public event EventHandler<PlaybackState> PlaybackStateChanged;

        #endregion

        #region Properties

        public bool IsPausedDueToRange => _isPausedDueToRange;

        public bool IsOffline => _videoPlayer == null || 
            _videoPlayer.IsEnded || 
            (!_videoPlayer.IsPlaying && !_videoPlayer.IsPaused);

        public bool IsPlaying => _videoPlayer?.IsPlaying ?? false;

        public bool IsPaused => _videoPlayer?.IsPaused ?? false;

        public bool IsBuffering => _videoPlayer?.IsBuffering ?? false;

        public bool IsSeekable => _videoPlayer?.IsSeekable ?? false;

        public long Duration => _videoPlayer?.Length ?? 0;

        public float Position => _videoPlayer?.Position ?? 0f;

        #endregion

        public PlaybackController(CinemaSettings moduleSettings, CinemaUserSettings userSettings, TwitchService twitchService, YouTubeService youtubeService)
        {
            _moduleSettings = moduleSettings;
            _userSettings = userSettings;
            _twitchService = twitchService;
            _youtubeService = youtubeService;
        }

        #region Registration

        public void RegisterPlayer(VideoPlayerClass player)
        {
            _videoPlayer = player;
            _videoPlayer.PlaybackStateChanged += OnPlaybackStateChanged;
        }

        public void StartInitialPlaybackIfEnabled()
        {
            if (!_moduleSettings.IsEnabled)
                return;

            bool autoplay = _userSettings.AutoplayOnStartup;

            if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel)
            {
                var channelName = _userSettings.CurrentTwitchChannel;
                if (!string.IsNullOrEmpty(channelName))
                {
                    _ = RefreshTwitchStreamAsync(autoplay);
                    return;
                }
            }
            else if (_userSettings.CurrentStreamSourceType == StreamSourceType.YouTubeVideo)
            {
                var videoId = _userSettings.CurrentYouTubeVideo;
                if (!string.IsNullOrEmpty(videoId))
                {
                    _ = RefreshYouTubeStreamAsync(autoplay);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(_userSettings.StreamUrl))
            {
                _videoPlayer.Play(_userSettings.StreamUrl);
                if (!autoplay)
                    _videoPlayer.Pause();
            }
        }

        private void OnPlaybackStateChanged(object sender, PlaybackStateEventArgs e)
        {
            PlaybackStateChanged?.Invoke(this, e.State);
        }

        #endregion

        #region Playback Control

        public void Update()
        {
            _videoPlayer?.Update();
        }

        public void Play(string url) => _videoPlayer?.Play(url);

        public void Play(string url, string audioUrl) => _videoPlayer?.Play(url, audioUrl);

        public void Stop() => _videoPlayer?.Stop();

        public void TogglePause() => _videoPlayer?.TogglePause();

        public void Pause() => _videoPlayer?.Pause();

        public void Resume() => _videoPlayer?.Resume();

        public void SetVolume(int volume)
        {
            if (_videoPlayer != null)
                _videoPlayer.Volume = volume;
        }

        public void SetQuality(int qualityIndex) => _videoPlayer?.SetQuality(qualityIndex);

        public void Seek(float position)
        {
            if (_videoPlayer == null || float.IsNaN(position) || float.IsInfinity(position))
                return;

            _videoPlayer.Position = Math.Max(0f, Math.Min(1f, position));
        }

        #endregion

        #region Event Handlers

        public void HandleStreamUrlChanged(string url)
        {
            if (_videoPlayer == null || !_moduleSettings.IsEnabled)
                return;

            if (string.IsNullOrEmpty(url))
            {
                _videoPlayer.Stop();
                return;
            }

            if (_videoPlayer.CurrentUrl == url)
                return;

            _twitchService.ClearCachedQualities();
            _youtubeService.ClearCachedQualities();

            _videoPlayer.Stop();
            _videoPlayer.Play(url, _userSettings.AudioUrl);
            _userSettings.AudioUrl = null;

            FetchQualitiesForCurrentSource();
        }

        public void HandleEnabledChanged(bool isEnabled)
        {
            if (_videoPlayer == null)
                return;

            if (!isEnabled)
            {
                _videoPlayer.Stop();
                return;
            }

            StartPlaybackForCurrentSource();
        }

        public void RestartPlaybackIfNeeded()
        {
            if (!_moduleSettings.IsEnabled || string.IsNullOrEmpty(_userSettings.StreamUrl))
                return;

            if (_videoPlayer == null || _videoPlayer.IsPlaying)
                return;

            _videoPlayer.Play(_userSettings.StreamUrl);
        }

        public void UpdateRangeBasedPlayback(bool isInRange, CinemaDisplayMode displayMode)
        {
            if (_videoPlayer == null)
                return;

            bool shouldPause = displayMode == CinemaDisplayMode.InGame && !isInRange;

            if (shouldPause && !_isPausedDueToRange && !_videoPlayer.IsPaused)
            {
                _videoPlayer.Pause();
                _isPausedDueToRange = true;
            }
            else if (!shouldPause && _isPausedDueToRange)
            {
                _videoPlayer.Resume();
                _isPausedDueToRange = false;
            }
        }

        #endregion

        #region Stream Refresh

        public async Task RefreshTwitchStreamAndPlayAsync() => await RefreshTwitchStreamAsync(autoplay: true);

        public async Task RefreshYouTubeStreamAndPlayAsync(YouTubeVideoInfo videoInfo = null) => await RefreshYouTubeStreamAsync(autoplay: true, videoInfo);

        public async Task RefreshTwitchStreamAsync(bool autoplay)
        {
            var channelName = _userSettings.CurrentTwitchChannel;
            if (string.IsNullOrEmpty(channelName))
            {
                Logger.Warn("Cannot refresh Twitch stream - no channel name");
                return;
            }

            try
            {
                var freshUrl = await _twitchService.GetPlayableStreamUrlAsync(channelName);
                if (string.IsNullOrEmpty(freshUrl))
                {
                    Logger.Warn($"Failed to get fresh stream URL for channel: {channelName}");
                    return;
                }

                PlayTwitchStream(freshUrl, channelName, autoplay);
                StreamUrlRefreshed?.Invoke(this, new TwitchStreamRefreshedEventArgs(channelName, freshUrl));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to refresh Twitch stream for channel: {channelName}");
            }
        }

        public async Task RefreshYouTubeStreamAsync(bool autoplay, YouTubeVideoInfo videoInfo = null)
        {
            var videoId = _userSettings.CurrentYouTubeVideo;
            if (string.IsNullOrEmpty(videoId))
            {
                Logger.Warn("Cannot refresh YouTube stream - no video ID");
                return;
            }

            try
            {
                videoInfo = videoInfo ?? await _youtubeService.GetVideoInfoAsync(videoId);
                string freshUrl;
                string audioUrl = null;

                if (videoInfo?.IsLiveStream == true)
                {
                    freshUrl = await _youtubeService.GetLiveStreamUrlAsync(videoId);
                }
                else
                {
                    var streamUrls = await _youtubeService.GetBestQualityStreamUrlsAsync(videoId);
                    freshUrl = streamUrls.VideoUrl;
                    audioUrl = streamUrls.AudioUrl;
                }

                if (string.IsNullOrEmpty(freshUrl))
                {
                    Logger.Warn($"Failed to get fresh stream URL for video: {videoId}");
                    return;
                }

                PlayYouTubeStream(freshUrl, audioUrl, videoId, autoplay);
                YouTubeStreamUrlRefreshed?.Invoke(this, new YouTubeStreamRefreshedEventArgs(videoId, freshUrl));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to refresh YouTube stream for video: {videoId}");
            }
        }

        #endregion

        #region Private Helpers

        private void PlayTwitchStream(string streamUrl, string channelName, bool autoplay = true)
        {
            PlayAndPauseIfNeeded(streamUrl, null, autoplay);
            _twitchService.FetchAndCacheQualitiesAsync(channelName);
        }

        private void PlayYouTubeStream(string streamUrl, string audioUrl, string videoId, bool autoplay = true)
        {
            PlayAndPauseIfNeeded(streamUrl, audioUrl, autoplay);
            _ = _youtubeService.FetchAndCacheQualitiesAsync(videoId);
        }

        private void PlayAndPauseIfNeeded(string url, string audioUrl, bool autoplay)
        {
            _videoPlayer.Play(url, audioUrl);
            if (!autoplay)
                _videoPlayer.Pause();
        }

        private void FetchQualitiesForCurrentSource()
        {
            switch (_userSettings.CurrentStreamSourceType)
            {
                case StreamSourceType.TwitchChannel:
                    var channelName = _userSettings.CurrentTwitchChannel;
                    if (!string.IsNullOrEmpty(channelName))
                        _twitchService.FetchAndCacheQualitiesAsync(channelName);
                    break;

                case StreamSourceType.YouTubeVideo:
                    var videoId = _userSettings.CurrentYouTubeVideo;
                    if (!string.IsNullOrEmpty(videoId))
                        _ = _youtubeService.FetchAndCacheQualitiesAsync(videoId);
                    break;
            }
        }

        private void StartPlaybackForCurrentSource()
        {
            switch (_userSettings.CurrentStreamSourceType)
            {
                case StreamSourceType.TwitchChannel:
                    _ = RefreshTwitchStreamAndPlayAsync();
                    break;

                case StreamSourceType.YouTubeVideo:
                    _ = RefreshYouTubeStreamAndPlayAsync();
                    break;

                default:
                    if (!string.IsNullOrEmpty(_userSettings.StreamUrl))
                        _videoPlayer.Play(_userSettings.StreamUrl);
                    break;
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            if (_videoPlayer != null)
                _videoPlayer.PlaybackStateChanged -= OnPlaybackStateChanged;

            _isPausedDueToRange = false;
            _isDisposed = true;
        }

        #endregion
    }
}
