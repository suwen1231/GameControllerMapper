using System.Windows;
using SharpDX.XInput; 
using WindowsInput; 

namespace GameControllerMapper
{
    public partial class App : Application
    {
        // 加入這個建構函式
        public App()
        {
            // 這是一個「假動作」，實際上不會執行
            // 只是為了讓編譯器把 DLL 複製到 bin 資料夾
            var dummy1 = typeof(Controller);
            var dummy2 = typeof(InputSimulator);
        }
    }
}