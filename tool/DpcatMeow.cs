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
        public Player? p;
        // 替身变量 用于绕过 Grabability (obj as Player).SlugCatClass == MoreSlugcatsEnums.SlugcatStatsName.Slugpup 检测
        // 不能更改 SlugCatClass 一，是因为这代表职业 二，是因为强行改为Slugpup会卡死游戏
        private DataPearl _dataPearl = (DataPearl)FormatterServices.GetUninitializedObject(typeof(DataPearl));
        // 当前是否处于官方的Standard过场动画中？
        private bool _isOfficalInCutsceneStandard = false;
        // 当前是否处于本模组的Standard过场动画(镜头修复)中？
        private bool _isMineInCutsceneStandard = false;
        // 手动维护PlayerNPCState
        private PlayerNPCState _playerNPCState;
        private CreatureTemplate _origCreatureTemplate;
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            AddPatch();
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Clear()
        {
            isInited = false;
            _isMineInCutsceneStandard = false;
            p = null;
        }
        public void InitDpcat()
        {
            if(isInited || !GameplayReady()) return;
            p = SlugcatLocator.GetPlayerBySlot(2);
            

            AbstractCreature? ac = p?.abstractCreature;
            if(p == null || ac == null || p.room == null || !p.room.readyForAI || p.room.aimap == null /**|| !(ac.realizedCreature is Player)**/)
            {
                return;
            }
            _origCreatureTemplate = ac.creatureTemplate;
            // 1) 先换模板
            // creatureTemplate.AI 变 true AbstractCreature.cs:833 的抽象目的地/路径维护才会跑
            // Player.isNPC 变 true Player.cs:1957-1966 → 相机/HUD 的 NPC 分支对齐
            // RoomCamera.cs:1025 会把相机从它切到别的活玩家 —— Jolly 里正是你想要的
            ac.creatureTemplate = StaticWorld.GetCreatureTemplate(MoreSlugcatsEnums.CreatureTemplateType.SlugNPC);

            // 2) 再建 state —— 顺序反了 socialMemory 仍然是 null（CreatureState.cs:35）
            if(_playerNPCState == null)
            {
                int playerNumber = p.playerState != null ? p.playerState.playerNumber : 0;
                var st = new PlayerNPCState(ac, playerNumber);      // PlayerNPCState : PlayerState（PlayerNPCState.cs:7/27） 这一行只继承ac
                st.isPup = p.playerState != null && p.playerState.isPup; // 继承体型
                st.slugcatCharacter = p.playerState != null ? p.playerState.slugcatCharacter : p.SlugCatClass; // 继承名字(惬意合作 这个名字也决定相关样式 不继承样式会出问题)
                st.foodInStomach = 1;

                SocialMemory.Relationship orInitiateRelationship = st.socialMemory.GetOrInitiateRelationship(p.room.game.Players[0].ID); // 初始化与玩家的关系，返回的是引用
                orInitiateRelationship.InfluenceLike(1f); // 因子(影响权重)
                orInitiateRelationship.InfluenceTempLike(1f); // 因子(影响权重)
                orInitiateRelationship.like = 0.52f; // 超过0.5就是朋友 like > 0.5 && tempLike > 0.5
                orInitiateRelationship.tempLike = 0.52f; // 超过0.5就是朋友  like > 0.5 && tempLike > 0.5
                ac.state = st;
            }
            else
            {
                ac.state = _playerNPCState;
            }
            

            // 3) 抽象层：用 pup 专用的 SlugNPCAbstractAI（自带 isTamed/跟随/回巢；普通 AbstractCreatureAI 缺这些）
            ac.abstractAI = new SlugNPCAbstractAI(ac.world, ac);

            // 4) 具体 AI（ctor 里会自己写 abstractAI.RealAI = this，SlugNPCAI.cs:442）
            ac.abstractAI.RealAI = new SlugNPCAI(ac, ac.world);

            // 5) 补 realizedRoom（PathFinder.cs:797 → Reset(): realizedRoom = room） 没有这个会崩
            ac.abstractAI.RealAI.NewRoom(p.room);        // PathFinder.cs:797 → Reset(): realizedRoom = room（:808）
            
            isInited = true;
        }
        private void AddPatch()
        {
            // 修复‘猫崽把玩家当物品抓’
            On.MoreSlugcats.SlugNPCAI.CanGrabItem += new On.MoreSlugcats.SlugNPCAI.hook_CanGrabItem(CanGrabItemPatch);
            // 修复‘抓崽困难’
            On.Player.CanIPickThisUp += new On.Player.hook_CanIPickThisUp(CanIPickThisUpPatch);
            // 修复‘相机对猫崽无效’
            On.RoomCamera.ChangeCameraToPlayer += new On.RoomCamera.hook_ChangeCameraToPlayer(ChangeCameraToPlayerPatch);
            // 修复补丁‘相机对猫崽无效’导致的‘猫崽相机无法切回’
            On.RoomCamera.EnterCutsceneMode += new On.RoomCamera.hook_EnterCutsceneMode(EnterCutsceneModePatch);
            On.RoomCamera.ExitCutsceneMode += new On.RoomCamera.hook_ExitCutsceneMode(ExitCutsceneModePatch);
            On.Player.JollyInputUpdate += new On.Player.hook_JollyInputUpdate(JollyInputUpdatePatch);
            // 修复‘猫崽不能雨眠’
            On.Player.Update += new On.Player.hook_Update(UpdatePatch);
            // 修复‘雨眠后出现复制猫崽’
            On.RegionState.AdaptRegionStateToWorld += new On.RegionState.hook_AdaptRegionStateToWorld(AdaptRegionStateToWorldPatch);
        }
        private static bool CanGrabItemPatch(On.MoreSlugcats.SlugNPCAI.orig_CanGrabItem orig, MoreSlugcats.SlugNPCAI self, PhysicalObject obj)
        {
            return orig.Invoke(self,obj) && !(obj is Player player); // 不抓玩家
        }
        private bool CanIPickThisUpPatch(On.Player.orig_CanIPickThisUp orig, Player self, PhysicalObject obj)
        {
            Player? player = obj as Player;
            // 抓猫崽时执行（仅本模组猫崽）因为本模组猫崽SlugCatClass不是Slugpup，导致原函数这段代码不会执行，此为修复补丁
            if(player != null && self != player && !(self.abstractCreature?.abstractAI?.RealAI is SlugNPCAI) && player == p)
            {
                if (self.slugOnBack != null && self.slugOnBack.slugcat == obj)
                {
                    return false;
                }
                if (self.onBack == player || player.onBack != null)
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
        private void ChangeCameraToPlayerPatch(On.RoomCamera.orig_ChangeCameraToPlayer orig, RoomCamera self, AbstractCreature cameraTarget)
        {
            if (_isMineInCutsceneStandard)
            {
                _isMineInCutsceneStandard = false;
                self.ExitCutsceneMode();
            }else if (cameraTarget == p?.abstractCreature && !_isMineInCutsceneStandard) // 如果需要从一个过场动画猫切换到另一个同样的，那就继续EnterCutsceneMode
            {
                _isMineInCutsceneStandard = true;
                self.EnterCutsceneMode(cameraTarget,RoomCamera.CameraCutsceneType.Standard);
                
            }

            orig.Invoke(self,cameraTarget);
        }
        private void EnterCutsceneModePatch(On.RoomCamera.orig_EnterCutsceneMode orig, RoomCamera self, AbstractCreature cutscenePlayer, RoomCamera.CameraCutsceneType type)
        {
            if(cutscenePlayer == p?.abstractCreature)
            {
                orig.Invoke(self,cutscenePlayer,type);
                return;
            }
            
            _isOfficalInCutsceneStandard = type == RoomCamera.CameraCutsceneType.Standard;
            orig.Invoke(self,cutscenePlayer,type);
        }
        private void ExitCutsceneModePatch(On.RoomCamera.orig_ExitCutsceneMode orig, RoomCamera self)
        {
            _isOfficalInCutsceneStandard = false;
            orig.Invoke(self);
        }
        private void JollyInputUpdatePatch(On.Player.orig_JollyInputUpdate orig, Player self)
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
        private void UpdatePatch(On.Player.orig_Update orig, Player self, bool eu)
        {
            if (self == p)
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
                            _playerNPCState = self.abstractCreature.state as PlayerNPCState;
                            if(_playerNPCState != null)
                            {
                                _playerNPCState.foodInStomach = _playerNPCState.foodInStomach - self.slugcatStats.foodToHibernate;
                            }
                        }
                    }
                }
            }
            orig.Invoke(self,eu);
        }
        private void AdaptRegionStateToWorldPatch(On.RegionState.orig_AdaptRegionStateToWorld orig, RegionState self, int playerShelter, int activeGate)
        {
            // 换成源模板，这样 abstractCreature.creatureTemplate.TopAncestor().type == MoreSlugcatsEnums.CreatureTemplateType.SlugNPC 就不成立
            // 如此，我们的猫崽就不会当做NPC加入下循环的猫崽生成列表中
            if(p != null)
            {
                p.abstractCreature.creatureTemplate = _origCreatureTemplate;
            }
            orig.Invoke(self,playerShelter,activeGate);
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
        
    }
}