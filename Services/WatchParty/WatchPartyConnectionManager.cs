using System;
using System.Net;
using System.Threading.Tasks;
using Blish_HUD;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaModule.Services.WatchParty
{
    public sealed class WatchPartyConnectionManager : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<WatchPartyConnectionManager>();

        private const string HubUrl = "https://stream.gw2music.com/hubs/watchparty";
        private static readonly TimeSpan[] ReconnectDelays = {
            TimeSpan.FromSeconds(0),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        };

        private HubConnection _connection;
        private bool _isDisposed;
        private Action<HubConnection> _handlerRegistration;
        private static bool _tlsConfigured;

        public event Func<Exception, Task> ConnectionClosed;
        public event Func<Exception, Task> ConnectionReconnecting;
        public event Func<string, Task> ConnectionReconnected;

        public HubConnection Connection => _connection;
        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        public void SetHandlerRegistration(Action<HubConnection> registration)
        {
            _handlerRegistration = registration;
        }

        public async Task EnsureConnectedAsync()
        {
            if (IsConnected) return;

            ConfigureTls();

            if (_connection != null)
                await DisposeConnectionAsync().ConfigureAwait(false);

            _connection = new HubConnectionBuilder()
                .WithUrl(HubUrl)
                .AddNewtonsoftJsonProtocol()
                .WithAutomaticReconnect(ReconnectDelays)
                .Build();

            _connection.Closed += OnConnectionClosed;
            _connection.Reconnecting += OnConnectionReconnecting;
            _connection.Reconnected += OnConnectionReconnected;

            _handlerRegistration?.Invoke(_connection);

            try
            {
                Logger.Info("Connecting to watch party server...");
                await _connection.StartAsync().ConfigureAwait(false);
                Logger.Info("Connected to watch party server");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to connect to watch party server");
                await DisposeConnectionAsync().ConfigureAwait(false);
                throw new InvalidOperationException("Unable to reach the watch party server.", ex);
            }
        }

        public async Task<T> InvokeAsync<T>(string method, params object[] args)
        {
            var connection = _connection;
            if (connection?.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to server.");

            try
            {
                return await connection.InvokeCoreAsync<T>(method, args).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Hub invoke failed: {method}", method);
                throw;
            }
        }

        public async Task InvokeAsync(string method, params object[] args)
        {
            var connection = _connection;
            if (connection?.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to server.");

            try
            {
                await connection.InvokeCoreAsync(method, args).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Hub invoke failed: {method}", method);
                throw;
            }
        }

        private Task OnConnectionClosed(Exception error)
        {
            if (_isDisposed) return Task.CompletedTask;

            if (error != null)
                Logger.Error(error, "Connection closed unexpectedly");
            else
                Logger.Info("Connection closed");

            return ConnectionClosed?.Invoke(error) ?? Task.CompletedTask;
        }

        private Task OnConnectionReconnecting(Exception error)
        {
            if (_isDisposed) return Task.CompletedTask;
            Logger.Warn("Connection lost, reconnecting...");
            return ConnectionReconnecting?.Invoke(error) ?? Task.CompletedTask;
        }

        private Task OnConnectionReconnected(string connectionId)
        {
            if (_isDisposed) return Task.CompletedTask;
            Logger.Info("Reconnected to watch party server");
            return ConnectionReconnected?.Invoke(connectionId) ?? Task.CompletedTask;
        }

        private static void ConfigureTls()
        {
            if (_tlsConfigured) return;
            _tlsConfigured = true;

            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to configure TLS");
            }
        }

        private async Task DisposeConnectionAsync()
        {
            var connection = _connection;
            if (connection == null) return;

            _connection = null;

            connection.Closed -= OnConnectionClosed;
            connection.Reconnecting -= OnConnectionReconnecting;
            connection.Reconnected -= OnConnectionReconnected;

            try
            {
                await connection.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error stopping connection during dispose");
            }

            try
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error disposing connection");
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            var connection = _connection;
            if (connection == null) return;

            _connection = null;

            connection.Closed -= OnConnectionClosed;
            connection.Reconnecting -= OnConnectionReconnecting;
            connection.Reconnected -= OnConnectionReconnected;

            _ = DisposeConnectionFireAndForgetAsync(connection);
        }

        private static async Task DisposeConnectionFireAndForgetAsync(HubConnection connection)
        {
            try
            {
                await connection.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error stopping connection during fire-and-forget dispose");
            }

            try
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Error disposing connection during fire-and-forget dispose");
            }
        }
    }
}
