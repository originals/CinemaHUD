using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using CinemaHUD.UI.Windows.MainSettings;
using CinemaModule.Player;
using CinemaModule.Services;
using CinemaModule.Settings;
using CinemaModule.UI.Displays;
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
        private VideoPlayer _videoPlayer;
        private CinemaSettingsWindow _settingsWindow;
        private WindowVideoDisplay _windowDisplayPanel;
        private WorldVideoDisplay _worldDisplayPanel;
        private Gw2MapService _mapService;
        private TwitchService _twitchService;
        private PresetService _presetService;
        private TextureService _textureService;
        private CornerIcon _cornerIcon;
        private bool _needsVideoPlayerInit;
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

        protected override async Task LoadAsync()
        {
            try
            {
                _libvlcDir = DirectoriesManager.GetFullDirectoryPath("libvlc");
                Logger.Info($"LibVLC directory: {_libvlcDir}");

                var cacheDirectory = Path.Combine(DirectoryUtil.CachePath, CacheDirectoryName);
                Logger.Info($"Settings directory: {cacheDirectory}");
                _userSettings = new CinemaUserSettings(cacheDirectory);
                Logger.Info("CinemaUserSettings initialized successfully");

                var extractor = new LibVlcService(ContentsManager);
                await extractor.ExtractAsync(_libvlcDir);


                string libvlcBinPath = LibVlcService.GetBinPath(_libvlcDir);
                Logger.Info($"Initializing LibVLC from: {libvlcBinPath}");
                
                if (!Directory.Exists(libvlcBinPath))
                {
                    Logger.Error($"LibVLC bin path does not exist: {libvlcBinPath}");
                    return;
                }

                Core.Initialize(libvlcBinPath);
                Logger.Info("LibVLC initialized successfully");

                _mapService = new Gw2MapService(cacheDirectory);
                Logger.Info("Gw2MapService initialized successfully");

                _twitchService = new TwitchService(cacheDirectory);
                Logger.Info("TwitchService initialized successfully");

                _presetService = new PresetService(cacheDirectory);
                await _presetService.LoadPresetsAsync();
                Logger.Info("PresetService initialized successfully");

                _textureService = new TextureService(cacheDirectory);
                Logger.Info("TextureService initialized successfully");

                _needsVideoPlayerInit = true;

                _controller = new CinemaController(_cinemaSettings, _userSettings, _twitchService);
                _controller.ShowSettingsRequested += (s, e) => _settingsWindow?.ToggleWindow();

                CreateCornerIcon();
                CreateSettingsWindow();
                CreateVideoDisplays();
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
                _videoPlayer = new VideoPlayer(ctx.GraphicsDevice, VideoPlayerOptions.Default);
            }
            finally
            {
                ctx.Dispose();
            }

            _controller.RegisterPlayer(_videoPlayer);
        }

        protected override void Update(GameTime gameTime)
        {
            if (_needsVideoPlayerInit)
            {
                _needsVideoPlayerInit = false;
                InitializeVideoPlayer();
            }

            _controller?.Update();
        }

        protected override void Unload()
        {
            _controller?.Dispose();
            _cornerIcon?.Dispose();
            _settingsWindow?.Dispose();
            _windowDisplayPanel?.Dispose();
            _worldDisplayPanel?.Dispose();
            _videoPlayer?.Dispose();
            _twitchService?.Dispose();
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
            Logger.Info("Creating settings window...");
            var windowTexture = _textureService.GetWindowTexture();
            var emblemTexture = _textureService.GetEmblem();
            
            if (emblemTexture == null)
            {
                Logger.Warn("Failed to load settings window emblem texture");
            }

            _settingsWindow = new CinemaSettingsWindow(
                windowTexture,
                _cinemaSettings,
                _userSettings,
                _controller,
                emblemTexture,
                _mapService,
                _twitchService,
                _presetService);
            Logger.Info("Settings window created successfully");
        }

        private void CreateVideoDisplays()
        {
            _windowDisplayPanel = new WindowVideoDisplay
            {
                Size = _userSettings.WindowSize,
                Location = _userSettings.WindowPosition,
                Parent = GameService.Graphics.SpriteScreen
            };

            _worldDisplayPanel = new WorldVideoDisplay
            {
                WorldPosition = _userSettings.WorldPosition,
                WorldWidth = _userSettings.WorldScreenWidth,
                Parent = GameService.Graphics.SpriteScreen
            };

            _worldDisplayPanel.Initialize(GameService.Graphics.SpriteScreen);

            _controller.RegisterDisplays(_windowDisplayPanel, _worldDisplayPanel);
        }

        #endregion
    }
}
