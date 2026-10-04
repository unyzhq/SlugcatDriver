using UnityEngine;
using MoreSlugcats;
using HarmonyLib;
using RWCustom;
using System.Reflection;


namespace SlugcatDriver.Tool
{
    /// <summary>
    /// 不许 AI 猫把玩家当道具抓。三层 Harmony 补丁，落点全在游戏本体：
    ///   L1 CanIPickThisUp  —— 资格层，把"玩家"从拾取候选里剔掉；
    ///   L2a SlugcatGrab    —— 创建层，所有 attach 调用点的汇流处；
    ///   L2b NPCForceGrab   —— 创建层，官方强制抓取入口，单独设防；
    ///   L3  Update         —— 清扫层，帧末兜底放掉"我们抓玩家"的手。
    /// </summary>
    internal static class DpcatMeowGrapPlayerFix
    {
        private static readonly Harmony harmony = new Harmony("SlugcatDriver.DpcatMeowGrapPlayerFix");

        /// <summary>总开关。</summary>
        public static bool Enabled = true;

        /// <summary>"这只猫的手是不是我们的 AI 在驱动"的判定，外部可整体替换。</summary>
        public static Func<Player, bool> IsAiCat = DefaultIsAiCat;

        /// <summary>唯一例外：返回 true 表示允许这只 AI 猫抓着那个玩家。默认一个都不允许。</summary>
        public static Func<Player, Player, bool> AllowGraspPlayer = delegate { return false; };

        private static bool DefaultIsAiCat(Player p)
        {
            if (p == null) return false;

            // ① 判断是否是AI
            if (p.abstractCreature?.abstractAI?.RealAI is SlugNPCAI) return true;

            // ② 官方 MoreSlugcats.SlugNPCAI 驱动的猫
            AbstractCreature? ac = p.abstractCreature;
            if (ac != null && ac.abstractAI != null && ac.abstractAI.RealAI is MoreSlugcats.SlugNPCAI)
                return true;

            return false;
        }

        /// <summary>该不该否决 "holder 抓 target" 这一次抓握。</summary>
        private static bool Block(Player holder, Player target)
        {
            if (!Enabled || holder == null || target == null || target == holder) return false;
            if (!IsAiCat(holder)) return false;
            return !AllowGraspPlayer(holder, target);
        }

        // ══════════════════════════════════════════════════════════════════

        public static void Install()
        {
            MethodInfo l1 = AccessTools.Method(typeof(Player), "CanIPickThisUp",
                new[] { typeof(PhysicalObject) });
            if (l1 != null)
                harmony.Patch(l1, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(DpcatMeowGrapPlayerFix), nameof(CanIPickThisUp_Post))));

            MethodInfo l2a = AccessTools.Method(typeof(Player), "SlugcatGrab",
                new[] { typeof(PhysicalObject), typeof(int) });
            if (l2a != null)
                harmony.Patch(l2a, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(DpcatMeowGrapPlayerFix), nameof(SlugcatGrab_Pre))));

            MethodInfo l2b = AccessTools.Method(typeof(Player), "NPCForceGrab",
                new[] { typeof(PhysicalObject) });
            if (l2b != null)
                harmony.Patch(l2b, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(DpcatMeowGrapPlayerFix), nameof(NPCForceGrab_Pre))));

            MethodInfo l3 = AccessTools.Method(typeof(Player), "Update",
                new[] { typeof(bool) });
            if (l3 != null)
                harmony.Patch(l3, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(DpcatMeowGrapPlayerFix), nameof(PlayerUpdate_Post))));
        }

        // ── 补丁实现 ──────────────────────────────────────────────────────

        /// <summary>L1：原判 true 时才需要改。</summary>
        private static void CanIPickThisUp_Post(Player __instance, PhysicalObject obj, ref bool __result)
        {
            if (!__result) return;
            Player? target = obj as Player;
            if (target == null || !Block(__instance, target)) return;
            __result = false;
        }

        /// <summary>L2a：返回 false = 跳过原方法 ⇒ 手不会 attach 上去。</summary>
        private static bool SlugcatGrab_Pre(Player __instance, PhysicalObject obj)
        {
            Player? target = obj as Player;
            if (target == null || !Block(__instance, target)) return true;
            return false;
        }

        /// <summary>L2b：NPCForceGrab 只查 dontGrabStuff 就挂手，这里单独设防。</summary>
        private static bool NPCForceGrab_Pre(Player __instance, PhysicalObject obj)
        {
            Player? target = obj as Player;
            if (target == null || !Block(__instance, target)) return true;
            return false;
        }

        /// <summary>L3：帧末兜底。</summary>
        private static void PlayerUpdate_Post(Player __instance) => Sweep(__instance);

        /// <summary>
        /// 把这只猫手里抓着的**玩家**全部放掉。只放"我们抓 Player"，
        /// 不碰"玩家抓/背我们"（那是 cat.grabbedBy）。
        /// </summary>
        public static void Sweep(Player cat)
        {
            if (!Enabled || cat == null || cat.grasps == null) return;
            if (!IsAiCat(cat)) return;

            for (int i = 0; i < cat.grasps.Length; i++)
            {
                Creature.Grasp g = cat.grasps[i];
                if (g == null) continue;

                Player? target = g.grabbed as Player;
                if (target == null || AllowGraspPlayer(cat, target)) continue;

                cat.ReleaseGrasp(i);
            }
        }
    }
}