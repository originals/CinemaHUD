using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using CinemaModule.Services.Twitch;
using Microsoft.Xna.Framework;
using System;

namespace CinemaModule.UI.Windows.Dialogs
{
    public class TwitchAuthWindow : SmallWindow
    {
        private static readonly Logger Logger = Logger.GetLogger<TwitchAuthWindow>();

        private readonly TwitchAuthService _authService;
        private readonly Action<string, string> _onTokensChanged;

        private FlowPanel _contentPanel;
        private Label _statusLabel;
        private Label _privacyNoteLabel;
        private Label _codeLabel;
        private Label _instructionLabel;
        private FlowPanel _codeSection;
        private StandardButton _actionButton;
        private StandardButton _cancelAuthButton;
        private StandardButton _openBrowserButton;
        private StandardButton _closeButton;

        private string _currentCode;
        private string _verificationUri;

        public TwitchAuthWindow(TwitchAuthService authService, Action<string, string> onTokensChanged)
            : base("Twitch Login")
        {
            _authService = authService;
            _onTokensChanged = onTokensChanged;

            _authService.AuthStatusChanged += OnAuthStatusChanged;
            _authService.DeviceCodeReceived += OnDeviceCodeReceived;

            Initialize();
        }

        protected override void BuildContent()
        {
            _contentPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                OuterControlPadding = new Vector2(20, 20),
                ControlPadding = new Vector2(0, 15),
                Parent = this
            };

            BuildHeader();
            BuildCodeSection();
            BuildButtons();
            
            UpdateUIForAuthStatus();
        }

        private void BuildHeader()
        {
            new Label
            {
                Text = "Link your Twitch account",
                Font = GameService.Content.DefaultFont18,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Parent = _contentPanel
            };

            _statusLabel = new Label
            {
                Text = "Not connected",
                TextColor = Color.Gray,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Parent = _contentPanel
            };

            _privacyNoteLabel = new Label
            {
                Text = "Your token is stored locally only and is never shared.",
                TextColor = Color.LightGray,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Parent = _contentPanel
            };
        }

        private void BuildCodeSection()
        {
            _codeSection = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(0, 12),
                Visible = false,
                Parent = _contentPanel
            };

            _codeLabel = new Label
            {
                Text = "--------",
                Font = GameService.Content.DefaultFont32,
                TextColor = new Color(145, 70, 255),
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Parent = _codeSection
            };

            _instructionLabel = new Label
            {
                Text = "Enter code and authorize CinemaHUD",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Parent = _codeSection
            };

            var buttonRow = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                WidthSizingMode = SizingMode.AutoSize,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(10, 0),
                Parent = _codeSection
            };

            _openBrowserButton = new StandardButton
            {
                Text = "Open Activation Page",
                Width = 150,
                Parent = buttonRow
            };
            _openBrowserButton.Click += OnOpenBrowserClicked;

            _cancelAuthButton = new StandardButton
            {
                Text = "Cancel",
                Width = 80,
                Parent = buttonRow
            };
            _cancelAuthButton.Click += OnCancelAuthClicked;
        }

        private void BuildButtons()
        {
            var buttonPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(10, 0),
                OuterControlPadding = new Vector2(0, 10),
                Parent = _contentPanel
            };

            _actionButton = new StandardButton
            {
                Text = "Login",
                Width = 100,
                Parent = buttonPanel
            };
            _actionButton.Click += OnActionButtonClicked;

            _closeButton = new StandardButton
            {
                Text = "Close",
                Width = 80,
                Parent = buttonPanel
            };
            _closeButton.Click += (s, e) => Hide();
        }

        private void OnActionButtonClicked(object sender, MouseEventArgs e)
        {
            if (_authService.IsAuthenticated)
            {
                _ = _authService.LogoutAsync();
            }
            else
            {
                _actionButton.Visible = false;
                _closeButton.Visible = false;
                _ = _authService.StartDeviceAuthFlowAsync();
            }
        }

        private void OnCancelAuthClicked(object sender, MouseEventArgs e)
        {
            _authService.CancelPendingAuth();
            _currentCode = null;
            UpdateUIForAuthStatus();
        }

        private void OnOpenBrowserClicked(object sender, MouseEventArgs e)
        {
            if (!string.IsNullOrEmpty(_verificationUri))
            {
                try
                {
                    System.Diagnostics.Process.Start(_verificationUri);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to open browser");
                }
            }
        }

        private void OnDeviceCodeReceived(object sender, DeviceCodeEventArgs e)
        {
            _currentCode = e.UserCode;
            _verificationUri = e.VerificationUri;

            _codeLabel.Text = e.UserCode;
            _codeSection.Visible = true;
        }

        private void OnAuthStatusChanged(object sender, TwitchAuthStatusEventArgs e)
        {
            Logger.Debug($"Auth status changed: {e.Status} - {e.Message}");

            switch (e.Status)
            {
                case TwitchAuthStatus.Authenticated:
                    _currentCode = null;
                    _onTokensChanged?.Invoke(e.AccessToken, e.RefreshToken);
                    UpdateUIForAuthStatus();
                    break;

                case TwitchAuthStatus.NotAuthenticated:
                    _currentCode = null;
                    _onTokensChanged?.Invoke(null, null);
                    UpdateUIForAuthStatus();
                    break;

                case TwitchAuthStatus.Cancelled:
                case TwitchAuthStatus.Failed:
                    UpdateUIForAuthStatus();
                    break;
            }
        }

        private void UpdateUIForAuthStatus()
        {
            if (_authService.IsAuthenticated)
            {
                _statusLabel.Text = $"Connected as: {_authService.Username}";
                _statusLabel.TextColor = new Color(100, 200, 100);
                _actionButton.Text = "Logout";
                _actionButton.Visible = true;
                _closeButton.Visible = true;
                _codeSection.Visible = false;
                _privacyNoteLabel.Visible = false;
            }
            else
            {
                _statusLabel.Text = "Not connected";
                _statusLabel.TextColor = Color.Gray;
                _actionButton.Text = "Login";

                bool hasActiveCode = !string.IsNullOrEmpty(_currentCode);
                _actionButton.Visible = !hasActiveCode;
                _closeButton.Visible = !hasActiveCode;
                _codeSection.Visible = hasActiveCode;
                _privacyNoteLabel.Visible = !hasActiveCode;
            }
        }

        protected override void DisposeControl()
        {
            _authService.AuthStatusChanged -= OnAuthStatusChanged;
            _authService.DeviceCodeReceived -= OnDeviceCodeReceived;
            _authService.CancelPendingAuth();
            base.DisposeControl();
        }
    }
}
