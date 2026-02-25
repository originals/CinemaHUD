using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Controls.Effects;
using Glide;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace CinemaHUD.UI.Windows.MainSettings
{
    public class ListCardButton
    {
        public string Text { get; set; }
        public int Width { get; set; } = 50;
        public Action OnClick { get; set; }
        public AsyncTexture2D Icon { get; set; }
        public string Tooltip { get; set; }
    }

    public class ListCard : Panel
    {
        private static readonly Logger Logger = Logger.GetLogger<ListCard>();
        private static readonly AsyncTexture2D _textureMenuItemFade = AsyncTexture2D.FromAssetId(156044);

        #region Fields

        public const int DefaultCardWidth = 450;
        public const int DefaultCardHeight = 60;
        public const int DefaultTextPanelWidth = 400;
        private const int ImageMaxHeight = 44;
        private const int ImageDefaultWidth = 44;
        private const int ImageMaxWidth = 80;
        private const int ImageLeft = 8;
        private const int ImageTop = 8;
        private const int TextPanelTop = 8;
        private const int TextPanelHeight = 48;
        private const int ButtonHeight = 28;
        private const int ButtonTop = 16;
        private const int ButtonSpacing = 5;
        private const int TitleIconSize = 18;
        private const int TitleIconSpacing = 4;

        private readonly Label _titleLabel;
        private readonly Label _subtitleLabel;
        private readonly Image _avatarImage;
        private readonly Image _titleIcon;
        private readonly FlowPanel _textPanel;
        private readonly Panel _titleRow;
        private readonly List<Control> _buttons = new List<Control>();
        private readonly object _buttonsLock = new object();
        private readonly ScrollingHighlightEffect _scrollEffect;
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
                UpdateSelectedState();
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
            WidthSizingMode = SizingMode.Fill;
            Height = DefaultCardHeight;
            Parent = parent;
            _isSelected = isSelected;

            _scrollEffect = new ScrollingHighlightEffect(this)
            {
                Size = new Vector2(Width, Height)
            };
            EffectBehind = _scrollEffect;
            UpdateSelectedState();

            int textPanelLeft = ImageLeft + ImageDefaultWidth + 8;

            _avatarImage = new Image
            {
                Size = new Point(ImageDefaultWidth, ImageMaxHeight),
                Left = ImageLeft,
                Top = ImageTop,
                Texture = ContentService.Textures.TransparentPixel,
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
                Height = 20,
                WidthSizingMode = SizingMode.Fill,
                Parent = _textPanel
            };

            int titleLeft = 0;
            int titleMaxWidth = textPanelWidth;
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
                titleMaxWidth -= titleLeft;
            }

            _titleLabel = new Label
            {
                Text = title,
                Height = 20,
                Width = titleMaxWidth,
                Left = titleLeft,
                Font = Blish_HUD.GameService.Content.DefaultFont16,
                Parent = _titleRow
            };

            _subtitleLabel = new Label
            {
                Text = subtitle,
                Height = 18,
                Width = textPanelWidth,
                TextColor = Color.LightGray,
                Font = Blish_HUD.GameService.Content.DefaultFont14,
                Parent = _textPanel
            };

            UpdateTitleCentering();

            if (buttons != null)
            {
                CreateButtonControls(buttons);
            }

            if (avatarTexture != null)
            {
                SetAvatar(avatarTexture);
            }
        }

        #region Rendering

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            base.PaintBeforeChildren(spriteBatch, bounds);

            if (_textureMenuItemFade.HasTexture && ShouldDrawDarkStripe())
            {
                spriteBatch.DrawOnCtrl(this, _textureMenuItemFade, bounds, Color.Black * 0.4f);
            }
        }

        private bool ShouldDrawDarkStripe()
        {
            if (Parent == null) return false;

            int index = 0;
            foreach (var child in Parent.Children)
            {
                if (child == this) break;
                if (child is ListCard) index++;
            }

            return index % 2 == 0;
        }

        #endregion

        #region Hover Effects

        protected override void OnMouseEntered(Blish_HUD.Input.MouseEventArgs e)
        {
            base.OnMouseEntered(e);
            _scrollEffect.Enable();
        }

        protected override void OnMouseLeft(Blish_HUD.Input.MouseEventArgs e)
        {
            base.OnMouseLeft(e);
            if (!_isSelected)
            {
                _scrollEffect.Disable();
            }
        }

        private void UpdateSelectedState()
        {
            _scrollEffect.ForceActive = _isSelected;
        }

        #endregion


        protected override void OnClick(Blish_HUD.Input.MouseEventArgs e)
        {
            var mousePos = e.MousePosition;
            Control[] buttonsCopy;
            lock (_buttonsLock)
            {
                buttonsCopy = _buttons.ToArray();
            }

            foreach (var button in buttonsCopy)
            {
                if (button.AbsoluteBounds.Contains(mousePos))
                    return;
            }

            base.OnClick(e);
        }

        private void CreateButtonControls(IEnumerable<ListCardButton> buttonConfigs)
        {
            int rightPosition = Width - ButtonSpacing - 15;

            foreach (var buttonConfig in buttonConfigs)
            {
                rightPosition -= buttonConfig.Width;

                bool isIconOnly = buttonConfig.Icon != null && string.IsNullOrEmpty(buttonConfig.Text);

                if (isIconOnly)
                {
                    var glowButton = new GlowButton
                    {
                        Icon = buttonConfig.Icon,
                        Size = new Point(buttonConfig.Width, ButtonHeight),
                        Left = rightPosition,
                        Top = ButtonTop,
                        BasicTooltipText = buttonConfig.Tooltip,
                        Parent = this
                    };

                    if (buttonConfig.OnClick != null)
                    {
                        glowButton.Click += (s, e) => buttonConfig.OnClick();
                    }

                    lock (_buttonsLock)
                    {
                        _buttons.Add(glowButton);
                    }
                }
                else
                {
                    var button = new StandardButton
                    {
                        Text = buttonConfig.Text ?? "",
                        Width = buttonConfig.Width,
                        Height = ButtonHeight,
                        Left = rightPosition,
                        Top = ButtonTop,
                        BasicTooltipText = buttonConfig.Tooltip,
                        Parent = this
                    };

                    if (buttonConfig.Icon != null)
                    {
                        button.Icon = buttonConfig.Icon;
                    }

                    if (buttonConfig.OnClick != null)
                    {
                        button.Click += (s, e) => buttonConfig.OnClick();
                    }

                    lock (_buttonsLock)
                    {
                        _buttons.Add(button);
                    }
                }

                rightPosition -= ButtonSpacing;
            }
        }

        public override void RecalculateLayout()
        {
            base.RecalculateLayout();
            if (_scrollEffect == null) return;
            _scrollEffect.Size = new Vector2(Width, Height);
            UpdateTextPanelWidth();
            RepositionButtons();
        }

        private void UpdateTextPanelWidth()
        {
            if (_textPanel == null || _titleLabel == null || _subtitleLabel == null) return;

            int totalButtonWidth = 0;
            lock (_buttonsLock)
            {
                foreach (var button in _buttons)
                {
                    totalButtonWidth += button.Width + ButtonSpacing;
                }
            }

            int availableWidth = Width - _textPanel.Left - totalButtonWidth - ButtonSpacing - 20;
            if (availableWidth > 0)
            {
                _textPanel.Width = availableWidth;
                _titleLabel.Width = availableWidth - (_titleLabel.Left > 0 ? _titleLabel.Left : 0);
                _subtitleLabel.Width = availableWidth;
                _textPanel.Invalidate();
            }
        }

        private void RepositionButtons()
        {
            Control[] buttonsCopy;
            lock (_buttonsLock)
            {
                if (_buttons.Count == 0) return;
                buttonsCopy = _buttons.ToArray();
            }

            int rightPosition = Width - ButtonSpacing - 15;
            foreach (var button in buttonsCopy)
            {
                rightPosition -= button.Width;
                button.Left = rightPosition;
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
            if (texture == null)
                return;

            _avatarImage.Texture = texture;

            if (texture.Texture != null)
            {
                UpdateAvatarSize(texture.Texture.Width, texture.Texture.Height);
            }
            else
            {
                texture.TextureSwapped += OnAvatarTextureSwapped;
            }
        }

        private void OnAvatarTextureSwapped(object sender, ValueChangedEventArgs<Texture2D> e)
        {
            if (sender is AsyncTexture2D asyncTexture)
            {
                asyncTexture.TextureSwapped -= OnAvatarTextureSwapped;
            }

            if (e.NewValue != null)
            {
                UpdateAvatarSize(e.NewValue.Width, e.NewValue.Height);
            }
        }

        private void UpdateAvatarSize(int textureWidth, int textureHeight)
        {
            if (textureWidth <= 0 || textureHeight <= 0)
            {
                Logger.Warn($"Invalid avatar dimensions: {textureWidth}x{textureHeight}");
                return;
            }

            float aspectRatio = (float)textureWidth / textureHeight;
            int newWidth = (int)(ImageMaxHeight * aspectRatio);

            newWidth = Math.Min(newWidth, ImageMaxWidth);
            newWidth = Math.Max(newWidth, ImageDefaultWidth);

            _avatarImage.Size = new Point(newWidth, ImageMaxHeight);
            _textPanel.Left = ImageLeft + newWidth + 8;
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
