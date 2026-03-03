using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WindowsInput.Native;

namespace GameControllerMapper
{
    public class KeyOption
    {
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public VirtualKeyCode Code { get; set; }
    }

    public static class KeyRepository
    {
        public static ObservableCollection<KeyOption> AllKeys { get; } = new ObservableCollection<KeyOption>();
        public static Dictionary<int, VirtualKeyCode[]> ComboDefinitions { get; } = new Dictionary<int, VirtualKeyCode[]>();

        static KeyRepository()
        {
            InitializeDefaultKeys();
        }

        private static void InitializeDefaultKeys()
        {
            AllKeys.Clear();
            AllKeys.Add(new KeyOption { Category = "---", Name = "--- 無功能 ---", Code = VirtualKeyCode.NONAME });

            // 預設組合鍵 (ID 從 2000 開始)
            AddCombo("Ctrl+C (複製)", 2001, VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_C);
            AddCombo("Ctrl+V (貼上)", 2002, VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_V);
            AddCombo("Ctrl+X (剪下)", 2003, VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_X);
            AddCombo("Ctrl+Z (復原)", 2004, VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_Z);
            AddCombo("Ctrl+A (全選)", 2005, VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_A);
            AddCombo("Ctrl+S (儲存)", 2006, VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_S);
            AddCombo("Alt+Tab (切視窗)", 2007, VirtualKeyCode.LMENU, VirtualKeyCode.TAB);
            AddCombo("Win+Shift+S (截圖)", 2008, VirtualKeyCode.LWIN, VirtualKeyCode.LSHIFT, VirtualKeyCode.VK_S);
            AddCombo("Win+D (顯示桌面)", 2009, VirtualKeyCode.LWIN, VirtualKeyCode.VK_D);
            AddCombo("Alt+F4 (強制關閉)", 2010, VirtualKeyCode.LMENU, VirtualKeyCode.F4);

            // 一般按鍵 (A-Z)
            for (int i = 65; i <= 90; i++)
                AllKeys.Add(new KeyOption { Category = "🔤 英文 (A-Z)", Name = ((VirtualKeyCode)i).ToString().Replace("VK_", ""), Code = (VirtualKeyCode)i });

            // 數字
            for (int i = 48; i <= 57; i++)
                AllKeys.Add(new KeyOption { Category = "🔢 數字 (鍵盤上方)", Name = ((VirtualKeyCode)i).ToString().Replace("VK_", ""), Code = (VirtualKeyCode)i });

            // 九宮格
            for (int i = 96; i <= 105; i++)
                AllKeys.Add(new KeyOption { Category = "🔢 數字 (右側九宮格)", Name = ((VirtualKeyCode)i).ToString().Replace("NUMPAD", "Num "), Code = (VirtualKeyCode)i });

            // F1-F12
            for (int i = 112; i <= 123; i++)
                AllKeys.Add(new KeyOption { Category = "⚡ 功能鍵 (F1-F12)", Name = ((VirtualKeyCode)i).ToString(), Code = (VirtualKeyCode)i });

            var sysKeys = new List<(string, VirtualKeyCode)>
            {
                ("Esc", VirtualKeyCode.ESCAPE), ("Tab", VirtualKeyCode.TAB), ("Caps Lock", VirtualKeyCode.CAPITAL),
                ("Shift (左)", VirtualKeyCode.LSHIFT), ("Shift (右)", VirtualKeyCode.RSHIFT),
                ("Ctrl (左)", VirtualKeyCode.LCONTROL), ("Ctrl (右)", VirtualKeyCode.RCONTROL),
                ("Alt (左)", VirtualKeyCode.LMENU), ("Alt (右)", VirtualKeyCode.RMENU),
                ("Space", VirtualKeyCode.SPACE), ("Enter", VirtualKeyCode.RETURN),
                ("Backspace", VirtualKeyCode.BACK), ("Delete", VirtualKeyCode.DELETE),
                ("↑", VirtualKeyCode.UP), ("↓", VirtualKeyCode.DOWN), ("←", VirtualKeyCode.LEFT), ("→", VirtualKeyCode.RIGHT),
                ("Windows", VirtualKeyCode.LWIN)
            };
            foreach (var k in sysKeys) AllKeys.Add(new KeyOption { Category = "⚙️ 系統與控制", Name = k.Item1, Code = k.Item2 });
        }

        private static void AddCombo(string name, int id, params VirtualKeyCode[] keys)
        {
            if (!AllKeys.Any(k => k.Code == (VirtualKeyCode)id))
            {
                AllKeys.Insert(11 > AllKeys.Count ? AllKeys.Count : 11, new KeyOption { Category = "✨ 組合鍵 (Combo)", Name = name, Code = (VirtualKeyCode)id });
                ComboDefinitions[id] = keys;
            }
        }

        public static string GetComboName(VirtualKeyCode code)
        {
            var option = AllKeys.FirstOrDefault(k => k.Code == code);
            return option != null ? option.Name : "未知組合鍵";
        }
    }
}