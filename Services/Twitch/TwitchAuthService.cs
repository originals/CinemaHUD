using Blish_HUD;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaModule.Services.Twitch
{
    public class TwitchAuthService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<TwitchAuthService>();

        private const string TwitchDeviceAuthUrl = "https://id.twitch.tv/oauth2/device";
        private const string TwitchTokenUrl = "https://id.twitch.tv/oauth2/token";
        private const string TwitchValidateUrl = "https://id.twitch.tv/oauth2/validate";
        private const string TwitchRevokeUrl = "https://id.twitch.tv/oauth2/revoke";

        public const string ClientId = "8m7h0mxthjx16qofx82mruz640ke67";
        private const string Scopes = "user:read:subscriptions user:read:follows chat:read chat:edit";
        private const int PollIntervalMs = 5000;
        private const int DeviceCodeExpirySeconds = 1800;
        private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

        private readonly HttpClient _httpClient;
        private readonly object _pollLock = new object();
        private CancellationTokenSource _pollCts;

        public event EventHandler<TwitchAuthStatusEventArgs> AuthStatusChanged;
        public event EventHandler<DeviceCodeEventArgs> DeviceCodeReceived;

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public string Username { get; private set; }
        public string UserId { get; private set; }
        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

        public TwitchAuthService()
        {
            _httpClient = new HttpClient { Timeout = HttpTimeout };
        }

        public void LoadTokens(string accessToken, string refreshToken)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;

            if (!string.IsNullOrEmpty(AccessToken))
            {
                _ = ValidateTokenAsync();
            }
        }

        public async Task StartDeviceAuthFlowAsync()
        {
            CancellationToken cancellationToken;
            lock (_pollLock)
            {
                CancelPendingAuthInternal();
                _pollCts = new CancellationTokenSource();
                cancellationToken = _pollCts.Token;
            }

            try
            {
                var deviceCode = await RequestDeviceCodeAsync().ConfigureAwait(false);
                if (deviceCode == null)
                {
                    RaiseAuthStatus(TwitchAuthStatus.Failed, "Failed to get device code");
                    return;
                }

                DeviceCodeReceived?.Invoke(this, new DeviceCodeEventArgs(
                    deviceCode.UserCode,
                    deviceCode.VerificationUri));

                RaiseAuthStatus(TwitchAuthStatus.WaitingForUser, $"Enter code: {deviceCode.UserCode}");

                await PollForTokenAsync(deviceCode.DeviceCode, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                RaiseAuthStatus(TwitchAuthStatus.Cancelled, "Authentication cancelled");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Device auth flow failed");
                RaiseAuthStatus(TwitchAuthStatus.Failed, ex.Message);
            }
        }

        public void CancelPendingAuth()
        {
            lock (_pollLock)
            {
                CancelPendingAuthInternal();
            }
        }

        private void CancelPendingAuthInternal()
        {
            _pollCts?.Cancel();
            _pollCts?.Dispose();
            _pollCts = null;
        }

        public async Task<bool> ValidateTokenAsync()
        {
            if (string.IsNullOrEmpty(AccessToken))
                return false;

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, TwitchValidateUrl))
                {
                    request.Headers.Add("Authorization", $"OAuth {AccessToken}");

                    var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var json = JObject.Parse(content);
                        Username = json["login"]?.ToString();
                        UserId = json["user_id"]?.ToString();

                        Logger.Info($"Twitch token validated for user: {Username} (ID: {UserId})");
                        RaiseAuthStatus(TwitchAuthStatus.Authenticated, $"Logged in as {Username}");
                        return true;
                    }

                    if ((int)response.StatusCode == 401)
                    {
                        Logger.Info("Twitch token expired, attempting refresh");
                        return await RefreshTokenAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to validate Twitch token");
            }

            ClearTokens();
            RaiseAuthStatus(TwitchAuthStatus.NotAuthenticated, "Not logged in");
            return false;
        }

        public async Task<bool> RefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken))
            {
                ClearTokens();
                return false;
            }

            try
            {
                var content = CreateFormContent(
                    ("grant_type", "refresh_token"),
                    ("refresh_token", RefreshToken),
                    ("client_id", ClientId));

                var response = await _httpClient.PostAsync(TwitchTokenUrl, content).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var json = JObject.Parse(responseContent);

                    AccessToken = json["access_token"]?.ToString();
                    RefreshToken = json["refresh_token"]?.ToString();

                    await ValidateTokenAsync().ConfigureAwait(false);
                    return true;
                }

                Logger.Warn($"Failed to refresh Twitch token: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to refresh Twitch token");
            }

            ClearTokens();
            RaiseAuthStatus(TwitchAuthStatus.NotAuthenticated, "Session expired");
            return false;
        }

        public async Task LogoutAsync()
        {
            CancelPendingAuth();

            if (!string.IsNullOrEmpty(AccessToken))
            {
                try
                {
                    var content = CreateFormContent(
                        ("client_id", ClientId),
                        ("token", AccessToken));

                    await _httpClient.PostAsync(TwitchRevokeUrl, content).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to revoke Twitch token");
                }
            }

            ClearTokens();
            RaiseAuthStatus(TwitchAuthStatus.NotAuthenticated, "Logged out");
        }

        private async Task<DeviceCodeResponse> RequestDeviceCodeAsync()
        {
            try
            {
                var content = CreateFormContent(
                    ("client_id", ClientId),
                    ("scopes", Scopes));

                var response = await _httpClient.PostAsync(TwitchDeviceAuthUrl, content).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Logger.Warn($"Failed to get device code: {error}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(responseContent);

                return new DeviceCodeResponse
                {
                    DeviceCode = json["device_code"]?.ToString(),
                    UserCode = json["user_code"]?.ToString(),
                    VerificationUri = json["verification_uri"]?.ToString(),
                    ExpiresIn = json["expires_in"]?.Value<int>() ?? DeviceCodeExpirySeconds,
                    Interval = json["interval"]?.Value<int>() ?? 5
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to request device code");
                return null;
            }
        }

        private async Task PollForTokenAsync(string deviceCode, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(DeviceCodeExpirySeconds);

            while (!cancellationToken.IsCancellationRequested)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    RaiseAuthStatus(TwitchAuthStatus.Failed, "Code expired");
                    return;
                }

                await Task.Delay(PollIntervalMs, cancellationToken).ConfigureAwait(false);

                var tokenResult = await TryGetTokenAsync(deviceCode).ConfigureAwait(false);

                if (tokenResult == TokenPollResult.Success)
                {
                    await ValidateTokenAsync().ConfigureAwait(false);
                    return;
                }

                if (tokenResult == TokenPollResult.Failed)
                {
                    RaiseAuthStatus(TwitchAuthStatus.Failed, "Authentication denied");
                    return;
                }
            }
        }

        private async Task<TokenPollResult> TryGetTokenAsync(string deviceCode)
        {
            try
            {
                var content = CreateFormContent(
                    ("client_id", ClientId),
                    ("scopes", Scopes),
                    ("device_code", deviceCode),
                    ("grant_type", "urn:ietf:params:oauth:grant-type:device_code"));

                var response = await _httpClient.PostAsync(TwitchTokenUrl, content).ConfigureAwait(false);
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(responseContent);

                if (response.IsSuccessStatusCode)
                {
                    AccessToken = json["access_token"]?.ToString();
                    RefreshToken = json["refresh_token"]?.ToString();
                    return TokenPollResult.Success;
                }

                var error = json["message"]?.ToString() ?? json["error"]?.ToString();

                if (error == "authorization_pending")
                    return TokenPollResult.Pending;

                if (error == "slow_down")
                {
                    await Task.Delay(PollIntervalMs).ConfigureAwait(false);
                    return TokenPollResult.Pending;
                }

                Logger.Warn($"Token poll failed: {error}");
                return TokenPollResult.Failed;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to poll for token");
                return TokenPollResult.Pending;
            }
        }

        private void ClearTokens()
        {
            AccessToken = null;
            RefreshToken = null;
            Username = null;
            UserId = null;
        }

        private void RaiseAuthStatus(TwitchAuthStatus status, string message)
        {
            AuthStatusChanged?.Invoke(this, new TwitchAuthStatusEventArgs(status, message, Username, UserId, AccessToken, RefreshToken));
        }

        private static FormUrlEncodedContent CreateFormContent(params (string key, string value)[] pairs)
        {
            var content = new List<KeyValuePair<string, string>>(pairs.Length);
            foreach (var (key, value) in pairs)
            {
                content.Add(new KeyValuePair<string, string>(key, value));
            }
            return new FormUrlEncodedContent(content);
        }

        public void Dispose()
        {
            CancelPendingAuth();
            _httpClient?.Dispose();
        }

        private enum TokenPollResult
        {
            Pending,
            Success,
            Failed
        }

        private class DeviceCodeResponse
        {
            public string DeviceCode { get; set; }
            public string UserCode { get; set; }
            public string VerificationUri { get; set; }
            public int ExpiresIn { get; set; }
            public int Interval { get; set; }
        }
    }

    public enum TwitchAuthStatus
    {
        NotAuthenticated,
        WaitingForUser,
        Authenticated,
        Failed,
        Cancelled
    }

    public class TwitchAuthStatusEventArgs : EventArgs
    {
        public TwitchAuthStatus Status { get; }
        public string Message { get; }
        public string Username { get; }
        public string UserId { get; }
        public string AccessToken { get; }
        public string RefreshToken { get; }

        public TwitchAuthStatusEventArgs(TwitchAuthStatus status, string message, string username = null, string userId = null, string accessToken = null, string refreshToken = null)
        {
            Status = status;
            Message = message;
            Username = username;
            UserId = userId;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
        }
    }

    public class DeviceCodeEventArgs : EventArgs
    {
        public string UserCode { get; }
        public string VerificationUri { get; }

        public DeviceCodeEventArgs(string userCode, string verificationUri)
        {
            UserCode = userCode;
            VerificationUri = verificationUri;
        }
    }
}
