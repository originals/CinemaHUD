using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using CinemaModule.Models.Location;
using CinemaModule.Services;
using CinemaModule.UI.Controls;
using CinemaModule.UI.VideoDisplays.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.UI.VideoDisplays
{
    public class WorldVideoDisplay : Control, IVideoDisplay
    {
        #region Fields

        private const float MinVisibleDistance = 0.001f;
            private const float ForwardDotThreshold = 0.01f;
            private const int MinVisibleWidth = 8;
            private const int MinVisibleHeight = 4;
            private const float BehindCameraThreshold = -5000f;
            private const float MinAreaThreshold = 10f;
            private const float CrossProductEpsilon = 0.001f;

        private Texture2D _videoTexture;
        private WorldPosition3D _worldPosition;
        private float _worldWidth = 10f;
        private float _aspectRatio = 16f / 9f;

        private bool _isOnScreen;
        private bool _isInRange;
        private Vector2[] _screenCorners = new Vector2[4];

        private WorldVideoControls _controlPanel;
        private readonly ScreenCornerCalculator _cornerCalculator = new ScreenCornerCalculator();
        private WorldScreenRenderer _renderer;
        private VideoControlsRenderer _controlsRenderer;

        private float _fadeStartDistance = 65f;
        private float _maxDistance = 70f;
        private float _currentOpacity = 1f;

        #endregion

        #region Events

        public event EventHandler PlayPauseClicked;
        public event EventHandler<int> VolumeChanged;
        public event EventHandler SettingsClicked;
        public event EventHandler<int> QualityChanged;
        public event EventHandler TwitchChatClicked;
        public event EventHandler CloseClicked;

        public event EventHandler<bool> InRangeChanged;

        #endregion

        #region Properties

        public bool IsInRange => _isInRange;

        public bool IsPaused
        {
            get => _controlPanel?.IsPaused ?? false;
            set
            {
                if (_controlPanel != null)
                {
                    _controlPanel.IsPaused = value;
                }
            }
        }

        public int Volume
        {
            get => _controlPanel?.Volume ?? 100;
            set
            {
                if (_controlPanel != null)
                {
                    _controlPanel.Volume = value;
                }
            }
        }

        public bool IsTwitchStream
        {
            get => _controlPanel?.IsTwitchStream ?? false;
            set
            {
                if (_controlPanel != null)
                {
                    _controlPanel.IsTwitchStream = value;
                }
            }
        }

        public bool IsSeekable
        {
            get => _controlPanel?.IsSeekable ?? false;
            set
            {
                if (_controlPanel != null)
                {
                    _controlPanel.IsSeekable = value;
                }
            }
        }

        public bool IsWatchPartyViewer
        {
            get => _controlPanel?.IsWatchPartyViewer ?? false;
            set
            {
                if (_controlPanel != null)
                {
                    _controlPanel.IsWatchPartyViewer = value;
                }
            }
        }

        public float CurrentPosition
        {
            get => _controlPanel?.CurrentPosition ?? 0f;
            set
            {
                if (_controlPanel != null)
                {
                    _controlPanel.CurrentPosition = value;
                }
            }
        }

        public long Duration
        {
            get => _controlPanel?.Duration ?? 0;
            set
            {
                if (_controlPanel != null)
                {
                    _controlPanel.Duration = value;
                }
            }
        }

        public bool IsOffline { get; set; }

        public Texture2D OfflineTexture { get; set; }

        public string RadioTrackName { get; set; }

        public event EventHandler<float> SeekRequested;

        public WorldPosition3D WorldPosition
        {
            get => _worldPosition;
            set
            {
                _worldPosition = value;
                _cornerCalculator.Recalculate(_worldPosition, _worldWidth, _aspectRatio);
                ResetDisplayState();
            }
        }

        public float WorldWidth
        {
            get => _worldWidth;
            set
            {
                _worldWidth = value;
                _cornerCalculator.Recalculate(_worldPosition, _worldWidth, _aspectRatio);
            }
        }

        #endregion

        public WorldVideoDisplay()
        {
            _worldPosition = new WorldPosition3D();
            _renderer = new WorldScreenRenderer(_cornerCalculator);
            _controlsRenderer = new VideoControlsRenderer(CinemaModule.Instance.TextureService);
            ClipsBounds = false;
        }

        protected override CaptureType CapturesInput()
        {
            // bug fix? sometimes mouse is blocking on old positions of the ingame world video display,
            // even when the display is not visible or offscreen. 
            if (!_isOnScreen || !_isInRange)
            {
                return CaptureType.None;
            }

            return base.CapturesInput();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_controlPanel != null)
            {
                _controlPanel.Visible = true;
            }
        }

        protected override void OnHidden(EventArgs e)
        {
            base.OnHidden(e);
            if (_controlPanel != null)
            {
                _controlPanel.Visible = false;
            }
        }

        public void Initialize(Container parent)
        {
            _controlPanel = new WorldVideoControls
            {
                Parent = parent,
                ZIndex = ZIndex + 1
            };

            _controlPanel.PlayPauseClicked += (s, e) => PlayPauseClicked?.Invoke(this, EventArgs.Empty);
            _controlPanel.VolumeChanged += (s, vol) => VolumeChanged?.Invoke(this, vol);
            _controlPanel.SettingsClicked += (s, e) => SettingsClicked?.Invoke(this, EventArgs.Empty);
            _controlPanel.QualityChanged += (s, index) => QualityChanged?.Invoke(this, index);
            _controlPanel.TwitchChatClicked += (s, e) => TwitchChatClicked?.Invoke(this, EventArgs.Empty);
            _controlPanel.CloseClicked += (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty);
            _controlPanel.SeekRequested += (s, pos) => SeekRequested?.Invoke(this, pos);
        }

        public void UpdateTexture(Texture2D texture)
        {
            if (texture == _videoTexture && _videoTexture != null && !_videoTexture.IsDisposed) return;

            _videoTexture = texture;
            if (!CinemaModule.Instance.TextureService.IsTextureReady(texture)) return;

            _aspectRatio = (float)texture.Width / texture.Height;
            _cornerCalculator.Recalculate(_worldPosition, _worldWidth, _aspectRatio);
        }

        public void UpdateAvailableQualities(IReadOnlyList<string> qualityNames, int selectedIndex)
        {
            _controlPanel?.UpdateAvailableQualities(qualityNames, selectedIndex);
        }

        public override void DoUpdate(GameTime gameTime)
        {
            base.DoUpdate(gameTime);
            UpdateScreenProjection();
            UpdateControlPanelPosition();
            UpdateControlPanelVisibility();
        }

        private void UpdateScreenProjection()
        {
            if (!_cornerCalculator.IsValid || _worldPosition == null)
            {
                SetInRange(false);
                SetOffScreen();
                return;
            }

            var mumble = GameService.Gw2Mumble;
            if (mumble == null || !mumble.IsAvailable || mumble.CurrentMap.Id != _worldPosition.MapId)
            {
                SetInRange(false);
                SetOffScreen();
                return;
            }

            if (!UpdateDistanceAndOpacity(mumble.PlayerCamera.Position))
            {
                SetInRange(false);
                SetOffScreen();
                return;
            }

            SetInRange(true);

            if (!ProjectCornersToScreen(mumble))
            {
                SetOffScreen();
                return;
            }

            CalculateScreenBounds(out int width, out int height, out int minX, out int minY);

            if (width < MinVisibleWidth || height < MinVisibleHeight)
            {
                _isOnScreen = false;
                return;
            }

            _isOnScreen = true;
            Location = new Point(minX, minY);
            Size = new Point(width, height);
        }

        private bool UpdateDistanceAndOpacity(Vector3 cameraPos)
        {
            Vector3 screenCenter = _worldPosition.ToVector3();
            Vector3 cameraToScreen = screenCenter - cameraPos;
            float distanceToCamera = cameraToScreen.Length();

            if (distanceToCamera > _maxDistance || distanceToCamera < MinVisibleDistance)
            {
                _currentOpacity = 0f;
                return false;
            }

            _currentOpacity = CalculateOpacity(distanceToCamera);
            return true;
        }

        private bool ProjectCornersToScreen(Gw2MumbleService mumble)
        {
            Vector3 cameraPos = mumble.PlayerCamera.Position;
            Vector3 cameraForward = mumble.PlayerCamera.Forward;
            Vector3 screenCenter = _worldPosition.ToVector3();

            Vector3 cameraToScreen = screenCenter - cameraPos;
            float distanceToCamera = cameraToScreen.Length();

            cameraToScreen = cameraToScreen / distanceToCamera;
            if (Vector3.Dot(cameraToScreen, cameraForward) < ForwardDotThreshold)
            {
                return false;
            }

            Vector3 cameraForwardNormalized = CameraProjection.SafeNormalize(cameraForward);
            float fov = mumble.PlayerCamera.FieldOfView;

            for (int i = 0; i < 4; i++)
            {
                _screenCorners[i] = CameraProjection.WorldToScreen(_cornerCalculator.WorldCorners[i], cameraPos, cameraForwardNormalized, fov);
            }

            return true;
        }

        private float CalculateOpacity(float distanceToCamera)
        {
            if (distanceToCamera <= _fadeStartDistance)
            {
                return 1f;
            }

            float fadeRange = _maxDistance - _fadeStartDistance;
            float fadeProgress = (distanceToCamera - _fadeStartDistance) / fadeRange;
            return 1f - MathHelper.Clamp(fadeProgress, 0f, 1f);
        }

        private void CalculateScreenBounds(out int width, out int height, out int minX, out int minY)
        {
            width = height = minX = minY = 0;

            if (!ValidateScreenCorners()) return;
            if (!ValidateQuadArea()) return;

            float screenWidth = GameService.Graphics.SpriteScreen.Width;
            float screenHeight = GameService.Graphics.SpriteScreen.Height;

            CalculateAxisAlignedBounds(out float minXf, out float minYf, out float maxXf, out float maxYf);

            int calculatedWidth = (int)(maxXf - minXf);
            int calculatedHeight = (int)(maxYf - minYf);

            if (calculatedWidth > screenWidth * 1.5f || calculatedHeight > screenHeight * 1.5f) return;

            width = calculatedWidth;
            height = calculatedHeight;
            minX = (int)minXf;
            minY = (int)minYf;
        }

        private bool ValidateScreenCorners()
        {
            for (int i = 0; i < 4; i++)
            {
                float cornerX = _screenCorners[i].X;
                float cornerY = _screenCorners[i].Y;

                if (float.IsNaN(cornerX) || float.IsNaN(cornerY) ||
                    float.IsInfinity(cornerX) || float.IsInfinity(cornerY))
                {
                    return false;
                }

                if (cornerX < BehindCameraThreshold || cornerY < BehindCameraThreshold)
                {
                    return false;
                }
            }
            return true;
        }

        private bool ValidateQuadArea()
        {
            float area = CalculateQuadArea(_screenCorners);
            return Math.Abs(area) >= MinAreaThreshold;
        }

        private void CalculateAxisAlignedBounds(out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                float cornerX = _screenCorners[i].X;
                float cornerY = _screenCorners[i].Y;

                if (cornerX < minX) minX = cornerX;
                if (cornerY < minY) minY = cornerY;
                if (cornerX > maxX) maxX = cornerX;
                if (cornerY > maxY) maxY = cornerY;
            }
        }

        private static float CalculateQuadArea(Vector2[] corners)
        {
            // Shoelace formula
            float area = 0f;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                area += corners[i].X * corners[j].Y;
                area -= corners[j].X * corners[i].Y;
            }
            return area * 0.5f;
        }

        private bool IsPointInScreenQuad(Vector2 point)
        {
            bool? expectedSign = null;

            for (int i = 0; i < 4; i++)
            {
                Vector2 a = _screenCorners[i];
                Vector2 b = _screenCorners[(i + 1) % 4];

                float cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);

                if (Math.Abs(cross) < CrossProductEpsilon) continue;

                bool isPositive = cross > 0;
                if (expectedSign == null)
                {
                    expectedSign = isPositive;
                }
                else if (expectedSign != isPositive)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetInRange(bool inRange)
        {
            if (_isInRange == inRange) return;

            _isInRange = inRange;
            InRangeChanged?.Invoke(this, inRange);
        }

        private void SetOffScreen()
        {
            _isOnScreen = false;
            Location = Point.Zero;
            Size = Point.Zero;
        }

        private void ResetDisplayState()
        {
            _isOnScreen = false;
            _isInRange = false;
            Location = Point.Zero;
            Size = Point.Zero;
            _currentOpacity = 0f;

            // Clear old screen corners to prevent stale data
            for (int i = 0; i < _screenCorners.Length; i++)
            {
                _screenCorners[i] = Vector2.Zero;
            }

            _controlPanel?.Reset();
        }

        private void UpdateControlPanelPosition()
        {
            if (_controlPanel == null || !_isOnScreen) return;

            var videoBounds = new Rectangle(Location.X, Location.Y, Size.X, Size.Y);
            _controlPanel.UpdatePosition(videoBounds);
        }

        private void UpdateControlPanelVisibility()
        {
            if (_controlPanel == null) return;

            // Hide controls if the display is not visible, not on screen, or not in range
            if (!Visible || !_isOnScreen || !_isInRange)
            {
                _controlPanel.Hide();
                return;
            }

            var mousePos = GameService.Input.Mouse.Position;
            bool isHoveringVideo = IsPointInScreenQuad(new Vector2(mousePos.X, mousePos.Y));

            // Only check panel hover if it has a valid position (not at origin from reset)
            bool isHoveringPanel = _controlPanel.Location != Point.Zero && 
                                   _controlPanel.AbsoluteBounds.Contains(mousePos);
            bool isTrackBarDragging = _controlPanel.IsTrackBarDragging;

            if (isHoveringVideo || isHoveringPanel || isTrackBarDragging)
            {
                _controlPanel.Show();
            }
            else
            {
                _controlPanel.Hide();
            }
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (!_isOnScreen) return;

            var graphicsDevice = spriteBatch.GraphicsDevice;
            _renderer.Initialize(graphicsDevice);

            spriteBatch.End();

            try
            {
                var mumble = GameService.Gw2Mumble;
                Vector3 cameraPos = mumble.PlayerCamera.Position;
                Vector3 cameraForward = mumble.PlayerCamera.Forward;
                float fov = mumble.PlayerCamera.FieldOfView;
                float aspectRatio = (float)GameService.Graphics.SpriteScreen.Width / GameService.Graphics.SpriteScreen.Height;

                CameraProjection.CreateViewProjectionMatrices(cameraPos, cameraForward, fov, aspectRatio, out var viewMatrix, out var projectionMatrix);

                var textureToRender = IsOffline && OfflineTexture != null && !OfflineTexture.IsDisposed
                    ? OfflineTexture
                    : _videoTexture;
                _renderer.Render(graphicsDevice, viewMatrix, projectionMatrix, textureToRender, _currentOpacity);

                if (!string.IsNullOrEmpty(RadioTrackName))
                {
                    var trackNameTexture = _controlsRenderer.GetOrCreateTrackNameTexture(graphicsDevice, RadioTrackName);
                    _renderer.RenderOverlay(graphicsDevice, viewMatrix, projectionMatrix, trackNameTexture, _currentOpacity);
                }
            }
            finally
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null);
            }
        }


        #region IDisposable

        protected override void DisposeControl()
        {
            _controlPanel?.Dispose();
            _renderer?.Dispose();
            _controlsRenderer?.DisposeTrackNameTexture();
            base.DisposeControl();
        }

        #endregion
    }
}
