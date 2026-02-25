using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using CinemaModule.Services;
using CinemaModule.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.UI.VideoDisplays
{
    public class WindowVideoDisplay : Panel, IVideoDisplay
    {
        #region Members

        private static readonly Logger Logger = Logger.GetLogger<WindowVideoDisplay>();

        private const int BorderSize = 6;
        private const int HandleSize = 12;
        private const int MinWidth = 320;
        private const int MaxWidth = 2300;
        private const float AspectRatio = 16f / 9f;
        private const int ResizeCornerSize = 128;
        private const int TopMargin = 35;

        private Texture2D _currentTexture;
        private WindowVideoControls _controlsOverlay;
        private VideoControlsRenderer _renderer;

        private readonly AsyncTexture2D _textureResizeCorner = CinemaModule.Instance.TextureService.GetResizeCorner();
        private readonly AsyncTexture2D _textureResizeCornerActive = CinemaModule.Instance.TextureService.GetResizeCornerActive();

        private bool _isDragging;
        private bool _isResizing;
        private bool _isHoveringResizeCorner;
        private Point _dragStartMouse;
        private Point _dragStartLocation;
        private Point _dragStartSize;
        private ResizeDirection _resizeDirection;

        #endregion

        #region Events

        public event EventHandler<Point> PositionChanged;
        public event EventHandler<Point> SizeChanged;
        public event EventHandler PlayPauseClicked;
        public event EventHandler<int> VolumeChanged;
        public event EventHandler SettingsClicked;
        public event EventHandler TwitchChatClicked;
        public event EventHandler CloseClicked;
        public event EventHandler<int> QualityChanged;
        public event EventHandler<float> SeekRequested;
        public event EventHandler<bool> LockToggled;

        #endregion

        #region Properties

        public bool IsPaused
        {
            get => _controlsOverlay.IsPaused;
            set => _controlsOverlay.IsPaused = value;
        }

        public int Volume
        {
            get => _controlsOverlay.Volume;
            set => _controlsOverlay.Volume = value;
        }

        public bool IsTwitchStream
        {
            get => _controlsOverlay.IsTwitchStream;
            set => _controlsOverlay.IsTwitchStream = value;
        }

        public bool IsSeekable
        {
            get => _controlsOverlay.IsSeekable;
            set => _controlsOverlay.IsSeekable = value;
        }

        public float CurrentPosition
        {
            get => _controlsOverlay.CurrentPosition;
            set => _controlsOverlay.CurrentPosition = value;
        }

        public long Duration
        {
            get => _controlsOverlay.Duration;
            set => _controlsOverlay.Duration = value;
        }

        public bool IsLocked
        {
            get => _controlsOverlay.IsLocked;
            set => _controlsOverlay.IsLocked = value;
        }

        public string StreamTitle
        {
            get => _controlsOverlay.StreamTitle;
            set => _controlsOverlay.StreamTitle = value;
        }

        public int? ViewerCount
        {
            get => _controlsOverlay.ViewerCount;
            set => _controlsOverlay.ViewerCount = value;
        }

        public string GameName
        {
            get => _controlsOverlay.GameName;
            set => _controlsOverlay.GameName = value;
        }

        public bool IsOffline { get; set; }

        public Texture2D OfflineTexture { get; set; }

        public string RadioTrackName { get; set; }

        public new Point Size
        {
            get => base.Size;
            set
            {
                int clampedWidth = Math.Max(MinWidth, Math.Min(value.X, MaxWidth));
                int calculatedHeight = (int)(clampedWidth / AspectRatio);
                var newSize = new Point(clampedWidth, calculatedHeight);
                base.Size = newSize;

                var reclampedLocation = ClampToScreenBounds(base.Location, newSize);
                if (reclampedLocation != base.Location)
                {
                    base.Location = reclampedLocation;
                }
            }
        }

        public new Point Location
        {
            get => base.Location;
            set
            {
                var clampedLocation = ClampToScreenBounds(value, Size);
                base.Location = clampedLocation;
            }
        }

        #endregion

        private enum ResizeDirection
        {
            None,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Top,
            Bottom,
            Left,
            Right
        }

        public WindowVideoDisplay()
        {
            BackgroundColor = new Color(40, 40, 40);
            ShowBorder = false;
            Size = new Point(800, 450);
            Location = new Point(100, 50);
            ZIndex = -9001;

            _renderer = new VideoControlsRenderer(CinemaModule.Instance.TextureService);
            _controlsOverlay = new WindowVideoControls(this);
            _controlsOverlay.PlayPauseClicked += (s, e) => PlayPauseClicked?.Invoke(this, EventArgs.Empty);
            _controlsOverlay.VolumeChanged += (s, vol) => VolumeChanged?.Invoke(this, vol);
            _controlsOverlay.SettingsClicked += (s, e) => SettingsClicked?.Invoke(this, EventArgs.Empty);
            _controlsOverlay.TwitchChatClicked += (s, e) => TwitchChatClicked?.Invoke(this, EventArgs.Empty);
            _controlsOverlay.CloseClicked += (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty);
            _controlsOverlay.QualityChanged += (s, index) => QualityChanged?.Invoke(this, index);
            _controlsOverlay.SeekRequested += (s, pos) => SeekRequested?.Invoke(this, pos);
            _controlsOverlay.LockToggled += (s, locked) => LockToggled?.Invoke(this, locked);

            GameService.Input.Mouse.LeftMouseButtonReleased += OnGlobalMouseRelease;
        }

        #region Public Methods

        public void UpdateTexture(Texture2D texture)
        {
            if (texture == _currentTexture)
            {
                return;
            }

            _currentTexture = texture;
        }

        protected override void OnResized(ResizedEventArgs e)
        {
            base.OnResized(e);
            Invalidate();
            SizeChanged?.Invoke(this, Size);
        }

        protected override void OnMoved(MovedEventArgs e)
        {
            base.OnMoved(e);
            Invalidate();
            PositionChanged?.Invoke(this, Location);
        }

        public void UpdateAvailableQualities(IReadOnlyList<string> qualityNames, int selectedIndex)
        {
            _controlsOverlay.UpdateAvailableQualities(qualityNames, selectedIndex);
        }

        #endregion

        #region Private Methods

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var panelRect = new Rectangle(Location.X, Location.Y, Size.X, Size.Y);

            var mousePos = GameService.Input.Mouse.Position;
            var resizeCornerRect = new Rectangle(
                panelRect.X + panelRect.Width - ResizeCornerSize,
                panelRect.Y + panelRect.Height - ResizeCornerSize,
                ResizeCornerSize,
                ResizeCornerSize);
            _isHoveringResizeCorner = resizeCornerRect.Contains(mousePos);

            spriteBatch.Draw(ContentService.Textures.Pixel, panelRect, new Color(30, 30, 30, 230));

            var videoRect = new Rectangle(
                Location.X + BorderSize,
                Location.Y + BorderSize,
                Math.Max(1, Size.X - (BorderSize * 2)),
                Math.Max(1, Size.Y - (BorderSize * 2)));

            if (IsOffline && OfflineTexture != null && !OfflineTexture.IsDisposed)
            {
                DrawOfflineTexture(spriteBatch, videoRect);
            }
            else if (_currentTexture != null && !_currentTexture.IsDisposed)
            {
                spriteBatch.Draw(_currentTexture, videoRect, Color.White);
            }

            if (!string.IsNullOrEmpty(RadioTrackName))
            {
                _renderer.DrawRadioTrackInfo(spriteBatch, videoRect, RadioTrackName, 1f);
            }

            DrawBorder(spriteBatch, panelRect, new Color(80, 80, 80, 210));
            DrawCornerHandles(spriteBatch, panelRect);

            _controlsOverlay.Update(panelRect);
            _controlsOverlay.Draw(spriteBatch);
        }

        private void DrawOfflineTexture(SpriteBatch spriteBatch, Rectangle videoRect)
        {
            float textureAspect = (float)OfflineTexture.Width / OfflineTexture.Height;
            float targetAspect = (float)videoRect.Width / videoRect.Height;

            Rectangle destRect;
            if (textureAspect > targetAspect)
            {
                int height = (int)(videoRect.Width / textureAspect);
                destRect = new Rectangle(
                    videoRect.X,
                    videoRect.Y + (videoRect.Height - height) / 2,
                    videoRect.Width,
                    height);
            }
            else
            {
                int width = (int)(videoRect.Height * textureAspect);
                destRect = new Rectangle(
                    videoRect.X + (videoRect.Width - width) / 2,
                    videoRect.Y,
                    width,
                    videoRect.Height);
            }

            spriteBatch.Draw(OfflineTexture, destRect, Color.White);
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle panelRect, Color baseColor)
        {
            var pixel = ContentService.Textures.Pixel;

            for (int i = 0; i < BorderSize; i++)
            {
                float gradientFactor = (float)i / (BorderSize - 1);
                byte r = (byte)MathHelper.Lerp(baseColor.R * 0.4f, baseColor.R, gradientFactor);
                byte g = (byte)MathHelper.Lerp(baseColor.G * 0.4f, baseColor.G, gradientFactor);
                byte b = (byte)MathHelper.Lerp(baseColor.B * 0.4f, baseColor.B, gradientFactor);
                var layerColor = new Color(r, g, b, baseColor.A);

                int x = panelRect.X + i;
                int y = panelRect.Y + i;
                int width = panelRect.Width - (i * 2);
                int height = panelRect.Height - (i * 2);

                spriteBatch.Draw(pixel, new Rectangle(x, y, width, 1), layerColor);
                spriteBatch.Draw(pixel, new Rectangle(x, y + height - 1, width, 1), layerColor);
                spriteBatch.Draw(pixel, new Rectangle(x, y, 1, height), layerColor);
                spriteBatch.Draw(pixel, new Rectangle(x + width - 1, y, 1, height), layerColor);
            }
        }

        private void DrawCornerHandles(SpriteBatch spriteBatch, Rectangle panelRect)
        {
            if (IsLocked)
            {
                return;
            }

            var resizeTexture = _isHoveringResizeCorner || _isResizing
                ? _textureResizeCornerActive
                : _textureResizeCorner;

            if (resizeTexture != null)
            {
                var resizeRect = new Rectangle(
                    panelRect.X + panelRect.Width - ResizeCornerSize,
                    panelRect.Y + panelRect.Height - ResizeCornerSize,
                    ResizeCornerSize,
                    ResizeCornerSize);

                spriteBatch.Draw(resizeTexture, resizeRect, Color.White);
            }
        }

        protected override void OnLeftMouseButtonPressed(MouseEventArgs e)
        {
            base.OnLeftMouseButtonPressed(e);

            var mousePos = GameService.Input.Mouse.Position;

            if (_controlsOverlay != null && _controlsOverlay.HandleMouseDown(mousePos))
            {
                return;
            }

            if (IsLocked)
            {
                return;
            }

            var localPos = new Point(mousePos.X - AbsoluteBounds.X, mousePos.Y - AbsoluteBounds.Y);
            _resizeDirection = GetResizeDirection(localPos);

            _dragStartMouse = mousePos;
            _dragStartLocation = Location;
            _dragStartSize = Size;

            if (_resizeDirection != ResizeDirection.None)
            {
                _isResizing = true;
            }
            else
            {
                _isDragging = true;
            }
        }

        protected override void OnLeftMouseButtonReleased(MouseEventArgs e)
        {
            base.OnLeftMouseButtonReleased(e);
            StopDragAndResize();
        }

        private void OnGlobalMouseRelease(object sender, MouseEventArgs e)
        {
            StopDragAndResize();
        }

        private void StopDragAndResize()
        {
            _isDragging = false;
            _isResizing = false;
            _resizeDirection = ResizeDirection.None;
        }

        private ResizeDirection GetResizeDirection(Point localPos)
        {
            bool isLeft = localPos.X < HandleSize;
            bool isRight = localPos.X > Size.X - HandleSize;
            bool isTop = localPos.Y < HandleSize;
            bool isBottom = localPos.Y > Size.Y - HandleSize;

            if (isTop && isLeft) return ResizeDirection.TopLeft;
            if (isTop && isRight) return ResizeDirection.TopRight;
            if (isBottom && isLeft) return ResizeDirection.BottomLeft;
            if (isBottom && isRight) return ResizeDirection.BottomRight;
            if (isTop) return ResizeDirection.Top;
            if (isBottom) return ResizeDirection.Bottom;
            if (isLeft) return ResizeDirection.Left;
            if (isRight) return ResizeDirection.Right;

            return ResizeDirection.None;
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            if (_isDragging)
            {
                var nOffset = GameService.Input.Mouse.Position - _dragStartMouse;
                var newLocation = ClampToScreenBounds(_dragStartLocation + nOffset, Size);

                if (newLocation != Location)
                {
                    Location = newLocation;
                    Invalidate();
                }
            }
            else if (_isResizing)
            {
                HandleResize(GameService.Input.Mouse.Position);
            }

            BasicTooltipText = _controlsOverlay.CurrentTooltip;
        }

        private void HandleResize(Point mousePos)
        {
            var delta = mousePos - _dragStartMouse;
            int newWidth = _dragStartSize.X;
            int newX = _dragStartLocation.X;
            int newY = _dragStartLocation.Y;

            switch (_resizeDirection)
            {
                case ResizeDirection.BottomRight:
                    newWidth = _dragStartSize.X + delta.X;
                    break;

                case ResizeDirection.BottomLeft:
                    newWidth = _dragStartSize.X - delta.X;
                    newX = _dragStartLocation.X + delta.X;
                    break;

                case ResizeDirection.TopRight:
                    newWidth = _dragStartSize.X + delta.X;
                    newY = _dragStartLocation.Y + (_dragStartSize.Y - (int)(newWidth / AspectRatio));
                    break;

                case ResizeDirection.TopLeft:
                    newWidth = _dragStartSize.X - delta.X;
                    newX = _dragStartLocation.X + delta.X;
                    newY = _dragStartLocation.Y + (_dragStartSize.Y - (int)(newWidth / AspectRatio));
                    break;

                case ResizeDirection.Right:
                    newWidth = _dragStartSize.X + delta.X;
                    break;

                case ResizeDirection.Left:
                    newWidth = _dragStartSize.X - delta.X;
                    newX = _dragStartLocation.X + (_dragStartSize.X - newWidth);
                    break;

                case ResizeDirection.Bottom:
                    newWidth = (int)((_dragStartSize.Y + delta.Y) * AspectRatio);
                    break;

                case ResizeDirection.Top:
                    newWidth = (int)((_dragStartSize.Y - delta.Y) * AspectRatio);
                    newY = _dragStartLocation.Y + (_dragStartSize.Y - (int)(newWidth / AspectRatio));
                    break;
            }

            newWidth = Math.Max(MinWidth, Math.Min(newWidth, MaxWidth));
            int newHeight = (int)(newWidth / AspectRatio);

            var newSize = new Point(newWidth, newHeight);
            var newLoc = ClampToScreenBounds(new Point(newX, newY), newSize);

            if (newSize != Size || newLoc != Location)
            {
                Size = newSize;
                Location = newLoc;
                Invalidate();
            }
        }

        private const int MinScreenSize = 480;
        private const int DefaultScreenWidth = 640;
        private const int DefaultScreenHeight = 480;
        private const int ScreenEdgeBuffer = 100;

        private Point ClampToScreenBounds(Point location, Point size)
        {
            var screenWidth = GameService.Graphics.SpriteScreen.Width;
            var screenHeight = GameService.Graphics.SpriteScreen.Height;

            bool screenNotReady = screenWidth < MinScreenSize || screenHeight < MinScreenSize;

            if (screenNotReady)
            {
                screenWidth = DefaultScreenWidth;
                screenHeight = DefaultScreenHeight;
            }

            int minX = 0;
            int minY = TopMargin;
            int maxX = Math.Max(minX, screenWidth - ScreenEdgeBuffer);
            int maxY = Math.Max(minY, screenHeight - ScreenEdgeBuffer);

            int clampedX = Math.Max(minX, Math.Min(location.X, maxX));
            int clampedY = Math.Max(minY, Math.Min(location.Y, maxY));

            return new Point(clampedX, clampedY);
        }

        #endregion

        #region IDisposable

        protected override void DisposeControl()
        {
            GameService.Input.Mouse.LeftMouseButtonReleased -= OnGlobalMouseRelease;
            _controlsOverlay.Dispose();
            base.DisposeControl();
        }

        #endregion
    }
}
