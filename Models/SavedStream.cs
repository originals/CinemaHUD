using System;
using System.Collections.Generic;
using CinemaModule.Models.Location;
using Newtonsoft.Json;

namespace CinemaModule.Models
{
    public enum StreamSourceType
    {
        Url,
        TwitchChannel,
        YouTubeVideo,
        YouTubeChannel,
        YouTubePlaylist
    }

    public class SavedStream
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("sourceType")]
        public StreamSourceType SourceType { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("tabId")]
        public string TabId { get; set; }

        [JsonConstructor]
        public SavedStream() { }

        public SavedStream(string name, StreamSourceType sourceType, string value, string tabId = null)
        {
            Id = IdGenerator.Generate();
            CreatedAt = DateTime.UtcNow;
            Name = name;
            SourceType = sourceType;
            Value = value;
            TabId = tabId;
        }

        [JsonIgnore]
        public bool IsYouTubeChannelOrPlaylist => SourceType == StreamSourceType.YouTubeChannel || SourceType == StreamSourceType.YouTubePlaylist;
    }

    public class CustomStreamTab
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonConstructor]
        public CustomStreamTab() { }

        public CustomStreamTab(string name)
        {
            Id = IdGenerator.Generate();
            Name = name;
            CreatedAt = DateTime.UtcNow;
        }
    }

    public class SavedStreamCollection
    {
        [JsonProperty("streams")]
        public List<SavedStream> Streams { get; set; } = new List<SavedStream>();

        [JsonProperty("tabs")]
        public List<CustomStreamTab> Tabs { get; set; } = new List<CustomStreamTab>();
    }
}
