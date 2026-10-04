using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace SlugcatDriver.Tool
{
    /// <summary>
    /// 让 isNPC 说真话的同时，允许相机停在"我们的 AI 猫"上。
    /// 主路：转译 RoomCamera.Update 里那处 get_isNPC，让"我们的 AI 猫"在相机眼里算玩家。
    /// 兜底：帧首记忆相机目标，帧末发现被官方抢走就还原（不碰 IL）。
    /// </summary>
    internal static class DpcatMeowCameraFix
    {
        private static readonly Harmony harmony = new Harmony("SlugcatDriver.DpcatMeowCameraFix");
        /// <summary>总开关。</summary>
        public static bool Enabled = true;

        /// <summary>主路（转译）是否命中。</summary>
        public static bool Transpiled;

        /// <summary>"这只猫是不是我们的 AI 在驱动"，外部可整体替换。</summary>
        public static Func<Player, bool> IsAiCat = DefaultIsAiCat;

        private static readonly ConditionalWeakTable<RoomCamera, TargetBox> _lastTarget
            = new ConditionalWeakTable<RoomCamera, TargetBox>();

        private sealed class TargetBox
        {
            public AbstractCreature? Target;
        }

        private static bool DefaultIsAiCat(Player p)
        {            
            if (p == null) return false;

            AbstractCreature ac = p.abstractCreature;
            if (ac != null && ac.abstractAI != null && ac.abstractAI.RealAI is MoreSlugcats.SlugNPCAI)
                return true;

            return false;
        }

        // ══════════════════════════════════════════════════════════════════

        public static void Install()
        {
            MethodInfo target = AccessTools.Method(typeof(RoomCamera), "Update", Type.EmptyTypes);
            if (target != null)
            {
                harmony.Patch(target, transpiler: new HarmonyMethod(
                    AccessTools.Method(typeof(DpcatMeowCameraFix), nameof(Update_Transpiler))));

                if (Transpiled) return;
            }

            InstallFallback(harmony);
        }

        private static void InstallFallback(Harmony harmony)
        {
            MethodInfo update = AccessTools.Method(typeof(RoomCamera), "Update", Type.EmptyTypes);
            MethodInfo change = AccessTools.Method(typeof(RoomCamera), "ChangeCameraToPlayer",
                new[] { typeof(AbstractCreature) });
            if (update == null || change == null) return;

            harmony.Patch(update,
                prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(DpcatMeowCameraFix), nameof(Update_Pre))),
                postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(DpcatMeowCameraFix), nameof(Update_Post))));
            harmony.Patch(change, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(DpcatMeowCameraFix), nameof(ChangeCameraToPlayer_Post))));
        }

        // ── 主路：转译 ──────────────────────────────────────────────────────

        private static IEnumerable<CodeInstruction> Update_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getter = AccessTools.PropertyGetter(typeof(Player), "isNPC");
            MethodInfo replacement = AccessTools.Method(
                typeof(DpcatMeowCameraFix), nameof(CameraSeesAsNpc));

            var list = new List<CodeInstruction>(instructions);
            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction ci = list[i];
                if (ci.opcode != OpCodes.Call && ci.opcode != OpCodes.Callvirt) continue;

                MethodInfo? mi = ci.operand as MethodInfo;
                if (mi == null) continue;

                bool isNpcGetter = mi == getter
                    || (mi.DeclaringType == typeof(Player) && mi.Name == "get_isNPC");
                if (!isNpcGetter) continue;

                var swapped = new CodeInstruction(OpCodes.Call, replacement);
                swapped.labels.AddRange(ci.labels);
                swapped.blocks.AddRange(ci.blocks);
                list[i] = swapped;
                Transpiled = true;
            }
            return list;
        }

        /// <summary>
        /// 相机眼里的"这猫算不算 NPC"。官方语义原样，只对"我们的 AI 猫"返回 false。
        /// 只被转译后的 RoomCamera.Update 调用。
        /// </summary>
        public static bool CameraSeesAsNpc(Player player)
        {
            if (player == null) return false;
            if (Enabled && IsAiCat(player)) return false;
            return player.isNPC;
        }

        // ── 兜底：帧首记忆 + 帧末还原 ────────────────────────────────────────

        private static void Remember(RoomCamera cam, AbstractCreature target)
        {
            TargetBox box = _lastTarget.GetOrCreateValue(cam);
            box.Target = target;
        }

        private static void Update_Pre(RoomCamera __instance)
        {
            if (Enabled && __instance != null)
                Remember(__instance, __instance.followAbstractCreature);
        }

        private static void Update_Post(RoomCamera __instance)
        {
            if (!Enabled || __instance == null || __instance.InCutscene) return;

            TargetBox box;
            if (!_lastTarget.TryGetValue(__instance, out box) || box.Target == null) return;
            if (__instance.followAbstractCreature == box.Target) return;

            Player? p = box.Target.realizedCreature as Player;
            if (p == null || p.dead) return;

            PlayerState st = p.playerState;
            if (st != null && st.permaDead) return;
            if (!IsAiCat(p)) return;

            __instance.followAbstractCreature = box.Target;
        }

        private static void ChangeCameraToPlayer_Post(RoomCamera __instance, AbstractCreature cameraTarget)
        {
            if (Enabled && __instance != null)
                Remember(__instance, cameraTarget);
        }
    }
}