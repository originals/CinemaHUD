using System;
using Blish_HUD;
using Blish_HUD.Content;
using CinemaModule.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.UI.VideoDisplays.Rendering
{
    public class WorldScreenRenderer : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<WorldScreenRenderer>();

        private const int VerticesPerQuad = 4;
        private const int QuadCount = 4;
        private const int SideVerticesCount = VerticesPerQuad * QuadCount;
        private const byte MaxAlpha = 255;

        private static readonly Color InnerFrameSideColor = new Color(31, 31, 31);
        private static readonly Color InnerFrameTopBottomColor = new Color(35, 35, 36);

        private static readonly Vector2[] StandardTexCoords = {
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1)
        };

        private readonly ScreenCornerCalculator _corners;
        private readonly short[] _quadIndices = { 0, 1, 2, 0, 2, 3 };

        private VertexPositionColorTexture[] _quadVertices = new VertexPositionColorTexture[VerticesPerQuad];
        private VertexPositionColorTexture[] _backVertices = new VertexPositionColorTexture[VerticesPerQuad];
        private VertexPositionColorTexture[] _sideVertices = new VertexPositionColorTexture[SideVerticesCount];
        private VertexPositionColorTexture[] _innerFrameVertices = new VertexPositionColorTexture[SideVerticesCount];
        private VertexPositionColorTexture[] _logoVertices = new VertexPositionColorTexture[VerticesPerQuad];
        private VertexPositionColorTexture[] _textVertices = new VertexPositionColorTexture[VerticesPerQuad];

        private BasicEffect _basicEffect;
        private AsyncTexture2D _logoTexture;
        private AsyncTexture2D _logoTextTexture;
        private AsyncTexture2D _sideTexture;
        private AsyncTexture2D _topBottomTexture;
        private AsyncTexture2D _backTexture;
        private AsyncTexture2D _screenOffTexture;

        private bool _initialized;

        public WorldScreenRenderer(ScreenCornerCalculator corners)
        {
            _corners = corners;
        }

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            if (_initialized) return;

            try
            {
                _basicEffect = new BasicEffect(graphicsDevice)
                {
                    TextureEnabled = true,
                    VertexColorEnabled = true,
                    LightingEnabled = false,
                    FogEnabled = false
                };

                _logoTexture = CinemaModule.Instance.TextureService.GetLogo();
                _logoTextTexture = CinemaModule.Instance.TextureService.GetLogoText();

                _sideTexture = CinemaModule.Instance.TextureService.GetTvSide();
                _topBottomTexture = CinemaModule.Instance.TextureService.GetTvTopBottom();
                _backTexture = CinemaModule.Instance.TextureService.GetTvBack();
                _screenOffTexture = CinemaModule.Instance.TextureService.GetTvScreenOff();

                _initialized = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize WorldScreenRenderer");
            }
        }

        public void Render(
            GraphicsDevice graphicsDevice,
            Matrix viewMatrix,
            Matrix projectionMatrix,
            Texture2D videoTexture,
            float opacity)
        {
            if (!_initialized || !_corners.IsValid) return;

            var savedState = SaveGraphicsState(graphicsDevice);
            try
            {
                Color texturedColor = BuildVertices(opacity);
                SetRenderState(graphicsDevice);

                _basicEffect.World = Matrix.Identity;
                _basicEffect.View = viewMatrix;
                _basicEffect.Projection = projectionMatrix;

                RenderBackPanel(graphicsDevice);
                RenderBackLogo(graphicsDevice, texturedColor);
                RenderVideoQuad(graphicsDevice, videoTexture);
                RenderInnerFrame(graphicsDevice);
                RenderSidePanels(graphicsDevice);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error during world screen rendering");
            }
            finally
            {
                RestoreGraphicsState(graphicsDevice, savedState);
            }
        }

        private Color BuildVertices(float opacity)
        {
            byte alpha = CalculateAlpha(opacity);

            Color sideColorWithAlpha = ApplyAlpha(InnerFrameSideColor, alpha);
            Color topBottomColorWithAlpha = ApplyAlpha(InnerFrameTopBottomColor, alpha);
            Color texturedColor = CreateOpaqueColorWithAlpha(alpha);

            BuildQuadVertices(_quadVertices, _corners.WorldCorners, texturedColor);
            BuildBackVertices(texturedColor);
            BuildSideVertices(texturedColor);
            BuildInnerFrameVertices(sideColorWithAlpha, topBottomColorWithAlpha);

            return texturedColor;
        }

        private static byte CalculateAlpha(float opacity)
        {
            return (byte)(Math.Max(0f, Math.Min(1f, opacity)) * MaxAlpha);
        }

        private static Color ApplyAlpha(Color color, byte alpha)
        {
            return new Color(color.R, color.G, color.B, alpha);
        }

        private static Color CreateOpaqueColorWithAlpha(byte alpha)
        {
            return new Color(MaxAlpha, MaxAlpha, MaxAlpha, alpha);
        }

        private void BuildQuadVertices(VertexPositionColorTexture[] vertices, Vector3[] corners, Color color)
        {
            for (int i = 0; i < VerticesPerQuad; i++)
            {
                vertices[i] = new VertexPositionColorTexture(corners[i], color, StandardTexCoords[i]);
            }
        }

        private void BuildBackVertices(Color color)
        {
            SetQuadVertex(ref _backVertices[0], _corners.BackCorners[0], color, 0);
            SetQuadVertex(ref _backVertices[1], _corners.BackCorners[3], color, 3);
            SetQuadVertex(ref _backVertices[2], _corners.BackCorners[2], color, 2);
            SetQuadVertex(ref _backVertices[3], _corners.BackCorners[1], color, 1);
        }

        private static void SetQuadVertex(ref VertexPositionColorTexture vertex, Vector3 position, Color color, int texCoordIndex)
        {
            vertex = new VertexPositionColorTexture(position, color, StandardTexCoords[texCoordIndex]);
        }

        private void BuildSideVertices(Color color)
        {
            BuildSideQuad(0, _corners.BorderCorners[0], _corners.BorderCorners[3], _corners.BackCorners[3], _corners.BackCorners[0], color);
            BuildSideQuad(4, _corners.BorderCorners[1], _corners.BackCorners[1], _corners.BackCorners[2], _corners.BorderCorners[2], color);
            BuildSideQuad(8, _corners.BorderCorners[0], _corners.BackCorners[0], _corners.BackCorners[1], _corners.BorderCorners[1], color);
            BuildSideQuad(12, _corners.BorderCorners[3], _corners.BorderCorners[2], _corners.BackCorners[2], _corners.BackCorners[3], color);
        }

        private void BuildSideQuad(int startIndex, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color color)
        {
            SetQuadVertex(ref _sideVertices[startIndex], v0, color, 0);
            SetQuadVertex(ref _sideVertices[startIndex + 1], v1, color, 3);
            SetQuadVertex(ref _sideVertices[startIndex + 2], v2, color, 2);
            SetQuadVertex(ref _sideVertices[startIndex + 3], v3, color, 1);
        }

        private void BuildInnerFrameVertices(Color sideColor, Color topBottomColor)
        {
            BuildInnerFrameQuad(0, _corners.BorderCorners[0], _corners.BorderCorners[1], _corners.InnerFrameCorners[1], _corners.InnerFrameCorners[0], topBottomColor);
            BuildInnerFrameQuad(4, _corners.InnerFrameCorners[3], _corners.InnerFrameCorners[2], _corners.BorderCorners[2], _corners.BorderCorners[3], topBottomColor);
            BuildInnerFrameQuad(8, _corners.BorderCorners[0], _corners.InnerFrameCorners[0], _corners.InnerFrameCorners[3], _corners.BorderCorners[3], sideColor);
            BuildInnerFrameQuad(12, _corners.InnerFrameCorners[1], _corners.BorderCorners[1], _corners.BorderCorners[2], _corners.InnerFrameCorners[2], sideColor);
        }

        private void BuildInnerFrameQuad(int startIndex, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color color)
        {
            SetQuadVertex(ref _innerFrameVertices[startIndex], v0, color, 0);
            SetQuadVertex(ref _innerFrameVertices[startIndex + 1], v1, color, 1);
            SetQuadVertex(ref _innerFrameVertices[startIndex + 2], v2, color, 2);
            SetQuadVertex(ref _innerFrameVertices[startIndex + 3], v3, color, 3);
        }

        private void RenderBackPanel(GraphicsDevice graphicsDevice)
        {
            _basicEffect.Texture = GetTextureOrFallback(_backTexture);
            ApplyEffectAndDrawQuad(graphicsDevice, _backVertices, 0);
        }

        private void RenderSidePanels(GraphicsDevice graphicsDevice)
        {
            Texture2D sideTex = GetTextureOrFallback(_sideTexture);
            Texture2D topBottomTex = GetTextureOrFallback(_topBottomTexture);

            for (int sideIndex = 0; sideIndex < QuadCount; sideIndex++)
            {
                _basicEffect.Texture = sideIndex < 2 ? sideTex : topBottomTex;
                ApplyEffectAndDrawQuad(graphicsDevice, _sideVertices, sideIndex * VerticesPerQuad);
            }
        }

        private void RenderInnerFrame(GraphicsDevice graphicsDevice)
        {
            _basicEffect.Texture = CinemaModule.Instance.TextureService.GetWhitePixel();
            ApplyEffect();

            for (int bevelIndex = 0; bevelIndex < QuadCount; bevelIndex++)
            {
                DrawIndexedQuad(graphicsDevice, _innerFrameVertices, bevelIndex * VerticesPerQuad);
            }
        }

        private void RenderBackLogo(GraphicsDevice graphicsDevice, Color color)
        {
            RenderLogoTexture(graphicsDevice, color);
            RenderTextTexture(graphicsDevice, color);
        }

        private void RenderLogoTexture(GraphicsDevice graphicsDevice, Color color)
        {
            Texture2D logoTex = _logoTexture?.Texture;
            if (!CinemaModule.Instance.TextureService.IsTextureReady(logoTex)) return;

            SetQuadVertex(ref _logoVertices[0], _corners.LogoCorners[0], color, 0);
            SetQuadVertex(ref _logoVertices[1], _corners.LogoCorners[3], color, 3);
            SetQuadVertex(ref _logoVertices[2], _corners.LogoCorners[2], color, 2);
            SetQuadVertex(ref _logoVertices[3], _corners.LogoCorners[1], color, 1);

            _basicEffect.Texture = logoTex;
            ApplyEffectAndDrawQuad(graphicsDevice, _logoVertices, 0);
        }

        private void RenderTextTexture(GraphicsDevice graphicsDevice, Color color)
        {
            Texture2D textTex = _logoTextTexture?.Texture;
            if (!CinemaModule.Instance.TextureService.IsTextureReady(textTex)) return;

            SetQuadVertex(ref _textVertices[0], _corners.TextCorners[0], color, 1);
            SetQuadVertex(ref _textVertices[1], _corners.TextCorners[3], color, 2);
            SetQuadVertex(ref _textVertices[2], _corners.TextCorners[2], color, 3);
            SetQuadVertex(ref _textVertices[3], _corners.TextCorners[1], color, 0);

            _basicEffect.Texture = textTex;
            ApplyEffectAndDrawQuad(graphicsDevice, _textVertices, 0);
        }

        private void RenderVideoQuad(GraphicsDevice graphicsDevice, Texture2D videoTexture)
        {
            _basicEffect.Texture = CinemaModule.Instance.TextureService.IsTextureReady(videoTexture)
                ? videoTexture
                : GetTextureOrFallback(_screenOffTexture);

            ApplyEffectAndDrawQuad(graphicsDevice, _quadVertices, 0);
        }

        private void DrawIndexedQuad(GraphicsDevice graphicsDevice, VertexPositionColorTexture[] vertices, int vertexOffset)
        {
            graphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                vertices,
                vertexOffset,
                VerticesPerQuad,
                _quadIndices,
                0,
                2);
        }

        private Texture2D GetTextureOrFallback(AsyncTexture2D asyncTexture)
        {
            return asyncTexture?.Texture ?? CinemaModule.Instance.TextureService.GetWhitePixel();
        }

        private void ApplyEffect()
        {
            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
            }
        }

        private void ApplyEffectAndDrawQuad(GraphicsDevice graphicsDevice, VertexPositionColorTexture[] vertices, int vertexOffset)
        {
            ApplyEffect();
            DrawIndexedQuad(graphicsDevice, vertices, vertexOffset);
        }

        private (RasterizerState rasterizer, SamplerState sampler, BlendState blend, DepthStencilState depth) SaveGraphicsState(GraphicsDevice graphicsDevice)
        {
            return (
                graphicsDevice.RasterizerState,
                graphicsDevice.SamplerStates[0],
                graphicsDevice.BlendState,
                graphicsDevice.DepthStencilState);
        }

        private void SetRenderState(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        private void RestoreGraphicsState(GraphicsDevice graphicsDevice,
            (RasterizerState rasterizer, SamplerState sampler, BlendState blend, DepthStencilState depth) state)
        {
            graphicsDevice.RasterizerState = state.rasterizer;
            graphicsDevice.SamplerStates[0] = state.sampler;
            graphicsDevice.BlendState = state.blend;
            graphicsDevice.DepthStencilState = state.depth;
        }

        public void Dispose()
        {
            _basicEffect?.Dispose();
            _basicEffect = null;
            _initialized = false;
        }
    }
}
