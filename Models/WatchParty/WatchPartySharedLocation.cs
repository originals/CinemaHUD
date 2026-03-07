using CinemaModule.Models.Location;
using Newtonsoft.Json;

namespace CinemaModule.Models.WatchParty
{
    public class WatchPartySharedLocation
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("z")]
        public float Z { get; set; }

        [JsonProperty("yaw")]
        public float Yaw { get; set; }

        [JsonProperty("pitch")]
        public float Pitch { get; set; }

        [JsonProperty("mapId")]
        public int MapId { get; set; }

        [JsonProperty("screenWidth")]
        public float ScreenWidth { get; set; }

        public static WatchPartySharedLocation FromSavedLocation(SavedLocation location)
        {
            return new WatchPartySharedLocation
            {
                Name = location.Name,
                X = location.Position.X,
                Y = location.Position.Y,
                Z = location.Position.Z,
                Yaw = location.Position.Yaw,
                Pitch = location.Position.Pitch,
                MapId = location.Position.MapId,
                ScreenWidth = location.ScreenWidth
            };
        }

        public SavedLocation ToSavedLocation()
        {
            return new SavedLocation(Name, new WorldPosition3D(X, Y, Z, Yaw, Pitch, MapId), ScreenWidth);
        }
    }
}
