using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using CinemaModule.Models.Twitch;
using CinemaModule.Services.Twitch;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace CinemaModule.UI.Chat
{
    public class TwitchChatPanel : Panel
    {
        private const int MessagePadding = 5;
        private const int MaxVisibleMessages = 300;
        private const int ScrollBarWidth = 8;
        private const int InputAreaHeight = 40;
        private const int SendButtonWidth = 50;
        private const int PauseButtonWidth = 80;

        private readonly TwitchChatService _chatService;
        private readonly List<TwitchChatMessage> _messages = new List<TwitchChatMessage>();
        private readonly List<TwitchChatMessage> _bufferedMessages = new List<TwitchChatMessage>();
        private readonly object _messagesLock = new object();
        private readonly List<Panel> _messagePanels = new List<Panel>();

        private int _currentYOffset;

        private Panel _messageContainer;
        private Panel _inputPanel;
        private TextBox _inputBox;
        private StandardButton _sendButton;
        private StandardButton _pauseButton;
        private Label _statusLabel;
        private Label _loginStatusLabel;

        private bool _isDisposed;
        private bool _isPaused;

        public event EventHandler<string> MessageSent;

        public TwitchChatPanel(TwitchChatService chatService)
        {
            _chatService = chatService;

            _chatService.MessageReceived += OnChatMessageReceived;
            _chatService.ConnectionStateChanged += OnConnectionStateChanged;

            BuildLayout();
        }

        private void BuildLayout()
        {
            BackgroundColor = new Color(0, 0, 0, 50);
            ShowBorder = true;
            ClipsBounds = true;

            _statusLabel = new Label
            {
                Parent = this,
                Location = new Point(MessagePadding, MessagePadding),
                Size = new Point(200, 20),
                Text = "Disconnected",
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.Gray
            };

            _loginStatusLabel = new Label
            {
                Parent = this,
                Location = new Point(210, MessagePadding + 2),
                Size = new Point(Width - 220, 18),
                Font = GameService.Content.DefaultFont14,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _messageContainer = new Panel
            {
                Parent = this,
                Location = new Point(0, 30),
                Size = new Point(Width, Height - 30 - InputAreaHeight - MessagePadding * 2),
                CanScroll = false,
                ShowBorder = false,
                ClipsBounds = true
            };

            _inputPanel = new Panel
            {
                Parent = this,
                Location = new Point(MessagePadding - 4, Height - InputAreaHeight - MessagePadding),
                Size = new Point(Width - MessagePadding * 2, InputAreaHeight),
                BackgroundColor = Color.Transparent,
                ShowBorder = false
            };

            _inputBox = new TextBox
            {
                Parent = _inputPanel,
                Location = new Point(5, 5),
                Size = new Point(_inputPanel.Width - SendButtonWidth - PauseButtonWidth - 20, 25),
                PlaceholderText = "[Say]",
                BackgroundColor = new Color(50, 50, 50)
            };

            _pauseButton = new StandardButton
            {
                Parent = _inputPanel,
                Location = new Point(_inputPanel.Width - SendButtonWidth - PauseButtonWidth - 10, 5),
                Size = new Point(PauseButtonWidth, 25),
                Text = "Pause"
            };

            _sendButton = new StandardButton
            {
                Parent = _inputPanel,
                Location = new Point(_inputPanel.Width - SendButtonWidth - 5, 5),
                Size = new Point(SendButtonWidth, 25),
                Text = "Send"
            };

            _inputBox.EnterPressed += OnInputEnterPressed;
            _sendButton.Click += OnSendButtonClicked;
            _pauseButton.Click += OnPauseButtonClicked;

            Resized += OnResized;

            SyncConnectionStatus();
            UpdateLoginStatusLabel();
        }

        private void SyncConnectionStatus()
        {
            bool isConnected = _chatService.IsConnected;
            _statusLabel.Text = isConnected ? "Connected" : "Disconnected";
            _statusLabel.TextColor = isConnected ? Color.LightGreen : Color.Gray;
        }

        public void RefreshAuthStatus()
        {
            SyncConnectionStatus();
            UpdateLoginStatusLabel();
        }

        private void OnResized(object sender, ResizedEventArgs e)
        {
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            _statusLabel.Size = new Point(200, 20);
            _loginStatusLabel.Size = new Point(Width - 220, 18);
            _messageContainer.Size = new Point(Width, Height - 30 - InputAreaHeight - MessagePadding * 2);

            _inputPanel.Location = new Point(MessagePadding - 4, Height - InputAreaHeight - MessagePadding - 3);
            _inputPanel.Size = new Point(Width - MessagePadding * 2, InputAreaHeight);
            _inputBox.Size = new Point(_inputPanel.Width - SendButtonWidth - PauseButtonWidth - 20, 25);
            _sendButton.Location = new Point(_inputPanel.Width - SendButtonWidth - 5, 5);

            var pauseX = _sendButton.Visible
                ? _inputPanel.Width - SendButtonWidth - PauseButtonWidth - 10
                : _inputPanel.Width - PauseButtonWidth - 5;
            _pauseButton.Location = new Point(pauseX, 5);

            UpdateLoginStatusLabel();
            RefreshMessageDisplay();
        }

        private void OnChatMessageReceived(object sender, TwitchChatMessageEventArgs e)
        {
            if (_isDisposed)
                return;

            if (_isPaused)
            {
                lock (_messagesLock)
                {
                    _bufferedMessages.Add(e.Message);
                }
                UpdatePauseButtonText();
                return;
            }

            AddMessageToDisplay(e.Message);
        }

        private void OnConnectionStateChanged(object sender, TwitchChatConnectionEventArgs e)
        {
            _statusLabel.Text = e.Status;
            _statusLabel.TextColor = e.IsConnected ? Color.LightGreen : Color.Gray;

            UpdateLoginStatusLabel();

            if (!e.IsConnected)
            {
                ClearMessages();
            }
        }

        private void AddMessageToDisplay(TwitchChatMessage message)
        {
            if (_messageContainer == null || _isDisposed)
                return;

            int availableWidth = _messageContainer.Width - ScrollBarWidth - MessagePadding * 2;
            var panel = CreateMessagePanel(message, availableWidth, _currentYOffset);

            lock (_messagesLock)
            {
                _messages.Add(message);
                _messagePanels.Add(panel);
                _currentYOffset += panel.Height + 2;

                while (_messages.Count > MaxVisibleMessages)
                {
                    RemoveOldestMessage();
                }
            }

            if (!_isPaused)
            {
                ScrollToBottom();
            }
        }

        private void ClearMessages()
        {
            lock (_messagesLock)
            {
                DisposePanels();
                _messages.Clear();
                _bufferedMessages.Clear();
            }

            _messageContainer.VerticalScrollOffset = 0;
        }

        private void DisposePanels()
        {
            foreach (var panel in _messagePanels)
            {
                panel.Dispose();
            }
            _messagePanels.Clear();
            _currentYOffset = 0;
        }

        private void RemoveOldestMessage()
        {
            if (_messagePanels.Count == 0)
                return;

            var oldestPanel = _messagePanels[0];
            int removedHeight = oldestPanel.Height + 2;

            oldestPanel.Dispose();
            _messagePanels.RemoveAt(0);
            _messages.RemoveAt(0);

            // Shift remaining panels up
            foreach (var panel in _messagePanels)
            {
                panel.Location = new Point(panel.Location.X, panel.Location.Y - removedHeight);
            }

            _currentYOffset -= removedHeight;
        }

        private void ScrollToBottom()
        {
            var contentHeight = _currentYOffset + MessagePadding;
            var containerHeight = _messageContainer.Height;

            _messageContainer.VerticalScrollOffset = contentHeight > containerHeight
                ? contentHeight - containerHeight
                : 0;
        }

        private void RefreshMessageDisplay()
        {
            List<TwitchChatMessage> messagesToRebuild;
            lock (_messagesLock)
            {
                messagesToRebuild = new List<TwitchChatMessage>(_messages);
                DisposePanels();
            }

            int availableWidth = _messageContainer.Width - ScrollBarWidth - MessagePadding * 2;

            lock (_messagesLock)
            {
                foreach (var message in messagesToRebuild)
                {
                    var panel = CreateMessagePanel(message, availableWidth, _currentYOffset);
                    _messagePanels.Add(panel);
                    _currentYOffset += panel.Height + 2;
                }
            }

            if (!_isPaused)
            {
                ScrollToBottom();
            }
        }

        private Panel CreateMessagePanel(TwitchChatMessage message, int availableWidth, int yOffset)
        {
            var panel = new Panel
            {
                Parent = _messageContainer,
                Location = new Point(MessagePadding, yOffset),
                Size = new Point(availableWidth, 0),
                BackgroundColor = Color.Transparent,
                ClipsBounds = true
            };

            var timestamp = message.Timestamp.ToString("[HH:mm] ");
            var displayName = SanitizeForDisplay(message.DisplayName);
            var messageText = SanitizeForDisplay(message.Message);

            var userColor = EnsureVisibleColor(message.UserColor);

            var messageLabel = new FormattedLabelBuilder()
                .SetWidth(availableWidth)
                .AutoSizeHeight()
                .Wrap()
                .CreatePart(timestamp, builder =>
                {
                    builder.SetTextColor(Color.Gray);
                })
                .CreatePart(displayName + ": ", builder =>
                {
                    builder.SetTextColor(userColor);
                })
                .CreatePart(messageText, builder =>
                {
                    builder.SetTextColor(message.IsAction ? userColor : Color.White);
                })
                .Build();

            messageLabel.Parent = panel;
            messageLabel.Location = new Point(0, 2);

            int totalHeight = Math.Max(20, messageLabel.Height + 3);
            panel.Size = new Point(availableWidth, totalHeight);

            return panel;
        }

        private string SanitizeForDisplay(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text.Replace('_', '-');
        }

        private Color EnsureVisibleColor(Color color)
        {
            float luminance = (0.299f * color.R + 0.587f * color.G + 0.114f * color.B) / 255f;

            if (luminance < 0.15f)
                return new Color(180, 180, 180);

            return color;
        }

        private void OnInputEnterPressed(object sender, EventArgs e)
        {
            SendCurrentMessage();
        }

        private void OnSendButtonClicked(object sender, MouseEventArgs e)
        {
            SendCurrentMessage();
        }

        private void OnPauseButtonClicked(object sender, MouseEventArgs e)
        {
            TogglePause();
        }

        private void TogglePause()
        {
            _isPaused = !_isPaused;
            _messageContainer.CanScroll = _isPaused;

            if (!_isPaused)
                FlushBufferedMessages();

            UpdatePauseButtonText();
        }

        private void FlushBufferedMessages()
        {
            List<TwitchChatMessage> messagesToAdd;

            lock (_messagesLock)
            {
                messagesToAdd = new List<TwitchChatMessage>(_bufferedMessages);
                _bufferedMessages.Clear();
            }

            foreach (var message in messagesToAdd)
            {
                AddMessageToDisplay(message);
            }
        }

        private void UpdatePauseButtonText()
        {
            if (!_isPaused)
            {
                _pauseButton.Text = "Pause";
                return;
            }

            int bufferedCount;
            lock (_messagesLock)
            {
                bufferedCount = _bufferedMessages.Count;
            }
            _pauseButton.Text = bufferedCount > 0 ? $"Resume ({bufferedCount})" : "Resume";
        }

        private void SendCurrentMessage()
        {
            var text = _inputBox.Text?.Trim();

            if (string.IsNullOrEmpty(text))
                return;

            _inputBox.Text = string.Empty;
            MessageSent?.Invoke(this, text);
        }

        private void UpdateLoginStatusLabel()
        {
            var isAuthenticated = _chatService.IsAuthenticated;
            var canSend = isAuthenticated && _chatService.IsConnected;

            _loginStatusLabel.Text = isAuthenticated
                ? $"as: {_chatService.Username}"
                : "Not logged in - read only";
            _loginStatusLabel.TextColor = isAuthenticated ? Color.LightGreen : Color.Gray;

            _sendButton.Visible = isAuthenticated;
            _sendButton.Enabled = canSend;
            _inputBox.Visible = isAuthenticated;
            _inputBox.Enabled = canSend;

            var pauseX = isAuthenticated
                ? _inputPanel.Width - SendButtonWidth - PauseButtonWidth - 10
                : _inputPanel.Width - PauseButtonWidth - 5;
            _pauseButton.Location = new Point(pauseX, 5);
        }

        protected override void DisposeControl()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _chatService.MessageReceived -= OnChatMessageReceived;
            _chatService.ConnectionStateChanged -= OnConnectionStateChanged;
            _inputBox.EnterPressed -= OnInputEnterPressed;
            _sendButton.Click -= OnSendButtonClicked;
            _pauseButton.Click -= OnPauseButtonClicked;
            Resized -= OnResized;

            base.DisposeControl();
        }
    }
}