using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using CinemaModule.UI.Windows.MainSettings;
using CinemaModule.Models;
using CinemaModule.VideoPlayer;
using VideoPlayerClass = CinemaModule.VideoPlayer.VideoPlayer;
using CinemaModule.Services;
using CinemaModule.Services.Twitch;
using CinemaModule.Services.YouTube;
using CinemaModule.Settings;
using CinemaModule.UI.Chat;
using CinemaModule.Controllers;
using CinemaModule.Controllers.WatchParty;
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
        private YouTubeService _youtubeService;
        private TwitchAuthService _twitchAuthService;
        private TwitchChatService _twitchChatService;
        private TwitchChatWindow _twitchChatWindow;
        private PresetService _presetService;
        private TextureService _textureService;
        private WatchPartyController _watchPartyController;
        private CornerIcon _cornerIcon;
        private bool _needsVideoPlayerInit;
        private bool _needsTwitchChatRestore;
        private bool _needsWindowDisplayRestore;
        private string _libvlcDir;
        private bool _libvlcInitialized;

        #endregion

        #region Properties

        internal const string ModuleVersion = "1.3.0";
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

                _ = Task.Run(async () => await InitializeLibVlcAsync());

                _mapService = new Gw2MapService(cacheDirectory);

                _twitchService = new TwitchService();
                _youtubeService = new YouTubeService();

                _twitchAuthService = new TwitchAuthService();
                InitializeTwitchAuth();

                _twitchChatService = new TwitchChatService();

                _textureService = new TextureService(cacheDirectory);

                _presetService = new PresetService(_textureService);
                _presetService.PresetImagesLoaded += OnPresetImagesLoaded;
                _ = _presetService.LoadPresetsAsync();

                _needsVideoPlayerInit = true;

                _watchPartyController = new WatchPartyController(ModuleParameters.Gw2ApiManager, _youtubeService);

                _controller = new CinemaController(_cinemaSettings, _userSettings, _twitchService, _youtubeService);
                    _controller.ShowSettingsRequested += (s, e) => _settingsWindow?.ToggleWindow();
                    _controller.ShowChatRequested += OnShowChatRequested;
                    _controller.ToggleChatRequested += OnToggleChatRequested;
                    _controller.RegisterWatchParty(_watchPartyController);

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

        private async Task InitializeLibVlcAsync()
        {
            try
            {
                var extractor = new LibVlcService(ContentsManager);
                await extractor.ExtractAsync(_libvlcDir).ConfigureAwait(false);

                string libvlcBinPath = LibVlcService.GetBinPath(_libvlcDir);

                if (!Directory.Exists(libvlcBinPath))
                {
                    Logger.Error($"LibVLC bin path does not exist: {libvlcBinPath}");
                    return;
                }

                Core.Initialize(libvlcBinPath);
                _libvlcInitialized = true;
                Logger.Info("LibVLC initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize LibVLC");
            }
        }

        private void InitializeVideoPlayer()
        {
            if (_videoPlayer != null || !_libvlcInitialized)
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
            if (_needsVideoPlayerInit && _libvlcInitialized)
            {
                _needsVideoPlayerInit = false;
                InitializeVideoPlayer();
            }

            ProcessDeferredRestoration();
            _controller?.Update();
        }

        private void ProcessDeferredRestoration()
        {
            if (!_needsTwitchChatRestore && !_needsWindowDisplayRestore)
                return;

            if (!IsScreenSizeValid())
                return;

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

        private static bool IsScreenSizeValid()
        {
            const int MinWidth = 640;
            const int MinHeight = 480;
            return GameService.Graphics.SpriteScreen.Width >= MinWidth 
                && GameService.Graphics.SpriteScreen.Height >= MinHeight;
        }

        protected override void Unload()
        {
            SafeUnsubscribe(() => _twitchAuthService.AuthStatusChanged -= OnTwitchAuthStatusChanged);
            SafeUnsubscribe(() => _twitchService.ScopeError -= OnTwitchScopeError);
            SafeUnsubscribe(() => _presetService.PresetImagesLoaded -= OnPresetImagesLoaded);
            SafeUnsubscribe(() => _controller.ShowChatRequested -= OnShowChatRequested);
            SafeUnsubscribe(() => _controller.ToggleChatRequested -= OnToggleChatRequested);
            SafeUnsubscribe(() => _controller.ChatChannelChangeRequested -= OnChatChannelChangeRequested);

            SafeDispose(_controller);
            SafeDispose(_cornerIcon);
            SafeDispose(_settingsWindow);
            SafeDispose(_twitchChatWindow);
            SafeDispose(_windowDisplayPanel);
            SafeDispose(_worldDisplayPanel);
            SafeDispose(_videoPlayer);
            SafeDispose(_twitchService);
            SafeDispose(_youtubeService);
            SafeDispose(_twitchAuthService);
            SafeDispose(_twitchChatService);
            SafeDispose(_watchPartyController);
            SafeDispose(_presetService);
            SafeDispose(_textureService);
            SafeDispose(_mapService);
            SafeDispose(_cinemaSettings);

            Instance = null;
        }

        private static void SafeUnsubscribe(Action unsubscribe)
        {
            try { unsubscribe(); } catch { }
        }

        private static void SafeDispose(IDisposable disposable)
        {
            try { disposable?.Dispose(); } catch { }
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
            var windowBackgroundTexture = _textureService.GetTabbedWindowBackground();

            _settingsWindow = new CinemaSettingsWindow(
                _cinemaSettings,
                _userSettings,
                _controller,
                emblemTexture,
                windowBackgroundTexture,
                _mapService,
                _twitchService,
                _twitchAuthService,
                _presetService,
                _youtubeService,
                _watchPartyController);
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
                ApplyTwitchCredentials(e);
            else if (e.Status == TwitchAuthStatus.NotAuthenticated)
                ClearTwitchCredentials();
        }

        private void ApplyTwitchCredentials(TwitchAuthStatusEventArgs e)
        {
            _userSettings.TwitchAccessToken = e.AccessToken;
            _userSettings.TwitchRefreshToken = e.RefreshToken;
            _twitchService.SetAuthToken(e.AccessToken, e.UserId);
            _twitchChatService.SetCredentials(e.Username, e.AccessToken);
        }

        private void ClearTwitchCredentials()
        {
            _userSettings.TwitchAccessToken = null;
            _userSettings.TwitchRefreshToken = null;
            _twitchService.SetAuthToken(null);
            _twitchChatService.SetCredentials(null, null);
        }

        private void OnShowChatRequested(object sender, string channelName)
        {
            EnsureChatWindowCreated();
            _twitchChatWindow.ConnectToChannel(channelName);

            if (!_twitchChatWindow.Visible)
                _twitchChatWindow.Show();
        }

        private void OnToggleChatRequested(object sender, string channelName)
        {
            EnsureChatWindowCreated();

            if (_twitchChatWindow.Visible)
            {
                _twitchChatWindow.Hide();
                return;
            }

            _twitchChatWindow.ConnectToChannel(channelName);
            _twitchChatWindow.Show();
        }

        private void EnsureChatWindowCreated()
        {
            if (_twitchChatWindow != null)
                return;

            _twitchChatWindow = new TwitchChatWindow(_twitchChatService, _twitchAuthService, _userSettings);
            _controller.ChatChannelChangeRequested += OnChatChannelChangeRequested;
        }

        private void OnChatChannelChangeRequested(object sender, string channelName)
        {
            if (_twitchChatWindow == null || !_twitchChatWindow.Visible)
                return;

            if (string.IsNullOrEmpty(channelName))
            {
                _twitchChatWindow.Disconnect();
                return;
            }

            _twitchChatWindow.ConnectToChannel(channelName);
        }

        private void RestoreTwitchChatWindow()
        {
            if (!_userSettings.TwitchChatWindowOpen)
                return;

            var channel = _userSettings.TwitchChatWindowChannel;
            if (!string.IsNullOrEmpty(channel))
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
