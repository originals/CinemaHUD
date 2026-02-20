using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using CinemaHUD.UI.Windows.MainSettings;
using CinemaModule.Models;
using CinemaModule.VideoPlayer;
using VideoPlayerClass = CinemaModule.VideoPlayer.VideoPlayer;
using CinemaModule.Services;
using CinemaModule.Settings;
using CinemaModule.UI.Chat;
using CinemaModule.UI.VideoDisplays;
using CinemaModule.UI.Views;
using LibVLCSharp.Shared;
using Microsoft.Xna.Framework;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace CinemaModule
{
    [Export(typeof(Module))]
    public class CinemaModule : Module
    {
        #region Members

        private static readonly Logger Logger = Logger.GetLogger<CinemaModule>();

        private const int CornerIconPriority = int.MaxValue;
        private const string CacheDirectoryName = "cinema";

        private CinemaSettings _cinemaSettings;
        private CinemaUserSettings _userSettings;
        private CinemaController _controller;
        private VideoPlayerClass _videoPlayer;
        private CinemaSettingsWindow _settingsWindow;
        private WindowVideoDisplay _windowDisplayPanel;
        private WorldVideoDisplay _worldDisplayPanel;
        private Gw2MapService _mapService;
        private TwitchService _twitchService;
        private TwitchAuthService _twitchAuthService;
        private TwitchChatService _twitchChatService;
        private TwitchChatWindow _twitchChatWindow;
        private PresetService _presetService;
        private TextureService _textureService;
        private CornerIcon _cornerIcon;
        private bool _needsVideoPlayerInit;
        private bool _needsTwitchChatRestore;
        private bool _needsWindowDisplayRestore;
        private string _libvlcDir;

        #endregion

        #region Properties

        internal SettingsManager SettingsManager => ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => ModuleParameters.DirectoriesManager;
        internal static CinemaModule Instance { get; private set; }
        internal CinemaUserSettings UserSettings => _userSettings;
        internal TextureService TextureService => _textureService;

        #endregion

        [ImportingConstructor]
        public CinemaModule([Import("ModuleParameters")] ModuleParameters moduleParameters) 
            : base(moduleParameters)
        {
            Instance = this;
        }

        #region Public Methods

        protected override void DefineSettings(SettingCollection settings)
        {
            _cinemaSettings = new CinemaSettings(settings);
        }

        public override IView GetSettingsView()
        {
            return new ModuleSettingsView(
                SettingsManager.ModuleSettings,
                _userSettings,
                _twitchAuthService,
                () => _cinemaSettings?.ShowThirdPartyNotices());
        }

        protected override async Task LoadAsync()
        {
            try
            {
                _libvlcDir = DirectoriesManager.GetFullDirectoryPath("libvlc");
                Logger.Info($"LibVLC directory: {_libvlcDir}");

                var cacheDirectory = Path.Combine(DirectoryUtil.CachePath, CacheDirectoryName);
                _userSettings = new CinemaUserSettings(cacheDirectory);

                var extractor = new LibVlcService(ContentsManager);
                await extractor.ExtractAsync(_libvlcDir);


                string libvlcBinPath = LibVlcService.GetBinPath(_libvlcDir);
                
                if (!Directory.Exists(libvlcBinPath))
                {
                    Logger.Error($"LibVLC bin path does not exist: {libvlcBinPath}");
                    return;
                }

                Core.Initialize(libvlcBinPath);

                _mapService = new Gw2MapService(cacheDirectory);

                _twitchService = new TwitchService(cacheDirectory);

                _twitchAuthService = new TwitchAuthService();
                InitializeTwitchAuth();

                _twitchChatService = new TwitchChatService();

                _presetService = new PresetService(cacheDirectory);
                _presetService.PresetImagesLoaded += OnPresetImagesLoaded;
                await _presetService.LoadPresetsAsync();

                _textureService = new TextureService(cacheDirectory);

                _needsVideoPlayerInit = true;

                _controller = new CinemaController(_cinemaSettings, _userSettings, _twitchService);
                    _controller.ShowSettingsRequested += (s, e) => _settingsWindow?.ToggleWindow();
                    _controller.ShowChatRequested += OnShowChatRequested;
                    _controller.ToggleChatRequested += OnToggleChatRequested;

                    CreateCornerIcon();
                CreateSettingsWindow();
                CreateVideoDisplays();
                _needsTwitchChatRestore = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load CinemaHUD module");
            }
        }

        private void InitializeVideoPlayer()
        {
            if (_videoPlayer != null)
                return;

            var ctx = GameService.Graphics.LendGraphicsDeviceContext();
            try
            {
                _videoPlayer = new VideoPlayerClass(ctx.GraphicsDevice, VideoPlayerOptions.Default);
            }
            finally
            {
                ctx.Dispose();
            }

            _controller.RegisterPlayer(_videoPlayer);
            _controller.StartInitialPlaybackIfEnabled();
        }

        protected override void Update(GameTime gameTime)
        {
            if (_needsVideoPlayerInit)
            {
                _needsVideoPlayerInit = false;
                InitializeVideoPlayer();
            }

            if (_needsTwitchChatRestore || _needsWindowDisplayRestore)
            {
                var screenWidth = GameService.Graphics.SpriteScreen.Width;
                var screenHeight = GameService.Graphics.SpriteScreen.Height;
                if (screenWidth >= 640 && screenHeight >= 480)
                {
                    if (_needsWindowDisplayRestore)
                    {
                        _needsWindowDisplayRestore = false;
                        RestoreWindowDisplayPosition();
                    }
                    if (_needsTwitchChatRestore)
                    {
                        _needsTwitchChatRestore = false;
                        RestoreTwitchChatWindow();
                    }
                }
            }

            _controller?.Update();
        }

        protected override void Unload()
        {
            _twitchAuthService.AuthStatusChanged -= OnTwitchAuthStatusChanged;
            _twitchService.ScopeError -= OnTwitchScopeError;
            _presetService.PresetImagesLoaded -= OnPresetImagesLoaded;
            _controller.ShowChatRequested -= OnShowChatRequested;
            _controller.ToggleChatRequested -= OnToggleChatRequested;
            _controller.ChatChannelChangeRequested -= OnChatChannelChangeRequested;
            _controller?.Dispose();
            _cornerIcon?.Dispose();
            _settingsWindow?.Dispose();
            _twitchChatWindow?.Dispose();
            _windowDisplayPanel?.Dispose();
            _worldDisplayPanel?.Dispose();
            _videoPlayer?.Dispose();
            _twitchService?.Dispose();
            _twitchAuthService?.Dispose();
            _twitchChatService?.Dispose();
            _presetService?.Dispose();
            _textureService?.Dispose();
            _cinemaSettings?.Dispose();

            Instance = null;
        }

        #endregion

        #region Private Methods

        private void CreateCornerIcon()
        {
            var tvIcon = _textureService.GetCornerIcon();
            if (tvIcon == null)
            {
                Logger.Warn("Failed to load corner icon texture");
                return;
            }

            _cornerIcon = new CornerIcon
            {
                Icon = tvIcon,
                HoverIcon = tvIcon,
                BasicTooltipText = "CinemaHUD Settings",
                Priority = CornerIconPriority,
                Parent = GameService.Graphics.SpriteScreen
            };

            _cornerIcon.Click += (s, e) => _settingsWindow?.ToggleWindow();
        }

        private void CreateSettingsWindow()
        {
            var emblemTexture = _textureService.GetEmblem();

            _settingsWindow = new CinemaSettingsWindow(
                _cinemaSettings,
                _userSettings,
                _controller,
                emblemTexture,
                _mapService,
                _twitchService,
                _twitchAuthService,
                _presetService);
        }

        private void InitializeTwitchAuth()
        {
            _twitchAuthService.LoadTokens(_userSettings.TwitchAccessToken, _userSettings.TwitchRefreshToken);

            if (_twitchAuthService.IsAuthenticated)
            {
                _twitchService.SetAuthToken(_userSettings.TwitchAccessToken, _twitchAuthService.UserId);
            }

            _twitchAuthService.AuthStatusChanged += OnTwitchAuthStatusChanged;
            _twitchService.ScopeError += OnTwitchScopeError;
        }

        private void OnTwitchScopeError(object sender, EventArgs e)
        {
            Logger.Warn("Twitch token missing required scopes - forcing re-authentication");
            _ = _twitchAuthService.LogoutAsync();
        }

        private void OnTwitchAuthStatusChanged(object sender, TwitchAuthStatusEventArgs e)
        {
            if (e.Status == TwitchAuthStatus.Authenticated)
            {
                _userSettings.TwitchAccessToken = e.AccessToken;
                _userSettings.TwitchRefreshToken = e.RefreshToken;
                _twitchService.SetAuthToken(e.AccessToken, e.UserId);
                _twitchChatService.SetCredentials(e.Username, e.AccessToken);
            }
            else if (e.Status == TwitchAuthStatus.NotAuthenticated)
            {
                _userSettings.TwitchAccessToken = null;
                _userSettings.TwitchRefreshToken = null;
                _twitchService.SetAuthToken(null);
                _twitchChatService.SetCredentials(null, null);
            }
        }

        private void OnShowChatRequested(object sender, string channelName)
        {
            if (_twitchChatWindow == null)
            {
                _twitchChatWindow = new TwitchChatWindow(_twitchChatService, _twitchAuthService, _userSettings);
                _controller.ChatChannelChangeRequested += OnChatChannelChangeRequested;
            }

            _twitchChatWindow.ConnectToChannel(channelName);

            if (!_twitchChatWindow.Visible)
            {
                _twitchChatWindow.Show();
            }
        }

        private void OnToggleChatRequested(object sender, string channelName)
        {
            if (_twitchChatWindow == null)
            {
                _twitchChatWindow = new TwitchChatWindow(_twitchChatService, _twitchAuthService, _userSettings);
                _controller.ChatChannelChangeRequested += OnChatChannelChangeRequested;
            }

            if (_twitchChatWindow.Visible)
            {
                _twitchChatWindow.Hide();
            }
            else
            {
                _twitchChatWindow.ConnectToChannel(channelName);
                _twitchChatWindow.Show();
            }
        }

        private void OnChatChannelChangeRequested(object sender, string channelName)
        {
            if (_twitchChatWindow == null || !_twitchChatWindow.Visible)
                return;

            if (!string.IsNullOrEmpty(channelName))
            {
                _twitchChatWindow.ConnectToChannel(channelName);
            }
            else
            {
                _twitchChatWindow.Disconnect();
            }
        }

        private void RestoreTwitchChatWindow()
        {
            if (!_userSettings.TwitchChatWindowOpen)
                return;

            var channel = _userSettings.TwitchChatWindowChannel;
            if (string.IsNullOrEmpty(channel))
                return;

            OnShowChatRequested(this, channel);
        }

        private void CreateVideoDisplays()
        {
            _windowDisplayPanel = new WindowVideoDisplay();
            _windowDisplayPanel.Parent = GameService.Graphics.SpriteScreen;
            _windowDisplayPanel.IsLocked = _userSettings.WindowLocked;
            _needsWindowDisplayRestore = true;

            _worldDisplayPanel = new WorldVideoDisplay
            {
                WorldPosition = _userSettings.WorldPosition,
                WorldWidth = _userSettings.WorldScreenWidth,
                Parent = GameService.Graphics.SpriteScreen
            };

            _worldDisplayPanel.Initialize(GameService.Graphics.SpriteScreen);

            _controller.RegisterDisplays(_windowDisplayPanel, _worldDisplayPanel);
        }

        private void RestoreWindowDisplayPosition()
        {
            if (_windowDisplayPanel == null)
                return;

            _windowDisplayPanel.Size = _userSettings.WindowSize;
            _windowDisplayPanel.Location = _userSettings.WindowPosition;
        }

        private void OnPresetImagesLoaded(object sender, EventArgs e)
        {
            if (_userSettings.CurrentStreamSourceType != StreamSourceType.Url)
                return;

            var channelId = _userSettings.SelectedUrlChannelId;
            if (string.IsNullOrEmpty(channelId))
                return;

            var channel = _presetService.FindChannelById(channelId);
            if (channel?.IsRadio == true)
            {
                _userSettings.CurrentStreamPreset = channel.ToStreamPresetData();
            }
        }

        #endregion
    }
}
