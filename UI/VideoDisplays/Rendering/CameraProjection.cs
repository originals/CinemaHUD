using Blish_HUD;
using Microsoft.Xna.Framework;

namespace CinemaModule.UI.VideoDisplays.Rendering
{
    public static class CameraProjection
    {
        private const float NearPlane = 0.1f;
        private const float FarPlane = 1000f;
        private const float ClipSpaceEpsilon = 0.01f;
        private const float LengthSquaredThreshold = 0.0001f;
        private const float OffScreenCoordinate = -10000f;
        private static readonly Vector3 WorldUp = new Vector3(0, 0, 1);

        public static void GetCameraOrientation(Vector3 cameraForward, out Vector3 right, out Vector3 up)
        {
            right = Vector3.Cross(cameraForward, WorldUp);

            if (right.LengthSquared() < LengthSquaredThreshold)
            {
                right = Vector3.UnitX;
            }
            else
            {
                right.Normalize();
            }

            up = Vector3.Cross(right, cameraForward);
            up.Normalize();
        }

        public static void CreateViewProjectionMatrices(
            Vector3 cameraPosition,
            Vector3 cameraForward,
            float fieldOfView,
            float aspectRatio,
            out Matrix viewMatrix,
            out Matrix projectionMatrix)
        {
            GetCameraOrientation(cameraForward, out _, out var cameraUp);

            viewMatrix = Matrix.CreateLookAt(cameraPosition, cameraPosition + cameraForward, cameraUp);
            projectionMatrix = Matrix.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, NearPlane, FarPlane);
        }

        public static Vector2 WorldToScreen(Vector3 worldPosition, Vector3 cameraPosition, Vector3 cameraForward, float fieldOfView)
        {
            var spriteScreen = GameService.Graphics.SpriteScreen;
            float aspectRatio = (float)spriteScreen.Width / spriteScreen.Height;

            CreateViewProjectionMatrices(cameraPosition, cameraForward, fieldOfView, aspectRatio, out var viewMatrix, out var projectionMatrix);

            return ProjectToScreen(worldPosition, viewMatrix * projectionMatrix, spriteScreen.Width, spriteScreen.Height);
        }

        private static Vector2 ProjectToScreen(Vector3 worldPosition, Matrix viewProjection, int screenWidth, int screenHeight)
        {
            var clipSpace = Vector4.Transform(new Vector4(worldPosition, 1f), viewProjection);

            if (clipSpace.W <= ClipSpaceEpsilon)
            {
                return new Vector2(OffScreenCoordinate, OffScreenCoordinate);
            }

            float inverseW = 1f / clipSpace.W;
            var ndc = new Vector3(clipSpace.X * inverseW, clipSpace.Y * inverseW, clipSpace.Z * inverseW);

            return new Vector2(
                (ndc.X + 1f) * 0.5f * screenWidth,
                (1f - ndc.Y) * 0.5f * screenHeight);
        }

        public static Vector3 SafeNormalize(Vector3 vector)
        {
            return vector.LengthSquared() > LengthSquaredThreshold ? Vector3.Normalize(vector) : vector;
        }
    }
}
