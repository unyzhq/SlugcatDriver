using UnityEngine;
using MoreSlugcats;
using HarmonyLib;
using RWCustom;
using System.Reflection;
using System.Reflection;
using System.Runtime.Serialization;

namespace SlugcatDriver.Tool
{
    public class DpcatMeow : MonoBehaviour
    {
        public static DpcatMeow? Instance { get; private set; }
        public bool isInited = false;
        public Dictionary<int,Player>? playerMap;
        private Dictionary<Player,int>? playerNumMap;
        // 替身变量 用于绕过 Grabability (obj as Player).SlugCatClass == MoreSlugcatsEnums.SlugcatStatsName.Slugpup 检测
        // 不能更改 SlugCatClass 一，是因为这代表职业 二，是因为强行改为Slugpup会卡死游戏
        private DataPearl _dataPearl = (DataPearl)FormatterServices.GetUninitializedObject(typeof(DataPearl));
        // 当前是否处于官方的Standard过场动画中？
        private bool _isOfficalInCutsceneStandard = false;
        // 当前是否处于本模组的Standard过场动画(镜头修复)中？
        private bool _isMineInCutsceneStandard = false;
        // 手动维护PlayerNPCState
        private Dictionary<int,PlayerNPCState> _playerNPCStateMap;
        private CreatureTemplate _origCreatureTemplate;

        public bool isAIApplyToPlayer1 = false;
        public int idPlayer1 = 0;
        public bool isAIApplyToPlayer2 = true;
        public int idPlayer2 = 1;
        public bool isAIApplyToPlayer3 = false;
        public int idPlayer3 = 2;
        public bool isAIApplyToPlayer4 = false;
        public int idPlayer4 = 3;
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            playerMap = new Dictionary<int, Player>();
            playerNumMap = new Dictionary<Player, int>();
            _playerNPCStateMap = new Dictionary<int, PlayerNPCState>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Clear()
        {
            isInited = false;
            _isMineInCutsceneStandard = false;
        }
        public void InitDpcat()
        {
            if(isInited || !GameplayReady()) return;
            playerMap?.Clear();     // 前置清理，放在这以避免在需要用的时候被Clear()清空
            playerNumMap?.Clear();  // 因为我们很难判断Clear会在何时执行
            
            DpcatMeowConfig dpcatMeowConfig = DpcatMeowConfig.Instance;
            if(dpcatMeowConfig != null)
            {
                isAIApplyToPlayer1 = dpcatMeowConfig.isAIApplyToPlayer1.Value;
                isAIApplyToPlayer2 = dpcatMeowConfig.isAIApplyToPlayer2.Value;
                isAIApplyToPlayer3 = dpcatMeowConfig.isAIApplyToPlayer3.Value;
                isAIApplyToPlayer4 = dpcatMeowConfig.isAIApplyToPlayer4.Value;
                idPlayer1 = dpcatMeowConfig.idPlayer1.Value;
                idPlayer2 = dpcatMeowConfig.idPlayer2.Value;
                idPlayer3 = dpcatMeowConfig.idPlayer3.Value;
                idPlayer4 = dpcatMeowConfig.idPlayer4.Value;
            }
            if (isAIApplyToPlayer1)
                InitDpcatByNumber(1);
            if (isAIApplyToPlayer2)
                InitDpcatByNumber(2);
            if (isAIApplyToPlayer3)
                InitDpcatByNumber(3);
            if (isAIApplyToPlayer4)
                InitDpcatByNumber(4);
            
            isInited = true;
        }
        private void InitDpcatByNumber(int num)
        {
            if(num < 1 || num > 4 || playerMap == null || playerNumMap == null) return;
            Player? p = SlugcatLocator.GetPlayerBySlot(num); // 从1开始
            AbstractCreature? ac = p?.abstractCreature;
            if(p == null || ac == null || p.room == null || !p.room.readyForAI || p.room.aimap == null /**|| !(ac.realizedCreature is Player)**/)
            {
                return;
            }
            playerMap.Add(num,p);
            playerNumMap.Add(p,num);
            if(_origCreatureTemplate == null) _origCreatureTemplate = ac.creatureTemplate;
            // 1) 先换模板
            // creatureTemplate.AI 变 true AbstractCreature.cs:833 的抽象目的地/路径维护才会跑
            // Player.isNPC 变 true Player.cs:1957-1966 → 相机/HUD 的 NPC 分支对齐
            ac.creatureTemplate = StaticWorld.GetCreatureTemplate(MoreSlugcatsEnums.CreatureTemplateType.SlugNPC);
            // 2) 再换ID
            int id = num switch
            {
                1 => idPlayer1,
                2 => idPlayer2,
                3 => idPlayer3,
                4 => idPlayer4,
                _ => 0,
            };
            ac.ID = new EntityID(-1,id);
            ac.personality = new AbstractCreature.Personality(ac.ID);
            // 3) 再建 state —— 顺序反了 socialMemory 仍然是 null（CreatureState.cs:35）
            if(!_playerNPCStateMap.TryGetValue(num,out PlayerNPCState playerNPCState))
            {
                int onlyPlayerNumber = -1;
                if (!isAIApplyToPlayer1)
                    onlyPlayerNumber = 0;
                else if (!isAIApplyToPlayer2)
                    onlyPlayerNumber = 1;
                else if (!isAIApplyToPlayer3)
                    onlyPlayerNumber = 2;
                else if (!isAIApplyToPlayer4)
                    onlyPlayerNumber = 3;
                int playerNumber = p.playerState != null ? p.playerState.playerNumber : 0;
                var st = new PlayerNPCState(ac, playerNumber);      // PlayerNPCState : PlayerState（PlayerNPCState.cs:7/27） 这一行只继承ac
                st.isPup = p.playerState != null && p.playerState.isPup; // 继承体型
                st.slugcatCharacter = p.playerState != null ? p.playerState.slugcatCharacter : p.SlugCatClass; // 继承名字(惬意合作 这个名字也决定相关样式 不继承样式会出问题)
                st.foodInStomach = 1; // 初始饱食度
                SocialMemory.Relationship orInitiateRelationship = st.socialMemory.GetOrInitiateRelationship(p.room.game.Players[onlyPlayerNumber == -1 ? 0 : onlyPlayerNumber].ID); // 初始化与玩家的关系，返回的是引用
                orInitiateRelationship.InfluenceLike(1f); // 因子(影响权重)
                orInitiateRelationship.InfluenceTempLike(1f); // 因子(影响权重)
                orInitiateRelationship.like = 0.52f; // 超过0.5就是朋友 like > 0.5 && tempLike > 0.5
                orInitiateRelationship.tempLike = 0.52f; // 超过0.5就是朋友  like > 0.5 && tempLike > 0.5
                ac.state = st;
            }
            else
            {
                ac.state = playerNPCState;
            }

            // 4) 抽象层：用 pup 专用的 SlugNPCAbstractAI（自带 isTamed/跟随/回巢；普通 AbstractCreatureAI 缺这些）
            ac.abstractAI = new SlugNPCAbstractAI(ac.world, ac);

            // 5) 具体 AI（ctor 里会自己写 abstractAI.RealAI = this，SlugNPCAI.cs:442）
            ac.abstractAI.RealAI = new SlugNPCAI(ac, ac.world);

            // 6) 补 realizedRoom（PathFinder.cs:797 → Reset(): realizedRoom = room） 没有这个会崩
            ac.abstractAI.RealAI.NewRoom(p.room);        // PathFinder.cs:797 → Reset(): realizedRoom = room（:808）
        }
        public void DpcatMeowPatch()
        {
            // 修复‘猫崽把玩家当物品抓’
            On.MoreSlugcats.SlugNPCAI.CanGrabItem += new On.MoreSlugcats.SlugNPCAI.hook_CanGrabItem(SlugNPCAICanGrabItemPatch);
            // 修复‘抓崽困难’
            On.Player.CanIPickThisUp += new On.Player.hook_CanIPickThisUp(PlayerCanIPickThisUpPatch);
            // 修复‘相机对猫崽无效’
            On.RoomCamera.ChangeCameraToPlayer += new On.RoomCamera.hook_ChangeCameraToPlayer(RoomCameraChangeCameraToPlayerPatch);
            // 修复补丁‘相机对猫崽无效’导致的‘猫崽相机无法切回’
            On.RoomCamera.EnterCutsceneMode += new On.RoomCamera.hook_EnterCutsceneMode(RoomCameraEnterCutsceneModePatch);
            On.RoomCamera.ExitCutsceneMode += new On.RoomCamera.hook_ExitCutsceneMode(RoomCameraExitCutsceneModePatch);
            On.Player.JollyInputUpdate += new On.Player.hook_JollyInputUpdate(PlayerJollyInputUpdatePatch);
            // 修复‘猫崽不能雨眠’
            On.Player.Update += new On.Player.hook_Update(PlayerUpdatePatch);
            // 修复‘雨眠后出现复制猫崽’
            On.RegionState.AdaptRegionStateToWorld += new On.RegionState.hook_AdaptRegionStateToWorld(RegionStateAdaptRegionStateToWorldPatch);
            // 修复‘死亡图标复活不活’
            On.JollyCoop.JollyHUD.JollyMeter.PlayerIcon.Update += new On.JollyCoop.JollyHUD.JollyMeter.PlayerIcon.hook_Update(PlayerIconUpdatePatch);
            // 修复‘观察猫崽时相机箭头变锁’
            On.JollyCoop.JollyHUD.JollyMeter.Update += new On.JollyCoop.JollyHUD.JollyMeter.hook_Update(JollyMeterUpdatePatch);
            // 修复‘官方相机只能在自己和下一位之间轮换’
            On.Player.TriggerCameraSwitch += new On.Player.hook_TriggerCameraSwitch(PlayerTriggerCameraSwitchPatch);
            
        }
        private static bool SlugNPCAICanGrabItemPatch(On.MoreSlugcats.SlugNPCAI.orig_CanGrabItem orig, MoreSlugcats.SlugNPCAI self, PhysicalObject obj)
        {
            return orig.Invoke(self,obj) && !(obj is Player player); // 不抓玩家
        }
        private bool PlayerCanIPickThisUpPatch(On.Player.orig_CanIPickThisUp orig, Player self, PhysicalObject obj)
        {
            Player? targetPlayer = obj as Player;
            // 抓猫崽时执行（仅本模组猫崽）因为本模组猫崽SlugCatClass不是Slugpup，导致原函数这段代码不会执行，此为修复补丁
            if(targetPlayer != null && self != targetPlayer && !(self.abstractCreature?.abstractAI?.RealAI is SlugNPCAI) && playerMap != null && playerMap.ContainsValue(targetPlayer))
            {
                if (self.slugOnBack != null && self.slugOnBack.slugcat == obj)
                {
                    return false;
                }
                if (self.onBack == targetPlayer || targetPlayer.onBack != null)
                {
                    return false;
                }
                for (int num3 = 0; num3 < self.grabbedBy.Count; num3++)
                {
                    if (obj == self.grabbedBy[0].grabber)
                    {
                        return false;
                    }
                }
                for (int num4 = 0; num4 < 2; num4++)
                {
                    if (self.grasps[num4] != null && self.grasps[num4].grabbed is Player && self.grasps[num4].grabbed != obj)
                    {
                        return self.CanPutSlugToBack;
                    }
                }
                // 如果以上条件不成立，那么其余逻辑和虚拟神经元完全相同（为什么不直接用猫崽？原因是猫崽无法通过函数首行的Grabability检测）
                // 这里的相同，同时包括前面相同(不会提前返回)和后面相同(完全等效)
                return orig.Invoke(self,_dataPearl);
            }
            return orig.Invoke(self,obj);
        }
        private void RoomCameraChangeCameraToPlayerPatch(On.RoomCamera.orig_ChangeCameraToPlayer orig, RoomCamera self, AbstractCreature cameraTarget)
        {
            if(cameraTarget.abstractAI?.RealAI is SlugNPCAI && IsSlugNPCInPlayerMap(cameraTarget))
            {
                _isMineInCutsceneStandard = true;
                self.EnterCutsceneMode(cameraTarget,RoomCamera.CameraCutsceneType.Standard);
            }
            else if(_isMineInCutsceneStandard)
            {
                _isMineInCutsceneStandard = false;
                self.ExitCutsceneMode();
            }
            orig.Invoke(self,cameraTarget);
        }
        private void RoomCameraEnterCutsceneModePatch(On.RoomCamera.orig_EnterCutsceneMode orig, RoomCamera self, AbstractCreature cutscenePlayer, RoomCamera.CameraCutsceneType type)
        {
            if(IsSlugNPCInPlayerMap(cutscenePlayer))
            {
                orig.Invoke(self,cutscenePlayer,type);
                return;
            }
            
            _isOfficalInCutsceneStandard = type == RoomCamera.CameraCutsceneType.Standard;
            orig.Invoke(self,cutscenePlayer,type);
        }
        private void RoomCameraExitCutsceneModePatch(On.RoomCamera.orig_ExitCutsceneMode orig, RoomCamera self)
        {
            _isOfficalInCutsceneStandard = false;
            orig.Invoke(self);
        }
        private void PlayerJollyInputUpdatePatch(On.Player.orig_JollyInputUpdate orig, Player self)
        {
            orig.Invoke(self);
            // cameraSwitchDelay == -1 // 私有变量
            int cameraSwitchDelay = (int)typeof(Player).GetField("cameraSwitchDelay",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self);
            // 原：!self.room.world.game.cameras[0].InCutscene
            // 现：(!self.room.world.game.cameras[0].InCutscene || (_isMineInCutsceneStandard && !_isOfficalInCutsceneStandard))
            // 仅在_isMineInCutsceneStandard为真并且_isOfficalInCutsceneStandard为假时豁免InCutscene非空的情况(即过场动画非空 相机补丁利用了过场动画)
            if (!self.input[0].mp && self.input[1].mp && cameraSwitchDelay == -1 && (!self.room.world.game.cameras[0].InCutscene || (_isMineInCutsceneStandard && !_isOfficalInCutsceneStandard)) && self.room.world.game.cameras[0].coopRippleDimensionPlayer == null)
            {
                int num = 0;
                
                // jollyButtonDown = false // 私有变量
                typeof(Player).GetField("jollyButtonDown",BindingFlags.NonPublic | BindingFlags.Instance).SetValue(self,false);

                for (int j = 2; j < self.input.Length && self.input[j].mp; j++)
                {
                    num++;
                }
                if (num <= self.CameraInputDelay)
                {
                    // cameraSwitchDelay = 5; // 私有变量
                    typeof(Player).GetField("cameraSwitchDelay",BindingFlags.NonPublic | BindingFlags.Instance).SetValue(self,5);
                }
            }
        }
        private void PlayerUpdatePatch(On.Player.orig_Update orig, Player self, bool eu)
        {
            if (playerMap.ContainsValue(self))
            {
                // 我们的蛞蝓猫会因为 AI != null 而不执行下列代码，因此，手动修复
                if(self.room.abstractRoom.shelter && self.room.game.IsStorySession && !self.dead && !self.Sleeping && self.room.shelterDoor != null && !self.room.shelterDoor.Broken)
                {
                    int timeSinceInCorridorMode = (int)typeof(Player).GetField("timeSinceInCorridorMode",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self);
                    if (Custom.ManhattanDistance(self.abstractCreature.pos.Tile, self.room.LocalCoordinateOfNode(0).Tile) > 6 && (!ModManager.MMF || timeSinceInCorridorMode > 10) && ShelterDoor.IsTileInsideShelterRange(self.room.abstractRoom, self.abstractCreature.pos.Tile))
                    {
                        if(ModManager.CoopAvailable && self.FoodInRoom(self.room, eatAndDestroy: false) >= self.slugcatStats.foodToHibernate)
                        {
                            // self.ReadyForWinJolly = true; // 私有属性
                            typeof(Player).GetProperty("ReadyForWinJolly",BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(self,true);
                            PlayerNPCState? playerNPCState = self.abstractCreature.state as PlayerNPCState;
                            
                            if(playerNPCState != null && playerNumMap != null && playerNumMap.TryGetValue(self,out int num) && _playerNPCStateMap != null && _playerNPCStateMap.TryGetValue(num,out _))
                            {
                                playerNPCState.foodInStomach = playerNPCState.foodInStomach - self.slugcatStats.foodToHibernate;
                                _playerNPCStateMap[num] = playerNPCState;
                            }
                        }
                    }
                }
            }
            orig.Invoke(self,eu);
        }
        private void RegionStateAdaptRegionStateToWorldPatch(On.RegionState.orig_AdaptRegionStateToWorld orig, RegionState self, int playerShelter, int activeGate)
        {
            // 换成源模板，这样 abstractCreature.creatureTemplate.TopAncestor().type == MoreSlugcatsEnums.CreatureTemplateType.SlugNPC 就不成立
            // 如此，我们的猫崽就不会当做NPC加入下循环的猫崽生成列表中
            if (isAIApplyToPlayer1 && playerMap != null && playerMap.TryGetValue(1,out Player p1))
                p1.abstractCreature.creatureTemplate = _origCreatureTemplate;
            if (isAIApplyToPlayer2 && playerMap != null && playerMap.TryGetValue(2,out Player p2))
                p2.abstractCreature.creatureTemplate = _origCreatureTemplate;
            if (isAIApplyToPlayer3 && playerMap != null && playerMap.TryGetValue(3,out Player p3))
                p3.abstractCreature.creatureTemplate = _origCreatureTemplate;
            if (isAIApplyToPlayer4 && playerMap != null && playerMap.TryGetValue(4,out Player p4))
                p4.abstractCreature.creatureTemplate = _origCreatureTemplate;
            orig.Invoke(self,playerShelter,activeGate);
        }
            
        private void PlayerIconUpdatePatch(On.JollyCoop.JollyHUD.JollyMeter.PlayerIcon.orig_Update orig, JollyCoop.JollyHUD.JollyMeter.PlayerIcon self)
        {
            orig.Invoke(self);
            PlayerState playerState = (PlayerState)typeof(JollyCoop.JollyHUD.JollyMeter.PlayerIcon).GetProperty("playerState",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self);
            bool dead = (bool)typeof(JollyCoop.JollyHUD.JollyMeter.PlayerIcon).GetField("dead",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self);
            if (dead && !playerState.permaDead && !playerState.dead)
            {
                self.iconSprite.RemoveFromContainer();
                self.iconSprite = new FSprite("Kill_Slugcat");
                JollyCoop.JollyHUD.JollyMeter meter = (JollyCoop.JollyHUD.JollyMeter)typeof(JollyCoop.JollyHUD.JollyMeter.PlayerIcon).GetField("meter",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self);
                FContainer fContainer = (FContainer)typeof(JollyCoop.JollyHUD.JollyMeter).GetField("fContainer",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(meter);
                fContainer.AddChild(self.iconSprite);
                typeof(JollyCoop.JollyHUD.JollyMeter.PlayerIcon).GetField("dead",BindingFlags.NonPublic | BindingFlags.Instance).SetValue(self,false);
            }
        }
        private void JollyMeterUpdatePatch(On.JollyCoop.JollyHUD.JollyMeter.orig_Update orig, JollyCoop.JollyHUD.JollyMeter self)
        {
            orig.Invoke(self);
            if(_isMineInCutsceneStandard && !_isOfficalInCutsceneStandard)
            {
                FSprite cameraArrowSprite = (FSprite)typeof(JollyCoop.JollyHUD.JollyMeter).GetField("cameraArrowSprite",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self);
                cameraArrowSprite.element = Futile.atlasManager.GetElementWithName("Multiplayer_Arrow");
                typeof(JollyCoop.JollyHUD.JollyMeter).GetField("cutscene",BindingFlags.NonPublic | BindingFlags.Instance).SetValue(self, false);
            }
        }
        private void PlayerTriggerCameraSwitchPatch(On.Player.orig_TriggerCameraSwitch orig, Player self)
        {
            RoomCamera roomCamera = self.abstractCreature.world.game.cameras[0];
            if (roomCamera.followAbstractCreature != null && roomCamera.followAbstractCreature.realizedCreature != null)
            {
                if (Custom.rainWorld.options.cameraCycling)
                {
                    int count = self.abstractCreature.world.game.session.Players.Count();
                    int baseIndex = self.playerState.playerNumber;
                    for(int index =  1; index < count; index++)
                    {
                        int i = (baseIndex + index) % count;
                        AbstractCreature ac = self.abstractCreature.world.game.Players[i];
                        if(ac != roomCamera.followAbstractCreature && ac.state.alive)
                        {
                            AbstractCreature cameraTarget = self.abstractCreature.world.game.Players[index];
                            roomCamera.ChangeCameraToPlayer(cameraTarget);
                            return;
                        }
                    }
                }
            }
            orig.Invoke(self);
        }
        internal static bool GameplayReady()
        {
            RainWorldGame? game;
            RoomCamera? cam;
            Room? room;
            RainWorld rw = Custom.rainWorld;
            if (rw == null) return false;
            ProcessManager pm = rw.processManager;
            if (pm == null) return false;
            // 0) 换场黑幕 / "Loading..." 已退完（ProcessManager.cs:209 / 774-785）
            if (pm.fadeToBlack > 0f) return false;
            // 1) 主循环就是游戏本体（排除主菜单、InitializationScreen、睡眠/死亡结算屏）
            if (!(pm.currentMainLoop is RainWorldGame g)) return false;
            game = g;
            // 2) 摄像机在，且 HUD 已建好（RoomCamera.cs:302；HUD 在 RoomCamera.Update:694-701 建）
            if (g.cameras == null || g.cameras.Length == 0) return false;
            cam = g.cameras[0];                      // Jolly 下请改成"跟随目标猫的那台"
            if (cam == null || cam.hud == null) return false;
            // 3) 不在换房间过程中（RoomCamera.cs:421  AboutToSwitchRoom => loadingRoom != null）
            if (cam.AboutToSwitchRoom) return false;
            // 4) 房间真加载完 + 允许玩家进入（Room.cs:529 / 531，由 Room.cs:2855/2970 置位）
            room = cam.room;
            if (room == null || !room.fullyLoaded || !room.ReadyForPlayer) return false;
            // 5) 镜头有跟随目标且已实体化（RoomCamera.cs:275 / 527）
            if (cam.followAbstractCreature == null || cam.followAbstractCreature.realizedCreature == null) return false;
            // 6) 不在过场里（RoomCamera.cs:435）
            if (cam.InCutscene) return false;
            return true;
        }
        private bool IsSlugNPCInPlayerMap(AbstractCreature ac)
        {
            bool result = false;
            if (isAIApplyToPlayer1 && playerMap != null && playerMap.TryGetValue(1,out Player p1))
                result = result || p1.abstractCreature == ac;
            if (isAIApplyToPlayer2 && playerMap != null && playerMap.TryGetValue(2,out Player p2))
                result = result || p2.abstractCreature == ac;
            if (isAIApplyToPlayer3 && playerMap != null && playerMap.TryGetValue(3,out Player p3))
                result = result || p3.abstractCreature == ac;
            if (isAIApplyToPlayer4 && playerMap != null && playerMap.TryGetValue(4,out Player p4))
                result = result || p4.abstractCreature == ac;
            return result;
        }
        
    }
}