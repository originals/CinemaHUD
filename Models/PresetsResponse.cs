using Blish_HUD.Content;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace CinemaModule.Models
{
    public interface IStreamData
    {
        string Id { get; }
        string Name { get; }
        string TypeString { get; }
        string Url { get; }
        string InfoUrl { get; }
        string StaticImage { get; }
        StreamType Type { get; }
        bool IsRadio { get; }
        AsyncTexture2D StaticImageTexture { get; set; }
    }

    public class PresetsResponse
    {
        [JsonProperty("worldLocations")]
        public List<WorldLocationPresetData> WorldLocations { get; set; } = new List<WorldLocationPresetData>();

        [JsonProperty("streams")]
        public List<StreamCategory> StreamCategories { get; set; } = new List<StreamCategory>();
    }

    public enum StreamCategoryType
    {
        Stream,
        Radio,
        Twitch
    }

    public enum StreamType
    {
        Video,
        Radio
    }

    public static class StreamTypeExtensions
    {
        private const string RadioType = "radio";

        public static StreamType ParseFromString(string typeString)
        {
            return string.Equals(typeString, RadioType, System.StringComparison.OrdinalIgnoreCase)
                ? StreamType.Radio
                : StreamType.Video;
        }
    }

    public class StreamCategory
    {
        private const string RadioTypeString = "radio";
        private const string TwitchTypeString = "twitch";

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("infoUrl")]
        public string InfoUrl { get; set; }

        [JsonProperty("type")]
        public string TypeString { get; set; } = "stream";

        [JsonProperty("channels")]
        public JToken ChannelsRaw { get; set; }

        [JsonIgnore]
        public StreamCategoryType CategoryType
        {
            get
            {
                var type = TypeString?.ToLowerInvariant();
                if (type == RadioTypeString) return StreamCategoryType.Radio;
                if (type == TwitchTypeString) return StreamCategoryType.Twitch;
                return StreamCategoryType.Stream;
            }
        }

        [JsonIgnore]
        public bool IsTwitch => CategoryType == StreamCategoryType.Twitch;

        [JsonIgnore]
        public List<ChannelData> Channels { get; set; } = new List<ChannelData>();

        [JsonIgnore]
        public List<string> TwitchChannelNames { get; set; } = new List<string>();

        [JsonIgnore]
        public AsyncTexture2D IconTexture { get; set; }

        public void ParseChannels()
        {
            if (ChannelsRaw == null) return;

            if (IsTwitch)
            {
                TwitchChannelNames = ChannelsRaw.ToObject<List<string>>() ?? new List<string>();
            }
            else
            {
                Channels = ChannelsRaw.ToObject<List<ChannelData>>() ?? new List<ChannelData>();
            }
        }
    }

    public abstract class StreamDataBase : IStreamData
    {
        public abstract string Id { get; set; }
        public abstract string Name { get; }
        public abstract string TypeString { get; set; }
        public abstract string Url { get; set; }
        public abstract string InfoUrl { get; set; }
        public abstract string StaticImage { get; set; }

        [JsonIgnore]
        public StreamType Type => StreamTypeExtensions.ParseFromString(TypeString);

        [JsonIgnore]
        public bool IsRadio => Type == StreamType.Radio;

        [JsonIgnore]
        public AsyncTexture2D StaticImageTexture { get; set; }
    }

    public class ChannelData : StreamDataBase
    {
        private static readonly string[] OnDemandExtensions = { ".mp4", ".webm", ".mkv", ".avi", ".mov" };
        private static readonly string[] OnDemandPathSegments = { "/vod/", "/video/" };

        [JsonProperty("id")]
        public override string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonIgnore]
        public override string Name => Title;

        [JsonProperty("type")]
        public override string TypeString { get; set; } = "video";

        [JsonProperty("url")]
        public override string Url { get; set; }

        [JsonProperty("infoUrl")]
        public override string InfoUrl { get; set; }

        [JsonProperty("youtubeUrl")]
        public string YoutubeUrl { get; set; }

        [JsonProperty("waypoint")]
        public string Waypoint { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("staticImage")]
        public override string StaticImage { get; set; }

        [JsonProperty("position")]
        public WorldPosition3D Position { get; set; }

        [JsonProperty("screenWidth")]
        public float? ScreenWidth { get; set; }

        [JsonIgnore]
        public bool HasWorldPosition => Position != null && ScreenWidth.HasValue && ScreenWidth.Value > 0;

        [JsonIgnore]
        public bool IsOnDemand => IsOnDemandUrl(Url);

        private static bool IsOnDemandUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            var lower = url.ToLowerInvariant();

            foreach (var ext in OnDemandExtensions)
                if (lower.EndsWith(ext)) return true;

            foreach (var segment in OnDemandPathSegments)
                if (lower.Contains(segment)) return true;

            return false;
        }

        [JsonIgnore]
        public AsyncTexture2D AvatarTexture { get; set; }

        public StreamPresetData ToStreamPresetData()
        {
            return new StreamPresetData
            {
                Id = Id,
                NameValue = Title,
                TypeString = TypeString,
                Url = Url,
                InfoUrl = InfoUrl,
                StaticImage = StaticImage,
                StaticImageTexture = StaticImageTexture
            };
        }
    }

    public class StreamPresetData : StreamDataBase
    {
        private string _name;

        [JsonProperty("id")]
        public override string Id { get; set; }

        [JsonProperty("name")]
        public string NameValue { get => _name; set => _name = value; }

        [JsonIgnore]
        public override string Name => _name;

        [JsonProperty("type")]
        public override string TypeString { get; set; } = "video";

        [JsonProperty("url")]
        public override string Url { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("infoUrl")]
        public override string InfoUrl { get; set; }

        [JsonProperty("staticImage")]
        public override string StaticImage { get; set; }
    }

    public class WorldLocationPresetData
    {
        private const float DefaultScreenWidth = 10f;

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
        public float ScreenWidth { get; set; } = DefaultScreenWidth;

        [JsonIgnore]
        public AsyncTexture2D AvatarTexture { get; set; }

        [JsonIgnore]
        public AsyncTexture2D PictureTexture { get; set; }
    }
}
