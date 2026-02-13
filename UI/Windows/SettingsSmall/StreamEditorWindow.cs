using Blish_HUD;
using Blish_HUD.Controls;
using CinemaModule.Models;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using System;

namespace CinemaHUD.UI.Windows.SettingsSmall
{
    public class StreamEditorWindow : SmallWindow
    {
        #region Fields

        private readonly CinemaUserSettings _settings;

        private SavedStream _stream;
        private bool _isNewStream;

        private TextBox _nameTextBox;
        private Dropdown _sourceTypeDropdown;
        private TextBox _valueTextBox;
        private Label _valueLabel;
        private Label _helpLabel;
        private StandardButton _saveButton;
        private StandardButton _deleteButton;

        #endregion

        #region Events

        public event EventHandler StreamSaved;
        public event EventHandler StreamDeleted;

        #endregion

        public StreamEditorWindow(CinemaUserSettings settings)
            : base("Add Stream")
        {
            _settings = settings;
        }


        protected override void BuildContent()
        {
            var panel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                OuterControlPadding = new Vector2(10, 10),
                ControlPadding = new Vector2(0, 10),
                CanScroll = true,
                Parent = this
            };

            new Label
            {
                Text = "Stream Name",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = panel
            };

            _nameTextBox = new TextBox
            {
                Width = 350,
                PlaceholderText = "Enter a name for this stream",
                Parent = panel
            };

            new Label
            {
                Text = "Source Type",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = panel
            };

            _sourceTypeDropdown = new Dropdown
            {
                Width = 200,
                Parent = panel
            };
            _sourceTypeDropdown.Items.Add("URL");
            _sourceTypeDropdown.Items.Add("Twitch Channel");
            _sourceTypeDropdown.SelectedItem = "Twitch Channel";
            _sourceTypeDropdown.ValueChanged += (s, e) => OnSourceTypeChanged();

            _valueLabel = new Label
            {
                Text = "Stream URL / Channel",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = panel
            };

            _valueTextBox = new TextBox
            {
                Width = 350,
                PlaceholderText = "Enter URL or Twitch channel name",
                Parent = panel
            };

            _helpLabel = new Label
            {
                Text = "",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                TextColor = Color.White,
                Parent = panel
            };

            var buttonPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(10, 0),
                Parent = panel
            };

            _saveButton = new StandardButton
            {
                Text = "Save",
                Width = 100,
                Parent = buttonPanel
            };
            _saveButton.Click += (s, e) => Save();

            _deleteButton = new StandardButton
            {
                Text = "Delete",
                Width = 100,
                Visible = false,
                Parent = buttonPanel
            };
            _deleteButton.Click += (s, e) => Delete();

            var cancelButton = new StandardButton
            {
                Text = "Cancel",
                Width = 100,
                Parent = buttonPanel
            };
            cancelButton.Click += (s, e) => Hide();
        }

        private void OnSourceTypeChanged()
        {
            bool isTwitch = _sourceTypeDropdown.SelectedItem == "Twitch Channel";

            _valueLabel.Text = isTwitch ? "Channel Name" : "Stream URL";

            _valueTextBox.PlaceholderText = isTwitch 
                ? "Channel name (e.g., phandrel)"
                : "Enter stream or video URL";

            _helpLabel.Text = isTwitch
                ? ""
                : "Supported formats:\n" +
                  "• Video files: MP4, MKV, AVI, WEBM\n" +
                  "• Live streams: M3U8, HLS, RTSP, RTMP\n" +
                  "• Radio/Audio: MP3, AAC, OGG streams\n" +
                  "• Local files: file:///C:/path/to/video.mp4";
        }

        public void OpenForNew(StreamSourceType sourceType = StreamSourceType.TwitchChannel)
        {
            _isNewStream = true;
            _stream = new SavedStream();
            Title = "Add Stream";

            _nameTextBox.Text = "";
            _sourceTypeDropdown.SelectedItem = sourceType == StreamSourceType.TwitchChannel 
                ? "Twitch Channel" 
                : "URL";
            _valueTextBox.Text = "";
            _deleteButton.Visible = false;

            OnSourceTypeChanged();
            Show();
        }

        public void OpenForEdit(SavedStream stream)
        {
            if (stream == null) return;

            _isNewStream = false;
            _stream = stream;
            Title = "Edit Stream";

            _nameTextBox.Text = stream.Name ?? "";
            _sourceTypeDropdown.SelectedItem = stream.SourceType == StreamSourceType.TwitchChannel 
                ? "Twitch Channel" 
                : "URL";
            _valueTextBox.Text = stream.Value ?? "";
            _deleteButton.Visible = true;

            OnSourceTypeChanged();
            Show();
        }

        private void Save()
        {
            var name = _nameTextBox.Text.Trim();
            var value = _valueTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = value;
            }

            var sourceType = _sourceTypeDropdown.SelectedItem == "Twitch Channel"
                ? StreamSourceType.TwitchChannel
                : StreamSourceType.Url;

            if (_isNewStream)
            {
                _settings.AddSavedStream(name, sourceType, value);
            }
            else
            {
                _stream.Name = name;
                _stream.SourceType = sourceType;
                _stream.Value = value;
                _settings.UpdateSavedStream(_stream);
            }

            StreamSaved?.Invoke(this, EventArgs.Empty);
            Hide();
        }

        private void Delete()
        {
            if (_stream == null || _isNewStream) return;

            _settings.DeleteSavedStream(_stream.Id);
            StreamDeleted?.Invoke(this, EventArgs.Empty);
            Hide();
        }
    }
}
