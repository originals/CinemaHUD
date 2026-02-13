using Blish_HUD;
using Microsoft.Xna.Framework;

namespace CinemaModule.UI.Displays.Rendering
{
    public static class CameraProjection
    {
        private const float NearPlane = 0.1f;
        private const float FarPlane = 1000f;
        private const float ClipSpaceEpsilon = 0.01f;
        private static readonly Vector3 WorldUp = new Vector3(0, 0, 1);

        public static void GetCameraOrientation(Vector3 cameraForward, out Vector3 right, out Vector3 up)
        {
            right = Vector3.Cross(cameraForward, WorldUp);

            if (right.LengthSquared() < 0.0001f)
            {
                right = new Vector3(1, 0, 0);
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
            GetCameraOrientation(cameraForward, out var cameraRight, out var cameraUp);

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
            Vector4 clipSpace = Vector4.Transform(new Vector4(worldPosition, 1.0f), viewProjection);

            if (clipSpace.W <= ClipSpaceEpsilon)
            {
                return new Vector2(-10000, -10000);
            }

            float inverseW = 1.0f / clipSpace.W;
            Vector3 normalizedDeviceCoordinates = new Vector3(clipSpace.X * inverseW, clipSpace.Y * inverseW, clipSpace.Z * inverseW);

            return new Vector2(
                (normalizedDeviceCoordinates.X + 1f) * 0.5f * screenWidth,
                (1f - normalizedDeviceCoordinates.Y) * 0.5f * screenHeight);
        }

        public static Vector3 SafeNormalize(Vector3 vector)
        {
            if (vector.LengthSquared() > 0.0001f)
            {
                vector.Normalize();
            }
            return vector;
        }
    }
}
