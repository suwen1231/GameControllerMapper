using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text; // StringBuilder
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using WindowsInput.Native;

namespace GameControllerMapper
{
    public class PressedStateCollection : INotifyPropertyChanged
    {
        private Dictionary<string, bool> _states = new Dictionary<string, bool>();
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool this[string name]
        {
            get => _states.ContainsKey(name) ? _states[name] : false;
            set
            {
                if (!_states.ContainsKey(name) || _states[name] != value)
                {
                    _states[name] = value;
                    // 【建議修正】改用 "Item[]" 是 WPF 索引器通知的標準寫法
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                }
            }
        }
    }

    public partial class MainViewModel : ObservableObject
    {
        // ==================================================
        // ==================================================
        [ObservableProperty] private bool _isDevMode = false;
        // ==================================================
        // ==================================================

        private InputEngine? _engine;
        private const string ProfileFileName = "profile.xmap";

        [ObservableProperty] private bool _isDriverInstalled;
        [ObservableProperty] private int _selectedTabIndex;
        [ObservableProperty] private ProfileConfig _config = new ProfileConfig();
        [ObservableProperty] private KeyMapping? _selectedMapping;
        [ObservableProperty] private bool _isRemappingActive;
        //[ObservableProperty] private bool _isRecordingMode;
        [ObservableProperty] private string _statusMessage = "等待操作...";
        [ObservableProperty] private ControllerType _selectedController = ControllerType.Xbox;

        public bool ShowTutorialTab => !IsDriverInstalled || _isDevMode;
        public ObservableCollection<KeyOption> AllKeyOptions => KeyRepository.AllKeys;
        public IEnumerable<KeyOption> BasicKeyOptions => KeyRepository.AllKeys.Where(k => !k.Category.Contains("組合鍵"));

        // ------------------ 組合鍵 UI 相關 ------------------
        [ObservableProperty] private bool _isComboCreatorOpen;
        [ObservableProperty] private VirtualKeyCode _comboKey1 = VirtualKeyCode.NONAME;
        [ObservableProperty] private VirtualKeyCode _comboKey2 = VirtualKeyCode.NONAME;
        [ObservableProperty] private VirtualKeyCode _comboKey3 = VirtualKeyCode.NONAME;
        [ObservableProperty] private VirtualKeyCode _comboKey4 = VirtualKeyCode.NONAME;
        [ObservableProperty] private VirtualKeyCode _comboKey5 = VirtualKeyCode.NONAME;

        // ------------------ 組合鍵錄製相關 ------------------
        [ObservableProperty] private string _newComboName = "";
        [ObservableProperty] private string _newComboPreview = "請輸入按鍵...";
        // 暫存錄製到的按鍵
        private List<VirtualKeyCode> _recordedComboKeys = new List<VirtualKeyCode>();

        public List<ControllerType> ControllerTypes => Enum.GetValues(typeof(ControllerType)).Cast<ControllerType>().ToList();
        public PressedStateCollection Pressed { get; } = new PressedStateCollection();

        public MainViewModel()
        {
            InitializeDefaultMappings();
            CheckDependencies();
            LoadDefaultProfile();

            // 觸發屬性更新通知
            OnPropertyChanged(nameof(ShowTutorialTab));
        }

        public void Cleanup()
        {
            if (_engine != null) _engine.Stop();
        }

        private void InitializeDefaultMappings()
        {
            var freshMappings = new ObservableCollection<KeyMapping>();

            foreach (ControllerInputType input in Enum.GetValues(typeof(ControllerInputType)))
            {
                var mapping = new KeyMapping
                {
                    Id = Guid.NewGuid(),
                    ControllerInput = input,
                    TargetType = TargetDeviceType.None,
                    KeyCode = VirtualKeyCode.NONAME,
                    Sensitivity = 5.0
                };

                // 自動生成的預設值代碼
                if (input == ControllerInputType.ButtonA)
                {
                    mapping.TargetType = TargetDeviceType.MouseClick;
                    mapping.MouseButton = "Left";
                }
                if (input == ControllerInputType.ButtonB)
                {
                    mapping.TargetType = TargetDeviceType.MouseClick;
                    mapping.MouseButton = "Right";
                }
                if (input == ControllerInputType.ButtonX)
                {
                    mapping.TargetType = TargetDeviceType.Keyboard;
                    mapping.KeyCode = (VirtualKeyCode)27;
                }
                if (input == ControllerInputType.ButtonY)
                {
                    mapping.TargetType = TargetDeviceType.Keyboard;
                    mapping.KeyCode = (VirtualKeyCode)13;
                }
                if (input == ControllerInputType.DPadUp)
                {
                    mapping.TargetType = TargetDeviceType.Keyboard;
                    mapping.KeyCode = (VirtualKeyCode)160;
                }
                if (input == ControllerInputType.DPadDown)
                {
                    mapping.TargetType = TargetDeviceType.Combo;
                    mapping.KeyCode = (VirtualKeyCode)2008;
                }
                if (input == ControllerInputType.DPadLeft)
                {
                    mapping.TargetType = TargetDeviceType.Keyboard;
                    mapping.KeyCode = (VirtualKeyCode)162;
                }
                if (input == ControllerInputType.DPadRight)
                {
                    mapping.TargetType = TargetDeviceType.Keyboard;
                    mapping.KeyCode = (VirtualKeyCode)164;
                }
                if (input == ControllerInputType.ShoulderLeft)
                {
                    mapping.TargetType = TargetDeviceType.Combo;
                    mapping.KeyCode = (VirtualKeyCode)2001;
                }
                if (input == ControllerInputType.ShoulderRight)
                {
                    mapping.TargetType = TargetDeviceType.Combo;
                    mapping.KeyCode = (VirtualKeyCode)2002;
                }
                if (input == ControllerInputType.TriggerLeft)
                {
                    mapping.TargetType = TargetDeviceType.MouseScrollUp;
                }
                if (input == ControllerInputType.TriggerRight)
                {
                    mapping.TargetType = TargetDeviceType.MouseScrollDown;
                }
                if (input == ControllerInputType.Start)
                {
                    mapping.TargetType = TargetDeviceType.Keyboard;
                    mapping.KeyCode = (VirtualKeyCode)46;
                }
                if (input == ControllerInputType.Back)
                {
                    mapping.TargetType = TargetDeviceType.Keyboard;
                    mapping.KeyCode = (VirtualKeyCode)8;
                }
                if (input == ControllerInputType.StickLeftClick)
                {
                    mapping.TargetType = TargetDeviceType.Combo;
                    mapping.KeyCode = (VirtualKeyCode)2005;
                }
                if (input == ControllerInputType.StickLeftMove)
                {
                    mapping.TargetType = TargetDeviceType.MouseMove;
                    mapping.Sensitivity = 3.0;
                }
                if (input == ControllerInputType.StickRightClick)
                {
                    mapping.TargetType = TargetDeviceType.MouseClick;
                    mapping.MouseButton = "Middle";
                }
                if (input == ControllerInputType.StickRightMove)
                {
                    mapping.TargetType = TargetDeviceType.MouseMove;
                    mapping.Sensitivity = 25.0;
                }
                // =======================================================

                freshMappings.Add(mapping);
            }

            _config.Mappings = freshMappings;
            OnPropertyChanged(nameof(Config));
            SelectedMapping = null; //_config.Mappings.FirstOrDefault();
        }

        // 【新增】預設按鈕文字字典 (當映射未啟動時顯示)
        private readonly Dictionary<string, string> _defaultButtonLabels = new Dictionary<string, string>
        {
            { "ButtonA", "A" }, { "ButtonB", "B" }, { "ButtonX", "X" }, { "ButtonY", "Y" },
            { "DPadUp", "▲" }, { "DPadDown", "▼" }, { "DPadLeft", "◀" }, { "DPadRight", "▶" },
            { "ShoulderLeft", "LB" }, { "ShoulderRight", "RB" }, { "TriggerLeft", "LT" }, { "TriggerRight", "RT" },
            { "StickLeftClick", "L3" }, { "StickRightClick", "R3" },
            { "StickLeftMove", "L-Move" }, { "StickRightMove", "R-Move" },
            { "Back", "Back" }, { "Start", "Start" } // 依照不同手把這可能要微調，但這裡用通用名稱
        };

        // 索引器：介面透過這個來拿文字 -> {Binding [ButtonA]}
        public string this[string inputName]
        {
            get
            {
                if (!IsRemappingActive)
                {
                    if (SelectedController == ControllerType.Switch)
                    {
                        if (inputName == "ButtonA") return "A"; if (inputName == "ButtonB") return "B";
                        if (inputName == "ButtonX") return "X"; if (inputName == "ButtonY") return "Y";
                        if (inputName == "Back") return "-"; if (inputName == "Start") return "+";
                        if (inputName == "TriggerLeft") return "ZL"; if (inputName == "TriggerRight") return "ZR";
                        if (inputName == "ShoulderLeft") return "L"; if (inputName == "ShoulderRight") return "R";
                    }
                    else if (SelectedController == ControllerType.PS4)
                    {
                        if (inputName == "ButtonA") return "×"; if (inputName == "ButtonB") return "○";
                        if (inputName == "ButtonX") return "□"; if (inputName == "ButtonY") return "△";
                        if (inputName == "Back") return "Share"; if (inputName == "Start") return "Opt";
                        if (inputName == "TriggerLeft") return "L2"; if (inputName == "TriggerRight") return "R2";
                        if (inputName == "ShoulderLeft") return "L1"; if (inputName == "ShoulderRight") return "R1";
                    }
                    return _defaultButtonLabels.ContainsKey(inputName) ? _defaultButtonLabels[inputName] : inputName;
                }
                else
                {
                    if (Enum.TryParse(typeof(ControllerInputType), inputName, out var type))
                    {
                        var mapping = Config.Mappings.FirstOrDefault(m => m.ControllerInput == (ControllerInputType)type);
                        if (mapping != null)
                        {
                            if (mapping.TargetType == TargetDeviceType.None) return "";

                            if (mapping.TargetType == TargetDeviceType.Keyboard)
                            {
                                string key = mapping.KeyCode.ToString().Replace("VK_", "");
                                key = key.Replace("LCONTROL", "Ctrl(L)").Replace("RCONTROL", "Ctrl(R)").Replace("CONTROL", "Ctrl");
                                key = key.Replace("LSHIFT", "Shift(L)").Replace("RSHIFT", "Shift(R)").Replace("SHIFT", "Shift");
                                key = key.Replace("LMENU", "Alt(L)").Replace("RMENU", "Alt(R)").Replace("MENU", "Alt");
                                key = key.Replace("LWIN", "Win").Replace("RWIN", "Win");
                                key = key.Replace("RETURN", "Enter").Replace("ESCAPE", "Esc").Replace("BACK", "Backspace");
                                key = key.Replace("DELETE", "Del").Replace("INSERT", "Ins").Replace("HOME", "Home").Replace("END", "End");
                                key = key.Replace("PRIOR", "PgUp").Replace("NEXT", "PgDn").Replace("SNAPSHOT", "PrtSc");
                                key = key.Replace("NUMPAD", "Num");
                                key = key.Replace("OEM_MINUS", "-").Replace("OEM_PLUS", "+");
                                return key;
                            }

                            if (mapping.TargetType == TargetDeviceType.CustomCombo) return mapping.DisplayName;

                            if (mapping.TargetType == TargetDeviceType.Combo)
                            {
                                string fullName = KeyRepository.GetComboName(mapping.KeyCode);

                                // 如果是我們自己建立的自訂組合鍵，直接拔掉「 (自訂)」字樣，顯示按鍵本身即可
                                if (fullName.EndsWith("(自訂)"))
                                {
                                    return fullName.Replace(" (自訂)", "");
                                }

                                // 系統預設的 Combo (例如 "Ctrl+C (複製)") 則保留原本邏輯，只顯示括號內的中文 "複製"
                                int start = fullName.IndexOf('(');
                                int end = fullName.LastIndexOf(')');
                                if (start != -1 && end > start) return fullName.Substring(start + 1, end - start - 1);

                                return fullName;
                            }

                            if (mapping.TargetType == TargetDeviceType.MouseClick)
                            {
                                if (mapping.MouseButton == "Left") return "左鍵";
                                if (mapping.MouseButton == "Right") return "右鍵";
                                if (mapping.MouseButton == "Middle") return "中鍵";
                            }

                            if (mapping.TargetType == TargetDeviceType.MouseMove) return $"滑鼠移動 ({mapping.Sensitivity:F1})";
                            if (mapping.TargetType == TargetDeviceType.MouseMove) return "滑鼠移動";
                            if (mapping.TargetType == TargetDeviceType.MouseScrollUp) return "滾輪(上)";
                            if (mapping.TargetType == TargetDeviceType.MouseScrollDown) return "滾輪(下)";
                        }
                    }
                    return "";
                }
            }
        }

        public void CheckDependencies()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string xinputDll = Path.Combine(basePath, "SharpDX.XInput.dll");
            string inputSimDll = Path.Combine(basePath, "WindowsInput.dll");

            if (File.Exists(xinputDll) && File.Exists(inputSimDll))
            {
                try
                {
                    if (_engine == null) _engine = new InputEngine { CurrentProfile = Config };
                    IsDriverInstalled = true;
                    StatusMessage = "系統就緒";

                    // 【修正】確保啟動時停在「手把設定」(Index 1)
                    // 注意：Index 0 是教學頁，Index 1 是手把設定，Index 2 是斗內
                    SelectedTabIndex = 1;
                }
                catch (Exception ex) { StatusMessage = $"初始化失敗: {ex.Message}"; }
            }
            else
            {
                IsDriverInstalled = false;
                SelectedTabIndex = 0; // 缺檔時強制跳教學
                StatusMessage = "缺少必要元件";
            }
            OnPropertyChanged(nameof(ShowTutorialTab));
        }

        [RelayCommand]
        public void SelectMapping(string inputName)
        {
            if (Enum.TryParse(typeof(ControllerInputType), inputName, out var result))
            {
                var type = (ControllerInputType)result;
                SelectedMapping = Config.Mappings.FirstOrDefault(m => m.ControllerInput == type);
            }
        }

        [RelayCommand]
        public void SetMouseFunction(string functionName)
        {
            if (SelectedMapping == null || SelectedMapping.IsStickMove) return;

            switch (functionName)
            {
                case "Left": SelectedMapping.TargetType = TargetDeviceType.MouseClick; SelectedMapping.MouseButton = "Left"; break;
                case "Right": SelectedMapping.TargetType = TargetDeviceType.MouseClick; SelectedMapping.MouseButton = "Right"; break;
                case "Middle": SelectedMapping.TargetType = TargetDeviceType.MouseClick; SelectedMapping.MouseButton = "Middle"; break;
                case "ScrollUp": SelectedMapping.TargetType = TargetDeviceType.MouseScrollUp; break;
                case "ScrollDown": SelectedMapping.TargetType = TargetDeviceType.MouseScrollDown; break;
                case "None": SelectedMapping.TargetType = TargetDeviceType.None; SelectedMapping.KeyCode = VirtualKeyCode.NONAME; break;
            }
            OnPropertyChanged(nameof(SelectedMapping));
            StatusMessage = $"已設定功能: {functionName}";
        }

        [RelayCommand]
        public void CopyInstallCommand()
        {
            try { Clipboard.SetDataObject("Install-Package SharpDX.XInput; Install-Package InputSimulatorPlus"); MessageBox.Show("指令已複製！"); } catch { }
        }

        [RelayCommand]
        public void OpenProfileFolder()
        {
            try { Process.Start(new ProcessStartInfo() { FileName = AppDomain.CurrentDomain.BaseDirectory, UseShellExecute = true, Verb = "open" }); } catch { }
        }

        [RelayCommand]
        public void ResetProfile()
        {
            if (MessageBox.Show("確定要還原預設值？\n這將會清除當前所有設定。", "還原", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                InitializeDefaultMappings();
                if (_engine != null) _engine.CurrentProfile = Config;
                StatusMessage = "已還原預設值";
            }
        }

        [RelayCommand]
        public void RecheckStatus()
        {
            CheckDependencies();
            if (IsDriverInstalled) MessageBox.Show("檢測成功！");
        }

        [RelayCommand]
        public void ToggleService()
        {
            if (_engine == null) return;
            IsRemappingActive = !IsRemappingActive;

            // 訂閱或取消訂閱事件
            if (IsRemappingActive)
            {
                _engine.OnButtonStateChanged += Engine_OnButtonStateChanged;
            }
            else
            {
                _engine.OnButtonStateChanged -= Engine_OnButtonStateChanged;
            }

            _engine.ToggleActive(IsRemappingActive);
            StatusMessage = IsRemappingActive ? "🔴 映射功能運作中 (輸入將被轉換)" : "⚪ 功能已停用";
            OnPropertyChanged(System.Windows.Data.Binding.IndexerName);
        }

        private void Engine_OnButtonStateChanged(string inputName, bool isPressed)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Pressed[inputName] = isPressed;

                // 【新增】當按下按鍵，且映射功能啟動時，更新狀態列文字
                if (isPressed && IsRemappingActive)
                {
                    string mappedFunc = this[inputName]; // 取得目前綁定的功能名稱
                    if (!string.IsNullOrEmpty(mappedFunc))
                    {
                        StatusMessage = $"🎮 觸發: {inputName} ➡ {mappedFunc}";
                    }
                    else
                    {
                        StatusMessage = $"🎮 觸發: {inputName} (無功能)";
                    }
                }
            });
        }

        // 同樣的，在切換手把類型時也要更新文字 (因為 Switch/PS4 符號不同)
        partial void OnSelectedControllerChanged(ControllerType value)
        {
            OnPropertyChanged(System.Windows.Data.Binding.IndexerName);
        }

        //[RelayCommand]
        //public void StartRecording()
        //{
        //    if (SelectedMapping == null || SelectedMapping.IsStickMove) return;
        //    IsRecordingMode = true;
        //    StatusMessage = "請按下鍵盤任意鍵...";
        //}

        [RelayCommand]
        public void SaveProfile()
        {
            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            string fullPath = Path.GetFullPath(ProfileFileName);
            File.WriteAllText(fullPath, json);
            StatusMessage = $"設定已儲存至: {fullPath}";
        }

        private void LoadDefaultProfile()
        {
            if (File.Exists(ProfileFileName))
            {
                try
                {
                    var json = File.ReadAllText(ProfileFileName);
                    var loadedConfig = JsonSerializer.Deserialize<ProfileConfig>(json);
                    if (loadedConfig != null && loadedConfig.Mappings.Count > 0)
                    {
                        Config = loadedConfig;
                        if (_engine != null) _engine.CurrentProfile = Config;
                        StatusMessage = "設定已自動載入";
                        SelectedMapping = null;
                    }
                    else { InitializeDefaultMappings(); }
                }
                catch { InitializeDefaultMappings(); }
            }
        }

        [RelayCommand]
        public void OpenComboCreator()
        {
            if (SelectedMapping == null || SelectedMapping.IsStickMove) return;

            // 若已經有設定組合鍵，先載入舊設定
            ComboKey1 = SelectedMapping.CustomComboKeys?.Count > 0 ? SelectedMapping.CustomComboKeys[0] : VirtualKeyCode.NONAME;
            ComboKey2 = SelectedMapping.CustomComboKeys?.Count > 1 ? SelectedMapping.CustomComboKeys[1] : VirtualKeyCode.NONAME;
            ComboKey3 = SelectedMapping.CustomComboKeys?.Count > 2 ? SelectedMapping.CustomComboKeys[2] : VirtualKeyCode.NONAME;
            ComboKey4 = SelectedMapping.CustomComboKeys?.Count > 3 ? SelectedMapping.CustomComboKeys[3] : VirtualKeyCode.NONAME;
            ComboKey5 = SelectedMapping.CustomComboKeys?.Count > 4 ? SelectedMapping.CustomComboKeys[4] : VirtualKeyCode.NONAME;

            IsComboCreatorOpen = true;
        }


        [RelayCommand]
        public void CloseComboCreator() => IsComboCreatorOpen = false;
        
        [RelayCommand]
        public void LoadProfileFromFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Xbox Mapper Profile (*.xmap)|*.xmap|All Files (*.*)|*.*",
                Title = "讀取設定檔"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var loadedConfig = JsonSerializer.Deserialize<ProfileConfig>(json);
                    if (loadedConfig != null)
                    {
                        Config = loadedConfig;
                        if (_engine != null) _engine.CurrentProfile = Config;
                        StatusMessage = $"已從 {dialog.SafeFileName} 載入設定";
                    }
                }
                catch (Exception ex) { MessageBox.Show($"讀取失敗: {ex.Message}", "錯誤"); }
            }
        }

        [RelayCommand]
        public void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show($"無法開啟網頁: {ex.Message}"); }
        }

        // 【新增】產生程式碼指令 (開發者用)
        [RelayCommand]
        public void GenerateCode()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// 自動生成的預設值代碼");
            foreach (var map in Config.Mappings)
            {
                if (map.TargetType == TargetDeviceType.None && !map.IsStickMove) continue; // 跳過沒設定的

                sb.AppendLine($"if (input == ControllerInputType.{map.ControllerInput})");
                sb.AppendLine("{");
                sb.AppendLine($"    mapping.TargetType = TargetDeviceType.{map.TargetType};");

                if (map.TargetType == TargetDeviceType.Keyboard || map.TargetType == TargetDeviceType.Combo)
                    sb.AppendLine($"    mapping.KeyCode = (VirtualKeyCode){(int)map.KeyCode};");

                if (map.TargetType == TargetDeviceType.MouseClick)
                    sb.AppendLine($"    mapping.MouseButton = \"{map.MouseButton}\";");

                if (map.IsStickMove)
                    sb.AppendLine($"    mapping.Sensitivity = {map.Sensitivity};");

                sb.AppendLine("}");
            }

            try
            {
                Clipboard.SetText(sb.ToString());
                StatusMessage = "程式碼已複製到剪貼簿！";
            }
            catch { StatusMessage = "複製失敗"; }
        }

        [RelayCommand]
        public void SaveNewCombo()
        {
            if (SelectedMapping == null) return;

            var rawKeys = new System.Collections.Generic.List<VirtualKeyCode> { ComboKey1, ComboKey2, ComboKey3, ComboKey4, ComboKey5 }
                       .Where(k => k != VirtualKeyCode.NONAME).ToList();

            if (rawKeys.Count > 0)
            {
                // 定義系統修飾鍵，確保把它們排在陣列前面 (Combo底層要求修飾鍵必須在前)
                var modifierKeys = new System.Collections.Generic.HashSet<VirtualKeyCode>
                {
                    VirtualKeyCode.LSHIFT, VirtualKeyCode.RSHIFT, VirtualKeyCode.SHIFT,
                    VirtualKeyCode.LCONTROL, VirtualKeyCode.RCONTROL, VirtualKeyCode.CONTROL,
                    VirtualKeyCode.LMENU, VirtualKeyCode.RMENU, VirtualKeyCode.MENU,
                    VirtualKeyCode.LWIN, VirtualKeyCode.RWIN
                };

                // 將修飾鍵排在前面，一般鍵排在最後面
                var sortedKeys = rawKeys.Where(k => modifierKeys.Contains(k)).ToList();
                sortedKeys.AddRange(rawKeys.Where(k => !modifierKeys.Contains(k)));

                // 【修改點】不再註冊進系統，直接切換為 CustomCombo，並把原本單鍵的 KeyCode 清空
                SelectedMapping.TargetType = TargetDeviceType.CustomCombo;
                SelectedMapping.CustomComboKeys = sortedKeys;
                SelectedMapping.KeyCode = VirtualKeyCode.NONAME;
            }
            else
            {
                SelectedMapping.TargetType = TargetDeviceType.None;
                SelectedMapping.CustomComboKeys?.Clear();
            }

            SelectedMapping.UpdateCustomCombo();
            CloseComboCreator();
            SaveProfile();
            StatusMessage = "已套用自定義組合鍵並自動存檔！";
        }
        /*
        public void RecordKey(KeyEventArgs e)
        {
            // 1. 如果是單鍵錄製模式 (原本的功能)
            if (IsRecordingMode && !_isComboCreatorOpen && SelectedMapping != null)
            {
                SelectedMapping.TargetType = TargetDeviceType.Keyboard;
                SelectedMapping.KeyCode = (VirtualKeyCode)KeyInterop.VirtualKeyFromKey(e.Key);
                // 觸發更新的小技巧
                var temp = SelectedMapping; SelectedMapping = null; SelectedMapping = temp;
                IsRecordingMode = false;
                StatusMessage = $"已設定為: {e.Key}";
            }
            // 2. 如果是組合鍵錄製模式 (新功能)
            else if (IsRecordingMode && _isComboCreatorOpen)
            {
                var vKey = (VirtualKeyCode)KeyInterop.VirtualKeyFromKey(e.Key);

                // 避免重複 (例如按住 Ctrl 會一直觸發)
                if (!_recordedComboKeys.Contains(vKey))
                {
                    // 這裡的邏輯：使用者按住 Ctrl, Shift, 然後按 T
                    // 我們會依序收到 KeyDown。
                    // 為了簡化，我們只負責「收集」按下的鍵。
                    // 使用者需要自行按「確認」來完成。
                    // 或者我們可以做「當放開所有按鍵時」自動完成，但手動確認比較穩。

                    // 排除一些系統重複的虛擬鍵 (例如 LeftCtrl 和 Control)
                    // 這裡簡單處理，全部收進來，顯示時過濾
                    _recordedComboKeys.Add(vKey);

                    // 更新顯示字串
                    _newComboPreview = string.Join(" + ", _recordedComboKeys.Select(k => k.ToString().Replace("VK_", "").Replace("LCONTROL", "Ctrl").Replace("RCONTROL", "Ctrl").Replace("LSHIFT", "Shift").Replace("LMENU", "Alt")));
                }
            }
        }*/
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean) return !boolean;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean) return !boolean;
            return false;
        }
    }

    public class EqualityMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] != null && values[1] != null)
            {
                return values[0].ToString() == values[1].ToString();
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}