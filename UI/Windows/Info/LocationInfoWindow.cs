using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using CinemaModule.Models;
using CinemaModule.Services;
using Microsoft.Xna.Framework;

namespace CinemaHUD.UI.Windows.Info
{
    public class LocationInfoWindow : StandardWindow
    {
        private WorldLocationPresetData _currentPreset;
        private Image _screenshotImage;
        private Label _descriptionLabel;
        private StandardButton _waypointButton;

        public LocationInfoWindow(AsyncTexture2D backgroundTexture)
            : base(backgroundTexture, new Rectangle(25, 26, 435, 480),
                new Rectangle(40, 30, 415, 440))
        {
            Parent = GameService.Graphics.SpriteScreen;
            Title = "Location Info";
            Emblem = CinemaModule.CinemaModule.Instance.TextureService.GetEmblem();
            Location = new Point(
                (GameService.Graphics.SpriteScreen.Width - Width) / 2,
                (GameService.Graphics.SpriteScreen.Height - Height) / 2);
            SavesPosition = false;
            CanResize = false;

            BuildContent();
        }

        private void BuildContent()
        {
            var panel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                OuterControlPadding = new Vector2(10, 10),
                ControlPadding = new Vector2(0, 10),
                Parent = this
            };

            _screenshotImage = new Image
            {
                Width = 380,
                Height = 350,
                Parent = panel
            };

            _descriptionLabel = new Label
            {
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont14,
                Parent = panel
            };

            _waypointButton = new StandardButton
            {
                Text = "Copy Waypoint",
                Width = 150,
                Parent = panel
            };

            _waypointButton.Click += (s, e) => CopyWaypoint();
        }

        public void ShowPreset(WorldLocationPresetData preset)
        {
            _currentPreset = preset;
            Title = preset.Name ?? "Location Info";
            _descriptionLabel.Text = preset.Description ?? string.Empty;

            _screenshotImage.Texture = preset.PictureTexture;
            _waypointButton.Visible = !string.IsNullOrEmpty(preset.Waypoint);

            Show();
        }

        private void CopyWaypoint()
        {
            var waypoint = _currentPreset?.Waypoint;
            if (string.IsNullOrEmpty(waypoint)) return;

            try
            {
                ClipboardUtil.WindowsClipboardService.SetTextAsync(waypoint);
            }
            catch
            {
                // Clipboard operation failed -> ignore
            }
        }
    }
}
