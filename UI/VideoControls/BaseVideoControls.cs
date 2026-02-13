using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
using Glide;
using Microsoft.Xna.Framework;

namespace CinemaModule.UI.Controls
{
    public class BaseVideoControls
    {
        #region Constants

        public const int IconSize = 32;
        public const int TrackBarWidth = 100;
        public const int TrackBarHeight = 16;
        public const int ControlSpacing = 8;
        public const int QualityDropdownWidth = 140;
        public const float FadeDuration = 0.2f;

        #endregion

        #region Members

        public VideoControlsRenderer Renderer { get; }
        public TrackBar VolumeTrackBar { get; }
        public Dropdown QualityDropdown { get; }

        protected bool IsHoveringPlayPause;
        protected bool IsHoveringVolume;
        protected bool IsHoveringSettings;
        protected int LastVolume = 100;

        protected Tween FadeAnimation;

        #endregion

        #region Events

        public event EventHandler PlayPauseClicked;
        public event EventHandler<int> VolumeChanged;
        public event EventHandler SettingsClicked;
        public event EventHandler<int> QualityChanged;

        #endregion

        #region Properties

        public bool IsPaused { get; set; }

        private int _volume = 100;
        public int Volume
        {
            get => _volume;
            set
            {
                if (_volume != value)
                {
                    _volume = value;
                    OnVolumePropertyChanged(value);
                }
            }
        }

        private float _opacity;
        public virtual float Opacity
        {
            get => _opacity;
            set => _opacity = value;
        }

        #endregion

        public BaseVideoControls(Container parent, int trackBarWidth, int trackBarHeight, int dropdownWidth)
        {
            Renderer = new VideoControlsRenderer(CinemaModule.Instance.TextureService);

            VolumeTrackBar = CreateVolumeTrackBar(parent, trackBarWidth, trackBarHeight);
            VolumeTrackBar.ValueChanged += OnVolumeTrackBarChanged;

            QualityDropdown = CreateQualityDropdown(parent, dropdownWidth);
            QualityDropdown.ValueChanged += OnQualityDropdownChanged;
        }

        #region Private Methods

        private TrackBar CreateVolumeTrackBar(Container parent, int width, int height)
        {
            return new TrackBar
            {
                Parent = parent,
                MinValue = 0,
                MaxValue = 100,
                Value = 100,
                SmallStep = true,
                Size = new Point(width, height)
            };
        }

        private Dropdown CreateQualityDropdown(Container parent, int width)
        {
            return new Dropdown
            {
                Parent = parent,
                Size = new Point(width, 27),
                Visible = false
            };
        }

        protected virtual void OnVolumePropertyChanged(int newValue)
        {
            if (VolumeTrackBar != null && !VolumeTrackBar.Dragging)
            {
                VolumeTrackBar.Value = newValue;
            }
        }

        private void OnVolumeTrackBarChanged(object sender, ValueEventArgs<float> e)
        {
            int newVolume = (int)e.Value;
            if (newVolume != _volume)
            {
                _volume = newVolume;
                HandleVolumeTrackBarChange(newVolume);
            }
        }

        protected virtual void HandleVolumeTrackBarChange(int newVolume)
        {
            RaiseVolumeChanged(newVolume);
        }

        public void ToggleMuteAndNotify()
        {
            if (_volume > 0)
            {
                LastVolume = _volume;
                _volume = 0;
            }
            else
            {
                _volume = LastVolume > 0 ? LastVolume : 50;
            }

            if (VolumeTrackBar != null)
            {
                VolumeTrackBar.Value = _volume;
            }
            RaiseVolumeChanged(_volume);
        }

        private void OnQualityDropdownChanged(object sender, ValueChangedEventArgs e)
        {
            int selectedIndex = QualityDropdown.SelectedItem != null 
                ? QualityDropdown.Items.IndexOf(QualityDropdown.SelectedItem)
                : -1;
            
            if (selectedIndex >= 0)
            {
                RaiseQualityChanged(selectedIndex);
            }
        }

        public void UpdateAvailableQualities(IReadOnlyList<string> qualityNames, int selectedIndex)
        {
            if (QualityDropdown == null) return;

            QualityDropdown.Items.Clear();

            if (qualityNames == null || qualityNames.Count == 0)
            {
                QualityDropdown.Visible = false;
                return;
            }

            foreach (var name in qualityNames)
            {
                QualityDropdown.Items.Add(name);
            }

            if (selectedIndex >= 0 && selectedIndex < QualityDropdown.Items.Count)
            {
                QualityDropdown.SelectedItem = QualityDropdown.Items[selectedIndex];
            }
        }

        protected void StartFadeIn()
        {
            FadeAnimation?.Cancel();
            FadeAnimation = GameService.Animation.Tweener
                .Tween(this, new { Opacity = 1f }, FadeDuration)
                .Ease(Ease.QuadOut);
        }

        protected void StartFadeOut(Action onComplete = null)
        {
            FadeAnimation?.Cancel();
            var tween = GameService.Animation.Tweener
                .Tween(this, new { Opacity = 0f }, FadeDuration)
                .Ease(Ease.QuadIn);
            
            if (onComplete != null)
            {
                tween.OnComplete(onComplete);
            }
            
            FadeAnimation = tween;
        }

        #endregion

        #region Public Methods

        public void RaisePlayPauseClicked()
        {
            PlayPauseClicked?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseVolumeChanged(int volume)
        {
            VolumeChanged?.Invoke(this, volume);
        }

        public void RaiseSettingsClicked()
        {
            SettingsClicked?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseQualityChanged(int index)
        {
            QualityChanged?.Invoke(this, index);
        }

        public virtual void Dispose()
        {
            CleanupEventHandlers();
            VolumeTrackBar?.Dispose();
            QualityDropdown?.Dispose();
        }

        #endregion

        protected virtual void CleanupEventHandlers()
        {
            if (VolumeTrackBar != null)
            {
                VolumeTrackBar.ValueChanged -= OnVolumeTrackBarChanged;
            }

            if (QualityDropdown != null)
            {
                QualityDropdown.ValueChanged -= OnQualityDropdownChanged;
            }
        }
    }
}
