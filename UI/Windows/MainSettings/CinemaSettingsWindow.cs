using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using CinemaModule;
using CinemaModule.Services;
using CinemaModule.Settings;
using CinemaModule.UI.Windows.Info;
using Microsoft.Xna.Framework;

namespace CinemaHUD.UI.Windows.MainSettings
{
    public class CinemaSettingsWindow : TabbedWindow2
    {
        #region Members

        // Standard Blish HUD TabbedWindow2 dimensions (matches 155985 background texture)
        private const int WindowWidth = 890;
        private const int WindowHeight = 680;
        private const int ContentWidth = 836;
        private const int ContentHeight = 631;

        private readonly CinemaSettings _settings;
        private readonly CinemaUserSettings _userSettings;
        private readonly CinemaController _controller;
        private readonly Gw2MapService _mapService;
        private readonly TwitchService _twitchService;
        private readonly TwitchAuthService _twitchAuthService;
        private readonly PresetService _presetService;

        private ThirdPartyNoticesWindow _thirdPartyNoticesWindow;
        private StandardButton _infoButton;

        #endregion

        public CinemaSettingsWindow(
            CinemaSettings settings,
            CinemaUserSettings userSettings,
            CinemaController controller,
            AsyncTexture2D emblemTexture,
            Gw2MapService mapService,
            TwitchService twitchService,
            TwitchAuthService twitchAuthService,
            PresetService presetService)
            : base(
                AsyncTexture2D.FromAssetId(155985),
                new Rectangle(40, 26, WindowWidth, WindowHeight),
                new Rectangle(70, 36, ContentWidth, ContentHeight))
        {
            _settings = settings;
            _userSettings = userSettings;
            _controller = controller;
            _mapService = mapService;
            _twitchService = twitchService;
            _twitchAuthService = twitchAuthService;
            _presetService = presetService;

            Parent = GameService.Graphics.SpriteScreen;
            Title = "CinemaHUD";
            Emblem = emblemTexture;
            Location = new Point(300, 300);
            SavesPosition = true;
            Id = "CinemaModule_SettingsWindow";

            BuildTabs();
            BuildInfoButton();

            TabChanged += OnTabChanged;
            UpdateSubtitleForCurrentTab();
        }

        public override void Show()
        {
            base.Show();
            RestoreSelectedTab();
        }

        #region Private Methods

        private void BuildTabs()
        {
            var displayIcon = CinemaModule.CinemaModule.Instance.TextureService.GetDisplayIcon();
            var sourceIcon = CinemaModule.CinemaModule.Instance.TextureService.GetSourceIcon();

            var displayTab = new Tab(displayIcon, () => new DisplayTabView(_settings, _userSettings, _controller, _mapService, _presetService), "Display settings");
            Tabs.Add(displayTab);

            var sourceTab = new Tab(sourceIcon, () => new SourceTabView(_userSettings, _controller, _twitchService, _twitchAuthService, _presetService), "Channel guide");
            Tabs.Add(sourceTab);
        }

        private void BuildInfoButton()
        {
            _infoButton = new StandardButton
            {
                Parent = this,
                Text = "Third-Party Notices",
                Width = 140,
                Location = new Point(ContentRegion.Width - 150, ContentRegion.Height + 10)
            };
            _infoButton.Click += (s, e) => ShowThirdPartyNotices();
        }

        private void ShowThirdPartyNotices()
        {
            _thirdPartyNoticesWindow = _thirdPartyNoticesWindow ?? new ThirdPartyNoticesWindow();
            _thirdPartyNoticesWindow.Show();
        }

        private void OnTabChanged(object sender, ValueChangedEventArgs<Tab> e)
        {
            UpdateSubtitleForCurrentTab();
            SaveSelectedTab();
        }

        private void UpdateSubtitleForCurrentTab()
        {
            Subtitle = SelectedTab?.Name ?? "Settings";
        }

        private void SaveSelectedTab()
        {
            if (SelectedTab == null)
                return;

            int tabIndex = Tabs.IndexOf(SelectedTab);
            if (tabIndex >= 0)
                _userSettings.SelectedSettingsTab = tabIndex;
        }

        private void RestoreSelectedTab()
        {
            int savedTabIndex = _userSettings.SelectedSettingsTab;
            if (savedTabIndex < 0 || savedTabIndex >= Tabs.Count || savedTabIndex == Tabs.IndexOf(SelectedTab))
                return;

            SelectedTab = Tabs.FromIndex(savedTabIndex);
        }

        #endregion

        protected override void DisposeControl()
        {
            _infoButton?.Dispose();
            _thirdPartyNoticesWindow?.Dispose();
            base.DisposeControl();
        }
    }
}
