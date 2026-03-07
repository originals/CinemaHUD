using System;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD;
using CinemaModule.Models.WatchParty;

namespace CinemaModule.Controllers.WatchParty
{
    public sealed class WatchPartySyncManager : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<WatchPartySyncManager>();

        private const double HostSeekDetectionThreshold = 2.0;
        private const double SeekThresholdSeconds = 3.0;
        private const double LargeDriftThreshold = 10.0;
        private const double MediumDriftThreshold = 5.0;
        private const double BaseSeekCooldownSeconds = 1.0;
        private const double PostLoadSeekCooldownSeconds = 3.0;
        private const double LargeDriftSeekCooldownSeconds = 0.5;
        private const double ForcedSeekCooldownSeconds = 0.3;
        private const int SeekDebounceMs = 150;
        private const double PendingSeekTimeoutSeconds = 3.0;
        private const double SeekSettleThresholdSeconds = 3.0;
        private const double SeekFailureDriftThreshold = 20.0;

        private readonly PlaybackController _playbackController;
        private readonly Func<MemberState, Task> _reportMemberState;
        private readonly object _seekStateLock = new object();
        private readonly object _pendingSyncLock = new object();

        private DateTime _lastSeekTime = DateTime.MinValue;
        private DateTime _lastForcedSeekTime = DateTime.MinValue;
        private DateTime _videoLoadTime = DateTime.MinValue;
        private double _pendingSeekTarget = -1;
        private DateTime _pendingSeekTime = DateTime.MinValue;
        private double _lastServerTime = -1;
        private DateTime _lastServerTimeReceived = DateTime.MinValue;
        private WatchPartyLocalState _pendingSyncState;
        private CancellationTokenSource _syncDebounceCts;
        private volatile bool _isDisposed;

        public WatchPartySyncManager(
            PlaybackController playbackController,
            Func<MemberState, Task> reportMemberState)
        {
            _playbackController = playbackController;
            _reportMemberState = reportMemberState;
        }

        public void ResetServerTimeTracking()
        {
            _lastServerTime = -1;
        }

        public void SetVideoLoadTime()
        {
            _videoLoadTime = DateTime.UtcNow;
        }

        public void ResetSeekState()
        {
            lock (_seekStateLock)
            {
                _pendingSeekTarget = -1;
            }
        }

        public bool DetectHostSeek(WatchPartyLocalState state)
        {
            var now = DateTime.UtcNow;
            double newServerTime = state.CurrentTime;

            if (_lastServerTime < 0)
            {
                _lastServerTime = newServerTime;
                _lastServerTimeReceived = now;
                return false;
            }

            double elapsed = (now - _lastServerTimeReceived).TotalSeconds;
            double expectedTime = _lastServerTime + elapsed;
            double serverDrift = Math.Abs(newServerTime - expectedTime);

            _lastServerTime = newServerTime;
            _lastServerTimeReceived = now;

            return serverDrift >= HostSeekDetectionThreshold;
        }

        public void SyncPlayback(WatchPartyLocalState state, bool isLiveStream)
        {
            SyncSeekPosition(state, isLiveStream, forceSeek: false);
            SyncPlayPause(state);
        }

        public void ForceSyncPlayback(WatchPartyLocalState state, bool isLiveStream)
        {
            CancellationToken token;
            lock (_pendingSyncLock)
            {
                _pendingSyncState = state;
                _syncDebounceCts?.Cancel();
                _syncDebounceCts = new CancellationTokenSource();
                token = _syncDebounceCts.Token;
            }

            _ = DebouncedForceSyncAsync(isLiveStream, token);
        }

        public void SyncPlayPauseImmediate(WatchPartyLocalState state)
        {
            bool isPlaying = _playbackController.IsPlaying;
            bool isPaused = _playbackController.IsPaused;
            bool isBuffering = _playbackController.IsBuffering;

            if (state.IsPlaying && (isPaused || isBuffering))
            {
                _playbackController.Resume();
                _ = _reportMemberState(MemberState.Playing);
            }
            else if (!state.IsPlaying && (isPlaying || isBuffering))
            {
                _playbackController.Pause();
                _ = _reportMemberState(MemberState.Paused);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            lock (_pendingSyncLock)
            {
                _syncDebounceCts?.Cancel();
                _syncDebounceCts?.Dispose();
                _syncDebounceCts = null;
            }
        }

        #region Private Methods

        private async Task DebouncedForceSyncAsync(bool isLiveStream, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(SeekDebounceMs, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (_isDisposed || cancellationToken.IsCancellationRequested)
                return;

            WatchPartyLocalState stateToSync;
            lock (_pendingSyncLock)
            {
                stateToSync = _pendingSyncState;
                _pendingSyncState = null;
            }

            if (stateToSync == null)
                return;

            SyncSeekPosition(stateToSync, isLiveStream, forceSeek: true);
            SyncPlayPause(stateToSync);
        }

        private void SyncSeekPosition(WatchPartyLocalState state, bool isLiveStream, bool forceSeek)
        {
            if (isLiveStream)
                return;

            long durationMs = _playbackController.Duration;
            if (durationMs <= 0)
                return;

            double durationSeconds = durationMs / 1000.0;
            double targetTime = state.CurrentTime;

            if (targetTime < 0)
                targetTime = 0;
            if (targetTime > durationSeconds)
            {
                Logger.Warn($"Invalid sync time {targetTime}s for video duration {durationSeconds}s - skipping seek");
                return;
            }

            double currentSeconds = _playbackController.Position * durationSeconds;
            double drift = Math.Abs(currentSeconds - targetTime);
            var now = DateTime.UtcNow;

            if (IsPendingSeekInProgress(currentSeconds, now))
                return;

            if (!ShouldSeek(forceSeek, drift, now))
                return;

            ExecuteSeek(targetTime, durationSeconds, currentSeconds, drift, forceSeek, now);
        }

        private bool IsPendingSeekInProgress(double currentSeconds, DateTime now)
        {
            lock (_seekStateLock)
            {
                if (_pendingSeekTarget < 0)
                    return false;

                double pendingElapsed = (now - _pendingSeekTime).TotalSeconds;
                double pendingDrift = Math.Abs(currentSeconds - _pendingSeekTarget);

                if (pendingDrift < SeekSettleThresholdSeconds)
                {
                    _pendingSeekTarget = -1;
                    return false;
                }

                bool hasMinimumWaitPassed = pendingElapsed >= 1.0;
                bool seekClearlyFailed = hasMinimumWaitPassed && pendingDrift > SeekFailureDriftThreshold;

                if (seekClearlyFailed)
                {
                    Logger.Debug($"Seek failed - drift {pendingDrift:F1}s after {pendingElapsed:F1}s");
                    _pendingSeekTarget = -1;
                    return false;
                }

                if (pendingElapsed < PendingSeekTimeoutSeconds)
                    return true;

                Logger.Debug($"Pending seek timed out after {pendingElapsed:F1}s");
                _pendingSeekTarget = -1;
                return false;
            }
        }

        private bool ShouldSeek(bool forceSeek, double drift, DateTime now)
        {
            if (drift < HostSeekDetectionThreshold)
                return false;

            if (forceSeek)
                return (now - _lastForcedSeekTime).TotalSeconds >= ForcedSeekCooldownSeconds;

            if (drift <= SeekThresholdSeconds)
                return false;

            double cooldownSeconds = GetAdaptiveCooldown(drift, now);
            return (now - _lastSeekTime).TotalSeconds >= cooldownSeconds;
        }

        private double GetAdaptiveCooldown(double drift, DateTime now)
        {
            double timeSinceLoad = (now - _videoLoadTime).TotalSeconds;
            if (timeSinceLoad < 5.0)
                return PostLoadSeekCooldownSeconds;

            if (drift > LargeDriftThreshold)
                return LargeDriftSeekCooldownSeconds;

            if (drift > MediumDriftThreshold)
                return BaseSeekCooldownSeconds;

            return BaseSeekCooldownSeconds * 2.0;
        }

        private void ExecuteSeek(double targetTime, double durationSeconds, double currentSeconds, double drift, bool forceSeek, DateTime now)
        {
            Logger.Debug($"Seeking {currentSeconds:F1}s -> {targetTime:F1}s (drift: {drift:F1}s)");

            float targetPosition = (float)(targetTime / durationSeconds);
            _playbackController.Seek(targetPosition);

            _lastSeekTime = now;
            if (forceSeek)
                _lastForcedSeekTime = now;

            lock (_seekStateLock)
            {
                _pendingSeekTarget = targetTime;
                _pendingSeekTime = now;
            }
        }

        private void SyncPlayPause(WatchPartyLocalState state)
        {
            if (_playbackController.IsBuffering)
                return;

            bool isPlaying = _playbackController.IsPlaying;
            bool isPaused = _playbackController.IsPaused;

            if (state.IsPlaying && isPaused)
            {
                _playbackController.Resume();
                _ = _reportMemberState(MemberState.Playing);
            }
            else if (!state.IsPlaying && isPlaying)
            {
                _playbackController.Pause();
                _ = _reportMemberState(MemberState.Paused);
            }
        }

        #endregion
    }
}
