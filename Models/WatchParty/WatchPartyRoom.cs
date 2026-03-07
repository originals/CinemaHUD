using Newtonsoft.Json;

namespace CinemaModule.Models.WatchParty
{
    public class WatchPartyRoom
    {
        [JsonProperty("roomId")]
        public string RoomId { get; set; }

        [JsonProperty("roomName")]
        public string RoomName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("hostUsername")]
        public string HostUsername { get; set; }

        [JsonProperty("isPrivate")]
        public bool IsPrivate { get; set; }

        [JsonProperty("sharedLocation")]
        public WatchPartySharedLocation SharedLocation { get; set; }

        [JsonProperty("memberCount")]
        public int MemberCount { get; set; }
    }
}
