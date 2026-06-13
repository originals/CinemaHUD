using System;
using Blish_HUD;
using Blish_HUD.Input;
using Blish_HUD.Settings;
using CinemaModule.Settings;

namespace CinemaModule.Services
{
    public class KeybindHandler : IDisposable
    {
        private readonly CinemaSettings _cinemaSettings;
        private readonly Action _togglePause;
        private readonly Action _toggleLockWindow;
        private readonly Action _toggleMute;
        private readonly Action _toggleEnabled;

        private readonly EventHandler<ValueChangedEventArgs<bool>> _keybindsEnabledChangedHandler;

        public KeybindHandler(
            CinemaSettings cinemaSettings,
            Action togglePause,
            Action toggleLockWindow,
            Action toggleMute,
            Action toggleEnabled)
        {
            _cinemaSettings = cinemaSettings;
            _togglePause = togglePause;
            _toggleLockWindow = toggleLockWindow;
            _toggleMute = toggleMute;
            _toggleEnabled = toggleEnabled;

            ApplyEnabledState(cinemaSettings.KeybindsEnabled.Value);

            _keybindsEnabledChangedHandler = (s, e) => ApplyEnabledState(e.NewValue);
            _cinemaSettings.KeybindsEnabled.SettingChanged += _keybindsEnabledChangedHandler;

            SubscribeToKeybinds();
        }

        private void ApplyEnabledState(bool enabled)
        {
            _cinemaSettings.KeybindPlayPause.Value.Enabled = enabled;
            _cinemaSettings.KeybindLockWindow.Value.Enabled = enabled;
            _cinemaSettings.KeybindMuteToggle.Value.Enabled = enabled;
            _cinemaSettings.KeybindToggleEnabled.Value.Enabled = enabled;
        }

        private void SubscribeToKeybinds()
        {
            _cinemaSettings.KeybindPlayPause.Value.Activated += OnPlayPauseActivated;
            _cinemaSettings.KeybindLockWindow.Value.Activated += OnLockWindowActivated;
            _cinemaSettings.KeybindMuteToggle.Value.Activated += OnMuteToggleActivated;
            _cinemaSettings.KeybindToggleEnabled.Value.Activated += OnToggleEnabledActivated;
        }

        private void OnPlayPauseActivated(object sender, EventArgs e) => _togglePause();
        private void OnLockWindowActivated(object sender, EventArgs e) => _toggleLockWindow();
        private void OnMuteToggleActivated(object sender, EventArgs e) => _toggleMute();
        private void OnToggleEnabledActivated(object sender, EventArgs e) => _toggleEnabled();

        public void Dispose()
        {
            _cinemaSettings.KeybindsEnabled.SettingChanged -= _keybindsEnabledChangedHandler;
            _cinemaSettings.KeybindPlayPause.Value.Activated -= OnPlayPauseActivated;
            _cinemaSettings.KeybindLockWindow.Value.Activated -= OnLockWindowActivated;
            _cinemaSettings.KeybindMuteToggle.Value.Activated -= OnMuteToggleActivated;
            _cinemaSettings.KeybindToggleEnabled.Value.Activated -= OnToggleEnabledActivated;
        }
    }
}
