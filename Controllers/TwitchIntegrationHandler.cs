using System;
using System.Linq;
using System.Threading.Tasks;
using Blish_HUD;
using CinemaModule.Models;
using CinemaModule.VideoPlayer;
using VideoPlayerClass = CinemaModule.VideoPlayer.VideoPlayer;
using CinemaModule.Services;
using CinemaModule.Settings;

namespace CinemaModule.Controllers
{
    public class TwitchIntegrationHandler : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<TwitchIntegrationHandler>();

        private readonly CinemaUserSettings _userSettings;
        private readonly TwitchService _twitchService;

        private VideoPlayerClass _videoPlayer;
        private bool _isDisposed;

        public event EventHandler<string> ChatChannelChangeRequested;
        public event EventHandler<TwitchQualitiesEventArgs> QualitiesUpdated;
        public event EventHandler<TwitchStreamInfo> StreamInfoUpdated;

        public TwitchIntegrationHandler(CinemaUserSettings userSettings, TwitchService twitchService)
        {
            _userSettings = userSettings;
            _twitchService = twitchService;

            _twitchService.QualitiesChanged += OnTwitchQualitiesChanged;
        }

        public void RegisterPlayer(VideoPlayerClass player)
        {
            _videoPlayer = player;
            _videoPlayer.QualitiesChanged += OnVideoQualitiesChanged;
        }

        public void InitializeStreamInfo()
        {
            if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel)
            {
                var channelName = _userSettings.CurrentTwitchChannel;
                if (!string.IsNullOrEmpty(channelName))
                    _ = FetchStreamInfoAsync(channelName);
            }
        }

        public bool IsTwitchStreamWithQualities()
        {
            return _userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel
                   && _twitchService.CachedQualities.Count > 0;
        }

        public void HandleQualityChange(int qualityIndex)
        {
            if (_videoPlayer == null)
                return;

            if (IsTwitchStreamWithQualities())
            {
                HandleTwitchQualityChange(qualityIndex);
                return;
            }

            HandleVideoPlayerQualityChange(qualityIndex);
        }

        public void HandleStreamSourceTypeChanged(StreamSourceType sourceType)
        {
            if (sourceType == StreamSourceType.TwitchChannel && _videoPlayer?.IsPlaying == true)
            {
                var channelName = _userSettings.CurrentTwitchChannel;
                if (!string.IsNullOrEmpty(channelName))
                {
                    _twitchService.FetchAndCacheQualitiesAsync(channelName);
                    _ = FetchStreamInfoAsync(channelName);
                    ChatChannelChangeRequested?.Invoke(this, channelName);
                }
            }
            else if (sourceType != StreamSourceType.TwitchChannel)
            {
                _twitchService.ClearCachedQualities();
                ChatChannelChangeRequested?.Invoke(this, null);
                StreamInfoUpdated?.Invoke(this, null);
            }
        }

        public void HandleStreamUrlChanged(bool isTwitchStream)
        {
            if (isTwitchStream)
            {
                var channelName = _userSettings.CurrentTwitchChannel;
                if (!string.IsNullOrEmpty(channelName))
                {
                    ChatChannelChangeRequested?.Invoke(this, channelName);
                    _ = FetchStreamInfoAsync(channelName);
                }
            }
            else
            {
                ChatChannelChangeRequested?.Invoke(this, null);
                StreamInfoUpdated?.Invoke(this, null);
            }
        }

        public async Task FetchStreamInfoAsync(string channelName)
        {
            if (string.IsNullOrEmpty(channelName))
            {
                StreamInfoUpdated?.Invoke(this, null);
                return;
            }

            var streamInfo = await _twitchService.GetStreamInfoAsync(channelName);
            StreamInfoUpdated?.Invoke(this, streamInfo);
        }

        public string GetCurrentTwitchChannel()
        {
            var channel = _userSettings.CurrentTwitchChannel;
            return string.IsNullOrEmpty(channel) ? null : channel;
        }

        public void FetchQualitiesForChannel(string channelName)
        {
            _twitchService.FetchAndCacheQualitiesAsync(channelName);
        }

        public void ClearCachedQualities()
        {
            _twitchService.ClearCachedQualities();
        }

        private void OnTwitchQualitiesChanged(object sender, TwitchQualitiesEventArgs e)
        {
            QualitiesUpdated?.Invoke(this, e);
        }

        private void OnVideoQualitiesChanged(object sender, EventArgs e)
        {
            if (_videoPlayer == null)
                return;

            if (_userSettings.CurrentStreamSourceType == StreamSourceType.TwitchChannel && _twitchService.CachedQualities.Count > 0)
                return;

            var qualityNames = _videoPlayer.AvailableQualities.Select(q => q.Name).ToList();
            var eventArgs = new TwitchQualitiesEventArgs(qualityNames, _videoPlayer.SelectedQualityIndex);
            QualitiesUpdated?.Invoke(this, eventArgs);
        }

        private void HandleTwitchQualityChange(int qualityIndex)
        {
            var streamUrl = _twitchService.SelectQuality(qualityIndex);
            if (!string.IsNullOrEmpty(streamUrl))
            {
                _videoPlayer.Stop();
                _videoPlayer.Play(streamUrl);
            }
        }

        private void HandleVideoPlayerQualityChange(int qualityIndex)
        {
            _videoPlayer.SetQuality(qualityIndex);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _twitchService.QualitiesChanged -= OnTwitchQualitiesChanged;

            if (_videoPlayer != null)
            {
                _videoPlayer.QualitiesChanged -= OnVideoQualitiesChanged;
            }

            _isDisposed = true;
        }
    }
}
