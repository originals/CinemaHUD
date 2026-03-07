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
        public const int SeekBarHeight = 16;
        public const int ControlSpacing = 8;
        public const int QualityDropdownWidth = 140;
        public const float FadeDuration = 0.2f;

        #endregion

        #region Members

        public VideoControlsRenderer Renderer { get; }
        public TrackBar VolumeTrackBar { get; }
        public TrackBar SeekBar { get; }
        public Dropdown QualityDropdown { get; }

        protected int LastVolume = 100;
        protected Tween FadeAnimation;

        private bool _wasSeekBarDragging;
        private float _currentPosition;
        private long _duration;
        private float _pendingSeekPosition = -1f;
        private const float SeekPositionTolerance = 0.02f;

        #endregion

        #region Events

        public event EventHandler PlayPauseClicked;
        public event EventHandler<int> VolumeChanged;
        public event EventHandler SettingsClicked;
        public event EventHandler<int> QualityChanged;
        public event EventHandler<float> SeekRequested;

        #endregion

        #region Properties

        public bool IsPaused { get; set; }

        private bool _isWatchPartyViewer;
        public bool IsWatchPartyViewer
        {
            get => _isWatchPartyViewer;
            set => _isWatchPartyViewer = value;
        }

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

        public bool IsSeekBarDragging => SeekBar?.Dragging ?? false;

        private bool IsSeekPending => _pendingSeekPosition >= 0f;

        private bool JustStoppedDragging => _wasSeekBarDragging && !IsSeekBarDragging;

        public float CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (SeekBar == null) return;

                if (IsSeekBarDragging) return;

                if (JustStoppedDragging) return;

                if (IsSeekPending)
                {
                    if (Math.Abs(value - _pendingSeekPosition) < SeekPositionTolerance)
                    {
                        _pendingSeekPosition = -1f;
                    }
                    else
                    {
                        return;
                    }
                }

                _currentPosition = value;
                SeekBar.Value = value * 100f;
            }
        }

        public long Duration
        {
            get => _duration;
            set => _duration = value;
        }

        #endregion

        public BaseVideoControls(Container parent, int trackBarWidth, int trackBarHeight, int dropdownWidth, bool createSeekBar = false)
        {
            Renderer = new VideoControlsRenderer(CinemaModule.Instance.TextureService);

            VolumeTrackBar = CreateVolumeTrackBar(parent, trackBarWidth, trackBarHeight);
            VolumeTrackBar.ValueChanged += OnVolumeTrackBarChanged;

            QualityDropdown = CreateQualityDropdown(parent, dropdownWidth);
            QualityDropdown.ValueChanged += OnQualityDropdownChanged;

            if (createSeekBar)
            {
                SeekBar = CreateSeekBar(parent);
                SeekBar.ValueChanged += OnSeekBarValueChanged;
            }
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

        private TrackBar CreateSeekBar(Container parent)
        {
            return new TrackBar
            {
                Parent = parent,
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                SmallStep = false,
                Size = new Point(200, SeekBarHeight),
                Visible = false
            };
        }

        private void OnSeekBarValueChanged(object sender, ValueEventArgs<float> e)
        {
            if (!SeekBar.Dragging) return;

            if (_isWatchPartyViewer)
            {
                SeekBar.Value = _currentPosition * 100f;
                return;
            }

            _currentPosition = e.Value / 100f;
        }

        public void UpdateSeekBarDragState()
        {
            if (SeekBar == null) return;

            if (_wasSeekBarDragging && !SeekBar.Dragging)
            {
                float seekPosition = SeekBar.Value / 100f;
                _pendingSeekPosition = seekPosition;
                _currentPosition = seekPosition;
                SeekRequested?.Invoke(this, seekPosition);
            }

            _wasSeekBarDragging = SeekBar.Dragging;
        }

        public string FormatTimeDisplay()
        {
            var currentTime = TimeSpan.FromMilliseconds(_duration * _currentPosition);
            var totalTime = TimeSpan.FromMilliseconds(_duration);
            return $"{FormatTime(currentTime)} / {FormatTime(totalTime)}";
        }

        private static string FormatTime(TimeSpan ts)
        {
            return ts.Hours > 0
                ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        protected virtual void OnVolumePropertyChanged(int newValue)
        {
            if (!VolumeTrackBar.Dragging)
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

            VolumeTrackBar.Value = _volume;
            RaiseVolumeChanged(_volume);
        }

        private bool _suppressQualityEvent;

        private void OnQualityDropdownChanged(object sender, ValueChangedEventArgs e)
        {
            if (_suppressQualityEvent) return;

            int selectedIndex = QualityDropdown.Items.IndexOf(QualityDropdown.SelectedItem);
            if (selectedIndex >= 0)
            {
                RaiseQualityChanged(selectedIndex);
            }
        }

        public void UpdateAvailableQualities(IReadOnlyList<string> qualityNames, int selectedIndex)
        {
            Logger.GetLogger<BaseVideoControls>().Debug($"UpdateAvailableQualities: {qualityNames?.Count ?? 0} qualities, selectedIndex={selectedIndex}");
            _suppressQualityEvent = true;

            QualityDropdown.Items.Clear();

            if (qualityNames == null || qualityNames.Count == 0)
            {
                QualityDropdown.Visible = false;
                _suppressQualityEvent = false;
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

            _suppressQualityEvent = false;
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
            VolumeTrackBar.Dispose();
            SeekBar?.Dispose();
            QualityDropdown.Dispose();
        }

        #endregion

        protected virtual void CleanupEventHandlers()
        {
            VolumeTrackBar.ValueChanged -= OnVolumeTrackBarChanged;
            QualityDropdown.ValueChanged -= OnQualityDropdownChanged;

            if (SeekBar != null)
            {
                SeekBar.ValueChanged -= OnSeekBarValueChanged;
            }
        }
    }
}
