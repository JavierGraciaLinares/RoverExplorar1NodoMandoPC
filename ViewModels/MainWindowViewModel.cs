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
        [NotifyPropertyChangedFor(nameof(ConnectionStatusColor))]
        [NotifyPropertyChangedFor(nameof(ConnectionDot))]
        private bool _isConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ControllerStatusColor))]
        [NotifyPropertyChangedFor(nameof(ControllerDot))]
        private bool _isControllerConnected;

        public bool CanConnect => !IsConnected;
        public string ConnectionStatusColor => IsConnected ? "#4ecca3" : "#e94560";
        public string ConnectionDot => IsConnected ? "●" : "●";
        public string ControllerStatusColor => IsControllerConnected ? "#4ecca3" : "#e94560";
        public string ControllerDot => IsControllerConnected ? "●" : "●";

        public double LeftMotorBarWidth => Math.Abs(LeftMotorSpeed) / 255.0;
        public double RightMotorBarWidth => Math.Abs(RightMotorSpeed) / 255.0;
        public string LeftMotorDirection => LeftMotorSpeed > 0 ? "Adelante" : LeftMotorSpeed < 0 ? "Atras" : "Detenido";
        public string RightMotorDirection => RightMotorSpeed > 0 ? "Adelante" : RightMotorSpeed < 0 ? "Atras" : "Detenido";
        public bool LeftMotorActive => LeftMotorSpeed != 0;
        public bool RightMotorActive => RightMotorSpeed != 0;
        public int LeftMotorForwardValue => Math.Max(0, LeftMotorSpeed);
        public int LeftMotorReverseAbs => Math.Abs(Math.Min(0, LeftMotorSpeed));
        public int RightMotorForwardValue => Math.Max(0, RightMotorSpeed);
        public int RightMotorReverseAbs => Math.Abs(Math.Min(0, RightMotorSpeed));
        public bool LeftMotorIsForward => LeftMotorSpeed > 0;
        public bool LeftMotorIsReverse => LeftMotorSpeed < 0;
        public bool RightMotorIsForward => RightMotorSpeed > 0;
        public bool RightMotorIsReverse => RightMotorSpeed < 0;

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
        }

        partial void OnLeftMotorSpeedChanged(int value)
        {
            OnPropertyChanged(nameof(LeftMotorBarWidth));
            OnPropertyChanged(nameof(LeftMotorDirection));
            OnPropertyChanged(nameof(LeftMotorActive));
            OnPropertyChanged(nameof(LeftMotorForwardValue));
            OnPropertyChanged(nameof(LeftMotorReverseAbs));
            OnPropertyChanged(nameof(LeftMotorIsForward));
            OnPropertyChanged(nameof(LeftMotorIsReverse));
        }

        partial void OnRightMotorSpeedChanged(int value)
        {
            OnPropertyChanged(nameof(RightMotorBarWidth));
            OnPropertyChanged(nameof(RightMotorDirection));
            OnPropertyChanged(nameof(RightMotorActive));
            OnPropertyChanged(nameof(RightMotorForwardValue));
            OnPropertyChanged(nameof(RightMotorReverseAbs));
            OnPropertyChanged(nameof(RightMotorIsForward));
            OnPropertyChanged(nameof(RightMotorIsReverse));
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

            LeftMotorSpeed = (int)(left * 255);
            RightMotorSpeed = (int)(right * 255);
            SendMotorCommand(LeftMotorSpeed, RightMotorSpeed);
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
            try { await _roverClient.ConnectAsync(RoverIp, RoverPort); }
            catch (Exception ex) { AddLog($"Error: {ex.Message}"); ConnectionStatus = "Error"; }
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
            if (_roverClient.IsConnected) _roverClient.SendCommand("0,0");
            AddLog("Parada de emergencia!");
        }

        private void AddLog(string message)
        {
            LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (LogMessages.Count > 200) LogMessages.RemoveAt(0);
        }
    }
}
