using BepInEx.Logging;
using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using UnityEngine;
using RWCustom;
using System.Globalization;
using System.Text.RegularExpressions;


namespace SlugcatDriver.Tool
{
    public class DpcatMeowConfig : OptionInterface
    {
        public static DpcatMeowConfig? Instance { get; private set; }
        private readonly ManualLogSource Logger;

        public DpcatMeowConfig()
        {
            var isAIApplyToPlayerRange = new ConfigAcceptableRange<bool>(false,true);
            isAIApplyToPlayer1 = this.config.Bind<bool>("isAIApplyToPlayer1",false,isAIApplyToPlayerRange);
            isAIApplyToPlayer2 = this.config.Bind<bool>("isAIApplyToPlayer2",true,isAIApplyToPlayerRange);
            isAIApplyToPlayer3 = this.config.Bind<bool>("isAIApplyToPlayer3",false,isAIApplyToPlayerRange);
            isAIApplyToPlayer4 = this.config.Bind<bool>("isAIApplyToPlayer4",false,isAIApplyToPlayerRange);

            var idPlayerRange = new ConfigAcceptableRange<int>(0,9999);
            idPlayer1 = this.config.Bind<int>("idPlayer1", 0, idPlayerRange);
            idPlayer2 = this.config.Bind<int>("idPlayer2", 1, idPlayerRange);
            idPlayer3 = this.config.Bind<int>("idPlayer3", 2, idPlayerRange);
            idPlayer4 = this.config.Bind<int>("idPlayer4", 3, idPlayerRange);
            Instance = this;
        }
        public readonly Configurable<bool> isAIApplyToPlayer1;
        public readonly Configurable<int> idPlayer1;
        public readonly Configurable<bool> isAIApplyToPlayer2;
        public readonly Configurable<int> idPlayer2;
        public readonly Configurable<bool> isAIApplyToPlayer3;
        public readonly Configurable<int> idPlayer3;
        public readonly Configurable<bool> isAIApplyToPlayer4;
        public readonly Configurable<int> idPlayer4;
        private bool isChinese => Custom.rainWorld.options.language == InGameTranslator.LanguageID.Chinese;



        private UIelement[] UIArrPlayerOptions;
        
        // Initialize 是真实的函数，它会被游戏调用 OnDestory、Awake、Update则不存在，因为它不是MonoBehaviour
        public override void Initialize()
        {
            OpTab opTab = new OpTab(this, isChinese ? "通用设置" : "Options");
            this.Tabs = new[]
            {
                opTab
            };
            OpTextBox opTextBox1 = new OpTextBox(idPlayer1, new Vector2( 50f, 430f), 46f);
            opTextBox1.OnValueChanged += (ui, nv, ov) => {string s = idPlayer1.Value.ToString(CultureInfo.InvariantCulture);if (!Regex.IsMatch(ui.value, @"^[0-9]{1,4}$")) ui.value = s;};
            opTextBox1.maxLength = 4;
            OpTextBox opTextBox2 = new OpTextBox(idPlayer2, new Vector2( 200f, 430f), 46f);
            opTextBox2.OnValueChanged += (ui, nv, ov) => {string s = idPlayer2.Value.ToString(CultureInfo.InvariantCulture);if (!Regex.IsMatch(ui.value, @"^[0-9]{1,4}$")) ui.value = s;};
            opTextBox2.maxLength = 4;
            OpTextBox opTextBox3 = new OpTextBox(idPlayer3, new Vector2( 350f, 430f), 46f);
            opTextBox3.OnValueChanged += (ui, nv, ov) => {string s = idPlayer3.Value.ToString(CultureInfo.InvariantCulture);if (!Regex.IsMatch(ui.value, @"^[0-9]{1,4}$")) ui.value = s;};
            opTextBox3.maxLength = 4;
            OpTextBox opTextBox4 = new OpTextBox(idPlayer4, new Vector2( 500f, 430f), 46f);
            opTextBox4.OnValueChanged += (ui, nv, ov) => {string s = idPlayer4.Value.ToString(CultureInfo.InvariantCulture);if (!Regex.IsMatch(ui.value, @"^[0-9]{1,4}$")) ui.value = s;};
            opTextBox4.maxLength = 4;

            UIArrPlayerOptions = new UIelement[]
            {
                new OpLabel( 10f, 550f, isChinese ? "通用设置" : "Options", bigText: true), // 0
                new OpLabel( 50f, 500f, isChinese ? "玩家 1" : "Player1", false),           // 1
                new OpLabel(200f, 500f, isChinese ? "玩家 2" : "Player2", false),           // 2
                new OpLabel(350f, 500f, isChinese ? "玩家 3" : "Player3", false),           // 3
                new OpLabel(500f, 500f, isChinese ? "玩家 4" : "Player4", false),           // 4
                new OpCheckBox(isAIApplyToPlayer1, new Vector2( 50f, 465f)),               // 5
                new OpCheckBox(isAIApplyToPlayer2, new Vector2(200f, 465f)),               // 6
                new OpCheckBox(isAIApplyToPlayer3, new Vector2(350f, 465f)),               // 7
                new OpCheckBox(isAIApplyToPlayer4, new Vector2(500f, 465f)),               // 8
                new OpLabel( 90f, 468f, "AI", false),                                       // 9
                new OpLabel(240f, 468f, "AI", false),                                       // 10
                new OpLabel(390f, 468f, "AI", false),                                       // 11
                new OpLabel(540f, 468f, "AI", false),                                       // 12
                opTextBox1,                                                                 // 13
                opTextBox2,                                                                 // 14
                opTextBox3,                                                                 // 15
                opTextBox4,                                                                 // 16
                new OpLabel( 106f, 433f, "ID", false),                                       // 17
                new OpLabel(256f, 433f, "ID", false),                                       // 18
                new OpLabel(406f, 433f, "ID", false),                                       // 19
                new OpLabel(556f, 433f, "ID", false),                                       // 20
            };

            opTab.AddItems(UIArrPlayerOptions);
        }
    }
}