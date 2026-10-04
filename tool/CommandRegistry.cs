using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MoreSlugcats;
using RWCustom;       // Custom.rainWorld（RWCustom.Custom，Custom.cs:18）
using UnityEngine;
using UnityEngineInternal.Video;    // Object.FindObjectOfType 兜底

namespace SlugcatDriver.Tool
{
    public delegate void CommandHandler(string[] args, ConsoleManager console);

    public static class CommandRegistry
    {
        private static readonly Dictionary<string, CommandHandler> _commands
            = new Dictionary<string, CommandHandler>(StringComparer.OrdinalIgnoreCase);
        static CommandRegistry()
        {
            // ── 内置指令（保持原样） ─────────────────────────────────────
            Register("help", (args, console) =>
            {
                foreach (var key in _commands.Keys)
                    console.LogInfo($"{key}");
            });

            Register("echo", (args, console) =>
            {
                console.LogInfo(string.Join(" ", args));
            });

            Register("clear", (args, console) =>
            {
                console.Clear();
            });

            Register("color", (args, console) =>
            {
                console.LogMessage("message");
                console.LogInfo("info");
                console.LogDebug("debug");
                console.LogWarning("warning");
                console.LogError("error");
            });

            // ── state：打印/监测蛞蝓猫状态 ──────────────────────────────────
            //   state                      世界里唯一那只蛞蝓猫的全部属性
            //   state all                  同上
            //   state <属性名>             该属性的值（旧用法）
            //   state <目标>               目标的全部属性       目标 = player1/player2/… | hand | back
            //   state <目标> <属性名>      打印该值，并在左上角【持续监测】它（面板）
            //   state clear                清空所有监测面板
            Register("state", (args, console) => StateCommand(args, console));

        }

        public static void Register(string name, CommandHandler handler)
        {
            _commands[name.ToLowerInvariant()] = handler;
        }

        public static void Execute(string raw, ConsoleManager console)
        {
            var parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var commandName = parts[0];
            var args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);

            if (_commands.TryGetValue(commandName, out var handler))
            {
                try { handler(args, console); }
                catch (Exception e) { console.LogError($"Command execution error, return message {e.Message}"); }
            }
            else
            {
                console.LogMessage($"Command not found! Try typing 'help'.");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  state 命令实现
        // ══════════════════════════════════════════════════════════════
        private static void StateCommand(string[] args, ConsoleManager console)
        {
            // —— state ——————————————————————————————————————————————————————
            if(args.Length < 1 || args.Length > 2)
            {
                console.LogMessage($"用法：\nstate <目标> 打印目标所有字段值\nstate <目标> <字段> 打印并持续监测目标字段值\n<目标> : \nplayerN 数字对应每个玩家槽位\nhand 抓住的蛞蝓猫\nback 背上的蛞蝓猫\nclear 清空所有在监测字段");
                return;
            }
            // ── state clear：清空所有监测面板 ───────────────────────────────
            if (args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                if(args.Length == 1)
                {
                    StateTracker.Instance?.Clear();
                    console.LogMessage("已清空监测面板");
                }
                else
                {
                    console.LogMessage($"用法：\nstate <目标> 打印目标所有字段值\nstate <目标> <字段> 打印并持续监测目标字段值\n<目标> : \nplayerN 数字对应每个玩家槽位\nhand 抓住的蛞蝓猫\nback 背上的蛞蝓猫\nclear 清空所有在监测字段");
                }
                
                return;
            }
            // ── 第一个参数是目标吗（playerN / hand / back）？ ──────────────────
            // —— state player1 ────────────────────────────────────────────────
            Player? p = null;
            bool isArgs0Legal = false;
            if (args[0].Length > 6 && args[0].StartsWith("player", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[0].Substring(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            {
                isArgs0Legal = true;
                p = SlugcatLocator.GetPlayerBySlot(n);
            }
            if (args[0].Equals("hand", StringComparison.OrdinalIgnoreCase))
            {
                isArgs0Legal = true;
                Player? customPlayer = SlugcatLocator.GetPrimaryPlayer();
                if(customPlayer != null)
                {
                    for (int i = 0; i < customPlayer.grasps.Length; i++)
                    {
                        if (customPlayer.grasps[i] == null) continue;
                        if (customPlayer.grasps[i].grabbed is Player held) p = held;
                    }
                }
            }
            if (args[0].Equals("back", StringComparison.OrdinalIgnoreCase))
            {
                isArgs0Legal = true;
                Player? customPlayer = SlugcatLocator.GetPrimaryPlayer();
                if(customPlayer != null) p = customPlayer.slugOnBack.slugcat;
            }
            if(p == null)
            {
                if(isArgs0Legal)
                    console.LogError("目标蛞蝓猫不存在");
                else
                    console.LogMessage($"用法：\nstate <目标> 打印目标所有字段值\nstate <目标> <字段> 打印并持续监测目标字段值\n<目标> : \nplayerN 数字对应每个玩家槽位\nhand 抓住的蛞蝓猫\nback 背上的蛞蝓猫\nclear 清空所有在监测字段");
                return;
            }
            if(p != null && args.Length == 1)
            {
                SlugcatState s = SlugcatStateCache.GetSlugcatStateByPlayer(p);

                DumpAllProperties(s, console);
                return;
            }
            PropertyInfo? pi = PropertyIntrospector.GetProperty(typeof(SlugcatState),args[1]);
            if(p != null && pi != null)
            {
                SlugcatState s = SlugcatStateCache.GetSlugcatStateByPlayer(p);
                DumpOneProperty(s, pi, console);
                StateTracker.Instance?.Add(s,pi);
                return;
            }
            console.LogError($"属性 {args[1]} 不存在");
            return;
        }

        private static void DumpAllProperties(SlugcatState s, ConsoleManager console)
        {
            List<PropertyInfo> props = PropertyIntrospector.GetPropertyList(typeof(SlugcatState));
            console.LogInfo($"[state] 全部 {props.Count} 个属性（按 SlugcatState.cs 声明顺序）:");

            var sw = Stopwatch.StartNew();
            int ok = 0, failed = 0;
            foreach (PropertyInfo pi in props)
            {
                string text;
                try
                {
                    text = Format(pi.GetValue(s, null));
                    ok++;
                }
                catch (TargetInvocationException tie)
                {
                    Exception ex = tie.InnerException ?? tie;
                    text = $"!! {ex.GetType().Name}: {ex.Message}";
                    failed++;
                }
                catch (Exception e)
                {
                    text = $"!! {e.GetType().Name}: {e.Message}";
                    failed++;
                }
                console.LogInfo($"    {pi.Name,-34} = {text}");
            }
            sw.Stop();

            console.LogInfo($"[state] 属性 {props.Count} 个：成功 {ok}，异常 {failed}，耗时 {sw.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)} ms");
            if (failed > 0)
                console.LogWarning($"[state] {failed} 个属性读取失败，这些字段在当前上下文不可用");
        }

        private static bool DumpOneProperty(SlugcatState s, PropertyInfo pi, ConsoleManager console)
        {
            string text;
            try
            {
                text = Format(pi.GetValue(s, null));
            }
            catch (TargetInvocationException tie)
            {
                Exception ex = tie.InnerException ?? tie;
                text = $"!! {ex.GetType().Name}: {ex.Message}";
            }
            catch (Exception e)
            {
                 text = $"!! {e.GetType().Name}: {e.Message}"; 
            }

            console.LogInfo($"[state] {pi.Name} ({pi.PropertyType.Name}) = {text}");
            return true;
        }

        // <summary>把值格式化成一行：浮点保留 3 位小数，集合/数组打印元素个数。</summary>
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