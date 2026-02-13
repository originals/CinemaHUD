using System;
using Blish_HUD;
using Blish_HUD.Content;
using CinemaModule.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.UI.Displays.Rendering
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
            _corners = corners ?? throw new ArgumentNullException(nameof(corners));
        }

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            if (_initialized) return;
            if (graphicsDevice == null) return;

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
            if (!_initialized || !_corners.IsValid || graphicsDevice == null) return;

            var savedState = SaveGraphicsState(graphicsDevice);
            try
            {
                BuildVertices(opacity);
                SetRenderState(graphicsDevice);

                _basicEffect.World = Matrix.Identity;
                _basicEffect.View = viewMatrix;
                _basicEffect.Projection = projectionMatrix;

                RenderBackPanel(graphicsDevice);
                RenderBackLogo(graphicsDevice, opacity);
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

        private void BuildVertices(float opacity)
        {
            byte alpha = CalculateAlpha(opacity);
            
            Color sideColorWithAlpha = ApplyAlpha(InnerFrameSideColor, alpha);
            Color topBottomColorWithAlpha = ApplyAlpha(InnerFrameTopBottomColor, alpha);
            Color texturedColor = CreateOpaqueColorWithAlpha(alpha);

            Vector2[] texCoords = {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            };

            BuildQuadVertices(_quadVertices, _corners.WorldCorners, texturedColor, texCoords);
            BuildBackVertices(texturedColor, texCoords);
            BuildSideVertices(texturedColor, texturedColor);
            BuildInnerFrameVertices(sideColorWithAlpha, topBottomColorWithAlpha);
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

        private void BuildQuadVertices(VertexPositionColorTexture[] vertices, Vector3[] corners, Color color, Vector2[] texCoords)
        {
            for (int i = 0; i < VerticesPerQuad; i++)
            {
                vertices[i] = new VertexPositionColorTexture(corners[i], color, texCoords[i]);
            }
        }

        private void BuildBackVertices(Color color, Vector2[] texCoords)
        {
            _backVertices[0] = new VertexPositionColorTexture(_corners.BackCorners[0], color, texCoords[0]);
            _backVertices[1] = new VertexPositionColorTexture(_corners.BackCorners[3], color, texCoords[3]);
            _backVertices[2] = new VertexPositionColorTexture(_corners.BackCorners[2], color, texCoords[2]);
            _backVertices[3] = new VertexPositionColorTexture(_corners.BackCorners[1], color, texCoords[1]);
        }

        private void BuildSideVertices(Color sideColor, Color topBottomColor)
        {
            // Left side
            _sideVertices[0] = new VertexPositionColorTexture(_corners.BorderCorners[0], sideColor, new Vector2(0, 0));
            _sideVertices[1] = new VertexPositionColorTexture(_corners.BorderCorners[3], sideColor, new Vector2(0, 1));
            _sideVertices[2] = new VertexPositionColorTexture(_corners.BackCorners[3], sideColor, new Vector2(1, 1));
            _sideVertices[3] = new VertexPositionColorTexture(_corners.BackCorners[0], sideColor, new Vector2(1, 0));

            // Right side
            _sideVertices[4] = new VertexPositionColorTexture(_corners.BorderCorners[1], sideColor, new Vector2(0, 0));
            _sideVertices[5] = new VertexPositionColorTexture(_corners.BackCorners[1], sideColor, new Vector2(1, 0));
            _sideVertices[6] = new VertexPositionColorTexture(_corners.BackCorners[2], sideColor, new Vector2(1, 1));
            _sideVertices[7] = new VertexPositionColorTexture(_corners.BorderCorners[2], sideColor, new Vector2(0, 1));

            // Top side
            _sideVertices[8] = new VertexPositionColorTexture(_corners.BorderCorners[0], topBottomColor, new Vector2(0, 0));
            _sideVertices[9] = new VertexPositionColorTexture(_corners.BackCorners[0], topBottomColor, new Vector2(0, 1));
            _sideVertices[10] = new VertexPositionColorTexture(_corners.BackCorners[1], topBottomColor, new Vector2(1, 1));
            _sideVertices[11] = new VertexPositionColorTexture(_corners.BorderCorners[1], topBottomColor, new Vector2(1, 0));

            // Bottom side
            _sideVertices[12] = new VertexPositionColorTexture(_corners.BorderCorners[3], topBottomColor, new Vector2(0, 0));
            _sideVertices[13] = new VertexPositionColorTexture(_corners.BorderCorners[2], topBottomColor, new Vector2(1, 0));
            _sideVertices[14] = new VertexPositionColorTexture(_corners.BackCorners[2], topBottomColor, new Vector2(1, 1));
            _sideVertices[15] = new VertexPositionColorTexture(_corners.BackCorners[3], topBottomColor, new Vector2(0, 1));
        }

        private void BuildInnerFrameVertices(Color sideColor, Color topBottomColor)
        {
            // These connect the front border face to the recessed video quad
            // Top inner bevel (border top edge to video top edge)
            _innerFrameVertices[0] = new VertexPositionColorTexture(_corners.BorderCorners[0], topBottomColor, new Vector2(0, 0));
            _innerFrameVertices[1] = new VertexPositionColorTexture(_corners.BorderCorners[1], topBottomColor, new Vector2(1, 0));
            _innerFrameVertices[2] = new VertexPositionColorTexture(_corners.InnerFrameCorners[1], topBottomColor, new Vector2(1, 1));
            _innerFrameVertices[3] = new VertexPositionColorTexture(_corners.InnerFrameCorners[0], topBottomColor, new Vector2(0, 1));

            // Bottom inner bevel (video bottom edge to border bottom edge)
            _innerFrameVertices[4] = new VertexPositionColorTexture(_corners.InnerFrameCorners[3], topBottomColor, new Vector2(0, 0));
            _innerFrameVertices[5] = new VertexPositionColorTexture(_corners.InnerFrameCorners[2], topBottomColor, new Vector2(1, 0));
            _innerFrameVertices[6] = new VertexPositionColorTexture(_corners.BorderCorners[2], topBottomColor, new Vector2(1, 1));
            _innerFrameVertices[7] = new VertexPositionColorTexture(_corners.BorderCorners[3], topBottomColor, new Vector2(0, 1));

            // Left inner bevel (border left edge to video left edge)
            _innerFrameVertices[8] = new VertexPositionColorTexture(_corners.BorderCorners[0], sideColor, new Vector2(0, 0));
            _innerFrameVertices[9] = new VertexPositionColorTexture(_corners.InnerFrameCorners[0], sideColor, new Vector2(1, 0));
            _innerFrameVertices[10] = new VertexPositionColorTexture(_corners.InnerFrameCorners[3], sideColor, new Vector2(1, 1));
            _innerFrameVertices[11] = new VertexPositionColorTexture(_corners.BorderCorners[3], sideColor, new Vector2(0, 1));

            // Right inner bevel (video right edge to border right edge)
            _innerFrameVertices[12] = new VertexPositionColorTexture(_corners.InnerFrameCorners[1], sideColor, new Vector2(0, 0));
            _innerFrameVertices[13] = new VertexPositionColorTexture(_corners.BorderCorners[1], sideColor, new Vector2(1, 0));
            _innerFrameVertices[14] = new VertexPositionColorTexture(_corners.BorderCorners[2], sideColor, new Vector2(1, 1));
            _innerFrameVertices[15] = new VertexPositionColorTexture(_corners.InnerFrameCorners[2], sideColor, new Vector2(0, 1));
        }

        private void RenderBackPanel(GraphicsDevice graphicsDevice)
        {
            _basicEffect.Texture = GetTextureOrFallback(_backTexture);
            ApplyEffectAndDraw(graphicsDevice, () => DrawQuad(graphicsDevice, _backVertices));
        }

        private void RenderSidePanels(GraphicsDevice graphicsDevice)
        {
            Texture2D sideTex = GetTextureOrFallback(_sideTexture);
            Texture2D topBottomTex = GetTextureOrFallback(_topBottomTexture);

            for (int sideIndex = 0; sideIndex < QuadCount; sideIndex++)
            {
                bool isLeftOrRight = sideIndex < 2;
                _basicEffect.Texture = isLeftOrRight ? sideTex : topBottomTex;
                int vertexOffset = sideIndex * VerticesPerQuad;
                ApplyEffectAndDraw(graphicsDevice, () => DrawIndexedQuad(graphicsDevice, _sideVertices, vertexOffset));
            }
        }

        private void RenderInnerFrame(GraphicsDevice graphicsDevice)
        {
            _basicEffect.Texture = CinemaModule.Instance.TextureService.GetWhitePixel();

            ApplyEffectAndDraw(graphicsDevice, () =>
            {
                for (int bevelIndex = 0; bevelIndex < QuadCount; bevelIndex++)
                {
                    int vertexOffset = bevelIndex * VerticesPerQuad;
                    DrawIndexedQuad(graphicsDevice, _innerFrameVertices, vertexOffset);
                }
            });
        }

        private void RenderBackLogo(GraphicsDevice graphicsDevice, float opacity)
        {
            byte alpha = CalculateAlpha(opacity);
            Color videoColor = CreateOpaqueColorWithAlpha(alpha);

            RenderLogoTexture(graphicsDevice, videoColor);
            RenderTextTexture(graphicsDevice, videoColor);
        }

        private void RenderLogoTexture(GraphicsDevice graphicsDevice, Color color)
        {
            Texture2D logoTex = _logoTexture?.Texture;
            if (!CinemaModule.Instance.TextureService.IsTextureReady(logoTex)) return;

            _logoVertices[0] = new VertexPositionColorTexture(_corners.LogoCorners[0], color, new Vector2(0, 0));
            _logoVertices[1] = new VertexPositionColorTexture(_corners.LogoCorners[3], color, new Vector2(0, 1));
            _logoVertices[2] = new VertexPositionColorTexture(_corners.LogoCorners[2], color, new Vector2(1, 1));
            _logoVertices[3] = new VertexPositionColorTexture(_corners.LogoCorners[1], color, new Vector2(1, 0));

            _basicEffect.Texture = logoTex;
            ApplyEffectAndDraw(graphicsDevice, () => DrawQuad(graphicsDevice, _logoVertices));
        }

        private void RenderTextTexture(GraphicsDevice graphicsDevice, Color color)
        {
            Texture2D textTex = _logoTextTexture?.Texture;
            if (!CinemaModule.Instance.TextureService.IsTextureReady(textTex)) return;

            _textVertices[0] = new VertexPositionColorTexture(_corners.TextCorners[0], color, new Vector2(1, 0));
            _textVertices[1] = new VertexPositionColorTexture(_corners.TextCorners[3], color, new Vector2(1, 1));
            _textVertices[2] = new VertexPositionColorTexture(_corners.TextCorners[2], color, new Vector2(0, 1));
            _textVertices[3] = new VertexPositionColorTexture(_corners.TextCorners[1], color, new Vector2(0, 0));

            _basicEffect.Texture = textTex;
            ApplyEffectAndDraw(graphicsDevice, () => DrawQuad(graphicsDevice, _textVertices));
        }

        private void RenderVideoQuad(GraphicsDevice graphicsDevice, Texture2D videoTexture)
        {
            _basicEffect.Texture = CinemaModule.Instance.TextureService.IsTextureReady(videoTexture)
                ? videoTexture
                : GetTextureOrFallback(_screenOffTexture);

            ApplyEffectAndDraw(graphicsDevice, () => DrawQuad(graphicsDevice, _quadVertices));
        }

        private void DrawQuad(GraphicsDevice graphicsDevice, VertexPositionColorTexture[] vertices)
        {
            DrawIndexedQuad(graphicsDevice, vertices, 0);
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

        private void ApplyEffectAndDraw(GraphicsDevice graphicsDevice, Action drawAction)
        {
            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                drawAction();
            }
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
