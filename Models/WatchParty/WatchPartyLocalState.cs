using System;
using System.Collections.Generic;

namespace CinemaModule.Models.WatchParty
{
    public sealed class WatchPartyLocalState
    {
        private static readonly IReadOnlyList<QueueItem> EmptyQueue = Array.AsReadOnly(new QueueItem[0]);
        private static readonly IReadOnlyList<string> EmptyMembers = Array.AsReadOnly(new string[0]);
        private static readonly IReadOnlyDictionary<string, double> EmptyMemberTimes = new Dictionary<string, double>();
        private static readonly IReadOnlyDictionary<string, MemberState> EmptyMemberStates = new Dictionary<string, MemberState>();

        public string RoomId { get; }
        public string RoomName { get; }
        public string Description { get; }
        public string HostUsername { get; }
        public WatchPartySharedLocation SharedLocation { get; }
        public string CurrentVideoId { get; }
        public double CurrentTime { get; }
        public bool IsPlaying { get; }
        public bool IsWaitingForReady { get; }
        public long SequenceNumber { get; }
        public IReadOnlyList<QueueItem> Queue { get; }
        public IReadOnlyList<string> Members { get; }
        public IReadOnlyDictionary<string, double> MemberTimes { get; }
        public IReadOnlyDictionary<string, MemberState> MemberStates { get; }
        public int MaxQueuePerUser { get; }

        public bool HasVideo => !string.IsNullOrEmpty(CurrentVideoId);

        private WatchPartyLocalState(
            string roomId,
            string roomName,
            string description,
            string hostUsername,
            WatchPartySharedLocation sharedLocation,
            string currentVideoId,
            double currentTime,
            bool isPlaying,
            bool isWaitingForReady,
            long sequenceNumber,
            IReadOnlyList<QueueItem> queue,
            IReadOnlyList<string> members,
            IReadOnlyDictionary<string, double> memberTimes,
            IReadOnlyDictionary<string, MemberState> memberStates,
            int maxQueuePerUser)
        {
            RoomId = roomId;
            RoomName = roomName;
            Description = description;
            HostUsername = hostUsername;
            SharedLocation = sharedLocation;
            CurrentVideoId = currentVideoId;
            CurrentTime = currentTime;
            IsPlaying = isPlaying;
            IsWaitingForReady = isWaitingForReady;
            SequenceNumber = sequenceNumber;
            Queue = queue ?? EmptyQueue;
            Members = members ?? EmptyMembers;
            MemberTimes = memberTimes ?? EmptyMemberTimes;
            MemberStates = memberStates ?? EmptyMemberStates;
            MaxQueuePerUser = maxQueuePerUser;
        }

        public static WatchPartyLocalState FromServerState(WatchPartyState serverState)
        {
            if (serverState == null)
                throw new ArgumentNullException(nameof(serverState));

            return new WatchPartyLocalState(
                serverState.RoomId,
                serverState.RoomName,
                serverState.Description,
                serverState.HostUsername,
                serverState.SharedLocation,
                serverState.CurrentVideoId,
                serverState.CurrentTime,
                serverState.IsPlaying,
                serverState.IsWaitingForReady,
                serverState.SequenceNumber,
                serverState.Queue,
                serverState.Members,
                serverState.MemberTimes,
                serverState.MemberStates,
                serverState.MaxQueuePerUser);
        }

        public WatchPartyLocalState WithPlaybackUpdate(double timestamp, bool isPlaying, bool clearWaitingForReady, long sequenceNumber)
        {
            return new WatchPartyLocalState(
                RoomId, RoomName, Description, HostUsername, SharedLocation,
                CurrentVideoId,
                timestamp,
                isPlaying,
                clearWaitingForReady ? false : IsWaitingForReady,
                sequenceNumber,
                Queue, Members, MemberTimes, MemberStates, MaxQueuePerUser);
        }

        public WatchPartyLocalState WithPlayStateUpdate(bool isPlaying, long sequenceNumber)
        {
            return new WatchPartyLocalState(
                RoomId, RoomName, Description, HostUsername, SharedLocation,
                CurrentVideoId,
                CurrentTime,
                isPlaying,
                IsWaitingForReady,
                sequenceNumber,
                Queue, Members, MemberTimes, MemberStates, MaxQueuePerUser);
        }

        public WatchPartyLocalState WithQueue(IReadOnlyList<QueueItem> queue)
        {
            return new WatchPartyLocalState(
                RoomId, RoomName, Description, HostUsername, SharedLocation,
                CurrentVideoId, CurrentTime, IsPlaying, IsWaitingForReady, SequenceNumber,
                queue, Members, MemberTimes, MemberStates, MaxQueuePerUser);
        }

        public WatchPartyLocalState WithNewVideo(string videoId)
        {
            return new WatchPartyLocalState(
                RoomId, RoomName, Description, HostUsername, SharedLocation,
                videoId,
                0,
                false,
                true,
                SequenceNumber,
                Queue, Members, EmptyMemberTimes, EmptyMemberStates, MaxQueuePerUser);
        }

        public WatchPartyLocalState WithMemberTimes(IReadOnlyDictionary<string, double> newMemberTimes)
        {
            return new WatchPartyLocalState(
                RoomId, RoomName, Description, HostUsername, SharedLocation,
                CurrentVideoId, CurrentTime, IsPlaying, IsWaitingForReady, SequenceNumber,
                Queue, Members, newMemberTimes, MemberStates, MaxQueuePerUser);
        }

        public WatchPartyLocalState WithMemberStates(IReadOnlyDictionary<string, MemberState> newMemberStates)
        {
            return new WatchPartyLocalState(
                RoomId, RoomName, Description, HostUsername, SharedLocation,
                CurrentVideoId, CurrentTime, IsPlaying, IsWaitingForReady, SequenceNumber,
                Queue, Members, MemberTimes, newMemberStates, MaxQueuePerUser);
        }

        public MemberState? GetHostState()
        {
            if (string.IsNullOrEmpty(HostUsername))
                return null;

            var hostKey = HostUsername.ToLowerInvariant();
            return MemberStates.TryGetValue(hostKey, out var hostState) ? hostState : (MemberState?)null;
        }

        public bool IsHostLoading() => GetHostState() == MemberState.Loading;

        public bool IsHostIdle() => GetHostState() == MemberState.Idle;

        public bool IsHostReady() => GetHostState() == MemberState.Playing || GetHostState() == MemberState.Paused;

        public bool HasPlaybackDifference(WatchPartyLocalState other)
        {
            if (other == null) return true;
            return CurrentVideoId != other.CurrentVideoId ||
                   IsPlaying != other.IsPlaying ||
                   IsWaitingForReady != other.IsWaitingForReady ||
                   Math.Abs(CurrentTime - other.CurrentTime) > 0.5;
        }

        public bool HasMembershipDifference(WatchPartyLocalState other)
        {
            if (other == null) return true;
            if (Members.Count != other.Members.Count) return true;
            if (Queue.Count != other.Queue.Count) return true;
            return HostUsername != other.HostUsername ||
                   RoomName != other.RoomName ||
                   Description != other.Description;
        }
    }
}
