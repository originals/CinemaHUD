using Blish_HUD;
using CinemaModule.Models;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaModule.Services
{
    public class TwitchChatService : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<TwitchChatService>();

        private const string TwitchIrcServer = "irc.chat.twitch.tv";
        private const int TwitchIrcPort = 6667;

        private static readonly Random ChatColorRandom = new Random();
        private static readonly Color TwitchPurpleColor = new Color(169, 112, 255);

        private TcpClient _tcpClient;
        private StreamReader _reader;
        private StreamWriter _writer;
        private CancellationTokenSource _cts;
        private Task _readTask;

        private string _currentChannel;
        private string _authToken;
        private string _username;
        private bool _isConnected;
        private bool _isDisposed;

        public event EventHandler<TwitchChatMessageEventArgs> MessageReceived;
        public event EventHandler<TwitchChatConnectionEventArgs> ConnectionStateChanged;

        public bool IsConnected => _isConnected;
        public string Username => _username;
        public bool IsAuthenticated => !string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_authToken);

        public void SetCredentials(string username, string authToken)
        {
            _username = username;
            _authToken = authToken;
        }

        public async Task ConnectAsync(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                Logger.Warn("Cannot connect to chat - channel name is empty");
                return;
            }

            channel = channel.ToLowerInvariant().TrimStart('#');

            if (_isConnected && _currentChannel == channel)
            {
                return;
            }

            await DisconnectAsync();

            _currentChannel = channel;
            _cts = new CancellationTokenSource();

            try
            {
                await EstablishConnectionAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to connect to chat for #{channel}");
                RaiseConnectionStateChanged(false, $"Connection failed: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (!_isConnected && _tcpClient == null)
                return;

            _cts?.Cancel();

            try
            {
                if (_readTask != null)
                {
                    await Task.WhenAny(_readTask, Task.Delay(1000));
                }
            }
            catch { }

            CleanupConnection();

            _isConnected = false;
            _currentChannel = null;

            RaiseConnectionStateChanged(false, "Disconnected");
        }

        public async Task SendMessageAsync(string message)
        {
            if (!_isConnected || string.IsNullOrWhiteSpace(message))
                return;

            if (string.IsNullOrEmpty(_authToken))
            {
                Logger.Warn("Cannot send message - not authenticated");
                return;
            }

            try
            {
                await _writer.WriteLineAsync($"PRIVMSG #{_currentChannel} :{message}");
                await _writer.FlushAsync();

                // Add own message to chat, doesn't show otherwise
                var ownMessage = new TwitchChatMessage(_username, _username, message, TwitchPurpleColor);
                RaiseMessageReceived(ownMessage);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to send chat message");
            }
        }

        private async Task EstablishConnectionAsync()
        {
            if (_isDisposed || _cts?.IsCancellationRequested == true)
                return;

            RaiseConnectionStateChanged(false, "Connecting...");

            var tcpClient = new TcpClient();

            try
            {
                await tcpClient.ConnectAsync(TwitchIrcServer, TwitchIrcPort);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (NullReferenceException)
            {
                return;
            }
            catch (SocketException ex)
            {
                Logger.Warn($"Socket error connecting to Twitch IRC: {ex.Message}");
                tcpClient.Close();
                return;
            }

            if (_isDisposed || _cts?.IsCancellationRequested == true)
            {
                tcpClient.Close();
                return;
            }

            _tcpClient = tcpClient;

            var stream = tcpClient.GetStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
            var reader = new StreamReader(stream);

            _writer = writer;
            _reader = reader;

            if (_isDisposed || _cts?.IsCancellationRequested == true)
                return;

            await SendAuthenticationAsync(writer);

            if (_isDisposed || _cts?.IsCancellationRequested == true)
                return;

            await JoinChannelAsync(writer);

            if (_isDisposed || _cts?.IsCancellationRequested == true)
                return;

            _isConnected = true;
            RaiseConnectionStateChanged(true, $"Connected to #{_currentChannel}");

            _readTask = Task.Run(() => ReadMessagesAsync(_cts.Token));
        }

        private async Task SendAuthenticationAsync(StreamWriter writer)
        {
            await writer.WriteLineAsync("CAP REQ :twitch.tv/tags twitch.tv/commands");

            if (!string.IsNullOrEmpty(_authToken))
            {
                var token = _authToken.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase)
                    ? _authToken
                    : $"oauth:{_authToken}";
                await writer.WriteLineAsync($"PASS {token}");
                await writer.WriteLineAsync($"NICK {_username}");
            }
            else
            {
                var anonNick = $"justinfan{ChatColorRandom.Next(10000, 99999)}";
                await writer.WriteLineAsync($"NICK {anonNick}");
            }
        }

        private async Task ReconnectAnonymouslyAsync()
        {
            var channel = _currentChannel;

            CleanupConnection();
            _isConnected = false;

            // Clear credentials to force anonymous mode
            var savedToken = _authToken;
            var savedUsername = _username;
            _authToken = null;
            _username = null;

            _cts = new CancellationTokenSource();

            try
            {
                await EstablishConnectionAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to reconnect anonymously");
                // Restore credentials for potential retry
                _authToken = savedToken;
                _username = savedUsername;
            }
        }

        private async Task JoinChannelAsync(StreamWriter writer)
        {
            await writer.WriteLineAsync($"JOIN #{_currentChannel}");
        }

        private async Task ReadMessagesAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && !_isDisposed && _tcpClient?.Connected == true)
                {
                    var reader = _reader;
                    if (reader == null)
                        break;

                    var line = await reader.ReadLineAsync();

                    if (line == null)
                    {
                        break;
                    }

                    ProcessIrcMessage(line);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during disconnect
            }
            catch (ObjectDisposedException)
            {
                // Expected during disconnect
            }
            catch (Exception ex) when (!token.IsCancellationRequested && !_isDisposed)
            {
                Logger.Error(ex, "Error reading from IRC");
                HandleDisconnection();
            }
        }

        private void ProcessIrcMessage(string rawMessage)
        {
            if (string.IsNullOrEmpty(rawMessage))
                return;

            if (rawMessage.StartsWith("PING"))
            {
                HandlePing(rawMessage);
                return;
            }

            // Detect authentication failure
            if (rawMessage.Contains("NOTICE") && rawMessage.Contains("Login unsuccessful"))
            {
                Logger.Warn("Authentication failed - reconnecting anonymously");
                _ = ReconnectAnonymouslyAsync();
                return;
            }

            if (rawMessage.Contains("PRIVMSG"))
            {
                var chatMessage = ParsePrivMsg(rawMessage);
                if (chatMessage != null)
                {
                    RaiseMessageReceived(chatMessage);
                }
            }
        }

        private void HandlePing(string pingMessage)
        {
            try
            {
                var pongResponse = pingMessage.Replace("PING", "PONG");
                _writer.WriteLine(pongResponse);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to send PONG");
            }
        }

        private TwitchChatMessage ParsePrivMsg(string rawMessage)
        {
            try
            {
                var message = new TwitchChatMessage();

                if (rawMessage.StartsWith("@"))
                {
                    var tagsEnd = rawMessage.IndexOf(' ');
                    if (tagsEnd > 0)
                    {
                        var tagsSection = rawMessage.Substring(1, tagsEnd - 1);
                        ParseTags(tagsSection, message);
                        rawMessage = rawMessage.Substring(tagsEnd + 1);
                    }
                }

                var privmsgIndex = rawMessage.IndexOf("PRIVMSG");
                if (privmsgIndex < 0)
                    return null;

                var usernameMatch = Regex.Match(rawMessage, @":(\w+)!");
                if (usernameMatch.Success)
                {
                    message.Username = usernameMatch.Groups[1].Value;
                }

                if (string.IsNullOrEmpty(message.DisplayName))
                {
                    message.DisplayName = message.Username;
                }

                var messageStart = rawMessage.IndexOf(':', privmsgIndex);
                if (messageStart >= 0)
                {
                    var text = rawMessage.Substring(messageStart + 1);

                    if (text.StartsWith("\u0001ACTION ") && text.EndsWith("\u0001"))
                    {
                        message.IsAction = true;
                        text = text.Substring(8, text.Length - 9);
                    }

                    message.Message = text;
                }

                return message;
            }
            catch (Exception ex)
            {
                Logger.Debug($"Failed to parse PRIVMSG: {ex.Message}");
                return null;
            }
        }

        private void ParseTags(string tagsSection, TwitchChatMessage message)
        {
            var tags = tagsSection.Split(';');

            foreach (var tag in tags)
            {
                var parts = tag.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                var key = parts[0];
                var value = parts[1];

                switch (key)
                {
                    case "display-name":
                        message.DisplayName = value;
                        break;

                    case "color":
                        message.UserColor = ParseColor(value);
                        break;
                }
            }
        }

        private Color ParseColor(string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex) || !colorHex.StartsWith("#"))
                return GetRandomChatColor();

            try
            {
                var hex = colorHex.TrimStart('#');
                var r = Convert.ToInt32(hex.Substring(0, 2), 16);
                var g = Convert.ToInt32(hex.Substring(2, 2), 16);
                var b = Convert.ToInt32(hex.Substring(4, 2), 16);
                return new Color(r, g, b);
            }
            catch
            {
                return GetRandomChatColor();
            }
        }

        private Color GetRandomChatColor()
        {
            var colors = new[]
            {
                new Color(255, 0, 0),      // Red
                new Color(0, 0, 255),      // Blue
                new Color(0, 128, 0),      // Green
                new Color(178, 34, 34),    // FireBrick
                new Color(255, 127, 80),   // Coral
                new Color(154, 205, 50),   // YellowGreen
                new Color(255, 69, 0),     // OrangeRed
                new Color(46, 139, 87),    // SeaGreen
                new Color(218, 165, 32),   // GoldenRod
                new Color(210, 105, 30),   // Chocolate
                new Color(95, 158, 160),   // CadetBlue
                new Color(30, 144, 255),   // DodgerBlue
                new Color(255, 105, 180),  // HotPink
                new Color(138, 43, 226),   // BlueViolet
                new Color(0, 255, 127)     // SpringGreen
            };

            return colors[ChatColorRandom.Next(colors.Length)];
        }

        private void RaiseMessageReceived(TwitchChatMessage message)
        {
            MessageReceived?.Invoke(this, new TwitchChatMessageEventArgs(message));
        }

        private void HandleDisconnection()
        {
            _isConnected = false;
            RaiseConnectionStateChanged(false, "Connection lost");
            CleanupConnection();
        }

        private void CleanupConnection()
        {
            try
            {
                _reader?.Dispose();
                _writer?.Dispose();
                _tcpClient?.Close();
                _tcpClient?.Dispose();
            }
            catch { }
            finally
            {
                _reader = null;
                _writer = null;
                _tcpClient = null;
            }

            _cts?.Dispose();
            _cts = null;
        }

        private void RaiseConnectionStateChanged(bool isConnected, string status)
        {
            ConnectionStateChanged?.Invoke(this, new TwitchChatConnectionEventArgs(isConnected, status));
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _cts?.Cancel();
            CleanupConnection();
        }
    }

    public class TwitchChatMessageEventArgs : EventArgs
    {
        public TwitchChatMessage Message { get; }

        public TwitchChatMessageEventArgs(TwitchChatMessage message)
        {
            Message = message;
        }
    }

    public class TwitchChatConnectionEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string Status { get; }

        public TwitchChatConnectionEventArgs(bool isConnected, string status)
        {
            IsConnected = isConnected;
            Status = status;
        }
    }
}
