using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Gw2Sharp.WebApi.V2.Models;

namespace CinemaModule.Services.WatchParty
{
    public sealed class Gw2IdentityService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<Gw2IdentityService>();
        private static readonly TimeSpan FetchCooldown = TimeSpan.FromSeconds(5);
        private static readonly TokenPermission[] RequiredPermissions = { TokenPermission.Account };

        private readonly Gw2ApiManager _gw2ApiManager;
        private string _localGw2Name;
        private bool _isDisposed;
        private DateTime _lastFetchAttempt = DateTime.MinValue;

        public event EventHandler ApiAvailabilityChanged;

        public string AccountName => _localGw2Name;
        public bool IsAvailable => !string.IsNullOrEmpty(_localGw2Name);

        public Gw2IdentityService(Gw2ApiManager gw2ApiManager)
        {
            _gw2ApiManager = gw2ApiManager;
            _gw2ApiManager.SubtokenUpdated += OnSubtokenUpdated;
            FetchAccountNameFireAndForget();
        }

        private void OnSubtokenUpdated(object sender, ValueEventArgs<IEnumerable<TokenPermission>> e)
        {
            if (_isDisposed) return;
            FetchAccountNameFireAndForget();
        }

        private async void FetchAccountNameFireAndForget()
        {
            try
            {
                await FetchAccountNameAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Fire-and-forget fetch failed");
            }
        }

        public async Task FetchAccountNameAsync(bool ignoreCooldown = false)
        {
            if (_isDisposed) return;
            if (!ignoreCooldown && (DateTime.UtcNow - _lastFetchAttempt) < FetchCooldown) return;

            _lastFetchAttempt = DateTime.UtcNow;
            bool wasAvailable = IsAvailable;

            try
            {
                var client = _gw2ApiManager.Gw2ApiClient;
                bool hasPermission = _gw2ApiManager.HasPermissions(RequiredPermissions);

                if (client == null || !hasPermission)
                {
                    ClearAccountName(wasAvailable);
                    return;
                }

                var account = await client.V2.Account.GetAsync().ConfigureAwait(false);
                if (_isDisposed) return;

                _localGw2Name = account.Name;
                Logger.Info($"Resolved GW2 account: {_localGw2Name}");
                if (!wasAvailable) RaiseApiAvailabilityChanged();
            }
            catch (Gw2Sharp.WebApi.Exceptions.AuthorizationRequiredException ex)
            {
                HandleFetchError(wasAvailable, ex, "API token unauthorized");
            }
            catch (Gw2Sharp.WebApi.Exceptions.RequestException ex)
            {
                HandleFetchError(wasAvailable, ex, "GW2 API request failed");
            }
            catch (Exception ex)
            {
                HandleFetchError(wasAvailable, ex, "Unexpected error fetching account name");
            }
        }

        private void ClearAccountName(bool wasAvailable)
        {
            _localGw2Name = null;
            if (wasAvailable) RaiseApiAvailabilityChanged();
        }

        private void RaiseApiAvailabilityChanged()
        {
            try
            {
                ApiAvailabilityChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error in ApiAvailabilityChanged handler");
            }
        }

        private void HandleFetchError(bool wasAvailable, Exception ex, string message)
        {
            if (_isDisposed) return;
            Logger.Warn(ex, message);
            ClearAccountName(wasAvailable);
        }

        public async Task<bool> EnsureAvailableAsync()
        {
            if (IsAvailable) return true;
            await FetchAccountNameAsync(ignoreCooldown: true).ConfigureAwait(false);
            return IsAvailable;
        }

        public string GetDiagnosticInfo()
        {
            bool hasAccountPermission = _gw2ApiManager.HasPermissions(RequiredPermissions);
            return $"AccountName: {_localGw2Name ?? "(not set)"}, " +
                   $"HasClient: {_gw2ApiManager.Gw2ApiClient != null}, " +
                   $"HasAccountPerm: {hasAccountPermission}";
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _gw2ApiManager.SubtokenUpdated -= OnSubtokenUpdated;
        }
    }
}
