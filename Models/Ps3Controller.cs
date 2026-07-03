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
        private bool _handleReady;
        private int _pollCount;

        public event Action<ControllerState>? OnStateChanged;
        public event Action<bool>? OnConnectionChanged;

        public bool IsConnected { get; private set; }

        public void SetWindowHandle(IntPtr handle)
        {
            _windowHandle = handle;
            _handleReady = true;
            Start();
        }

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
            ReleaseJoystick();
            SetConnected(false);
        }

        private void PollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_handleReady)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (_joystick == null || _joystick.IsDisposed)
                    {
                        FindAndAcquireJoystick();
                        if (_joystick == null)
                        {
                            SetConnected(false);
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
                    SetConnected(true);
                    _pollCount++;

                    if (_pollCount % 500 == 0)
                    {
                        try { _joystick.Unacquire(); _joystick.Acquire(); }
                        catch { ReleaseJoystick(); SetConnected(false); Thread.Sleep(200); continue; }
                    }
                }
                catch
                {
                    ReleaseJoystick();
                    SetConnected(false);
                    Thread.Sleep(1000);
                }

                Thread.Sleep(15);
            }
        }

        private void FindAndAcquireJoystick()
        {
            var devices = _directInput.GetDevices();
            DeviceInstance found = default!;
            bool foundDevice = false;

            foreach (var dev in devices)
            {
                string name = dev.ProductName.ToLower();
                string[] keywords = ["ps3", "playstation", "dual", "wireless controller",
                                     "gamepad", "joystick", "xbox", "controller"];
                for (int i = 0; i < keywords.Length; i++)
                {
                    if (name.Contains(keywords[i]))
                    {
                        found = dev;
                        foundDevice = true;
                        break;
                    }
                }
                if (foundDevice)
                {
                    if (name.Contains("ps3") || name.Contains("playstation"))
                        break;
                }
            }

            if (!foundDevice) return;

            var joystick = new Joystick(_directInput, found.InstanceGuid);
            try
            {
                joystick.SetCooperativeLevel(_windowHandle,
                    CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                joystick.Properties.AxisMode = DeviceAxisMode.Absolute;
                joystick.Acquire();
                _joystick = joystick;
                System.Diagnostics.Debug.WriteLine($"Mando: {found.ProductName!}");
            }
            catch
            {
                joystick.Dispose();
            }
        }

        private void SetConnected(bool connected)
        {
            if (IsConnected == connected) return;
            IsConnected = connected;
            OnConnectionChanged?.Invoke(connected);
        }

        private void ReleaseJoystick()
        {
            if (_joystick == null) return;
            try { _joystick.Unacquire(); } catch { }
            _joystick.Dispose();
            _joystick = null;
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
