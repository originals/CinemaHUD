using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.UI.VideoDisplays
{
    public interface IVideoDisplay : IDisposable
    {
        bool IsPaused { get; set; }

        int Volume { get; set; }

        bool IsTwitchStream { get; set; }

        bool IsSeekable { get; set; }

        float CurrentPosition { get; set; }

        long Duration { get; set; }

        bool IsBuffering { get; set; }

        bool IsOffline { get; set; }

        Texture2D OfflineTexture { get; set; }

        void UpdateTexture(Texture2D texture);

        void UpdateAvailableQualities(IReadOnlyList<string> qualityNames, int selectedIndex);

        event EventHandler PlayPauseClicked;

        event EventHandler<int> VolumeChanged;

        event EventHandler SettingsClicked;

        event EventHandler<int> QualityChanged;

        event EventHandler TwitchChatClicked;

        event EventHandler CloseClicked;

        event EventHandler<float> SeekRequested;
    }
}
