using System;
using System.Collections.Generic;

namespace SlugcatDriver.Tool
{
    public delegate void CommandHandler(string[] args, ConsoleManager console);

    public static class CommandRegistry
    {
        private static readonly Dictionary<string, CommandHandler> _commands
            = new Dictionary<string, CommandHandler>(StringComparer.OrdinalIgnoreCase);

        static CommandRegistry()
        {
            // 注册内置指令
            Register("help", (args, con) =>
            {
                con.Log("可用指令：");
                foreach (var key in _commands.Keys)
                    con.Log($"  {key}");
            });

            Register("echo", (args, con) =>
            {
                con.Log(string.Join(" ", args));
            });

            Register("clear", (args, con) =>
            {
                // 需要 ConsoleManager 暴露一个 Clear 方法
                con.Clear();
            });
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
                catch (Exception e) { console.Log($"指令执行出错：{e.Message}"); }
            }
            else
            {
                console.Log($"未知指令：{commandName}，输入 help 查看可用指令");
            }
        }
    }
}