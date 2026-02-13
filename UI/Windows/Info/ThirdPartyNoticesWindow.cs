using Blish_HUD;
using Blish_HUD.Controls;
using CinemaModule.Services;
using Microsoft.Xna.Framework;

namespace CinemaModule.UI.Windows.Info
{
    public class ThirdPartyNoticesWindow : StandardWindow
    {
        private Panel _scrollPanel;
        private MultilineTextBox _noticesTextBox;

        public ThirdPartyNoticesWindow()
                         : base(CinemaModule.Instance.TextureService.GetSmallWindowBackground(), new Rectangle(25, 26, 435, 480),
                new Rectangle(40, 10, 395, 440))
        {
            Parent = GameService.Graphics.SpriteScreen;
            Title = "Third-Party Notices";
            Location = new Point(
                (GameService.Graphics.SpriteScreen.Width - Width) / 2,
                (GameService.Graphics.SpriteScreen.Height - Height) / 2);
            SavesPosition = true;

            BuildContent();
        }

        private void BuildContent()
        {
            _scrollPanel = new Panel
            {
                Parent = this,
                Location = new Point(0, 0),
                Size = new Point(ContentRegion.Width, ContentRegion.Height),
                CanScroll = true
            };

            var noticesText = LoadThirdPartyNotices();
            var lines = noticesText.Split('\n');

            _noticesTextBox = new MultilineTextBox
            {
                Parent = _scrollPanel,
                Location = new Point(5, 5),
                Width = _scrollPanel.ContentRegion.Width,
                Height = lines.Length * 14 + 50,
                Text = noticesText,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size11, ContentService.FontStyle.Regular)
            };
        }

        private string LoadThirdPartyNotices()
        {
            try
            {
                var stream = CinemaModule.Instance.ContentsManager.GetFileStream("THIRD-PARTY-NOTICES.txt");
                if (stream == null)
                {
                    Logger.GetLogger<ThirdPartyNoticesWindow>().Warn("THIRD-PARTY-NOTICES.txt stream is null");
                    return "Third-party-notices file not found.";
                }

                using (stream)
                using (var reader = new System.IO.StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (System.Exception ex)
            {
                Logger.GetLogger<ThirdPartyNoticesWindow>().Warn($"Failed to load third-party-notices: {ex.Message}");
                return $"Third-party-notices could not be loaded.\nError: {ex.Message}";
            }
        }

        protected override void DisposeControl()
        {
            _noticesTextBox?.Dispose();
            _scrollPanel?.Dispose();
            base.DisposeControl();
        }
    }
}
