using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;

namespace CinemaModule.Models
{
    /// GW2: X = East/West, Y = North/South, Z = Up/Down
    /// Yaw 0 = facing North (+Y), Yaw 90 = facing East (+X)
    public class WorldPosition3D
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("z")]
        public float Z { get; set; }

        [JsonProperty("yaw")]
        public float Yaw { get; set; }   // Horizontal rotation in degrees (0 = North, 90 = East)

        [JsonProperty("pitch")]
        public float Pitch { get; set; } // Vertical tilt in degrees (-90 = down, 90 = up)

        [JsonProperty("mapId")]
        public int MapId { get; set; }

        public WorldPosition3D() : this(0, 0, 0, 0, 0, 0) { }

        public WorldPosition3D(float x, float y, float z, int mapId) 
            : this(x, y, z, 0, 0, mapId) { }

        public WorldPosition3D(float x, float y, float z, float yaw, float pitch, int mapId)
        {
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
            Pitch = pitch;
            MapId = mapId;
        }

        public Vector3 ToVector3() => new Vector3(X, Y, Z);

        public Vector3 GetRightDirection()
        {
            Vector3 normal = GetNormalDirection();
            Vector3 up = GetUpDirection();
            Vector3 right = Vector3.Cross(up, normal);
            
            if (right.LengthSquared() > 0.0001f)
            {
                right.Normalize();
            }
            
            return right;
        }

        public Vector3 GetUpDirection()
        {
            float pitchRad = MathHelper.ToRadians(Pitch);
            float yawRad = MathHelper.ToRadians(Yaw);

            float cosP = (float)Math.Cos(pitchRad);
            float sinP = (float)Math.Sin(pitchRad);

            return new Vector3(
                -sinP * (float)Math.Sin(yawRad),
                -sinP * (float)Math.Cos(yawRad),
                cosP
            );
        }

        public Vector3 GetNormalDirection()
        {
            float pitchRad = MathHelper.ToRadians(Pitch);
            float yawRad = MathHelper.ToRadians(Yaw);

            float cosP = (float)Math.Cos(pitchRad);
            float sinP = (float)Math.Sin(pitchRad);

            return new Vector3(
                cosP * (float)Math.Sin(yawRad),
                cosP * (float)Math.Cos(yawRad),
                sinP
            );
        }

        public static float NormalizeYaw(float yaw)
        {
            while (yaw < 0) yaw += 360;
            while (yaw >= 360) yaw -= 360;
            return yaw;
        }
    }
}
