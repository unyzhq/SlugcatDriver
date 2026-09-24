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
        private float _contentWidth;
        private float _contentHeight;

        // 4. 动态计算滚动区域和输入框的高度
        private float _scrollViewHeight; 
        private float _textFieldHeight;
        private bool _isCalculate = false;

        private GUIStyle? _logStyle;
        private GUIStyle? _errorStyle;
        private bool _stylesInitialized = false;

        private Vector2 _scroll = Vector2.zero;

        private readonly List<string> _log = new List<string>();
        private string _input = string.Empty;
        private const string InputControl = "SlugcatDriver.ConsoleInput";

        private Action<string>? _logInfo;
        public void SetLogInfo(Action<string> logAction) => _logInfo = logAction;
        private Action<string>? _logWarning;
        public void SetLogWarning(Action<string> logAction) => _logWarning = logAction;
        private Action<string>? _logError;
        public void SetLogError(Action<string> logAction) => _logError = logAction;     
        public enum LogType{INFO,WARNING,ERROR};//TODO 目前_log 不缓存类型

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

            if (_logStyle == null)
            {
                _logStyle = new GUIStyle(GUI.skin.label);
                _logStyle.richText = true; // 关键：开启富文本
                _logStyle.wordWrap = true; // 自动换行
                _logStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);// 普通信息样式（浅灰色/白色）
            }
            if (_errorStyle == null)
            {
                _errorStyle = new GUIStyle(GUI.skin.label);
                _errorStyle.richText = true;
                _errorStyle.normal.textColor = new Color(1f, 0.4f, 0.4f); // 红色
            }
            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!_isVisible || !_isCalculate || Event.current == null) return;

            // --- 开始绘制 ---
            
            // 绘制半透明背景
            GUI.Box(new Rect(0, 0, _panelWidth, _panelHeight), GUIContent.none);

            GUILayout.BeginArea(new Rect(_padding, _padding, _contentWidth, _contentHeight));

            // 滚动视图
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            foreach (var line in _log)
            {
                // 使用支持富文本的 GUIStyle
                GUILayout.Label(line, _logStyle); 
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

            _input = GUILayout.TextField(_input ?? string.Empty, GUILayout.Height(_baseFontSize * 1.8f));

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
                // 1. 动态计算字体大小：基于屏幕高度，并限制在 12 到 22 之间
                _baseFontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 45f), 12, 22);

                // 2. 动态计算面板尺寸：宽度占屏幕的 95%，高度占 40%
                _panelWidth = Screen.width * 0.95f;
                _panelHeight = Screen.height * 0.4f;

                // 3. 计算内边距，避免内容紧贴边缘
                _padding = 10f;
                _contentWidth = _panelWidth - _padding * 2;
                _contentHeight = _panelHeight - _padding * 2;

                // 4. 动态计算滚动区域和输入框的高度
                _scrollViewHeight = _contentHeight - _baseFontSize * 2.5f; 
                _textFieldHeight = _baseFontSize * 1.8f;

                _isCalculate = true;
            }
        }
        private void Execute(string raw)
        {
            Log($"> {raw}");
            CommandRegistry.Execute(raw, this);
        }

        private void ScrollToBottom() => _scroll.y = float.MaxValue;

        public void Log(string message)
        {
            message ??= string.Empty;

            _log.Add(message);
            _logInfo?.Invoke(message); // BepInEx 侧：落盘到 BepInEx/LogOutput.log
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
                _logWarning?.Invoke($"[SlugcatDriver] 日志文件写入失败，已停用磁盘日志：{e.Message}");
                _log.Add($"[SlugcatDriver] 日志文件写入失败：{e.Message}");
            }
        }

        public void Clear() => _log.Clear();

    }
}