using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoverExplorer1NodoMandoPC.Models;

namespace RoverExplorer1NodoMandoPC.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly Ps3Controller _controller;
        private readonly RoverClient _roverClient;
        private readonly SynchronizationContext _uiContext;
        private DateTime _lastCommandSent = DateTime.MinValue;
        private static readonly TimeSpan CommandInterval = TimeSpan.FromMilliseconds(50);

        [ObservableProperty]
        private string _roverIp = "192.168.1.100";

        [ObservableProperty]
        private int _roverPort = 8080;

        [ObservableProperty]
        private string _connectionStatus = "Desconectado";

        [ObservableProperty]
        private string _controllerStatus = "Buscando mando...";

        [ObservableProperty]
        private int _leftMotorSpeed;

        [ObservableProperty]
        private int _rightMotorSpeed;

        [ObservableProperty]
        private double _leftStickX;

        [ObservableProperty]
        private double _leftStickY;

        [ObservableProperty]
        private double _rightStickX;

        [ObservableProperty]
        private double _rightStickY;

        [ObservableProperty]
        private string _batteryLevel = "--";

        [ObservableProperty]
        private string _temperature = "--";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanConnect))]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isControllerConnected;

        public bool CanConnect => !IsConnected;

        public ObservableCollection<string> LogMessages { get; } = new();

        public MainWindowViewModel()
        {
            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _controller = new Ps3Controller();
            _roverClient = new RoverClient();

            _controller.OnStateChanged += s => _uiContext.Post(_ => ProcessControllerState(s), null);
            _controller.OnConnectionChanged += c => _uiContext.Post(_ => OnControllerConnection(c), null);
            _roverClient.OnConnectionChanged += c => _uiContext.Post(_ => OnRoverConnection(c), null);
            _roverClient.OnTelemetryReceived += d => _uiContext.Post(_ => ProcessTelemetry(d), null);

            _controller.Start();
        }

        public void SetWindowHandle(IntPtr handle)
        {
            _controller.SetWindowHandle(handle);
        }

        private void OnControllerConnection(bool connected)
        {
            IsControllerConnected = connected;
            ControllerStatus = connected ? "Conectado" : "Desconectado";
            AddLog(connected ? "Mando conectado" : "Mando desconectado");
        }

        private void OnRoverConnection(bool connected)
        {
            IsConnected = connected;
            ConnectionStatus = connected ? "Conectado" : "Desconectado";
            AddLog(connected ? "Conectado al rover" : "Desconectado del rover");
        }

        private void ProcessTelemetry(string data)
        {
            foreach (var part in data.Split(','))
            {
                var kv = part.Split(':');
                if (kv.Length != 2) continue;
                if (kv[0].Equals("BAT", StringComparison.OrdinalIgnoreCase))
                    BatteryLevel = kv[1] + "%";
                else if (kv[0].Equals("TEMP", StringComparison.OrdinalIgnoreCase))
                    Temperature = kv[1] + "°C";
            }
        }

        private void ProcessControllerState(ControllerState state)
        {
            LeftStickX = Math.Round(state.LeftStickX, 2);
            LeftStickY = Math.Round(state.LeftStickY, 2);
            RightStickX = Math.Round(state.RightStickX, 2);
            RightStickY = Math.Round(state.RightStickY, 2);

            if (state.Buttons.Length > 14 && state.Buttons[14])
            {
                LeftMotorSpeed = 0;
                RightMotorSpeed = 0;
                SendMotorCommand(0, 0);
                return;
            }

            double forward = -state.LeftStickY;
            double turn = state.LeftStickX;
            double left = forward + turn;
            double right = forward - turn;
            double max = Math.Max(Math.Abs(left), Math.Abs(right));
            if (max > 1.0) { left /= max; right /= max; }

            int leftSpeed = (int)(left * 255);
            int rightSpeed = (int)(right * 255);

            LeftMotorSpeed = leftSpeed;
            RightMotorSpeed = rightSpeed;

            SendMotorCommand(leftSpeed, rightSpeed);
        }

        private void SendMotorCommand(int left, int right)
        {
            if (DateTime.UtcNow - _lastCommandSent < CommandInterval) return;
            _lastCommandSent = DateTime.UtcNow;
            _roverClient.SendCommand($"{left},{right}");
        }

        [RelayCommand]
        private async Task Connect()
        {
            AddLog($"Conectando a {RoverIp}:{RoverPort}...");
            ConnectionStatus = "Conectando...";
            try
            {
                await _roverClient.ConnectAsync(RoverIp, RoverPort);
            }
            catch (Exception ex)
            {
                AddLog($"Error: {ex.Message}");
                ConnectionStatus = "Error de conexión";
            }
        }

        [RelayCommand]
        private void Disconnect()
        {
            _roverClient.Disconnect();
            AddLog("Desconectado");
        }

        [RelayCommand]
        private void EmergencyStop()
        {
            LeftMotorSpeed = 0;
            RightMotorSpeed = 0;
            if (_roverClient.IsConnected)
                _roverClient.SendCommand("0,0");
            AddLog("¡Parada de emergencia!");
        }

        private void AddLog(string message)
        {
            LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (LogMessages.Count > 200)
                LogMessages.RemoveAt(0);
        }
    }
}
