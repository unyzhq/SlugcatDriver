using UnityEngine;

namespace SlugcatDriver.Tool
{
    public class ConsolePanelBackground
    {        // 白色纹理，用于创建纯色背景
        private static UnityEngine.Texture2D? _whiteTexture;

        public static UnityEngine.Texture2D WhiteTexture
        {
            get
            {
                if (_whiteTexture == null)
                {
                    _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32,false);
                    _whiteTexture.SetPixel(0, 0, Color.white);
                    _whiteTexture.Apply();
                }
                return _whiteTexture;
            }
        }
    }
}