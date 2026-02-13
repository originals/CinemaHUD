using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using CinemaModule.Services;
using Microsoft.Xna.Framework;

namespace CinemaHUD.UI.Windows.SettingsSmall
{
    public abstract class SmallWindow : StandardWindow
    {
        private static readonly Rectangle DefaultWindowRegion = new Rectangle(25, 26, 435, 500);
        private static readonly Rectangle DefaultContentRegion = new Rectangle(40, 40, 415, 450);

        private static AsyncTexture2D BackgroundTexture => CinemaModule.CinemaModule.Instance.TextureService.GetSmallWindowBackground();

        protected SmallWindow(string title)
            : base(BackgroundTexture, DefaultWindowRegion, DefaultContentRegion)
        {
            Parent = GameService.Graphics.SpriteScreen;
            Title = title;
            Emblem = CinemaModule.CinemaModule.Instance.TextureService.GetEmblem();
            Location = new Point(
                (GameService.Graphics.SpriteScreen.Width - Width) / 2,
                (GameService.Graphics.SpriteScreen.Height - Height) / 2);
            SavesPosition = false;
            CanResize = false;

            BuildContent();
        }

        protected abstract void BuildContent();
    }
}
