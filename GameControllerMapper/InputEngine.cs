using SharpDX.XInput;
using WindowsInput;
using WindowsInput.Native;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace GameControllerMapper
{
    class KeyStateInfo
    {
        public bool IsPressed { get; set; }
        public DateTime PressStartTime { get; set; }
        public DateTime LastRepeatTime { get; set; }
    }

    public class InputEngine
    {
        private Controller _controller;
        private InputSimulator _simulator;

        private CancellationTokenSource? _cts;
        private Task? _pollingTask;

        private State _previousState;
        private double _remainderX = 0;
        private double _remainderY = 0;

        private Dictionary<ControllerInputType, KeyStateInfo> _keyStates = new Dictionary<ControllerInputType, KeyStateInfo>();
        private Dictionary<ControllerInputType, bool> _lastButtonStates = new Dictionary<ControllerInputType, bool>();

        private const double RepeatDelay = 0.5;
        private const double RepeatInterval = 0.05;

        public event Action<string, bool>? OnButtonStateChanged;

        private readonly HashSet<VirtualKeyCode> _modifierKeys = new HashSet<VirtualKeyCode>
        {
            VirtualKeyCode.LSHIFT, VirtualKeyCode.RSHIFT,
            VirtualKeyCode.LCONTROL, VirtualKeyCode.RCONTROL,
            VirtualKeyCode.LMENU, VirtualKeyCode.RMENU,
            VirtualKeyCode.LWIN, VirtualKeyCode.RWIN,
            VirtualKeyCode.CAPITAL
        };

        public ProfileConfig? CurrentProfile { get; set; }

        public InputEngine()
        {
            _controller = new Controller(UserIndex.One);
            _simulator = new InputSimulator();

            foreach (ControllerInputType input in Enum.GetValues(typeof(ControllerInputType)))
            {
                _keyStates[input] = new KeyStateInfo();
                _lastButtonStates[input] = false; // 初始化
            }
        }

        public void ToggleActive(bool isActive)
        {
            if (isActive)
            {
                Stop();
                if (_controller.IsConnected) _previousState = _controller.GetState();
                foreach (var key in _keyStates.Keys)
                {
                    _keyStates[key].IsPressed = false;
                    _keyStates[key].PressStartTime = DateTime.MinValue;
                }
                ResetAccumulators();
                _cts = new CancellationTokenSource();
                _pollingTask = Task.Run(() => InputLoop(_cts.Token));
            }
            else
            {
                Stop();
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _pollingTask?.Wait(100); } catch { }
            _cts = null;
            _pollingTask = null;
            ResetAccumulators();
        }

        private void ResetAccumulators()
        {
            _remainderX = 0; _remainderY = 0;
        }

        private void InputLoop(CancellationToken token)
        {
            int frameDelay = 8;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_controller.IsConnected)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    var currentState = _controller.GetState();
                    if (CurrentProfile != null) ProcessMappings(currentState);
                    _previousState = currentState;
                }
                catch { }
                Thread.Sleep(frameDelay);
            }
        }

        private void ProcessMappings(State current)
        {
            var mappingsSnapshot = CurrentProfile?.Mappings.ToList();
            if (mappingsSnapshot == null) return;

            foreach (var map in mappingsSnapshot)
            {
                bool isDown = false;

                // 1. 先判斷當前按鍵是否被按下
                switch (map.ControllerInput)
                {
                    case ControllerInputType.StickLeftMove:
                        // 搖桿移動判斷 (這裡簡化處理，有推動就算按下)
                        short lx = current.Gamepad.LeftThumbX;
                        short ly = current.Gamepad.LeftThumbY;
                        isDown = (lx * lx + ly * ly) > (6000 * 6000);
                        HandleStickMove(lx, ly, map);
                        break;
                    case ControllerInputType.StickRightMove:
                        short rx = current.Gamepad.RightThumbX;
                        short ry = current.Gamepad.RightThumbY;
                        isDown = (rx * rx + ry * ry) > (6000 * 6000);
                        HandleStickMove(rx, ry, map);
                        break;
                    case ControllerInputType.TriggerLeft:
                        isDown = current.Gamepad.LeftTrigger > 30;
                        HandleButtonWithRepeat(isDown, map);
                        break;
                    case ControllerInputType.TriggerRight:
                        isDown = current.Gamepad.RightTrigger > 30;
                        HandleButtonWithRepeat(isDown, map);
                        break;
                    case ControllerInputType.ButtonA: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.A); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.ButtonB: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.B); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.ButtonX: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.X); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.ButtonY: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.ShoulderLeft: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.ShoulderRight: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.DPadUp: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.DPadDown: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.DPadLeft: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.DPadRight: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.Start: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Start); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.Back: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.StickLeftClick: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb); HandleButtonWithRepeat(isDown, map); break;
                    case ControllerInputType.StickRightClick: isDown = current.Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb); HandleButtonWithRepeat(isDown, map); break;
                }

                if (_lastButtonStates.ContainsKey(map.ControllerInput) && _lastButtonStates[map.ControllerInput] != isDown)
                {
                    _lastButtonStates[map.ControllerInput] = isDown;
                    OnButtonStateChanged?.Invoke(map.ControllerInput.ToString(), isDown);
                }
            }
        }

        private void HandleButtonWithRepeat(bool isDownNow, KeyMapping map)
        {
            if (!_keyStates.ContainsKey(map.ControllerInput)) return;
            var state = _keyStates[map.ControllerInput];

            if (isDownNow && !state.IsPressed)
            {
                state.IsPressed = true;
                state.PressStartTime = DateTime.Now;
                state.LastRepeatTime = DateTime.Now;
                SimulateDown(map);
            }
            else if (isDownNow && state.IsPressed)
            {
                // 組合鍵不執行連發，避免卡鍵
                if (map.TargetType == TargetDeviceType.Combo || map.TargetType == TargetDeviceType.CustomCombo) 
                    return;

                if (map.TargetType == TargetDeviceType.Keyboard && !_modifierKeys.Contains(map.KeyCode))
                {
                    double duration = (DateTime.Now - state.PressStartTime).TotalSeconds;
                    if (duration > RepeatDelay)
                    {
                        if ((DateTime.Now - state.LastRepeatTime).TotalSeconds > RepeatInterval)
                        {
                            _simulator.Keyboard.KeyDown(map.KeyCode);
                            state.LastRepeatTime = DateTime.Now;
                        }
                    }
                }
                else if (map.TargetType == TargetDeviceType.MouseScrollUp || map.TargetType == TargetDeviceType.MouseScrollDown)
                {
                    double duration = (DateTime.Now - state.PressStartTime).TotalSeconds;
                    if (duration > 0.3)
                    {
                        if ((DateTime.Now - state.LastRepeatTime).TotalSeconds > 0.1)
                        {
                            SimulateDown(map);
                            state.LastRepeatTime = DateTime.Now;
                        }
                    }
                }
            }
            else if (!isDownNow && state.IsPressed)
            {
                state.IsPressed = false;
                SimulateUp(map);
            }
        }

        private void SimulateDown(KeyMapping map)
        {
            try
            {
                if (map.TargetType == TargetDeviceType.Keyboard)
                {
                    _simulator.Keyboard.KeyDown(map.KeyCode);
                }
                else if (map.TargetType == TargetDeviceType.MouseClick)
                {
                    if (map.MouseButton == "Left") _simulator.Mouse.LeftButtonDown();
                    if (map.MouseButton == "Right") _simulator.Mouse.RightButtonDown();
                    if (map.MouseButton == "Middle") _simulator.Mouse.MiddleButtonDown();
                }
                else if (map.TargetType == TargetDeviceType.MouseScrollUp) _simulator.Mouse.VerticalScroll(1);
                else if (map.TargetType == TargetDeviceType.MouseScrollDown) _simulator.Mouse.VerticalScroll(-1);

                else if (map.TargetType == TargetDeviceType.Combo)
                {
                    int id = (int)map.KeyCode;
                    if (KeyRepository.ComboDefinitions.ContainsKey(id))
                    {
                        var keys = KeyRepository.ComboDefinitions[id];
                        // 組合鍵邏輯：按住所有修飾鍵 -> 點擊主鍵 -> 放開所有
                        if (keys.Length > 1)
                        {
                            var modifiers = keys.Take(keys.Length - 1);
                            var mainKey = keys.Last();
                            _simulator.Keyboard.ModifiedKeyStroke(modifiers, mainKey);
                        }
                    }
                }
                else if (map.TargetType == TargetDeviceType.CustomCombo)
                {
                    if (map.CustomComboKeys != null && map.CustomComboKeys.Count > 0)
                    {
                        if (map.CustomComboKeys.Count > 1)
                        {
                            var modifiers = map.CustomComboKeys.Take(map.CustomComboKeys.Count - 1);
                            var mainKey = map.CustomComboKeys.Last();
                            _simulator.Keyboard.ModifiedKeyStroke(modifiers, mainKey);
                        }
                        else
                        {
                            _simulator.Keyboard.KeyPress(map.CustomComboKeys[0]);
                        }
                    }
                }
            }
            catch { }
        }

        private void SimulateUp(KeyMapping map)
        {
            try
            {
                if (map.TargetType == TargetDeviceType.Keyboard)
                {
                    _simulator.Keyboard.KeyUp(map.KeyCode);
                }
                else if (map.TargetType == TargetDeviceType.MouseClick)
                {
                    if (map.MouseButton == "Left") _simulator.Mouse.LeftButtonUp();
                    if (map.MouseButton == "Right") _simulator.Mouse.RightButtonUp();
                    if (map.MouseButton == "Middle") _simulator.Mouse.MiddleButtonUp();
                }
            }
            catch { }
        }

        private void HandleStickMove(short rawX, short rawY, KeyMapping map)
        {
            if (map.TargetType != TargetDeviceType.MouseMove) return;
            double deadzone = 6000.0;
            double magnitude = Math.Sqrt(rawX * rawX + rawY * rawY);
            if (magnitude < deadzone) { ResetAccumulators(); return; }

            try
            {
                double normalizedMagnitude = (magnitude - deadzone) / (32767 - deadzone);
                if (normalizedMagnitude > 1.0) normalizedMagnitude = 1.0;
                double directionX = rawX / magnitude;
                double directionY = rawY / magnitude;
                double targetSpeed = normalizedMagnitude * map.Sensitivity;
                double exactMoveX = directionX * targetSpeed;
                double exactMoveY = directionY * targetSpeed * -1;

                if (double.IsNaN(exactMoveX) || double.IsInfinity(exactMoveX) || double.IsNaN(exactMoveY) || double.IsInfinity(exactMoveY)) { ResetAccumulators(); return; }

                double totalMoveX = exactMoveX + _remainderX;
                double totalMoveY = exactMoveY + _remainderY;
                int finalMoveX = (int)totalMoveX;
                int finalMoveY = (int)totalMoveY;
                _remainderX = totalMoveX - finalMoveX;
                _remainderY = totalMoveY - finalMoveY;

                if (_remainderX > 1.0) _remainderX = 1.0; else if (_remainderX < -1.0) _remainderX = -1.0;
                if (_remainderY > 1.0) _remainderY = 1.0; else if (_remainderY < -1.0) _remainderY = -1.0;

                if (finalMoveX != 0 || finalMoveY != 0) _simulator.Mouse.MoveMouseBy(finalMoveX, finalMoveY);
            }
            catch { ResetAccumulators(); }
        }
    }
}