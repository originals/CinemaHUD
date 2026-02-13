namespace CinemaModule.Models
{
    public class TwitchStreamInfo
    {
        public string ChannelName { get; set; }
        public bool IsLive { get; set; }
        public string Title { get; set; }
        public string GameName { get; set; }
        public int ViewerCount { get; set; }
        public string AvatarUrl { get; set; }
    }

    public class TwitchStreamQuality
    {
        public string DisplayName { get; set; }

        public string StreamUrl { get; set; }
    }
}
