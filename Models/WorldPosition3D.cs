using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;

namespace CinemaModule.Models
{
    public class WorldPosition3D
    {
        private const float MinLengthSquared = 0.0001f;
        private const float FullRotation = 360f;

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

        public void GetDirections(out Vector3 normal, out Vector3 up, out Vector3 right)
        {
            float pitchRad = MathHelper.ToRadians(Pitch);
            float yawRad = MathHelper.ToRadians(Yaw);
            float cosP = (float)Math.Cos(pitchRad);
            float sinP = (float)Math.Sin(pitchRad);
            float sinY = (float)Math.Sin(yawRad);
            float cosY = (float)Math.Cos(yawRad);

            normal = new Vector3(cosP * sinY, cosP * cosY, sinP);
            up = new Vector3(-sinP * sinY, -sinP * cosY, cosP);
            right = Vector3.Cross(up, normal);

            if (right.LengthSquared() > MinLengthSquared)
                right.Normalize();
        }

        public Vector3 GetNormalDirection()
        {
            GetDirections(out Vector3 normal, out _, out _);
            return normal;
        }

        public Vector3 GetUpDirection()
        {
            GetDirections(out _, out Vector3 up, out _);
            return up;
        }

        public Vector3 GetRightDirection()
        {
            GetDirections(out _, out _, out Vector3 right);
            return right;
        }

        public static float NormalizeYaw(float yaw)
        {
            yaw %= FullRotation;
            return yaw < 0 ? yaw + FullRotation : yaw;
        }
    }
}
