using Blish_HUD;
using Blish_HUD.Controls;
using CinemaModule.Models;
using CinemaModule.Services;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using System;

namespace CinemaModule.UI.Windows.Dialogs
{
    public class StreamEditorWindow : SmallWindow
    {
        private const int TextBoxWidth = 350;
        private const int ButtonWidth = 100;
        private const int SourceButtonSize = 32;
        private const string UrlHelpText = "Supported formats:\n" +
            "• Video files: MP4, MKV, AVI, WEBM\n" +
            "• Live streams: M3U8, HLS, RTSP, RTMP\n" +
            "• Radio/Audio: MP3, AAC, OGG streams\n" +
            "• Local files: file:///C:/path/to/video.mp4";
        private const string YouTubeVideoHelpText = "Supported formats:\n" +
            "• Full URL: https://www.youtube.com/watch?v=VIDEO_ID\n" +
            "• Short URL: https://youtu.be/VIDEO_ID\n" +
            "• Video ID: VIDEO_ID";
        private const string YouTubePlaylistHelpText = "Supported formats:\n" +
            "• Playlist URL: https://www.youtube.com/playlist?list=PLxxxxxxxx\n" +
            "• Playlist ID: PLxxxxxxxx\n" +
            "• Channel ID: UCxxxxxxxx (shows latest uploads)";

        private readonly CinemaUserSettings _settings;
        private readonly TextureService _textureService;
        private SavedStream _stream;
        private bool _isNewStream;
        private string _tabId;
        private StreamSourceType _selectedSourceType;

        private TextBox _nameTextBox;
        private GlowButton _twitchButton;
        private GlowButton _urlButton;
        private GlowButton _youtubeVideoButton;
        private GlowButton _youtubePlaylistButton;
        private TextBox _valueTextBox;
        private Label _valueLabel;
        private Label _helpLabel;
        private StandardButton _saveButton;
        private StandardButton _deleteButton;

        public event EventHandler StreamSaved;
        public event EventHandler StreamDeleted;

        public StreamEditorWindow(CinemaUserSettings settings, TextureService textureService)
            : base("Add Source")
        {
            _settings = settings;
            _textureService = textureService;

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
                Text = "Name",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = parent
            };

            _nameTextBox = new TextBox
            {
                Width = TextBoxWidth,
                PlaceholderText = "Enter a name",
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

            var buttonPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                WidthSizingMode = SizingMode.AutoSize,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(8, 0),
                Parent = parent
            };

            _twitchButton = CreateSourceButton(buttonPanel, _textureService.GetTwitchIcon(), "Twitch Channel", StreamSourceType.TwitchChannel);
            _urlButton = CreateSourceButton(buttonPanel, _textureService.GetVlcIcon(), "URL", StreamSourceType.Url);
            _youtubeVideoButton = CreateSourceButton(buttonPanel, _textureService.GetYoutubeIcon(), "YouTube Video", StreamSourceType.YouTubeVideo);
            _youtubePlaylistButton = CreateSourceButton(buttonPanel, _textureService.GetYoutubeIcon(), "YouTube Playlist", StreamSourceType.YouTubePlaylist);
        }

        private GlowButton CreateSourceButton(Container parent, Blish_HUD.Content.AsyncTexture2D icon, string tooltip, StreamSourceType sourceType)
        {
            var button = new GlowButton
            {
                Icon = icon,
                ToggleGlow = true,
                Size = new Point(SourceButtonSize, SourceButtonSize),
                BasicTooltipText = tooltip,
                Parent = parent
            };
            button.Click += (s, e) => SelectSourceType(sourceType);
            return button;
        }

        private void SelectSourceType(StreamSourceType sourceType)
        {
            _selectedSourceType = sourceType;
            _twitchButton.Checked = sourceType == StreamSourceType.TwitchChannel;
            _urlButton.Checked = sourceType == StreamSourceType.Url;
            _youtubeVideoButton.Checked = sourceType == StreamSourceType.YouTubeVideo;
            _youtubePlaylistButton.Checked = sourceType == StreamSourceType.YouTubePlaylist;
            OnSourceTypeChanged();
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
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                WidthSizingMode = SizingMode.AutoSize,
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
            switch (_selectedSourceType)
            {
                case StreamSourceType.TwitchChannel:
                    _valueLabel.Text = "Channel Name";
                    _valueTextBox.PlaceholderText = "Channel name (e.g., phandrel)";
                    _helpLabel.Text = "";
                    break;
                case StreamSourceType.YouTubeVideo:
                    _valueLabel.Text = "YouTube URL or Video ID";
                    _valueTextBox.PlaceholderText = "YouTube URL or video ID";
                    _helpLabel.Text = YouTubeVideoHelpText;
                    break;
                case StreamSourceType.YouTubePlaylist:
                    _valueLabel.Text = "YouTube Playlist";
                    _valueTextBox.PlaceholderText = "Playlist URL or ID";
                    _helpLabel.Text = YouTubePlaylistHelpText;
                    break;
                default:
                    _valueLabel.Text = "URL";
                    _valueTextBox.PlaceholderText = "Enter URL";
                    _helpLabel.Text = UrlHelpText;
                    break;
            }
        }

        public void OpenForNew(string tabId = null, StreamSourceType sourceType = StreamSourceType.TwitchChannel)
        {
            _isNewStream = true;
            _stream = new SavedStream();
            _tabId = tabId;
            Title = "Add Source";

            _nameTextBox.Text = "";
            SelectSourceType(sourceType);
            _valueTextBox.Text = "";
            _saveButton.Visible = true;
            _deleteButton.Visible = false;

            OnSourceTypeChanged();
            Show();
        }

        public void OpenForEdit(SavedStream stream)
        {
            _isNewStream = false;
            _stream = stream;
            _tabId = stream.TabId;
            Title = "Edit Source";

            _nameTextBox.Text = stream.Name ?? "";
            SelectSourceType(stream.SourceType);
            _valueTextBox.Text = stream.Value ?? "";
            _saveButton.Visible = true;
            _deleteButton.Visible = true;

            OnSourceTypeChanged();
            Show();
        }

        private void Save()
        {
            var name = _nameTextBox.Text.Trim();
            var value = _valueTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(value)) return;
            if (string.IsNullOrWhiteSpace(name)) name = value;

            var sourceType = _selectedSourceType;

            if (_isNewStream)
            {
                _settings.AddSavedStream(name, sourceType, value, _tabId);
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
