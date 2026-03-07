using System;
using Blish_HUD.Settings;
using CinemaModule.UI.Windows.Info;

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

        private ThirdPartyNoticesWindow _thirdPartyNoticesWindow;

        public CinemaSettings(SettingCollection settings)
        {
            EnabledSetting = settings.DefineSetting(
                "CinemaEnabled",
                false,
                () => "Enable Cinema",
                () => "Toggle the CinemaHUD video player on or off");
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
