using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Settings;
using CinemaModule.UI.Windows.Info;

namespace CinemaModule.Settings
{
    public enum CinemaDisplayMode
    {
        OnScreen,
        InGame
    }

    public class CinemaSettings
    {
        public SettingEntry<bool> EnabledSetting { get; }

        public bool IsEnabled => EnabledSetting.Value;

        private ThirdPartyNoticesWindow _thirdPartyNoticesWindow;

        private bool _isShowingNotices = false;

        public CinemaSettings(SettingCollection settings)
        {
            EnabledSetting = settings.DefineSetting(
                "CinemaEnabled",
                false,
                () => "Enable Cinema",
                () => "Toggle the CinemaHUD video player on or off");

            var buttonSetting = settings.DefineSetting(
                "ShowThirdPartyNotices",
                false,
                () => "Third-Party Notices",
                () => "View third-party software notices and licenses");

            buttonSetting.SettingChanged += (s, e) =>
            {
                if (e.NewValue && !_isShowingNotices)
                {
                    _isShowingNotices = true;
                    ShowThirdPartyNotices();
                    _isShowingNotices = false;
                }
                else if (!e.NewValue && _isShowingNotices)
                {
                    _isShowingNotices = false;
                }
            };
        }

        private void ShowThirdPartyNotices()
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
