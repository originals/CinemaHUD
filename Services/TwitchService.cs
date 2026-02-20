using Blish_HUD;
using Blish_HUD.Content;
using CinemaModule.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CinemaModule.Services
{
    public class TwitchService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<TwitchService>();

        private const string TwitchGqlUrl = "https://gql.twitch.tv/gql";
        private const string TwitchHelixUrl = "https://api.twitch.tv/helix";
        private const string TwitchClientId = "kimne78kx3ncx6brgo4mv6wki5h1ko";
        private const string TwitchAuthClientId = "8m7h0mxthjx16qofx82mruz640ke67";
        private const string TwitchUsherUrl = "https://usher.ttvnw.net/api/channel/hls";
        private const string AvatarCacheSubfolder = "avatars";

        private readonly HttpClient _httpClient;
        private readonly ImageCacheService _imageCache;

        private List<TwitchStreamQuality> _cachedQualities = new List<TwitchStreamQuality>();
        private string _cachedQualitiesChannel;
        private int _selectedQualityIndex;
        private bool _isFetchingQualities;
        private string _authToken;
        private string _userId;

        public event EventHandler<TwitchQualitiesEventArgs> QualitiesChanged;
        public event EventHandler ScopeError;

        public ImageCacheService ImageCache => _imageCache;
        public bool IsAuthenticated => !string.IsNullOrEmpty(_authToken);

        public TwitchService(string cacheDirectory)
        {
            _httpClient = new HttpClient();

            var avatarCacheDir = Path.Combine(cacheDirectory, AvatarCacheSubfolder);
            _imageCache = new ImageCacheService(avatarCacheDir, _httpClient);
        }

        public void SetAuthToken(string token, string userId = null)
        {
            _authToken = token;
            _userId = userId;
        }

        public async Task<TwitchStreamInfo> GetStreamInfoAsync(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return null;

            try
            {
                var query = BuildStreamInfoQuery(channelName);
                var json = await ExecuteGqlRequestAsync(query, "GetStreamInfo");

                if (json == null)
                    return null;

                return ParseStreamInfo(json, channelName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to get stream info for channel: {channelName}");
                return null;
            }
        }

        public async Task<Dictionary<string, TwitchStreamInfo>> GetMultipleStreamInfoAsync(List<string> channelNames)
        {
            var result = new Dictionary<string, TwitchStreamInfo>(StringComparer.OrdinalIgnoreCase);

            if (channelNames == null || channelNames.Count == 0)
                return result;

            var validChannels = channelNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (validChannels.Count == 0)
                return result;

            try
            {
                var query = BuildMultipleStreamInfoQuery(validChannels);
                var json = await ExecuteGqlRequestAsync(query, "GetMultipleStreamInfo");

                if (json == null)
                    return result;

                return ParseMultipleStreamInfo(json, validChannels);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to get stream info for {validChannels.Count} channels");
                return result;
            }
        }

        public async Task<List<TwitchStreamInfo>> GetFollowedChannelsAsync()
        {
            if (!IsAuthenticated || string.IsNullOrEmpty(_userId))
                return new List<TwitchStreamInfo>();

            try
            {
                var streams = await GetFollowedLiveStreamsHelixAsync();
                return streams;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to get followed channels");
                return new List<TwitchStreamInfo>();
            }
        }

        private async Task<List<TwitchStreamInfo>> GetFollowedLiveStreamsHelixAsync()
        {
            var url = $"{TwitchHelixUrl}/streams/followed?user_id={_userId}&first=50";

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Add("Client-ID", TwitchAuthClientId);
                request.Headers.Add("Authorization", $"Bearer {_authToken}");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Logger.Warn($"Failed to get followed streams: {error}");

                    if (error.Contains("Missing scope"))
                    {
                        Logger.Info("Token missing required scope - triggering re-authentication");
                        ScopeError?.Invoke(this, EventArgs.Empty);
                    }

                    return new List<TwitchStreamInfo>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                var streams = ParseHelixFollowedStreams(json);
                return await EnrichWithUserAvatars(streams);
            }
        }

        private async Task<List<TwitchStreamInfo>> EnrichWithUserAvatars(List<TwitchStreamInfo> streams)
        {
            if (streams.Count == 0)
                return streams;

            var userIds = streams.Select(s => s.UserId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            if (userIds.Count == 0)
                return streams;

            var url = $"{TwitchHelixUrl}/users?{string.Join("&", userIds.Select(id => $"id={id}"))}";

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Add("Client-ID", TwitchAuthClientId);
                request.Headers.Add("Authorization", $"Bearer {_authToken}");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return streams;

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                var users = json["data"] as JArray;

                if (users == null)
                    return streams;

                var avatarMap = new Dictionary<string, string>();
                foreach (var user in users)
                {
                    var id = user["id"]?.ToString();
                    var avatar = user["profile_image_url"]?.ToString();
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(avatar))
                        avatarMap[id] = avatar;
                }

                foreach (var stream in streams)
                {
                    if (!string.IsNullOrEmpty(stream.UserId) && avatarMap.TryGetValue(stream.UserId, out var avatarUrl))
                        stream.AvatarUrl = avatarUrl;
                }
            }

            return streams;
        }

        private List<TwitchStreamInfo> ParseHelixFollowedStreams(JObject json)
        {
            var result = new List<TwitchStreamInfo>();
            var data = json["data"] as JArray;

            if (data == null)
                return result;

            foreach (var stream in data)
            {
                var login = stream["user_login"]?.ToString();
                if (string.IsNullOrEmpty(login))
                    continue;

                result.Add(new TwitchStreamInfo
                {
                    ChannelName = login,
                    UserId = stream["user_id"]?.ToString(),
                    IsLive = true,
                    Title = stream["title"]?.ToString(),
                    GameName = stream["game_name"]?.ToString(),
                    ViewerCount = stream["viewer_count"]?.Value<int>() ?? 0
                });
            }

            return result;
        }

        public async Task<string> GetPlayableStreamUrlAsync(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return null;

            try
            {
                var accessToken = await GetStreamAccessTokenAsync(channelName);
                if (accessToken == null)
                {
                    Logger.Warn($"Could not get access token for channel: {channelName}");
                    return null;
                }

                return BuildHlsUrl(channelName, accessToken.Token, accessToken.Signature);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to get playable stream URL for channel: {channelName}");
                return null;
            }
        }

        public async Task<List<TwitchStreamQuality>> GetStreamQualitiesAsync(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return new List<TwitchStreamQuality>();

            try
            {
                var masterPlaylistUrl = await GetPlayableStreamUrlAsync(channelName);
                if (string.IsNullOrEmpty(masterPlaylistUrl))
                {
                    Logger.Warn($"Could not get master playlist URL for channel: {channelName}");
                    return new List<TwitchStreamQuality>();
                }

                var playlistContent = await _httpClient.GetStringAsync(masterPlaylistUrl);
                return ParseM3U8Playlist(playlistContent);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to get stream qualities for channel: {channelName}");
                return new List<TwitchStreamQuality>();
            }
        }

        public IReadOnlyList<TwitchStreamQuality> CachedQualities => _cachedQualities;


        public async void FetchAndCacheQualitiesAsync(string channelName)
        {
            if (_isFetchingQualities)
                return;

            if (string.IsNullOrWhiteSpace(channelName))
                return;

            _isFetchingQualities = true;

            try
            {
                var qualities = await GetStreamQualitiesAsync(channelName);

                if (qualities.Count > 0)
                {
                    _cachedQualities = qualities;
                    _cachedQualitiesChannel = channelName;
                    _selectedQualityIndex = 0;

                    var qualityNames = _cachedQualities.Select(q => q.DisplayName).ToList();
                    QualitiesChanged?.Invoke(this, new TwitchQualitiesEventArgs(qualityNames, _selectedQualityIndex));
                }
                else
                {
                    Logger.Warn($"No quality options found for Twitch channel: {channelName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to fetch Twitch qualities for channel: {channelName}");
            }
            finally
            {
                _isFetchingQualities = false;
            }
        }

        public string SelectQuality(int qualityIndex)
        {
            if (qualityIndex < 0 || qualityIndex >= _cachedQualities.Count)
            {
                Logger.Warn($"Invalid quality index: {qualityIndex}");
                return null;
            }

            _selectedQualityIndex = qualityIndex;
            var selectedQuality = _cachedQualities[qualityIndex];

            return selectedQuality.StreamUrl;
        }

        public void ClearCachedQualities()
        {
            _cachedQualities.Clear();
            _cachedQualitiesChannel = null;
            _selectedQualityIndex = 0;
        }

        private List<TwitchStreamQuality> ParseM3U8Playlist(string playlistContent)
        {
            var qualities = new List<TwitchStreamQuality>();
            
            if (string.IsNullOrEmpty(playlistContent))
                return qualities;

            var lines = playlistContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            var resolutionRegex = new Regex(@"RESOLUTION=(\d+x\d+)", RegexOptions.Compiled);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                if (!line.StartsWith("#EXT-X-STREAM-INF:"))
                    continue;

                var attributes = line.Substring("#EXT-X-STREAM-INF:".Length);
                
                string streamUrl = null;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var nextLine = lines[j].Trim();
                    if (!nextLine.StartsWith("#") && !string.IsNullOrEmpty(nextLine))
                    {
                        streamUrl = nextLine;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(streamUrl))
                    continue;

                int height = 0;
                int frameRate = 0;
                long bandwidth = 0;
                string name = null;
                bool isAudioOnly = false;
                bool isSource = false;

                var resolutionMatch = resolutionRegex.Match(attributes);
                if (resolutionMatch.Success)
                {
                    var parts = resolutionMatch.Groups[1].Value.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int h))
                    {
                        height = h;
                    }
                }

                var frameRateMatch = Regex.Match(attributes, @"FRAME-RATE=([\d.]+)");
                if (frameRateMatch.Success && double.TryParse(frameRateMatch.Groups[1].Value, out double fps))
                {
                    frameRate = (int)Math.Round(fps);
                }

                var bandwidthMatch = Regex.Match(attributes, @"BANDWIDTH=(\d+)");
                if (bandwidthMatch.Success)
                {
                    bandwidth = long.Parse(bandwidthMatch.Groups[1].Value);
                }

                var videoMatch = Regex.Match(attributes, @"VIDEO=""([^""]+)""");
                if (videoMatch.Success)
                {
                    name = videoMatch.Groups[1].Value;
                }

                isAudioOnly = name?.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0 || height == 0;
                isSource = name?.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0;

                var displayName = BuildQualityDisplayName(isAudioOnly, isSource, height, frameRate, name);

                qualities.Add(new TwitchStreamQuality(displayName, streamUrl));
            }

            qualities = qualities
                .OrderByDescending(q => q.DisplayName.StartsWith("Source"))
                .ThenByDescending(q => ExtractHeightFromDisplayName(q.DisplayName))
                .ToList();

            return qualities;
        }

        private string BuildQualityDisplayName(bool isAudioOnly, bool isSource, int height, int frameRate, string name)
        {
            if (isAudioOnly)
                return "Audio Only";

            if (isSource)
            {
                if (height > 0 && frameRate > 0)
                    return $"Source ({height}p{frameRate})";
                if (height > 0)
                    return $"Source ({height}p)";
                return "Source";
            }

            if (height > 0)
            {
                if (frameRate > 0 && frameRate != 30)
                    return $"{height}p{frameRate}";
                return $"{height}p";
            }

            return name ?? "Unknown";
        }

        private int ExtractHeightFromDisplayName(string displayName)
        {
            var match = Regex.Match(displayName, @"(\d+)p");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }


        private async Task<StreamAccessToken> GetStreamAccessTokenAsync(string channelName)
        {
            var query = BuildPlaybackAccessTokenQuery(channelName);
            var json = await ExecuteGqlRequestAsync(query, "PlaybackAccessToken", useAuth: true);

            if (json == null)
                return null;

            return ParseAccessToken(json, channelName);
        }

        private string BuildHlsUrl(string channelName, string token, string signature)
        {
            var random = new Random().Next(1000000, 9999999);
            return $"{TwitchUsherUrl}/{channelName.ToLowerInvariant()}.m3u8" +
                   $"?allow_source=true" +
                   $"&allow_audio_only=true" +
                   $"&fast_bread=true" +
                   $"&p={random}" +
                   $"&player_backend=mediaplayer" +
                   $"&playlist_include_framerate=true" +
                   $"&reassignments_supported=true" +
                   $"&sig={signature}" +
                   $"&supported_codecs=avc1" +
                   $"&token={Uri.EscapeDataString(token)}" +
                   $"&cdm=wv";
        }

        public string GetChannelUrl(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
                return null;

            return $"https://www.twitch.tv/{channelName.ToLowerInvariant()}";
        }

        public bool OpenTwitchChat(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                Logger.Warn("Cannot open Twitch chat - channel name is empty");
                return false;
            }

            var chatUrl = $"https://www.twitch.tv/popout/{channelName.ToLowerInvariant()}/chat?popout=";

            try
            {
                System.Diagnostics.Process.Start(chatUrl);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open Twitch chat URL: {0}", chatUrl);
                return false;
            }
        }

        public async Task<UrlAvailabilityResult> CheckUrlAvailabilityAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return new UrlAvailabilityResult { IsAvailable = false, StatusMessage = "No URL" };

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    return response.IsSuccessStatusCode
                        ? new UrlAvailabilityResult { IsAvailable = true, StatusMessage = "Available" }
                        : new UrlAvailabilityResult
                        {
                            IsAvailable = false,
                            StatusMessage = $"Unavailable ({(int)response.StatusCode})"
                        };
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return new UrlAvailabilityResult { IsAvailable = null, StatusMessage = "Unknown" };
            }
        }

        public async Task<AsyncTexture2D> GetAvatarTextureAsync(string cacheKey, string avatarUrl)
        {
            return await _imageCache.GetImageAsync(cacheKey, avatarUrl);
        }

        private JObject BuildStreamInfoQuery(string channelName)
        {
            return new JObject
            {
                ["query"] = @"
                    query GetStreamInfo($login: String!) {
                        user(login: $login) {
                            id
                            login
                            displayName
                            profileImageURL(width: 600)
                            stream {
                                id
                                title
                                viewersCount
                                game {
                                    id
                                    name
                                }
                            }
                        }
                    }",
                ["variables"] = new JObject { ["login"] = channelName.ToLowerInvariant() }
            };
        }

        private JObject BuildMultipleStreamInfoQuery(List<string> channelNames)
        {
            var loginsArray = new JArray(channelNames);
            return new JObject
            {
                ["query"] = @"
                    query GetMultipleStreamInfo($logins: [String!]!) {
                        users(logins: $logins) {
                            id
                            login
                            displayName
                            profileImageURL(width: 600)
                            stream {
                                id
                                title
                                viewersCount
                                game {
                                    id
                                    name
                                }
                            }
                        }
                    }",
                ["variables"] = new JObject { ["logins"] = loginsArray }
            };
        }

        private JObject BuildPlaybackAccessTokenQuery(string channelName)
        {
            return new JObject
            {
                ["operationName"] = "PlaybackAccessToken_Template",
                ["query"] = @"
                    query PlaybackAccessToken_Template($login: String!, $isLive: Boolean!, $vodID: ID!, $isVod: Boolean!, $playerType: String!) {
                        streamPlaybackAccessToken(channelName: $login, params: {platform: ""web"", playerBackend: ""mediaplayer"", playerType: $playerType}) @include(if: $isLive) {
                            value
                            signature
                            __typename
                        }
                        videoPlaybackAccessToken(id: $vodID, params: {platform: ""web"", playerBackend: ""mediaplayer"", playerType: $playerType}) @include(if: $isVod) {
                            value
                            signature
                            __typename
                        }
                    }",
                ["variables"] = new JObject
                {
                    ["isLive"] = true,
                    ["login"] = channelName.ToLowerInvariant(),
                    ["isVod"] = false,
                    ["vodID"] = "",
                    ["playerType"] = "site"
                }
            };
        }

        private Task<JObject> ExecuteGqlRequestAsync(JObject query, string operationName)
        {
            return ExecuteGqlRequestAsync(query, operationName, useAuth: false);
        }

        private async Task<JObject> ExecuteGqlRequestAsync(JObject query, string operationName, bool useAuth)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, TwitchGqlUrl)
            {
                Content = new StringContent(query.ToString(), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Client-ID", TwitchClientId);

            if (useAuth && !string.IsNullOrEmpty(_authToken))
            {
                request.Headers.Add("Authorization", $"Bearer {_authToken}");
            }

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Logger.Warn($"GQL {operationName} request failed: {errorContent}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            LogGqlErrors(json, operationName);

            return json;
        }

        private void LogGqlErrors(JObject json, string operationName)
        {
            var errors = json["errors"] as JArray;
            if (errors != null && errors.Count > 0)
            {
                Logger.Warn($"GQL {operationName} returned errors: {errors}");
            }
        }

        private TwitchStreamInfo ParseStreamInfo(JObject json, string channelName)
        {
            var user = json["data"]?["user"];

            if (user == null || user.Type == JTokenType.Null)
            {
                return new TwitchStreamInfo
                {
                    ChannelName = channelName,
                    IsLive = false
                };
            }

            var stream = user["stream"];
            bool isLive = stream != null && stream.Type != JTokenType.Null;

            return new TwitchStreamInfo
            {
                ChannelName = channelName,
                IsLive = isLive,
                Title = isLive ? stream["title"]?.ToString() : null,
                GameName = isLive ? stream["game"]?["name"]?.ToString() : null,
                ViewerCount = isLive ? stream["viewersCount"]?.Value<int>() ?? 0 : 0,
                AvatarUrl = user["profileImageURL"]?.ToString()
            };
        }

        private Dictionary<string, TwitchStreamInfo> ParseMultipleStreamInfo(JObject json, List<string> requestedChannels)
        {
            var result = new Dictionary<string, TwitchStreamInfo>(StringComparer.OrdinalIgnoreCase);
            var users = json["data"]?["users"] as JArray;

            if (users != null)
            {
                foreach (var user in users)
                {
                    var login = user["login"]?.ToString();
                    if (string.IsNullOrEmpty(login))
                        continue;

                    var stream = user["stream"];
                    bool isLive = stream != null && stream.Type != JTokenType.Null;

                    result[login] = new TwitchStreamInfo
                    {
                        ChannelName = login,
                        IsLive = isLive,
                        Title = isLive ? stream["title"]?.ToString() : null,
                        GameName = isLive ? stream["game"]?["name"]?.ToString() : null,
                        ViewerCount = isLive ? stream["viewersCount"]?.Value<int>() ?? 0 : 0,
                        AvatarUrl = user["profileImageURL"]?.ToString()
                    };
                }
            }

            foreach (var channelName in requestedChannels)
            {
                if (!result.ContainsKey(channelName))
                {
                    result[channelName] = new TwitchStreamInfo
                    {
                        ChannelName = channelName,
                        IsLive = false
                    };
                }
            }

            return result;
        }

        private StreamAccessToken ParseAccessToken(JObject json, string channelName)
        {
            var tokenData = json["data"]?["streamPlaybackAccessToken"];

            if (tokenData == null || tokenData.Type == JTokenType.Null)
            {
                Logger.Warn($"streamPlaybackAccessToken is null for channel: {channelName}");
                return null;
            }

            var token = tokenData["value"]?.ToString();
            var signature = tokenData["signature"]?.ToString();

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(signature))
            {
                Logger.Warn($"Token or signature is empty for channel: {channelName}");
                return null;
            }

            return new StreamAccessToken { Token = token, Signature = signature };
        }

        #region IDisposable

        public void Dispose()
        {
            _imageCache?.Dispose();
            _httpClient?.Dispose();
        }

        #endregion
    }

    internal class StreamAccessToken
    {
        public string Token { get; set; }
        public string Signature { get; set; }
    }

    public class UrlAvailabilityResult
    {
        public bool? IsAvailable { get; set; }
        public string StatusMessage { get; set; }
    }

    public class TwitchQualitiesEventArgs : EventArgs
    {
        public IReadOnlyList<string> QualityNames { get; }
        public int SelectedIndex { get; }

        public TwitchQualitiesEventArgs(IReadOnlyList<string> qualityNames, int selectedIndex)
        {
            QualityNames = qualityNames;
            SelectedIndex = selectedIndex;
        }
    }
}
