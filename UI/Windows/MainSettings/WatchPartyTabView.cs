using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Input;
using CinemaModule.Controllers;
using CinemaModule.Controllers.WatchParty;
using CinemaModule.Models.Location;
using CinemaModule.Models.WatchParty;
using CinemaModule.Services.YouTube;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;

namespace CinemaModule.UI.Windows.MainSettings
{
    public class WatchPartyTabView : View
    {
        private static readonly Logger Logger = Logger.GetLogger<WatchPartyTabView>();

        private const int LeftColumnWidth = 340;
        private const int ColumnSpacing = 6;
        private const int TopPadding = 10;
        private const int SidePadding = 23;
        private const int BottomOffset = 110;
        private const int CardVerticalSpacing = 4;
        private const int HostControlsHeight = 140;
        private const int StatusSectionHeight = 100;
        private const int DescriptionSectionHeight = 90;
        private const int HelpSectionHeight = 220;
        private const int CreateRoomSectionHeight = 280;
        private const string NothingPlayingText = "Nothing playing";
        private const string NoDescriptionText = "No description";

        private readonly WatchPartyController _controller;
        private readonly YouTubeService _youtubeService;
        private readonly CinemaUserSettings _userSettings;
        private readonly CinemaController _cinemaController;

        private Panel _leftColumn;
        private Panel _rightColumn;
        private Panel _browserPanel;
        private Panel _membersSection;
        private FlowPanel _roomListFlow;
        private FlowPanel _membersFlow;
        private readonly Dictionary<ListCard, WatchPartyRoom> _roomCardMap = new Dictionary<ListCard, WatchPartyRoom>();
        private readonly Dictionary<string, ListCard> _memberCards = new Dictionary<string, ListCard>();
        private FlowPanel _queuePanel;
        private Panel _queueContainer;
        private Panel _hostControlsSection;
        private Panel _statusSection;
        private Panel _createRoomSection;
        private Panel _descriptionSection;
        private Panel _helpSection;
        private Label _descriptionLabel;
        private Label _serverStatusLabel;
        private GlowButton _applyLocationButton;
        private ListCard _nowPlayingCard;
        private ListCard _hostNowPlayingCard;
        private Panel _addVideoPanel;
        private string _nowPlayingVideoId;
        private StandardButton _playNextButton;
        private Dropdown _queueLimitDropdown;
        private StandardButton _addToQueueButton;
        private List<string> _lastQueueVideoIds = new List<string>();
        private List<string> _lastMembers = new List<string>();
        private StandardButton _joinRoomButton;
        private StandardButton _leaveRoomButton;
        private StandardButton _createRoomButton;
        private Label _apiWarningLabel;
        private StandardButton _resyncButton;
        private TextBox _roomNameBox;
        private WatchPartyRoom _selectedRoom;
        private bool _isViewActive;

        private readonly Dictionary<ListCard, int> _queueCardIndexMap = new Dictionary<ListCard, int>();
        private readonly Dictionary<string, string> _videoTitleCache = new Dictionary<string, string>();

        public WatchPartyTabView(WatchPartyController controller, YouTubeService youtubeService, CinemaUserSettings userSettings, CinemaController cinemaController)
        {
            _controller = controller;
            _youtubeService = youtubeService;
            _userSettings = userSettings;
            _cinemaController = cinemaController;
        }

        protected override void Build(Container buildPanel)
        {
            _isViewActive = true;

            BuildLeftColumn(buildPanel);
            BuildRightColumn(buildPanel);
            SubscribeToEvents();

            if (_controller.IsInRoom)
                ShowRoomView();
            else
                ShowLobbyView();

            _ = ServerStatusCheckLoopAsync();
        }

        private string FormatViewerCount(int count) => $"{count} viewer{(count != 1 ? "s" : "")}";


        #region Layout

        private void BuildLeftColumn(Container parent)
        {
            _leftColumn = new Panel
            {
                Size = new Point(LeftColumnWidth, parent.Height - BottomOffset),
                Location = new Point(SidePadding, TopPadding),
                Parent = parent
            };

            BuildRoomBrowser();
            BuildMembersSection();
            BuildTitleBarButtons();

            parent.Resized += OnParentResized;
        }

        private void BuildRightColumn(Container parent)
        {
            int rightX = SidePadding + LeftColumnWidth + ColumnSpacing;
            _rightColumn = new Panel
            {
                Size = new Point(parent.Width - rightX - SidePadding - 30, parent.Height - BottomOffset),
                Location = new Point(rightX, TopPadding),
                Parent = parent
            };

            BuildDescriptionSection();
            BuildHostControls();
            BuildStatusSection();
            BuildCreateRoomSection();
            BuildHelpSection();
            BuildQueueSection();
        }

        private void OnParentResized(object sender, ResizedEventArgs e)
        {
            var parent = (Container)sender;
            int newHeight = parent.Height - BottomOffset;
            _leftColumn.Size = new Point(LeftColumnWidth, newHeight);

            int rightX = SidePadding + LeftColumnWidth + ColumnSpacing;
            _rightColumn.Size = new Point(parent.Width - rightX - SidePadding - 30, newHeight);

            UpdateLeftColumnLayout();
            UpdateLobbyLayout();
            UpdateQueuePosition();
        }

        private void UpdateLobbyLayout()
        {
            if (!_createRoomSection.Visible) return;

            int availableHeight = _rightColumn.Height - HelpSectionHeight - 5;
            int createRoomHeight = Math.Min(CreateRoomSectionHeight, availableHeight);
            _createRoomSection.Size = new Point(_rightColumn.Width, createRoomHeight);
        }

        #endregion

        #region Left Column - Room Browser

        private void BuildTitleBarButtons()
        {
            _joinRoomButton = new StandardButton
            {
                Text = "Join",
                Size = new Point(70, 26),
                Location = new Point(LeftColumnWidth - 80, 5),
                Parent = _leftColumn,
                Enabled = false,
                BasicTooltipText = "Join the selected room"
            };

            _joinRoomButton.Click += (s, e) => JoinSelectedRoom();

            _leaveRoomButton = new StandardButton
            {
                Text = "Leave Room",
                Size = new Point(100, 26),
                Location = new Point(LeftColumnWidth - 110, 5),
                Parent = _leftColumn,
                Visible = false,
                BasicTooltipText = "Leave the current room"
            };

            _leaveRoomButton.Click += async (s, e) =>
            {
                await _controller.LeaveRoomAsync();
            };
        }

        private void BuildRoomBrowser()
        {
            _browserPanel = new Panel
            {
                ShowBorder = true,
                Title = "Rooms",
                Size = new Point(LeftColumnWidth, _leftColumn.Height),
                Location = new Point(0, 0),
                Parent = _leftColumn,
                CanScroll = true
            };

            _roomListFlow = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(0, CardVerticalSpacing),
                Parent = _browserPanel
            };
        }

        private async Task RefreshRoomsAsync()
        {
            try
            {
                await _controller.RefreshRoomsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to refresh room list: {ex.Message}");
            }
        }

        private void PopulateRoomList(List<WatchPartyRoom> rooms)
        {
            _roomListFlow.ClearChildren();
            _roomCardMap.Clear();

            string activeRoomId = _controller.CurrentRoom?.RoomId;

            foreach (var room in rooms)
            {
                if (room == null) continue;

                string title = room.IsPrivate ? $"[Private] {room.RoomName}" : room.RoomName;
                string subtitle = $"Host: {room.HostUsername} | {FormatViewerCount(room.MemberCount)}";
                bool isSelected = room.RoomId == activeRoomId;

                var card = new ListCard(_roomListFlow, title, subtitle, isSelected, showAvatar: false);
                card.Click += OnRoomCardClicked;
                _roomCardMap[card] = room;
            }

            _roomListFlow.Invalidate();
            ResetPanelScroll(_browserPanel);
        }

        private void ShowPasswordPrompt(string roomId)
        {
            _createRoomSection.Visible = false;

            var prompt = new Panel
            {
                ShowBorder = true,
                Title = "Enter Room Password",
                Size = new Point(_rightColumn.Width, 100),
                Location = new Point(0, 0),
                Parent = _rightColumn
            };

            var pwBox = new TextBox
            {
                PlaceholderText = "Enter password...",
                Size = new Point(_rightColumn.Width - 200, 30),
                Location = new Point(10, 10),
                Parent = prompt
            };

            var okButton = new StandardButton
            {
                Text = "Join",
                Size = new Point(80, 30),
                Location = new Point(_rightColumn.Width - 180, 10),
                Parent = prompt
            };

            var cancelButton = new StandardButton
            {
                Text = "Cancel",
                Size = new Point(80, 30),
                Location = new Point(_rightColumn.Width - 90, 10),
                Parent = prompt
            };

            okButton.Click += async (s, e) =>
            {
                await _controller.JoinRoomAsync(roomId, pwBox.Text);
                prompt.Dispose();
                if (!_controller.IsInRoom)
                    _createRoomSection.Visible = true;
            };

            cancelButton.Click += (s, e) =>
            {
                prompt.Dispose();
                _createRoomSection.Visible = true;
            };
        }

        #endregion

        #region Left Column - Viewers

        private void BuildMembersSection()
        {
            int availableHeight = _leftColumn.Height;
            _membersSection = new Panel
            {
                ShowBorder = true,
                Title = "Viewers",
                Size = new Point(LeftColumnWidth, availableHeight / 2),
                Location = new Point(0, availableHeight / 2 + 5),
                Parent = _leftColumn,
                CanScroll = true,
                Visible = false
            };

            _membersFlow = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(0, 2),
                Parent = _membersSection
            };
        }

        private void UpdateLeftColumnLayout()
        {
            UpdateLeftColumnLayout(_membersSection.Visible);
        }

        private void UpdateLeftColumnLayout(bool showMembers)
        {
            int availableHeight = _leftColumn.Height;

            if (!showMembers)
            {
                _membersSection.Visible = false;
                _browserPanel.Size = new Point(LeftColumnWidth, availableHeight);
                _browserPanel.Location = new Point(0, 0);
                ResetPanelScroll(_browserPanel);
                _roomListFlow.Invalidate();
                return;
            }

            int halfHeight = availableHeight / 2;
            _browserPanel.Size = new Point(LeftColumnWidth, halfHeight - 3);
            _browserPanel.Location = new Point(0, 0);
            _membersSection.Size = new Point(LeftColumnWidth, halfHeight - 3);
            _membersSection.Location = new Point(0, halfHeight + 3);

            _membersSection.Visible = true;
            ResetPanelScroll(_browserPanel);
            ResetPanelScroll(_membersSection);
            _roomListFlow.Invalidate();
            _membersFlow.Invalidate();
        }

        private void ResetPanelScroll(Panel panel)
        {
            panel.CanScroll = false;
            panel.CanScroll = true;
        }

        private void PopulateMembers(IReadOnlyList<string> members)
        {
            _lastMembers = new List<string>(members);
            _membersFlow.ClearChildren();
            _memberCards.Clear();

            var state = _controller.CurrentState;
            string localName = _controller.LocalGw2Name;
            string hostName = _controller.CurrentRoom?.HostUsername;
            bool isHost = _controller.IsHost;

            foreach (var member in members)
            {
                bool isSelf = member == localName;
                bool isMemberHost = member == hostName;
                string displayName = BuildViewerDisplayName(member, isMemberHost);
                string subtitle = FormatMemberInfo(member, state);

                List<ListCardButton> buttons = null;
                if (isHost && !isSelf && !isMemberHost)
                {
                    buttons = new List<ListCardButton>
                    {
                        new ListCardButton
                        {
                            Text = "Ban",
                            Width = 50,
                            Tooltip = $"Ban {member} from this room",
                            OnClick = () => _ = _controller.BanMemberAsync(member)
                        }
                    };
                }

                var card = new ListCard(_membersFlow, displayName, subtitle, false, showAvatar: false, buttons: buttons);
                _memberCards[member] = card;
            }
        }

        private string BuildViewerDisplayName(string name, bool isHost)
        {
            return isHost ? $"[Host] {name}" : name;
        }

        private void RefreshMemberInfo()
        {
            RefreshMemberInfo(_controller.CurrentState);
        }

        private void RefreshMemberInfo(WatchPartyLocalState state)
        {
            foreach (var kvp in _memberCards)
            {
                string formatted = FormatMemberInfo(kvp.Key, state);
                kvp.Value.SetSubtitle(formatted);
            }
        }

        private string FormatMemberInfo(string username, WatchPartyLocalState state)
        {
            if (state == null)
                return "—";

            bool isSelf = string.Equals(username, _controller.LocalGw2Name, StringComparison.OrdinalIgnoreCase);
            string usernameLower = username.ToLowerInvariant();

            MemberState memberState = GetMemberState(isSelf, usernameLower, state);
            double time = GetMemberTime(isSelf, usernameLower, state);

            string stateIcon = $"[{memberState}]";
            string timeStr = FormatPlaybackTime(time);

            return $"{stateIcon} {timeStr}";
        }

        private MemberState GetMemberState(bool isSelf, string usernameLower, WatchPartyLocalState state)
        {
            if (isSelf)
                return _controller.LocalMemberState;

            if (state.MemberStates.TryGetValue(usernameLower, out var serverState))
                return serverState;

            return MemberState.Idle;
        }

        private double GetMemberTime(bool isSelf, string usernameLower, WatchPartyLocalState state)
        {
            if (isSelf)
                return _controller.LocalPlaybackTime;

            if (state.MemberTimes.TryGetValue(usernameLower, out double serverTime) && serverTime > 0)
                return serverTime;

            bool isHost = string.Equals(usernameLower, state.HostUsername, StringComparison.OrdinalIgnoreCase);
            if (isHost)
                return state.CurrentTime;

            return 0;
        }

        private string FormatPlaybackTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        #endregion

        #region Right Column - Host Controls

        private void BuildDescriptionSection()
        {
            _descriptionSection = new Panel
            {
                ShowBorder = true,
                Title = "Room Description",
                Size = new Point(_rightColumn.Width, DescriptionSectionHeight),
                Location = new Point(0, 0),
                Parent = _rightColumn,
                Visible = false
            };

            _descriptionLabel = new Label
            {
                Text = "",
                Location = new Point(10, 5),
                Size = new Point(_rightColumn.Width - 60, DescriptionSectionHeight - 40),
                WrapText = true,
                Parent = _descriptionSection
            };

            _applyLocationButton = new GlowButton
            {
                Icon = CinemaModule.Instance.TextureService.GetSetScreenIcon(),
                Location = new Point(_rightColumn.Width - 45, 5),
                Parent = _descriptionSection,
                Visible = false,
                BasicTooltipText = "Apply the host's shared screen position"
            };

            _applyLocationButton.Click += OnApplyLocationClicked;
        }

        private void OnApplyLocationClicked(object sender, MouseEventArgs e)
        {
            var state = _controller.CurrentState;
            if (state?.SharedLocation == null)
                return;

            var savedLocation = state.SharedLocation.ToSavedLocation();
            _cinemaController.ApplyLocation(savedLocation);
        }

        private void UpdateApplyLocationButton()
        {
            var state = _controller.CurrentState;
            bool hasSharedLocation = state?.SharedLocation != null;
            _applyLocationButton.Visible = hasSharedLocation;

            if (hasSharedLocation)
            {
                _applyLocationButton.BasicTooltipText = $"Apply screen position: {state.SharedLocation.Name}";
            }
        }

        private void BuildHostControls()
        {
            _hostControlsSection = new Panel
            {
                ShowBorder = true,
                Title = "Host Controls",
                Size = new Point(_rightColumn.Width, HostControlsHeight),
                Location = new Point(0, DescriptionSectionHeight + 5),
                Parent = _rightColumn,
                Visible = false
            };

            var hostNowPlayingFlow = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                Parent = _hostControlsSection
            };

            _hostNowPlayingCard = new ListCard(
                hostNowPlayingFlow,
                NothingPlayingText,
                "",
                false);

            _playNextButton = new StandardButton
            {
                Text = "Play Next",
                Size = new Point(90, 30),
                Location = new Point(10, 65),
                Parent = _hostControlsSection,
                Enabled = false,
                BasicTooltipText = "Play the next video in the queue"
            };

            _playNextButton.Click += async (s, e) =>
            {
                await _controller.PlayNextInQueueAsync();
            };

            new Label
            {
                Text = "Limit:",
                Location = new Point(110, 71),
                AutoSizeWidth = true,
                Parent = _hostControlsSection,
                BasicTooltipText = "Max videos per user in queue (0 = unlimited)"
            };

            _queueLimitDropdown = new Dropdown
            {
                Size = new Point(90, 25),
                Location = new Point(150, 67),
                Parent = _hostControlsSection,
                BasicTooltipText = "Max videos per user in queue (0 = unlimited)"
            };

            _queueLimitDropdown.Items.Add("Unlimited");
            for (int i = 1; i <= 5; i++)
                _queueLimitDropdown.Items.Add(i.ToString());

            _queueLimitDropdown.SelectedItem = "Unlimited";
            _queueLimitDropdown.ValueChanged += OnQueueLimitChanged;

            var autoplayCheckbox = new Checkbox
            {
                Text = "Autoplay next",
                Location = new Point(250, 71),
                Checked = _userSettings.WatchPartyAutoplayNext,
                Parent = _hostControlsSection,
                BasicTooltipText = "Automatically play the next video when current one ends"
            };

            autoplayCheckbox.CheckedChanged += (s, e) =>
            {
                _userSettings.WatchPartyAutoplayNext = e.Checked;
            };
        }

        #endregion

        #region Right Column - Status (Non-Host)

        private void BuildStatusSection()
        {
            _statusSection = new Panel
            {
                ShowBorder = true,
                Title = "Now Playing",
                Size = new Point(_rightColumn.Width, StatusSectionHeight),
                Location = new Point(0, DescriptionSectionHeight + 5),
                Parent = _rightColumn,
                Visible = false
            };

            _resyncButton = new StandardButton
            {
                Text = "Resync",
                Size = new Point(70, 26),
                Location = new Point(_rightColumn.Width - 80, DescriptionSectionHeight + 10),
                Parent = _rightColumn,
                Visible = false,
                BasicTooltipText = "Reload video (only needed when stuck)"
            };

            _resyncButton.Click += (s, e) => _cinemaController.ForceWatchPartyResync();

            var nowPlayingFlow = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                Parent = _statusSection
            };

            _nowPlayingCard = new ListCard(
                nowPlayingFlow,
                NothingPlayingText,
                "",
                false);
        }

        #endregion

        #region Right Column - Create Room

        private void BuildCreateRoomSection()
        {
            _createRoomSection = new Panel
            {
                ShowBorder = true,
                Title = "Create Room",
                Size = new Point(_rightColumn.Width, CreateRoomSectionHeight),
                Location = new Point(0, HelpSectionHeight + 5),
                Parent = _rightColumn,
                Visible = false,
                CanScroll = true
            };

            const int labelX = 10;
            const int inputX = 120;
            const int rightPadding = 20;
            int inputWidth = _rightColumn.Width - inputX - rightPadding;
            int y = 5;

            new Label
            {
                Text = "Room Name:",
                Location = new Point(labelX, y + 4),
                AutoSizeWidth = true,
                Parent = _createRoomSection
            };

            _roomNameBox = new TextBox
            {
                PlaceholderText = "Movie Night, Chill Stream, etc.",
                Size = new Point(inputWidth, 30),
                Location = new Point(inputX, y),
                Parent = _createRoomSection
            };

            _roomNameBox.TextChanged += (s, e) => UpdateCreateRoomButtonState();

            y += 35;

            new Label
            {
                Text = "Description:",
                Location = new Point(labelX, y + 4),
                AutoSizeWidth = true,
                Parent = _createRoomSection
            };

            var descriptionBox = new MultilineTextBox
            {
                PlaceholderText = "Meet at Lion's Arch, /sqjoin YourName...",
                Size = new Point(inputWidth, 50),
                Location = new Point(inputX, y),
                Parent = _createRoomSection
            };

            y += 55;

            new Label
            {
                Text = "Share Location:",
                Location = new Point(labelX, y + 4),
                AutoSizeWidth = true,
                Parent = _createRoomSection
            };

            var locationDropdown = new Dropdown
            {
                Size = new Point(inputWidth, 30),
                Location = new Point(inputX, y),
                Parent = _createRoomSection
            };

            PopulateLocationDropdown(locationDropdown);
            _userSettings.SavedLocationsChanged += (s, e) => PopulateLocationDropdown(locationDropdown);

            y += 35;

            var privateCheckbox = new Checkbox
            {
                Text = "Private (password required)",
                Location = new Point(labelX, y),
                Parent = _createRoomSection
            };

            y += 25;

            var passwordBox = new TextBox
            {
                PlaceholderText = "Room password...",
                Size = new Point(inputWidth, 30),
                Location = new Point(inputX, y),
                Visible = false,
                Parent = _createRoomSection
            };

            privateCheckbox.CheckedChanged += (s, e) =>
            {
                passwordBox.Visible = e.Checked;
            };

            y += 45;

            _apiWarningLabel = new Label
            {
                Text = "API key with Account permission required. (may take a bit to load)",
                Location = new Point(10, y),
                AutoSizeWidth = true,
                TextColor = Microsoft.Xna.Framework.Color.Orange,
                Parent = _createRoomSection,
                Visible = !_controller.IsApiAvailable
            };

            _createRoomButton = new StandardButton
            {
                Text = "Create Room",
                Size = new Point(120, 30),
                Location = new Point(10, y),
                Parent = _createRoomSection,
                Visible = _controller.IsApiAvailable
            };

            UpdateCreateRoomButtonState();

            _createRoomButton.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_roomNameBox.Text))
                    return;

                _createRoomButton.Enabled = false;
                _createRoomButton.Text = "Creating...";

                try
                {
                    WatchPartySharedLocation sharedLocation = null;
                    if (locationDropdown.SelectedItem != "None")
                    {
                        var savedLocation = GetSelectedLocation(locationDropdown);
                        if (savedLocation != null)
                            sharedLocation = WatchPartySharedLocation.FromSavedLocation(savedLocation);
                    }

                    var success = await _controller.CreateRoomAsync(
                        _roomNameBox.Text,
                        privateCheckbox.Checked,
                        passwordBox.Text,
                        sharedLocation);

                    if (success)
                    {
                        if (!string.IsNullOrWhiteSpace(descriptionBox.Text))
                            await _controller.UpdateRoomAsync(_roomNameBox.Text, descriptionBox.Text);

                        _roomNameBox.Text = "";
                        descriptionBox.Text = "";
                        passwordBox.Text = "";
                        privateCheckbox.Checked = false;
                        locationDropdown.SelectedItem = "None";
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Create Room exception");
                }
                finally
                {
                    UpdateCreateRoomButtonState();
                    _createRoomButton.Text = "Create Room";
                }
            };
        }

        private void PopulateLocationDropdown(Dropdown dropdown)
        {
            dropdown.Items.Clear();
            dropdown.Items.Add("None");
            foreach (var location in _userSettings.SavedLocations.Locations)
            {
                dropdown.Items.Add(location.Name);
            }
            dropdown.SelectedItem = "None";
        }

        private SavedLocation GetSelectedLocation(Dropdown dropdown)
        {
            if (dropdown.SelectedItem == "None")
                return null;

            return _userSettings.SavedLocations.Locations.Find(l => l.Name == dropdown.SelectedItem);
        }

        #endregion

        #region Right Column - Info Section

        private void BuildHelpSection()
        {
            _helpSection = new Panel
            {
                ShowBorder = true,
                Title = "How It Works",
                Size = new Point(_rightColumn.Width, HelpSectionHeight),
                Location = new Point(0, 0),
                Parent = _rightColumn,
                Visible = false
            };

            int y = 5;

            _serverStatusLabel = new Label
            {
                Text = "Server: Checking...",
                Location = new Point(10, y),
                AutoSizeWidth = true,
                Parent = _helpSection
            };

            y += 25;

            new Label
            {
                Text = "Watch Party lets you watch YouTube videos in sync with friends.",
                Location = new Point(10, y),
                Size = new Point(_rightColumn.Width - 30, 20),
                WrapText = true,
                Parent = _helpSection
            };

            y += 25;

            new Label
            {
                Text = "• Create or join a room to get started",
                Location = new Point(10, y),
                AutoSizeWidth = true,
                Parent = _helpSection
            };

            y += 20;

            new Label
            {
                Text = "• Host controls playback for all (stable connection helps)",
                Location = new Point(10, y),
                AutoSizeWidth = true,
                Parent = _helpSection
            };

            y += 20;

            new Label
            {
                Text = "• Viewers can add videos to the queue",
                Location = new Point(10, y),
                AutoSizeWidth = true,
                Parent = _helpSection
            };

            y += 20;

            new Label
            {
                Text = "• Share a screen location so everyone can watch on the same screen",
                Location = new Point(10, y),
                AutoSizeWidth = true,
                Parent = _helpSection
            };

            y += 20;

            new Label
            {
                Text = "• If desynced or video not loading, try the Resync button",
                Location = new Point(10, y),
                AutoSizeWidth = true,
                Parent = _helpSection
            };
        }

        private async Task ServerStatusCheckLoopAsync()
        {
            while (_isViewActive)
            {
                try
                {
                    await _controller.CheckServerStatusAsync().ConfigureAwait(false);
                    UpdateServerStatusLabel();

                    if (_controller.ServerStatus == ServerStatus.Online || 
                        _controller.ServerStatus == ServerStatus.VersionMismatch)
                    {
                        await RefreshRoomsAsync().ConfigureAwait(false);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Server status check failed: {ex.Message}");
                }

                await Task.Delay(3000).ConfigureAwait(false);
            }
        }

        private void UpdateServerStatusLabel()
        {
            var status = _controller.ServerStatus;
            var serverVersion = _controller.ServerVersion;
            var clientVersion = CinemaModule.ModuleVersion;

            switch (status)
            {
                case ServerStatus.Checking:
                    _serverStatusLabel.Text = "Server: Checking...";
                    _serverStatusLabel.TextColor = Color.Yellow;
                    break;

                case ServerStatus.Online:
                    _serverStatusLabel.Text = $"Server: Online (v{serverVersion ?? clientVersion})";
                    _serverStatusLabel.TextColor = Color.LightGreen;
                    break;

                case ServerStatus.Offline:
                    _serverStatusLabel.Text = "Server: Offline - Watch Party unavailable";
                    _serverStatusLabel.TextColor = Color.Red;
                    break;

                case ServerStatus.VersionMismatch:
                    _serverStatusLabel.Text = $"Server: v{serverVersion} - Please update CinemaHUD to v{serverVersion}";
                    _serverStatusLabel.TextColor = Color.Orange;
                    break;

                default:
                    _serverStatusLabel.Text = "Server: Unknown";
                    _serverStatusLabel.TextColor = Color.Gray;
                    break;
            }
        }

        #endregion

        #region Right Column - Queue

        private void BuildQueueSection()
        {
            int queueTop = StatusSectionHeight + 10;

            _addVideoPanel = new Panel
            {
                Size = new Point(_rightColumn.Width, 40),
                Location = new Point(0, queueTop),
                Parent = _rightColumn,
                Visible = false
            };

            var urlBox = new TextBox
            {
                PlaceholderText = "YouTube URL...",
                Size = new Point(_rightColumn.Width - 120, 30),
                Location = new Point(0, 5),
                Parent = _addVideoPanel
            };

            _addToQueueButton = new StandardButton
            {
                Text = "Add to Queue",
                Size = new Point(110, 30),
                Location = new Point(_rightColumn.Width - 115, 5),
                Parent = _addVideoPanel
            };

            _addToQueueButton.Click += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(urlBox.Text))
                {
                    await _controller.AddVideoAsync(urlBox.Text);
                    urlBox.Text = "";
                }
            };

            _queueContainer = new Panel
            {
                ShowBorder = true,
                Title = "Queue",
                Size = new Point(_rightColumn.Width, _rightColumn.Height - queueTop - 50),
                Location = new Point(0, queueTop + 45),
                Parent = _rightColumn,
                CanScroll = true,
                Visible = false
            };

            _queuePanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                ControlPadding = new Vector2(0, CardVerticalSpacing),
                Parent = _queueContainer
            };
        }

        private int GetQueueTop()
        {
            int descOffset = _descriptionSection.Visible ? DescriptionSectionHeight + 5 : 0;
            return descOffset + (_hostControlsSection.Visible
                ? HostControlsHeight + 10
                : StatusSectionHeight + 10);
        }

        private void UpdateQueuePosition()
        {
            int queueTop = GetQueueTop();
            _addVideoPanel.Location = new Point(0, queueTop);
            _queueContainer.Location = new Point(0, queueTop + 45);
            _queueContainer.Size = new Point(_rightColumn.Width, _rightColumn.Height - queueTop - 50);
        }

        private bool HasQueueChanged(IReadOnlyList<QueueItem> queue)
        {
            if (queue.Count != _lastQueueVideoIds.Count)
                return true;

            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].VideoId != _lastQueueVideoIds[i])
                    return true;
            }

            return false;
        }

        private bool HasMembersChanged(IReadOnlyList<string> members)
        {
            if (members.Count != _lastMembers.Count)
                return true;

            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != _lastMembers[i])
                    return true;
            }

            return false;
        }

        private void PopulateQueue(IReadOnlyList<QueueItem> queue)
        {
            _lastQueueVideoIds = new List<string>();
            foreach (var q in queue)
                _lastQueueVideoIds.Add(q.VideoId);

            _queuePanel.ClearChildren();
            _queueCardIndexMap.Clear();

            _playNextButton.Enabled = queue.Count > 0;

            bool isHost = _controller.IsHost;
            int queueCount = queue.Count;

            for (int i = 0; i < queueCount; i++)
            {
                var video = queue[i];
                var index = i;

                List<ListCardButton> buttons = isHost ? CreateQueueItemButtons(index, queueCount) : null;

                bool hasCachedTitle = _videoTitleCache.TryGetValue(video.VideoId, out var cachedTitle);

                var card = new ListCard(
                    _queuePanel,
                    hasCachedTitle ? cachedTitle : "",
                    $"Added by {video.AddedBy}",
                    false,
                    buttons: buttons);

                _queueCardIndexMap[card] = index;

                card.ShowLoading(!hasCachedTitle);
                _ = LoadQueueCardInfoAsync(card, video.VideoId, hasCachedTitle);
            }
        }

        private List<ListCardButton> CreateQueueItemButtons(int index, int queueCount)
        {
            var textureService = CinemaModule.Instance.TextureService;
            return new List<ListCardButton>
            {
                new ListCardButton
                {
                    Text = "X",
                    Width = 30,
                    Tooltip = "Remove from Queue",
                    OnClick = () => _ = _controller.RemoveFromQueueAsync(index)
                },
                new ListCardButton
                {
                    Icon = textureService.GetArrowDownIcon(),
                    Width = 30,
                    Tooltip = "Move Down",
                    OnClick = () =>
                    {
                        if (index < queueCount - 1)
                            _ = _controller.ReorderQueueAsync(index, index + 1);
                    }
                },
                new ListCardButton
                {
                    Icon = textureService.GetArrowUpIcon(),
                    Width = 30,
                    Tooltip = "Move Up",
                    OnClick = () =>
                    {
                        if (index > 0)
                            _ = _controller.ReorderQueueAsync(index, index - 1);
                    }
                }
            };
        }

        private async Task LoadQueueCardInfoAsync(ListCard card, string videoId, bool titleAlreadyCached)
        {
            if (string.IsNullOrEmpty(videoId))
            {
                card.ShowLoading(false);
                return;
            }

            var result = await FetchVideoInfoAsync(videoId, titleAlreadyCached).ConfigureAwait(false);
            if (!_isViewActive || !_queueCardIndexMap.ContainsKey(card))
                return;

            ApplyVideoInfoToCard(card, result.Thumbnail, result.Info);
        }

        #endregion

        #region View State

        private void ShowLobbyView()
        {
            _joinRoomButton.Visible = true;
            _joinRoomButton.Enabled = false;
            _leaveRoomButton.Visible = false;
            _selectedRoom = null;
            _hostControlsSection.Visible = false;
            _statusSection.Visible = false;
            _resyncButton.Visible = false;
            _createRoomSection.Visible = true;
            _helpSection.Visible = true;
            _descriptionSection.Visible = false;

            ResetRoomState();
            UpdateLeftColumnLayout(false);
            UpdateLobbyLayout();
            SetRightColumnChildrenVisible(false);
        }

        private void ShowRoomView()
        {
            _joinRoomButton.Visible = false;
            _leaveRoomButton.Visible = true;
            _selectedRoom = null;
            _createRoomSection.Visible = false;
            _helpSection.Visible = false;

            UpdateLeftColumnLayout(true);

            bool isHost = _controller.IsHost;
            _hostControlsSection.Visible = isHost;
            _statusSection.Visible = !isHost;
            _resyncButton.Visible = !isHost;
            _descriptionSection.Visible = true;

            SetRightColumnChildrenVisible(true);
            UpdateQueuePosition();

            ResetRoomState();

            if (_controller.CurrentState != null)
            {
                PopulateQueue(_controller.CurrentState.Queue);
                UpdateNowPlaying(_controller.CurrentState);
                PopulateMembers(_controller.CurrentState.Members);
                UpdateDescription(_controller.CurrentState);
                UpdateAddToQueueButton(_controller.CurrentState);
            }
        }

        private void ResetRoomState()
        {
            _nowPlayingVideoId = null;
            _lastQueueVideoIds.Clear();
            _lastMembers.Clear();
            _queueCardIndexMap.Clear();
            _memberCards.Clear();

            ResetNowPlayingCard(_nowPlayingCard);
            ResetNowPlayingCard(_hostNowPlayingCard);

            _playNextButton.Enabled = false;

            _descriptionSection.Title = "Room";
            _descriptionLabel.Text = NoDescriptionText;
            _applyLocationButton.Visible = false;

            _queuePanel.ClearChildren();
            _membersFlow.ClearChildren();
        }

        private void ResetNowPlayingCard(ListCard card)
        {
            card.Title = NothingPlayingText;
            card.SetSubtitle("");
            card.SetAvatar(null);
            card.ShowLoading(false);
        }

        private void SetRightColumnChildrenVisible(bool visible)
        {
            foreach (var child in _rightColumn.Children)
            {
                if (child is Panel p)
                {
                    if (p == _hostControlsSection || p == _statusSection || p == _createRoomSection || p == _descriptionSection || p == _helpSection) continue;
                    p.Visible = visible;
                }
            }
        }

        private void UpdateNowPlaying(WatchPartyLocalState state)
        {
            string subtitle = state.HasVideo ? (state.IsPlaying ? "Playing" : "Paused") : "";
            _nowPlayingCard.SetSubtitle(subtitle);
            _hostNowPlayingCard.SetSubtitle(subtitle);

            if (state.HasVideo && state.CurrentVideoId != _nowPlayingVideoId)
            {
                _nowPlayingVideoId = state.CurrentVideoId;

                bool hasCachedTitle = _videoTitleCache.TryGetValue(state.CurrentVideoId, out var cachedTitle);
                string title = hasCachedTitle ? cachedTitle ?? "" : "";
                _nowPlayingCard.Title = title;
                _hostNowPlayingCard.Title = title;

                _nowPlayingCard.ShowLoading(!hasCachedTitle);
                _hostNowPlayingCard.ShowLoading(!hasCachedTitle);
                _ = LoadNowPlayingCardInfoAsync(state.CurrentVideoId, hasCachedTitle);
            }
            else if (!state.HasVideo)
            {
                _nowPlayingVideoId = null;
                _nowPlayingCard.Title = NothingPlayingText;
                _nowPlayingCard.SetAvatar(null);
                _hostNowPlayingCard.Title = NothingPlayingText;
                _hostNowPlayingCard.SetAvatar(null);
            }
        }

        private void UpdateDescription(WatchPartyLocalState state)
        {
            _descriptionSection.Title = string.IsNullOrEmpty(state.RoomName) ? "Room" : state.RoomName;
            _descriptionLabel.Text = string.IsNullOrEmpty(state.Description) ? NoDescriptionText : state.Description;
            UpdateApplyLocationButton();
        }

        private async Task LoadNowPlayingCardInfoAsync(string videoId, bool titleAlreadyCached)
        {
            if (string.IsNullOrEmpty(videoId))
            {
                _nowPlayingCard.ShowLoading(false);
                _hostNowPlayingCard.ShowLoading(false);
                return;
            }

            var result = await FetchVideoInfoAsync(videoId, titleAlreadyCached).ConfigureAwait(false);
            if (!_isViewActive)
                return;

            ApplyVideoInfoToCard(_nowPlayingCard, result.Thumbnail, result.Info);
            ApplyVideoInfoToCard(_hostNowPlayingCard, result.Thumbnail, result.Info);
        }

        private async Task<(AsyncTexture2D Thumbnail, YouTubeVideoInfo Info)> FetchVideoInfoAsync(string videoId, bool titleAlreadyCached)
        {
            var textureService = CinemaModule.Instance?.TextureService;
            if (textureService == null)
                return (null, null);

            try
            {
                var thumbnailTask = textureService.GetYouTubeThumbnailAsync(videoId);
                var infoTask = titleAlreadyCached 
                    ? Task.FromResult<YouTubeVideoInfo>(null) 
                    : _youtubeService.GetVideoInfoAsync(videoId);

                await Task.WhenAll(thumbnailTask, infoTask).ConfigureAwait(false);

                var info = infoTask.Result;
                if (info != null)
                    _videoTitleCache[videoId] = info.Title;

                return (thumbnailTask.Result, info);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to fetch video info for {videoId}: {ex.Message}");
                return (null, null);
            }
        }

        private void ApplyVideoInfoToCard(ListCard card, AsyncTexture2D thumbnail, YouTubeVideoInfo info)
        {
            try
            {
                if (thumbnail != null)
                    card.SetAvatar(thumbnail);

                if (info != null)
                    card.Title = info.Title;

                card.ShowLoading(false);
            }
            catch (ObjectDisposedException) { }
        }

        #endregion

        #region Event Handlers

        private void SubscribeToEvents()
        {
            _controller.RoomsUpdated += OnRoomsUpdated;
            _controller.StateChanged += OnStateChanged;
            _controller.RoomJoined += OnRoomJoined;
            _controller.RoomLeft += OnRoomLeft;
            _controller.HostStatusChanged += OnHostStatusChanged;
            _controller.ErrorOccurred += OnErrorOccurred;
            _controller.MemberBanned += OnMemberBanned;
            _controller.ApiAvailabilityChanged += OnApiAvailabilityChanged;
            _controller.ServerStatusChanged += OnServerStatusChanged;
        }

        private void OnRoomsUpdated(object sender, List<WatchPartyRoom> rooms)
        {
            PopulateRoomList(rooms);
        }

        private void OnStateChanged(object sender, WatchPartyStateArgs e)
        {
            var state = e.State;
            if (state == null) return;

            if (e.ChangeType == WatchPartyStateChangeType.MemberTimesUpdated ||
                e.ChangeType == WatchPartyStateChangeType.MemberStatesUpdated)
            {
                RefreshMemberInfo(state);
                return;
            }

            if (HasQueueChanged(state.Queue))
                PopulateQueue(state.Queue);

            UpdateNowPlaying(state);
            UpdateDescription(state);
            UpdateQueueLimitDropdown(state.MaxQueuePerUser);
            UpdateAddToQueueButton(state);

            if (HasMembersChanged(state.Members))
                PopulateMembers(state.Members);
            else
                RefreshMemberInfo(state);

            UpdateHostControlsVisibility();
        }

        private void UpdateHostControlsVisibility()
        {
            if (!_controller.IsInRoom) return;

            bool isHost = _controller.IsHost;
            _hostControlsSection.Visible = isHost;
            _statusSection.Visible = !isHost;
            _resyncButton.Visible = !isHost;
            UpdateQueuePosition();
        }

        private void UpdateAddToQueueButton(WatchPartyLocalState state)
        {
            if (state == null || state.MaxQueuePerUser == 0)
            {
                _addToQueueButton.Enabled = true;
                _addToQueueButton.BasicTooltipText = "Add a YouTube video to the queue";
                return;
            }

            string localName = _controller.LocalGw2Name;
            int userQueueCount = 0;
            foreach (var item in state.Queue)
            {
                if (string.Equals(item.AddedBy, localName, StringComparison.OrdinalIgnoreCase))
                    userQueueCount++;
            }

            bool canAdd = userQueueCount < state.MaxQueuePerUser;
            _addToQueueButton.Enabled = canAdd;
            _addToQueueButton.BasicTooltipText = canAdd
                ? $"Add a YouTube video to the queue ({userQueueCount}/{state.MaxQueuePerUser})"
                : $"Queue limit reached ({userQueueCount}/{state.MaxQueuePerUser}).";
        }

        private void OnRoomJoined(object sender, EventArgs e)
        {
            Logger.Info($"Room joined: {_controller.CurrentRoom?.RoomName}");
            ShowRoomView();
        }

        private void OnHostStatusChanged(object sender, bool isHost)
        {
            UpdateHostControlsVisibility();

            if (_controller.CurrentState != null)
                PopulateQueue(_controller.CurrentState.Queue);
        }

        private void OnRoomLeft(object sender, EventArgs e)
        {
            ShowLobbyView();
        }

        private void OnRoomCardClicked(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            if (!(sender is ListCard card)) return;
            if (!_roomCardMap.TryGetValue(card, out var room)) return;

            string activeRoomId = _controller.CurrentRoom?.RoomId;
            if (activeRoomId != null && room.RoomId == activeRoomId)
            {
                _selectedRoom = null;
                _joinRoomButton.Enabled = false;
                return;
            }

            foreach (var kvp in _roomCardMap)
                kvp.Key.IsSelected = false;

            card.IsSelected = true;
            _selectedRoom = room;
            _joinRoomButton.Enabled = _controller.IsApiAvailable;
        }

        private void JoinSelectedRoom()
        {
            if (_selectedRoom == null) return;

            if (_selectedRoom.IsPrivate)
                ShowPasswordPrompt(_selectedRoom.RoomId);
            else
                _ = _controller.JoinRoomAsync(_selectedRoom.RoomId);
        }

        private void OnErrorOccurred(object sender, string message)
        {
            Logger.Warn($"Watch party error: {message}");
        }

        private void OnMemberBanned(object sender, string username)
        {
            if (HasMembersChanged(_controller.CurrentState?.Members ?? new List<string>()))
                PopulateMembers(_controller.CurrentState.Members);
        }

        private void OnApiAvailabilityChanged(object sender, EventArgs e)
        {
            bool isAvailable = _controller.IsApiAvailable;
            _createRoomButton.Visible = isAvailable;
            _apiWarningLabel.Visible = !isAvailable;
            UpdateCreateRoomButtonState();

            if (_selectedRoom != null)
                _joinRoomButton.Enabled = isAvailable;
        }

        private void UpdateCreateRoomButtonState()
        {
            bool hasRoomName = !string.IsNullOrWhiteSpace(_roomNameBox?.Text);
            bool apiAvailable = _controller.IsApiAvailable;

            _createRoomButton.Enabled = hasRoomName && apiAvailable;

            if (!apiAvailable)
                _createRoomButton.BasicTooltipText = "API key with Account permission required";
            else if (!hasRoomName)
                _createRoomButton.BasicTooltipText = "Enter a room name to create a room";
            else
                _createRoomButton.BasicTooltipText = "Create a new watch party room";
        }

        private void OnServerStatusChanged(object sender, EventArgs e)
        {
            UpdateServerStatusLabel();
        }

        private void OnQueueLimitChanged(object sender, ValueChangedEventArgs e)
        {
            if (!_controller.IsHost) return;

            int limit = e.CurrentValue == "Unlimited" ? 0 : int.Parse(e.CurrentValue);
            _ = _controller.UpdateMaxQueuePerUserAsync(limit);
        }

        private void UpdateQueueLimitDropdown(int maxQueuePerUser)
        {
            _queueLimitDropdown.ValueChanged -= OnQueueLimitChanged;
            _queueLimitDropdown.SelectedItem = maxQueuePerUser == 0 ? "Unlimited" : maxQueuePerUser.ToString();
            _queueLimitDropdown.ValueChanged += OnQueueLimitChanged;
        }

        #endregion

        protected override void Unload()
        {
            _isViewActive = false;

            _controller.RoomsUpdated -= OnRoomsUpdated;
            _controller.StateChanged -= OnStateChanged;
            _controller.RoomJoined -= OnRoomJoined;
            _controller.RoomLeft -= OnRoomLeft;
            _controller.HostStatusChanged -= OnHostStatusChanged;
            _controller.ErrorOccurred -= OnErrorOccurred;
            _controller.MemberBanned -= OnMemberBanned;
            _controller.ApiAvailabilityChanged -= OnApiAvailabilityChanged;
            _controller.ServerStatusChanged -= OnServerStatusChanged;

            foreach (var card in _roomCardMap.Keys)
                card.Click -= OnRoomCardClicked;

            base.Unload();
        }
    }
}
