using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.UI.Displays
{
    public interface IVideoDisplay : IDisposable
    {
        bool IsPaused { get; set; }

        int Volume { get; set; }

        bool IsTwitchStream { get; set; }

        void UpdateTexture(Texture2D texture);

        void UpdateAvailableQualities(IReadOnlyList<string> qualityNames, int selectedIndex);

        event EventHandler PlayPauseClicked;

        event EventHandler<int> VolumeChanged;

        event EventHandler SettingsClicked;

        event EventHandler<int> QualityChanged;

        event EventHandler TwitchChatClicked;

        event EventHandler CloseClicked;
    }
}
