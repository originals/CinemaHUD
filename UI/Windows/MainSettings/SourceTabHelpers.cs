using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using CinemaModule.Models;
using CinemaModule.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaHUD.UI.Windows.MainSettings
{
    public class StreamStatus
    {
        private static readonly Color AvailableColor = new Color(100, 200, 100);

        public bool IsOnline { get; set; }
        public string Subtitle { get; set; }
        public Color SubtitleColor { get; set; } = Color.Gray;
        public string AvatarUrl { get; set; }

        public static StreamStatus Offline() => new StreamStatus
        {
            IsOnline = false,
            Subtitle = "Offline",
            SubtitleColor = Color.Gray
        };

        public static StreamStatus Available() => new StreamStatus
        {
            IsOnline = true,
            Subtitle = "Available",
            SubtitleColor = AvailableColor
        };

        public static StreamStatus OnDemand() => new StreamStatus
        {
            IsOnline = true,
            Subtitle = "Playback Ready",
            SubtitleColor = new Color(180, 200, 220)
        };

        public static StreamStatus Live(string gameName, int viewerCount) => new StreamStatus
        {
            IsOnline = true,
            Subtitle = $"LIVE: {gameName ?? "Streaming"} - {viewerCount:N0} viewers",
            SubtitleColor = AvailableColor
        };
    }

    public class StreamListItem
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public Color SubtitleColor { get; set; } = Color.Gray;
        public AsyncTexture2D AvatarTexture { get; set; }
        public ChannelData ChannelData { get; set; }
        public string TwitchChannel { get; set; }
        public string AvatarUrl { get; set; }
        public bool IsOnline { get; set; }
        public bool IsOnDemand { get; set; }

        public void ApplyStatus(StreamStatus status)
        {
            IsOnline = status.IsOnline;
            Subtitle = status.Subtitle;
            SubtitleColor = status.SubtitleColor;
            AvatarUrl = string.IsNullOrEmpty(status.AvatarUrl) ? AvatarUrl : status.AvatarUrl;
        }
    }

    public class StreamCardFactory
    {
        private const int CardTextPanelWidth = 220;

        private readonly TextureService _textureService;
        private readonly TwitchService _twitchService;
        private readonly Func<string> _getSelectedKey;
        private readonly Dictionary<string, ListCard> _streamCards;

        public Action<string> OnOpenChat { get; set; }
        public Action<string> OnCopyWaypoint { get; set; }
        public Action<ChannelData> OnApplyWorldPosition { get; set; }

        public StreamCardFactory(
            TextureService textureService,
            TwitchService twitchService,
            Func<string> getSelectedKey,
            Dictionary<string, ListCard> streamCards)
        {
            _textureService = textureService;
            _twitchService = twitchService;
            _getSelectedKey = getSelectedKey;
            _streamCards = streamCards;
        }

        public ListCard CreateFollowedCard(FlowPanel parent, string key, TwitchStreamInfo stream, Action<string, string> onSelect)
        {
            var buttons = CreateTwitchButtons(stream.ChannelName);
            var status = StreamStatus.Live(stream.GameName, stream.ViewerCount);
            var card = CreateCard(parent, key, stream.ChannelName, status, buttons, _textureService.GetDefaultAvatar());
            card.Click += (s, e) => onSelect(stream.ChannelName, key);
            return card;
        }

        public ListCard CreateTwitchCard(FlowPanel parent, StreamListItem item, Action<string, string> onSelect)
        {
            var buttons = CreateTwitchButtons(item.TwitchChannel);
            var status = new StreamStatus { Subtitle = item.Subtitle, SubtitleColor = item.SubtitleColor };
            var card = CreateCard(parent, item.Key, item.Title, status, buttons, item.AvatarTexture);
            card.Click += (s, e) => onSelect(item.TwitchChannel, item.Key);
            return card;
        }

        public ListCard CreateChannelCard(FlowPanel parent, StreamListItem item, Action<ChannelData, string> onSelect)
        {
            var buttons = CreateChannelButtons(item.ChannelData);
            var status = new StreamStatus { Subtitle = item.Subtitle, SubtitleColor = item.SubtitleColor };
            var card = CreateCard(parent, item.Key, item.Title, status, buttons, item.AvatarTexture);
            card.Click += (s, e) => onSelect(item.ChannelData, item.Key);
            return card;
        }

        public ListCard CreateCustomCard(FlowPanel parent, string key, SavedStream stream, StreamStatus status,
            Action onDelete, Action onEdit, Action<SavedStream> onSelect)
        {
            var buttons = CreateCustomButtons(stream, onDelete, onEdit);
            var effectiveStatus = status ?? new StreamStatus { Subtitle = stream.Value };
            var card = CreateCard(parent, key, stream.Name, effectiveStatus, buttons, _textureService.GetDefaultAvatar());
            card.Click += (s, e) => onSelect(stream);
            return card;
        }

        private ListCard CreateCard(FlowPanel parent, string key, string title, StreamStatus status,
            List<ListCardButton> buttons, AsyncTexture2D avatar)
        {
            var subtitle = status.Subtitle ?? string.Empty;
            var card = new ListCard(parent, title, subtitle, _getSelectedKey() == key, CardTextPanelWidth, buttons, avatar);
            card.SetSubtitle(subtitle, status.SubtitleColor);
            _streamCards[key] = card;
            return card;
        }

        private List<ListCardButton> CreateTwitchButtons(string channelName)
        {
            var twitchUrl = _twitchService.GetChannelUrl(channelName);
            return new List<ListCardButton>
            {
                CreateIconButton(_textureService.GetInfoIcon(), "Open in Browser", () => OpenUrl(twitchUrl)),
                CreateIconButton(_textureService.GetTwitchChatIcon(), "Open Chat", () => OnOpenChat?.Invoke(channelName))
            };
        }

        private List<ListCardButton> CreateChannelButtons(ChannelData channel)
        {
            var buttons = new List<ListCardButton>();
            if (!string.IsNullOrEmpty(channel?.InfoUrl))
                buttons.Add(CreateIconButton(_textureService.GetInfoIcon(), "Open in Browser", () => OpenUrl(channel.InfoUrl)));
            if (!string.IsNullOrEmpty(channel?.YoutubeUrl))
                buttons.Add(CreateIconButton(_textureService.GetYoutubeIcon(), "Watch on YouTube", () => OpenUrl(channel.YoutubeUrl)));
            if (!string.IsNullOrEmpty(channel?.Waypoint))
                buttons.Add(CreateIconButton(_textureService.GetWaypointIcon(), "Copy Waypoint", () => OnCopyWaypoint?.Invoke(channel.Waypoint)));
            if (channel?.HasWorldPosition == true)
                buttons.Add(CreateIconButton(_textureService.GetSetScreenIcon(), "Set Ingame Screen Position", () => OnApplyWorldPosition?.Invoke(channel)));
            return buttons;
        }

        private List<ListCardButton> CreateCustomButtons(SavedStream stream, Action onDelete, Action onEdit)
        {
            var buttons = new List<ListCardButton> { CreateIconButton(_textureService.GetDeleteIcon(), "Delete", onDelete) };
            if (stream.SourceType == StreamSourceType.TwitchChannel)
                buttons.Add(CreateIconButton(_textureService.GetTwitchChatIcon(), "Open Chat", () => OnOpenChat?.Invoke(stream.Value)));
            buttons.Add(new ListCardButton { Text = "Edit", Width = 50, OnClick = onEdit });
            return buttons;
        }

        private ListCardButton CreateIconButton(AsyncTexture2D icon, string tooltip, Action onClick) =>
            new ListCardButton { Text = "", Width = 30, Icon = icon, Tooltip = tooltip, OnClick = onClick };

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.GetLogger<StreamCardFactory>().Debug($"Failed to open URL: {ex.Message}");
            }
        }
    }

    public class StreamStatusLoader
    {
        private static readonly Logger Logger = Logger.GetLogger<StreamStatusLoader>();
        private readonly TwitchService _twitchService;

        public StreamStatusLoader(TwitchService twitchService) => _twitchService = twitchService;

        public async Task FetchTwitchStatusesAsync(List<StreamListItem> items, CancellationToken token)
        {
            var channels = items.Select(i => i.TwitchChannel).Where(c => !string.IsNullOrEmpty(c)).ToList();
            if (channels.Count == 0) return;

            try
            {
                var infos = await _twitchService.GetMultipleStreamInfoAsync(channels);
                if (token.IsCancellationRequested) return;
                foreach (var item in items)
                    item.ApplyStatus(CreateTwitchStatus(infos, item.TwitchChannel));
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Logger.Warn(ex, "Failed to fetch Twitch statuses");
            }
        }

        public async Task FetchUrlStatusesAsync(List<StreamListItem> items, CancellationToken token)
        {
            var tasks = items
                .Where(i => !string.IsNullOrEmpty(i.ChannelData?.Url))
                .Select(i => FetchUrlStatusAsync(i, token));
            await Task.WhenAll(tasks);
        }

        public async Task<Dictionary<string, StreamStatus>> FetchCustomStreamStatusesAsync(
            List<SavedStream> streams, CancellationToken token)
        {
            var statusMap = new Dictionary<string, StreamStatus>();
            var twitchStreams = streams.Where(s => s.SourceType == StreamSourceType.TwitchChannel).ToList();
            var urlStreams = streams.Where(s => s.SourceType == StreamSourceType.Url).ToList();

            await Task.WhenAll(
                FetchTwitchCustomStatusesAsync(twitchStreams, statusMap, token),
                FetchUrlCustomStatusesAsync(urlStreams, statusMap, token));
            return statusMap;
        }

        public static StreamStatus CreateTwitchStatus(Dictionary<string, TwitchStreamInfo> infos, string channelName)
        {
            if (infos.TryGetValue(channelName, out var info))
            {
                var status = info.IsLive ? StreamStatus.Live(info.GameName, info.ViewerCount) : StreamStatus.Offline();
                status.AvatarUrl = info.AvatarUrl;
                return status;
            }
            return StreamStatus.Offline();
        }

        private async Task FetchUrlStatusAsync(StreamListItem item, CancellationToken token)
        {
            try
            {
                var result = await _twitchService.CheckUrlAvailabilityAsync(item.ChannelData.Url);
                if (token.IsCancellationRequested) return;
                item.ApplyStatus(GetUrlStatus(result.IsAvailable == true, item.IsOnDemand));
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) return;
                Logger.Debug($"Failed to check URL status: {ex.Message}");
                item.ApplyStatus(StreamStatus.Offline());
            }
        }

        private static StreamStatus GetUrlStatus(bool isAvailable, bool isOnDemand)
        {
            if (!isAvailable) return StreamStatus.Offline();
            return isOnDemand ? StreamStatus.OnDemand() : StreamStatus.Available();
        }

        private async Task FetchTwitchCustomStatusesAsync(List<SavedStream> twitchStreams,
            Dictionary<string, StreamStatus> statusMap, CancellationToken token)
        {
            if (twitchStreams.Count == 0) return;
            var channelNames = twitchStreams.Select(s => s.Value).Where(v => !string.IsNullOrEmpty(v)).ToList();
            if (channelNames.Count == 0) return;

            try
            {
                var infos = await _twitchService.GetMultipleStreamInfoAsync(channelNames);
                if (token.IsCancellationRequested) return;
                foreach (var stream in twitchStreams)
                    statusMap[stream.Id] = CreateTwitchStatus(infos, stream.Value);
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Logger.Debug($"Failed to fetch Twitch statuses for custom streams: {ex.Message}");
            }
        }

        private async Task FetchUrlCustomStatusesAsync(List<SavedStream> urlStreams,
            Dictionary<string, StreamStatus> statusMap, CancellationToken token)
        {
            if (urlStreams.Count == 0) return;

            var tasks = urlStreams.Select(async stream =>
            {
                try
                {
                    var result = await _twitchService.CheckUrlAvailabilityAsync(stream.Value);
                    if (token.IsCancellationRequested) return;
                    statusMap[stream.Id] = GetUrlStatus(result.IsAvailable == true, false);
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested) return;
                    Logger.Debug($"Failed to check URL status for {stream.Name}: {ex.Message}");
                    statusMap[stream.Id] = new StreamStatus { Subtitle = "Unknown" };
                }
            });
            await Task.WhenAll(tasks);
        }
    }
}
