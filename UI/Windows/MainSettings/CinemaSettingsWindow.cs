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

        private static readonly Logger Logger = Logger.GetLogger<CinemaSettingsWindow>();

        private const int WindowWidth = 560;
        private const int WindowHeight = 645;
        private const int ContentWidth = 520;
        private const int ContentHeight = 555;

        private readonly CinemaSettings _settings;
        private readonly CinemaUserSettings _userSettings;
        private readonly CinemaController _controller;
        private readonly Gw2MapService _mapService;
        private readonly TwitchService _twitchService;
        private readonly PresetService _presetService;
        private readonly AsyncTexture2D _emblemTexture;

        private ThirdPartyNoticesWindow _thirdPartyNoticesWindow;
        private StandardButton _infoButton;

        #endregion

        public CinemaSettingsWindow(
            AsyncTexture2D backgroundTexture,
            CinemaSettings settings,
            CinemaUserSettings userSettings,
            CinemaController controller,
            AsyncTexture2D emblemTexture,
            Gw2MapService mapService,
            TwitchService twitchService,
            PresetService presetService)
            : base(
                backgroundTexture,
                new Rectangle(25, 26, WindowWidth, WindowHeight),
                new Rectangle(40, 50, ContentWidth, ContentHeight))
        {
            _settings = settings;
            _userSettings = userSettings;
            _controller = controller;
            _mapService = mapService;
            _twitchService = twitchService;
            _presetService = presetService;
            _emblemTexture = emblemTexture;

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


        #region Private Methods

        private void BuildTabs()
        {
            var displayIcon = CinemaModule.CinemaModule.Instance.TextureService.GetDisplayIcon();
            var sourceIcon = CinemaModule.CinemaModule.Instance.TextureService.GetSourceIcon();

            var displayTab = new Tab(displayIcon, () => new DisplayTabView(_settings, _userSettings, _controller, _mapService, _presetService), "Display");
            Tabs.Add(displayTab);

            var sourceTab = new Tab(sourceIcon, () => new SourceTabView(_userSettings, _controller, _twitchService, _presetService), "Source");
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
            if (_thirdPartyNoticesWindow == null)
            {
                _thirdPartyNoticesWindow = new ThirdPartyNoticesWindow();
            }
            _thirdPartyNoticesWindow.Show();
        }

        private void OnTabChanged(object sender, ValueChangedEventArgs<Tab> e)
        {
            UpdateSubtitleForCurrentTab();
        }

        private void UpdateSubtitleForCurrentTab()
        {
            if (SelectedTab == null)
            {
                Subtitle = "Settings";
                return;
            }

            switch (SelectedTab.Name)
            {
                case "Display":
                    Subtitle = "Display settings";
                    break;
                case "Source":
                    Subtitle = "Stream settings";
                    break;
                default:
                    Subtitle = "Settings";
                    break;
            }
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
