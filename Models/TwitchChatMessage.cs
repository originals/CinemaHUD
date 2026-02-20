using Microsoft.Xna.Framework;
using System;

namespace CinemaModule.Models
{
    public class TwitchChatMessage
    {
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Message { get; set; }
        public Color UserColor { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsAction { get; set; }

        public TwitchChatMessage()
        {
            Timestamp = DateTime.UtcNow;
            UserColor = Color.White;
        }

        public TwitchChatMessage(string username, string displayName, string message, Color userColor, bool isAction = false)
            : this()
        {
            Username = username;
            DisplayName = displayName;
            Message = message;
            UserColor = userColor;
            IsAction = isAction;
        }
    }
}
