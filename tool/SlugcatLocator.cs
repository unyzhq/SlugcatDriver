using RWCustom;
using System.Reflection;
namespace SlugcatDriver.Tool
{
    public class SlugcatLocator
    {
        private static Type? _rmExtensions;
        private static MethodInfo? _rmGetOnlineObject;     // Extensions.GetOnlineObject(AbstractPhysicalObject) 静态
        private static FieldInfo?  _rmMePlayer;            // OnlineManager.mePlayer（静态字段）
        private static FieldInfo?  _rmOwner;               // OnlineEntity.owner（字段）
        private static PropertyInfo? _rmIsMine;            // OnlineEntity.isMine（属性）
        private static PropertyInfo? _rmIsAvatar;          // OnlineCreature.isAvatar（属性）
        private static bool _rmReady = false;
        private static bool _rmChecked = false;
        private static RainWorldGame? Game()
        {
            RainWorld rw = Custom.rainWorld;
            if (rw == null) return null;
            ProcessManager pm = rw.processManager;
            if (pm == null) return null;
            return pm.currentMainLoop as RainWorldGame;
        }
        private static bool IsRainMeadowReady()
        {
            if(_rmChecked) return _rmReady;
            _rmChecked = true;
            try
            {
                Assembly? asm = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                    if (a.GetName().Name == "Rain Meadow") { asm = a; break; } // 程序集名带空格
                if (asm == null) return false;

                _rmExtensions = asm.GetType("RainMeadow.Extensions");
                _rmGetOnlineObject = _rmExtensions?.GetMethod("GetOnlineObject", new[] { typeof(AbstractPhysicalObject) });
                _rmMePlayer = asm.GetType("RainMeadow.OnlineManager")
                                ?.GetField("mePlayer", BindingFlags.Public | BindingFlags.Static);
                Type? oe = asm.GetType("RainMeadow.OnlineEntity");
                _rmOwner = oe?.GetField("owner");
                _rmIsMine = oe?.GetProperty("isMine");
                Type? oc = asm.GetType("RainMeadow.OnlineCreature");
                _rmIsAvatar = oc?.GetProperty("isAvatar");

                _rmReady = _rmMePlayer != null && _rmGetOnlineObject != null;
                return _rmReady;
            }
            catch
            {
                _rmReady = false;
                return _rmReady;
            }
        }
        // RainMeadow 判定：这条生物是不是“我的”
        private static bool IsMineByRainMeadow(AbstractCreature? ac, out bool isAvatar)
        {
            isAvatar = false;
            if (!IsRainMeadowReady() || ac == null) return false;
            try
            {
                object? opo = _rmGetOnlineObject!.Invoke(null, new object[] { ac });   // ac 是 AbstractPhysicalObject 的子类
                if (opo == null) return false;

                if (_rmIsAvatar != null) isAvatar = (bool)_rmIsAvatar.GetValue(opo)!;
                if (_rmIsMine != null) return (bool)_rmIsMine.GetValue(opo)!;          // 最直接

                object? me = _rmMePlayer!.GetValue(null);
                object? owner = _rmOwner?.GetValue(opo);
                return me != null && ReferenceEquals(me, owner);
            }
            catch { return false; }
        }

        public static Player? GetPrimaryPlayer()
        {
            RainWorldGame? game = Game();
            if (game == null) return null;

            List<AbstractCreature> players = game.Players;
            if (players == null) return null;

            for (int i = 0; i < players.Count; i++)
            {
                AbstractCreature ac = players[i];
                if (ac == null) continue;
                if (IsMineByRainMeadow(ac, out bool av) && av && ac.realizedCreature is Player rp)
                    return rp;
                if (ac.controlled && ac.realizedCreature is Player p && !p.isNPC)
                    return p;
            }

            if (game.cameras != null && game.cameras.Length > 0 && game.cameras[0] != null)
            {
                AbstractCreature? cam = game.cameras[0].followAbstractCreature;      // RoomCamera.cs:275
                if (cam != null && cam.realizedCreature is Player cp) return cp;
            }

            return null;
        }
        public static Player? GetPlayerBySlot(int slot)
        {
            RainWorldGame? game = Game();
            if (game == null) return null;
            
            slot = slot - 1;
            List<AbstractCreature> players = game.Players;

            if (players == null || slot < 0 || slot >= players.Count) return null;

            AbstractCreature ac = players[slot];
            Player? p = ac != null ? ac.realizedCreature as Player : null;
            
            if (p == null) return null;
            return p;
        }
    }
}