using System;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using CinemaModule.Controllers;
using CinemaModule.Controllers.WatchParty;
using CinemaModule.Services;
using CinemaModule.Services.Twitch;
using CinemaModule.Services.YouTube;
using CinemaModule.Settings;
using CinemaModule.UI.Windows.Info;
using Microsoft.Xna.Framework;

namespace CinemaModule.UI.Windows.MainSettings
{
    public class CinemaSettingsWindow : TabbedWindow2
    {
        #region Members

        private const int WindowWidth = 890;
        private const int BackgroundHeight = 688;
        private const int MinWindowHeight = 460;
        private const int MaxWindowHeight = 1400;
        private const int ContentWidth = 836;
        private const int ContentHeightOffset = 49;

        private readonly CinemaSettings _settings;
        private readonly CinemaUserSettings _userSettings;
        private readonly CinemaController _controller;
        private readonly Gw2MapService _mapService;
        private readonly TwitchService _twitchService;
        private readonly TwitchAuthService _twitchAuthService;
        private readonly PresetService _presetService;
        private readonly YouTubeService _youtubeService;
        private readonly WatchPartyController _watchPartyController;

        private ThirdPartyNoticesWindow _thirdPartyNoticesWindow;
        private StandardButton _infoButton;
        private int _fixedWidth;
        private Tab _sourceTab;
        private const string SourceTabName = "Channel guide";
        private const string SourceTabDisabledName = "Channel guide (disabled while in Watch Party)";

        #endregion

        public CinemaSettingsWindow(
            CinemaSettings settings,
            CinemaUserSettings userSettings,
            CinemaController controller,
            AsyncTexture2D emblemTexture,
            AsyncTexture2D windowBackgroundTexture,
            Gw2MapService mapService,
            TwitchService twitchService,
            TwitchAuthService twitchAuthService,
            PresetService presetService,
            YouTubeService youtubeService,
            WatchPartyController watchPartyController)
            : base(
                windowBackgroundTexture,
                new Rectangle(40, 26, WindowWidth, BackgroundHeight),
                new Rectangle(70, 36, ContentWidth, BackgroundHeight - ContentHeightOffset))
        {
            _settings = settings;
            _userSettings = userSettings;
            _controller = controller;
            _mapService = mapService;
            _twitchService = twitchService;
            _twitchAuthService = twitchAuthService;
            _presetService = presetService;
            _youtubeService = youtubeService;
            _watchPartyController = watchPartyController;

            Parent = GameService.Graphics.SpriteScreen;
            Title = "CinemaHUD";
            Emblem = emblemTexture;
            Location = new Point(300, 300);
            SavesPosition = true;
            Id = "CinemaModule_SettingsWindow";
            CanResize = true;

            BuildTabs();
            BuildInfoButton();

            _fixedWidth = Width;

            TabChanged += OnTabChanged;
            Resized += OnWindowResized;
            _watchPartyController.RoomJoined += OnWatchPartyRoomChanged;
            _watchPartyController.RoomLeft += OnWatchPartyRoomChanged;
            UpdateSubtitleForCurrentTab();
            UpdateSourceTabEnabled();

            ApplySavedHeight();
        }

        private void ApplySavedHeight()
        {
            int savedHeight = _userSettings.SettingsWindowHeight;
            if (savedHeight >= MinWindowHeight && savedHeight <= MaxWindowHeight && savedHeight != Height)
            {
                Size = new Point(_fixedWidth, savedHeight);
            }
        }

        public override void Show()
        {
            base.Show();
            RestoreSelectedTab();
        }

        #region Private Methods

        private void BuildTabs()
        {
            var displayIcon = CinemaModule.Instance.TextureService.GetDisplayIcon();
            var sourceIcon = CinemaModule.Instance.TextureService.GetSourceIcon();
            var watchPartyIcon = CinemaModule.Instance.TextureService.GetWatchPartyIcon();

            var displayTab = new Tab(displayIcon, () => new DisplayTabView(_settings, _userSettings, _controller, _mapService, _presetService), "Display settings");
            Tabs.Add(displayTab);

            _sourceTab = new Tab(sourceIcon, () => new SourceTabView(_userSettings, _controller, _twitchService, _twitchAuthService, _presetService, _youtubeService), SourceTabName);
            Tabs.Add(_sourceTab);

            var watchPartyTab = new Tab(watchPartyIcon, () => new WatchPartyTabView(_watchPartyController, _youtubeService, _userSettings, _controller), "Watch Party");
            Tabs.Add(watchPartyTab);
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

        private void OnWindowResized(object sender, ResizedEventArgs e)
        {
            int clampedHeight = Math.Max(MinWindowHeight, Math.Min(Height, MaxWindowHeight));
            if (Width != _fixedWidth || Height != clampedHeight)
            {
                Size = new Point(_fixedWidth, clampedHeight);
            }

            _userSettings.SettingsWindowHeight = Height - 40;
            UpdateInfoButtonPosition();
        }

        private void UpdateInfoButtonPosition()
        {
            _infoButton.Location = new Point(ContentRegion.Width - 150, ContentRegion.Height + 10);
        }

        private void OnWatchPartyRoomChanged(object sender, EventArgs e) => UpdateSourceTabEnabled();

        private void UpdateSourceTabEnabled()
        {
            bool inRoom = _watchPartyController.IsInRoom;
            _sourceTab.Enabled = !inRoom;
            _sourceTab.Name = inRoom ? SourceTabDisabledName : SourceTabName;
        }

        #endregion

        protected override void DisposeControl()
        {
            _watchPartyController.RoomJoined -= OnWatchPartyRoomChanged;
            _watchPartyController.RoomLeft -= OnWatchPartyRoomChanged;
            _infoButton?.Dispose();
            _thirdPartyNoticesWindow?.Dispose();
            base.DisposeControl();
        }
    }
}
