---
TODO_01
蛞蝓猫有一个grasps字段，它是一个Grasp[]，现在我需要给这个类添加几个字段或方法，以便后续通过它们决定做什么操作，我主要关心：
1. 动作类，我的蛞蝓猫需要手中的物品提供动量，因此我需要一个或两个字段判断能否从中安全的获取动量。
2. 地形创造，我的蛞蝓猫需要用矛创建地形
3. 食物类，我的蛞蝓猫需要判断手中的食物是否能够安全食用
4. 重要物品，我的蛞蝓猫需要判断手中的物品价值是否足够高
5. 危险品，我的蛞蝓猫需要判断手中的物品是否是炸弹、奇点炸弹等极度危险的物品(需要确保安全使用)
6. 累赘，我的蛞蝓猫需要判断手中的物品是否太重了

你认为就我的目的而言，应该如何设计？
---

这是个纯设计问题，我先说结论：**不要把这些判断直接塞进 `Grasp` 或 `Player` 的扩展里**，而是单独做一个"手持物品分析"层，把"物品是什么"和"它能干什么"分开。

---

## 一、为什么不要直接扩展 Grasp

`Grasp` 是游戏原生的结构，只描述"抓取位置 + 物理对象引用"。往里加 `CanProvideMomentum` 之类的字段，问题有三：

1. **污染游戏类型**：扩展方法多了，`Grasp` 会变成一个谁都能挂东西的杂货铺；
2. **语义混淆**：`CanProvideMomentum` 不是物品的属性，而是"猫 + 物品 + 当前场景"的关系；
3. **无法缓存**：每次决策都要重新计算一遍，分散在各处。

正确的做法：**把分析结果单独封装成一个值对象，每 tick 或每次决策时算一次**。

---

## 二、分层设计

```text
Player.grasps (Grasp[])
   │
   ▼
HeldItemAnalyzer.Analyze(player)
   │
   ▼
HeldItemInfo[]  ←── AI 决策只读这个
```

- **`Grasp`**：游戏原生，不动它。
- **`HeldItemAnalyzer`**：无状态的分析器，输入 `Player`，输出分析结果。
- **`HeldItemInfo`**：分析结果，包含分类标签 + 能力判断 + 物理量。

---

## 三、`HeldItemInfo` 的建议字段

按你的 6 个需求，分成"是什么（分类）"和"能干什么（能力）"两组。

### 分类组（物品是什么）

```csharp
public bool IsSpear;           // 普通矛
public bool IsExplosiveSpear;  // 爆炸矛
public bool IsBomb;            // 拾荒者炸弹
public bool IsSingularityBomb; // 奇点炸弹
public bool IsFlashBomb;       // 闪光弹
public bool IsRock;            // 石头
public bool IsFood;            // 食物
public bool IsValuable;        // 珍珠、业力花、面具等
public bool IsHeavy;           // 质量超过阈值
public bool IsAlive;           // 手里是不是活物
```

分类是**客观的**，只取决于物品类型，不依赖猫的状态或场景。

### 能力组（能干什么）

```csharp
public bool CanProvideMomentum;  // 需求1
public bool CanCreateTerrain;    // 需求2
public bool CanBeSafelyEaten;    // 需求3
public bool IsHighValue;         // 需求4
public bool IsDangerous;         // 需求5
public bool IsBurden;            // 需求6
```

能力是**派生的**，依赖分类 + 猫的状态 + 场景。举几个例子：

| 能力 | 判断逻辑 |
|---|---|
| `CanProvideMomentum` | `IsSpear || IsRock || IsHeavy`，且物品质量 ≥ 阈值 |
| `CanCreateTerrain` | `IsSpear`，且猫当前不在管道 / 不在水中 |
| `CanBeSafelyEaten` | `IsFood`，且食物未被污染，且猫不是 Hunter（或已满足条件） |
| `IsHighValue` | `IsValuable`，且当前猫没有更重要的持有物 |
| `IsDangerous` | `IsBomb || IsSingularityBomb || IsFlashBomb`，且附近有同伴时升级为 true |
| `IsBurden` | 质量 ≥ 猫的承载力，或占用两只手且当前需要空手 |

### 物理量组

```csharp
public float Mass;      // 物品质量
public float Speed;     // 如果被投掷，出手时的速度估计
public float Momentum;  // Mass * Speed
```

---

## 四、`HeldItemAnalyzer` 的接口

```csharp
public static class HeldItemAnalyzer
{
    public static HeldItemInfo Analyze(Player p, int graspIndex, float massThreshold, float burdenThreshold);
    public static HeldItemInfo[] AnalyzeAll(Player p, float massThreshold = 5f, float burdenThreshold = 10f);
}
```

调用方（AI 决策）：

```csharp
var held = HeldItemAnalyzer.AnalyzeAll(player);
foreach (var item in held)
{
    if (item.CanCreateTerrain) { /* 用矛造地形 */ }
    if (item.IsDangerous)     { /* 远离 / 谨慎处理 */ }
    if (item.CanProvideMomentum) { /* 投掷加速 */ }
}
```

---

## 五、为什么把分类和能力分开

假设你以后想加一个"受伤时优先吃食物"的策略，只需要：

```csharp
if (item.CanBeSafelyEaten && player.IsInjured) { /* 吃 */ }
```

`CanBeSafelyEaten` 的内部逻辑可能随游戏版本变，但**调用方完全不用改**。这比在 AI 代码里到处写：

```csharp
if (item.Type == FoodType.BlueFruit && player.foodInStomach < 5 && ...) 
```

清晰得多。分类是"它是什么"，能力是"我现在能用它做什么"，两者解耦。

---

## 六、一个具体的分类规则表

把物品类型映射到分类，建议用 `Dictionary<Type, HeldItemFlags>` 或 `switch`：

```csharp
private static void Classify(AbstractPhysicalObject apo, HeldItemInfo info)
{
    switch (apo)
    {
        case Spear _:              info.IsSpear = true; break;
        case ExplosiveSpear _:     info.IsSpear = true; info.IsExplosiveSpear = true; break;
        case ScavengerBomb _:      info.IsBomb = true; break;
        case SingularityBomb _:    info.IsSingularityBomb = true; break;
        case Rock _:               info.IsRock = true; break;
        case BlueFruit _:
        case DangleFruit _:
        case SlimeMold _:
        case Mushroom _:           info.IsFood = true; break;
        case DataPearl _:
        case KarmaFlower _:        info.IsValuable = true; break;
        // ...
    }
}
```

用 `switch` 表达式 + 模式匹配，比反射快，也比大量 `if` 清晰。

---

## 七、关于"安全"和"危险"的判断

这两个是最容易做错的。它们**不是物品的固有属性**，而是场景相关的：

- **`CanBeSafelyEaten`**：如果猫是 Hunter，吃 BlueFruit 可能不安全；如果食物被污水污染，也不安全。所以判断要读猫的状态和房间状态。
- **`IsDangerous`**：一个炸弹在手，如果附近没有同伴，只是"有潜在风险"；如果附近有同伴，才升级为"危险"。

所以在 `HeldItemInfo` 里，建议提供**两个层级**：

```csharp
public bool IsDangerousInherent;   // 物品本身危险（炸弹、奇点弹）
public bool IsDangerousNow;        // 当前场景下危险（附近有同伴）
```

AI 优先看 `IsDangerousNow`，只关心物品属性时看 `IsDangerousInherent`。

同理：

```csharp
public bool IsFoodInherent;
public bool CanBeSafelyEatenNow;
```

---

## 八、`IsBurden` 的特殊性

"累赘"不只是质量，还包括：

- **占用抓取位**：两只手都占满，就没法抓矛；
- **阻碍动作**：某些物品在特定动作下会掉，比如翻滚；
- **质量影响**：某些动作需要空手或轻物。

所以 `IsBurden` 应该接受一个**上下文参数**：

```csharp
public bool IsBurdenFor(Player p, PlayerAction upcomingAction);
```

在 `HeldItemInfo` 里存一个默认判断，AI 需要精细判断时再调方法。

---

## 九、完整类结构建议

```csharp
public sealed class HeldItemInfo
{
    // 身份
    public int GraspIndex;
    public AbstractPhysicalObject Apo;
    public PhysicalObject Item;   // 可为 null（尚未实现化）

    // 分类（是什么）
    public bool IsSpear, IsExplosiveSpear, IsRock;
    public bool IsBomb, IsSingularityBomb, IsFlashBomb;
    public bool IsFood, IsValuable, IsAlive;
    public float Mass;
    public bool IsHeavy;

    // 能力（能干什么）
    public bool CanProvideMomentum;
    public bool CanCreateTerrain;
    public bool CanBeSafelyEatenNow;
    public bool IsHighValue;
    public bool IsDangerousInherent;
    public bool IsDangerousNow;
    public bool IsBurden;

    // 物理量
    public float SpeedEstimate;
    public float MomentumEstimate;
}

public static class HeldItemAnalyzer
{
    public static HeldItemInfo[] AnalyzeAll(Player p, AnalysisContext ctx);
    public static HeldItemInfo AnalyzeOne(Player p, int graspIndex, AnalysisContext ctx);
}

public struct AnalysisContext
{
    public float MassThreshold;
    public float BurdenThreshold;
    public bool HasCompanion;   // 附近是否有同伴 → 影响 IsDangerousNow
    public bool InWater;        // 影响 CanCreateTerrain
}
```

---

## 十、一句话总结

- **不要把判断塞进 `Grasp` 或 `Player`**，做一个独立的 `HeldItemInfo` + `HeldItemAnalyzer`。
- 把**分类（是什么）**和**能力（能干什么）**分开，分类是客观的，能力是场景相关的。
- 危险判断分**固有危险**和**当前危险**两层，避免把场景依赖混进物品属性。
- `IsBurden` 这类上下文相关的判断，用带参数的方法，不要做成纯布尔属性。
- AI 决策只读 `HeldItemInfo`，不读原始 `Grasp`，这样策略代码干净、可测试。

--------------------
        private static readonly Dictionary<string, string> AbbrevMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 身份 / 位置
            { "roomName", "Room" },
            { "bodyMode", "Mode" },
            { "animation", "Anim" },
            { "x", "PosX" },
            { "y", "PosY" },
            { "vx", "VelX" },
            { "vy", "VelY" },
            { "isAirborne", "Air_" },
            { "hasItem", "Item" },
            // 输入
            { "inputX", "InX_" },
            { "inputY", "InY_" },
            { "inputIsPckp", "Pckp" },
            { "inputIsJmp", "Jump" },
            { "inputIsThrw", "Thrw" },
            { "inputSpec", "Spec" },
            { "inputMp", "Map_" },
            { "inputDownDiagonal", "DgDl" },
            // 翻滚 / 滑行
            { "rollCounter", "Roll" },
            { "slideCounter", "Slid" },
            { "initSlideCounter", "ISld" },
            { "stopRollingCounter", "StpR" },
            { "allowRoll", "ARol" },
            { "rollDirection", "RoDr" },
            { "slideDirection", "SlDr" },
            { "slideUpPole", "SlUp" },
            { "slowMovementStun", "Slow" },
            // 跳跃
            { "jumpStun", "JmpS" },
            { "wantToJump", "WtJp" },
            { "canJump", "CnJp" },
            { "canWallJump", "WlJp" },
            { "wallSlideCounter", "WlSl" },
            { "superLaunchJump", "SprL" },
            { "shootUpCounter", "ShUp" },
            { "jumpBoost", "JmpB" },
            // 姿态窗口
            { "lowerBodyFramesOnGround", "LwOn" },
            { "lowerBodyFramesOffGround", "LwOf" },
            { "upperBodyFramesOnGround", "UpOn" },
            { "upperBodyFramesOffGround", "UpOf" },
            { "consistentDownDiagonal", "Cons" },
            // 水 / 呼吸 / 速度 / 增益
            { "airInLungs", "Air" },
            { "isSubmerged", "Subm" },
            { "initRunSpeedFac", "RunF" },
            { "aerobicLevel", "Aero" },
            { "mushroomEffect", "Mush" },
            // 角色专属
            { "pyroJumpCounter", "Pyro" },
            { "pyroParryCooldown", "Pary" },
            // 杆 / 管道
            { "forceFeetToHorizontalBeam", "FFtB" },
            { "wantToGrab", "Grab" },
            { "straightUpOnHorizontalBeam", "SUHB" },
            { "poleSkipPenalty", "Pole" },
            { "goIntoCorridorClimb", "Into" },
            { "corridorTurnCounter", "CTur" },
            { "timeSinceInCorridorMode", "TCor" },
            { "corridorDrop", "CDrp" },
            { "crawlTurnDelay", "Crwl" },
            { "landingDelay", "Land" },
            { "ledgeGrabCounter", "Ledg" },
            { "backwardsCounter", "Back" },
            { "simulateHoldJumpButton", "SimJ" },
            { "exitBellySlideCounter", "Bely" },
            { "canCorridorJump", "CdJp" },
            { "horizontalCorridorSlideCounter", "HCor" },
            { "verticalCorridorSlideCounter", "VCor" },
            { "swimForce", "Swim" },
            { "dynamicRunSpeed", "DynS" },
            { "dynamicRunSpeed0", "Dyn0" },
            { "dynamicRunSpeed1", "Dyn1" },
            { "runSpeedRatio", "SpdR" },
            { "corridorSlideTicksLeft", "CSli" },
            // is* 系列统一 I + 前 3 字母，避免和计数器撞名
            { "isDefault", "IDef" },
            { "isCrawl", "ICrw" },
            { "isStand", "IStd" },
            { "isCorridorClimb", "ICor" },
            { "isClimbIntoShortCut", "ICut" },
            { "isWallClimb", "IWCl" },
            { "isClimbingOnBeam", "IBem" },
            { "isSwimming", "ISwm" },
            { "isZeroG", "IZer" },
            { "isStunned", "IStn" },
            { "isDead", "IDed" },
            { "isRoll", "IRol" },
            { "isFlip", "IFlp" },
            { "isRocketJump", "IRck" },
            { "isBellySlide", "IBel" },
            { "isSurfaceSwim", "ISur" },
            { "isDeepSwim", "IDep" },
            { "isCrawlTurn", "ICtT" },
            { "isStandUp", "IStU" },
            { "isDownOnFours", "IDwn" },
            { "isLedgeCrawl", "ILdC" },
            { "isLedgeGrab", "ILdG" },
            { "isHangFromBeam", "IHng" },
            { "isGetUpOnBeam", "IGtU" },
            { "isStandOnBeam", "IStB" },
            { "isClimbOnBeam", "IClB" },
            { "isGetUpToBeamTip", "IGtT" },
            { "isHangUnderVerticalBeam", "IHUV" },
            { "isBeamTip", "IBmT" },
            { "isCorridorTurn", "ICoT" },
            { "isNone", "INon" },
            { "isAntlerClimb", "IAnt" },
            { "isGrapplingSwing", "IGrp" },
            { "isZeroGSwim", "IZSw" },
            { "isZeroGPoleGrab", "IZPG" },
            { "isVineGrab", "IVin" },
        };


        输入
        /// 跑 **一个 tick**。
        ///
        /// ★★ v0.22.131：**这两行以前都是错的**（用户问"手里那个物品在沙盒房间里真的有物理作用吗"时
        /// 顺着查出来的，两处都会让"包络"算出来的数字不可信）：
        ///
        /// ① **输入根本没生效**。以前是：
        ///      `for (n=input.Length-1; n>0; n--) input[n]=input[n-1];  Cat.input[0]=ip;  Cat.Update(eu);`
        ///    可是 `Player.Update` 内部会自己调 `checkInput()`（`Player.cs:6376`），而 `checkInput`
        ///    **第一件事就是把输入历史再移一位**（`:6971-6974`），然后
        ///    `input[0] = controller.GetInput()` / `AI.Update()` / **`RWInput.PlayerInput(playerNumber)`**（`:6980-6993`）
        ///    ⇒ 我们设的 `ip` 被挤到 `input[1]`，`input[0]` 变成**真键盘/空输入**，
        ///    而 `MovementUpdate`（`:6631`）读的是 `input[0]` ⇒ **沙盒里跑的根本不是我们给的输入**。
        ///    修法：走游戏**自己的**注入通道 —— `checkInput` 的第一个分支就是
        ///    `if (controller != null) input[0] = controller.GetInput();`，
        ///    所以我们临时给影子猫挂一个 `SimController`（`PlayerController` 只有 `GetInput()` 一个虚方法，
        ///    `Player.cs:1083-1093`），它返回当前这一 tick 要注入的输入；跑完立刻还原成 `null`。
        ///    历史移位交给 `checkInput` 自己（**不再自己移**，否则会移两次）。
        ///
        /// ② **沙盒里只有猫在动**。以前只 `Cat.Update(eu)`；而真实房间是遍历 `updateList`
        ///    （`Room.cs:5058-5089`），对每个对象 `Update(eu)`、再 `graphicsModule.Update()` +
        ///    `GraphicsModuleUpdated(actuallyViewed, eu)`。**手里的东西就靠后者** ——
        ///    `Player.GraphicsModuleUpdated`（`:6721-6830+`）里按质量比推拉双方体块、
        ///    对 `ObjectGrabability.Drag` 的重物施加拖拽力 ⇒ **它绝不只是"图形"**。
        ///    不遍历 `updateList` 的话：掷出去的矛**不会飞**（飞行在 `Spear.Update` 里），
        ///    重物的拖拽/负重也全都不会发生。⇒ 现在照 `Room.Update` 一模一样地遍历。
        /// </summary>
        public void StepOneTick(Player.InputPackage ip)
        {
            if (!Built) return;
            Player.PlayerController saved = null;
            try
            {
                // 让 `checkInput` 从我们这里取输入（见上面 ①）
                saved = Cat.controller;
                _simCtrl.Input = ip;
                Cat.controller = _simCtrl;

                // 影子猫的图形模块必须先存在：`Room.Update` 是按 `graphicsModule != null`
                // 决定给 `GraphicsModuleUpdated(actuallyViewed: true)` 的，
                // 否则物理会**取决于影子猫可不可见**（那是不可接受的不忠实）。
                if (Cat.graphicsModule == null)
                {
                    try { Cat.InitiateGraphicsModule(); } catch { }
                }

                TickRoom();
                // ★★★ v0.22.134：**步进之后立刻归位**（管道逻辑可能趁这一 tick 把猫塞进真房间）
                EnforceSandbox();
            }
            catch (Exception e)
            {
                if (!_stepErrorLogged) { _stepErrorLogged = true; _log("[影子] ✗ 单 tick 模拟异常（只报一次）：\n" + e); }
            }
            finally
            {
                try { Cat.controller = saved; } catch { }
            }
            _eu = !_eu;            // `game.evenUpdate` 每 tick 翻转 ⇒ 一帧连跑 N tick 时必须自己翻
            TicksRan++;
        }

官方AI输入：
SlugNPCAI.Move();
官方玩家输入：
RWInput
	private static Player.InputPackage PlayerInputLogic(int categoryID, int playerNumber)
	{
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		Player.InputPackage result = default(Player.InputPackage);
		Controller newController = PlayerRecentController(playerNumber);
		Custom.rainWorld.options.controls[playerNumber].UpdateActiveController(newController);
		result.controllerType = Custom.rainWorld.options.controls[playerNumber].GetActivePreset();
		result.gamePad = result.controllerType != Options.ControlSetup.Preset.KeyboardSinglePlayer && result.controllerType != Options.ControlSetup.Preset.None;
		Options.ControlSetup controlSetup = Custom.rainWorld.options.controls[playerNumber];
		switch (categoryID)
		{
		case 0:
			if (controlSetup.GetButton(0))
			{
				result.jmp = true;
			}
			if (controlSetup.GetButton(4))
			{
				result.thrw = true;
			}
			if (controlSetup.GetButton(11))
			{
				result.mp = true;
			}
			if (controlSetup.GetButton(3))
			{
				result.pckp = true;
			}
			if (controlSetup.GetButton(34))
			{
				result.spec = true;
			}
			result.analogueDir = new Vector2(controlSetup.GetAxis(1), controlSetup.GetAxis(2));
			break;
		case 1:
			if (controlSetup.GetButton(8))
			{
				result.jmp = true;
			}
			if (controlSetup.GetButton(9))
			{
				result.thrw = true;
			}
			if (controlSetup.GetButton(13))
			{
				result.mp = true;
			}
			result.analogueDir = new Vector2(controlSetup.GetAxis(6), controlSetup.GetAxis(7));
			break;
		}
		result.analogueDir = Vector2.ClampMagnitude(result.analogueDir * (ModManager.MMF ? Custom.rainWorld.options.analogSensitivity : 1f), 1f);
		if (Custom.rainWorld.options.controls[playerNumber].xInvert)
		{
			result.analogueDir.x *= -1f;
		}
		if (Custom.rainWorld.options.controls[playerNumber].yInvert)
		{
			result.analogueDir.y *= -1f;
		}
		if (result.analogueDir.x < -0.5f)
		{
			result.x = -1;
		}
		if (result.analogueDir.x > 0.5f)
		{
			result.x = 1;
		}
		if (result.analogueDir.y < -0.5f)
		{
			result.y = -1;
		}
		if (result.analogueDir.y > 0.5f)
		{
			result.y = 1;
		}
		if (ModManager.MMF)
		{
			if (result.analogueDir.y < -0.05f || result.y < 0)
			{
				if (result.analogueDir.x < -0.05f || result.x < 0)
				{
					result.downDiagonal = -1;
				}
				else if (result.analogueDir.x > 0.05f || result.x > 0)
				{
					result.downDiagonal = 1;
				}
			}
		}
		else if (result.analogueDir.y < -0.05f)
		{
			if (result.analogueDir.x < -0.05f)
			{
				result.downDiagonal = -1;
			}
			else if (result.analogueDir.x > 0.05f)
			{
				result.downDiagonal = 1;
			}
		}
		return result;
	}


    --
    构建 Player.InputPackage 然后返回给上游
    default(Player.InputPackage) 创建一个结构体实例
    new InputPackage(gamePad: false, Options.ControlSetup.Preset.None, 0, 0, jmp: false, thrw: false, pckp: false, mp: false, crouchToggle: false);
    public struct InputPackage
	{
		public int x;

		public int y;

		public bool jmp;

		public bool thrw;

		public bool pckp;

		public bool mp;

		public bool spec;

		public bool gamePad;

		public Options.ControlSetup.Preset controllerType;

		public bool crouchToggle;

		public Vector2 analogueDir;

		public int downDiagonal;

		public IntVector2 IntVec => new IntVector2(x, y);

		public IntVector2 ZeroGGamePadIntVec
		{
			get
			{
				if (analogueDir.magnitude > 0.2f)
				{
					return new IntVector2((Mathf.Abs(analogueDir.x) > 0.1f) ? ((int)Mathf.Sign(analogueDir.x)) : 0, (Mathf.Abs(analogueDir.y) > 0.1f) ? ((int)Mathf.Sign(analogueDir.y)) : 0);
				}
				return IntVec;
			}
		}

		public bool AnyInput
		{
			get
			{
				if (!AnyDirectionalInput && !jmp && !thrw && !pckp)
				{
					return spec;
				}
				return true;
			}
		}

		public bool AnyDirectionalInput
		{
			get
			{
				if (x == 0 && y == 0)
				{
					return analogueDir != Vector2.zero;
				}
				return true;
			}
		}

		public InputPackage(bool gamePad, Options.ControlSetup.Preset controllerType, int x, int y, bool jmp, bool thrw, bool pckp, bool mp, bool crouchToggle)
			: this(gamePad, controllerType, x, y, jmp, thrw, pckp, mp, crouchToggle, spec: false)
		{
		}

		public InputPackage(bool gamePad, Options.ControlSetup.Preset controllerType, int x, int y, bool jmp, bool thrw, bool pckp, bool mp, bool crouchToggle, bool spec)
		{
			this.gamePad = gamePad;
			this.controllerType = controllerType;
			this.x = x;
			this.y = y;
			this.jmp = jmp;
			this.thrw = thrw;
			this.pckp = pckp;
			this.spec = spec;
			this.mp = mp;
			this.crouchToggle = crouchToggle;
			analogueDir = new Vector2(0f, 0f);
			downDiagonal = 0;
		}
	}
    // playerNumber的作用是用于从输入层获取对应玩家的输入，而我们不需要这个，
    // 但我们可以利用RWInput.PlayerInput(playerNumber)来获取玩家的真实输入，再按需改造它
    public static Player.InputPackage PlayerInput(int playerNumber)
    {
        Player.InputPackage result = default(Player.InputPackage);
    }
    现在问题变成，怎么将构建的InputPackage注入玩家输入中？
    答案：
    PlayerController实例.Input = InputPackage实例;
    Player实例.controller = PlayerController实例;
    错！
    游戏最终读取输入看的是input[0]
    而每次更新游戏帧时，
    Player会用checkInput()检查输入
    如果controller != null，就input[0] = controller.GetInput(); // ！！！给程序用
    如果 AI != null 就 AI.Update(); // 给 ai 用
    否则 input[0] = RWInput.PlayerInput(num2); // 给玩家用
    因此，我们需要构筑的是一整个controller虽然controller只有GetInput()有用，最终生效的InputPackage是controller.GetInput()返回的那个。

	public abstract class PlayerController
	{
		public PlayerController()
		{
		}

		public virtual InputPackage GetInput()
		{
			return new InputPackage(gamePad: false, Options.ControlSetup.Preset.None, 0, 0, jmp: false, thrw: false, pckp: false, mp: false, crouchToggle: false);
		}
	}

    因此我们的做法是：
    1. 实例IP = new InputPackage(gamePad: false, Options.ControlSetup.Preset.None, 0, 0, jmp: false, thrw: false, pckp: false, mp: false, crouchToggle: false);
    2. 实例PC = new 定制的PlayerController(实例IP);
    3. Player实例.controller = 实例PC;
    游戏每帧都会自动将input向后移动一位，因此不用手动为输入收尾。（不过，谁会把contoller置空呢？如果不置空，那么下一帧它还是会生效 已确认会被游戏自动置空）
    注意，这个做法会完全屏蔽玩家的输入和AI的输入。

    关于SlugNPCAI：
    DecideBehavior() 是用于决定行为的方法
    SlugNPCAI 的行为控制是通过input[0] = InputPackage实例 直接控制的，而非controller
    所以，controller更像是专门留给开发者的接口
    
    现在的问题：
    1. 怎么让玩家猫变成AI猫？使之拥有SlugNPC的性格和行为？
    2. SlugNPCAI的控制接口是什么？要怎么通过接口控制AI猫的行为？比如，像Moba游戏一样让蛞蝓猫前往指定地点？拾取指定物品？
    3. SlugNPCAI的数据接口是什么？我怎么知道蛞蝓猫准备去哪？

    1. 线索
    AbstractCreature
    MSCInitiateAI()
    if (ModManager.MSC)
        if (creatureTemplate.TopAncestor().type == MoreSlugcatsEnums.CreatureTemplateType.SlugNPC)
            abstractAI.RealAI = new SlugNPCAI(this, world);
    // this = AbstractCreature实例
    // world 是父类的父类的...的属性
    // 访问公开属性 abstractAI.RealAI 
    // 访问公开属性 abstractAI.world‘
    // SlugNPCAI内部会直接给参数以注入SlugNPCAI实例(也就是方法里的this)
    // 所以，对于一个Player，想让它变成AI，只需执行 new SlugNPCAI(Player as AbstractCreature,Player.world);
    // 但最稳妥的方式还是 让条件 base.abstractCreature.creatureTemplate.TopAncestor().type == MoreSlugcatsEnums.CreatureTemplateType.SlugNPC 成立，然后调用MSCInitiateAI

    类AbstractCreatureAI似乎有关于AI接口的线索

旧项目
public sealed class NpcGoalEngine
    private void ApplyAbstractLayer(SlugNpcAI ai, NpcGoal g,AbstractCreature followWho)
    public void ApplyIntent(Player cat, SlugNpcAI ai)

源码 关于朋友
if (AI.creature.Room.creatures[k].ID == AI.creature.state.socialMemory.relationShips[j].subjectID && AI.creature.Room.creatures[k].realizedCreature != null)
{
    friend = AI.creature.Room.creatures[k].realizedCreature;
    friendRel = AI.creature.state.socialMemory.relationShips[j];
    break;
}
SlugNPCAI
    Update()
        if (base.friendTracker.friend == null && cat.room != null && cat.room.abstractRoom.shelter)
        {
            for (int i = 0; i < cat.room.game.Players.Count; i++)
            {
                if (cat.room.game.Players[i].realizedCreature != null && cat.room.game.Players[i].realizedCreature.room == cat.room)
                {
                    SocialMemory.Relationship orInitiateRelationship = cat.State.socialMemory.GetOrInitiateRelationship(cat.room.game.Players[i].ID);
                    orInitiateRelationship.InfluenceLike(1f);
                    orInitiateRelationship.InfluenceTempLike(1f);
                }
            }
        }
房间中的蛞蝓猫将通过GetOrInitiateRelationship自动进入AI.creature.state.socialMemory.relationShips
FriendTracker
    Update()
    ...
        if (!(AI.creature.state.socialMemory.relationShips[j].like > 0.5f) || !(AI.creature.state.socialMemory.relationShips[j].tempLike > 0.5f))
        {
            continue;
        }
        ...
但FriendTracker是否执行需要门槛 like > 0.5 && tempLike > 0.5，过了它，才有base.friendTracker.friend != null

SocialMemory
    Relationship
        public float like
        public float tempLike

而这两个字段都是公开的，因此，可以直接设置为1

配置界面

Player1 Player2 Player3 Player4
乙币图标 乙币图标 乙币图标 乙币图标（改成蛞蝓猫一脸茫然的表情头像+乙币印章/蛞蝓猫一脸自信的表情头像+开智印章）参考 牧原 表情
ID __   ID __   ID __   ID __  
[] AI   [] AI   [] AI   [] AI  
勾选后就显示 开智图标
全局：
智能对话（开启测本地2B模型，能接收并执行指令 目标是能听懂人话，并且覆盖dev console的大部分指令和开发者功能 生成物体、移除生物等等）
触发后会询问玩家获得许可，玩家回复可决定是否执行
云端模型（提供API）

如何实现？
给AI提供一个说明书，并给出调用接口让AI用(参考之前的幕间系统，AI只管返回json)

这只蛞蝓猫必须能起到带路的功能，约等于向导（没错，就是向导！陪伴型猫崽+向导NPC）

通过反射执行私有方法：
ConsoleManager.Instance?.LogDebug($"{(typeof(Player).GetMethod("Grabability", BindingFlags.NonPublic | BindingFlags.Instance)).Invoke(self,new []{obj})}");


			if (ModManager.MSC && rainWorld.safariMode)
			{
				AbstractCreature abstractCreature8 = new AbstractCreature(world, StaticWorld.GetCreatureTemplate("Overseer"), null, new WorldCoordinate(num, 15, 25, -1), new EntityID(-1, 0));
				world.GetAbstractRoom(num).AddEntity(abstractCreature8);
				cameras[0].followAbstractCreature = abstractCreature8;
				(abstractCreature8.abstractAI as OverseerAbstractAI).safariOwner = true;
				abstractCreature8.ignoreCycle = true;
				GetStorySession.saveState.deathPersistentSaveData.karma = 0;
			}