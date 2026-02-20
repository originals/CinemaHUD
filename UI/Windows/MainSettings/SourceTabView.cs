using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using CinemaHUD.UI.Windows.SettingsSmall;
using CinemaModule;
using CinemaModule.Models;
using CinemaModule.Services;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaHUD.UI.Windows.MainSettings
{
    public class SourceTabView : View
    {
        private static readonly Logger Logger = Logger.GetLogger<SourceTabView>();

        private const int MenuPanelWidth = 240;
        private const int CardVerticalSpacing = 4;
        private const string KeyPrefixChannel = "channel:";
        private const string KeyPrefixPresetTwitch = "preset_twitch:";
        private const string KeyPrefixSaved = "saved:";
        private const string KeyPrefixFollowed = "followed:";
        private const string CategoryFollowed = "Followed Channels";
        private const string CategoryMyStreams = "My Streams";

        private readonly CinemaUserSettings _settings;
        private readonly CinemaController _controller;
        private readonly TwitchService _twitchService;
        private readonly TwitchAuthService _twitchAuthService;
        private readonly PresetService _presetService;

        private Menu _categoryMenu;
        private FlowPanel _contentPanel;
        private readonly Dictionary<string, ListCard> _streamCards = new Dictionary<string, ListCard>();
        private string _selectedStreamKey;
        private string _selectedCategoryId;

        private StreamEditorWindow _editorWindow;
        private TwitchAuthWindow _twitchAuthWindow;
        private CancellationTokenSource _cts;
        private CancellationTokenSource _contentCts;

        private readonly Dictionary<string, StreamCategory> _categoryLookup = new Dictionary<string, StreamCategory>();
        private StreamCardFactory _cardFactory;
        private StreamStatusLoader _statusLoader;

        public SourceTabView(
            CinemaUserSettings settings,
            CinemaController controller,
            TwitchService twitchService,
            TwitchAuthService twitchAuthService,
            PresetService presetService)
        {
            _settings = settings;
            _controller = controller;
            _twitchService = twitchService;
            _twitchAuthService = twitchAuthService;
            _presetService = presetService;
            _cts = new CancellationTokenSource();
        }

        protected override void Build(Container buildPanel)
        {
            InitializeHelpers();
            InitializeSelectedStreamKey();
            BuildCategoryMenu(buildPanel);
            BuildContentPanel(buildPanel);
            SubscribeToEvents();
            SelectInitialCategory();
        }

        private void InitializeHelpers()
        {
            _cardFactory = new StreamCardFactory(
                CinemaModule.CinemaModule.Instance.TextureService,
                _twitchService,
                () => _selectedStreamKey,
                _streamCards)
            {
                OnOpenChat = OpenTwitchChat,
                OnCopyWaypoint = CopyWaypointToClipboard,
                OnApplyWorldPosition = ApplyWorldPosition
            };
            _statusLoader = new StreamStatusLoader(_twitchService);
        }

        private void SubscribeToEvents()
        {
            _settings.SavedStreamsChanged += OnSavedStreamsChanged;
            _presetService.PresetsLoaded += OnPresetsLoaded;
            _twitchAuthService.AuthStatusChanged += OnTwitchAuthStatusChanged;
        }

        private void OnSavedStreamsChanged(object s, EventArgs e) => RefreshContent();
        private void OnPresetsLoaded(object s, EventArgs e) => RefreshContent();
        private void OnTwitchAuthStatusChanged(object s, TwitchAuthStatusEventArgs e)
        {
            if (_selectedCategoryId == CategoryFollowed)
                RefreshContent();
        }

        private void BuildCategoryMenu(Container parent)
        {
            var menuPanel = new Panel
            {
                ShowBorder = true,
                Size = new Point(MenuPanelWidth, parent.Height - 110),
                Location = new Point(23, 10),
                Title = "Categories",
                Parent = parent,
                CanScroll = true
            };

            var refreshButton = new Image
            {
                Texture = CinemaModule.CinemaModule.Instance.TextureService.GetRefreshIcon(),
                Size = new Point(24, 24),
                Location = new Point(23 + MenuPanelWidth - 34, 16),
                BasicTooltipText = "Refresh",
                Opacity = 0.6f,
                Parent = parent
            };
            refreshButton.Click += async (s, e) => await RefreshAllAsync();

            _categoryMenu = new Menu
            {
                Size = menuPanel.ContentRegion.Size,
                MenuItemHeight = 50,
                Parent = menuPanel,
                CanSelect = true
            };

            PopulateCategoryMenu();
            _categoryMenu.ItemSelected += OnCategorySelected;
        }

        private void PopulateCategoryMenu()
        {
            _categoryMenu.ClearChildren();
            _categoryLookup.Clear();

            foreach (var category in _presetService.StreamCategories)
            {
                var menuItem = _categoryMenu.AddMenuItem(category.Name);
                menuItem.Icon = category.IconTexture;
                _categoryLookup[category.Name] = category;
            }

            var followedItem = _categoryMenu.AddMenuItem(CategoryFollowed);
            followedItem.Icon = CinemaModule.CinemaModule.Instance.TextureService.GetTwitchBigIcon();

            var myStreamsItem = _categoryMenu.AddMenuItem(CategoryMyStreams);
            myStreamsItem.Icon = CinemaModule.CinemaModule.Instance.TextureService.GetEmblem();
        }

        private void BuildContentPanel(Container parent)
        {
            _contentPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Size = new Point(parent.Width - MenuPanelWidth - 90, parent.Height - 110),
                Location = new Point(MenuPanelWidth + 29, 10),
                ControlPadding = new Vector2(0, CardVerticalSpacing),
                CanScroll = true,
                ShowBorder = true,
                Parent = parent
            };
        }

        private void SelectInitialCategory()
        {
            string initialCategory = DetermineInitialCategory();
            var menuItem = _categoryMenu.Children
                .OfType<MenuItem>()
                .FirstOrDefault(m => m.Text == initialCategory);

            if (menuItem != null)
                _categoryMenu.Select(menuItem);
        }

        private string DetermineInitialCategory()
        {
            var lastCategory = _settings.LastSelectedSourceCategory;
            if (!string.IsNullOrEmpty(lastCategory) &&
                (_categoryLookup.ContainsKey(lastCategory) ||
                 lastCategory == CategoryFollowed ||
                 lastCategory == CategoryMyStreams))
            {
                return lastCategory;
            }

            return _presetService.StreamCategories.FirstOrDefault()?.Name ?? CategoryFollowed;
        }

        private void OnCategorySelected(object sender, ControlActivatedEventArgs e)
        {
            if (e.ActivatedControl is MenuItem menuItem)
            {
                _selectedCategoryId = menuItem.Text;
                _settings.LastSelectedSourceCategory = _selectedCategoryId;
                RefreshContent();
            }
        }

        private async Task RefreshAllAsync()
        {
            await _presetService.LoadPresetsAsync();
            PopulateCategoryMenu();
            ReselectCurrentCategory();
            RefreshContent();
        }

        private void ReselectCurrentCategory()
        {
            if (string.IsNullOrEmpty(_selectedCategoryId))
                return;

            var menuItem = _categoryMenu.Children
                .OfType<MenuItem>()
                .FirstOrDefault(m => m.Text == _selectedCategoryId);

            if (menuItem != null)
                _categoryMenu.Select(menuItem);
        }

        private void RefreshContent()
        {
            _contentCts?.Cancel();
            _contentCts?.Dispose();
            _contentCts = new CancellationTokenSource();
            _streamCards.Clear();

            if (_selectedCategoryId == CategoryFollowed)
                LoadFollowedContentAsync(_contentCts.Token);
            else if (_selectedCategoryId == CategoryMyStreams)
                LoadCustomContent();
            else
            {
                var category = _presetService.StreamCategories
                    .FirstOrDefault(c => c.Name == _selectedCategoryId);
                if (category != null)
                    LoadCategoryContentAsync(category, _contentCts.Token);
            }
        }

        private async void LoadFollowedContentAsync(CancellationToken token)
        {
            if (!_twitchAuthService.IsAuthenticated)
            {
                BuildCenteredLoginButton();
                return;
            }

            ShowLoadingSpinner();

            try
            {
                var followedStreams = await _twitchService.GetFollowedChannelsAsync();
                if (token.IsCancellationRequested) return;

                ReplaceSpinnerWithContent();
                BuildFollowedHeader(followedStreams.Count);

                if (followedStreams.Count == 0)
                {
                    ShowEmptyMessage("No followed channels are currently live");
                    return;
                }

                foreach (var stream in followedStreams.OrderByDescending(s => s.ViewerCount))
                {
                    if (token.IsCancellationRequested) return;
                    var key = $"{KeyPrefixFollowed}{stream.ChannelName}";
                    _cardFactory.CreateFollowedCard(_contentPanel, key, stream, SelectFollowedChannel);
                }

                _ = LoadFollowedAvatarsAsync(followedStreams, token);
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) return;
                Logger.Warn(ex, "Failed to load followed channels");
                _contentPanel.ClearChildren();
                BuildTwitchToolbar();
                ShowEmptyMessage("Failed to load followed channels");
            }
        }

        private async void LoadCategoryContentAsync(StreamCategory category, CancellationToken token)
        {
            if (category.IsTwitch)
                await LoadTwitchCategoryAsync(category, token);
            else
                await LoadStreamCategoryAsync(category, token);
        }

        private async Task LoadTwitchCategoryAsync(StreamCategory category, CancellationToken token)
        {
            var items = BuildTwitchItems(category);
            if (items.Count == 0)
            {
                ShowEmptyMessage("No Twitch channels available");
                return;
            }

            ShowLoadingSpinner();
            await _statusLoader.FetchTwitchStatusesAsync(items, token);
            if (token.IsCancellationRequested) return;

            ReplaceSpinnerWithContent();
            BuildCategoryHeader(category);

            foreach (var item in items.OrderByDescending(i => i.IsOnline).ThenByDescending(i => i.ViewerCount))
            {
                if (token.IsCancellationRequested) return;
                _cardFactory.CreateTwitchCard(_contentPanel, item, SelectTwitchChannel);
            }

            _ = LoadAvatarsAsync(items, token);
        }

        private async Task LoadStreamCategoryAsync(StreamCategory category, CancellationToken token)
        {
            var items = BuildChannelItems(category);
            if (items.Count == 0)
            {
                ShowEmptyMessage("No channels available");
                return;
            }

            ShowLoadingSpinner();
            await _statusLoader.FetchUrlStatusesAsync(items, token);
            if (token.IsCancellationRequested) return;

            ReplaceSpinnerWithContent();
            BuildCategoryHeader(category);

            var livestreams = items.Where(i => !i.IsOnDemand).OrderByDescending(i => i.IsOnline).ThenBy(i => i.Index).ToList();
            var onDemand = items.Where(i => i.IsOnDemand).OrderBy(i => i.Index).ToList();
            bool hasBothSections = livestreams.Count > 0 && onDemand.Count > 0;

            if (hasBothSections) BuildSectionHeader("Livestreams");
            foreach (var item in livestreams)
            {
                if (token.IsCancellationRequested) return;
                _cardFactory.CreateChannelCard(_contentPanel, item, SelectChannel);
            }

            if (hasBothSections) BuildSectionHeader("On Demand");
            foreach (var item in onDemand)
            {
                if (token.IsCancellationRequested) return;
                _cardFactory.CreateChannelCard(_contentPanel, item, SelectChannel);
            }

            _ = LoadAvatarsAsync(items, token);
        }

        private async void LoadCustomContent()
        {
            var customStreams = _settings.SavedStreams.Streams.ToList();
            if (customStreams.Count == 0)
            {
                BuildCustomToolbar();
                ShowEmptyMessage("No custom streams. Click '+ Add New' to create one.");
                return;
            }

            ShowLoadingSpinner();
            var statusMap = await _statusLoader.FetchCustomStreamStatusesAsync(customStreams, _contentCts.Token);
            if (_contentCts.Token.IsCancellationRequested) return;

            ReplaceSpinnerWithContent();
            BuildCustomToolbar();

            foreach (var stream in customStreams)
            {
                statusMap.TryGetValue(stream.Id, out var status);
                var key = GetSavedStreamKey(stream.Id);
                _cardFactory.CreateCustomCard(
                    _contentPanel,
                    key,
                    stream,
                    status,
                    () => _settings.DeleteSavedStream(stream.Id),
                    () => OpenEditorForEdit(stream),
                    SelectSavedStream);
            }

            _ = LoadCustomAvatarsAsync(customStreams, statusMap, _contentCts.Token);
        }

        private List<StreamListItem> BuildTwitchItems(StreamCategory category)
        {
            return category.TwitchChannelNames.Select((channelName, index) => new StreamListItem
            {
                Key = GetPresetTwitchKey(channelName),
                Title = channelName,
                TwitchChannel = channelName,
                AvatarTexture = CinemaModule.CinemaModule.Instance.TextureService.GetDefaultAvatar(),
                Index = index
            }).ToList();
        }

        private List<StreamListItem> BuildChannelItems(StreamCategory category)
        {
            return category.Channels.Select((channel, index) => new StreamListItem
            {
                Key = GetChannelKey(channel.Id),
                Title = channel.Title,
                Subtitle = string.IsNullOrEmpty(channel.Url) ? "No URL configured" : null,
                ChannelData = channel,
                AvatarTexture = channel.AvatarTexture ?? CinemaModule.CinemaModule.Instance.TextureService.GetDefaultAvatar(),
                IsOnDemand = channel.IsOnDemand,
                Index = index
            }).ToList();
        }

        private void BuildCenteredLoginButton()
        {
            _contentPanel.ClearChildren();
            var container = new Panel { WidthSizingMode = SizingMode.Fill, Height = 200, Parent = _contentPanel };
            var loginButton = new StandardButton
            {
                Text = "Login to Twitch",
                Width = 160,
                Height = 40,
                Left = (_contentPanel.Width - 160) / 2,
                Top = 80,
                Parent = container
            };
            loginButton.Click += (s, e) => ShowTwitchAuthWindow();
        }

        private void BuildTwitchToolbar()
        {
            var toolbar = new Panel { WidthSizingMode = SizingMode.Fill, Height = 40, Parent = _contentPanel };
            var text = _twitchAuthService.IsAuthenticated && !string.IsNullOrEmpty(_twitchAuthService.Username)
                ? $"Twitch: {_twitchAuthService.Username}"
                : "Twitch Login";
            var btn = new StandardButton { Text = text, Width = 140, Left = 5, Top = 5, Parent = toolbar };
            btn.Click += (s, e) => ShowTwitchAuthWindow();
        }

        private void BuildFollowedHeader(int liveCount)
        {
            var headerPanel = new Panel { WidthSizingMode = SizingMode.Fill, Height = 50, Parent = _contentPanel };
            var username = _twitchAuthService.Username ?? "Unknown";
            var statsText = liveCount > 0
                ? $"Logged in as {username}  •  {liveCount} followed channels live"
                : $"Logged in as {username}";

            new Label
            {
                Text = statsText,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                TextColor = Color.LightGray,
                Font = GameService.Content.DefaultFont14,
                Left = 10,
                Top = 15,
                Parent = headerPanel
            };

            var logoutBtn = new StandardButton
            {
                Text = "Logout",
                Width = 80,
                Height = 26,
                Left = _contentPanel.Width - 110,
                Top = 12,
                Parent = headerPanel
            };
            logoutBtn.Click += async (s, e) =>
            {
                await _twitchAuthService.LogoutAsync();
                _settings.TwitchAccessToken = string.Empty;
                _settings.TwitchRefreshToken = string.Empty;
                RefreshContent();
            };
        }

        private void BuildCustomToolbar()
        {
            var toolbar = new Panel { WidthSizingMode = SizingMode.Fill, Height = 40, Parent = _contentPanel };

            new Label
            {
                Text = "Add your own Twitch channels or streams",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                TextColor = Color.Gray,
                Left = 10,
                Top = 12,
                Parent = toolbar
            };

            var addButton = new StandardButton
            {
                Text = "+ Add New",
                Width = 100,
                Left = _contentPanel.Width - 110,
                Top = 5,
                Parent = toolbar
            };
            addButton.Click += (s, e) => OpenEditorForNew();
        }

        private void BuildCategoryHeader(StreamCategory category)
        {
            if (string.IsNullOrEmpty(category.Description) && string.IsNullOrEmpty(category.InfoUrl))
                return;

            const int margin = 10;
            var headerPanel = new Panel { WidthSizingMode = SizingMode.Fill, Parent = _contentPanel };
            int contentHeight = 0;

            if (!string.IsNullOrEmpty(category.Description))
            {
                bool hasInfoButton = !string.IsNullOrEmpty(category.InfoUrl);
                int labelWidth = _contentPanel.Width - (hasInfoButton ? 90 : 40);

                var descLabel = new Label
                {
                    Text = category.Description,
                    Width = Math.Max(labelWidth, 100),
                    AutoSizeHeight = true,
                    WrapText = true,
                    TextColor = Color.LightGray,
                    Font = GameService.Content.DefaultFont14,
                    Left = margin,
                    Top = margin,
                    Parent = headerPanel
                };
                contentHeight = descLabel.Height;
            }

            headerPanel.Height = Math.Max(contentHeight + (margin * 2), 46);

            if (!string.IsNullOrEmpty(category.InfoUrl))
            {
                var infoUrl = category.InfoUrl;
                var infoButton = new StandardButton
                {
                    Text = "Info",
                    Width = 50,
                    Height = 26,
                    Left = _contentPanel.Width - 80,
                    Top = (headerPanel.Height - 26) / 2,
                    Parent = headerPanel,
                    BasicTooltipText = "Open in browser"
                };
                infoButton.Click += (s, e) => OpenUrlInBrowser(infoUrl);
            }
        }

        private void BuildSectionHeader(string title)
        {
            var headerPanel = new Panel { WidthSizingMode = SizingMode.Fill, Height = 32, Parent = _contentPanel };
            new Label
            {
                Text = title,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                TextColor = Color.White,
                Font = GameService.Content.DefaultFont16,
                Left = 10,
                Top = 8,
                Parent = headerPanel
            };
        }

        private void ShowLoadingSpinner()
        {
            _contentPanel.ClearChildren();
            var container = new Panel { WidthSizingMode = SizingMode.Fill, Height = 100, Parent = _contentPanel };
            var spinner = new LoadingSpinner { Parent = container };
            spinner.Left = (_contentPanel.Width - spinner.Width) / 2;
            spinner.Top = (container.Height - spinner.Height) / 2;
        }

        private void ReplaceSpinnerWithContent() => _contentPanel.ClearChildren();

        private void ShowEmptyMessage(string message)
        {
            var container = new Panel { WidthSizingMode = SizingMode.Fill, Height = 40, Parent = _contentPanel };
            new Label
            {
                Text = message,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                TextColor = Color.Gray,
                Left = 10,
                Top = 12,
                Parent = container
            };
        }

        private async Task LoadFollowedAvatarsAsync(List<TwitchStreamInfo> streams, CancellationToken token)
        {
            var tasks = streams
                .Where(s => !string.IsNullOrEmpty(s.AvatarUrl))
                .Select(s => UpdateAvatarAsync($"{KeyPrefixFollowed}{s.ChannelName}", s.ChannelName, s.AvatarUrl, token));
            await Task.WhenAll(tasks);
        }

        private async Task LoadCustomAvatarsAsync(List<SavedStream> streams, Dictionary<string, StreamStatus> statusMap, CancellationToken token)
        {
            var tasks = streams
                .Where(s => s.SourceType == StreamSourceType.TwitchChannel)
                .Where(s => statusMap.TryGetValue(s.Id, out var status) && !string.IsNullOrEmpty(status.AvatarUrl))
                .Select(s => UpdateAvatarAsync(GetSavedStreamKey(s.Id), s.Value, statusMap[s.Id].AvatarUrl, token));
            await Task.WhenAll(tasks);
        }

        private async Task LoadAvatarsAsync(List<StreamListItem> items, CancellationToken token)
        {
            var tasks = items
                .Where(i => !string.IsNullOrEmpty(i.AvatarUrl ?? i.ChannelData?.Avatar) && !string.IsNullOrEmpty(i.TwitchChannel ?? i.ChannelData?.Id))
                .Select(i => UpdateAvatarAsync(i.Key, i.TwitchChannel ?? i.ChannelData?.Id, i.AvatarUrl ?? i.ChannelData?.Avatar, token));
            await Task.WhenAll(tasks);
        }

        private async Task UpdateAvatarAsync(string itemKey, string cacheKey, string avatarUrl, CancellationToken token)
        {
            try
            {
                var avatarTexture = await _twitchService.GetAvatarTextureAsync(cacheKey, avatarUrl);
                if (token.IsCancellationRequested) return;
                if (avatarTexture != null && _streamCards.TryGetValue(itemKey, out var card))
                    card.SetAvatar(avatarTexture);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to load avatar for {cacheKey}: {ex.Message}");
            }
        }

        private void InitializeSelectedStreamKey()
        {
            if (!string.IsNullOrEmpty(_settings.SelectedSavedStreamId))
            {
                _selectedStreamKey = GetSavedStreamKey(_settings.SelectedSavedStreamId);
                return;
            }

            if (_settings.CurrentStreamSourceType == StreamSourceType.TwitchChannel &&
                !string.IsNullOrEmpty(_settings.CurrentTwitchChannel))
            {
                var channelName = _settings.CurrentTwitchChannel;
                var presetChannel = _presetService.TwitchChannels
                    .FirstOrDefault(c => string.Equals(c, channelName, StringComparison.OrdinalIgnoreCase));

                if (presetChannel != null)
                    _selectedStreamKey = GetPresetTwitchKey(presetChannel);
                else if (_settings.LastSelectedSourceCategory == CategoryFollowed)
                    _selectedStreamKey = $"{KeyPrefixFollowed}{channelName}";
                else
                    _selectedStreamKey = GetPresetTwitchKey(channelName);
                return;
            }

            if (_settings.CurrentStreamSourceType == StreamSourceType.Url &&
                !string.IsNullOrEmpty(_settings.SelectedUrlChannelId))
            {
                _selectedStreamKey = GetChannelKey(_settings.SelectedUrlChannelId);
            }
        }

        private void UpdateCardSelection()
        {
            foreach (var kvp in _streamCards)
                kvp.Value.IsSelected = kvp.Key == _selectedStreamKey;
        }

        private async void SelectFollowedChannel(string channelName, string key) =>
            await SelectTwitchChannelInternal(channelName, key, "followed channel");

        private async void SelectTwitchChannel(string channelName, string key) =>
            await SelectTwitchChannelInternal(channelName, key, "Twitch channel");

        private async Task SelectTwitchChannelInternal(string channelName, string key, string description)
        {
            _selectedStreamKey = key;
            _settings.SelectTwitchChannel(channelName);
            UpdateCardSelection();
            await TrySetStreamUrlAsync(() => _twitchService.GetPlayableStreamUrlAsync(channelName), $"{description}: {channelName}");
        }

        private void SelectChannel(ChannelData channel, string key)
        {
            _selectedStreamKey = key;
            _settings.SelectUrlChannel(channel);
            UpdateCardSelection();
        }

        private async void SelectSavedStream(SavedStream stream)
        {
            _selectedStreamKey = GetSavedStreamKey(stream.Id);
            _settings.SelectSavedStream(stream);
            UpdateCardSelection();

            if (stream.SourceType == StreamSourceType.TwitchChannel)
                await TrySetStreamUrlAsync(() => _twitchService.GetPlayableStreamUrlAsync(stream.Value), $"stream: {stream.Name}");
            else
                _settings.StreamUrl = stream.Value;
        }

        private async Task TrySetStreamUrlAsync(Func<Task<string>> getUrlAsync, string streamDescription)
        {
            try
            {
                var url = await getUrlAsync();
                if (_cts.IsCancellationRequested || string.IsNullOrEmpty(url)) return;
                _settings.StreamUrl = url;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_cts.IsCancellationRequested)
                    Logger.Error(ex, $"Failed to get stream URL for {streamDescription}");
            }
        }

        private void ShowTwitchAuthWindow()
        {
            if (_twitchAuthWindow == null)
                _twitchAuthWindow = new TwitchAuthWindow(_twitchAuthService, OnTwitchTokensChanged);
            _twitchAuthWindow.Show();
        }

        private void OnTwitchTokensChanged(string accessToken, string refreshToken)
        {
            _settings.TwitchAccessToken = accessToken;
            _settings.TwitchRefreshToken = refreshToken;
        }

        private void OpenEditorForNew()
        {
            EnsureEditorWindow();
            _editorWindow.OpenForNew();
        }

        private void OpenEditorForEdit(SavedStream stream)
        {
            EnsureEditorWindow();
            _editorWindow.OpenForEdit(stream);
        }

        private void EnsureEditorWindow()
        {
            if (_editorWindow != null) return;
            _editorWindow = new StreamEditorWindow(_settings);
            _editorWindow.StreamSaved += (s, e) => RefreshContent();
            _editorWindow.StreamDeleted += (s, e) => RefreshContent();
        }

        private void OpenUrlInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to open URL in browser: {url}");
            }
        }

        private void CopyWaypointToClipboard(string waypoint)
        {
            if (string.IsNullOrEmpty(waypoint)) return;
            try
            {
                ClipboardUtil.WindowsClipboardService.SetTextAsync(waypoint);
                ScreenNotification.ShowNotification("Waypoint copied!", ScreenNotification.NotificationType.Info);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to copy waypoint to clipboard: {ex.Message}");
            }
        }

        private void ApplyWorldPosition(ChannelData channelData)
        {
            if (channelData?.Position == null || !channelData.ScreenWidth.HasValue) return;
            _settings.WorldPosition = channelData.Position;
            _settings.WorldScreenWidth = channelData.ScreenWidth.Value;
            _settings.DisplayMode = CinemaDisplayMode.InGame;
        }

        private void OpenTwitchChat(string channelName)
        {
            if (string.IsNullOrEmpty(channelName))
            {
                Logger.Warn("Cannot open Twitch chat - channel name is empty");
                return;
            }
            _controller.RequestShowChat(channelName);
        }

        private static string GetChannelKey(string channelId) => $"{KeyPrefixChannel}{channelId}";
        private static string GetPresetTwitchKey(string channel) => $"{KeyPrefixPresetTwitch}{channel}";
        private static string GetSavedStreamKey(string id) => $"{KeyPrefixSaved}{id}";

        protected override void Unload()
        {
            _contentCts?.Cancel();
            _contentCts?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();

            _categoryMenu.ItemSelected -= OnCategorySelected;
            _settings.SavedStreamsChanged -= OnSavedStreamsChanged;
            _presetService.PresetsLoaded -= OnPresetsLoaded;
            _twitchAuthService.AuthStatusChanged -= OnTwitchAuthStatusChanged;
            _editorWindow?.Dispose();
            _twitchAuthWindow?.Dispose();
            base.Unload();
        }
    }
}
