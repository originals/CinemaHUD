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
        public event EventHandler<StreamPresetData> CurrentStreamPresetChanged;
        public event EventHandler SavedLocationsChanged;
        public event EventHandler SavedStreamsChanged;

        #endregion

        #region Members (Runtime Only)

        private StreamPresetData _currentStreamPreset;

        #endregion

        #region Properties

        public StreamPresetData CurrentStreamPreset
        {
            get => _currentStreamPreset;
            set
            {
                if (_currentStreamPreset == value) return;
                _currentStreamPreset = value;
                CurrentStreamPresetChanged?.Invoke(this, value);
            }
        }

        public string StreamUrl
        {
            get => _data.StreamUrl;
            set => SetPropertyWithEvent(_data.StreamUrl, value, v => _data.StreamUrl = v, StreamUrlChanged);
        }

        public string CurrentTwitchChannel
        {
            get => _data.CurrentTwitchChannel;
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
            get => _data.SelectedPresetLocationId;
            set => SetProperty(_data.SelectedPresetLocationId, value, v => _data.SelectedPresetLocationId = v);
        }

        public string SelectedSavedLocationId
        {
            get => _data.SelectedSavedLocationId;
            set => SetProperty(_data.SelectedSavedLocationId, value, v => _data.SelectedSavedLocationId = v);
        }

        public WorldPosition3D WorldPosition
        {
            get => _data.WorldPosition;
            set => SetPropertyWithEvent(_data.WorldPosition, value, v => _data.WorldPosition = v, WorldPositionChanged);
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
            set => SetProperty(_data.WindowPosition, value, v => _data.WindowPosition = v);
        }

        public Point WindowSize
        {
            get => _data.WindowSize;
            set => SetProperty(_data.WindowSize, value, v => _data.WindowSize = v);
        }

        public bool WindowLocked
        {
            get => _data.WindowLocked;
            set => SetProperty(_data.WindowLocked, value, v => _data.WindowLocked = v);
        }

        public SavedLocationCollection SavedLocations => _data.SavedLocations;

        public SavedStreamCollection SavedStreams => _data.SavedStreams;

        public string SelectedSavedStreamId
        {
            get => _data.SelectedSavedStreamId;
            set => SetProperty(_data.SelectedSavedStreamId, value, v => _data.SelectedSavedStreamId = v);
        }

        public string SelectedUrlChannelId
        {
            get => _data.SelectedUrlChannelId;
            set => SetProperty(_data.SelectedUrlChannelId, value, v => _data.SelectedUrlChannelId = v);
        }

        public string TwitchAccessToken
        {
            get => _data.TwitchAccessToken;
            set => SetProperty(_data.TwitchAccessToken, value, v => _data.TwitchAccessToken = v);
        }

        public string TwitchRefreshToken
        {
            get => _data.TwitchRefreshToken;
            set => SetProperty(_data.TwitchRefreshToken, value, v => _data.TwitchRefreshToken = v);
        }

        public string LastSelectedSourceCategory
        {
            get => _data.LastSelectedSourceCategory;
            set => SetProperty(_data.LastSelectedSourceCategory, value, v => _data.LastSelectedSourceCategory = v);
        }

        public int SelectedSettingsTab
        {
            get => _data.SelectedSettingsTab;
            set => SetProperty(_data.SelectedSettingsTab, value, v => _data.SelectedSettingsTab = v);
        }

        public bool TwitchChatWindowLocked
        {
            get => _data.TwitchChatWindowLocked;
            set => SetProperty(_data.TwitchChatWindowLocked, value, v => _data.TwitchChatWindowLocked = v);
        }

        public bool TwitchChatWindowOpen
        {
            get => _data.TwitchChatWindowOpen;
            set => SetProperty(_data.TwitchChatWindowOpen, value, v => _data.TwitchChatWindowOpen = v);
        }

        public Point TwitchChatWindowSize
        {
            get => _data.TwitchChatWindowSize;
            set => SetProperty(_data.TwitchChatWindowSize, value, v => _data.TwitchChatWindowSize = v);
        }

        public string TwitchChatWindowChannel
        {
            get => _data.TwitchChatWindowChannel;
            set => SetProperty(_data.TwitchChatWindowChannel, value ?? "", v => _data.TwitchChatWindowChannel = v);
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
            if (!removed)
                return false;

            if (SelectedSavedLocationId == id)
                _data.SelectedSavedLocationId = "";
            Save();
            RaiseEvent(SavedLocationsChanged);
            return true;
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

        public void SelectTwitchChannel(string channelName)
        {
            SelectedSavedStreamId = "";
            CurrentTwitchChannel = channelName;
            CurrentStreamSourceType = StreamSourceType.TwitchChannel;
            CurrentStreamPreset = null;
        }

        public void SelectUrlChannel(ChannelData channel)
        {
            SelectedSavedStreamId = "";
            CurrentTwitchChannel = "";
            SelectedUrlChannelId = channel.Id;
            CurrentStreamSourceType = StreamSourceType.Url;
            CurrentStreamPreset = channel.IsRadio ? channel.ToStreamPresetData() : null;
            StreamUrl = channel.Url;
        }

        public void SelectSavedStream(SavedStream stream)
        {
            SelectedSavedStreamId = stream.Id;
            CurrentStreamSourceType = stream.SourceType;
            CurrentTwitchChannel = stream.SourceType == StreamSourceType.TwitchChannel ? stream.Value : "";
            if (stream.SourceType == StreamSourceType.Url)
                StreamUrl = stream.Value;
            CurrentStreamPreset = null;
        }

        #endregion

        #region Private Methods

        private bool SetProperty<T>(T currentValue, T newValue, Action<T> setter)
        {
            if (Equals(currentValue, newValue))
                return false;
            setter(newValue);
            Save();
            return true;
        }

        private bool SetPropertyWithEvent<T>(T currentValue, T newValue, Action<T> setter, EventHandler<T> eventHandler)
        {
            if (!SetProperty(currentValue, newValue, setter))
                return false;
            RaiseEvent(eventHandler, newValue);
            return true;
        }

        private void RaiseEvent<T>(EventHandler<T> eventHandler, T value) => eventHandler?.Invoke(this, value);

        private void RaiseEvent(EventHandler eventHandler) => eventHandler?.Invoke(this, EventArgs.Empty);

        private static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;
            return value;
        }

        private void MigrateTwitchChannelFromSelectedStream()
        {
            if (!string.IsNullOrEmpty(_data.CurrentTwitchChannel) || string.IsNullOrEmpty(_data.SelectedSavedStreamId))
                return;

            var selectedStream = _data.SavedStreams.Streams.Find(s => s.Id == _data.SelectedSavedStreamId);
            if (selectedStream != null && selectedStream.SourceType == StreamSourceType.TwitchChannel)
            {
                _data.CurrentTwitchChannel = selectedStream.Value;
                Save();
            }
        }

        private void Load()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    _data = JsonConvert.DeserializeObject<CinemaUserSettingsData>(json) 
                        ?? new CinemaUserSettingsData();
                    
                    if (_data.SavedLocations == null) _data.SavedLocations = new SavedLocationCollection();
                    if (_data.SavedStreams == null) _data.SavedStreams = new SavedStreamCollection();
                    if (_data.WorldPosition == null) _data.WorldPosition = new WorldPosition3D(0, 0, 0, 0);
                                        
                    MigrateTwitchChannelFromSelectedStream();
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
        public string SelectedUrlChannelId { get; set; } = "";
        public WorldPosition3D WorldPosition { get; set; } = new WorldPosition3D(0, 0, 0, 0);
        public float WorldScreenWidth { get; set; } = 10f;
        public Point WindowPosition { get; set; } = new Point(100, 50);
        public Point WindowSize { get; set; } = new Point(640, 360);
        public bool WindowLocked { get; set; }
        public SavedLocationCollection SavedLocations { get; set; } = new SavedLocationCollection();
        public SavedStreamCollection SavedStreams { get; set; } = new SavedStreamCollection();
        public string TwitchAccessToken { get; set; }
        public string TwitchRefreshToken { get; set; }
        public string LastSelectedSourceCategory { get; set; }
        public int SelectedSettingsTab { get; set; }
        public bool TwitchChatWindowLocked { get; set; }
        public bool TwitchChatWindowOpen { get; set; }
        public Point TwitchChatWindowSize { get; set; } = new Point(439, 514);
        public string TwitchChatWindowChannel { get; set; } = "";
    }
}
