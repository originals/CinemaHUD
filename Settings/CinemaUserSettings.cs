using System;
using System.IO;
using Blish_HUD;
using CinemaModule.Models;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace CinemaModule.Settings
{
    public class CinemaUserSettings
    {
        #region Members

        private static readonly Logger Logger = Logger.GetLogger<CinemaUserSettings>();
        private const string SettingsFileName = "cinema_settings.json";
        private const int MinVolume = 0;
        private const int MaxVolume = 100;
        private const float MinScreenWidth = 4f;
        private const float MaxScreenWidth = 50f;
        private const float ScreenWidthTolerance = 0.001f;
        private const int MinWindowEdgeSpacing = 50;
        private const int DefaultWindowX = 100;
        private const int DefaultWindowY = 50;

        private readonly string _settingsFilePath;
        private CinemaUserSettingsData _data;

        #endregion

        #region Events

        public event EventHandler<string> StreamUrlChanged;
        public event EventHandler<CinemaDisplayMode> DisplayModeChanged;
        public event EventHandler<WorldPosition3D> WorldPositionChanged;
        public event EventHandler<float> WorldScreenWidthChanged;
        public event EventHandler<int> VolumeChanged;
        public event EventHandler<StreamSourceType> CurrentStreamSourceTypeChanged;
        public event EventHandler SavedLocationsChanged;
        public event EventHandler SavedStreamsChanged;

        #endregion

        #region Properties

        public string StreamUrl
        {
            get => _data.StreamUrl;
            set => SetPropertyWithEvent(_data.StreamUrl, value, v => _data.StreamUrl = v, StreamUrlChanged);
        }

        public string CurrentTwitchChannel
        {
            get => _data.CurrentTwitchChannel ?? "";
            set
            {
                var normalizedValue = value ?? "";
                if (_data.CurrentTwitchChannel == normalizedValue) return;
                _data.CurrentTwitchChannel = normalizedValue;
                Save();
            }
        }

        public StreamSourceType CurrentStreamSourceType
        {
            get => _data.CurrentStreamSourceType;
            set => SetPropertyWithEvent(_data.CurrentStreamSourceType, value, v => _data.CurrentStreamSourceType = v, CurrentStreamSourceTypeChanged);
        }

        public int Volume
        {
            get => _data.Volume;
            set => SetPropertyWithEvent(_data.Volume, Clamp(value, MinVolume, MaxVolume), v => _data.Volume = v, VolumeChanged);
        }

        public CinemaDisplayMode DisplayMode
        {
            get => _data.DisplayMode;
            set => SetPropertyWithEvent(_data.DisplayMode, value, v => _data.DisplayMode = v, DisplayModeChanged);
        }

        public string SelectedPresetLocationId
        {
            get => _data.SelectedPresetLocationId ?? "";
            set { _data.SelectedPresetLocationId = value; Save(); }
        }

        public string SelectedSavedLocationId
        {
            get => _data.SelectedSavedLocationId;
            set { _data.SelectedSavedLocationId = value; Save(); }
        }

        public WorldPosition3D WorldPosition
        {
            get => _data.WorldPosition;
            set { _data.WorldPosition = value; Save(); RaiseEvent(WorldPositionChanged, value); }
        }

        public float WorldScreenWidth
        {
            get => _data.WorldScreenWidth;
            set
            {
                float clampedValue = Clamp(value, MinScreenWidth, MaxScreenWidth);
                if (Math.Abs(_data.WorldScreenWidth - clampedValue) > ScreenWidthTolerance)
                {
                    _data.WorldScreenWidth = clampedValue;
                    Save();
                    RaiseEvent(WorldScreenWidthChanged, clampedValue);
                }
            }
        }

        public Point WindowPosition
        {
            get => _data.WindowPosition;
            set { _data.WindowPosition = value; Save(); }
        }

        public Point WindowSize
        {
            get => _data.WindowSize;
            set { _data.WindowSize = value; Save(); }
        }

        public SavedLocationCollection SavedLocations => _data.SavedLocations;

        public SavedStreamCollection SavedStreams => _data.SavedStreams;

        public string SelectedSavedStreamId
        {
            get => _data.SelectedSavedStreamId;
            set { _data.SelectedSavedStreamId = value; Save(); }
        }

        #endregion

        public CinemaUserSettings(string settingsDirectory)
        {
            _settingsFilePath = Path.Combine(settingsDirectory, SettingsFileName);
            Load();
        }

        #region Public Methods
        public SavedLocation AddSavedLocation(string name, WorldPosition3D position, float screenWidth)
        {
            var location = new SavedLocation(name, position, screenWidth);
            SavedLocations.Locations.Add(location);
            Save();
            RaiseEvent(SavedLocationsChanged);
            return location;
        }

        public void UpdateSavedLocation(SavedLocation location)
        {
            var index = SavedLocations.Locations.FindIndex(l => l.Id == location.Id);
            if (index >= 0)
            {
                SavedLocations.Locations[index] = location;
                Save();
                RaiseEvent(SavedLocationsChanged);
            }
        }

        public bool DeleteSavedLocation(string id)
        {
            var removed = SavedLocations.Locations.RemoveAll(l => l.Id == id) > 0;
            if (removed)
            {
                if (SelectedSavedLocationId == id)
                {
                    _data.SelectedSavedLocationId = "";
                }
                Save();
                RaiseEvent(SavedLocationsChanged);
                return true;
            }
            return false;
        }

        public SavedStream AddSavedStream(string name, StreamSourceType sourceType, string value)
        {
            var stream = new SavedStream(name, sourceType, value);
            SavedStreams.Streams.Add(stream);
            Save();
            RaiseEvent(SavedStreamsChanged);
            return stream;
        }

        public void UpdateSavedStream(SavedStream stream)
        {
            var index = SavedStreams.Streams.FindIndex(s => s.Id == stream.Id);
            if (index >= 0)
            {
                SavedStreams.Streams[index] = stream;
                Save();
                RaiseEvent(SavedStreamsChanged);
            }
        }

        public bool DeleteSavedStream(string id)
        {
            var removed = SavedStreams.Streams.RemoveAll(s => s.Id == id) > 0;
            if (removed)
            {
                if (SelectedSavedStreamId == id)
                    _data.SelectedSavedStreamId = "";
                Save();
                RaiseEvent(SavedStreamsChanged);
            }
            return removed;
        }

        public string GetCurrentTwitchChannel()
        {
            return string.IsNullOrEmpty(CurrentTwitchChannel) ? null : CurrentTwitchChannel;
        }

        #endregion

        #region Private Methods

        private bool SetPropertyWithEvent<T>(T currentValue, T newValue, Action<T> setter, EventHandler<T> eventHandler)
        {
            if (Equals(currentValue, newValue))
                return false;
            setter(newValue);
            Save();
            RaiseEvent(eventHandler, newValue);
            return true;
        }

        private void RaiseEvent<T>(EventHandler<T> eventHandler, T value) => eventHandler?.Invoke(this, value);

        private void RaiseEvent(EventHandler eventHandler) => eventHandler?.Invoke(this, EventArgs.Empty);

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));

        private void Load()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    _data = JsonConvert.DeserializeObject<CinemaUserSettingsData>(json) 
                        ?? new CinemaUserSettingsData();
                    
                    // Ensure collections are initialized (handles legacy data without these fields)
                    _data.SavedLocations = _data.SavedLocations ?? new SavedLocationCollection();
                    _data.SavedStreams = _data.SavedStreams ?? new SavedStreamCollection();
                    _data.WorldPosition = _data.WorldPosition ?? new WorldPosition3D(0, 0, 0, 0);
                                        
                    // Ensure CurrentTwitchChannel is populated from selected stream if empty
                    if (string.IsNullOrEmpty(_data.CurrentTwitchChannel) && !string.IsNullOrEmpty(_data.SelectedSavedStreamId))
                    {
                        var selectedStream = _data.SavedStreams?.Streams.Find(s => s.Id == _data.SelectedSavedStreamId);
                        if (selectedStream != null && selectedStream.SourceType == StreamSourceType.TwitchChannel)
                        {
                            _data.CurrentTwitchChannel = selectedStream.Value;
                            Save();
                            Logger.Debug($"Populated CurrentTwitchChannel from selected stream: {selectedStream.Value}");
                        }
                    }
                    
                    Logger.Info("Loaded CinemaHUD settings from JSON");
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to load CinemaHUD settings: {ex.Message}");
                    _data = new CinemaUserSettingsData();
                }
            }
            else
            {
                _data = new CinemaUserSettingsData();
            }
        }

        private void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(_settingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(_data, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save CinemaHUD settings: {ex.Message}");
            }
        }

        #endregion
    }


    /// Data model for JSON serialization of CinemaHUD settings.
    internal class CinemaUserSettingsData
    {
        public string StreamUrl { get; set; } = "";
        public string CurrentTwitchChannel { get; set; } = "";
        public StreamSourceType CurrentStreamSourceType { get; set; } = StreamSourceType.Url;
        public int Volume { get; set; } = 50;
        public CinemaDisplayMode DisplayMode { get; set; } = CinemaDisplayMode.OnScreen;
        public string SelectedPresetLocationId { get; set; } = "";
        public string SelectedSavedLocationId { get; set; } = "";
        public string SelectedSavedStreamId { get; set; } = "";
        public WorldPosition3D WorldPosition { get; set; } = new WorldPosition3D(0, 0, 0, 0);
        public float WorldScreenWidth { get; set; } = 10f;
        public Point WindowPosition { get; set; } = new Point(100, 50);
        public Point WindowSize { get; set; } = new Point(640, 360);
        public SavedLocationCollection SavedLocations { get; set; } = new SavedLocationCollection();
        public SavedStreamCollection SavedStreams { get; set; } = new SavedStreamCollection();
    }
}
