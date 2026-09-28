namespace SlugcatDriver.Tool
{
    public static class SlugcatStateCache
    {
        private static Dictionary<Player,SlugcatState> _cacheMap = new Dictionary<Player,SlugcatState>();
        public static SlugcatState GetSlugcatStateByPlayer(Player p)
        {
            if(_cacheMap.TryGetValue(p,out SlugcatState ss))
            {
                return ss;
            }
            else
            {
                _cacheMap.Add(p,new SlugcatState(p));
                return _cacheMap[p];
            }
        }
    }
}