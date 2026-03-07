using System;

namespace CinemaModule.Models.WatchParty
{
    public enum WatchPartyStateChangeType
    {
        FullStateReceived,
        PlaybackUpdated,
        PlayStateChanged,
        QueueUpdated,
        VideoChanged,
        MemberTimesUpdated,
        MemberStatesUpdated
    }

    public sealed class WatchPartyStateArgs : EventArgs
    {
        public WatchPartyLocalState State { get; }
        public WatchPartyLocalState PreviousState { get; }
        public WatchPartyStateChangeType ChangeType { get; }

        public bool VideoChanged { get; }
        public bool PlayStateChanged { get; }
        public bool WaitingForReadyChanged { get; }
        public bool IsPlaybackRelated => ChangeType == WatchPartyStateChangeType.PlaybackUpdated ||
                                         ChangeType == WatchPartyStateChangeType.PlayStateChanged ||
                                         ChangeType == WatchPartyStateChangeType.VideoChanged;

        public WatchPartyStateArgs(
            WatchPartyLocalState state,
            WatchPartyLocalState previousState,
            WatchPartyStateChangeType changeType)
        {
            State = state;
            PreviousState = previousState;
            ChangeType = changeType;

            VideoChanged = state?.CurrentVideoId != previousState?.CurrentVideoId;
            PlayStateChanged = state?.IsPlaying != previousState?.IsPlaying;
            WaitingForReadyChanged = state?.IsWaitingForReady != previousState?.IsWaitingForReady;
        }
    }
}
