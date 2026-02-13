using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using CinemaModule.Models;
using CinemaModule.Services;
using CinemaModule.UI.Controls;
using CinemaModule.UI.Displays.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.UI.Displays
{
    public class WorldVideoDisplay : Control, IVideoDisplay
    {
        #region Fields

        private const float MinVisibleDistance = 0.001f;
        private const float ForwardDotThreshold = 0.01f;
        private const int MinVisibleWidth = 8;
        private const int MinVisibleHeight = 4;

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
        }

        public void UpdateTexture(Texture2D texture)
        {
            if (_videoTexture == null || _videoTexture.IsDisposed)
            {
                _videoTexture = texture;
                if (CinemaModule.Instance.TextureService.IsTextureReady(texture))
                {
                    _aspectRatio = (float)texture.Width / texture.Height;
                    _cornerCalculator.Recalculate(_worldPosition, _worldWidth, _aspectRatio);
                }
                return;
            }

            if (texture == _videoTexture) return;

            _videoTexture = texture;
            if (CinemaModule.Instance.TextureService.IsTextureReady(texture))
            {
                _aspectRatio = (float)texture.Width / texture.Height;
                _cornerCalculator.Recalculate(_worldPosition, _worldWidth, _aspectRatio);
            }
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

            Vector3 cameraPos = mumble.PlayerCamera.Position;
            Vector3 cameraForward = mumble.PlayerCamera.Forward;
            Vector3 screenCenter = _worldPosition.ToVector3();
            Vector3 screenNormal = CameraProjection.SafeNormalize(_worldPosition.GetNormalDirection());

            Vector3 cameraToScreen = screenCenter - cameraPos;
            float distanceToPlane = Vector3.Dot(cameraToScreen, screenNormal);
            float distanceToCamera = cameraToScreen.Length();

            if (distanceToCamera > _maxDistance || distanceToCamera < MinVisibleDistance)
            {
                SetInRange(false);
                SetOffScreen();
                _currentOpacity = 0f;
                return;
            }

            _currentOpacity = CalculateOpacity(distanceToCamera);
            SetInRange(true);

            cameraToScreen = cameraToScreen / distanceToCamera;
            if (Vector3.Dot(cameraToScreen, cameraForward) < ForwardDotThreshold)
            {
                SetOffScreen();
                return;
            }

            Vector3 cameraForwardNormalized = CameraProjection.SafeNormalize(cameraForward);
            float fov = mumble.PlayerCamera.FieldOfView;

            for (int i = 0; i < 4; i++)
            {
                _screenCorners[i] = CameraProjection.WorldToScreen(_cornerCalculator.WorldCorners[i], cameraPos, cameraForwardNormalized, fov);
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
            float screenWidth = GameService.Graphics.SpriteScreen.Width;
            float screenHeight = GameService.Graphics.SpriteScreen.Height;

            // Validate corners are not NaN/Infinity
            for (int i = 0; i < 4; i++)
            {
                float cornerX = _screenCorners[i].X;
                float cornerY = _screenCorners[i].Y;

                if (float.IsNaN(cornerX) || float.IsNaN(cornerY) ||
                    float.IsInfinity(cornerX) || float.IsInfinity(cornerY))
                {
                    width = height = minX = minY = 0;
                    return;
                }
            }

            // Check if any corner is behind the camera 
            const float BehindCameraThreshold = -5000f;
            for (int i = 0; i < 4; i++)
            {
                if (_screenCorners[i].X < BehindCameraThreshold || _screenCorners[i].Y < BehindCameraThreshold)
                {
                    width = height = minX = minY = 0;
                    return;
                }
            }

            //  reject if area is too small or negative
            float area = CalculateQuadArea(_screenCorners);
            const float MinAreaThreshold = 10f;
            if (Math.Abs(area) < MinAreaThreshold)
            {
                width = height = minX = minY = 0;
                return;
            }

            // Calculate AABB
            float minXf = float.MaxValue, minYf = float.MaxValue;
            float maxXf = float.MinValue, maxYf = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                float cornerX = _screenCorners[i].X;
                float cornerY = _screenCorners[i].Y;

                if (cornerX < minXf) minXf = cornerX;
                if (cornerY < minYf) minYf = cornerY;
                if (cornerX > maxXf) maxXf = cornerX;
                if (cornerY > maxYf) maxYf = cornerY;
            }

            width = (int)(maxXf - minXf);
            height = (int)(maxYf - minYf);
            minX = (int)minXf;
            minY = (int)minYf;

            // Final bounds check
            if (width > screenWidth * 1.5f || height > screenHeight * 1.5f)
            {
                width = height = minX = minY = 0;
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

            var videoBounds = new Rectangle(Location.X, Location.Y, Size.X, Size.Y);
            var mousePos = GameService.Input.Mouse.Position;
            bool isHoveringVideo = videoBounds.Contains(mousePos);

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

                _renderer.Render(graphicsDevice, viewMatrix, projectionMatrix, _videoTexture, _currentOpacity);
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
            base.DisposeControl();
        }

        #endregion
    }
}
