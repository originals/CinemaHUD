using CinemaModule.Models;
using Microsoft.Xna.Framework;

namespace CinemaModule.UI.Displays.Rendering
{
    public class ScreenCornerCalculator
    {
        private const float BorderSize = 0.15f;
        private const float Depth = 0.4f;
        private const float VideoInset = 0.08f;
        private const float BackLogoOffset = 0.03f;
        private const float LogoSizeRatio = 0.75f;
        private const float LogoVerticalOffset = 0.12f;
        private const float TextWidthRatio = 1.1f;
        private const float TextHeightRatio = 0.25f;
        private const float TextVerticalOffset = 0.55f;

        public Vector3[] WorldCorners { get; } = new Vector3[4];
        public Vector3[] BorderCorners { get; } = new Vector3[4];
        public Vector3[] InnerFrameCorners => WorldCorners;
        public Vector3[] BackCorners { get; } = new Vector3[4];
        public Vector3[] LogoCorners { get; } = new Vector3[4];
        public Vector3[] TextCorners { get; } = new Vector3[4];
        public bool IsValid { get; private set; }

        public void Recalculate(WorldPosition3D worldPosition, float worldWidth, float aspectRatio)
        {
            if (worldPosition == null || worldPosition.MapId == 0)
            {
                IsValid = false;
                return;
            }

            Vector3 bottomCenter = worldPosition.ToVector3();
            Vector3 right = worldPosition.GetRightDirection();
            Vector3 up = worldPosition.GetUpDirection();
            Vector3 normal = worldPosition.GetNormalDirection();
            float worldHeight = worldWidth / aspectRatio;

            CalculateBorderCorners(bottomCenter, right, up, worldWidth, worldHeight);
            CalculateVideoCorners(bottomCenter, right, up, normal, worldWidth, worldHeight);
            CalculateBackCorners(normal);
            CalculateBackPanelElements(right, up, normal);

            IsValid = true;
        }

        private void CalculateBorderCorners(Vector3 bottomCenter, Vector3 right, Vector3 up, float worldWidth, float worldHeight)
        {
            Vector3 halfRight = right * (worldWidth / 2f + BorderSize);
            Vector3 fullUp = up * (worldHeight + BorderSize);
            Vector3 bottomOffset = -up * BorderSize;
            SetQuadCorners(BorderCorners, bottomCenter, halfRight, fullUp, bottomOffset);
        }

        private void CalculateVideoCorners(Vector3 bottomCenter, Vector3 right, Vector3 up, Vector3 normal, float worldWidth, float worldHeight)
        {
            Vector3 halfRight = right * (worldWidth / 2f);
            Vector3 fullUp = up * worldHeight;
            Vector3 insetOffset = -normal * VideoInset;
            SetQuadCorners(WorldCorners, bottomCenter + insetOffset, halfRight, fullUp, Vector3.Zero);
        }

        private void CalculateBackCorners(Vector3 normal)
        {
            Vector3 depthOffset = -normal * Depth;
            for (int i = 0; i < 4; i++)
                BackCorners[i] = BorderCorners[i] + depthOffset;
        }

        private void CalculateBackPanelElements(Vector3 right, Vector3 up, Vector3 normal)
        {
            Vector3 backCenter = (BackCorners[0] + BackCorners[1] + BackCorners[2] + BackCorners[3]) / 4f;
            float backHeight = (BackCorners[0] - BackCorners[3]).Length();
            float logoSize = backHeight * LogoSizeRatio;
            Vector3 vertOffset = up * (backHeight * LogoVerticalOffset);

            Vector3 logoHalfUp = up * (logoSize / 2f);
            Vector3 logoCenter = backCenter + vertOffset - normal * BackLogoOffset;
            SetQuadCorners(LogoCorners, logoCenter, right * (logoSize / 2f), logoHalfUp, -logoHalfUp);

            float textWidth = logoSize * TextWidthRatio;
            float textHeight = textWidth * TextHeightRatio;
            Vector3 textHalfUp = up * (textHeight / 2f);
            Vector3 textCenter = backCenter + vertOffset - up * (logoSize * TextVerticalOffset) - normal * (BackLogoOffset + 0.01f);
            SetQuadCorners(TextCorners, textCenter, right * (textWidth / 2f), textHalfUp, -textHalfUp);
        }

        private static void SetQuadCorners(Vector3[] corners, Vector3 center, Vector3 halfRight, Vector3 halfUp, Vector3 bottomOffset)
        {
            corners[0] = center - halfRight + halfUp;               // Top-left
            corners[1] = center + halfRight + halfUp;               // Top-right
            corners[2] = center + halfRight + bottomOffset;         // Bottom-right
            corners[3] = center - halfRight + bottomOffset;         // Bottom-left
        }
    }
}
