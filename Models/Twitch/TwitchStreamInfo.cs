namespace CinemaModule.Models.Twitch
{
    public class TwitchStreamInfo
    {
        public string ChannelName { get; set; }
        public string UserId { get; set; }
        public string Title { get; set; }
        public string GameName { get; set; }
        public string AvatarUrl { get; set; }
        public int ViewerCount { get; set; }
        public bool IsLive { get; set; }

        public TwitchStreamInfo() { }

        public TwitchStreamInfo(string channelName, bool isLive = false)
        {
            ChannelName = channelName;
            IsLive = isLive;
        }
    }

    public class TwitchStreamQuality
    {
        public string DisplayName { get; }
        public string StreamUrl { get; }

        public TwitchStreamQuality(string displayName, string streamUrl)
        {
            DisplayName = displayName;
            StreamUrl = streamUrl;
        }
    }
}
