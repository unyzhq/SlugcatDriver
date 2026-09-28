using UnityEngine;
namespace SlugcatDriver.Tool
{
    // UI样式静态资源(窗口、面板、按钮、字体、颜色)
    public static class UiTheme
    {
        // 白色纹理，用于创建纯色背景
        public static UnityEngine.Texture2D? _whiteTexture;
        public static UnityEngine.Texture2D WhiteTexture
        {
            get
            {
                if (_whiteTexture == null)
                {
                    _whiteTexture = new UnityEngine.Texture2D(1, 1, TextureFormat.RGBA32,false);
                    _whiteTexture.SetPixel(0, 0, Color.white);
                    _whiteTexture.Apply();
                }
                return _whiteTexture;
            }
        }
        public static readonly int baseFontSize = 15; // 字体大小
        public static readonly Font consoleFont = UnityEngine.Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Cascadia Mono", "Courier New", "Monaco" }, baseFontSize); // 等宽字体
        public static readonly Color bgColor = new Color32(26, 26, 46, 230); // 怪猫
        public static readonly Color messageColor = new Color32(255, 247, 233, 255); // 饕鬄

        public static readonly Color infoColor = new Color32(166, 219, 255, 255); // 溪流
        public static readonly Color debugColor = new Color32(255, 236, 175, 255); // 僧侣 
        public static readonly Color warningColor = new Color32(255, 221, 221, 255); // 猎手
        public static readonly Color errorColor = new Color32(255, 70, 50, 255); // 工匠
        // 调用链的最上层，必须是OnGUI()
        public static GUIStyle createMessageStyle()
        {
            return createStyle(messageColor); // 饕鬄
        }
        public static GUIStyle createInfoStyle()
        {
            return createStyle(infoColor); // 溪流
        }
        public static GUIStyle createDebugStyle()
        {
            return createStyle(debugColor); // 僧侣
        }
        public static GUIStyle createWarningStyle()
        {
            return createStyle(warningColor); // 猎手
        }
        public static GUIStyle createErrorStyle()
        {
            return createStyle(errorColor); // 工匠
        }
        public static GUIStyle createStyle(Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.richText = true; // 关键：开启富文本
            style.wordWrap = true; // 自动换行
            style.normal.textColor  = color;
            style.fontSize = baseFontSize;
            style.margin = new RectOffset(0, 0, 5, 5);
            style.padding = new RectOffset(0, 0, 2, 2);
            style.font = consoleFont;
            return style;
        }
    }    
}