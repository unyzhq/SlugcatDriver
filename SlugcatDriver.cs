using System.Security.Permissions;
using BepInEx;
using BepInEx.Configuration;
using SlugcatDriver.Tool;
using UnityEngine;
using RWCustom;

#pragma warning disable CS0618 // SecurityAction.RequestMinimum is obsolete. However, this does not apply to the mod, which still needs it. Suppress the warning indicating that it is obsolete.
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace SlugcatDriver {
	[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
	public class SlugcatDriverPlugin : BaseUnityPlugin
	{
		public const string PLUGIN_GUID = "unyzhq.SlugcatDriver";
		public const string PLUGIN_NAME = "Slugcat Driver";
		public const string PLUGIN_VERSION = "0.2.0";

		private ConfigEntry<KeyboardShortcut>? _toggleConsoleKey;
		private static bool _isInGame = false; // _isInGame 检查游戏状态
		private static bool _lastIsGame = false; // _lastInGame 保证只在切换时执行一次代码


		private void Awake()
		{
			Logger.LogInfo("Hello Rain World from " + PLUGIN_NAME + "!");

			// ConsoleManager.cs

			ConsoleManager console = gameObject.GetComponent<ConsoleManager>();
            if (console == null) console = gameObject.AddComponent<ConsoleManager>();

			console.SetLogMessage(message => Logger.LogMessage(message));
			console.SetLogInfo(message => Logger.LogInfo(message));
			console.SetLogDebug(message => Logger.LogDebug(message));
			console.SetLogWarning(message => Logger.LogWarning(message));
			console.SetLogError(message => Logger.LogError(message));

			// 默认绑定为 BackQuote（即 ~ 键），不按任何修饰键
            _toggleConsoleKey = Config.Bind(
                "General",
                "ToggleConsoleKey",
                // new KeyboardShortcut(KeyCode.BackQuote),
				new KeyboardShortcut(KeyCode.Tab),
                "打开/关闭控制台的按键"
            );

			var shortcut = _toggleConsoleKey.Value;
    		console.SetToggleKey(shortcut.MainKey);
			
			// ConsoleManager.cs

			// StateTracker.cs
			StateTracker stateTracker = gameObject.GetComponent<StateTracker>();
			if (stateTracker == null) gameObject.AddComponent<StateTracker>();
			// StateTracker.cs

			// DpcatMeow.cs
			DpcatMeow dpcatMeow = gameObject.GetComponent<DpcatMeow>();
			if (dpcatMeow == null) gameObject.AddComponent<DpcatMeow>();
			// DpcatMeow.cs
		}

		private void Update()
        {
			_isInGame = Custom.rainWorld?.processManager?.currentMainLoop is RainWorldGame;
            // _toggleConsoleKey 理论上已在 Awake 中赋值，这里做空值保护，避免异常中断 Update。
            var shortcut = _toggleConsoleKey?.Value;
            if (shortcut == null) return;

            if (shortcut.Value.IsDown() && ConsoleManager.Instance != null && _isInGame)
			{
				ConsoleManager.Instance.Open();
			}
			if (DpcatMeow.Instance != null && !DpcatMeow.Instance.isInited && _isInGame)
			{
				DpcatMeow.Instance.InitDpcat();
			}
			if (ConsoleManager.Instance != null && _lastIsGame != _isInGame && !_isInGame)
			{
				ConsoleManager.Instance.Close();
			}
			if(StateTracker.Instance != null && _lastIsGame != _isInGame && !_isInGame)
			{
				StateTracker.Instance.Clear();
			}
			if(DpcatMeow.Instance != null && _lastIsGame != _isInGame && !_isInGame)
			{
				DpcatMeow.Instance.Clear();
			}
			_lastIsGame = _isInGame;
        }
	}
}
