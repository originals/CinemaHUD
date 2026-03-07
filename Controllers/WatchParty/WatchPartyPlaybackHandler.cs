using System;
using System.Threading.Tasks;
using Blish_HUD;
using CinemaModule.Models.WatchParty;
using CinemaModule.Services.YouTube;
using CinemaModule.Settings;
using CinemaModule.VideoPlayer;

namespace CinemaModule.Controllers.WatchParty
{
    public sealed class WatchPartyPlaybackHandler : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<WatchPartyPlaybackHandler>();

        private const double ReportIntervalSeconds = 0.3;
        private const double LatencyMeasureIntervalSeconds = 30.0;
        private const int PostLoadSyncDelayMs = 5000;

        private readonly WatchPartyController _watchPartyController;
        private readonly PlaybackController _playbackController;
        private readonly DisplayController _displayController;
        private readonly CinemaUserSettings _userSettings;
        private readonly WatchPartyVideoLoader _videoLoader;
        private readonly WatchPartySyncManager _syncManager;
        private readonly object _videoStateLock = new object();

        private string _currentVideoId;
        private string _loadingVideoId;
        private string _pendingVideoId;
        private volatile bool _isCurrentVideoLiveStream;
        private DateTime _lastReportTime = DateTime.MinValue;
        private DateTime _lastLatencyMeasure = DateTime.MinValue;
        private volatile bool _isDisposed;

        public WatchPartyPlaybackHandler(
            WatchPartyController watchPartyController,
            PlaybackController playbackController,
            DisplayController displayController,
            YouTubeService youtubeService,
            CinemaUserSettings userSettings)
        {
            _watchPartyController = watchPartyController;
            _playbackController = playbackController;
            _displayController = displayController;
            _userSettings = userSettings;

            _videoLoader = new WatchPartyVideoLoader(
                playbackController, displayController, youtubeService, ReportMemberStateSafeAsync);
            _syncManager = new WatchPartySyncManager(playbackController, ReportMemberStateSafeAsync);

            SubscribeToEvents();
        }

        #region Event Subscriptions

        private void SubscribeToEvents()
        {
            _watchPartyController.RoomJoined += OnRoomJoined;
            _watchPartyController.StateChanged += OnStateChanged;
            _watchPartyController.RoomLeft += OnRoomLeft;
            _watchPartyController.HostStatusChanged += OnHostStatusChanged;
            _playbackController.PlaybackStateChanged += OnPlaybackStateChanged;
        }

        private void UnsubscribeFromEvents()
        {
            _watchPartyController.RoomJoined -= OnRoomJoined;
            _watchPartyController.StateChanged -= OnStateChanged;
            _watchPartyController.RoomLeft -= OnRoomLeft;
            _watchPartyController.HostStatusChanged -= OnHostStatusChanged;
            _playbackController.PlaybackStateChanged -= OnPlaybackStateChanged;
        }

        #endregion

        #region Event Handlers

        private void OnRoomJoined(object sender, EventArgs e)
        {
            Logger.Debug("Room joined - clearing selection and stopping playback");
            _userSettings.ClearStreamSelection();
            StopPlayback();
            _displayController.UpdateTwitchStreamState(false);
            _displayController.UpdateDisplayVisibility();
            _ = ReportMemberStateSafeAsync(MemberState.Idle);
        }

        private void OnRoomLeft(object sender, EventArgs e)
        {
            Logger.Debug("Room left - stopping playback");
            StopPlayback();
        }

        private void OnHostStatusChanged(object sender, bool isHost)
        {
            UpdateViewerState();
        }

        private void OnPlaybackStateChanged(object sender, PlaybackState playbackState)
        {
            if (_isDisposed || !_watchPartyController.IsInRoom || IsLoading())
                return;

            var memberState = ConvertToMemberState(playbackState);
            if (memberState.HasValue)
                _ = ReportMemberStateSafeAsync(memberState.Value);

            if (playbackState == PlaybackState.Ended && _watchPartyController.IsHost && _userSettings.WatchPartyAutoplayNext)
            {
                Logger.Debug("Video ended - triggering autoplay next");
                _ = _watchPartyController.PlayNextInQueueAsync();
            }
        }

        private void OnStateChanged(object sender, WatchPartyStateArgs e)
        {
            if (_isDisposed || !_watchPartyController.IsInRoom)
                return;

            UpdateViewerState();

            if (e.ChangeType == WatchPartyStateChangeType.FullStateReceived)
                ReportCurrentMemberState();

            var state = e.State;
            if (state == null || !state.HasVideo)
            {
                HandleNoVideo();
                return;
            }

            ProcessStateChange(state, e.ChangeType);
        }

        private void ProcessStateChange(WatchPartyLocalState state, WatchPartyStateChangeType changeType)
        {
            bool isViewer = !_watchPartyController.IsHost;
            bool isLoading = IsLoading();
            bool hasVideoLoaded = HasVideoLoaded(state.CurrentVideoId);

            if (changeType == WatchPartyStateChangeType.PlayStateChanged && isViewer && !isLoading && hasVideoLoaded)
            {
                _syncManager.SyncPlayPauseImmediate(state);
                return;
            }

            if (ShouldWaitForVideo(state, isViewer))
                return;

            if (NeedsVideoLoad(state.CurrentVideoId, isLoading))
            {
                Logger.Debug($"Loading video {state.CurrentVideoId}");
                _syncManager.ResetServerTimeTracking();
                _ = LoadAndPlayVideoAsync(state);
                return;
            }

            if (!isLoading && isViewer && hasVideoLoaded)
                SyncToHost(state, changeType);
        }

        #endregion

        #region Public Methods

        public void Update()
        {
            if (_isDisposed || !_watchPartyController.IsInRoom || IsLoading())
                return;

            MeasureLatencyIfNeeded();

            bool hasActivePlayback = _playbackController.IsPlaying || _playbackController.IsPaused;
            if (!hasActivePlayback)
                return;

            if ((DateTime.UtcNow - _lastReportTime).TotalSeconds < ReportIntervalSeconds)
                return;

            _lastReportTime = DateTime.UtcNow;
            _ = ReportPlaybackTimeSafeAsync(GetCurrentTimeSeconds());
        }

        public void HandleScreenEnabled()
        {
            if (_isDisposed || !_watchPartyController.IsInRoom)
                return;

            Logger.Debug("Screen re-enabled - requesting state to resume playback");
            _displayController.UpdateTwitchStreamState(false);
            _displayController.UpdateDisplayVisibility();

            ClearVideoState();
            _ = _watchPartyController.RequestStateAsync();
        }

        public void ForceResync()
        {
            if (_isDisposed || !_watchPartyController.IsInRoom || _watchPartyController.IsHost)
                return;

            Logger.Debug("Force resync requested");
            ClearVideoState();
            _playbackController.Stop();
            _displayController.ClearVideoTexture();
            _displayController.UpdateOfflineState(true);
            _ = ReportMemberStateSafeAsync(MemberState.Idle);
            _ = _watchPartyController.RequestStateAsync();
        }

        public void HandleQualityChanged()
        {
            if (_isDisposed || !_watchPartyController.IsInRoom || _watchPartyController.IsHost)
                return;

            Logger.Debug("Quality changed - requesting state sync");
            _ = SyncAfterQualityChangeAsync();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;

            _syncManager.Dispose();
            UnsubscribeFromEvents();
        }

        #endregion

        #region Video State Management

        private void ClearVideoState()
        {
            lock (_videoStateLock)
            {
                _currentVideoId = null;
                _loadingVideoId = null;
                _pendingVideoId = null;
            }
        }

        private bool HasVideoLoaded(string videoId)
        {
            lock (_videoStateLock)
            {
                return _currentVideoId == videoId;
            }
        }

        private bool IsLoading()
        {
            lock (_videoStateLock)
            {
                return _loadingVideoId != null;
            }
        }

        private bool NeedsVideoLoad(string videoId, bool isLoading)
        {
            if (isLoading)
                return false;

            lock (_videoStateLock)
            {
                if (videoId == _currentVideoId || videoId == _pendingVideoId)
                    return false;

                _loadingVideoId = videoId;
                return true;
            }
        }

        private bool ShouldWaitForVideo(WatchPartyLocalState state, bool isViewer)
        {
            if (!isViewer)
                return false;

            string pendingId;
            lock (_videoStateLock)
            {
                pendingId = _pendingVideoId;
            }

            if (pendingId != state.CurrentVideoId)
                return false;

            bool canProceed = state.IsPlaying || state.IsHostReady();
            if (canProceed)
            {
                Logger.Debug($"Ready to load pending video {state.CurrentVideoId}");
                lock (_videoStateLock)
                {
                    _pendingVideoId = null;
                }
                _ = LoadAndPlayVideoAsync(state);
            }

            return true;
        }

        private void HandleNoVideo()
        {
            bool hadVideo;
            lock (_videoStateLock)
            {
                hadVideo = _currentVideoId != null || _pendingVideoId != null;
            }

            if (hadVideo)
            {
                Logger.Debug("No current video - stopping playback");
                StopPlayback();
            }
        }

        #endregion

        #region Playback Control

        private void StopPlayback()
        {
            ClearVideoState();
            _isCurrentVideoLiveStream = false;
            _syncManager.ResetServerTimeTracking();
            _syncManager.ResetSeekState();
            _playbackController.Stop();
            _displayController.ClearVideoTexture();
            _displayController.UpdateOfflineState(true);
            _displayController.UpdateOfflineTexture(null);
            _displayController.UpdateStreamInfo(null, null, null);
            _displayController.UpdateWatchPartyViewerState(false);
        }

        private async Task LoadAndPlayVideoAsync(WatchPartyLocalState state)
        {
            if (_isDisposed) return;

            var videoId = state.CurrentVideoId;
            bool isViewer = !_watchPartyController.IsHost;

            if (isViewer && !state.IsPlaying && !state.IsHostReady())
            {
                Logger.Debug($"Waiting for host before loading {videoId}");
                lock (_videoStateLock)
                {
                    _pendingVideoId = videoId;
                    _loadingVideoId = null;
                }
                _ = ReportMemberStateSafeAsync(MemberState.Idle);
                _ = _videoLoader.LoadThumbnailAsync(videoId);
                _ = _videoLoader.LoadVideoInfoAsync(videoId);
                return;
            }

            try
            {
                var result = await _videoLoader.LoadVideoAsync(videoId, ValidateLoadingState).ConfigureAwait(false);

                if (_isDisposed || result.IsCancelled)
                    return;

                if (!result.IsSuccess)
                {
                    HandleLoadFailure(videoId);
                    return;
                }

                await StartPlaybackAsync(videoId, result.StreamUrl, result.IsLiveStream, state).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!_isDisposed)
                {
                    Logger.Error(ex, $"Exception during video load for {videoId}");
                    HandleLoadFailure(videoId);
                }
            }
        }

        private async Task StartPlaybackAsync(string videoId, string streamUrl, bool isLiveStream, WatchPartyLocalState state)
        {
            if (_isDisposed) return;

            Logger.Debug($"Playing video {videoId} (livestream: {isLiveStream})");
            _videoLoader.StartPlayback(streamUrl, isLiveStream);
            _isCurrentVideoLiveStream = isLiveStream;

            lock (_videoStateLock)
            {
                _currentVideoId = videoId;
                _loadingVideoId = null;
            }

            _syncManager.SetVideoLoadTime();

            if (!isLiveStream)
                _videoLoader.FetchQualities(videoId);

            var currentState = _watchPartyController.CurrentState;
            if (currentState != null && !_watchPartyController.IsHost && !isLiveStream && !currentState.IsPlaying)
            {
                _playbackController.Pause();
                _ = ReportMemberStateSafeAsync(MemberState.Paused);
            }
            else
            {
                _ = ReportMemberStateSafeAsync(MemberState.Playing);
                if (_watchPartyController.IsHost)
                    _ = ReportPlaybackTimeSafeAsync(0);
            }

            _ = RequestFreshStateAfterDelayAsync();
        }

        private bool ValidateLoadingState(string expectedVideoId)
        {
            lock (_videoStateLock)
            {
                if (_loadingVideoId != expectedVideoId)
                {
                    Logger.Debug($"Video load cancelled - different video now loading: {_loadingVideoId}");
                    return false;
                }
                return true;
            }
        }

        private void HandleLoadFailure(string videoId)
        {
            Logger.Warn($"Video load failed for {videoId}");
            lock (_videoStateLock)
            {
                if (_loadingVideoId == videoId)
                    _loadingVideoId = null;
            }
            _ = ReportMemberStateSafeAsync(MemberState.Idle);
        }

        #endregion

        #region Synchronization

        private void SyncToHost(WatchPartyLocalState state, WatchPartyStateChangeType changeType)
        {
            bool hasPlaybackUpdate = changeType == WatchPartyStateChangeType.PlaybackUpdated ||
                                     changeType == WatchPartyStateChangeType.PlayStateChanged ||
                                     changeType == WatchPartyStateChangeType.FullStateReceived;

            if (!hasPlaybackUpdate)
                return;

            bool hostSeeked = _syncManager.DetectHostSeek(state);
            if (hostSeeked)
                _syncManager.ForceSyncPlayback(state, _isCurrentVideoLiveStream);
            else
                _syncManager.SyncPlayback(state, _isCurrentVideoLiveStream);
        }

        private async Task SyncAfterQualityChangeAsync()
        {
            await Task.Delay(200).ConfigureAwait(false);

            if (_isDisposed || !_watchPartyController.IsInRoom || _watchPartyController.IsHost)
                return;

            await _watchPartyController.RequestStateAsync().ConfigureAwait(false);

            await Task.Delay(300).ConfigureAwait(false);

            if (_isDisposed || IsLoading())
                return;

            var currentState = _watchPartyController.CurrentState;
            if (currentState != null)
                _syncManager.ForceSyncPlayback(currentState, _isCurrentVideoLiveStream);
        }

        #endregion

        #region Helper Methods

        private MemberState? ConvertToMemberState(PlaybackState playbackState)
        {
            switch (playbackState)
            {
                case PlaybackState.Playing:
                    return MemberState.Playing;
                case PlaybackState.Paused:
                    return MemberState.Paused;
                case PlaybackState.Stopped:
                case PlaybackState.Ended:
                case PlaybackState.Error:
                    return MemberState.Idle;
                default:
                    return null;
            }
        }

        private void ReportCurrentMemberState()
        {
            PlaybackState playbackState;
            if (_playbackController.IsPlaying)
                playbackState = PlaybackState.Playing;
            else if (_playbackController.IsPaused)
                playbackState = PlaybackState.Paused;
            else
                playbackState = PlaybackState.Stopped;

            var memberState = ConvertToMemberState(playbackState);
            if (memberState.HasValue)
                _ = ReportMemberStateSafeAsync(memberState.Value);
        }

        private void UpdateViewerState()
        {
            bool isViewer = _watchPartyController.IsInRoom && !_watchPartyController.IsHost;
            _displayController.UpdateWatchPartyViewerState(isViewer);
        }

        private void MeasureLatencyIfNeeded()
        {
            if ((DateTime.UtcNow - _lastLatencyMeasure).TotalSeconds < LatencyMeasureIntervalSeconds)
                return;

            _lastLatencyMeasure = DateTime.UtcNow;
            _ = _watchPartyController.MeasureLatencyAsync();
        }

        private double GetCurrentTimeSeconds()
        {
            long durationMs = _playbackController.Duration;
            if (durationMs <= 0)
                return 0;

            return _playbackController.Position * (durationMs / 1000.0);
        }

        #endregion

        #region Async Helpers

        private async Task ReportPlaybackTimeSafeAsync(double currentTime)
        {
            if (_isDisposed) return;
            try
            {
                await _watchPartyController.ReportPlaybackTimeAsync(currentTime).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!_isDisposed)
                    Logger.Warn(ex, $"Failed to report playback time: {currentTime:F1}s");
            }
        }

        private async Task ReportMemberStateSafeAsync(MemberState state)
        {
            if (_isDisposed) return;
            try
            {
                await _watchPartyController.ReportMemberStateAsync(state).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!_isDisposed)
                    Logger.Warn(ex, $"Failed to report member state: {state}");
            }
        }

        private async Task RequestFreshStateAfterDelayAsync()
        {
            await Task.Delay(PostLoadSyncDelayMs).ConfigureAwait(false);

            bool shouldSkip;
            lock (_videoStateLock)
            {
                shouldSkip = _isDisposed || _loadingVideoId != null || _currentVideoId == null;
            }

            if (shouldSkip)
                return;

            try
            {
                await _watchPartyController.RequestStateAsync().ConfigureAwait(false);

                await Task.Delay(500).ConfigureAwait(false);

                if (_isDisposed || IsLoading())
                    return;

                var currentState = _watchPartyController.CurrentState;
                if (currentState != null && !_watchPartyController.IsHost && !_isCurrentVideoLiveStream)
                    _syncManager.SyncPlayback(currentState, _isCurrentVideoLiveStream);
            }
            catch (Exception ex)
            {
                if (!_isDisposed)
                    Logger.Warn(ex, "Failed to request fresh state after video load");
            }
        }

        #endregion
    }
}
