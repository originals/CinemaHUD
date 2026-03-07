using Newtonsoft.Json;

namespace CinemaModule.Models.WatchParty
{
    public enum MemberState
    {
        Idle = 0,
        Playing = 1,
        Paused = 2,
        Loading = 3
    }

    public enum ServerStatus
    {
        Unknown,
        Checking,
        Online,
        Offline,
        VersionMismatch
    }

    public class QueueItem
    {
        [JsonProperty("videoId")]
        public string VideoId { get; set; }

        [JsonProperty("addedBy")]
        public string AddedBy { get; set; }
    }

    public class SyncPayload
    {
        [JsonProperty("timestamp")]
        public double Timestamp { get; set; }

        [JsonProperty("isPlaying")]
        public bool IsPlaying { get; set; }

        [JsonProperty("sequenceNumber")]
        public long SequenceNumber { get; set; }
    }
}
