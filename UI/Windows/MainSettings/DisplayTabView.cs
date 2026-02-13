using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using CinemaHUD.UI.Windows.Info;
using CinemaModule;
using CinemaModule.Models;
using CinemaModule.Services;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
        private FlowPanel _locationSection;
        private FlowPanel _windowHelpSection;
        private FlowPanel _locationsContainer;
        private Dictionary<string, ListCard> _locationCards = new Dictionary<string, ListCard>();

        private LocationEditorWindow _editorWindow;
        private LocationInfoWindow _presetInfoWindow;


        private EventHandler _presetsLoadedHandler;

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
            var panel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                OuterControlPadding = new Vector2(55, 0),
                ControlPadding = new Vector2(20, 10),
                Parent = buildPanel
            };

            BuildDisplayModeSection(panel);

            _windowHelpSection = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(0, 6),
                Parent = panel
            };
            BuildWindowHelpSection();

            _locationSection = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(0, 10),
                Parent = panel
            };

            BuildLocationSection();
            UpdateVisibility();

            _settings.SavedLocationsChanged += (s, e) => RebuildSavedLocationCards();
            _presetsLoadedHandler = (s, e) => RebuildLocationCards();
            _presetService.PresetsLoaded += _presetsLoadedHandler;
        }

        #endregion

        #region Private Methods

        private void BuildDisplayModeSection(Container parent)
        {
            new Label
            {
                Text = "Display Settings",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = parent
            };

            var modePanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(190, 0),
                Parent = parent
            };

            var checkboxWrapper = new Panel
            {
                Width = 100,
                Height = 40,
                Parent = modePanel
            };

            _enabledCheckbox = new Checkbox
            {
                Text = "Enabled",
                Checked = _cinemaSettings.IsEnabled,
                Top = 4,
                Parent = checkboxWrapper
            };

            _enabledCheckbox.CheckedChanged += (s, e) =>
            {
                _cinemaSettings.EnabledSetting.Value = _enabledCheckbox.Checked;
                UpdateVisibility();
            };

            _cinemaSettings.EnabledSetting.SettingChanged += (s, e) =>
            {
                if (_enabledCheckbox.Checked != e.NewValue)
                {
                    _enabledCheckbox.Checked = e.NewValue;
                }
            };

            _displayModeDropdown = new Dropdown
            {
                Width = 160,
                Parent = modePanel
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
            new Label
            {
                Text = "Window Controls",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = _windowHelpSection
            };

            var helpText = new Label
            {
                Text = "• Drag anywhere on the video to move the window\n" +
                       "• Drag the corners or edges to resize\n" +
                       "• Hover over the video to access playback controls",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                TextColor = Color.LightGray,
                Parent = _windowHelpSection
            };
        }

        private void BuildLocationSection()
        {
            var header = new Panel
            {
                WidthSizingMode = SizingMode.Fill,
                Height = 30,
                Parent = _locationSection
            };

            new Label
            {
                Text = "Locations",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Top = 4,
                Parent = header
            };

            var importButton = new StandardButton
            {
                Text = "Import",
                Width = 70,
                Left = 270,
                Parent = header
            };
            importButton.Click += (s, e) => ImportLocationFromClipboard();

            var addButton = new StandardButton
            {
                Text = "+ Add New",
                Width = 100,
                Left = 350,
                Parent = header
            };
            addButton.Click += (s, e) => OpenEditorForNewLocation();

            _locationsContainer = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                Height = 420,
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
                new ListCardButton { Text = "Info", Width = 50, OnClick = () => ShowPresetInfo(preset) }
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

        private void RebuildSavedLocationCards()
        {
            RebuildLocationCards();
        }

        private void CreateSavedLocationListItem(SavedLocation location)
        {
            bool isSelected = _settings.SelectedSavedLocationId == location.Id;
            var position = location.Position;
            var mapId = position?.MapId ?? 0;

            var buttons = new List<ListCardButton>
            {
                new ListCardButton { Text = "X", Width = 30, OnClick = () => DeleteLocation(location) },
                new ListCardButton { Text = "Edit", Width = 50, OnClick = () => OpenEditorForLocation(location) }
            };

            var card = new ListCard(
                _locationsContainer,
                $"{location.Name ?? "Unnamed"} - Map {mapId}",
                string.Empty,
                isSelected,
                textPanelWidth: 320,
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

            if (_windowHelpSection != null)
            {
                bool showWindowHelp = isEnabled && _settings.DisplayMode == CinemaDisplayMode.OnScreen;
                _windowHelpSection.Visible = showWindowHelp;
                _windowHelpSection.Height = showWindowHelp ? -1 : 0;
            }

            if (_locationSection != null)
            {
                bool showLocations = isEnabled && _settings.DisplayMode == CinemaDisplayMode.InGame;
                _locationSection.Visible = showLocations;
                _locationSection.Height = showLocations ? -1 : 0;
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
            _presetService.PresetsLoaded -= _presetsLoadedHandler;
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
