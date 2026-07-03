using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoverExplorer1NodoMandoPC.Models
{
    public class RoverClient : IDisposable
    {
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();

        public event Action<string>? OnTelemetryReceived;
        public event Action<bool>? OnConnectionChanged;

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        public async Task ConnectAsync(string ip, int port)
        {
            Disconnect();
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();

            var uri = new Uri($"ws://{ip}:{port}");
            await _ws.ConnectAsync(uri, _cts.Token);

            OnConnectionChanged?.Invoke(true);
            _ = Task.Run(() => ReceiveLoop(_cts.Token));
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            lock (_lock)
            {
                if (_ws?.State == WebSocketState.Open)
                {
                    try { _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).GetAwaiter().GetResult(); }
                    catch { }
                }
                _ws?.Dispose();
                _ws = null;
            }
            OnConnectionChanged?.Invoke(false);
        }

        public void SendCommand(string command)
        {
            if (_ws?.State != WebSocketState.Open) return;
            _ = SendAsync(command);
        }

        private async Task SendAsync(string command)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(command);
                await _ws!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch { }
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[4096];
            while (!token.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _ws.ReceiveAsync(new Memory<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    OnTelemetryReceived?.Invoke(text);
                }
                catch (OperationCanceledException) { break; }
                catch { if (!token.IsCancellationRequested) break; }
            }
            if (!token.IsCancellationRequested)
                OnConnectionChanged?.Invoke(false);
        }

        public void Dispose() => Disconnect();
    }
}
