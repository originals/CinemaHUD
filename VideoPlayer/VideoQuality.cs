using System.Collections.Generic;

namespace CinemaModule.Player
{
    public class VideoQuality
    {
        public int TrackId { get; set; }

        public string Name { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public bool IsSelected { get; set; }

        public static string GetQualityName(int width, int height)
        {
            if (height >= 2160) return "4K";
            if (height >= 1440) return "1440p";
            if (height >= 1080) return "1080p";
            if (height >= 720) return "720p";
            if (height >= 480) return "480p";
            if (height >= 360) return "360p";
            if (height > 0) return $"{height}p";
            return "Auto";
        }

        public override string ToString()
        {
            return Name ?? GetQualityName(Width, Height);
        }
    }
}
