using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SlugcatDriver.Tool
{
    /// <summary>
    /// 控制台打开期间的输入法抑制：按“侵入性从小到大”分三层回退。
    /// 与旧 ConsoleInputLock 的区别：
    ///   1) 只在“打开/关闭”两个状态切换点动作，不再每帧改布局（OnGUI 一帧跑多次是复原失败的元凶）；
    ///   2) 优先用 Unity 自带的 IME 开关，不碰 Win32；
    ///   3) Win32 回退只作用于**游戏窗口**（ImmAssociateContext / WM_INPUTLANGCHANGEREQUEST），不影响其他程序；
    ///   4) 开时记下原值（在主线程抓），关时精确还原，并显式取消残留组合串（候选窗）。
    /// 必须在主线程调用（ConsoleManager.Instance?.是 MonoBehaviour，OnGUI/Update 都在主线程）。
    /// </summary>
    public static class ConsoleImeGuard
    {
        private static bool _suppressed;

        // ── L0 ────────────────────────────────────────────────────────
        private static IMECompositionMode _prevMode = IMECompositionMode.Auto;
        private static bool _l0Applied;

        // ── L1：窗口级 IMM32 ─────────────────────────────────────────
        [DllImport("imm32.dll")] private static extern IntPtr ImmGetContext(IntPtr hWnd);
        [DllImport("imm32.dll")] private static extern bool   ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
        [DllImport("imm32.dll")] private static extern IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hIMC);
        [DllImport("imm32.dll")] private static extern bool   ImmNotifyIME(IntPtr hIMC, uint dwAction, uint dwIndex, uint dwValue);
        [DllImport("imm32.dll")] private static extern bool   ImmSetOpenStatus(IntPtr hIMC, bool fOpen);
        private const uint NI_COMPOSITIONSTR = 0x0015;
        private const uint CPS_CANCEL        = 0x0004;

        // ── L1'：窗口级布局切换（替代全局 ActivateKeyboardLayout） ────
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);
        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll")] private static extern bool   PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        private const uint KLF_ACTIVATE              = 0x0001;
        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        private static IntPtr _hwnd;
        private static IntPtr _savedHimc = IntPtr.Zero;
        private static IntPtr _savedHkl  = IntPtr.Zero;
        private static IntPtr _enHkl     = IntPtr.Zero;

        /// <summary>游戏主窗口句柄（缓存）。优先用进程主窗口，退化为前台窗口。</summary>
        private static IntPtr Hwnd()
        {
            if (_hwnd != IntPtr.Zero) return _hwnd;
            try { _hwnd = Process.GetCurrentProcess().MainWindowHandle; } catch { }
            if (_hwnd == IntPtr.Zero) _hwnd = GetActiveWindow();
            if (_hwnd == IntPtr.Zero) _hwnd = GetForegroundWindow();
            return _hwnd;
        }

        /// <summary>控制台“打开”时调用一次（不要每帧调）。</summary>
        public static void Suppress()
        {
            if (_suppressed) return;
            _suppressed = true;

            // L0：先让 Unity 自己不再处理 IME 组合
            try
            {
                _prevMode = Input.imeCompositionMode;
                Input.imeCompositionMode = IMECompositionMode.Off;
                _l0Applied = true;
            }
            catch (Exception e) { ConsoleManager.Instance?.LogWarning("[IME] L0 失败：" + e.Message); }

            IntPtr hwnd = Hwnd();
            if (hwnd == IntPtr.Zero) { ConsoleManager.Instance?.LogWarning("[IME] 拿不到游戏窗口句柄，退化为仅 L0"); return; }

            // L1：先取消正在进行的组合串（否则候选窗会赖着不走），再把输入法上下文从窗口摘掉
            IntPtr himc = ImmGetContext(hwnd);
            if (himc != IntPtr.Zero)
            {
                ImmNotifyIME(himc, NI_COMPOSITIONSTR, CPS_CANCEL, 0);
                ImmSetOpenStatus(himc, false);
                ImmReleaseContext(hwnd, himc);
            }
            _savedHimc = ImmAssociateContext(hwnd, IntPtr.Zero);   // 返回原 HIMC，关闭时精确还原

            // L1'：顺手把“这个窗口”的布局切成英语（个别 IME 仍吃键时兜底）
            _savedHkl = GetKeyboardLayout(0);                      // ★ 在主线程、打开时抓，别在静态初始化里抓
            if (_enHkl == IntPtr.Zero) _enHkl = LoadKeyboardLayout("00000409", KLF_ACTIVATE);
            if (_enHkl != IntPtr.Zero)
                PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, _enHkl);

            // ConsoleManager.Instance?.LogMessage($"[IME] 抑制生效 hwnd=0x{hwnd.ToInt64():X} savedHimc=0x{_savedHimc.ToInt64():X} savedHkl=0x{_savedHkl.ToInt64():X}");
        }

        /// <summary>控制台“关闭”时调用一次（不要每帧调）。</summary>
        public static void Restore()
        {
            if (!_suppressed) return;
            _suppressed = false;

            IntPtr hwnd = Hwnd();

            // L1'：还原该窗口的布局
            if (hwnd != IntPtr.Zero && _savedHkl != IntPtr.Zero)
                PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, _savedHkl);

            // L1：精确还原输入法上下文（原本没有上下文就别硬塞一个）
            if (hwnd != IntPtr.Zero && _savedHimc != IntPtr.Zero)
                ImmAssociateContext(hwnd, _savedHimc);

            // L0
            if (_l0Applied)
            {
                try { Input.imeCompositionMode = _prevMode; } catch { }
                _l0Applied = false;
            }

            // ConsoleManager.Instance?.LogMessage("[IME] 已还原");
        }

        /// <summary>进程退出/组件销毁时的兜底，避免“卡在英文布局”。</summary>
        public static void RestoreSafe()
        {
            try { Restore(); } catch (Exception e) { ConsoleManager.Instance?.LogWarning("[IME] 还原失败：" + e.Message); }
        }

        /// <summary>排错用：打印当前 IME 相关状态。</summary>
        public static void DumpState()
        {
            IntPtr hwnd = Hwnd();
            IntPtr himc = hwnd != IntPtr.Zero ? ImmGetContext(hwnd) : IntPtr.Zero;
            // ConsoleManager.Instance?.LogMessage($"[IME] hwnd=0x{hwnd.ToInt64():X} himc=0x{himc.ToInt64():X} " +
            //          $"hkl(thread0)=0x{GetKeyboardLayout(0).ToInt64():X} " +
            //          $"imeIsSelected={Input.imeIsSelected} mode={Input.imeCompositionMode} suppressed={_suppressed}");
            if (himc != IntPtr.Zero) ImmReleaseContext(hwnd, himc);
        }

        // ── 看门狗：焦点切回来 / IME 重新附着时，把输入法上下文再摘掉 ──────────
        private static float _nextReassert;
        private const float ReassertInterval = 0.25f;   // 4 次/秒，够快且无感

        /// <summary>
        /// 强制重申抑制：忽略 _suppressed 早退，重新检查并摘掉窗口的输入法上下文。
        /// 已经是摘除状态（ImmGetContext == NULL）时直接返回，零副作用。
        /// </summary>
        public static void Reassert()
        {
            if (!_suppressed) return;                  // 只在“本该被压制”的期间重申
            IntPtr hwnd = Hwnd();
            if (hwnd == IntPtr.Zero) return;

            IntPtr himc = ImmGetContext(hwnd);
            if (himc == IntPtr.Zero) return;           // ★ 已经摘掉了 → 什么都不做（这就是“廉价”的原因）

            // IME 又回来了：先取消它可能已经开始的组合串（候选窗），再摘上下文
            ImmNotifyIME(himc, NI_COMPOSITIONSTR, CPS_CANCEL, 0);
            ImmSetOpenStatus(himc, false);
            ImmReleaseContext(hwnd, himc);

            IntPtr prev = ImmAssociateContext(hwnd, IntPtr.Zero);
            if (_savedHimc == IntPtr.Zero) _savedHimc = prev;   // 只补记“第一次”的原值，绝不覆盖
            // Debug.Log("[IME] Reassert：输入法上下文已重新摘除");
        }

        /// <summary>每帧调一次即可（内部按 0.25s 限流）。控制台没开时是 1 次 bool 判断。</summary>
        public static void Watchdog()
        {
            if (!_suppressed) return;
            if (Time.unscaledTime < _nextReassert) return;
            _nextReassert = Time.unscaledTime + ReassertInterval;
            Reassert();
        }
    }
}