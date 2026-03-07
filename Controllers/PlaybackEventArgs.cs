using System;

namespace CinemaModule.Controllers
{
    public abstract class StreamRefreshedEventArgs : EventArgs
    {
        public string Identifier { get; }
        public string StreamUrl { get; }

        protected StreamRefreshedEventArgs(string identifier, string streamUrl)
        {
            Identifier = identifier;
            StreamUrl = streamUrl;
        }
    }

    public class TwitchStreamRefreshedEventArgs : StreamRefreshedEventArgs
    {
        public string ChannelName => Identifier;

        public TwitchStreamRefreshedEventArgs(string channelName, string streamUrl)
            : base(channelName, streamUrl) { }
    }

    public class YouTubeStreamRefreshedEventArgs : StreamRefreshedEventArgs
    {
        public string VideoId => Identifier;

        public YouTubeStreamRefreshedEventArgs(string videoId, string streamUrl)
            : base(videoId, streamUrl) { }
    }
}
