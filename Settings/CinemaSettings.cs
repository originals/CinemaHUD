using System;
using Blish_HUD.Input;
using Blish_HUD.Settings;
using CinemaModule.UI.Windows.Info;
using Microsoft.Xna.Framework.Input;

namespace CinemaModule.Settings
{
    public enum CinemaDisplayMode
    {
        OnScreen,
        InGame
    }

    public class CinemaSettings : IDisposable
    {
        public SettingEntry<bool> EnabledSetting { get; }

        public bool IsEnabled => EnabledSetting.Value;

        public SettingEntry<bool> KeybindsEnabled { get; }
        public SettingEntry<KeyBinding> KeybindPlayPause { get; }
        public SettingEntry<KeyBinding> KeybindLockWindow { get; }
        public SettingEntry<KeyBinding> KeybindMuteToggle { get; }
        public SettingEntry<KeyBinding> KeybindToggleEnabled { get; }

        private ThirdPartyNoticesWindow _thirdPartyNoticesWindow;

        public CinemaSettings(SettingCollection settings)
        {
            EnabledSetting = settings.DefineSetting(
                "CinemaEnabled",
                false,
                () => "Enable Cinema",
                () => "Toggle the CinemaHUD video player on or off");

            var keybindCollection = settings.AddSubCollection("Keybinds");

            KeybindsEnabled = keybindCollection.DefineSetting(
                "KeybindsEnabled",
                false,
                () => "Enable Keybinds",
                () => "Master toggle for all CinemaHUD keybinds");

            KeybindPlayPause = keybindCollection.DefineSetting(
                "KeybindPlayPause",
                new KeyBinding(Keys.None),
                () => "Play / Pause",
                () => "Toggle playback play or pause");

            KeybindLockWindow = keybindCollection.DefineSetting(
                "KeybindLockWindow",
                new KeyBinding(Keys.None),
                () => "Lock / Unlock Window",
                () => "Toggle the on-screen window lock to prevent moving or resizing");

            KeybindMuteToggle = keybindCollection.DefineSetting(
                "KeybindMuteToggle",
                new KeyBinding(Keys.None),
                () => "Mute / Unmute",
                () => "Toggle playback audio mute");

            KeybindToggleEnabled = keybindCollection.DefineSetting(
                "KeybindToggleEnabled",
                new KeyBinding(Keys.None),
                () => "Toggle Cinema On/Off",
                () => "Enable or disable the entire CinemaHUD module");
        }

        public void ShowThirdPartyNotices()
        {
            if (_thirdPartyNoticesWindow == null)
            {
                _thirdPartyNoticesWindow = new ThirdPartyNoticesWindow();
            }
            _thirdPartyNoticesWindow.Show();
        }

        public void Dispose()
        {
            _thirdPartyNoticesWindow?.Dispose();
        }
    }
}

