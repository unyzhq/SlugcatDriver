using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SlugcatDriver.Tool
{
    public class ConsoleManager : MonoBehaviour
    {
        public static ConsoleManager? Instance { get; private set; }

        // 面板可见性
        private bool _isVisible = false;
        // ime
        private bool _imeSuppressed = false;
        // 面板宽高
        private float _panelWidth;
        private float _panelHeight;

        // 面板内边距
        private float _padding;
        private float _contentX;
        private float _contentY;
        
        private float _contentWidth;
        private float _contentHeight;
        private float _panelX;
        private float _panelY;

        // 滚动区域和输入框高度
        private float _scrollViewHeight; 
        private float _textFieldHeight;
        private bool _isCalculated = false;

        private GUIStyle? _messageStyle;
        private GUIStyle? _infoStyle;
        private GUIStyle? _debugStyle;
        private GUIStyle? _warningStyle;
        private GUIStyle? _errorStyle;
        private GUIStyle? _inputStyle;
        private GUIStyle? _promptStyle;
        private bool _isStylesInitialized = false;

        private Vector2 _scroll = Vector2.zero;
        
        private string _input = string.Empty;
        private const string InputControl = "SlugcatDriver.ConsoleInput";

        // MESSAGE : 用户消息 INFO : 程序信息 DEBUG : 调试信息 WARNING : 风险提示 ERROR : 错误代码
        public enum LogType{MESSAGE,INFO,DEBUG,WARNING,ERROR};
        private class LogEntry
        {
            public LogType Type { get; }
            public string Message { get; }

            public LogEntry(LogType type, string message)
            {
                Type = type;
                Message = message;
            }
        }

        private readonly List<LogEntry> _log = new List<LogEntry>();


        private Action<string>? _logMessage;
        public void SetLogMessage(Action<string> logAction) => _logMessage = logAction;
        private Action<string>? _logInfo;
        public void SetLogInfo(Action<string> logAction) => _logInfo = logAction;
        private Action<String>? _logDebug;
        public void SetLogDebug(Action<string> logAction) => _logDebug = logAction;
        private Action<string>? _logWarning;
        public void SetLogWarning(Action<string> logAction) => _logWarning = logAction;
        private Action<string>? _logError;
        public void SetLogError(Action<string> logAction) => _logError = logAction;

        // 注入控制台开关的快捷键。
        private KeyCode? _toggleKey;
        public void SetToggleKey(KeyCode mainKey) => _toggleKey = mainKey;

        private string _logFilePath = "BepInEx/SlugcatDriver.log";

        private const int MaxLines = 500;




        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            if (!_isVisible || !_isCalculated || Event.current == null) return;
            if (!_isStylesInitialized) InitializeStyles();

            // 控制台开着时，保证输入法一直处于“被摘除”状态（对付切出游戏换输入法再切回来）
            ConsoleImeGuard.Watchdog();

            // 拦截主键，防止字符进入 TextField
            if (Event.current.type == EventType.KeyDown &&
                _toggleKey.HasValue &&
                Event.current.keyCode == _toggleKey.Value)
            {
                Close();
                Event.current.Use();
                return;
            }

            // 粘贴过滤
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.V &&
                (Event.current.modifiers & EventModifiers.Control) != 0)
            {
                if (ContainsNonAscii(GUIUtility.systemCopyBuffer))
                {
                    Event.current.Use();  // 阻止这次按键传给 TextField
                }
            }
            var pasteModifier = EventModifiers.Control | EventModifiers.Command;
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.V &&
                (Event.current.modifiers & pasteModifier) != 0)
            {
                if (ContainsNonAscii(GUIUtility.systemCopyBuffer))
                {
                    Event.current.Use();  // 阻止这次按键传给 TextField
                }
                
            }
            if (Event.current.type == EventType.ValidateCommand &&
                Event.current.commandName == "Paste")
            {
                if (ContainsNonAscii(GUIUtility.systemCopyBuffer))
                {
                    Event.current.Use();
                }
            }
            if (Event.current.type == EventType.ExecuteCommand)
            {
                // 某些 Unity 版本在 Win+V 时走 ExecuteCommand
                string cmd = Event.current.commandName;
                if ((cmd == "Paste" || cmd == "PasteSpecial") &&
                    ContainsNonAscii(GUIUtility.systemCopyBuffer))
                {
                    Event.current.Use();
                }
            }
            
            // 锁定焦点：只在 Layout 事件里做，避免每帧多次设置
            if (Event.current.type == EventType.Layout &&
                GUI.GetNameOfFocusedControl() != InputControl)
            {
                GUI.FocusControl(InputControl);
            }

            // --- 开始绘制 ---
            GUI.skin.textField.fontSize = UiTheme.baseFontSize;
            GUI.matrix = Matrix4x4.identity;
            // 绘制半透明背景
            var oldColor = GUI.color;
            GUI.color = UiTheme.bgColor;
            GUI.DrawTexture(new Rect(_panelX, _panelY, _panelWidth, _panelHeight), UiTheme.WhiteTexture);
            GUI.color = oldColor;
            
            GUILayout.BeginArea(new Rect(_contentX, _contentY, _contentWidth, _contentHeight));

            // 滚动视图
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_scrollViewHeight));
            _scroll.y = Mathf.Round(_scroll.y);   // 每帧取整，避免半像素位置

            foreach (var entry in _log)
            {
                GUIStyle style = (entry.Type switch
                {
                    LogType.INFO    => _infoStyle,
                    LogType.DEBUG   => _debugStyle,
                    LogType.WARNING => _warningStyle,
                    LogType.ERROR   => _errorStyle,
                    _               => _messageStyle
                }) ?? GUI.skin.label;

                GUILayout.Label(entry.Message, style);
            }

            GUILayout.EndScrollView();
            
            // --- 输入框 ---
            var submit = false;
            GUI.SetNextControlName(InputControl);

            if (Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                submit = true;
                Event.current.Use();
            }

            GUILayout.BeginHorizontal();

            GUILayout.Label(" >", _promptStyle, GUILayout.Width(UiTheme.baseFontSize * 1.2f), GUILayout.Height(_textFieldHeight));

            _input = GUILayout.TextField(_input ?? string.Empty, _inputStyle, GUILayout.Height(_textFieldHeight));

            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            // --- 提交处理 ---
            if (submit && _input.Length > 0)
            {
                var command = _input;
                _input = string.Empty;
                Execute(command);
                ScrollToBottom();
            }
        }
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;          // 切出去：什么都不做（L1/L2 都是游戏自身作用域，不必还原）
            if (!_isVisible) return;        // 控制台没开：不需要压制

            try
            {
                ConsoleImeGuard.Suppress(); // 若期间被 Restore 过则重新压制（已压制时是 no-op）
                ConsoleImeGuard.Reassert(); // 立刻重申一次，省掉看门狗那 ≤0.25s 的空窗
            }
            catch (Exception e) { ConsoleManager.Instance?.LogWarning("[IME] 焦点恢复重申失败：" + e.Message); }
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused || !_isVisible) return;
            try { ConsoleImeGuard.Suppress(); ConsoleImeGuard.Reassert(); }
            catch (Exception e) { ConsoleManager.Instance?.LogWarning("[IME] 暂停恢复重申失败：" + e.Message); }
        }
        private void OnApplicationQuit() => ConsoleImeGuard.RestoreSafe();
        private void OnDisable()  => ConsoleImeGuard.RestoreSafe();
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ConsoleImeGuard.RestoreSafe();
        }

        public void Open()
        {
            _isVisible = true;
            ApplyImeState();
            RecalculateLayout();
        }
        public void Close()
        {
            _isVisible = false;
            ApplyImeState();
        }

        private void RecalculateLayout()
        {
            // 面板：宽 70%，高 82%，水平垂直居中，向上偏移1%
            _panelWidth  = Mathf.Round(Screen.width  * 0.7f);
            _panelHeight = Mathf.Round(Screen.height * 0.82f);
            _panelX      = Mathf.Round((Screen.width  - _panelWidth)  * 0.5f);
            _panelY      = Mathf.Round((Screen.height - _panelHeight) * 0.49f);

            // 面板内边距
            _padding = 12f;
            _contentX = Mathf.Round(_panelX + _padding);
            _contentY = Mathf.Round(_panelY + _padding);
            _contentWidth  = Mathf.Round(_panelWidth  - _padding * 2);
            _contentHeight = Mathf.Round(_panelHeight - _padding * 2);

            // 输入框高度 + 滚动区高度
            _textFieldHeight  = UiTheme.baseFontSize * 1.8f;
            _scrollViewHeight = _contentHeight - _textFieldHeight - _padding;

            _isCalculated = true;
        }
        private void InitializeStyles()
        {
            _messageStyle = UiTheme.createMessageStyle();
            _infoStyle = UiTheme.createInfoStyle();
            _debugStyle = UiTheme.createDebugStyle();
            _warningStyle = UiTheme.createWarningStyle();
            _errorStyle = UiTheme.createErrorStyle();

            _inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = UiTheme.baseFontSize,
                richText = false,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.MiddleLeft
            };
            // 清掉所有状态的背景
            _inputStyle.normal.background  = null;
            _inputStyle.focused.background = null;
            _inputStyle.hover.background   = null;
            _inputStyle.active.background  = null;

            _inputStyle.normal.textColor  = UiTheme.messageColor;
            _inputStyle.focused.textColor = UiTheme.messageColor;
            _inputStyle.hover.textColor   = UiTheme.messageColor;
            _inputStyle.active.textColor  = UiTheme.messageColor;
            _inputStyle.font = UiTheme.consoleFont;

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UiTheme.baseFontSize,
                richText = false,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };
            _promptStyle.alignment = TextAnchor.MiddleLeft;
            _promptStyle.normal.textColor = UiTheme.messageColor;
            _promptStyle.font = UiTheme.consoleFont;

            _isStylesInitialized = true;
        }
        private bool ContainsNonAscii(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (c > 127) return true;
            }
            return false;
        }
        private void Execute(string raw)
        {
            LogMessage($" > {raw}");
            CommandRegistry.Execute(raw, this);
        }
        
        private void ApplyImeState()
        {
            if (_isVisible == _imeSuppressed) return;    // 状态没变 → 什么都不做
            _imeSuppressed = _isVisible;
            if (_isVisible) ConsoleImeGuard.Suppress();
            else         ConsoleImeGuard.Restore();
        }

        private void ScrollToBottom() => _scroll.y = float.MaxValue;

        public void LogMessage(string message)
        {
            if(_logMessage != null) Log(_logMessage,LogType.MESSAGE,message);
        }
        public void LogInfo(string message)
        {
            if(_logInfo !=  null) Log(_logInfo,LogType.INFO,message);
        }
        public void LogDebug(string message)
        {
            if(_logDebug !=  null) Log(_logDebug,LogType.DEBUG,message);
        }

        public void LogWarning(string message)
        {
            if(_logWarning !=  null) Log(_logWarning,LogType.WARNING,message);
        }
        public void LogError(string message)
        {
            if(_logError !=  null) Log(_logError,LogType.ERROR,message);
        }
        private void Log(Action<string> action,LogType logType,string message)
        {
            message ??= string.Empty;

            _log.Add(new LogEntry(logType,message));
            action?.Invoke(message); // BepInEx 侧：落盘到 BepInEx/LogOutput.log
            WriteToFile(message);        // 本模组侧：追加到独立日志文件

            while (_log.Count > MaxLines) _log.RemoveAt(0);
            ScrollToBottom();
        }

        private void WriteToFile(string message)
        {
            var payload = message;

            try
            {
                var dir = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // 追加 + 每次落盘：游戏崩溃或被强杀也不会丢日志
                File.AppendAllText(_logFilePath, payload + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                // 磁盘日志失败不能影响游戏：只报一次，然后彻底关闭文件日志
                LogError($"[SlugcatDriver] 日志文件写入失败，已停用磁盘日志：{e.Message}");
                LogError($"[SlugcatDriver] 日志文件写入失败：{e.Message}");
            }
        }

        public void Clear() => _log.Clear();




    }
}