using System;
using System.Threading.Tasks;
using Blish_HUD;
using CinemaModule.Models;
using CinemaModule.VideoPlayer;
using VideoPlayerClass = CinemaModule.VideoPlayer.VideoPlayer;
using CinemaModule.Services;
using CinemaModule.Settings;
using CinemaModule.UI.VideoDisplays;

namespace CinemaModule.Controllers
{
    public class TwitchStreamRefreshedEventArgs : EventArgs
    {
        public string ChannelName { get; }
        public string StreamUrl { get; }

        public TwitchStreamRefreshedEventArgs(string channelName, string streamUrl)
        {
            ChannelName = channelName;
            StreamUrl = streamUrl;
        }
    }

    public class PlaybackController : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<PlaybackController>();

        private readonly CinemaSettings _moduleSettings;
        private readonly CinemaUserSettings _userSettings;
        private readonly TwitchService _twitchService;

        private VideoPlayerClass _videoPlayer;
        private bool _isPausedDueToRange;
        private bool _isDisposed;

        public event EventHandler<TwitchStreamRefreshedEventArgs> StreamUrlRefreshed;
        public event EventHandler<PlaybackState> PlaybackStateChanged;

        public bool IsPausedDueToRange => _isPausedDueToRange;

        public PlaybackController(CinemaSettings moduleSettings, CinemaUserSettings userSettings, TwitchService twitchService)
        {
            _moduleSettings = moduleSettings;
            _userSettings = userSettings;
            _twitchService = twitchService;
        }

        public void RegisterPlayer(VideoPlayerClass player)
        {
            _videoPlayer = player;
            _videoPlayer.PlaybackStateChanged += OnPlaybackStateChanged;
        }

        public void StartInitialPlaybackIfEnabled()
        {
            if (!_moduleSettings.IsEnabled || string.IsNullOrEmpty(_userSettings.StreamUrl))
                return;

            _videoPlayer.Play(_userSettings.StreamUrl);

            if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel)
            {
                var channelName = _userSettings.CurrentTwitchChannel;
                if (!string.IsNullOrEmpty(channelName))
                    _twitchService.FetchAndCacheQualitiesAsync(channelName);
            }
        }

        private void OnPlaybackStateChanged(object sender, PlaybackStateEventArgs e)
        {
            PlaybackStateChanged?.Invoke(this, e.State);
        }

        public void Update()
        {
            _videoPlayer?.Update();
        }

        public void Play(string url)
        {
            _videoPlayer?.Play(url);
        }

        public void Stop()
        {
            _videoPlayer?.Stop();
        }

        public void TogglePause()
        {
            _videoPlayer?.TogglePause();
        }

        public void SetVolume(int volume)
        {
            if (_videoPlayer != null)
                _videoPlayer.Volume = volume;
        }

        public void SetQuality(int qualityIndex)
        {
            _videoPlayer?.SetQuality(qualityIndex);
        }

        public void Seek(float position)
        {
            if (_videoPlayer != null)
                _videoPlayer.Position = position;
        }

        public bool IsSeekable => _videoPlayer?.IsSeekable ?? false;

        public long Duration => _videoPlayer?.Length ?? 0;

        public float Position => _videoPlayer?.Position ?? 0f;

        public void HandleStreamUrlChanged(string url)
        {
            if (_videoPlayer == null)
                return;

            _twitchService.ClearCachedQualities();

            if (!_moduleSettings.IsEnabled || string.IsNullOrEmpty(url))
                return;

            if (_videoPlayer.CurrentUrl == url)
                return;

            _videoPlayer.Stop();
            _videoPlayer.Play(url);

            if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel)
            {
                var channelName = _userSettings.CurrentTwitchChannel;
                if (!string.IsNullOrEmpty(channelName))
                    _twitchService.FetchAndCacheQualitiesAsync(channelName);
            }
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

            if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel)
            {
                _ = RefreshTwitchStreamAndPlayAsync();
                return;
            }

            if (!string.IsNullOrEmpty(_userSettings.StreamUrl))
            {
                _videoPlayer.Play(_userSettings.StreamUrl);
            }
        }

        public void RestartPlaybackIfNeeded()
        {
            if (!_moduleSettings.IsEnabled)
                return;

            if (string.IsNullOrEmpty(_userSettings.StreamUrl))
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
                return;
            }

            if (!shouldPause && _isPausedDueToRange)
            {
                _videoPlayer.Resume();
                _isPausedDueToRange = false;
            }
        }

        public async Task RefreshTwitchStreamAndPlayAsync()
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

                PlayTwitchStream(freshUrl, channelName);
                StreamUrlRefreshed?.Invoke(this, new TwitchStreamRefreshedEventArgs(channelName, freshUrl));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to refresh Twitch stream for channel: {channelName}");
            }
        }

        private void PlayTwitchStream(string streamUrl, string channelName)
        {
            _videoPlayer.Play(streamUrl);
            _twitchService.FetchAndCacheQualitiesAsync(channelName);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            if (_videoPlayer != null)
            {
                _videoPlayer.PlaybackStateChanged -= OnPlaybackStateChanged;
            }

            _isPausedDueToRange = false;
            _isDisposed = true;
        }
    }
}
