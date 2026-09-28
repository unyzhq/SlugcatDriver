using System.Reflection;

namespace SlugcatDriver.Tool
{
    // UI样式静态资源(窗口、面板、按钮、字体、颜色)
    public class PropertyIntrospector
    {
        private static Dictionary<Type,List<PropertyInfo>> _cacheMap = new Dictionary<Type,List<PropertyInfo>>();
        // 公开属性列表，按 MetadataToken 排序 = 元数据顺序 = 源码声明顺序。
        public static List<PropertyInfo> GetPropertyList(Type T)
        {
            // 键不存在时直接抛出错误，不会返回null
            if(_cacheMap.TryGetValue(T, out var cached)) return cached;
            var list = new List<PropertyInfo>(T.GetProperties(BindingFlags.Public | BindingFlags.Instance));
            list.RemoveAll(pi => pi.GetIndexParameters().Length > 0);
            list.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            _cacheMap[T] = list;
            return list;
        }
        public static PropertyInfo? GetProperty(Type T, string name)
        {
            PropertyInfo? pi = null;
            try
            {
                pi = T.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                return pi;
            }
            catch (AmbiguousMatchException)
            {
                return pi;
            }
        }
    }
}