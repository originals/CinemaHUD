using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CinemaModule.Models
{
    public enum StreamSourceType
    {
        Url,
        TwitchChannel
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

        [JsonConstructor]
        public SavedStream() { }

        public SavedStream(string name, StreamSourceType sourceType, string value)
        {
            Id = IdGenerator.Generate();
            CreatedAt = DateTime.UtcNow;
            Name = name;
            SourceType = sourceType;
            Value = value;
        }
    }

    public class SavedStreamCollection
    {
        [JsonProperty("streams")]
        public List<SavedStream> Streams { get; set; } = new List<SavedStream>();
    }
}
