using System;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.DirectInput;

namespace RoverExplorer1NodoMandoPC.Models
{
    public class ControllerState
    {
        public double LeftStickX, LeftStickY;
        public double RightStickX, RightStickY;
        public double L2, R2;
        public bool[] Buttons = [];
    }

    public class Ps3Controller : IDisposable
    {
        private readonly DirectInput _directInput = new();
        private Joystick? _joystick;
        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private IntPtr _windowHandle;

        public event Action<ControllerState>? OnStateChanged;
        public event Action<bool>? OnConnectionChanged;

        public bool IsConnected { get; private set; }

        public void SetWindowHandle(IntPtr handle) => _windowHandle = handle;

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _pollTask?.Wait(1000); } catch { }
            _joystick?.Unacquire();
            _joystick?.Dispose();
            _joystick = null;
            IsConnected = false;
        }

        private void PollLoop(CancellationToken token)
        {
            int failCount = 0;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_joystick == null || _joystick.IsDisposed)
                    {
                        FindAndAcquireJoystick();
                        if (_joystick == null)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }
                    }

                    _joystick.Poll();
                    var state = _joystick.GetCurrentState();

                    var cs = new ControllerState
                    {
                        LeftStickX = NormalizeAxis(state.X),
                        LeftStickY = NormalizeAxis(state.Y),
                        RightStickX = NormalizeAxis(state.Z),
                        RightStickY = NormalizeAxis(state.RotationZ),
                        L2 = state.Sliders.Length > 0 ? NormalizeAxis(state.Sliders[0]) : 0,
                        R2 = state.Sliders.Length > 1 ? NormalizeAxis(state.Sliders[1]) : 0,
                        Buttons = (bool[])state.Buttons.Clone()
                    };

                    OnStateChanged?.Invoke(cs);

                    if (!IsConnected)
                    {
                        IsConnected = true;
                        OnConnectionChanged?.Invoke(true);
                    }
                    failCount = 0;
                }
                catch
                {
                    failCount++;
                    if (IsConnected)
                    {
                        IsConnected = false;
                        OnConnectionChanged?.Invoke(false);
                    }
                    _joystick?.Dispose();
                    _joystick = null;
                    Thread.Sleep(failCount > 5 ? 2000 : 100);
                }

                Thread.Sleep(15);
            }
        }

        private void FindAndAcquireJoystick()
        {
            var devices = _directInput.GetDevices();
            foreach (var dev in devices)
            {
                string name = dev.ProductName.ToLower();
                if (name.Contains("ps3") || name.Contains("playstation") ||
                    name.Contains("dual") || name.Contains("wireless controller"))
                {
                    _joystick = new Joystick(_directInput, dev.InstanceGuid);
                    _joystick.SetCooperativeLevel(_windowHandle, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                    _joystick.Properties.AxisMode = DeviceAxisMode.Absolute;
                    _joystick.Acquire();
                    System.Diagnostics.Debug.WriteLine($"Mando PS3 encontrado: {dev.ProductName}");
                    return;
                }
            }
            foreach (var dev in devices)
            {
                _joystick = new Joystick(_directInput, dev.InstanceGuid);
                _joystick.SetCooperativeLevel(_windowHandle, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                _joystick.Properties.AxisMode = DeviceAxisMode.Absolute;
                _joystick.Acquire();
                System.Diagnostics.Debug.WriteLine($"Mando alternativo: {dev.ProductName}");
                return;
            }
        }

        private static double NormalizeAxis(int value)
        {
            const double deadZone = 0.15;
            double normalized = (value - 32767.0) / 32767.0;
            if (normalized > 1.0) normalized = 1.0;
            if (normalized < -1.0) normalized = -1.0;
            if (Math.Abs(normalized) < deadZone) return 0.0;
            return (normalized - Math.Sign(normalized) * deadZone) / (1.0 - deadZone);
        }

        public void Dispose()
        {
            Stop();
            _directInput.Dispose();
        }
    }
}
