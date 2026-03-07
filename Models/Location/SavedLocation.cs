using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CinemaModule.Models.Location
{
    public static class IdGenerator
    {
        private const int DefaultIdLength = 8;

        public static string Generate(int length = DefaultIdLength) 
            => Guid.NewGuid().ToString("N").Substring(0, length);
    }

    public class SavedLocation
    {
        private const float DefaultScreenWidth = 10f;

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("position")]
        public WorldPosition3D Position { get; set; }

        [JsonProperty("screenWidth")]
        public float ScreenWidth { get; set; } = DefaultScreenWidth;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonConstructor]
        public SavedLocation() { }

        public SavedLocation(string name, WorldPosition3D position, float screenWidth)
        {
            Id = IdGenerator.Generate();
            CreatedAt = DateTime.UtcNow;
            Name = name;
            Position = position;
            ScreenWidth = screenWidth;
        }
    }

    public class SavedLocationCollection
    {
        [JsonProperty("locations")]
        public List<SavedLocation> Locations { get; set; } = new List<SavedLocation>();
    }
}
