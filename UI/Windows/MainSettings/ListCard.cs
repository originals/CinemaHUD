using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using CinemaModule.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace CinemaHUD.UI.Windows.MainSettings
{
    public class ListCardButton
    {
        public string Text { get; set; }
        public int Width { get; set; } = 50;
        public Action OnClick { get; set; }
    }

    public class ListCard : Panel
    {
        #region Fields

        public const int DefaultCardWidth = 450;
        public const int DefaultCardHeight = 60;
        public const int DefaultTextPanelWidth = 400;
        private const int ImageSize = 44;
        private const int ImageLeft = 8;
        private const int ImageTop = 8;
        private const int TextPanelTop = 8;
        private const int TextPanelHeight = 48;
        private const int ButtonHeight = 28;
        private const int ButtonTop = 16;
        private const int ButtonSpacing = 5;
        private const int TitleIconSize = 18;
        private const int TitleIconSpacing = 4;

        public static readonly Color SelectedColor = new Color(60, 90, 60, 200);
        public static readonly Color DefaultColor = new Color(45, 45, 48, 180);

        private readonly Label _titleLabel;
        private readonly Label _subtitleLabel;
        private readonly Image _avatarImage;
        private readonly Image _titleIcon;
        private readonly FlowPanel _textPanel;
        private readonly Panel _titleRow;
        private readonly List<StandardButton> _buttons = new List<StandardButton>();
        private bool _isSelected;

        #endregion

        #region Properties

        public Label SubtitleLabel => _subtitleLabel;

        public new string Title
        {
            get => _titleLabel.Text;
            set => _titleLabel.Text = value;
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                
                _isSelected = value;
                BackgroundColor = _isSelected ? SelectedColor : DefaultColor;
            }
        }

        #endregion

        public ListCard(
            Container parent,
            string title,
            string subtitle,
            bool isSelected,
            int textPanelWidth = DefaultTextPanelWidth,
            IEnumerable<ListCardButton> buttons = null,
            AsyncTexture2D avatarTexture = null,
            AsyncTexture2D iconTexture = null)
        {
            Width = DefaultCardWidth;
            Height = DefaultCardHeight;
            BackgroundColor = isSelected ? SelectedColor : DefaultColor;
            BackgroundTexture = CinemaModule.CinemaModule.Instance.TextureService.GetCardBackground();
            Parent = parent;
            _isSelected = isSelected;

            int textPanelLeft = ImageLeft + ImageSize + 8;

            _avatarImage = new Image
            {
                Size = new Point(ImageSize, ImageSize),
                Left = ImageLeft,
                Top = ImageTop,
                Texture = avatarTexture ?? ContentService.Textures.TransparentPixel,
                Parent = this
            };

            _textPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Left = textPanelLeft,
                Top = TextPanelTop,
                Width = textPanelWidth,
                Height = TextPanelHeight,
                ControlPadding = new Vector2(0, 4),
                Parent = this
            };

            _titleRow = new Panel
            {
                HeightSizingMode = SizingMode.AutoSize,
                WidthSizingMode = SizingMode.AutoSize,
                Parent = _textPanel
            };

            int titleLeft = 0;
            if (iconTexture != null)
            {
                _titleIcon = new Image
                {
                    Size = new Point(TitleIconSize, TitleIconSize),
                    Left = 0,
                    Top = 1,
                    Texture = iconTexture,
                    Parent = _titleRow
                };
                titleLeft = TitleIconSize + TitleIconSpacing;
            }

            _titleLabel = new Label
            {
                Text = title,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                Left = titleLeft,
                Font = Blish_HUD.GameService.Content.DefaultFont16,
                Parent = _titleRow
            };

            _subtitleLabel = new Label
            {
                Text = subtitle,
                AutoSizeHeight = true,
                AutoSizeWidth = true,
                TextColor = Color.LightGray,
                Font = Blish_HUD.GameService.Content.DefaultFont14,
                Parent = _textPanel
            };

            UpdateTitleCentering();

            if (buttons != null)
            {
                AddButtons(buttons, textPanelWidth);
            }
        }


        protected override void OnClick(Blish_HUD.Input.MouseEventArgs e)
        {
            var mousePos = e.MousePosition;
            foreach (var button in _buttons)
            {
                if (button.AbsoluteBounds.Contains(mousePos))
                    return;
            }

            base.OnClick(e);
        }

        private void AddButtons(IEnumerable<ListCardButton> buttons, int textPanelWidth)
        {
            int rightPosition = Width - ButtonSpacing - 10;

            foreach (var buttonConfig in buttons)
            {
                rightPosition -= buttonConfig.Width;

                var button = new StandardButton
                {
                    Text = buttonConfig.Text,
                    Width = buttonConfig.Width,
                    Height = ButtonHeight,
                    Left = rightPosition,
                    Top = ButtonTop,
                    Parent = this
                };

                if (buttonConfig.OnClick != null)
                {
                    button.Click += (s, e) => buttonConfig.OnClick();
                }

                _buttons.Add(button);
                rightPosition -= ButtonSpacing;
            }
        }

        public void SetSubtitle(string text, Color? color = null)
        {
            _subtitleLabel.Text = text;
            if (color.HasValue)
            {
                _subtitleLabel.TextColor = color.Value;
            }
            UpdateTitleCentering();
        }

        public void SetAvatar(AsyncTexture2D texture)
        {
            if (texture != null)
            {
                _avatarImage.Texture = texture;
            }
        }

        private void UpdateTitleCentering()
        {
            bool hasSubtitle = !string.IsNullOrEmpty(_subtitleLabel.Text);
            _subtitleLabel.Visible = hasSubtitle;

            if (hasSubtitle)
            {
                _textPanel.Top = TextPanelTop;
            }
            else
            {
                _textPanel.Top = (DefaultCardHeight - 20) / 2;
            }
        }
    }
}
