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

        private bool _isVisible = false;

        // 1. 动态计算字体大小：基于屏幕高度，并限制在 12 到 22 之间
        private int _baseFontSize;

        // 2. 动态计算面板尺寸：宽度占屏幕的 95%，高度占 40%
        private float _panelWidth;
        private float _panelHeight;

        // 3. 计算内边距，避免内容紧贴边缘
        private float _padding = 10f;
        private float _contentX;
        private float _contentY;
        
        private float _contentWidth;
        private float _contentHeight;
        private float _panelX;
        private float _panelY;

        // 4. 动态计算滚动区域和输入框的高度
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
        private Font? _consoleFont;
        private bool _isStylesInitialized = false;

        // 背景色 (半透明深色)
        private static readonly Color _BgColor = new Color(26f / 255f, 26f / 255f, 46f / 255f, 0.9f); // 怪猫

        // 文字颜色
        private static readonly Color _MessageColor = new Color(255f / 255f, 247f / 255f, 233f / 255f); // 饕鬄

        private static readonly Color _InfoColor = new Color(166f / 255f, 219f / 255f, 255f / 255f); // 溪流
        private static readonly Color _DebugColor = new Color(255f / 255f, 236f / 255f, 175f / 255f); // 僧侣 
        private static readonly Color _WarningColor = new Color(255f / 255f, 221f / 255f, 221f / 255f); // 猎手
        private static readonly Color _ErrorColor = new Color(255f / 255f, 70f / 255f, 50f / 255f); // 工匠

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
            if (!_isStylesInitialized)
            {
                InitializeStyles();
            }
            ConsoleInputLock.SwitchToEnInput();
            // 拦截主键，防止字符进入 TextField
            if (Event.current.type == EventType.KeyDown &&
                _toggleKey.HasValue &&
                Event.current.keyCode == _toggleKey.Value)
            {
                Toggle();
                Event.current.Use();
                ConsoleInputLock.SwitchToOriInput();
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
            GUI.skin.textField.fontSize = _baseFontSize;
            GUI.matrix = Matrix4x4.identity;
            // 绘制半透明背景
            var oldColor = GUI.color;
            GUI.color = _BgColor;
            GUI.DrawTexture(new Rect(_panelX, _panelY, _panelWidth, _panelHeight), ConsolePanelBackground.WhiteTexture);
            GUI.color = oldColor;
            
            //GUI.Box(new Rect(_panelX, _panelY, _panelWidth, _panelHeight), GUIContent.none);
            GUILayout.BeginArea(new Rect(_contentX, _contentY, _contentWidth, _contentHeight));

            // 滚动视图
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_scrollViewHeight));

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

            GUILayout.Label(" >", _promptStyle, GUILayout.Width(_baseFontSize * 1.2f), GUILayout.Height(_textFieldHeight));

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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        public void Toggle()
        {
            _isVisible = !_isVisible;
            if (_isVisible)
            {
                RecalculateLayout();
            }
        }

        private void RecalculateLayout()
        {
            // 面板：宽 80%，高 70%，水平居中，距顶部 5%
            _panelWidth  = Mathf.Round(Screen.width  * 0.7f);
            _panelHeight = Mathf.Round(Screen.height * 0.82f);
            _panelX      = Mathf.Round((Screen.width  - _panelWidth)  * 0.5f);
            _panelY      = Mathf.Round((Screen.height - _panelHeight) * 0.49f);

            // 字体：基于屏幕高度，范围 12~20，原来 /45 偏大
            _baseFontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 70f), 15, 18);

            // 面板内边距
            _padding = 12f;
            _contentX = Mathf.Round(_panelX + _padding);
            _contentY = Mathf.Round(_panelY + _padding);
            _contentWidth  = Mathf.Round(_panelWidth  - _padding * 2);
            _contentHeight = Mathf.Round(_panelHeight - _padding * 2);

            // 输入框高度 + 滚动区高度
            _textFieldHeight  = _baseFontSize * 1.8f;
            _scrollViewHeight = _contentHeight - _textFieldHeight - _padding;

            _isCalculated = true;
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
        private void InitializeStyles()
        {
            int marginLeft = 0,marginRight = 0,marginTop = 4,marginBottom = 4;
            int paddingLeft = 0,paddingRifht = 0,paddingTop = 0,paddingBottom = 0;
            if (_consoleFont == null)
            {
                // 优先尝试 Consolas，Windows 上基本都有
                _consoleFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Consolas", "Cascadia Mono", "Courier New", "Monaco" },
                    _baseFontSize
                );
            }

            if (_messageStyle == null) _messageStyle = new GUIStyle(GUI.skin.label);
            _messageStyle.richText = true; // 关键：开启富文本
            _messageStyle.wordWrap = true; // 自动换行
            _messageStyle.normal.textColor  = _MessageColor;
            _messageStyle.fontSize = _baseFontSize;
            _messageStyle.margin = new RectOffset(marginLeft, marginRight, marginTop, marginBottom);
            _messageStyle.padding = new RectOffset(paddingLeft, paddingRifht, paddingTop, paddingBottom);
            _messageStyle.font = _consoleFont;
            

            if (_infoStyle == null) _infoStyle = new GUIStyle(GUI.skin.label);
            _infoStyle.richText = true; // 关键：开启富文本
            _infoStyle.wordWrap = true; // 自动换行
            _infoStyle.normal.textColor = _InfoColor;
            _infoStyle.fontSize = _baseFontSize;
            _infoStyle.margin = new RectOffset(marginLeft, marginRight, marginTop, marginBottom);
            _infoStyle.padding = new RectOffset(paddingLeft, paddingRifht, paddingTop, paddingBottom);
            _infoStyle.font = _consoleFont;

            if (_debugStyle == null) _debugStyle = new GUIStyle(GUI.skin.label);
            _debugStyle.richText = true; // 关键：开启富文本
            _debugStyle.wordWrap = true; // 自动换行
            _debugStyle.normal.textColor = _DebugColor;
            _debugStyle.fontSize = _baseFontSize;
            _debugStyle.margin = new RectOffset(marginLeft, marginRight, marginTop, marginBottom);
            _debugStyle.padding = new RectOffset(paddingLeft, paddingRifht, paddingTop, paddingBottom);
            _debugStyle.font = _consoleFont;

            if (_warningStyle == null) _warningStyle = new GUIStyle(GUI.skin.label);
            _warningStyle.richText = true; // 关键：开启富文本
            _warningStyle.wordWrap = true; // 自动换行
            _warningStyle.normal.textColor = _WarningColor;
            _warningStyle.fontSize = _baseFontSize;
            _warningStyle.margin = new RectOffset(marginLeft, marginRight, marginTop, marginBottom);
            _warningStyle.padding = new RectOffset(paddingLeft, paddingRifht, paddingTop, paddingBottom);
            _debugStyle.font = _consoleFont;

            if (_errorStyle == null) _errorStyle = new GUIStyle(GUI.skin.label);
            _errorStyle.richText = true; // 关键：开启富文本
            _errorStyle.wordWrap = true; // 自动换行
            _errorStyle.normal.textColor = _ErrorColor;
            _errorStyle.fontSize = _baseFontSize;
            _errorStyle.margin = new RectOffset(marginLeft, marginRight, marginTop, marginBottom);
            _errorStyle.padding = new RectOffset(paddingLeft, paddingRifht, paddingTop, paddingBottom);
            _errorStyle.font = _consoleFont;

            if (_inputStyle == null)
            {
                _inputStyle = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = _baseFontSize,
                    richText = false,
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.MiddleLeft
                };
            }

            // 清掉所有状态的背景
            _inputStyle.normal.background  = null;
            _inputStyle.focused.background = null;
            _inputStyle.hover.background   = null;
            _inputStyle.active.background  = null;

            _inputStyle.normal.textColor  = _MessageColor;
            _inputStyle.focused.textColor = _MessageColor;
            _inputStyle.hover.textColor   = _MessageColor;
            _inputStyle.active.textColor  = _MessageColor;
            _inputStyle.font = _consoleFont;

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _baseFontSize,
                richText = false,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };
            _promptStyle.alignment = TextAnchor.MiddleLeft;
            _promptStyle.normal.textColor = _MessageColor;
            _promptStyle.font = _consoleFont;

            _isStylesInitialized = true;
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