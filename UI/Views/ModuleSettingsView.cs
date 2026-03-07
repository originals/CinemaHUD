using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Settings;
using CinemaModule.UI.Windows.Dialogs;
using CinemaModule.Services.Twitch;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using System;

namespace CinemaModule.UI.Views
{
    public class ModuleSettingsView : View
    {
        private const int BUTTON_WIDTH = 200;
        private const int BUTTON_HEIGHT = 26;
        private const int PADDING = 10;
        private const int BUTTON_SPACING = 15;

        private readonly SettingCollection _settings;
        private readonly CinemaUserSettings _userSettings;
        private readonly TwitchAuthService _twitchAuthService;
        private readonly Action _showThirdPartyNoticesAction;

        private FlowPanel _settingsPanel;
        private StandardButton _twitchButton;
        private StandardButton _thirdPartyNoticesButton;
        private TwitchAuthWindow _twitchAuthWindow;

        public ModuleSettingsView(
            SettingCollection settings,
            CinemaUserSettings userSettings,
            TwitchAuthService twitchAuthService,
            Action showThirdPartyNoticesAction)
        {
            _settings = settings;
            _userSettings = userSettings;
            _twitchAuthService = twitchAuthService;
            _showThirdPartyNoticesAction = showThirdPartyNoticesAction;
        }

        protected override void Build(Container buildPanel)
        {
            _settingsPanel = new FlowPanel
            {
                Size = buildPanel.Size,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(5, 8),
                OuterControlPadding = new Vector2(PADDING, PADDING),
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                AutoSizePadding = new Point(0, 15),
                Parent = buildPanel
            };

            BuildSettingsEntries();
            BuildButtons();

            _twitchAuthService.AuthStatusChanged += OnTwitchAuthStatusChanged;
        }

        private void BuildSettingsEntries()
        {
            foreach (var setting in _settings)
            {
                if (!setting.SessionDefined)
                    continue;

                var settingView = Blish_HUD.Settings.UI.Views.SettingView.FromType(setting, _settingsPanel.Width);
                if (settingView == null)
                    continue;

                var container = new ViewContainer
                {
                    WidthSizingMode = SizingMode.Fill,
                    HeightSizingMode = SizingMode.AutoSize,
                    Parent = _settingsPanel
                };

                container.Show(settingView);
            }
        }

        private void BuildButtons()
        {
            var buttonPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                ControlPadding = new Vector2(BUTTON_SPACING, 0),
                WidthSizingMode = SizingMode.AutoSize,
                HeightSizingMode = SizingMode.AutoSize,
                Parent = _settingsPanel
            };

            _twitchButton = new StandardButton
            {
                Text = GetTwitchButtonText(),
                Width = BUTTON_WIDTH,
                Height = BUTTON_HEIGHT,
                Parent = buttonPanel
            };
            _twitchButton.Click += OnTwitchButtonClick;

            _thirdPartyNoticesButton = new StandardButton
            {
                Text = "Third-Party Notices",
                Width = BUTTON_WIDTH,
                Height = BUTTON_HEIGHT,
                Parent = buttonPanel
            };
            _thirdPartyNoticesButton.Click += OnThirdPartyNoticesButtonClick;
        }

        private string GetTwitchButtonText()
        {
            if (_twitchAuthService.IsAuthenticated && !string.IsNullOrEmpty(_twitchAuthService.Username))
                return $"Twitch: {_twitchAuthService.Username}";
            return "Twitch Login";
        }

        private void OnTwitchButtonClick(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            ShowTwitchAuthWindow();
        }

        private void ShowTwitchAuthWindow()
        {
            if (_twitchAuthWindow == null)
            {
                _twitchAuthWindow = new TwitchAuthWindow(_twitchAuthService, OnTwitchTokensChanged);
            }
            _twitchAuthWindow.Show();
        }

        private void OnTwitchTokensChanged(string accessToken, string refreshToken)
        {
            _userSettings.TwitchAccessToken = accessToken;
            _userSettings.TwitchRefreshToken = refreshToken;
        }

        private void OnTwitchAuthStatusChanged(object sender, TwitchAuthStatusEventArgs e)
        {
            if (_twitchButton != null)
            {
                _twitchButton.Text = GetTwitchButtonText();
            }
        }

        private void OnThirdPartyNoticesButtonClick(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            _showThirdPartyNoticesAction?.Invoke();
        }

        protected override void Unload()
        {
            _twitchAuthService.AuthStatusChanged -= OnTwitchAuthStatusChanged;

            if (_twitchButton != null)
            {
                _twitchButton.Click -= OnTwitchButtonClick;
            }

            if (_thirdPartyNoticesButton != null)
            {
                _thirdPartyNoticesButton.Click -= OnThirdPartyNoticesButtonClick;
            }

            _twitchAuthWindow?.Dispose();
        }
    }
}
