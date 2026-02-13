using System;
using System.Collections.Generic;
using System.Linq;
using Blish_HUD;
using LibVLCSharp.Shared;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.Player
{
    public class VideoPlayerOptions
    {
        public bool EnableHardwareAcceleration { get; set; } = true;
        public int NetworkCachingMs { get; set; } = 1000;
        public int MaxWidth { get; set; } = 1920;
        public int MaxHeight { get; set; } = 1080;
        public int PreferredResolution { get; set; } = 1080;
        public string[] AdditionalLibVlcOptions { get; set; } = Array.Empty<string>();
        public static VideoPlayerOptions Default => new VideoPlayerOptions();
    }

    public class VideoPlayer : IDisposable
    {
        #region Members

        private static readonly Logger Logger = Logger.GetLogger<VideoPlayer>();

        private readonly GraphicsDevice _graphicsDevice;
        private readonly LibVLC _libVLC;
        private readonly MediaPlayer _mediaPlayer;
        private readonly VideoBuffer _buffer;
        private readonly VideoFormatHandler _formatHandler;
        private readonly VideoCallbackHandler _callbackHandler;

        private Texture2D _videoTexture;
        private bool _isDisposed;
        private string _currentUrl;
        private List<VideoQuality> _availableQualities = new List<VideoQuality>();
        private int _selectedQualityIndex = -1;
        private volatile bool _qualitiesNeedRefresh;

        #endregion

        #region Events

        public event EventHandler FrameReady;

        public event EventHandler<PlaybackStateEventArgs> PlaybackStateChanged;

        public event EventHandler QualitiesChanged;

        #endregion

        #region Properties

        public Texture2D VideoTexture => _videoTexture;

        public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;

        public bool IsPaused => _mediaPlayer?.State == VLCState.Paused;

        public int Volume
        {
            get => _mediaPlayer?.Volume ?? 0;
            set
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Volume = Math.Max(0, Math.Min(100, value));
                }
            }
        }

        public float Position
        {
            get => _mediaPlayer?.Position ?? 0f;
            set
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Position = Math.Max(0f, Math.Min(1f, value));
                }
            }
        }

        public int VideoWidth => (int)_formatHandler.Width;

        public int VideoHeight => (int)_formatHandler.Height;

        public string CurrentUrl => _currentUrl;

        public IReadOnlyList<VideoQuality> AvailableQualities => _availableQualities;

        public int SelectedQualityIndex => _selectedQualityIndex;

        public VideoQuality SelectedQuality => 
            _selectedQualityIndex >= 0 && _selectedQualityIndex < _availableQualities.Count 
                ? _availableQualities[_selectedQualityIndex] 
                : null;

        #endregion

        public VideoPlayer(GraphicsDevice device, VideoPlayerOptions options)
        {
            _graphicsDevice = device;

            var libVlcOptions = BuildLibVlcOptions(options);
            _libVLC = new LibVLC(libVlcOptions);
            _mediaPlayer = new MediaPlayer(_libVLC);

            _buffer = new VideoBuffer();
            _formatHandler = new VideoFormatHandler(_buffer);
            _callbackHandler = new VideoCallbackHandler(_buffer);

            SetupCallbacks();
            SetupEventHandlers();
        }

        #region Initialization

        private string[] BuildLibVlcOptions(VideoPlayerOptions options)
        {
            var optionsList = new System.Collections.Generic.List<string>
            {
                "--no-osd",
                $"--network-caching={options.NetworkCachingMs}",
                "--adaptive-logic=highest",
                $"--adaptive-maxwidth={options.MaxWidth}",
                $"--adaptive-maxheight={options.MaxHeight}",
                $"--preferred-resolution={options.PreferredResolution}"
            };

            if (options.EnableHardwareAcceleration)
            {
                optionsList.Add("--avcodec-hw=any");
            }

            if (options.AdditionalLibVlcOptions != null)
            {
                optionsList.AddRange(options.AdditionalLibVlcOptions);
            }

            return optionsList.ToArray();
        }

        private void SetupCallbacks()
        {
            _mediaPlayer.SetVideoFormatCallbacks(
                _formatHandler.HandleFormatCallback,
                null);

            _mediaPlayer.SetVideoCallbacks(
                _callbackHandler.LockCallback,
                null,
                _callbackHandler.DisplayCallback);
        }

        private void SetupEventHandlers()
        {
            _mediaPlayer.Playing += (s, e) => 
            {
                OnPlaybackStateChanged(PlaybackState.Playing);
                _qualitiesNeedRefresh = true;
            };
            _mediaPlayer.Paused += (s, e) => OnPlaybackStateChanged(PlaybackState.Paused);
            _mediaPlayer.Stopped += (s, e) => OnPlaybackStateChanged(PlaybackState.Stopped);
            _mediaPlayer.EndReached += (s, e) => OnPlaybackStateChanged(PlaybackState.Ended);
            _mediaPlayer.EncounteredError += (s, e) => OnPlaybackStateChanged(PlaybackState.Error);
            _mediaPlayer.ESAdded += (s, e) => 
            {
                if (e.Type == TrackType.Video)
                {
                    _qualitiesNeedRefresh = true;
                }
            };
        }

        #endregion

        #region Playback Control

        public void Play(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                OnPlaybackStateChanged(PlaybackState.Error);
                return;
            }

            Stop();

            _currentUrl = url;
            using (var media = new Media(_libVLC, uri))
            {
                _mediaPlayer.Play(media);
            }
        }

        public void Pause()
        {
            if (_mediaPlayer.CanPause)
            {
                _mediaPlayer.Pause();
            }
        }

        public void Resume()
        {
            if (IsPaused)
            {
                _mediaPlayer.Play();
            }
        }

        public void TogglePause()
        {
            if (IsPlaying)
            {
                Pause();
            }
            else if (IsPaused)
            {
                Resume();
            }
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
            _formatHandler.Reset();
            _videoTexture?.Dispose();
            _videoTexture = null;
            _currentUrl = null;
            _availableQualities.Clear();
            _selectedQualityIndex = -1;
        }

        #endregion

        #region Quality Management

        public void RefreshAvailableQualities()
        {
            try
            {
                var media = _mediaPlayer.Media;
                if (media == null)
                {
                    _availableQualities.Clear();
                    _selectedQualityIndex = -1;
                    return;
                }

                var tracks = media.Tracks;
                if (tracks == null || tracks.Length == 0)
                {
                    return;
                }

                var newQualities = new List<VideoQuality>();
                int currentTrackId = _mediaPlayer.VideoTrack;

                foreach (var track in tracks)
                {
                    if (track.TrackType != TrackType.Video)
                        continue;

                    var quality = new VideoQuality
                    {
                        TrackId = track.Id,
                        Width = (int)track.Data.Video.Width,
                        Height = (int)track.Data.Video.Height,
                        Name = BuildQualityName(track),
                        IsSelected = track.Id == currentTrackId
                    };

                    newQualities.Add(quality);
                }

                // Sort by resolution (highest first)
                newQualities = newQualities.OrderByDescending(q => q.Height).ThenByDescending(q => q.Width).ToList();

                // Remove duplicate quality names (keep first occurrence which has highest bitrate)
                var seenNames = new HashSet<string>();
                newQualities = newQualities.Where(q =>
                {
                    if (seenNames.Contains(q.Name))
                        return false;
                    seenNames.Add(q.Name);
                    return true;
                }).ToList();

                // Recalculate selected index after sort
                int selectedIdx = newQualities.FindIndex(q => q.IsSelected);

                bool qualitiesChanged = !AreQualitiesEqual(_availableQualities, newQualities);
                
                _availableQualities = newQualities;
                _selectedQualityIndex = selectedIdx;

                if (qualitiesChanged)
                {
                    QualitiesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Failed to refresh available qualities - will retry");
            }
        }


        private string BuildQualityName(MediaTrack track)
        {
            return VideoQuality.GetQualityName(
                (int)track.Data.Video.Width, 
                (int)track.Data.Video.Height);
        }

        private bool AreQualitiesEqual(List<VideoQuality> a, List<VideoQuality> b)
        {
            if (a.Count != b.Count) return false;
            
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].TrackId != b[i].TrackId || a[i].Height != b[i].Height)
                {
                    return false;
                }
            }
            
            return true;
        }

        public void SetQuality(int index)
        {
            if (index < 0 || index >= _availableQualities.Count)
            {
                return;
            }

            var quality = _availableQualities[index];
            SetQualityByTrackId(quality.TrackId);
        }

        public void SetQualityByTrackId(int trackId)
        {
            try
            {
                _mediaPlayer.SetVideoTrack(trackId);
                
                // Update selection state
                for (int i = 0; i < _availableQualities.Count; i++)
                {
                    _availableQualities[i].IsSelected = _availableQualities[i].TrackId == trackId;
                    if (_availableQualities[i].IsSelected)
                    {
                        _selectedQualityIndex = i;
                    }
                }
                
                QualitiesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Failed to set quality to track ID {trackId}");
            }
        }

        #endregion

        #region Frame Update

        public void Update()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_qualitiesNeedRefresh)
            {
                _qualitiesNeedRefresh = false;
                RefreshAvailableQualities();
            }

            if (_formatHandler.IsInitialized)
            {
                bool needsNewTexture = _videoTexture == null;

                if (_videoTexture != null)
                {
                    bool sizeChanged = _videoTexture.Width != (int)_formatHandler.Width ||
                                       _videoTexture.Height != (int)_formatHandler.Height;

                    if (sizeChanged)
                    {
                        _videoTexture.Dispose();
                        _videoTexture = null;
                        needsNewTexture = true;
                    }
                }

                if (needsNewTexture)
                {
                    CreateTexture();
                }
            }

            if (_callbackHandler.IsFrameDirty && _videoTexture != null)
            {
                _buffer.CopyToTextureWithPitch(_videoTexture, (int)_formatHandler.Width, (int)_formatHandler.Height, _formatHandler.Pitch);
                _callbackHandler.ClearFrameDirty();
                FrameReady?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CreateTexture()
        {
            int width = (int)_formatHandler.Width;
            int height = (int)_formatHandler.Height;

            if (width > 0 && height > 0)
            {
                _videoTexture = new Texture2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    SurfaceFormat.Color);
            }
        }

        private void OnPlaybackStateChanged(PlaybackState state)
        {
            PlaybackStateChanged?.Invoke(this, new PlaybackStateEventArgs(state));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            _buffer?.Dispose();
            _videoTexture?.Dispose();
        }

        #endregion
    }

    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused,
        Ended,
        Error
    }

    public class PlaybackStateEventArgs : EventArgs
    {
        public PlaybackState State { get; }

        public PlaybackStateEventArgs(PlaybackState state)
        {
            State = state;
        }
    }
}
