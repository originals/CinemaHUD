using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using CinemaHUD.UI.Windows.SettingsSmall;
using CinemaModule;
using CinemaModule.Models;
using CinemaModule.Services;
using CinemaModule.Settings;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaHUD.UI.Windows.MainSettings
{
    public class SourceTabView : View
    {
        #region Members

        private static readonly Logger Logger = Logger.GetLogger<SourceTabView>();

        private const int SavedStreamTextPanelWidth = 300;
        private const int PanelLeftPadding = 55;
        private const int ControlVerticalSpacing = 10;
        private const int CardVerticalSpacing = 4;

        private const string KeyPrefixPreset = "preset:";
        private const string KeyPrefixPresetTwitch = "preset_twitch:";
        private const string KeyPrefixSaved = "saved:";

        private static readonly Color AvailableColor = new Color(100, 200, 100);

        private readonly CinemaUserSettings _settings;
        private readonly CinemaController _controller;
        private readonly TwitchService _twitchService;
        private readonly PresetService _presetService;

        private FlowPanel _streamContainer;
        private Dictionary<string, ListCard> _streamCards = new Dictionary<string, ListCard>();

        private string _selectedStreamKey;

        private StreamEditorWindow _editorWindow;
        private CancellationTokenSource _cts;
        private CancellationTokenSource _rebuildCts;
        private EventHandler _savedStreamsChangedHandler;
        private EventHandler _presetsLoadedHandler;

        #endregion

        public SourceTabView(CinemaUserSettings settings, CinemaController controller, TwitchService twitchService, PresetService presetService)
        {
            _settings = settings;
            _controller = controller;
            _twitchService = twitchService;
            _presetService = presetService;
            _cts = new CancellationTokenSource();
        }

        protected override void Build(Container buildPanel)
        {
            InitializeSelectedStreamKey();

            var panel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.Fill,
                OuterControlPadding = new Vector2(PanelLeftPadding, 0),
                ControlPadding = new Vector2(0, ControlVerticalSpacing),
                Parent = buildPanel
            };

            BuildToolbarButtons(buildPanel);

            BuildStreamSection(panel);

            _savedStreamsChangedHandler = (s, e) => RebuildStreamList();
            _settings.SavedStreamsChanged += _savedStreamsChangedHandler;

            _presetsLoadedHandler = (s, e) => RebuildStreamList();
            _presetService.PresetsLoaded += _presetsLoadedHandler;
        }

        #region Private Methods

        private void BuildToolbarButtons(Container parent)
        {
            var addButton = new StandardButton
            {
                Text = "+ Add New",
                Width = 100,
                Left = 405,
                Parent = parent
            };
            addButton.Click += (s, e) => OpenEditorForNew();
        }

        private void BuildStreamSection(Container parent)
        {
            new Label
            {
                Text = "Select stream",
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Font = GameService.Content.DefaultFont16,
                Parent = parent
            };

            new Panel
            {
                WidthSizingMode = SizingMode.Fill,
                Height = 6,
                Parent = parent
            };

            _streamContainer = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                WidthSizingMode = SizingMode.Fill,
                Height = 500,
                ControlPadding = new Vector2(0, CardVerticalSpacing),
                CanScroll = true,
                Parent = parent
            };

            RebuildStreamList();
        }

        private void RebuildStreamList()
        {
            // Cancel any in-progress rebuild to prevent duplicate cards
            _rebuildCts?.Cancel();
            _rebuildCts?.Dispose();
            _rebuildCts = new CancellationTokenSource();

            _streamContainer?.ClearChildren();
            _streamCards.Clear();
            InitializeSelectedStreamKey();

            RebuildStreamListAsync(_rebuildCts.Token);
        }

        private async void RebuildStreamListAsync(CancellationToken rebuildToken)
        {
            var items = BuildStreamItems();
            if (rebuildToken.IsCancellationRequested || _cts.IsCancellationRequested) return;

            await FetchStreamStatusesAsync(items, rebuildToken);
            if (rebuildToken.IsCancellationRequested || _cts.IsCancellationRequested) return;

            var sortedItems = items.OrderByDescending(i => i.IsOnline).ToList();

            foreach (var item in sortedItems)
            {
                if (rebuildToken.IsCancellationRequested) return;
                CreateCardForItem(item);
            }

            _ = LoadAvatarsAsync(sortedItems, rebuildToken);
        }

        private List<StreamListItem> BuildStreamItems()
        {
            var items = new List<StreamListItem>();
            var addedTwitchChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var preset in _presetService.StreamPresets)
            {
                var item = new StreamListItem
                {
                    Key = GetPresetKey(preset.Id),
                    Title = preset.Name,
                    Subtitle = string.IsNullOrEmpty(preset.Url) ? "No URL configured" : string.Empty,
                    ItemType = StreamListItemType.Preset,
                    PresetData = preset,
                    AvatarTexture = CinemaModule.CinemaModule.Instance.TextureService.GetDefaultAvatar()
                };
                items.Add(item);
            }

            foreach (var channel in _presetService.TwitchChannels)
            {
                var item = new StreamListItem
                {
                    Key = GetPresetTwitchKey(channel),
                    Title = channel,
                    Subtitle = string.Empty,
                    ItemType = StreamListItemType.PresetTwitch,
                    TwitchChannel = channel,
                    IconTexture = CinemaModule.CinemaModule.Instance.TextureService.GetTwitchIcon(),
                    AvatarTexture = CinemaModule.CinemaModule.Instance.TextureService.GetDefaultAvatar()
                };
                items.Add(item);
                addedTwitchChannels.Add(channel);
            }

            foreach (var stream in _settings.SavedStreams.Streams)
            {
                var isTwitch = stream.SourceType == StreamSourceType.TwitchChannel;

                if (isTwitch && addedTwitchChannels.Contains(stream.Value))
                {
                    Logger.Debug($"Skipping duplicate Twitch channel: {stream.Value}");
                    continue;
                }

                var item = new StreamListItem
                {
                    Key = GetSavedStreamKey(stream.Id),
                    Title = stream.Name,
                    Subtitle = string.Empty,
                    ItemType = StreamListItemType.Saved,
                    SavedStream = stream,
                    TwitchChannel = isTwitch ? stream.Value : null,
                    IconTexture = isTwitch ? CinemaModule.CinemaModule.Instance.TextureService.GetTwitchIcon() : null,
                    AvatarTexture = CinemaModule.CinemaModule.Instance.TextureService.GetDefaultAvatar()
                };
                items.Add(item);

                if (isTwitch)
                {
                    addedTwitchChannels.Add(stream.Value);
                }
            }

            return items;
        }

        private async Task FetchStreamStatusesAsync(List<StreamListItem> items, CancellationToken rebuildToken)
        {
            var tasks = new List<Task>();

            var twitchChannels = new List<string>();
            var twitchItems = new Dictionary<string, StreamListItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                if (rebuildToken.IsCancellationRequested) return;

                switch (item.ItemType)
                {
                    case StreamListItemType.Preset:
                        if (!string.IsNullOrEmpty(item.PresetData.Url))
                        {
                            tasks.Add(FetchUrlStatusAsync(item.PresetData.Url, item, rebuildToken));
                        }
                        break;

                    case StreamListItemType.PresetTwitch:
                        twitchChannels.Add(item.TwitchChannel);
                        twitchItems[item.TwitchChannel] = item;
                        break;

                    case StreamListItemType.Saved:
                        if (item.SavedStream.SourceType == StreamSourceType.TwitchChannel)
                        {
                            twitchChannels.Add(item.SavedStream.Value);
                            twitchItems[item.SavedStream.Value] = item;
                        }
                        else
                        {
                            tasks.Add(FetchUrlStatusAsync(item.SavedStream.Value, item, rebuildToken));
                        }
                        break;
                }
            }

            if (twitchChannels.Count > 0)
            {
                tasks.Add(FetchMultipleTwitchStatusAsync(twitchChannels, twitchItems, rebuildToken));
            }

            await Task.WhenAll(tasks);
        }

        private async Task FetchMultipleTwitchStatusAsync(
            List<string> channelNames,
            Dictionary<string, StreamListItem> itemsByChannel,
            CancellationToken rebuildToken)
        {
            try
            {
                var streamInfos = await _twitchService.GetMultipleStreamInfoAsync(channelNames);
                if (rebuildToken.IsCancellationRequested || _cts.IsCancellationRequested) return;

                foreach (var kvp in streamInfos)
                {
                    var channelName = kvp.Key;
                    var streamInfo = kvp.Value;

                    if (!itemsByChannel.TryGetValue(channelName, out var item))
                        continue;

                    item.IsOnline = streamInfo.IsLive;
                    item.AvatarUrl = streamInfo.AvatarUrl;

                    if (streamInfo.IsLive)
                    {
                        item.Subtitle = $"@{channelName} - LIVE: {streamInfo.GameName ?? "Streaming"}";
                        item.SubtitleColor = AvailableColor;
                    }
                    else
                    {
                        item.Subtitle = $"@{channelName} - Offline";
                        item.SubtitleColor = Color.Gray;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // unload?
            }
            catch (Exception ex)
            {
                if (rebuildToken.IsCancellationRequested || _cts.IsCancellationRequested) return;
                Logger.Warn(ex, "Failed to check status for multiple Twitch channels");

                foreach (var channelName in channelNames)
                {
                    if (itemsByChannel.TryGetValue(channelName, out var item))
                    {
                        item.Subtitle = $"@{channelName} - Status unknown";
                        item.SubtitleColor = Color.Gray;
                        item.IsOnline = false;
                    }
                }
            }
        }

        private async Task UpdateAvatarAsync(string itemKey, string channelName, string avatarUrl, CancellationToken rebuildToken)
        {
            try
            {
                var avatarTexture = await _twitchService.GetAvatarTextureAsync(channelName, avatarUrl);
                if (rebuildToken.IsCancellationRequested || _cts.IsCancellationRequested)
                    return;

                if (avatarTexture != null)
                    UpdateCardStatus(itemKey, null, null, avatarTexture);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to load avatar for {channelName}: {ex.Message}");
            }
        }

        private async Task FetchUrlStatusAsync(string url, StreamListItem item, CancellationToken rebuildToken)
        {
            try
            {
                var result = await _twitchService.CheckUrlAvailabilityAsync(url);
                if (rebuildToken.IsCancellationRequested || _cts.IsCancellationRequested) return;

                item.IsOnline = result.IsAvailable == true;
                item.Subtitle = item.IsOnline ? "LIVE" : "Offline";
                item.SubtitleColor = item.IsOnline ? AvailableColor : Color.Gray;
            }
            catch (OperationCanceledException)
            {
                // unload?
            }
            catch (Exception ex)
            {
                if (rebuildToken.IsCancellationRequested || _cts.IsCancellationRequested) return;
                Logger.Warn(ex, $"Failed to check status for URL: {url}");
                item.Subtitle = "Offline";
                item.SubtitleColor = Color.Gray;
                item.IsOnline = false;
            }
        }

        private async Task LoadAvatarsAsync(List<StreamListItem> items, CancellationToken rebuildToken)
        {
            var avatarTasks = new List<Task>();

            foreach (var item in items)
            {
                if (rebuildToken.IsCancellationRequested) return;

                string avatarUrl = item.AvatarUrl ?? item.PresetData?.Avatar;
                string cacheKey = item.TwitchChannel ?? item.PresetData?.Id;

                if (!string.IsNullOrEmpty(avatarUrl) && !string.IsNullOrEmpty(cacheKey))
                    avatarTasks.Add(UpdateAvatarAsync(item.Key, cacheKey, avatarUrl, rebuildToken));
            }

            await Task.WhenAll(avatarTasks);
        }

        private void UpdateCardStatus(string key, string subtitle, Color? subtitleColor, AsyncTexture2D avatarTexture)
        {
            if (_streamCards.TryGetValue(key, out var card))
            {
                if (subtitle != null && subtitleColor.HasValue)
                {
                    card.SetSubtitle(subtitle, subtitleColor.Value);
                }
                if (avatarTexture != null)
                {
                    card.SetAvatar(avatarTexture);
                }
            }
        }

        private void CreateCardForItem(StreamListItem item)
        {
            IEnumerable<ListCardButton> buttons = null;
            int textPanelWidth = ListCard.DefaultTextPanelWidth;

            if (item.ItemType == StreamListItemType.Saved)
            {
                textPanelWidth = SavedStreamTextPanelWidth;
                buttons = new List<ListCardButton>
                {
                    new ListCardButton { Text = "X", Width = 30, OnClick = () => _settings.DeleteSavedStream(item.SavedStream.Id) },
                    new ListCardButton { Text = "Edit", Width = 50, OnClick = () => OpenEditorForEdit(item.SavedStream) }
                };
            }
            else if (item.ItemType == StreamListItemType.Preset && !string.IsNullOrEmpty(item.PresetData?.InfoUrl))
            {
                textPanelWidth = SavedStreamTextPanelWidth;
                var infoUrl = item.PresetData.InfoUrl;
                buttons = new List<ListCardButton>
                {
                    new ListCardButton { Text = "Info", Width = 50, OnClick = () => OpenUrlInBrowser(infoUrl) }
                };
            }
            else if (item.ItemType == StreamListItemType.PresetTwitch && !string.IsNullOrEmpty(item.TwitchChannel))
            {
                textPanelWidth = SavedStreamTextPanelWidth;
                var twitchUrl = _twitchService.GetChannelUrl(item.TwitchChannel);
                buttons = new List<ListCardButton>
                {
                    new ListCardButton { Text = "Info", Width = 50, OnClick = () => OpenUrlInBrowser(twitchUrl) }
                };
            }

            var card = CreateCard(item.Key, item.Title, item.Subtitle, textPanelWidth, buttons, item.IconTexture);
            card.SetSubtitle(item.Subtitle, item.SubtitleColor);

            if (item.AvatarTexture != null)
            {
                card.SetAvatar(item.AvatarTexture);
            }

            switch (item.ItemType)
            {
                case StreamListItemType.Preset:
                    card.Click += (s, e) => SelectPresetStream(item.PresetData, item.Key);
                    break;
                case StreamListItemType.PresetTwitch:
                    card.Click += (s, e) => SelectTwitchChannelAsync(item.TwitchChannel, item.Key);
                    break;
                case StreamListItemType.Saved:
                    card.Click += (s, e) => SelectSavedStreamAsync(item.SavedStream);
                    break;
            }
        }

        private void SelectPresetStream(StreamPresetData preset, string key)
        {
            _selectedStreamKey = key;
            _settings.SelectedSavedStreamId = "";
            _settings.CurrentTwitchChannel = "";
            _settings.CurrentStreamSourceType = StreamSourceType.Url;
            _settings.StreamUrl = preset.Url;
            UpdateCardSelection();
        }

        private async void SelectTwitchChannelAsync(string channelName, string key)
        {
            _selectedStreamKey = key;
            _settings.SelectedSavedStreamId = "";
            _settings.CurrentTwitchChannel = channelName;
            _settings.CurrentStreamSourceType = StreamSourceType.TwitchChannel;
            UpdateCardSelection();

            await TrySetStreamUrlAsync(
                () => _twitchService.GetPlayableStreamUrlAsync(channelName),
                $"Twitch channel: {channelName}");
        }

        private async void SelectSavedStreamAsync(SavedStream stream)
        {
            _selectedStreamKey = GetSavedStreamKey(stream.Id);
            _controller.SelectSavedStream(stream.Id);
            UpdateCardSelection();

            if (stream.SourceType == StreamSourceType.TwitchChannel)
            {
                await TrySetStreamUrlAsync(
                    () => _twitchService.GetPlayableStreamUrlAsync(stream.Value),
                    $"stream: {stream.Name}");
            }
            else
            {
                _settings.StreamUrl = stream.Value;
                Logger.Info($"Selected stream: {stream.Name}");
            }
        }

        private void OpenEditorForNew()
        {
            EnsureEditorWindow();
            _editorWindow.OpenForNew();
        }

        private void OpenEditorForEdit(SavedStream stream)
        {
            EnsureEditorWindow();
            _editorWindow.OpenForEdit(stream);
        }

        private void OpenUrlInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to open URL in browser: {url}");
            }
        }

        private void EnsureEditorWindow()
        {
            if (_editorWindow == null)
            {
                _editorWindow = new StreamEditorWindow(_settings);
                _editorWindow.StreamSaved += (s, e) => RebuildStreamList();
                _editorWindow.StreamDeleted += (s, e) => RebuildStreamList();
            }
        }

        private void UpdateCardSelection()
        {
            foreach (var kvp in _streamCards)
            {
                kvp.Value.IsSelected = kvp.Key == _selectedStreamKey;
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
                foreach (var channel in _presetService.TwitchChannels)
                {
                    if (string.Equals(channel, _settings.CurrentTwitchChannel, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedStreamKey = GetPresetTwitchKey(channel);
                        return;
                    }
                }
            }
        }

        private static string GetPresetKey(string presetId) => $"{KeyPrefixPreset}{presetId}";
        private static string GetPresetTwitchKey(string channel) => $"{KeyPrefixPresetTwitch}{channel}";
        private static string GetSavedStreamKey(string id) => $"{KeyPrefixSaved}{id}";

        private ListCard CreateCard(
            string key,
            string title,
            string subtitle,
            int textPanelWidth = ListCard.DefaultTextPanelWidth,
            IEnumerable<ListCardButton> buttons = null,
            AsyncTexture2D iconTexture = null)
        {
            bool isSelected = _selectedStreamKey == key;
            var card = new ListCard(_streamContainer, title, subtitle, isSelected, textPanelWidth, buttons, null, iconTexture);
            _streamCards[key] = card;
            return card;
        }

        private async Task TrySetStreamUrlAsync(Func<Task<string>> getUrlAsync, string streamDescription)
        {
            try
            {
                var url = await getUrlAsync();
                if (_cts.IsCancellationRequested) return;

                if (string.IsNullOrEmpty(url)) return;

                _settings.StreamUrl = url;
                Logger.Info($"Selected {streamDescription}");
            }
            catch (OperationCanceledException)
            {
                // unload?
            }
            catch (Exception ex)
            {
                if (_cts.IsCancellationRequested) return;
                Logger.Error(ex, $"Failed to get stream URL for {streamDescription}");
            }
        }


        #endregion

        #region Cleanup

        protected override void Unload()
        {
            _rebuildCts?.Cancel();
            _rebuildCts?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
            _settings.SavedStreamsChanged -= _savedStreamsChangedHandler;
            _presetService.PresetsLoaded -= _presetsLoadedHandler;
            _editorWindow?.Dispose();
            base.Unload();
        }

        #endregion

        private enum StreamListItemType
        {
            Preset,
            PresetTwitch,
            Saved
        }

        private class StreamListItem
        {
            public string Key { get; set; }
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public Color SubtitleColor { get; set; } = Color.Gray;
            public AsyncTexture2D AvatarTexture { get; set; }
            public AsyncTexture2D IconTexture { get; set; }
            public StreamListItemType ItemType { get; set; }
            public StreamPresetData PresetData { get; set; }
            public string TwitchChannel { get; set; }
            public string AvatarUrl { get; set; }
            public SavedStream SavedStream { get; set; }
            public bool IsOnline { get; set; }
        }
    }
}
