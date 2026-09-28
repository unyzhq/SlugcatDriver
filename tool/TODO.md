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