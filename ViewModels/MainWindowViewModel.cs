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
        private ControllerState? _lastState;

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
        private double _l2Value;

        [ObservableProperty]
        private double _r2Value;

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

        [ObservableProperty]
        private int _selectedTabIndex;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedDeviceDisplay))]
        private ControllerDeviceInfo? _selectedController;

        public bool CanConnect => !IsConnected;
        public string ConnectionStatusColor => IsConnected ? "#3fb950" : "#da3633";
        public string ConnectionDot => IsConnected ? "●" : "●";
        public string ControllerStatusColor => IsControllerConnected ? "#3fb950" : "#da3633";
        public string ControllerDot => IsControllerConnected ? "●" : "●";
        public string SelectedDeviceDisplay => SelectedController != null ? SelectedController.Name : "(seleccionar controlador)";

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

        public double L2Progress => Math.Abs(L2Value);
        public double R2Progress => Math.Abs(R2Value);

        [ObservableProperty]
        private string _rawAxesInfo = "";

        [ObservableProperty]
        private string _commandText = "";

        public ObservableCollection<string> LogMessages { get; } = new();
        public ObservableCollection<ControllerButtonState> ControllerButtons { get; } = new();
        public ObservableCollection<ControllerDeviceInfo> AvailableControllers { get; } = new();

        public MainWindowViewModel()
        {
            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _controller = new Ps3Controller();
            _roverClient = new RoverClient();

            _controller.OnStateChanged += s => _uiContext.Post(_ => ProcessControllerState(s), null);
            _controller.OnConnectionChanged += c => _uiContext.Post(_ => OnControllerConnection(c), null);
            _roverClient.OnConnectionChanged += c => _uiContext.Post(_ => OnRoverConnection(c), null);
            _roverClient.OnTelemetryReceived += d => _uiContext.Post(_ => ProcessTelemetry(d), null);

            InitButtons();
        }

        private void InitButtons()
        {
            string[] names = [
                "Cruz", "Circulo", "Cuadrado", "Triang",
                "L1", "R1",
                "Select", "Start",
                "L3", "R3",
                "Home",
                "D-Up", "D-Right", "D-Down", "D-Left",
                "L2", "R2"
            ];
            for (int i = 0; i < names.Length; i++)
                ControllerButtons.Add(new ControllerButtonState(i, names[i]));
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

        partial void OnL2ValueChanged(double value) => OnPropertyChanged(nameof(L2Progress));
        partial void OnR2ValueChanged(double value) => OnPropertyChanged(nameof(R2Progress));

        partial void OnSelectedControllerChanged(ControllerDeviceInfo? value)
        {
            OnPropertyChanged(nameof(SelectedDeviceDisplay));
            if (value == null) return;
            bool ok = _controller.TrySelectDevice(value.InstanceGuid);
            if (ok)
            {
                _controller.Start();
                AddLog($"Mando seleccionado: {value.Name}");
            }
            else
            {
                AddLog($"Error al conectar: {value.Name}");
                ControllerStatus = "Error";
            }
        }

        public void SetWindowHandle(IntPtr handle)
        {
            _controller.SetWindowHandle(handle);
            RefreshControllers();
        }

        [RelayCommand]
        private void RefreshControllers()
        {
            AvailableControllers.Clear();
            foreach (var dev in _controller.EnumerateDevices())
                AvailableControllers.Add(dev);
            AddLog($"Dispositivos encontrados: {AvailableControllers.Count}");
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
            _lastState = state;

            LeftStickX = Math.Round(state.LeftStickX, 2);
            LeftStickY = Math.Round(-state.LeftStickY, 2);
            RightStickX = Math.Round(state.RightStickX, 2);
            RightStickY = Math.Round(-state.RightStickY, 2);
            L2Value = Math.Round(state.L2, 2);
            R2Value = Math.Round(state.R2, 2);
            RawAxesInfo = state.RawDebugInfo;

            for (int i = 0; i < state.Buttons.Length && i < ControllerButtons.Count; i++)
                ControllerButtons[i].Pressed = state.Buttons[i];

            if (state.Buttons.Length > 0 && state.Buttons[0])
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

            LeftMotorSpeed = MotorBias((int)(left * 255));
            RightMotorSpeed = MotorBias((int)(right * 255));
            SendMotorCommand(LeftMotorSpeed, RightMotorSpeed);
        }

        private static int MotorBias(int speed)
        {
            const int min = 55;
            if (speed == 0) return 0;
            return Math.Abs(speed) < min ? (speed > 0 ? min : -min) : speed;
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

        [RelayCommand]
        private void SendCustomCommand()
        {
            if (string.IsNullOrWhiteSpace(CommandText)) return;
            string cmd = CommandText.Trim();
            _roverClient.SendCommand(cmd);
            AddLog($"> {cmd}");
            CommandText = "";
        }

        private void AddLog(string message)
        {
            LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (LogMessages.Count > 200) LogMessages.RemoveAt(0);
        }
    }
}
