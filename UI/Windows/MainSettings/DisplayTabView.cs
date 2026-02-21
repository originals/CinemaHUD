using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Settings;
using CinemaHUD.UI.Windows.Info;
using CinemaModule;
using CinemaModule.Models;
using CinemaModule.Services;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using LocationEditorWindow = CinemaHUD.UI.Windows.SettingsSmall.LocationEditorWindow;

namespace CinemaHUD.UI.Windows.MainSettings
{
    public class DisplayTabView : View
    {
        #region Members

        private static readonly Logger Logger = Logger.GetLogger<DisplayTabView>();

        private readonly CinemaSettings _cinemaSettings;
        private readonly CinemaUserSettings _settings;
        private readonly CinemaController _controller;
        private readonly Gw2MapService _mapService;
        private readonly PresetService _presetService;
        
        private Checkbox _enabledCheckbox;
        private Dropdown _displayModeDropdown;
        private Panel _locationSection;
        private Panel _windowHelpSection;
        private FlowPanel _locationsContainer;
        private Dictionary<string, ListCard> _locationCards = new Dictionary<string, ListCard>();

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

        #region Public Methods

        protected override void Build(Container buildPanel)
        {
            _buildPanel = buildPanel;

            var displaySettingsPanel = new Panel
            {
                ShowBorder = true,
                Title = "Display Settings",
                Size = new Point(buildPanel.Width - 70, 120),
                Location = new Point(23, 10),
                Parent = buildPanel
            };

            BuildDisplayModeSection(displaySettingsPanel);

            _windowHelpSection = new Panel
            {
                ShowBorder = true,
                Title = "Window Controls",
                Size = new Point(buildPanel.Width - 70, 130),
                Location = new Point(23, 140),
                Parent = buildPanel
            };
            BuildWindowHelpSection();

            _locationSection = new Panel
            {
                ShowBorder = true,
                Size = new Point(buildPanel.Width - 70, buildPanel.Height - 280),
                Location = new Point(23, 140),
                Parent = buildPanel
            };

            BuildLocationSection();
            UpdateVisibility();

            _savedLocationsChangedHandler = (s, e) => RebuildLocationCards();
            _settings.SavedLocationsChanged += _savedLocationsChangedHandler;
            _presetsLoadedHandler = (s, e) => RebuildLocationCards();
            _presetService.PresetsLoaded += _presetsLoadedHandler;
            _parentResizedHandler = (s, e) => UpdateSectionSizes();
            buildPanel.Resized += _parentResizedHandler;
        }

        #endregion

        #region Private Methods

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
                {
                    _enabledCheckbox.Checked = e.NewValue;
                }
            };
            _cinemaSettings.EnabledSetting.SettingChanged += _enabledSettingChangedHandler;

            _displayModeDropdown = new Dropdown
            {
                Width = 160,
                Location = new Point(150, 13),
                Parent = parent
            };

            foreach (var mode in Enum.GetValues(typeof(CinemaDisplayMode)))
            {
                _displayModeDropdown.Items.Add(GetDisplayModeName((CinemaDisplayMode)mode));
            }
            _displayModeDropdown.SelectedItem = GetDisplayModeName(_settings.DisplayMode);

            _displayModeDropdown.ValueChanged += (s, e) =>
            {
                var selectedMode = ParseDisplayMode(_displayModeDropdown.SelectedItem);
                _settings.DisplayMode = selectedMode;
                UpdateVisibility();
            };
        }

        private void BuildWindowHelpSection()
        {
            var helpText = new Label
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

        private void BuildLocationSection()
        {
            var headerPanel = new Panel
            {
                Size = new Point(_locationSection.ContentRegion.Width, 36),
                Location = new Point(0, 0),
                BackgroundTexture = CinemaModule.CinemaModule.Instance.TextureService.GetCardBackground(),
                Parent = _locationSection
            };

            new Label
            {
                Text = "Locations",
                Font = GameService.Content.DefaultFont18,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(10, 8),
                Parent = headerPanel
            };

            var addButton = new StandardButton
            {
                Text = "+ Add New",
                Width = 100,
                Parent = headerPanel
            };
            addButton.Location = new Point(headerPanel.Width - addButton.Width - 10, 5);
            addButton.Click += (s, e) => OpenEditorForNewLocation();

            var importButton = new GlowButton
            {
                Icon = CinemaModule.CinemaModule.Instance.TextureService.GetImportIcon(),
                Size = new Point(30, 26),
                BasicTooltipText = "Import from Clipboard",
                Parent = headerPanel
            };
            importButton.Location = new Point(addButton.Location.X - importButton.Width - 5, 7);
            importButton.Click += (s, e) => ImportLocationFromClipboard();

            _locationsContainer = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Size = new Point(_locationSection.ContentRegion.Width, _locationSection.ContentRegion.Height - 50),
                Location = new Point(0, 46),
                ControlPadding = new Vector2(0, 4),
                CanScroll = true,
                Parent = _locationSection
            };

            RebuildLocationCards();
        }

        private void RebuildLocationCards()
        {
            _locationsContainer?.ClearChildren();
            _locationCards.Clear();

            var presets = _presetService.WorldLocationPresets;
            Logger.Debug($"RebuildLocationCards: Found {presets.Count} preset locations, IsLoaded={_presetService.IsLoaded}");
            foreach (var preset in presets)
            {
                CreatePresetListItem(preset);
            }

            var savedLocations = _settings.SavedLocations;
            foreach (var location in savedLocations.Locations)
            {
                CreateSavedLocationListItem(location);
            }

            if (_locationCards.Count == 0)
            {
                new Label
                {
                    Text = "No locations available. Click '+ Add New' to create one.",
                    AutoSizeHeight = true,
                    AutoSizeWidth = true,
                    TextColor = Color.Gray,
                    Parent = _locationsContainer
                };
            }
        }

        private void CreatePresetListItem(WorldLocationPresetData preset)
        {
            bool isSelected = _settings.SelectedPresetLocationId == preset.Id && 
                              string.IsNullOrEmpty(_settings.SelectedSavedLocationId);

            var position = preset.Position;
            var mapId = position?.MapId ?? 0;

            var buttons = new List<ListCardButton>
            {
                new ListCardButton 
                { 
                    Text = "", 
                    Width = 30, 
                    Icon = CinemaModule.CinemaModule.Instance.TextureService.GetInfoIcon(),
                    Tooltip = "View Details",
                    OnClick = () => ShowPresetInfo(preset) 
                }
            };

            var card = new ListCard(
                _locationsContainer,
                $"{preset.Name} - Map {mapId}",
                string.Empty,
                isSelected,
                textPanelWidth: 350,
                buttons: buttons);

            _locationCards["preset_" + preset.Id] = card;

            card.Click += (s, e) =>
            {
                _controller.SelectPresetLocation(preset.Id, preset.Position, preset.ScreenWidth);
                UpdateCardSelection();
            };

            UpdateCardMapName(card, mapId);
            
            if (preset.AvatarTexture != null)
            {
                card.SetAvatar(preset.AvatarTexture);
            }
        }

        private void CreateSavedLocationListItem(SavedLocation location)
        {
            bool isSelected = _settings.SelectedSavedLocationId == location.Id;
            var position = location.Position;
            var mapId = position?.MapId ?? 0;

            var buttons = new List<ListCardButton>
            {
                new ListCardButton 
                { 
                    Text = "", 
                    Width = 30, 
                    Icon = CinemaModule.CinemaModule.Instance.TextureService.GetDeleteIcon(),
                    Tooltip = "Delete",
                    OnClick = () => DeleteLocation(location) 
                },
                new ListCardButton 
                { 
                    Text = "", 
                    Width = 30, 
                    Icon = CinemaModule.CinemaModule.Instance.TextureService.GetExportIcon(),
                    Tooltip = "Export to Clipboard",
                    OnClick = () => ExportLocationToClipboard(location) 
                },
                new ListCardButton { Text = "Edit", Width = 50, Tooltip = "Edit Location", OnClick = () => OpenEditorForLocation(location) }
            };

            var card = new ListCard(
                _locationsContainer,
                $"{location.Name ?? "Unnamed"} - Map {mapId}",
                string.Empty,
                isSelected,
                textPanelWidth: 260,
                buttons: buttons);

            _locationCards["saved_" + location.Id] = card;

            card.Click += (s, e) =>
            {
                _controller.SelectSavedLocation(location.Id);
                UpdateCardSelection();
            };

            UpdateCardMapName(card, mapId);

            var avatarTexture = CinemaModule.CinemaModule.Instance.TextureService.GetDefaultAvatar();
            if (avatarTexture != null)
            {
                card.SetAvatar(avatarTexture);
            }
        }

        private LocationEditorWindow GetOrCreateEditorWindow()
        {
            if (_editorWindow == null)
            {
                _editorWindow = new LocationEditorWindow(_settings, _controller);
            }
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

        private void UpdateCardSelection()
        {
            foreach (var kvp in _locationCards)
            {
                if (kvp.Key.StartsWith("preset_"))
                {
                    var presetId = kvp.Key.Substring(7);
                    kvp.Value.IsSelected = presetId == _settings.SelectedPresetLocationId && 
                                           string.IsNullOrEmpty(_settings.SelectedSavedLocationId);
                }
                else if (kvp.Key.StartsWith("saved_"))
                {
                    var savedId = kvp.Key.Substring(6);
                    kvp.Value.IsSelected = savedId == _settings.SelectedSavedLocationId;
                }
            }
        }

        private void UpdateVisibility()
        {
            bool isEnabled = _cinemaSettings.IsEnabled;

            if (_displayModeDropdown != null)
            {
                _displayModeDropdown.Enabled = isEnabled;
            }

            if (_windowHelpSection == null || _locationSection == null) return;

            _windowHelpSection.Visible = isEnabled && _settings.DisplayMode == CinemaDisplayMode.OnScreen;
            _locationSection.Visible = isEnabled && _settings.DisplayMode == CinemaDisplayMode.InGame;
        }

        private void UpdateSectionSizes()
        {
            if (_buildPanel == null || _locationSection == null || _locationsContainer == null) return;

            _locationSection.Size = new Point(_buildPanel.Width - 70, _buildPanel.Height - 280);
            _locationsContainer.Size = new Point(_locationSection.ContentRegion.Width, _locationSection.ContentRegion.Height - 50);
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

        private void ShowPresetInfo(WorldLocationPresetData preset)
        {
            var infoWindow = GetOrCreatePresetInfoWindow();
            infoWindow.ShowPreset(preset);
        }

        private LocationInfoWindow GetOrCreatePresetInfoWindow()
        {
            if (_presetInfoWindow == null)
            {
                var bgTexture = CinemaModule.CinemaModule.Instance.TextureService.GetSmallWindowBackground();
                _presetInfoWindow = new LocationInfoWindow(bgTexture);
            }
            return _presetInfoWindow;
        }

        #endregion

        #region Cleanup

        protected override void Unload()
        {
            _settings.SavedLocationsChanged -= _savedLocationsChangedHandler;
            _presetService.PresetsLoaded -= _presetsLoadedHandler;
            _cinemaSettings.EnabledSetting.SettingChanged -= _enabledSettingChangedHandler;
            if (_buildPanel != null)
            {
                _buildPanel.Resized -= _parentResizedHandler;
            }
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
