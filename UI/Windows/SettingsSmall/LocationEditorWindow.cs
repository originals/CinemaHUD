using Blish_HUD;
using Blish_HUD.Controls;
using CinemaModule;
using CinemaModule.Models;
using CinemaModule.Services;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;

namespace CinemaHUD.UI.Windows.SettingsSmall
{
    public class LocationEditorWindow : SmallWindow
    {
        private const float MoveStep = 0.5f;
        private const float RotateStep = 5f;
        private const float DefaultScreenDistanceFromPlayer = 10f;
        private const float DefaultScreenHeightAbovePlayer = 2f;

        private readonly CinemaUserSettings _settings;
        private readonly CinemaController _controller;
        private SavedLocation _editingLocation;

        private TextBox _nameTextBox;
        private Label _positionLabel;
        private TrackBar _widthTrackBar;
        private Label _widthValueLabel;

        public LocationEditorWindow(CinemaUserSettings settings, CinemaController controller)
            : base("Edit Location")
        {
            _settings = settings;
            _controller = controller;

            Initialize();
        }

        protected override void BuildContent()
        {
            var panel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                OuterControlPadding = new Vector2(10, 10),
                ControlPadding = new Vector2(0, 8),
                CanScroll = true,
                Parent = this
            };

            new Label
            {
                Text = "Location Name",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = panel
            };

            _nameTextBox = new TextBox
            {
                Width = 280,
                PlaceholderText = "Enter a name for this location",
                Parent = panel
            };

            _nameTextBox.TextChanged += (s, e) => ApplyNameChange();

            new Label
            {
                Text = "Position",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = panel
            };

            _positionLabel = new Label
            {
                Text = "Not set",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                TextColor = Color.LightGray,
                Parent = panel
            };

            var setPositionBtn = new StandardButton
            {
                Text = "Set to My Current Position",
                Width = 200,
                Parent = panel
            };
            setPositionBtn.Click += (s, e) => SetPositionFromPlayer();

            BuildMovementControls(panel);
            BuildRotationControls(panel);

            new Label
            {
                Text = "Screen Width",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = panel
            };

            var widthRow = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(10, 0),
                Parent = panel
            };

            _widthTrackBar = new TrackBar
            {
                MinValue = 4,
                MaxValue = 50,
                Value = 10,
                Width = 200,
                Parent = widthRow
            };

            _widthValueLabel = new Label
            {
                Text = "10m",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Parent = widthRow
            };

            _widthTrackBar.ValueChanged += (s, e) =>
            {
                _widthValueLabel.Text = $"{_widthTrackBar.Value:F0}m";
                ApplyWidthChange();
            };

            var copyButton = new StandardButton
            {
                Text = "Export Location",
                Width = 200,
                Parent = panel
            };
            copyButton.Click += (s, e) => CopyLocationToClipboard();
        }

        private void BuildMovementControls(Container parent)
        {
            new Label
            {
                Text = "Move Position",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = parent
            };

            var horizontalPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(5, 0),
                Parent = parent
            };

            var westBtn = new StandardButton { Text = "West", Width = 70, Parent = horizontalPanel };
            var eastBtn = new StandardButton { Text = "East", Width = 70, Parent = horizontalPanel };
            var northBtn = new StandardButton { Text = "North", Width = 65, Parent = horizontalPanel };
            var southBtn = new StandardButton { Text = "South", Width = 65, Parent = horizontalPanel };

            westBtn.Click += (s, e) => MovePosition(-MoveStep, 0, 0);
            eastBtn.Click += (s, e) => MovePosition(MoveStep, 0, 0);
            northBtn.Click += (s, e) => MovePosition(0, MoveStep, 0);
            southBtn.Click += (s, e) => MovePosition(0, -MoveStep, 0);

            var verticalPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(5, 0),
                Parent = parent
            };

            var upBtn = new StandardButton { Text = "Up", Width = 70, Parent = verticalPanel };
            var downBtn = new StandardButton { Text = "Down", Width = 70, Parent = verticalPanel };

            upBtn.Click += (s, e) => MovePosition(0, 0, MoveStep);
            downBtn.Click += (s, e) => MovePosition(0, 0, -MoveStep);
        }

        private void BuildRotationControls(Container parent)
        {
            new Label
            {
                Text = "Rotate Screen",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = parent
            };

            var rotationPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(5, 0),
                Parent = parent
            };

            var panWestBtn = new StandardButton { Text = "Pan West", Width = 70, Parent = rotationPanel };
            var panEastBtn = new StandardButton { Text = "Pan East", Width = 70, Parent = rotationPanel };
            var tiltUpBtn = new StandardButton { Text = "Tilt Up", Width = 65, Parent = rotationPanel };
            var tiltDownBtn = new StandardButton { Text = "Tilt Down", Width = 65, Parent = rotationPanel };

            panWestBtn.Click += (s, e) => RotatePosition(-RotateStep, 0);
            panEastBtn.Click += (s, e) => RotatePosition(RotateStep, 0);
            tiltUpBtn.Click += (s, e) => RotatePosition(0, RotateStep);
            tiltDownBtn.Click += (s, e) => RotatePosition(0, -RotateStep);

            var extraPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(5, 0),
                Parent = parent
            };

            var resetRotationBtn = new StandardButton { Text = "Reset Rotation", Width = 110, Parent = extraPanel };
            var facePlayerBtn = new StandardButton { Text = "Face Me", Width = 80, Parent = extraPanel };

            resetRotationBtn.Click += (s, e) => ResetRotation();
            facePlayerBtn.Click += (s, e) => FacePlayer();
        }

        public void CreateNew()
        {
            var position = CreatePositionFromPlayer();
            _editingLocation = _settings.AddSavedLocation("New Location", position, _settings.WorldScreenWidth);
            _controller.SelectSavedLocation(_editingLocation.Id);

            Title = "New Location";
            LoadLocationIntoUI();
            Show();
        }

        private WorldPosition3D CreatePositionFromPlayer()
        {
            var mumble = GameService.Gw2Mumble;
            if (mumble == null || !mumble.IsAvailable)
                return new WorldPosition3D(0, 0, 0, 0, 0, 0);

            var playerPos = mumble.PlayerCharacter.Position;
            var playerForward = mumble.PlayerCharacter.Forward;
            var mapId = mumble.CurrentMap.Id;

            var screenPos = playerPos + playerForward * DefaultScreenDistanceFromPlayer
                + new Vector3(0, 0, DefaultScreenHeightAbovePlayer);
            var toPlayer = playerPos - screenPos;
            float yaw = MathHelper.ToDegrees((float)Math.Atan2(toPlayer.X, toPlayer.Y));
            yaw = WorldPosition3D.NormalizeYaw(yaw);

            return new WorldPosition3D(screenPos.X, screenPos.Y, screenPos.Z, yaw, 0, mapId);
        }

        public void Edit(SavedLocation location)
        {
            _editingLocation = location;

            if (_settings.SelectedSavedLocationId != location.Id)
            {
                _controller.SelectSavedLocation(location.Id);
            }

            Title = $"Edit: {location.Name}";
            LoadLocationIntoUI();
            Show();
        }

        private void LoadLocationIntoUI()
        {
            _nameTextBox.Text = _editingLocation?.Name ?? "";
            _widthTrackBar.Value = _editingLocation?.ScreenWidth ?? 10f;
            _widthValueLabel.Text = $"{_widthTrackBar.Value:F0}m";
            UpdatePositionLabel();
        }

        private void UpdatePositionLabel()
        {
            if (_editingLocation?.Position != null && _editingLocation.Position.MapId != 0)
            {
                var pos = _editingLocation.Position;
                _positionLabel.Text = $"X: {pos.X:F1} Y: {pos.Y:F1} Z: {pos.Z:F1} | Yaw: {pos.Yaw:F0}° Tilt: {pos.Pitch:F0}° | Map: {pos.MapId}";
            }
            else
            {
                _positionLabel.Text = "Not set - Go in-game and click 'Set to My Current Position'";
            }
        }

        private void ApplyNameChange()
        {
            if (_editingLocation == null) return;

            _editingLocation.Name = string.IsNullOrWhiteSpace(_nameTextBox.Text) ? "Unnamed Location" : _nameTextBox.Text;
            _settings.UpdateSavedLocation(_editingLocation);
        }

        private void ApplyWidthChange()
        {
            if (_editingLocation == null) return;

            _editingLocation.ScreenWidth = _widthTrackBar.Value;
            _settings.UpdateSavedLocation(_editingLocation);
            _settings.WorldScreenWidth = _widthTrackBar.Value;
        }

        private void ApplyPositionChange()
        {
            if (_editingLocation == null) return;

            _settings.UpdateSavedLocation(_editingLocation);
            if (_editingLocation.Position != null)
            {
                _settings.WorldPosition = new WorldPosition3D(
                    _editingLocation.Position.X,
                    _editingLocation.Position.Y,
                    _editingLocation.Position.Z,
                    _editingLocation.Position.Yaw,
                    _editingLocation.Position.Pitch,
                    _editingLocation.Position.MapId);
            }
        }

        private void SetPositionFromPlayer()
        {
            if (_editingLocation == null) return;

            var position = CreatePositionFromPlayer();
            if (position.MapId == 0) return;

            _editingLocation.Position = position;
            ApplyPositionChange();
            UpdatePositionLabel();
        }

        private void MovePosition(float deltaX, float deltaY, float deltaZ)
        {
            if (_editingLocation?.Position == null || _editingLocation.Position.MapId == 0)
            {
                return;
            }

            _editingLocation.Position.X += deltaX;
            _editingLocation.Position.Y += deltaY;
            _editingLocation.Position.Z += deltaZ;
            ApplyPositionChange();
            UpdatePositionLabel();
        }

        private void RotatePosition(float deltaYaw, float deltaPitch)
        {
            if (_editingLocation?.Position == null || _editingLocation.Position.MapId == 0)
            {
                return;
            }

            _editingLocation.Position.Yaw = WorldPosition3D.NormalizeYaw(_editingLocation.Position.Yaw + deltaYaw);
            _editingLocation.Position.Pitch = MathHelper.Clamp(_editingLocation.Position.Pitch + deltaPitch, -89f, 89f);
            ApplyPositionChange();
            UpdatePositionLabel();
        }

        private void ResetRotation()
        {
            if (_editingLocation?.Position == null) return;

            _editingLocation.Position.Yaw = 0;
            _editingLocation.Position.Pitch = 0;
            ApplyPositionChange();
            UpdatePositionLabel();
        }

        private void FacePlayer()
        {
            if (_editingLocation?.Position == null || _editingLocation.Position.MapId == 0)
            {
                return;
            }

            var mumble = GameService.Gw2Mumble;
            if (mumble == null || !mumble.IsAvailable)
            {
                return;
            }

            var playerPos = mumble.PlayerCharacter.Position;
            var screenPos = _editingLocation.Position.ToVector3();
            var toPlayer = playerPos - screenPos;

            float yaw = MathHelper.ToDegrees((float)Math.Atan2(toPlayer.X, toPlayer.Y));
            _editingLocation.Position.Yaw = WorldPosition3D.NormalizeYaw(yaw);
            _editingLocation.Position.Pitch = 0;
            ApplyPositionChange();
            UpdatePositionLabel();
        }

        private void CopyLocationToClipboard()
        {
            if (_editingLocation == null) return;

            try
            {
                var exportData = new SavedLocationExport
                {
                    Name = _editingLocation.Name,
                    Position = _editingLocation.Position,
                    ScreenWidth = _editingLocation.ScreenWidth
                };
                var json = JsonConvert.SerializeObject(exportData, Formatting.None);
                System.Windows.Forms.Clipboard.SetText(json);
                Logger.GetLogger(GetType()).Info($"Copied location '{_editingLocation.Name}' to clipboard");
            }
            catch (Exception ex)
            {
                Logger.GetLogger(GetType()).Warn($"Failed to copy location to clipboard: {ex.Message}");
            }
        }

        private class SavedLocationExport
        {
            public string Name { get; set; }
            public WorldPosition3D Position { get; set; }
            public float ScreenWidth { get; set; }
        }
    }
}
