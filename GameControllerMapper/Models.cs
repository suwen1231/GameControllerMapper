using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using WindowsInput.Native;

namespace GameControllerMapper
{
    public enum ControllerType { Xbox, PS4, Switch }

    public enum ControllerInputType
    {
        ButtonA, ButtonB, ButtonX, ButtonY,
        DPadUp, DPadDown, DPadLeft, DPadRight,
        ShoulderLeft, ShoulderRight,
        TriggerLeft, TriggerRight,
        Start, Back,
        StickLeftClick, StickLeftMove,
        StickRightClick, StickRightMove
    }

    public enum TargetDeviceType
    {
        None, Keyboard, MouseClick, MouseMove, MouseScrollUp, MouseScrollDown,
        Combo,
        CustomCombo // 【新增】自定義組合鍵
    }

    public class KeyMapping : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private ControllerInputType _controllerInput;
        public ControllerInputType ControllerInput
        {
            get => _controllerInput;
            set => SetProperty(ref _controllerInput, value);
        }

        private TargetDeviceType _targetType;
        public TargetDeviceType TargetType
        {
            get => _targetType;
            set
            {
                if (SetProperty(ref _targetType, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(IsStickMove));
                }
            }
        }

        private VirtualKeyCode _keyCode;
        public VirtualKeyCode KeyCode
        {
            get => _keyCode;
            set
            {
                if (SetProperty(ref _keyCode, value))
                {
                    if (value == VirtualKeyCode.NONAME)
                    {
                        if (TargetType == TargetDeviceType.Keyboard || TargetType == TargetDeviceType.Combo)
                            TargetType = TargetDeviceType.None;
                    }
                    else
                    {
                        if ((int)value >= 2000)
                            TargetType = TargetDeviceType.Combo;
                        else
                            TargetType = TargetDeviceType.Keyboard;
                    }

                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        private string _mouseButton = string.Empty;
        public string MouseButton
        {
            get => _mouseButton;
            set
            {
                if (SetProperty(ref _mouseButton, value)) OnPropertyChanged(nameof(DisplayName));
            }
        }

        private double _sensitivity = 5.0;
        public double Sensitivity
        {
            get => _sensitivity;
            set
            {
                if (SetProperty(ref _sensitivity, value)) OnPropertyChanged(nameof(DisplayName));
            }
        }

        // 【新增】存放自定義組合鍵的清單
        public System.Collections.Generic.List<VirtualKeyCode> CustomComboKeys { get; set; } = new System.Collections.Generic.List<VirtualKeyCode>();

        public void UpdateCustomCombo()
        {
            OnPropertyChanged(nameof(DisplayName));
        }

        [JsonIgnore]
        public bool IsStickMove =>
            ControllerInput == ControllerInputType.StickLeftMove ||
            ControllerInput == ControllerInputType.StickRightMove;

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                switch (TargetType)
                {
                    case TargetDeviceType.None: return "---";
                    case TargetDeviceType.MouseMove: return $"滑鼠移動 ({Sensitivity:F1})";

                    case TargetDeviceType.Keyboard:
                        // 順便把單鍵的 VK_ 去除，讓畫面更好看
                        return KeyCode.ToString().Replace("VK_", "");

                    case TargetDeviceType.Combo:
                        string fullName = KeyRepository.GetComboName(KeyCode);
                        if (fullName.EndsWith("(自訂)")) return fullName.Replace(" (自訂)", "");

                        // 預設組合鍵：只顯示括號內的文字，例如 "複製"
                        int start = fullName.IndexOf('(');
                        int end = fullName.LastIndexOf(')');
                        if (start != -1 && end > start) return fullName.Substring(start + 1, end - start - 1);
                        return fullName;

                    case TargetDeviceType.MouseClick: return $"滑鼠 {MouseButton} 鍵";
                    case TargetDeviceType.MouseScrollUp: return "滑鼠滾輪 (上)";
                    case TargetDeviceType.MouseScrollDown: return "滑鼠滾輪 (下)";
                    case TargetDeviceType.CustomCombo:
                        if (CustomComboKeys == null || CustomComboKeys.Count == 0) return "未設定";

                        // 將 List 中的按鍵轉換為乾淨的字串 (例如將 LCONTROL 轉為 Ctrl)
                        var names = System.Linq.Enumerable.Select(CustomComboKeys, k => {
                            string n = k.ToString().Replace("VK_", "");
                            n = n.Replace("LCONTROL", "Ctrl").Replace("RCONTROL", "Ctrl").Replace("CONTROL", "Ctrl");
                            n = n.Replace("LSHIFT", "Shift").Replace("RSHIFT", "Shift").Replace("SHIFT", "Shift");
                            n = n.Replace("LMENU", "Alt").Replace("RMENU", "Alt").Replace("MENU", "Alt");
                            n = n.Replace("LWIN", "Win").Replace("RWIN", "Win");
                            return n;
                        });
                        // 用 " + " 把按鍵串起來，例如 "Ctrl + Shift + S"
                        return string.Join(" + ", names);

                    default: return "未設定";
                }
            }
        }
    }

    public class ProfileConfig
    {
        public ObservableCollection<KeyMapping> Mappings { get; set; } = new ObservableCollection<KeyMapping>();
    }
}