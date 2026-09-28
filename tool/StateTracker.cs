using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace SlugcatDriver.Tool
{
    public class StateTracker : MonoBehaviour
    {
        public static StateTracker? Instance { get; private set; }
        private Dictionary<SlugcatState,HashSet<PropertyInfo>> _cacheMap = new Dictionary<SlugcatState,HashSet<PropertyInfo>>();
        private Dictionary<int,SlugcatState> _indexCacheMap = new Dictionary<int, SlugcatState>();
        private float _panelX;
        private float _panelY;
        private float _panelWidth;
        private float _panelHeight;
        private float _contentX;
        private float _contentY;
        
        private float _contentWidth;
        private float _contentHeight;
        private float _padding;

        private GUIStyle? _messageStyle;
        private GUIStyle? _messageStyleAligLeft;
        private GUIStyle? _messageStyleAligRight;
        private bool _isCalculated;
        private List<List<float>> _cacheList = new List<List<float>>();
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
            if(!_isCalculated || _cacheList.Count() < 1 || Event.current == null) return;
            float lineHeight = _messageStyle != null ? Mathf.Max(_messageStyle.lineHeight,_messageStyle.lineHeight) : UiTheme.baseFontSize * 1.1f;
            for(int i = 0;i < _cacheList.Count(); i++)
            {
                float x = _cacheList[i][0];
                float y = _cacheList[i][1];
                float width = _cacheList[i][2];
                float height = _cacheList[i][3];
                var oldColor = GUI.color;
                GUI.color = UiTheme.bgColor;
                GUI.DrawTexture(new Rect(x, y, width, height), UiTheme.WhiteTexture);
                GUI.color = oldColor;
                GUI.Label(new Rect(x, y, width, lineHeight), $"Slugcat{i+1}", _messageStyle);
                y += lineHeight;
                foreach(PropertyInfo pi in _cacheMap[_indexCacheMap[i]])
                {
                    GUI.Label(new Rect(x, y, width, lineHeight), $"{Format(pi.Name), 32} {Format(pi.GetValue(_indexCacheMap[i],null)),-32}", _messageStyleAligLeft);
                    y += lineHeight;
                }
            }

        }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Add(SlugcatState ss, PropertyInfo pi)
        {
            if(_cacheMap.TryGetValue(ss, out var cached))
            {
                cached.Add(pi);
            }
            else
            {
                HashSet<PropertyInfo> hashSet = new HashSet<PropertyInfo>();
                _cacheMap.Add(ss,hashSet);
                hashSet.Add(pi);
                _indexCacheMap.Add(_cacheMap.Count - 1,ss);
            }
            RecalculateLayout();
        }
        public void Clear()
        {
            _cacheMap.Clear();
            _indexCacheMap.Clear();
            _cacheList.Clear();
            _isCalculated = false;
        }

        private void RecalculateLayout()
        {
            if(_messageStyle == null)
            {
                _messageStyle = UiTheme.createMessageStyle();
                _messageStyle.alignment = TextAnchor.MiddleCenter;
                _messageStyleAligLeft = UiTheme.createMessageStyle();
                _messageStyleAligLeft.alignment = TextAnchor.MiddleLeft;
                _messageStyleAligRight = UiTheme.createMessageStyle();
                _messageStyleAligRight.alignment = TextAnchor.MiddleRight;
            }
            _cacheList.Clear();
            for(int i = 0;i < _indexCacheMap.Count(); i++)
            {
                // 面板：宽 80%，高 70%，水平居中，距顶部 5%
                _panelWidth  = Mathf.Round(_messageStyle.CalcSize(new GUIContent(new string('W', 64 + 1))).x);
                _panelHeight = Mathf.Round(_messageStyle.lineHeight*(1 + _cacheMap[_indexCacheMap[i]].Count()));
                _panelX      = Mathf.Round(12f + _panelWidth * i);
                _panelY      = Mathf.Round(12f);
                // 面板内边距
                _padding = 12f;
                _contentX = Mathf.Round(_panelX + _padding);
                _contentY = Mathf.Round(_panelY + _padding);
                _contentWidth  = Mathf.Round(_panelWidth  - _padding * 2);
                _contentHeight = Mathf.Round(_panelHeight - _padding * 2);
                List<float> list = new List<float>
                {
                    _panelX,            // [i][0]
                    _panelY,            // [i][1]
                    _panelWidth,        // [i][2]
                    _panelHeight,       // [i][3]
                    _contentX,          // [i][4]
                    _contentY,          // [i][5]
                    _contentWidth,      // [i][6]
                    _contentHeight,     // [i][7]
                    _padding            // [i][8]
                };
                _cacheList.Add(list);
            }

            _isCalculated = true;
        }
        // 把值格式化成一行：浮点保留 3 位小数，集合/数组打印元素个数。
        private static string Format(object v)
        {
            if (v == null) return "null";
            if (v is string str) return str;
            if (v is bool b) return b ? "true" : "false";
            if (v is float f) return f.ToString("0.###", CultureInfo.InvariantCulture);
            if (v is double d) return d.ToString("0.###", CultureInfo.InvariantCulture);
            if (v is Array arr) return $"{v.GetType().Name}[{arr.Length}]";
            if (v is System.Collections.ICollection col) return $"{v.GetType().Name}({col.Count})";
            return v.ToString();
        }
    }
}