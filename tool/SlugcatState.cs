using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection;
using BepInEx.Logging;
using static CreatureTemplate.Relationship;
using Type = System.Type;
namespace SlugcatDriver.Tool
{
    public class SlugcatState
    {
        private Player _p;
        public SlugcatState(Player p) => _p = p;
        // 实际输入
        public int inputX => _p.input[0].x; // 水平方向 −1/0/1（↔️）
        public int inputY => _p.input[0].y; // 垂直方向 −1/0/1（↕️，上为正）
        public bool inputIsPckp => _p.input[0].pckp; // 拾取/换手/放置/食用
        public bool inputIsJmp => _p.input[0].jmp; // 跳跃（按住）
        public bool inputIsThrw => _p.input[0].thrw; // 投掷
        
        public bool inputSpec => _p.input[0].spec; // 特殊键
        public bool inputMp => _p.input[0].mp; // 地图
        public int inputDownDiagonal => _p.input[0].downDiagonal; // 斜下方向 −1/0/1：翻滚/钻管道的起手输入
        

        // 身份与位置
        public string? roomName => _p.room?.abstractRoom?.name; // 房间（如 SU_B04）不在房间里时为 null（管道中、避难所、梦境、过场）
        public string bodyMode => _p.bodyMode.ToString(); // 体态状态机 cs:10913 UpdateBodyMode()
        public bool isDefault => bodyMode == "Default"; // 默认(动画加载、被生物抓住)
        public bool isCrawl => bodyMode == "Crawl"; // 匍匐
        public bool isStand => bodyMode == "Stand"; // 站立
        public bool isCorridorClimb => bodyMode == "CorridorClimb"; // 狭长管道内
        public bool isClimbIntoShortCut => bodyMode == "ClimbIntoShortCut"; // 传送管道内
        public bool isWallClimb => bodyMode == "WallClimb"; // 爬墙中
        public bool isClimbingOnBeam => bodyMode == "ClimbingOnBeam"; // 爬杆中
        public bool isSwimming => bodyMode == "Swimming"; // 游泳中
        public bool isZeroG => bodyMode == "ZeroG"; // 零重力
        public bool isStunned => bodyMode == "Stunned"; // 眩晕
        public bool isDead => bodyMode == "Dead"; // 死亡
        public string animation => _p.animation.ToString(); // 动画
        public bool isNone => animation == "None"; // 无
        public bool isCrawlTurn => animation == "CrawlTurn"; // 爬行转身
        public bool isStandUp => animation == "StandUp"; // 站起
        public bool isDownOnFours => animation == "DownOnFours"; // 趴下
        public bool isLedgeCrawl => animation == "LedgeCrawl"; // 边缘爬行
        public bool isLedgeGrab => animation == "LedgeGrab"; // 边缘抓取
        public bool isHangFromBeam => animation == "HangFromBeam"; // 悬挂于梁
        public bool isGetUpOnBeam => animation == "GetUpOnBeam"; // 上梁
        public bool isStandOnBeam => animation == "StandOnBeam"; // 站在梁上
        public bool isClimbOnBeam => animation == "ClimbOnBeam"; // 爬梁
        public bool isGetUpToBeamTip => animation == "GetUpToBeamTip"; // 上到梁端
        public bool isHangUnderVerticalBeam => animation == "HangUnderVerticalBeam"; // 悬挂于垂直梁下
        public bool isBeamTip => animation == "BeamTip"; // 梁端
        public bool isCorridorTurn => animation == "CorridorTurn"; // 管道转身
        public bool isSurfaceSwim => animation == "SurfaceSwim"; // 水面游泳
        public bool isDeepSwim => animation == "DeepSwim"; // 深水游泳
        public bool isRoll => animation == "Roll"; // 翻滚
        public bool isFlip => animation == "Flip"; // 空翻
        public bool isRocketJump => animation == "RocketJump"; // 火箭跳
        public bool isBellySlide => animation == "BellySlide"; // 腹部滑行
        public bool isAntlerClimb => animation == "AntlerClimb"; // 攀爬鹿角
        public bool isGrapplingSwing => animation == "GrapplingSwing"; // 抓钩摆荡
        public bool isZeroGSwim => animation == "ZeroGSwim"; // 零重力游泳
        public bool isZeroGPoleGrab => animation == "ZeroGPoleGrab"; // 零重力抓杆
        public bool isVineGrab => animation == "VineGrab"; // 抓藤蔓
        public BodyChunk[] bodyChunks => _p.bodyChunks; // 身体块数组：[0]=上半身（头/主块，多数位移以它为准），[1]=下半身（腹/尾块）
        public bool isAirborne // 腾空(脚不沾地) 目前在狭长管道内也是True 需要很多测试样本
        {
            get
            { 
                return bodyChunks[0].ContactPoint.x == 0 && bodyChunks[0].ContactPoint.y == 0
                    && bodyChunks[1].ContactPoint.x == 0 && bodyChunks[1].ContactPoint.y == 0;
            }
        }
        public float x => bodyChunks != null && bodyChunks.Length > 0 ? bodyChunks[0].pos.x : float.MaxValue; // 上半身位置 x 左上角(0,0)
        public float y => bodyChunks != null && bodyChunks.Length > 0 ? bodyChunks[0].pos.y : float.MaxValue; // 上半身位置 y 左上角(0,0)
        public float vx => bodyChunks != null && bodyChunks.Length > 0 ? bodyChunks[0].vel.x : float.MaxValue; // 上半身速度 vx
        public float vy => bodyChunks != null && bodyChunks.Length > 0 ? bodyChunks[0].vel.y : float.MaxValue; // 上半身速度 vy
        public bool hasItem => _p.grasps[0] != null || _p.grasps[1] != null; // 手上是否有东西
        // TODO.md TODO_01
        // 蛞蝓猫的 41 个观测量（MovementMarker 清单）
        // 翻滚/肚皮滑行的持续帧数：起手时清零（:7549-7550）；普通滑行上限 11、长滑 99（:7345-7347、:7446）；
        // >15 帧（长滑按 30+80×Adrenaline）且不再按住下方向时收尾（:10177）｜ Player.cs:1869
        public int rollCounter => _p.rollCounter;
        // 刹车滑行(skid)的持续帧数：反向输入时开始累积，>20 帧或不再反向输入即结束（:11357-11363）｜ Player.cs:1877
        public int slideCounter => _p.slideCounter;
        // "顶着惯性按反向方向"的持续帧数：>10（Rivulet 5）且速度方向一致时才真正转入滑行（slideCounter=1，:11376-11391）；
        // 松开方向键则逐帧衰减（:11393-11395）｜ Player.cs:1875
        public int initSlideCounter => _p.initSlideCounter;
        // 翻滚收尾阶段仍在 Roll 动画时的帧数（:10169-10176 累积/清零；:9329-9331 用于强制结束动画）
        // 可理解为"翻滚还没结束、但已经在收尾"的计时 ｜ Player.cs:2009
        public int stopRollingCounter => _p.stopRollingCounter;
        // 起手翻滚的冷却/许可计时：被消耗时置 15，每帧递减（:6220-6226）；用下斜方向起手翻滚的前提之一（:7538）｜ Player.cs:1865
        public int allowRoll => _p.allowRoll;
        // 翻滚方向（−1/0/1）：起手时取 input.downDiagonal（:7549），收尾时置 0（:10179）；0 = 没有在翻滚 ｜ Player.cs:1867
        public int rollDirection => _p.rollDirection;
        // 滑行方向（±1）：由输入方向决定/翻转（:11389）；slideCounter 用它判断是否还在"逆向刹车"（:11360）｜ Player.cs:1879
        public int slideDirection => _p.slideDirection;
        // 沿杆向上滑（快速上杆）的剩余帧：每帧递减，>8 时强制某动画帧（:9714-9720）；
        // 一旦动画不再是 ClimbOnBeam 就清零（:9333-9335）｜ Player.cs:2011
        public int slideUpPole => _p.slideUpPole;
        // 移动减速硬直帧：>0 时移动被压制，每帧 −1（:6333-6335）；
        // 极度疲劳（aerobicLevel 低）时会被抬起来（:3931、:5833、:5849）｜ Player.cs:1735
        public int slowMovementStun => _p.slowMovementStun;

        // 跳跃
        // 跳跃受阻的方向性硬直（= 15 × −受阻方向，:7635）；正/负分别表示左/右受阻，逐帧往 0 回归（:6353-6359）｜ Player.cs:1761
        public int jumpStun => _p.jumpStun;
        // 跳跃请求的缓冲帧：>0 表示"最近按过跳"（含被缓存/延后的输入），用于抓边缘、上杆后自动起跳等
        // （:3434 与 pckp 组合判断；:4188、:4337-4340 消费后清零）｜ Player.cs:1831
        public int wantToJump => _p.wantToJump;
        // 允许起跳的剩余帧（土狼时间/落地缓冲）：>0 时按跳才能起跳（:3465、:3549）；每帧递减 ｜ Player.cs:1833
        public int canJump => _p.canJump;
        // 墙面蹬跳的许可与方向：= 输入方向 × −15（:11711），逐帧向 0 衰减（:6301-6307）；0 = 当前不可蹬墙跳 ｜ Player.cs:1837
        public int canWallJump => _p.canWallJump;
        // 贴墙下滑的持续帧数：用于把下滑速度按 0→30 帧映射（0.8→0 的重力抑制），即"贴墙越久滑得越慢"
        // （:6295-6299、:11717-11718）｜ Player.cs:1769
        public int wallSlideCounter => _p.wallSlideCounter;
        // 蓄力超级跳的计时：贴墙/杆下滑时按住跳累积，上限 20（:15326-15328）；
        // >10 且三帧都按住跳则触发超级跳（:7021）｜ Player.cs:1767
        public int superLaunchJump => _p.superLaunchJump;
        // 垂直管道"上蹿"之后的计时：成功上蹿时置 30（:11553），每帧 −1（:5929-5935）；
        // >0 时不允许立刻再次上蹿（:11687）｜ Player.cs:1785
        public int shootUpCounter => _p.shootUpCounter;
        // 本次起跳附带的额外上跳力度（不同动作会设成 6/8/10 等，:3506/:3514/:3520/:3554/:4714）；
        // >0 且按住跳（或 simulateHoldJumpButton > 0）时被视为"可蓄力/被加速的跳"（:14859）｜ Player.cs:1763
        public float jumpBoost => _p.jumpBoost;
        // 姿态窗口（公有部分）
        // 下半身"连续贴地"的帧数：换到离地就清零（:11084-11089）；>=3 帧才算稳定站立（:11235）｜ Player.cs:2023
        public int lowerBodyFramesOnGround => _p.lowerBodyFramesOnGround;
        // 下半身"连续离地"的帧数（与上一个互补，:11085/:11090）｜ Player.cs:2025
        public int lowerBodyFramesOffGround => _p.lowerBodyFramesOffGround;
        // 上半身"连续贴地"的帧数（:11074-11079）｜ Player.cs:2027
        public int upperBodyFramesOnGround => _p.upperBodyFramesOnGround;
        // 上半身"连续离地"的帧数；与下半身同时 >=5 才允许某些体态切换（:11075/:11080、:11273）｜ Player.cs:2029
        public int upperBodyFramesOffGround => _p.upperBodyFramesOffGround;
        // 连续保持"斜下方向"输入的帧数：输入变化即清零（:6379-6383）；>6 帧可触发钻进管道/通道（:11097）｜ Player.cs:1787
        public int consistentDownDiagonal => _p.consistentDownDiagonal;

        // 水 / 呼吸（公有部分）
        // 肺里的空气 0..1（1=满）：溺水/窒息时被削（:4175），开局复位为 1（:5325）；游泳决策的核心量 ｜ Player.cs:1933
        public float airInLungs => _p.airInLungs;
        // 身体是否浸没在水中（:7218 置真）｜ Player.cs:1937
        public bool isSubmerged => _p.submerged;

        // 速度 / 疲劳 / 增益（公有部分）
        // 本次"起步"时的基准奔跑系数（= slugcatStats.runspeedFac，:5322）；
        // 起跳/滑动结束时用它回写 runspeedFac（:16880、:16884）｜ Player.cs:1811
        // ⚠️ 该字段是 1.10+/Downpour 之后才有的；引用前的版本相关说明见 docs/RainWorldCode.md
        public float initRunSpeedFac => _p.initRunSpeedFac;
        // 有氧疲劳度 0..1（1 = 力竭）：持续移动会升高、静止时缓慢恢复（:5869-5873）；
        // >=0.95 会触发饕餮(Gourmand)的特殊判定（:3921）；低疲劳会反过来抬高 slowMovementStun ｜ Player.cs:1897
        public float aerobicLevel=> _p.aerobicLevel;
        // 蘑菇（致幻）效果强度 0..1：生效时上升、结束后衰减（:6056-6070）；
        // 游戏内部的 Adrenaline 属性就是它（:2335，影响翻滚时长、滑行速度等）｜ Player.cs:1793
        public float mushroomEffect => _p.mushroomEffect;

        // 角色专属（倾盆大雨：工匠）
        // 工匠(Artificer)爆炸跳的阶段/蓄力计时：逐帧递减并参与触发判定（:3437-3454）｜ Player.cs:1645
        public int pyroJumpCounter => _p.pyroJumpCounter;
        // 工匠弹反的冷却（帧）：成功弹反后置 40（:3565），逐帧递减（:3453）｜ Player.cs:1649
        public float pyroParryCooldown => _p.pyroParryCooldown;

        // 杆 / 管道（公有走强类型，私有走反射）
        // 双脚被强制吸附到横杆格的倒计时：翻上横杆时置 20（:9555），每帧 −1（:6325-6327）；
        // >0 时脚底位置受横杆格约束，避免刚上杆就掉下去（:11726）｜ Player.cs:1889
        public int forceFeetToHorizontalBeam => _p.forceFeetToHorizontalBeamTile;
        // "向上抓/爬上"请求的缓冲帧：>0 表示最近按了上/抓（:15024 与 input.y 一起判断）；消费后清零（:15051、:15079），
        // 另有一处置 −1 表示抑制（:15113）；用于贴墙上爬、抓边缘、爬管道口 ｜ Player.cs:1835
        public int wantToGrab => _p.wantToGrab;

        private static readonly Dictionary<string, FieldInfo> _privCache = new Dictionary<string, FieldInfo>();
        private static FieldInfo? F(string name)
        {
            FieldInfo fi;
            if (_privCache.TryGetValue(name, out fi)) return fi;      // 每次读取的成本 = 这 1 次字典查找

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            fi = typeof(Player).GetField(name, Flags);
            for (Type t = typeof(Player).BaseType; fi == null && t != null; t = t.BaseType)
                fi = t.GetField(name, Flags | BindingFlags.DeclaredOnly);   // GetField 不找基类私有字段，手动往上走

            if (fi == null)
            {
                ConsoleManager.Instance?.LogError($"Player.{name} 不存在（游戏版本变了？）");
            }
            else
            {
                _privCache[name] = fi;              // 命中缓存后不再反射；缓存 null 表示永久缺失（已告警）

            }
            return fi;
        }

        // 取字段并校验类型；类型不符按“缺失”处理（只告警一次），避免静默读到错误的值。
        private static FieldInfo? Typed<T>(string name)
        {
            FieldInfo? fi = F(name);
            if (fi == null) return null;
            if (fi.FieldType != typeof(T)) { ConsoleManager.Instance?.LogError($"[PlayerPriv] Player.{name} 实际类型 {fi.FieldType.Name} ≠ {typeof(T).Name}"); return null; }
            return fi;
        }

        // ═════════ 每个字段一个定制读取器（字段名+类型都写死，写错是编译期错误） ═════════
        // 在横杆(horizontal beam)上“竖直贴着站”，不是趴/挂在杆上 ｜ Player.cs:1999、:9520-9561
        private bool Priv_straightUpOnHorizontalBeam()
        { var fi = Typed<bool>("straightUpOnHorizontalBeam"); return fi != null && (bool)fi.GetValue(_p); }

        // “从横杆蹬跳”的冷却/惩罚帧：成功置 3(Rivulet)/6，失败 +12 上限 30；≥1 时不允许再蹬跳 ｜ :1843、:6262-6286、:5925-5927
        private int Priv_poleSkipPenalty()
        { var fi = Typed<int>("poleSkipPenalty"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 朝管道里挤进去的连续帧数：>2 才真正进入 CorridorClimb 体态 ｜ :1745、:14840-14844、:14925
        private int Priv_goIntoCorridorClimb()
        { var fi = Typed<int>("goIntoCorridorClimb"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 管道内上次转向后的帧数（最多 40）：<40 允许部分管道动作、<30 播转向动画 ｜ :1755、:10350-10369、:11459/11469
        private int Priv_corridorTurnCounter()
        { var fi = Typed<int>("corridorTurnCounter"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 离开“管道爬行”体态后的帧数（在管道里恒 0）：抑制刚出管道就触发的判定 ｜ :1757、:6212-6219、:6586
        private int Priv_timeSinceInCorridorMode()
        { var fi = Typed<int>("timeSinceInCorridorMode"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 是否正在管道里往下掉/滑落（而不是在爬）｜ :1747、:11441（置真）、:14936（清假）、:6154、:14925
        private bool Priv_corridorDrop()
        { var fi = Typed<bool>("corridorDrop"); return fi != null && (bool)fi.GetValue(_p); }

        // 爬行中按方向键累积的帧数（转身窗口）：>5 且两侧脚下非实心且按了横向才允许转身 ｜ :1907、:15098/15102、:11189-11191
        private int Priv_crawlTurnDelay()
        { var fi = Typed<int>("crawlTurnDelay"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // ⚠️ 死字段：全树只有声明与每帧递减，从未被赋值 ⇒ 运行时恒 0 ｜ :1905、:6341-6343
        private int Priv_landingDelay()
        { var fi = Typed<int>("landingDelay"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 抓住边缘(ledge)的持续帧数：抓住++、没抓--、落地/离边清零；>0 时会被往边缘方向推 ｜ :1997、:9428-9454、:9422
        private int Priv_ledgeGrabCounter()
        { var fi = Typed<int>("ledgeGrabCounter"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 管道内“逆行程度”：反向输入 +2、上限 20、每帧 −1；>10 时管道爬速 ×0.6 ｜ :1903、:11632-11646、:6337-6339
        private int Priv_backwardsCounter()
        { var fi = Typed<int>("backwardsCounter"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 游戏内部“假装按住跳跃键”的剩余帧（Jump() 里置 6≈150ms），与 input.jmp 等价 ｜ :1765、:15974、:15349-15351、:14859
        private int Priv_simulateHoldJumpButton()
        { var fi = Typed<int>("simulateHoldJumpButton"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 肚皮滑行中“偏离滑行方向”的帧数：参与“滑行→翻滚”的切换门槛 ｜ :2003、:10291-10298、:10314
        private int Priv_exitBellySlideCounter()
        { var fi = Typed<int>("exitBellySlideCounter"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 管道内跳跃许可窗口的剩余帧：有支撑置 5（≈125ms），否则 −1，转向清零；>0 才能纵向管道上蹿 ｜ :1839、:11479-11483、:11551-11570
        private int Priv_canCorridorJump()
        { var fi = Typed<int>("canCorridorJump"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 水平管道滑行的剩余帧：成功 25、失败 15，每帧 −1 ｜ :1751、:11520/11540/11596/11604、:6349-6351
        private int Priv_horizontalCorridorSlideCounter()
        { var fi = Typed<int>("horizontalCorridorSlideCounter"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 垂直管道滑行/上蹿的剩余帧：上蹿成功 22、失败 34，每帧 −1 ｜ :1749、:11563/11569、:6345-6347
        private int Priv_verticalCorridorSlideCounter()
        { var fi = Typed<int>("verticalCorridorSlideCounter"); return fi == null ? 0 : (int)fi.GetValue(_p); }

        // 游泳“发力程度”0..1（不是速度）：基础 |vel.x| 映射后快升慢降，驱动划水周期与推进力 ｜ :1853、:7118、:9933-9950
        private float Priv_swimForce()
        { var fi = Typed<float>("swimForce"); return fi == null ? 0f : (float)fi.GetValue(_p); }

        // dynamicRunSpeed 真实类型是 float[2]（按身体块索引，不是 float！）｜ :1759、:9633-9634、:15281
        private float[]? Priv_dynamicRunSpeed()
        { var fi = Typed<float[]>("dynamicRunSpeed"); return fi == null ? null : (float[])fi.GetValue(_p); }

        // ═════════ Getter：全部用 _p ═════════

        // 在横杆上“竖直贴着站”（不是趴/挂在杆上）。
        public bool straightUpOnHorizontalBeam
        { get { return _p != null && Priv_straightUpOnHorizontalBeam(); } }

        // “从横杆蹬跳”的冷却/惩罚帧（单位 tick，40fps ⇒ 1≈25ms）；&gt;0 时蹬跳无效。
        public int poleSkipPenalty
        { get { return _p == null ? 0 : Priv_poleSkipPenalty(); } }

        // 朝管道里挤进去的连续帧数；&gt;2 才算真的在管道里爬。
        public int goIntoCorridorClimb
        { get { return _p == null ? 0 : Priv_goIntoCorridorClimb(); } }

        // 管道内上次转向后的帧数（最多 40）。
        public int corridorTurnCounter
        { get { return _p == null ? 0 : Priv_corridorTurnCounter(); } }

        // 离开管道爬行体态后的帧数（管道内恒 0）。
        public int timeSinceInCorridorMode
        { get { return _p == null ? 0 : Priv_timeSinceInCorridorMode(); } }

        // 是否正在管道里往下掉/滑落（而非在爬）。
        public bool corridorDrop
        { get { return _p != null && Priv_corridorDrop(); } }

        // 爬行中按方向键累积的帧数（转身窗口计时）。
        public int crawlTurnDelay
        { get { return _p == null ? 0 : Priv_crawlTurnDelay(); } }

        // ⚠️ 死字段，恒 0（保留仅为与旧记录格式对齐）。
        public int landingDelay
        { get { return _p == null ? 0 : Priv_landingDelay(); } }

        // 抓住边缘(ledge)的持续帧数；&gt;0 表示挂在边上。
        public int ledgeGrabCounter
        { get { return _p == null ? 0 : Priv_ledgeGrabCounter(); } }

        // 管道内逆行程度（0..20）；&gt;10 时管道爬速 ×0.6。
        public int backwardsCounter
        { get { return _p == null ? 0 : Priv_backwardsCounter(); } }

        // 游戏内部“假装按住跳跃键”的剩余帧（Jump() 里置 6）；记录输入时要把它算作“按住跳”。
        public int simulateHoldJumpButton
        { get { return _p == null ? 0 : Priv_simulateHoldJumpButton(); } }

        // 肚皮滑行中偏离滑行方向的帧数（决定何时从滑行切到翻滚）。
        public int exitBellySlideCounter
        { get { return _p == null ? 0 : Priv_exitBellySlideCounter(); } }

        // 管道内跳跃许可窗口剩余帧；&gt;0 才能上蹿成功，否则吃 34 帧硬直。
        public int canCorridorJump
        { get { return _p == null ? 0 : Priv_canCorridorJump(); } }

        // 水平管道滑行剩余帧（成功 25 / 失败 15）。
        public int horizontalCorridorSlideCounter
        { get { return _p == null ? 0 : Priv_horizontalCorridorSlideCounter(); } }

        // 垂直管道滑行/上蹿剩余帧（成功 22 / 失败 34）。
        public int verticalCorridorSlideCounter
        { get { return _p == null ? 0 : Priv_verticalCorridorSlideCounter(); } }

        // 游泳“发力程度”（0..1，不是速度）。
        public float swimForce
        { get { return _p == null ? 0f : Priv_swimForce(); } }

        // dynamicRunSpeed[0] = 上/头侧身体块此刻的最大水平速度钳制值。
        public float dynamicRunSpeed0
        {
            get
            {
                if (_p == null) return 0f;
                float[]? a = Priv_dynamicRunSpeed();              // ⚠️ 数组是活引用，用完即弃，不要存字段
                return (a != null && a.Length > 0) ? a[0] : 0f;
            }
        }

        // dynamicRunSpeed[1] = 下/身侧身体块此刻的最大水平速度钳制值。
        public float dynamicRunSpeed1
        {
            get
            {
                if (_p == null) return 0f;
                float[]? a = Priv_dynamicRunSpeed();
                return (a != null && a.Length > 1) ? a[1] : 0f;
            }
        }

        // 兼容旧的单值口径：返回 chunk0 的值（旧代码把 float[] 读成了 0，这里修好了）。
        public float dynamicRunSpeed
        { get { return dynamicRunSpeed0; } }

        // ═════════ 可选派生量（不想用可删） ═════════

        // 实际最大跑速 / 基准跑速：1.0=正常，0.75≈爬行/滑行，更低=眩晕/水/泥；肾上腺素会 &gt;1。
        public float runSpeedRatio
        {
            get
            {
                if (_p == null) return 1f;
                SlugcatStats st = _p.slugcatStats;                 // 公开属性 Player.cs:2361
                float baseSpeed = 2.1f * (st != null ? st.runspeedFac : 1f);   // Player.cs:9633-9634
                return baseSpeed > 0.01f ? dynamicRunSpeed0 / baseSpeed : 1f;
            }
        }

        // 管道滑行还剩多少 tick（水平/垂直取大者）× 25ms = 剩余时间。
        public int corridorSlideTicksLeft
        {
            get
            {
                if (_p == null) return 0;
                int h = Priv_horizontalCorridorSlideCounter();
                int v = Priv_verticalCorridorSlideCounter();
                return h > v ? h : v;
            }
        }

        public SocialMemory.Relationship relationshipWithMe => _p.abstractCreature.state.socialMemory.GetOrInitiateRelationship(_p.room.game.Players[0].ID);
        public float likeMe => relationshipWithMe.like;
        public float fearMe => relationshipWithMe.fear;
        public float knowMe => relationshipWithMe.know;
        public float tempLikeMe => relationshipWithMe.tempLike;
        public float tempFearMe => relationshipWithMe.tempFear;

    }
}