using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Settings;
using CinemaModule.UI.Windows.Info;
using CinemaModule.UI.Windows.Dialogs;
using CinemaModule.Controllers;
using CinemaModule.Models;
using CinemaModule.Models.Location;
using CinemaModule.Services;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CinemaModule.UI.Windows.MainSettings
{
    public class DisplayTabView : View
    {
        #region Constants

        private const int MenuPanelWidth = 240;
        private const int CardVerticalSpacing = 4;
        private const string KeyPrefixPreset = "preset:";
        private const string KeyPrefixSaved = "saved:";
        private const string CategoryMyLocations = "My Locations";

        #endregion

        #region Members

        private static readonly Logger Logger = Logger.GetLogger<DisplayTabView>();

        private readonly CinemaSettings _cinemaSettings;
        private readonly CinemaUserSettings _settings;
        private readonly CinemaController _controller;
        private readonly Gw2MapService _mapService;
        private readonly PresetService _presetService;

        private Checkbox _enabledCheckbox;
        private Dropdown _displayModeDropdown;
        private Panel _windowHelpSection;
        private Panel _locationSection;
        private Menu _categoryMenu;
        private Panel _menuPanel;
        private Panel _contentContainer;
        private Panel _headerSection;
        private FlowPanel _cardsPanel;
        private readonly Dictionary<string, ListCard> _locationCards = new Dictionary<string, ListCard>();
        private readonly Dictionary<string, WorldLocationCategory> _categoryLookup = new Dictionary<string, WorldLocationCategory>();
        private string _selectedLocationKey;
        private string _selectedCategoryId;

        private LocationEditorWindow _editorWindow;
        private LocationInfoWindow _presetInfoWindow;
        private EventHandler _presetsLoadedHandler;
        private EventHandler _savedLocationsChangedHandler;
        private EventHandler<ValueChangedEventArgs<bool>> _enabledSettingChangedHandler;
        private EventHandler<ResizedEventArgs> _parentResizedHandler;
        private Container _buildPanel;

        #endregion

        public DisplayTabView(CinemaSettings cinemaSettings, CinemaUserSettings settings, CinemaController controller, Gw2MapService mapService, PresetService presetService)
        {
            _cinemaSettings = cinemaSettings;
            _settings = settings;
            _controller = controller;
            _mapService = mapService;
            _presetService = presetService;
        }

        #region Build

        protected override void Build(Container buildPanel)
        {
            _buildPanel = buildPanel;
            InitializeSelectedLocationKey();
            BuildDisplaySettingsPanel(buildPanel);
            BuildWindowHelpSection(buildPanel);
            BuildLocationSection(buildPanel);
            SubscribeToEvents();
            UpdateVisibility();
        }

        private void BuildDisplaySettingsPanel(Container parent)
        {
            var displaySettingsPanel = new Panel
            {
                ShowBorder = true,
                Title = "Display Settings",
                Size = new Point(parent.Width - 70, 120),
                Location = new Point(23, 10),
                Parent = parent
            };

            BuildDisplayModeSection(displaySettingsPanel);
        }

        private void BuildDisplayModeSection(Container parent)
        {
            _enabledCheckbox = new Checkbox
            {
                Text = "Enabled",
                Checked = _cinemaSettings.IsEnabled,
                Location = new Point(10, 15),
                Parent = parent
            };

            _enabledCheckbox.CheckedChanged += (s, e) =>
            {
                _cinemaSettings.EnabledSetting.Value = _enabledCheckbox.Checked;
                UpdateVisibility();
            };

            _enabledSettingChangedHandler = (s, e) =>
            {
                if (_enabledCheckbox.Checked != e.NewValue)
                    _enabledCheckbox.Checked = e.NewValue;
            };
            _cinemaSettings.EnabledSetting.SettingChanged += _enabledSettingChangedHandler;

            _displayModeDropdown = new Dropdown
            {
                Width = 160,
                Location = new Point(120, 13),
                Parent = parent
            };

            _displayModeDropdown.Items.Add(GetDisplayModeName(CinemaDisplayMode.InGame));
            _displayModeDropdown.Items.Add(GetDisplayModeName(CinemaDisplayMode.OnScreen));

            _displayModeDropdown.SelectedItem = GetDisplayModeName(_settings.DisplayMode);

            _displayModeDropdown.ValueChanged += (s, e) =>
            {
                var selectedMode = ParseDisplayMode(_displayModeDropdown.SelectedItem);
                _settings.DisplayMode = selectedMode;
                UpdateVisibility();
            };

            new Label
            {
                Text = "In-Game World: 3D Video displayed at in-game, press '+ Add New' to start\n" +
                       "On-Screen Window: A draggable, resizable overlay window",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Location = new Point(300, 10),
                TextColor = Color.LightGray,
                Parent = parent
            };
        }

        private void BuildWindowHelpSection(Container parent)
        {
            _windowHelpSection = new Panel
            {
                ShowBorder = true,
                Title = "Window Controls",
                Size = new Point(parent.Width - 70, 130),
                Location = new Point(23, 140),
                Parent = parent
            };

            new Label
            {
                Text = "• Drag anywhere on the video to move the window\n" +
                       "• Drag the corners or edges to resize\n" +
                       "• Hover over the video to access playback controls",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Location = new Point(10, 10),
                TextColor = Color.LightGray,
                Parent = _windowHelpSection
            };
        }

        private void BuildLocationSection(Container parent)
        {
            _locationSection = new Panel
            {
                ShowBorder = false,
                Size = new Point(parent.Width - 70, parent.Height - 280),
                Location = new Point(23, 140),
                Parent = parent
            };

            BuildCategoryMenu();
            BuildContentPanel();
            PopulateCategoryMenu();
            SelectInitialCategory();
        }

        private void BuildCategoryMenu()
        {
            _menuPanel = new Panel
            {
                ShowBorder = true,
                Size = new Point(MenuPanelWidth, _locationSection.Height),
                Location = new Point(0, 0),
                Title = "Categories",
                Parent = _locationSection,
                CanScroll = true
            };

            _categoryMenu = new Menu
            {
                Size = _menuPanel.ContentRegion.Size,
                MenuItemHeight = 50,
                Parent = _menuPanel,
                CanSelect = true
            };

            _categoryMenu.ItemSelected += OnCategorySelected;
        }

        private void BuildContentPanel()
        {
            _contentContainer = new Panel
            {
                Size = new Point(_locationSection.Width - MenuPanelWidth - 6, _locationSection.Height),
                Location = new Point(MenuPanelWidth + 6, 0),
                ShowBorder = true,
                Parent = _locationSection
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

        #endregion

        #region Category Menu

        private void PopulateCategoryMenu()
        {
            _categoryMenu.ClearChildren();
            _categoryLookup.Clear();

            var myLocationsItem = _categoryMenu.AddMenuItem(CategoryMyLocations);
            myLocationsItem.Icon = CinemaModule.Instance.TextureService.GetEmblem();

            foreach (var category in _presetService.WorldLocationCategories)
            {
                var menuItem = _categoryMenu.AddMenuItem(category.Name);
                menuItem.Icon = category.IconTexture;
                _categoryLookup[category.Name] = category;
            }
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
            var lastCategory = _settings.LastSelectedLocationCategory;
            if (!string.IsNullOrEmpty(lastCategory) &&
                (_categoryLookup.ContainsKey(lastCategory) || lastCategory == CategoryMyLocations))
            {
                return lastCategory;
            }

            return _presetService.WorldLocationCategories.FirstOrDefault()?.Name ?? CategoryMyLocations;
        }

        private void OnCategorySelected(object sender, ControlActivatedEventArgs e)
        {
            if (e.ActivatedControl is MenuItem menuItem)
            {
                _selectedCategoryId = menuItem.Text;
                _settings.LastSelectedLocationCategory = _selectedCategoryId;
                RefreshContent();
            }
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

        #endregion

        #region Content Loading

        private void RefreshContent()
        {
            _locationCards.Clear();
            _headerSection.ClearChildren();
            _headerSection.Height = 0;
            _cardsPanel.ClearChildren();
            UpdateCardsPanelLayout();

            if (_selectedCategoryId == CategoryMyLocations)
                LoadMyLocationsContent();
            else if (_categoryLookup.TryGetValue(_selectedCategoryId, out var category))
                LoadCategoryContent(category);
        }

        private void LoadCategoryContent(WorldLocationCategory category)
        {
            BuildCategoryHeader(category);

            if (category.Locations.Count == 0)
            {
                ShowEmptyMessage("No locations available in this category");
                return;
            }

            foreach (var location in category.Locations)
                CreatePresetLocationCard(location);
        }

        private void LoadMyLocationsContent()
        {
            BuildMyLocationsToolbar();

            var savedLocations = _settings.SavedLocations.Locations;
            if (savedLocations.Count == 0)
            {
                ShowEmptyMessage("No custom locations. Click '+ Add New' to create one.");
                return;
            }

            foreach (var location in savedLocations)
                CreateSavedLocationCard(location);
        }

        private void BuildCategoryHeader(WorldLocationCategory category)
        {
            if (string.IsNullOrEmpty(category.Description))
                return;

            const int margin = 10;
            var headerPanel = new Panel { WidthSizingMode = SizingMode.Fill, Parent = _headerSection };

            var descLabel = new Label
            {
                Text = category.Description,
                Width = Math.Max(_contentContainer.Width - 40, 100),
                AutoSizeHeight = true,
                WrapText = true,
                TextColor = Color.LightGray,
                Font = GameService.Content.DefaultFont14,
                Left = margin,
                Top = margin,
                Parent = headerPanel
            };

            headerPanel.Height = Math.Max(descLabel.Height + (margin * 2), 46);
            _headerSection.Height = headerPanel.Height;
            UpdateCardsPanelLayout();
        }

        private void BuildMyLocationsToolbar()
        {
            _headerSection.Height = 40;
            var toolbar = new Panel { WidthSizingMode = SizingMode.Fill, Height = 40, Parent = _headerSection };

            new Label
            {
                Text = "Create your own screen locations",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                TextColor = Color.Gray,
                Left = 10,
                Top = 12,
                Parent = toolbar
            };

            var importButton = new GlowButton
            {
                Icon = CinemaModule.Instance.TextureService.GetImportIcon(),
                Size = new Point(30, 26),
                BasicTooltipText = "Import from Clipboard",
                Left = _contentContainer.Width - 145,
                Top = 7,
                Parent = toolbar
            };
            importButton.Click += (s, e) => ImportLocationFromClipboard();

            var addButton = new StandardButton
            {
                Text = "+ Add New",
                Width = 100,
                Left = _contentContainer.Width - 110,
                Top = 5,
                Parent = toolbar
            };
            addButton.Click += (s, e) => OpenEditorForNewLocation();

            UpdateCardsPanelLayout();
        }

        private void UpdateCardsPanelLayout()
        {
            _cardsPanel.Location = new Point(0, _headerSection.Height);
            _cardsPanel.Height = _contentContainer.ContentRegion.Height - _headerSection.Height;
        }

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

        #endregion

        #region Location Cards

        private void CreatePresetLocationCard(WorldLocationPresetData preset)
        {
            string key = $"{KeyPrefixPreset}{preset.Id}";
            bool isSelected = key == _selectedLocationKey;

            var position = preset.Position;
            var mapId = position?.MapId ?? 0;

            var buttons = new List<ListCardButton>
            {
                new ListCardButton
                {
                    Text = "",
                    Width = 30,
                    Icon = CinemaModule.Instance.TextureService.GetInfoIcon(),
                    Tooltip = "View Details",
                    OnClick = () => ShowPresetInfo(preset)
                }
            };

            if (!string.IsNullOrEmpty(preset.Waypoint))
            {
                buttons.Insert(0, new ListCardButton
                {
                    Text = "",
                    Width = 30,
                    Icon = CinemaModule.Instance.TextureService.GetWaypointIcon(),
                    Tooltip = "Copy Waypoint",
                    OnClick = () => CopyWaypointToClipboard(preset.Waypoint)
                });
            }

            var card = new ListCard(
                _cardsPanel,
                $"{preset.Name} - Map {mapId}",
                preset.Description ?? string.Empty,
                isSelected,
                textPanelWidth: 220,
                buttons: buttons);

            _locationCards[key] = card;

            card.Click += (s, e) => SelectPresetLocation(preset, key);

            UpdateCardMapName(card, mapId);

            if (preset.AvatarTexture != null)
                card.SetAvatar(preset.AvatarTexture);
        }

        private void CreateSavedLocationCard(SavedLocation location)
        {
            string key = $"{KeyPrefixSaved}{location.Id}";
            bool isSelected = key == _selectedLocationKey;

            var position = location.Position;
            var mapId = position?.MapId ?? 0;

            var buttons = new List<ListCardButton>
            {
                new ListCardButton
                {
                    Text = "",
                    Width = 30,
                    Icon = CinemaModule.Instance.TextureService.GetDeleteIcon(),
                    Tooltip = "Delete",
                    OnClick = () => DeleteLocation(location)
                },
                new ListCardButton
                {
                    Text = "",
                    Width = 30,
                    Icon = CinemaModule.Instance.TextureService.GetExportIcon(),
                    Tooltip = "Export to Clipboard",
                    OnClick = () => ExportLocationToClipboard(location)
                },
                new ListCardButton { Text = "Edit", Width = 50, Tooltip = "Edit Location", OnClick = () => OpenEditorForLocation(location) }
            };

            var card = new ListCard(
                _cardsPanel,
                $"{location.Name ?? "Unnamed"} - Map {mapId}",
                string.Empty,
                isSelected,
                textPanelWidth: 220,
                buttons: buttons);

            _locationCards[key] = card;

            card.Click += (s, e) => SelectSavedLocation(location, key);

            UpdateCardMapName(card, mapId);

            var avatarTexture = CinemaModule.Instance.TextureService.GetDefaultAvatar();
            if (avatarTexture != null)
                card.SetAvatar(avatarTexture);
        }

        #endregion

        #region Selection

        private void InitializeSelectedLocationKey()
        {
            if (!string.IsNullOrEmpty(_settings.SelectedSavedLocationId))
            {
                _selectedLocationKey = $"{KeyPrefixSaved}{_settings.SelectedSavedLocationId}";
                return;
            }

            if (!string.IsNullOrEmpty(_settings.SelectedPresetLocationId))
                _selectedLocationKey = $"{KeyPrefixPreset}{_settings.SelectedPresetLocationId}";
        }

        private void SelectPresetLocation(WorldLocationPresetData preset, string key)
        {
            _selectedLocationKey = key;
            _controller.SelectPresetLocation(preset.Id, preset.Position, preset.ScreenWidth);
            UpdateCardSelection();
        }

        private void SelectSavedLocation(SavedLocation location, string key)
        {
            _selectedLocationKey = key;
            _controller.SelectSavedLocation(location.Id);
            UpdateCardSelection();
        }

        private void UpdateCardSelection()
        {
            foreach (var kvp in _locationCards)
                kvp.Value.IsSelected = kvp.Key == _selectedLocationKey;
        }

        #endregion

        #region Actions

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

        private LocationEditorWindow GetOrCreateEditorWindow()
        {
            if (_editorWindow == null)
                _editorWindow = new LocationEditorWindow(_settings, _controller);
            return _editorWindow;
        }

        private void OpenEditorForNewLocation()
        {
            var editor = GetOrCreateEditorWindow();
            editor.CreateNew();
        }

        private void OpenEditorForLocation(SavedLocation location)
        {
            var editor = GetOrCreateEditorWindow();
            editor.Edit(location);
        }

        private void DeleteLocation(SavedLocation location)
        {
            _settings.DeleteSavedLocation(location.Id);
        }

        private void ImportLocationFromClipboard()
        {
            try
            {
                if (!System.Windows.Forms.Clipboard.ContainsText())
                {
                    Logger.Debug("Clipboard does not contain text for import");
                    return;
                }

                var json = System.Windows.Forms.Clipboard.GetText();
                var importData = JsonConvert.DeserializeObject<SavedLocationExport>(json);

                if (importData?.Position == null)
                {
                    Logger.Debug("Invalid location data in clipboard");
                    return;
                }

                var name = string.IsNullOrWhiteSpace(importData.Name) ? "Imported Location" : importData.Name;
                _settings.AddSavedLocation(name, importData.Position, importData.ScreenWidth);
                Logger.Debug($"Imported location '{name}' from clipboard");
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to import location from clipboard: {ex.Message}");
            }
        }

        private void ExportLocationToClipboard(SavedLocation location)
        {
            ExportToClipboard(location.Name, location.Position, location.ScreenWidth);
        }

        private void ExportToClipboard(string name, WorldPosition3D position, float screenWidth)
        {
            try
            {
                var exportData = new SavedLocationExport
                {
                    Name = name,
                    Position = position,
                    ScreenWidth = screenWidth
                };

                var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                System.Windows.Forms.Clipboard.SetText(json);
                Logger.Debug($"Exported location '{name}' to clipboard");
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to export location to clipboard: {ex.Message}");
            }
        }

        private void ShowPresetInfo(WorldLocationPresetData preset)
        {
            var infoWindow = GetOrCreatePresetInfoWindow();
            infoWindow.ShowPreset(preset);
        }

        private LocationInfoWindow GetOrCreatePresetInfoWindow()
        {
            if (_presetInfoWindow == null)
            {
                var bgTexture = CinemaModule.Instance.TextureService.GetSmallWindowBackground();
                _presetInfoWindow = new LocationInfoWindow(bgTexture);
            }
            return _presetInfoWindow;
        }

        #endregion

        #region Visibility

        private void UpdateVisibility()
        {
            bool isEnabled = _cinemaSettings.IsEnabled;

            if (_displayModeDropdown != null)
                _displayModeDropdown.Enabled = isEnabled;

            if (_windowHelpSection == null || _locationSection == null) return;

            _windowHelpSection.Visible = isEnabled && _settings.DisplayMode == CinemaDisplayMode.OnScreen;
            _locationSection.Visible = isEnabled && _settings.DisplayMode == CinemaDisplayMode.InGame;
        }

        #endregion

        #region Helpers

        private async void UpdateCardMapName(ListCard card, int mapId)
        {
            if (mapId <= 0) return;

            var mapName = await _mapService.GetMapNameAsync(mapId);
            var currentTitle = card.Title;
            var dashIndex = currentTitle.LastIndexOf(" - ");

            if (dashIndex > 0)
            {
                var namePrefix = currentTitle.Substring(0, dashIndex);
                card.Title = $"{namePrefix} - {mapName}";
            }
        }

        private CinemaDisplayMode ParseDisplayMode(string name)
        {
            if (name == "On-Screen Window") return CinemaDisplayMode.OnScreen;
            if (name == "In-Game World") return CinemaDisplayMode.InGame;
            return CinemaDisplayMode.OnScreen;
        }

        private string GetDisplayModeName(CinemaDisplayMode mode)
        {
            switch (mode)
            {
                case CinemaDisplayMode.OnScreen:
                    return "On-Screen Window";
                case CinemaDisplayMode.InGame:
                    return "In-Game World";
                default:
                    return mode.ToString();
            }
        }

        #endregion

        #region Events

        private void SubscribeToEvents()
        {
            _savedLocationsChangedHandler = (s, e) =>
            {
                if (_selectedCategoryId == CategoryMyLocations)
                    RefreshContent();
            };
            _settings.SavedLocationsChanged += _savedLocationsChangedHandler;

            _presetsLoadedHandler = (s, e) =>
            {
                PopulateCategoryMenu();
                ReselectCurrentCategory();
                RefreshContent();
            };
            _presetService.PresetsLoaded += _presetsLoadedHandler;

            _parentResizedHandler = (s, e) => UpdateSectionSizes();
            _buildPanel.Resized += _parentResizedHandler;
        }

        private void UpdateSectionSizes()
        {
            if (_buildPanel == null || _locationSection == null) return;

            _locationSection.Size = new Point(_buildPanel.Width - 70, _buildPanel.Height - 280);
            _menuPanel.Height = _locationSection.Height;
            _categoryMenu.Height = _menuPanel.ContentRegion.Height;
            _contentContainer.Size = new Point(_locationSection.Width - MenuPanelWidth - 6, _locationSection.Height);
            _cardsPanel.Width = _contentContainer.ContentRegion.Width;
            UpdateCardsPanelLayout();
        }

        #endregion

        #region Cleanup

        protected override void Unload()
        {
            _settings.SavedLocationsChanged -= _savedLocationsChangedHandler;
            _presetService.PresetsLoaded -= _presetsLoadedHandler;
            _cinemaSettings.EnabledSetting.SettingChanged -= _enabledSettingChangedHandler;
            _categoryMenu.ItemSelected -= OnCategorySelected;

            if (_buildPanel != null)
                _buildPanel.Resized -= _parentResizedHandler;

            _editorWindow?.Dispose();
            _presetInfoWindow?.Dispose();
            base.Unload();
        }

        #endregion

        private class SavedLocationExport
        {
            public string Name { get; set; }
            public WorldPosition3D Position { get; set; }
            public float ScreenWidth { get; set; }
        }
    }
}
