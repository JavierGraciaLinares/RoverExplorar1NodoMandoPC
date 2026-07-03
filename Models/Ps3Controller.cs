using System;
using System.Collections.Generic;
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
        private bool _manualSelection;
        private Guid? _selectedGuid;

        public event Action<ControllerState>? OnStateChanged;
        public event Action<bool>? OnConnectionChanged;

        public bool IsConnected { get; private set; }
        public string? CurrentDeviceName { get; private set; }

        public void SetWindowHandle(IntPtr handle)
        {
            _windowHandle = handle;
            _handleReady = true;
            if (!_manualSelection) Start();
        }

        public List<ControllerDeviceInfo> EnumerateDevices()
        {
            var list = new List<ControllerDeviceInfo>();
            foreach (var dev in _directInput.GetDevices())
            {
                list.Add(new ControllerDeviceInfo
                {
                    Name = dev.ProductName,
                    InstanceGuid = dev.InstanceGuid
                });
            }
            return list;
        }

        public bool TrySelectDevice(Guid guid)
        {
            Stop();
            _manualSelection = true;
            _selectedGuid = guid;

            if (!_handleReady) return true;

            return TrySelectDeviceInternal(guid);
        }

        private bool TrySelectDeviceInternal(Guid guid)
        {
            try
            {
                ReleaseJoystick();
                var joystick = new Joystick(_directInput, guid);
                joystick.SetCooperativeLevel(_windowHandle,
                    CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                joystick.Properties.AxisMode = DeviceAxisMode.Absolute;
                joystick.Acquire();
                _joystick = joystick;
                CurrentDeviceName = joystick.Properties.ProductName;
                System.Diagnostics.Debug.WriteLine($"Mando: {CurrentDeviceName}");
                return true;
            }
            catch
            {
                _joystick?.Dispose();
                _joystick = null;
                return false;
            }
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
            _cts = null;
            _pollTask = null;
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
                        if (_manualSelection && _selectedGuid.HasValue)
                            TrySelectDeviceInternal(_selectedGuid.Value);
                        else
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
            foreach (var dev in _directInput.GetDevices())
            {
                string name = dev.ProductName.ToLower();
                string[] keywords = ["ps3", "playstation", "dual", "wireless controller",
                                     "gamepad", "joystick", "xbox", "controller"];
                bool match = false;
                for (int i = 0; i < keywords.Length; i++)
                    if (name.Contains(keywords[i])) { match = true; break; }

                if (!match) continue;

                try
                {
                    var joystick = new Joystick(_directInput, dev.InstanceGuid);
                    joystick.SetCooperativeLevel(_windowHandle,
                        CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                    joystick.Properties.AxisMode = DeviceAxisMode.Absolute;
                    joystick.Acquire();
                    _joystick = joystick;
                    CurrentDeviceName = dev.ProductName;
                    System.Diagnostics.Debug.WriteLine($"Mando: {dev.ProductName}");
                    return;
                }
                catch { continue; }
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
