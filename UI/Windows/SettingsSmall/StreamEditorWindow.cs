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
        private const int TextBoxWidth = 350;
        private const int DropdownWidth = 200;
        private const int ButtonWidth = 100;
        private const string SourceTypeTwitch = "Twitch Channel";
        private const string SourceTypeUrl = "URL";
        private const string UrlHelpText = "Supported formats:\n" +
            "• Video files: MP4, MKV, AVI, WEBM\n" +
            "• Live streams: M3U8, HLS, RTSP, RTMP\n" +
            "• Radio/Audio: MP3, AAC, OGG streams\n" +
            "• Local files: file:///C:/path/to/video.mp4";

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

        public event EventHandler StreamSaved;
        public event EventHandler StreamDeleted;

        public StreamEditorWindow(CinemaUserSettings settings)
            : base("Add Stream")
        {
            _settings = settings;

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
                ControlPadding = new Vector2(0, 10),
                CanScroll = true,
                Parent = this
            };

            BuildNameSection(panel);
            BuildSourceTypeSection(panel);
            BuildValueSection(panel);
            BuildButtons(panel);
        }

        private void BuildNameSection(Container parent)
        {
            new Label
            {
                Text = "Stream Name",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = parent
            };

            _nameTextBox = new TextBox
            {
                Width = TextBoxWidth,
                PlaceholderText = "Enter a name for this stream",
                Parent = parent
            };
        }

        private void BuildSourceTypeSection(Container parent)
        {
            new Label
            {
                Text = "Source Type",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = parent
            };

            _sourceTypeDropdown = new Dropdown
            {
                Width = DropdownWidth,
                Parent = parent
            };
            _sourceTypeDropdown.Items.Add(SourceTypeUrl);
            _sourceTypeDropdown.Items.Add(SourceTypeTwitch);
            _sourceTypeDropdown.SelectedItem = SourceTypeTwitch;
            _sourceTypeDropdown.ValueChanged += (s, e) => OnSourceTypeChanged();
        }

        private void BuildValueSection(Container parent)
        {
            _valueLabel = new Label
            {
                Text = "Stream URL / Channel",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = parent
            };

            _valueTextBox = new TextBox
            {
                Width = TextBoxWidth,
                PlaceholderText = "Enter URL or Twitch channel name",
                Parent = parent
            };

            _helpLabel = new Label
            {
                Text = "",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                TextColor = Color.White,
                Parent = parent
            };
        }

        private void BuildButtons(Container parent)
        {
            var buttonPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(10, 0),
                Parent = parent
            };

            _saveButton = new StandardButton
            {
                Text = "Save",
                Width = ButtonWidth,
                Parent = buttonPanel
            };
            _saveButton.Click += (s, e) => Save();

            _deleteButton = new StandardButton
            {
                Text = "Delete",
                Width = ButtonWidth,
                Visible = false,
                Parent = buttonPanel
            };
            _deleteButton.Click += (s, e) => Delete();

            var cancelButton = new StandardButton
            {
                Text = "Cancel",
                Width = ButtonWidth,
                Parent = buttonPanel
            };
            cancelButton.Click += (s, e) => Hide();
        }

        private void OnSourceTypeChanged()
        {
            bool isTwitch = _sourceTypeDropdown.SelectedItem == SourceTypeTwitch;

            _valueLabel.Text = isTwitch ? "Channel Name" : "Stream URL";
            _valueTextBox.PlaceholderText = isTwitch 
                ? "Channel name (e.g., phandrel)"
                : "Enter stream or video URL";
            _helpLabel.Text = isTwitch ? "" : UrlHelpText;
        }

        public void OpenForNew(StreamSourceType sourceType = StreamSourceType.TwitchChannel)
        {
            _isNewStream = true;
            _stream = new SavedStream();
            Title = "Add Stream";

            _nameTextBox.Text = "";
            _sourceTypeDropdown.SelectedItem = GetDropdownValue(sourceType);
            _valueTextBox.Text = "";
            _deleteButton.Visible = false;

            OnSourceTypeChanged();
            Show();
        }

        public void OpenForEdit(SavedStream stream)
        {
            _isNewStream = false;
            _stream = stream;
            Title = "Edit Stream";

            _nameTextBox.Text = stream.Name ?? "";
            _sourceTypeDropdown.SelectedItem = GetDropdownValue(stream.SourceType);
            _valueTextBox.Text = stream.Value ?? "";
            _deleteButton.Visible = true;

            OnSourceTypeChanged();
            Show();
        }

        private string GetDropdownValue(StreamSourceType sourceType)
        {
            return sourceType == StreamSourceType.TwitchChannel ? SourceTypeTwitch : SourceTypeUrl;
        }

        private void Save()
        {
            var name = _nameTextBox.Text.Trim();
            var value = _valueTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(value)) return;
            if (string.IsNullOrWhiteSpace(name)) name = value;

            var sourceType = _sourceTypeDropdown.SelectedItem == SourceTypeTwitch
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
