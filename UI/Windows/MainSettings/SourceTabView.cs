using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using CinemaModule.UI.Windows.Dialogs;
using CinemaModule.Controllers;
using CinemaModule.Models;
using CinemaModule.Models.Twitch;
using CinemaModule.Services;
using CinemaModule.Services.Twitch;
using CinemaModule.Services.YouTube;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaModule.UI.Windows.MainSettings
{
    public class SourceTabView : View
    {
        private static readonly Logger Logger = Logger.GetLogger<SourceTabView>();

        private const int MenuPanelWidth = 240;
        private const int CardVerticalSpacing = 4;
        private const int VerticalPadding = 110;
        private const string KeyPrefixChannel = "channel:";
        private const string KeyPrefixPresetTwitch = "preset_twitch:";
        private const string KeyPrefixSaved = "saved:";
        private const string KeyPrefixFollowed = "followed:";
        private const string KeyPrefixCustomTab = "customtab:";
        private const string CategoryFollowed = "Followed Channels";

        private readonly CinemaUserSettings _settings;
        private readonly CinemaController _controller;
        private readonly TwitchService _twitchService;
        private readonly TwitchAuthService _twitchAuthService;
        private readonly PresetService _presetService;
        private readonly YouTubeService _youtubeService;

        private Menu _categoryMenu;
        private Panel _menuPanel;
        private Panel _contentContainer;
        private Panel _headerSection;
        private FlowPanel _cardsPanel;
        private readonly Dictionary<string, ListCard> _streamCards = new Dictionary<string, ListCard>();
        private string _selectedStreamKey;
        private string _selectedCategoryId;

        private StreamEditorWindow _editorWindow;
        private TwitchAuthWindow _twitchAuthWindow;
        private CancellationTokenSource _contentCts;
        private CancellationTokenSource _selectionCts;

        private readonly Dictionary<string, StreamCategory> _categoryLookup = new Dictionary<string, StreamCategory>();
        private readonly Dictionary<string, MenuItem> _customTabMenuItems = new Dictionary<string, MenuItem>();
        private StreamCardFactory _cardFactory;
        private StreamStatusLoader _statusLoader;
        private Panel _addTabPanel;
        private TextBox _editingTextBox;
        private string _editingTabId;

        public SourceTabView(
            CinemaUserSettings settings,
            CinemaController controller,
            TwitchService twitchService,
            TwitchAuthService twitchAuthService,
            PresetService presetService,
            YouTubeService youtubeService)
        {
            _settings = settings;
            _controller = controller;
            _twitchService = twitchService;
            _twitchAuthService = twitchAuthService;
            _presetService = presetService;
            _youtubeService = youtubeService;
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
                CinemaModule.Instance.TextureService,
                _twitchService,
                () => _selectedStreamKey,
                _streamCards)
            {
                OnOpenChat = OpenTwitchChat,
                OnCopyWaypoint = CopyWaypointToClipboard,
                OnApplyWorldPosition = ApplyWorldPosition
            };
            _statusLoader = new StreamStatusLoader(_twitchService, _youtubeService);
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
            _menuPanel = new Panel
            {
                ShowBorder = true,
                Size = new Point(MenuPanelWidth, parent.Height - VerticalPadding),
                Location = new Point(23, 10),
                Title = "Categories",
                Parent = parent,
                CanScroll = true
            };

            var refreshButton = new Image
            {
                Texture = CinemaModule.Instance.TextureService.GetRefreshIcon(),
                Size = new Point(24, 24),
                Location = new Point(23 + MenuPanelWidth - 34, 16),
                BasicTooltipText = "Refresh categories and stream status",
                Opacity = 0.6f,
                Parent = parent
            };
            refreshButton.Click += async (s, e) => await RefreshAllAsync();

            _categoryMenu = new Menu
            {
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                MenuItemHeight = 50,
                Parent = _menuPanel,
                CanSelect = true
            };

            _addTabPanel = new Panel
            {
                Size = new Point(_menuPanel.ContentRegion.Width, 36),
                Parent = _menuPanel
            };

            var addTabButton = new StandardButton
            {
                Text = "+ Add Category",
                Size = new Point(_menuPanel.ContentRegion.Width - 10, 28),
                Location = new Point(5, 4),
                Parent = _addTabPanel,
                BasicTooltipText = "Create a custom category to manage your own streams and videos"
            };
            addTabButton.Click += (s, e) => StartAddingNewTab();

            PopulateCategoryMenu();
            _categoryMenu.ItemSelected += OnCategorySelected;
            parent.Resized += OnParentResized;
        }

        private void PopulateCategoryMenu()
        {
            _categoryMenu.ClearChildren();
            _categoryLookup.Clear();
            _customTabMenuItems.Clear();

            foreach (var category in _presetService.StreamCategories)
            {
                var menuItem = _categoryMenu.AddMenuItem(category.Name);
                menuItem.Icon = category.IconTexture;
                _categoryLookup[category.Name] = category;
            }

            var followedItem = _categoryMenu.AddMenuItem(CategoryFollowed);
            followedItem.Icon = CinemaModule.Instance.TextureService.GetTwitchBigIcon();

            foreach (var tab in _settings.SavedStreams.Tabs)
            {
                var tabItem = _categoryMenu.AddMenuItem(tab.Name);
                tabItem.Icon = CinemaModule.Instance.TextureService.GetEmblem();
                tabItem.BasicTooltipText = $"Custom category: {tab.Name}";
                _customTabMenuItems[tab.Id] = tabItem;
            }

            UpdateAddTabPanelPosition();
        }

        private void UpdateAddTabPanelPosition()
        {
            int menuHeight = _categoryMenu.Children.Count() * _categoryMenu.MenuItemHeight;
            _addTabPanel.Location = new Point(0, menuHeight);
        }

        private void BuildContentPanel(Container parent)
        {
            _contentContainer = new Panel
            {
                Size = new Point(parent.Width - MenuPanelWidth - 90, parent.Height - VerticalPadding),
                Location = new Point(MenuPanelWidth + 29, 10),
                ShowBorder = true,
                Parent = parent
            };

            _headerSection = new Panel
            {
                WidthSizingMode = SizingMode.Fill,
                Height = 0,
                Parent = _contentContainer
            };

            _cardsPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Size = new Point(_contentContainer.ContentRegion.Width, _contentContainer.ContentRegion.Height),
                Location = new Point(0, 0),
                ControlPadding = new Vector2(0, CardVerticalSpacing),
                CanScroll = true,
                Parent = _contentContainer
            };
        }

        private void OnParentResized(object sender, ResizedEventArgs e)
        {
            var parent = (Container)sender;
            int newHeight = parent.Height - VerticalPadding;
            int newWidth = parent.Width - MenuPanelWidth - 90;

            _menuPanel.Height = newHeight;
            _contentContainer.Size = new Point(newWidth, newHeight);
            _cardsPanel.Width = _contentContainer.ContentRegion.Width;
            UpdateCardsPanelLayout();
        }

        private void SelectInitialCategory()
        {
            string initialCategory = DetermineInitialCategory();
            MenuItem menuItem;

            if (initialCategory.StartsWith(KeyPrefixCustomTab))
            {
                string tabId = initialCategory.Substring(KeyPrefixCustomTab.Length);
                _customTabMenuItems.TryGetValue(tabId, out menuItem);
            }
            else
            {
                menuItem = _categoryMenu.Children
                    .OfType<MenuItem>()
                    .FirstOrDefault(m => m.Text == initialCategory);
            }

            if (menuItem != null)
                _categoryMenu.Select(menuItem);
        }

        private string DetermineInitialCategory()
        {
            var lastCategory = _settings.LastSelectedSourceCategory;
            if (!string.IsNullOrEmpty(lastCategory))
            {
                if (_categoryLookup.ContainsKey(lastCategory) || lastCategory == CategoryFollowed)
                    return lastCategory;

                if (lastCategory.StartsWith(KeyPrefixCustomTab))
                {
                    string tabId = lastCategory.Substring(KeyPrefixCustomTab.Length);
                    if (_settings.SavedStreams.Tabs.Exists(t => t.Id == tabId))
                        return lastCategory;
                }
            }

            return _presetService.StreamCategories.FirstOrDefault()?.Name ?? CategoryFollowed;
        }

        private void OnCategorySelected(object sender, ControlActivatedEventArgs e)
        {
            if (!(e.ActivatedControl is MenuItem menuItem))
                return;

            string tabId = GetTabIdFromMenuItem(menuItem);
            _selectedCategoryId = tabId != null ? KeyPrefixCustomTab + tabId : menuItem.Text;
            _settings.LastSelectedSourceCategory = _selectedCategoryId;
            RefreshContent();
        }

        private string GetTabIdFromMenuItem(MenuItem menuItem)
        {
            return _customTabMenuItems.FirstOrDefault(kvp => kvp.Value == menuItem).Key;
        }

        private async Task RefreshAllAsync()
        {
            _youtubeService.ClearVideoInfoCache();
            await _presetService.LoadPresetsAsync();
            PopulateCategoryMenu();
            ReselectCurrentCategory();
            RefreshContent();
        }

        private void ReselectCurrentCategory()
        {
            if (string.IsNullOrEmpty(_selectedCategoryId))
                return;

            MenuItem menuItem;
            if (_selectedCategoryId.StartsWith(KeyPrefixCustomTab))
            {
                string tabId = _selectedCategoryId.Substring(KeyPrefixCustomTab.Length);
                _customTabMenuItems.TryGetValue(tabId, out menuItem);
            }
            else
            {
                menuItem = _categoryMenu.Children
                    .OfType<MenuItem>()
                    .FirstOrDefault(m => m.Text == _selectedCategoryId);
            }

            if (menuItem != null)
                _categoryMenu.Select(menuItem);
        }

        private void UpdateCardsPanelLayout()
        {
            _cardsPanel.Location = new Point(0, _headerSection.Height);
            _cardsPanel.Height = _contentContainer.ContentRegion.Height - _headerSection.Height;
        }

        private void RefreshContent()
        {
            _contentCts?.Cancel();
            _contentCts?.Dispose();
            _contentCts = new CancellationTokenSource();
            _streamCards.Clear();
            _headerSection.ClearChildren();
            _headerSection.Height = 0;
            _cardsPanel.ClearChildren();
            UpdateCardsPanelLayout();

            if (_selectedCategoryId == CategoryFollowed)
                LoadFollowedContentAsync(_contentCts.Token);
            else if (IsCustomTab(_selectedCategoryId))
                LoadCustomTabContent(_contentCts.Token);
            else
            {
                var category = _presetService.StreamCategories
                    .FirstOrDefault(c => c.Name == _selectedCategoryId);
                if (category != null)
                    LoadCategoryContentAsync(category, _contentCts.Token);
            }
        }

        private bool IsCustomTab(string categoryId) =>
            categoryId != null && categoryId.StartsWith(KeyPrefixCustomTab);

        private CustomStreamTab GetCurrentCustomTab()
        {
            if (!IsCustomTab(_selectedCategoryId))
                return null;

            string tabId = _selectedCategoryId.Substring(KeyPrefixCustomTab.Length);
            return _settings.SavedStreams.Tabs.Find(t => t.Id == tabId);
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
                    _cardFactory.CreateFollowedCard(_cardsPanel, key, stream, SelectFollowedChannel);
                }

                _ = LoadFollowedAvatarsAsync(followedStreams, token);
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) return;
                Logger.Warn(ex, "Failed to load followed channels");
                _cardsPanel.ClearChildren();
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
                _cardFactory.CreateTwitchCard(_cardsPanel, item, SelectTwitchChannel);
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
                _cardFactory.CreateChannelCard(_cardsPanel, item, SelectChannel);
            }

            if (hasBothSections) BuildSectionHeader("On Demand");
            foreach (var item in onDemand)
            {
                if (token.IsCancellationRequested) return;
                _cardFactory.CreateChannelCard(_cardsPanel, item, SelectChannel);
            }

            _ = LoadAvatarsAsync(items, token);
        }

        private async void LoadCustomTabContent(CancellationToken token)
        {
            var currentTab = GetCurrentCustomTab();
            if (currentTab == null) return;

            var customStreams = _settings.SavedStreams.Streams
                .Where(s => s.TabId == currentTab.Id)
                .ToList();

            BuildCustomToolbar(currentTab);

            if (customStreams.Count == 0)
            {
                ShowEmptyMessage("No streams in this tab. Click '+ Add New' to create one.");
                return;
            }

            foreach (var stream in customStreams)
            {
                var key = GetSavedStreamKey(stream.Id);
                _cardFactory.CreateCustomCard(
                    _cardsPanel,
                    key,
                    stream,
                    null,
                    () => _settings.DeleteSavedStream(stream.Id),
                    () => OpenEditorForEdit(stream),
                    SelectSavedStream);
            }

            _ = LoadYouTubeThumbnailsImmediatelyAsync(customStreams, token);
            _ = FetchAndApplyCustomStatusesAsync(customStreams, token);
        }

        private async Task LoadYouTubeThumbnailsImmediatelyAsync(List<SavedStream> streams, CancellationToken token)
        {
            var youtubeTasks = streams
                .Where(s => s.SourceType == StreamSourceType.YouTubeVideo && !string.IsNullOrEmpty(s.Value))
                .Select(s => UpdateYouTubeThumbnailAsync(GetSavedStreamKey(s.Id), s.Value, token));
            await Task.WhenAll(youtubeTasks);
        }

        private async Task FetchAndApplyCustomStatusesAsync(List<SavedStream> streams, CancellationToken token)
        {
            var statusMap = await _statusLoader.FetchCustomStreamStatusesAsync(streams, token);
            if (token.IsCancellationRequested) return;

            foreach (var stream in streams)
            {
                if (token.IsCancellationRequested) return;
                if (!statusMap.TryGetValue(stream.Id, out var status)) continue;

                var key = GetSavedStreamKey(stream.Id);
                if (_streamCards.TryGetValue(key, out var card))
                    card.SetSubtitle(status.Subtitle, status.SubtitleColor);
            }

            var twitchTasks = streams
                .Where(s => s.SourceType == StreamSourceType.TwitchChannel)
                .Where(s => statusMap.TryGetValue(s.Id, out var status) && !string.IsNullOrEmpty(status.AvatarUrl))
                .Select(s => UpdateAvatarAsync(GetSavedStreamKey(s.Id), s.Value, statusMap[s.Id].AvatarUrl, token));
            await Task.WhenAll(twitchTasks);
        }

        private List<StreamListItem> BuildTwitchItems(StreamCategory category)
        {
            return category.TwitchChannelNames.Select((channelName, index) => new StreamListItem
            {
                Key = GetPresetTwitchKey(channelName),
                Title = channelName,
                TwitchChannel = channelName,
                AvatarTexture = CinemaModule.Instance.TextureService.GetDefaultAvatar(),
                Index = index
            }).ToList();
        }

        private List<StreamListItem> BuildChannelItems(StreamCategory category)
        {
            return category.Channels.Select((channel, index) => new StreamListItem
            {
                Key = GetChannelKey(channel.Id),
                Title = channel.Title,
                Subtitle = string.IsNullOrEmpty(channel.Url) && !channel.IsTwitchChannel ? "No URL configured" : null,
                ChannelData = channel,
                TwitchChannel = channel.IsTwitchChannel ? channel.TwitchName : null,
                AvatarTexture = channel.AvatarTexture ?? CinemaModule.Instance.TextureService.GetDefaultAvatar(),
                IsOnDemand = channel.IsOnDemand,
                Index = index
            }).ToList();
        }

        private void BuildCenteredLoginButton()
        {
            _cardsPanel.ClearChildren();
            var container = new Panel { WidthSizingMode = SizingMode.Fill, Height = 200, Parent = _cardsPanel };
            var loginButton = new StandardButton
            {
                Text = "Login to Twitch",
                Width = 160,
                Height = 40,
                Left = (_contentContainer.Width - 160) / 2,
                Top = 80,
                Parent = container,
                BasicTooltipText = "Connect your Twitch account to see followed channels"
            };
            loginButton.Click += (s, e) => ShowTwitchAuthWindow();
        }

        private void BuildTwitchToolbar()
        {
            _headerSection.Height = 40;
            var toolbar = new Panel { WidthSizingMode = SizingMode.Fill, Height = 40, Parent = _headerSection };
            var text = _twitchAuthService.IsAuthenticated && !string.IsNullOrEmpty(_twitchAuthService.Username)
                ? $"Twitch: {_twitchAuthService.Username}"
                : "Twitch Login";
            var btn = new StandardButton
            {
                Text = text,
                Width = 140,
                Left = 5,
                Top = 5,
                Parent = toolbar,
                BasicTooltipText = "Manage Twitch connection"
            };
            btn.Click += (s, e) => ShowTwitchAuthWindow();
            UpdateCardsPanelLayout();
        }

        private void BuildFollowedHeader(int liveCount)
        {
            _headerSection.Height = 50;
            var headerPanel = new Panel { WidthSizingMode = SizingMode.Fill, Height = 50, Parent = _headerSection };
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
                Left = _contentContainer.Width - 110,
                Top = 12,
                Parent = headerPanel,
                BasicTooltipText = "Disconnect your Twitch account"
            };
            logoutBtn.Click += async (s, e) =>
            {
                await _twitchAuthService.LogoutAsync();
                _settings.TwitchAccessToken = string.Empty;
                _settings.TwitchRefreshToken = string.Empty;
                RefreshContent();
            };
            UpdateCardsPanelLayout();
        }

        private void BuildCustomToolbar(CustomStreamTab currentTab)
        {
            _headerSection.Height = 40;
            var toolbar = new Panel { WidthSizingMode = SizingMode.Fill, Height = 40, Parent = _headerSection };

            new Label
            {
                Text = currentTab.Name,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.White,
                Left = 10,
                Top = 10,
                Parent = toolbar
            };

            int rightX = _contentContainer.Width - 10;

            var addStreamButton = new StandardButton
            {
                Text = "+ Add",
                Width = 70,
                Left = rightX - 70,
                Top = 5,
                Parent = toolbar,
                BasicTooltipText = "Add a new stream or video"
            };
            addStreamButton.Click += (s, e) => OpenEditorForNew(currentTab.Id);
            rightX -= 80;

            var renameButton = new StandardButton
            {
                Text = "Rename",
                Width = 70,
                Left = rightX - 70,
                Top = 5,
                Parent = toolbar,
                BasicTooltipText = "Rename this category"
            };
            renameButton.Click += (s, e) => StartRenamingTab(currentTab);
            rightX -= 80;

            bool canDelete = _settings.SavedStreams.Tabs.Count > 1;
            if (canDelete)
            {
                var deleteButton = new StandardButton
                {
                    Text = "Delete",
                    Width = 60,
                    Left = rightX - 60,
                    Top = 5,
                    Parent = toolbar,
                    BasicTooltipText = "Delete this category and all its streams"
                };
                deleteButton.Click += (s, e) => DeleteCurrentTab(currentTab);
            }

            UpdateCardsPanelLayout();
        }

        private void StartAddingNewTab()
        {
            CancelInlineEditing();

            var newTab = _settings.AddCustomTab("New Category");
            _selectedCategoryId = KeyPrefixCustomTab + newTab.Id;
            _settings.LastSelectedSourceCategory = _selectedCategoryId;
            PopulateCategoryMenu();

            if (_customTabMenuItems.TryGetValue(newTab.Id, out var menuItem))
            {
                _categoryMenu.Select(menuItem);
                StartRenamingTabById(newTab.Id);
            }
        }

        private void StartRenamingTab(CustomStreamTab tab) => StartRenamingTabById(tab.Id);

        private void StartRenamingTabById(string tabId)
        {
            CancelInlineEditing();

            if (!_customTabMenuItems.TryGetValue(tabId, out var menuItem))
                return;

            var tab = _settings.SavedStreams.Tabs.Find(t => t.Id == tabId);
            if (tab == null) return;

            _editingTabId = tabId;

            int menuItemIndex = _categoryMenu.Children.ToList().IndexOf(menuItem);
            int yPosition = menuItemIndex * _categoryMenu.MenuItemHeight + 10;

            _editingTextBox = new TextBox
            {
                Text = tab.Name,
                Size = new Point(MenuPanelWidth - 60, 30),
                Location = new Point(45, yPosition),
                MaxLength = 16,
                Parent = _menuPanel
            };

            menuItem.Text = "";

            _editingTextBox.InputFocusChanged += OnEditingTextBoxFocusChanged;
            _editingTextBox.EnterPressed += OnEditingTextBoxEnterPressed;
            _editingTextBox.Focused = true;
            _editingTextBox.SelectionStart = 0;
            _editingTextBox.SelectionEnd = _editingTextBox.Text.Length;
        }

        private void OnEditingTextBoxEnterPressed(object sender, EventArgs e) => FinishInlineEditing(true);

        private void OnEditingTextBoxFocusChanged(object sender, ValueEventArgs<bool> e)
        {
            if (!e.Value)
                FinishInlineEditing(true);
        }

        private void FinishInlineEditing(bool save)
        {
            if (_editingTextBox == null || string.IsNullOrEmpty(_editingTabId))
                return;

            string newName = _editingTextBox.Text?.Trim();
            string tabId = _editingTabId;

            DisposeEditingTextBox();

            if (save && !string.IsNullOrWhiteSpace(newName))
                _settings.RenameCustomTab(tabId, newName);

            _selectedCategoryId = KeyPrefixCustomTab + tabId;
            _settings.LastSelectedSourceCategory = _selectedCategoryId;

            PopulateCategoryMenu();
            SelectTabById(tabId);
        }

        private void CancelInlineEditing()
        {
            if (_editingTextBox == null)
                return;

            DisposeEditingTextBox();
        }

        private void DisposeEditingTextBox()
        {
            _editingTextBox.InputFocusChanged -= OnEditingTextBoxFocusChanged;
            _editingTextBox.EnterPressed -= OnEditingTextBoxEnterPressed;
            _editingTextBox.Dispose();
            _editingTextBox = null;
            _editingTabId = null;
        }

        private void DeleteCurrentTab(CustomStreamTab tab)
        {
            if (_settings.SavedStreams.Tabs.Count <= 1) return;

            _settings.DeleteCustomTab(tab.Id);
            var firstTab = _settings.SavedStreams.Tabs[0];
            _selectedCategoryId = KeyPrefixCustomTab + firstTab.Id;
            _settings.LastSelectedSourceCategory = _selectedCategoryId;
            PopulateCategoryMenu();
            SelectTabById(firstTab.Id);
        }

        private void SelectTabById(string tabId)
        {
            if (_customTabMenuItems.TryGetValue(tabId, out var menuItem))
                _categoryMenu.Select(menuItem);
        }

        private void BuildCategoryHeader(StreamCategory category)
        {
            bool hasDescription = !string.IsNullOrEmpty(category.Description);
            bool hasInfoUrl = !string.IsNullOrEmpty(category.InfoUrl);

            if (!hasDescription && !hasInfoUrl)
                return;

            const int margin = 10;
            var headerPanel = new Panel { WidthSizingMode = SizingMode.Fill, Parent = _headerSection };
            int contentHeight = 0;

            if (hasDescription)
            {
                int labelWidth = _contentContainer.Width - (hasInfoUrl ? 90 : 40);

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
            _headerSection.Height = headerPanel.Height;

            if (hasInfoUrl)
            {
                var infoUrl = category.InfoUrl;
                var infoButton = new StandardButton
                {
                    Text = "Info",
                    Width = 50,
                    Height = 26,
                    Left = _contentContainer.Width - 80,
                    Top = (headerPanel.Height - 26) / 2,
                    Parent = headerPanel,
                    BasicTooltipText = "Learn more about this category"
                };
                infoButton.Click += (s, e) => OpenUrlInBrowser(infoUrl);
            }
            UpdateCardsPanelLayout();
        }

        private void BuildSectionHeader(string title)
        {
            var headerPanel = new Panel { WidthSizingMode = SizingMode.Fill, Height = 32, Parent = _cardsPanel };
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
            _cardsPanel.ClearChildren();
            var container = new Panel { WidthSizingMode = SizingMode.Fill, Height = 100, Parent = _cardsPanel };
            var spinner = new LoadingSpinner { Parent = container };
            spinner.Left = (_contentContainer.Width - spinner.Width) / 2;
            spinner.Top = (container.Height - spinner.Height) / 2;
        }

        private void ReplaceSpinnerWithContent() => _cardsPanel.ClearChildren();

        private void ShowEmptyMessage(string message)
        {
            var container = new Panel { WidthSizingMode = SizingMode.Fill, Height = 40, Parent = _cardsPanel };
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

        private async Task LoadAvatarsAsync(List<StreamListItem> items, CancellationToken token)
        {
            var tasks = items
                .Where(i => HasAvatarUrlAndCacheKey(i))
                .Select(i => UpdateAvatarAsync(i.Key, i.TwitchChannel ?? i.ChannelData?.Id, i.AvatarUrl ?? i.ChannelData?.Avatar, token));
            await Task.WhenAll(tasks);
        }

        private static bool HasAvatarUrlAndCacheKey(StreamListItem item)
        {
            string avatarUrl = item.AvatarUrl ?? item.ChannelData?.Avatar;
            string cacheKey = item.TwitchChannel ?? item.ChannelData?.Id;
            return !string.IsNullOrEmpty(avatarUrl) && !string.IsNullOrEmpty(cacheKey);
        }

        private async Task UpdateAvatarAsync(string itemKey, string cacheKey, string avatarUrl, CancellationToken token)
        {
            try
            {
                var textureService = CinemaModule.Instance.TextureService;
                var avatarTexture = await textureService.GetTwitchAvatarAsync(cacheKey, avatarUrl);
                if (token.IsCancellationRequested) return;
                if (avatarTexture != null && _streamCards.TryGetValue(itemKey, out var card))
                    card.SetAvatar(avatarTexture);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to load avatar for {cacheKey}: {ex.Message}");
            }
        }

        private async Task UpdateYouTubeThumbnailAsync(string itemKey, string videoIdOrUrl, CancellationToken token)
        {
            try
            {
                var textureService = CinemaModule.Instance.TextureService;
                var texture = await textureService.GetYouTubeThumbnailAsync(videoIdOrUrl);
                if (token.IsCancellationRequested) return;
                if (texture != null && _streamCards.TryGetValue(itemKey, out var card))
                    card.SetAvatar(texture);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to load YouTube thumbnail for {videoIdOrUrl}: {ex.Message}");
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
            CancelPendingSelection();
            _selectedStreamKey = key;
            _settings.SelectTwitchChannel(channelName);
            _controller.PrepareForStreamChange();
            UpdateCardSelection();
            await TrySetStreamUrlAsync(() => _twitchService.GetPlayableStreamUrlAsync(channelName), $"{description}: {channelName}", _selectionCts.Token);
        }

        private async void SelectChannel(ChannelData channel, string key)
        {
            CancelPendingSelection();
            _selectedStreamKey = key;

            if (channel.IsTwitchChannel && !string.IsNullOrEmpty(channel.TwitchName))
            {
                _settings.SelectTwitchChannel(channel.TwitchName);
                _controller.PrepareForStreamChange();
                UpdateCardSelection();
                await TrySetStreamUrlAsync(() => _twitchService.GetPlayableStreamUrlAsync(channel.TwitchName), $"channel: {channel.Title}", _selectionCts.Token);
                return;
            }

            _settings.SelectUrlChannel(channel);
            UpdateCardSelection();
        }

        private async void SelectSavedStream(SavedStream stream)
        {
            CancelPendingSelection();
            _selectedStreamKey = GetSavedStreamKey(stream.Id);
            _controller.SelectSavedStream(stream.Id);
            _controller.PrepareForStreamChange();
            UpdateCardSelection();

            switch (stream.SourceType)
            {
                case StreamSourceType.TwitchChannel:
                    await TrySetStreamUrlAsync(() => _twitchService.GetPlayableStreamUrlAsync(stream.Value), $"stream: {stream.Name}", _selectionCts.Token);
                    break;
                case StreamSourceType.YouTubeVideo:
                    await TrySetYouTubeStreamUrlAsync(stream.Value, $"YouTube video: {stream.Name}", _selectionCts.Token);
                    break;
            }
        }

        private void CancelPendingSelection()
        {
            _selectionCts?.Cancel();
            _selectionCts?.Dispose();
            _selectionCts = new CancellationTokenSource();
        }

        private async Task TrySetStreamUrlAsync(Func<Task<string>> getUrlAsync, string streamDescription, CancellationToken token)
        {
            try
            {
                var url = await getUrlAsync();
                if (token.IsCancellationRequested) return;
                _settings.StreamUrl = url ?? "";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Logger.Error(ex, $"Failed to get stream URL for {streamDescription}");
            }
        }

        private async Task TrySetYouTubeStreamUrlAsync(string videoIdOrUrl, string streamDescription, CancellationToken token)
        {
            try
            {
                var urls = await _youtubeService.GetBestStreamUrlsAsync(videoIdOrUrl);
                if (token.IsCancellationRequested) return;
                _settings.AudioUrl = urls.AudioUrl;
                _settings.StreamUrl = urls.VideoUrl ?? "";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
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

        private void OpenEditorForNew(string tabId = null)
        {
            EnsureEditorWindow();
            _editorWindow.OpenForNew(tabId);
        }

        private void OpenEditorForEdit(SavedStream stream)
        {
            EnsureEditorWindow();
            _editorWindow.OpenForEdit(stream);
        }

        private void EnsureEditorWindow()
        {
            if (_editorWindow != null) return;
            _editorWindow = new StreamEditorWindow(_settings, CinemaModule.Instance.TextureService);
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
            CancelInlineEditing();

            _contentCts?.Cancel();
            _contentCts?.Dispose();
            _selectionCts?.Cancel();
            _selectionCts?.Dispose();

            if (_contentContainer?.Parent != null)
                _contentContainer.Parent.Resized -= OnParentResized;

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
