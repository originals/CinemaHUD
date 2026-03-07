using System.Collections.Generic;
using Newtonsoft.Json;

namespace CinemaModule.Models.WatchParty
{
    public class WatchPartyState
    {
        [JsonProperty("roomId")]
        public string RoomId { get; set; }

        [JsonProperty("roomName")]
        public string RoomName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("hostUsername")]
        public string HostUsername { get; set; }

        [JsonProperty("sharedLocation")]
        public WatchPartySharedLocation SharedLocation { get; set; }

        [JsonProperty("currentVideoId")]
        public string CurrentVideoId { get; set; }

        [JsonProperty("currentTime")]
        public double CurrentTime { get; set; }

        [JsonProperty("isPlaying")]
        public bool IsPlaying { get; set; }

        [JsonProperty("isWaitingForReady")]
        public bool IsWaitingForReady { get; set; }

        [JsonProperty("sequenceNumber")]
        public long SequenceNumber { get; set; }

        [JsonProperty("queue")]
        public List<QueueItem> Queue { get; set; } = new List<QueueItem>();

        [JsonProperty("members")]
        public List<string> Members { get; set; } = new List<string>();

        [JsonProperty("memberTimes")]
        public Dictionary<string, double> MemberTimes { get; set; } = new Dictionary<string, double>();

        [JsonProperty("memberStates")]
        public Dictionary<string, MemberState> MemberStates { get; set; } = new Dictionary<string, MemberState>();

        [JsonProperty("maxQueuePerUser")]
        public int MaxQueuePerUser { get; set; }
    }
}
