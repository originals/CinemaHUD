using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using CinemaModule.Services;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CinemaModule.UI.Chat
{
    public class TwitchChatWindow : StandardWindow
    {
        private static readonly Logger Logger = Logger.GetLogger<TwitchChatWindow>();

        private static readonly Rectangle DefaultWindowRegion = new Rectangle(0, 0, 439, 514);
        private static readonly Rectangle DefaultContentRegion = new Rectangle(10, 20, 429, 490);

        private const int MinWindowWidth = 350;
        private const int MinWindowHeight = 400;
        private const int LockButtonSize = 32;
        private const int LockButtonMargin = 8;
        private const int TitleLeftMargin = 16;
        private const int TitleBarHeight = 40;
        private const int CloseButtonWidth = 45;
        private const int TitleRightMargin = 50;
        private const int ContentPadding = 20;

        private readonly TwitchChatService _chatService;
        private readonly TwitchAuthService _authService;
        private readonly CinemaUserSettings _settings;
        private readonly AsyncTexture2D _lockIcon;
        private readonly AsyncTexture2D _lockActiveIcon;
        private TwitchChatPanel _chatPanel;
        private string _currentChannel;
        private bool _isLocked;
        private Rectangle _lockButtonBounds;
        private Point _lockedPosition;
        private string _windowTitle = "Twitch Chat";

        public string CurrentChannel => _currentChannel;

        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                _isLocked = value;
                CanResize = !value;
                _settings.TwitchChatWindowLocked = value;
                if (value)
                {
                    _lockedPosition = Location;
                }
            }
        }

        public TwitchChatWindow(TwitchChatService chatService, TwitchAuthService authService, CinemaUserSettings settings)
            : base(CinemaModule.Instance.TextureService.GetChatBackground(), DefaultWindowRegion, DefaultContentRegion)
        {
            _chatService = chatService;
            _authService = authService;
            _settings = settings;

            _lockIcon = CinemaModule.Instance.TextureService.GetLockIcon();
            _lockActiveIcon = CinemaModule.Instance.TextureService.GetLockActiveIcon();

            Parent = GameService.Graphics.SpriteScreen;
            Title = "";
            Emblem = null;
            Id = "CinemaModule_TwitchChatWindow";
            SavesPosition = true;
            SavesSize = false;
            ZIndex = -9001;
            Location = new Point(
                (GameService.Graphics.SpriteScreen.Width - Width) / 2,
                (GameService.Graphics.SpriteScreen.Height - Height) / 2);

            RestoreSavedSize();

            _isLocked = _settings.TwitchChatWindowLocked;
            CanResize = !_isLocked;

            BuildWindowContent();

            Resized += OnWindowResized;
            _authService.AuthStatusChanged += OnAuthStatusChanged;
        }

        private void OnAuthStatusChanged(object sender, TwitchAuthStatusEventArgs e)
        {
            _chatPanel.RefreshAuthStatus();
        }

        private void OnWindowResized(object sender, ResizedEventArgs e)
        {
            if (Width < MinWindowWidth || Height < MinWindowHeight)
            {
                Size = new Point(
                    Math.Max(Width, MinWindowWidth),
                    Math.Max(Height, MinWindowHeight));
            }

            _settings.TwitchChatWindowSize = Size;
            _chatPanel.Size = new Point(ContentRegion.Width - ContentPadding, ContentRegion.Height);
        }

        private void RestoreSavedSize()
        {
            var savedSize = _settings.TwitchChatWindowSize;
            if (savedSize.X >= MinWindowWidth && savedSize.Y >= MinWindowHeight)
            {
                Size = savedSize;
            }
        }

        private void BuildWindowContent()
        {
            _chatPanel = new TwitchChatPanel(_chatService)
            {
                Parent = this,
                Location = Point.Zero,
                Size = new Point(ContentRegion.Width - ContentPadding, ContentRegion.Height)
            };

            _chatPanel.MessageSent += OnChatMessageSent;
        }

        private async void OnChatMessageSent(object sender, string message)
        {
            if (!_authService.IsAuthenticated)
                return;

            try
            {
                await _chatService.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to send chat message");
            }
        }

        public async void ConnectToChannel(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return;

            if (_currentChannel == channelName.ToLowerInvariant() && _chatService.IsConnected)
                return;

            _currentChannel = channelName.ToLowerInvariant();
            _settings.TwitchChatWindowChannel = _currentChannel;
            _windowTitle = $"Chat - #{_currentChannel}";

            if (_authService.IsAuthenticated)
            {
                _chatService.SetCredentials(_authService.Username, _authService.AccessToken);
            }

            try
            {
                await _chatService.ConnectAsync(_currentChannel);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to connect to channel: {channelName}");
            }
        }

        public async void Disconnect()
        {
            try
            {
                await _chatService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to disconnect from chat");
            }

            _currentChannel = null;
            _settings.TwitchChatWindowChannel = "";
            _windowTitle = "Twitch Chat";
        }

        public override void Show()
        {
            base.Show();
            _settings.TwitchChatWindowOpen = true;
            CanResize = !_isLocked;
            if (_isLocked)
            {
                _lockedPosition = Location;
            }
        }

        public override void Hide()
        {
            base.Hide();
            _settings.TwitchChatWindowOpen = false;
        }

        protected override void DisposeControl()
        {
            _chatPanel.MessageSent -= OnChatMessageSent;
            Resized -= OnWindowResized;
            _authService.AuthStatusChanged -= OnAuthStatusChanged;
            _ = _chatService.DisconnectAsync();
            base.DisposeControl();
        }

        public override void PaintAfterChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            base.PaintAfterChildren(spriteBatch, bounds);

            var titleBounds = new Rectangle(TitleLeftMargin, 0, Width - TitleLeftMargin - TitleRightMargin, TitleBarHeight);
            spriteBatch.DrawStringOnCtrl(this, _windowTitle, Content.DefaultFont32, titleBounds, ContentService.Colors.ColonialWhite);

            PaintLockButton(spriteBatch);
        }

        private void PaintLockButton(SpriteBatch spriteBatch)
        {
            _lockButtonBounds = new Rectangle(
                AbsoluteBounds.X + AbsoluteBounds.Width - LockButtonSize - LockButtonMargin - CloseButtonWidth,
                AbsoluteBounds.Y + LockButtonMargin,
                LockButtonSize,
                LockButtonSize);

            var texture = _isLocked ? _lockActiveIcon : _lockIcon;
            if (texture == null || !texture.HasSwapped)
                return;

            bool isHovering = _lockButtonBounds.Contains(GameService.Input.Mouse.Position);
            var color = isHovering ? Color.White : new Color(220, 220, 220);
            spriteBatch.Draw(texture, _lockButtonBounds, color);
        }

        protected override void OnClick(Blish_HUD.Input.MouseEventArgs e)
        {
            var absoluteMousePos = GameService.Input.Mouse.Position;
            if (_lockButtonBounds.Contains(absoluteMousePos))
            {
                return;
            }

            base.OnClick(e);
        }

        protected override void OnLeftMouseButtonPressed(Blish_HUD.Input.MouseEventArgs e)
        {
            var absoluteMousePos = GameService.Input.Mouse.Position;

            if (_lockButtonBounds.Contains(absoluteMousePos))
            {
                IsLocked = !IsLocked;
                return;
            }

            if (_isLocked)
            {
                bool isInTitleBar = e.MousePosition.Y < TitleBarHeight;
                bool isOnCloseButton = e.MousePosition.X > Width - CloseButtonWidth && e.MousePosition.Y < TitleBarHeight;

                if (isInTitleBar && !isOnCloseButton)
                    return;
            }

            base.OnLeftMouseButtonPressed(e);
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            if (_isLocked && Dragging && Location != _lockedPosition)
            {
                Location = _lockedPosition;
            }
        }
    }
}
