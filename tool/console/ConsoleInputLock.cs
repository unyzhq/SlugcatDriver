using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SlugcatDriver.Tool
{
    public static class ConsoleInputLock
    {
        [DllImport("user32.dll")]
        private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        [DllImport("user32.dll")]
        private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

        private const uint KLF_ACTIVATE = 0x00000001;

        // 切换到英语键盘布局，关闭中文输入
        public static void SwitchToEnInput()
        {
            // "00000409" 是英语的语言标识
            IntPtr hkl = LoadKeyboardLayout("00000409", KLF_ACTIVATE);
            if (hkl != IntPtr.Zero)
            {
                ActivateKeyboardLayout(hkl, KLF_ACTIVATE);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        // 记住切换前
        static IntPtr original = GetKeyboardLayout(0);

        
        public static void SwitchToOriInput()
        {
            if (original != IntPtr.Zero)
            {
                // 切回
                ActivateKeyboardLayout(original, KLF_ACTIVATE);
            }
        }
    }
}