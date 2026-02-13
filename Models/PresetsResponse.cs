using Blish_HUD.Content;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace CinemaModule.Models
{
    public class PresetsResponse
    {
        [JsonProperty("worldLocations")]
        public List<WorldLocationPresetData> WorldLocations { get; set; } = new List<WorldLocationPresetData>();

        [JsonProperty("streams")]
        public List<StreamPresetData> Streams { get; set; } = new List<StreamPresetData>();

        [JsonProperty("twitchChannels")]
        public List<string> TwitchChannels { get; set; } = new List<string>();
    }

    public class StreamPresetData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("infoUrl")]
        public string InfoUrl { get; set; }
    }

    public class WorldLocationPresetData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("waypoint")]
        public string Waypoint { get; set; }

        [JsonProperty("picture")]
        public string Picture { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("position")]
        public WorldPosition3D Position { get; set; }

        [JsonProperty("screenWidth")]
        public float ScreenWidth { get; set; } = 10f;


        [JsonIgnore]
        public AsyncTexture2D AvatarTexture { get; set; }

        [JsonIgnore]
        public AsyncTexture2D PictureTexture { get; set; }
    }
}
