using BepInEx.Logging;
using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using UnityEngine;

namespace SlugcatDriver.Tool
{
    public class TemplateModOptions : OptionInterface
    {
        private readonly ManualLogSource Logger;

        public TemplateModOptions(SlugcatDriver modInstance, ManualLogSource loggerSource)
        {
            Logger = loggerSource;
            PlayerSpeed = this.config.Bind<float>("PlayerSpeed", 1f, new ConfigAcceptableRange<float>(0f, 100f));
        }

        public readonly Configurable<float> PlayerSpeed;
        private UIelement[] UIArrPlayerOptions;
        
        
        public override void Initialize()
        {
            var opTab = new OpTab(this, "Options");
            this.Tabs = new[]
            {
                opTab
            };

            UIArrPlayerOptions = new UIelement[]
            {
                new OpLabel(10f, 550f, "Options", true),
                new OpLabel(10f, 520f, "Player run speed factor"),
                new OpUpdown(PlayerSpeed, new Vector2(10f,490f), 100f, 1),
                
                new OpLabel(10f, 460f, "Gotta go fast!", false){ color = new Color(0.2f, 0.5f, 0.8f) }
            };
            opTab.AddItems(UIArrPlayerOptions);
            opTab.AddItems(new OpCheckBox(setting, pos), new OpLabel(pos.x + 30f, pos.y + 3f, desc ?? setting.info.description));
            opTab.AddItems(btns[0] = new OpRadioButton(pos), btns[1] = new OpRadioButton(pos.x + 80f, pos.y));
            opTab.AddItems(new OpLabel(10f, 550f, "ID to Name Mapping", bigText: true), new OpLabel(30f, 500f, "ID"), new OpLabel(140f, 500f, "Name"), new OpLabel(350f, 500f, "Creature Type"), new OpScrollBox(new Vector2(20f, 20f), new Vector2(500f, 430f), 0f), new OpLabel(10f, 520f, ""), new OpHoldButton(new Vector2(515f, 515f), 10f, "del all"), new OpTextBox(n0_id, new Vector2(15f, 475f), 100f), new OpTextBox(new Configurable<string>("1223"), new Vector2(125f, 475f), 200f), new OpSimpleButton(new Vector2(485f, 475f), new Vector2(40f, 24f), "add"), new OpSimpleButton(new Vector2(535f, 475f), new Vector2(40f, 24f), "del"), new OpLabelLong(new Vector2(10f, 10f), new Vector2(500f, 0f), ""), new OpComboBox(n0_crea, new Vector2(335f, 475f), 140f, new List<ListItem>(ExtEnum<CreatureTemplate.Type>.values.entries.Select((string s) => new ListItem(s)))));
        }

        public override void Update()
        {
            if (((OpUpdown)UIArrPlayerOptions[2]).GetValueFloat() > 10)
            {
                ((OpLabel)UIArrPlayerOptions[3]).Show();
            }
            else
            {
                ((OpLabel)UIArrPlayerOptions[3]).Hide();
            }
        }

    }
}
