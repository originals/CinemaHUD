using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD;

namespace CinemaModule.Services
{
    public class RadioTrackInfo
    {
        public string TrackName { get; set; }
        public string Artist { get; set; }
        public string Title { get; set; }
        public int? Listeners { get; set; }
        public int? Bitrate { get; set; }
    }

    public class RadioMetadataService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<RadioMetadataService>();
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
        private const int MinShoutcastParts = 7;
        private const int TrackNameIndex = 6;
        private const int ListenersIndex = 0;
        private const int BitrateIndex = 5;

        private readonly HttpClient _httpClient;
        private CancellationTokenSource _pollingCts;
        private bool _isDisposed;

        public event EventHandler<RadioTrackInfo> TrackInfoUpdated;

        public RadioTrackInfo CurrentTrackInfo { get; private set; }

        public RadioMetadataService()
        {
            _httpClient = new HttpClient { Timeout = RequestTimeout };
        }

        public void StartPolling(string streamUrl, string infoUrl)
        {
            StopPolling();

            string metadataUrl = ResolveMetadataUrl(streamUrl, infoUrl);
            if (string.IsNullOrEmpty(metadataUrl))
            {
                Logger.Debug("No valid metadata URL for radio stream");
                return;
            }

            _pollingCts = new CancellationTokenSource();
            _ = PollMetadataAsync(metadataUrl, _pollingCts.Token);
        }

        public void StopPolling()
        {
            if (_pollingCts != null)
            {
                _pollingCts.Cancel();
                _pollingCts.Dispose();
                _pollingCts = null;
            }

            CurrentTrackInfo = null;
            TrackInfoUpdated?.Invoke(this, null);
        }

        private string ResolveMetadataUrl(string streamUrl, string infoUrl)
        {
            if (!string.IsNullOrEmpty(infoUrl))
                return infoUrl;

            return TryBuildShoutcastMetadataUrl(streamUrl);
        }

        private string TryBuildShoutcastMetadataUrl(string streamUrl)
        {
            if (string.IsNullOrEmpty(streamUrl))
                return null;

            try
            {
                var uri = new Uri(streamUrl);
                return $"{uri.Scheme}://{uri.Host}:{uri.Port}/7.html";
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Failed to build metadata URL from stream URL");
                return null;
            }
        }

        private async Task PollMetadataAsync(string metadataUrl, CancellationToken cancellationToken)
        {
            await Task.Delay(InitialDelay, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var trackInfo = await FetchTrackInfoAsync(metadataUrl, cancellationToken);
                    if (trackInfo != null && !cancellationToken.IsCancellationRequested)
                    {
                        CurrentTrackInfo = trackInfo;
                        TrackInfoUpdated?.Invoke(this, trackInfo);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "Failed to fetch radio metadata");
                }

                try
                {
                    await Task.Delay(PollingInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task<RadioTrackInfo> FetchTrackInfoAsync(string url, CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.GetAsync(url, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                return ParseShoutcastResponse(content);
            }
        }

        private RadioTrackInfo ParseShoutcastResponse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            content = StripHtmlTags(content);
            var parts = content.Split(',');

            if (parts.Length < MinShoutcastParts)
                return null;

            string trackName = parts[TrackNameIndex].Trim();
            if (string.IsNullOrEmpty(trackName))
                return null;

            var trackInfo = new RadioTrackInfo
            {
                TrackName = trackName,
                Listeners = TryParseInt(parts[ListenersIndex]),
                Bitrate = TryParseInt(parts[BitrateIndex])
            };

            ParseArtistAndTitle(trackName, trackInfo);

            return trackInfo;
        }

        private string StripHtmlTags(string input)
        {
            return Regex.Replace(input, "<[^>]+>", string.Empty).Trim();
        }

        private int? TryParseInt(string value)
        {
            return int.TryParse(value.Trim(), out int result) ? result : (int?)null;
        }

        private void ParseArtistAndTitle(string trackName, RadioTrackInfo trackInfo)
        {
            int separatorIndex = trackName.IndexOf(" - ", StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                trackInfo.Artist = trackName.Substring(0, separatorIndex).Trim();
                trackInfo.Title = trackName.Substring(separatorIndex + 3).Trim();
            }
            else
            {
                trackInfo.Title = trackName;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            StopPolling();
            _httpClient.Dispose();
            _isDisposed = true;
        }
    }
}
