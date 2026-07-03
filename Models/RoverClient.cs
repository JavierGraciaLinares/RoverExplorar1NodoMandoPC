using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoverExplorer1NodoMandoPC.Models
{
    public class RoverClient : IDisposable
    {
        private TcpClient? _client;
        private StreamWriter? _writer;
        private StreamReader? _reader;
        private CancellationTokenSource? _cts;
        private readonly object _lock = new();

        public event Action<string>? OnTelemetryReceived;
        public event Action<bool>? OnConnectionChanged;

        public bool IsConnected => _client?.Connected ?? false;

        public async Task ConnectAsync(string ip, int port)
        {
            Disconnect();
            _cts = new CancellationTokenSource();
            _client = new TcpClient();
            await _client.ConnectAsync(ip, port);
            var stream = _client.GetStream();
            _writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\n" };
            _reader = new StreamReader(stream, Encoding.ASCII);
            OnConnectionChanged?.Invoke(true);
            _ = Task.Run(() => ReceiveLoop(_cts.Token));
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            lock (_lock)
            {
                _writer?.Close();
                _reader?.Close();
                _client?.Close();
                _writer = null;
                _reader = null;
                _client = null;
            }
            OnConnectionChanged?.Invoke(false);
        }

        public void SendCommand(string command)
        {
            lock (_lock)
            {
                _writer?.WriteLine(command);
            }
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _reader != null)
            {
                try
                {
                    var line = await _reader.ReadLineAsync(token);
                    if (line == null) break;
                    OnTelemetryReceived?.Invoke(line);
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    if (!token.IsCancellationRequested)
                        OnConnectionChanged?.Invoke(false);
                    break;
                }
            }
        }

        public void Dispose() => Disconnect();
    }
}
