# 野兽苹果 Boss 架构说明

> 本文说明地下洞穴关卡（`underground.tscn`）里**野兽苹果（Beast Apple）**这只 Boss 的
> 代码组织、职责边界、状态机与关键设计取舍。
>
> 目标：读完能回答三个问题——
> ① 它由哪几个文件组成、各管什么；② 一场战斗的时间线是怎么跑的；③ 为什么当初要这么拆。
>
> 通用分层与全局服务见 [ARCHITECTURE.md](ARCHITECTURE.md)，本文只讲这只 Boss。

---

## 1. 定位与规模

| 项 | 值 |
|---|---|
| 出现场景 | `underground.tscn`（地下洞穴，第二关） |
| 战场范围 | `Rect2(21970, 0, 1580, 500)`，地面 `y = 493`，天花板 `y = 37` |
| 出场坐标 | `(22750, 100)`，初始倒挂在天花板上 |
| 血量 / 伤害 | 1000 / 30（场景里配的） |
| 代码规模 | 7 个 `.cs`，约 2050 行 |
| 素材 | 5 张静态帧 + 1 张 6 帧碎裂序列 |

和第一关的大苹果 Boss（`BossEnemy.cs`）**没有任何代码复用**，是完全独立的一套实现。

---

## 2. 文件清单

| 文件 | 行数 | 职责 |
|---|---:|---|
| `BeastAppleBoss.cs` | 1179 | 本体：状态机、位移、命中判定、半血召唤、死亡 |
| `BeastArena.cs` | 329 | 战场：开战触发、镜头锁定、顶部血条、空气墙、震屏 |
| `BeastSpitBall.cs` | 132 | 从嘴里吐出的弹道（抛物线下落 + 翻滚 + 红色残影） |
| `BeastShockwave.cs` | 121 | 冲击波光圈 / 落地烟尘 / 爆炸碎片，一次性 |
| `BeastLungeWarning.cs` | 110 | 技能 1 的地面安全圈（纯 `_Draw`） |
| `BeastExplosion.cs` | 112 | 旧版火球爆炸（**当前未使用**，见 §9） |
| `BeastShatter.cs` | 66 | 死亡碎裂序列播放 |

素材（`assets/boss/beast/`）：

| 文件 | 尺寸 | 用途 |
|---|---|---|
| `beast_front.png` | 352 × 400 | 正面 / 倒挂 / 坠落 |
| `beast_right.png` | — | 朝右（走位、冲锋、飞扑） |
| `beast_left.png` | — | 朝左 |
| `beast_roar.png` | 357 × 400 | 吼叫帧（张嘴最大） |
| `beast_shatter.png` | 4500 × 750 | 死亡碎裂，6 帧 × 750 × 750 |
| `beast_explode.png` | 3680 × 460 | 旧版火球，8 帧（当前未使用） |

---

## 3. 场景挂法

```
BeastArena            Node2D            ← 战场管理器，脚本 BeastArena.cs
└─ BeastAppleBoss     CharacterBody2D   ← position (22750, 100)
   │                  collision_layer = 4（敌人层）· mask = 1（只挡地形）
   ├─ CollisionShape2D                   ← 尺寸由代码按贴图算，场景里只是占位
   ├─ Sprite2D                           ← 名字必须叫 Sprite2D（父类硬取）
   ├─ HurtArea         Area2D            ← mask = 2（只收玩家），受击判定
   │  └─ CollisionShape2D
   ├─ RayLeft          RayCast2D         ← 父类硬取，缺了会抛异常
   └─ RayRight         RayCast2D
```

**Boss 必须是 `BeastArena` 的子节点**——`BeastArena` 在 `_Ready` 里遍历子节点找 `BeastAppleBoss`，
找不到就只打一条警告、整场战斗不会启动。

`Sprite2D` / `RayLeft` / `RayRight` / `HurtArea` 这四个名字是 `Enemy._Ready()` 里
`GetNode<>` 硬取的，改名或删掉会直接抛 `NullReferenceException`。

---

## 4. 职责边界：为什么拆成 7 个文件

一句话原则：**Boss 只管"什么时候做什么动作"，"动作看起来什么样"和"战场怎么配合"全部外包。**

```
                 ┌──────────────────────────────┐
                 │        BeastAppleBoss        │
                 │  唯一的状态机持有者           │
                 └───┬───────┬───────┬──────┬───┘
        new 出来就丢 │       │       │      │ 发信号
                    ▼       ▼       ▼      ▼
        LungeWarning  Shockwave  SpitBall  Shatter
          (地面圈)     (光圈)    (弹道)    (碎裂)
                    │
                    │ EmitSignal(ShakeRequested)
                    ▼
                 ┌──────────────────────────────┐
                 │         BeastArena           │
                 │ 镜头 / 血条 / 空气墙 / 震屏   │
                 └──────────────────────────────┘
```

### 4.1 为什么震屏要发信号，而不是直接摇镜头

Boss 只说「**现在该震多强、震多久**」，具体怎么震由 `BeastArena` 决定。
好处是 Boss 完全不知道相机存在，单元上更干净；坏处是震屏实现藏在另一个文件里，
调试时要两个文件对着看。**这个取舍是故意的**——震屏是"战场表现"，不是"Boss 行为"。

### 4.2 为什么预警圈用 `_Draw` 而不是贴图

绿色安全圈的宽度必须等于 `ArenaRect.Size.X × DodgeZoneRatio`。
一旦以后改战场宽度或躲避比例，贴图就得重新出一张；`_Draw` 是参数驱动，改完立刻生效。

同理，红色飞扑轨道最早也是 `_Draw` 画的，后来按手感要求删掉了（见 §6.3）。

### 4.3 为什么碎裂是独立节点

Boss 本体在死亡那一刻要**先消失**（贴图藏掉），碎裂是接着演的独立东西。
挂在一起就没法做到"本体先没、碎片继续飞"。

---

## 5. 状态机

### 5.1 十二个状态

| 状态 | 含义 | 出口 |
|---|---|---|
| `Dormant` | 倒挂休眠，无伤害碰撞 | 玩家靠近 → `Falling` |
| `Falling` | 从天花板砸向平台 | 落地 → `Roar`（半血时也从 `PhaseWait` 回到这里） |
| `Roar` | 落地吼叫 + 持续震屏 | 计时结束 → `Idle` |
| `Idle` | 站定呼吸，随机选技能 | → `Reposition` |
| `Reposition` | **弧线跳到战场一侧边界** | 落地 → `LungeCharge` / `ChargeUp` |
| `LungeCharge` | 技能 1 蓄力，地面绿圈闪烁 | 计时结束 → `LungeDash` |
| `LungeDash` | 技能 1 弧线飞扑 | 到达对面边界 → `Idle` |
| `ChargeUp` | 技能 2 斜向上冲 | 撞天花板 → `ChargeDown` |
| `ChargeDown` | 技能 2 斜向下冲 | 落地 → 没到对面就回 `ChargeUp`，到了 → `Idle` |
| `PhaseJump` | 半血：飞回天花板中心 | 到位 → `PhaseHang` |
| `PhaseHang` | 半血：倒挂吐怪 | 吐完 → `PhaseWait` |
| `PhaseWait` | 半血：挂 5 秒 | 跳下 → `Falling` |

### 5.2 为什么不调父类的 `_PhysicsProcess`

`BeastAppleBoss` 继承 `Enemy`，但**完全重写了 `_PhysicsProcess`，一行都不调 `base`**。

原因：`Enemy._PhysicsProcess` 是"左右巡逻 + 掉头 + 接触扣血"，而且它每帧都会
`Velocity = new Vector2(ComputeHorizontalVelocity(dt), Velocity.Y)` **覆盖水平速度**——
对 Boss 的位移是灾难性的。

父类只保留三样东西：血量 `_hp`、受击 `TryTakeHit()`、死亡信号 `Died`。

> **代价**：`Enemy._hitInvuln` 的递减写在父类物理帧里，Boss 不调父类就永远不减。
> 所以 `Enemy.cs` 里把它从 `private` 改成了 `protected`，Boss 自己每帧减一次。
> 不补这一行的话，表现是「Boss 只吃第一下伤害，之后所有攻击都无效」。

### 5.3 半血判定必须每帧查

`CheckPhaseTwo()` 在 `_PhysicsProcess` 里**每帧调用**，而不是放在 `EnterIdle()` 里。

早期版本只在 `EnterIdle` 里检查一个 `_phaseTwoPending` 标志，但那个标志从来没人设置过，
结果 Boss 一路打到 0 血都没进半血阶段。血线判定是"随时可能跨过"的事件，不能挂在某个状态入口上。

优先级规则：如果正在放技能（`Reposition` / `LungeCharge` / `LungeDash` / `ChargeUp` / `ChargeDown`），
只置 `_phaseTwoPending = true`，**等回到 `Idle` 再执行**，绝不打断当前技能。

---

## 6. 技能 1：横贯飞扑

### 6.1 三步走

```
Idle
 └─① Reposition   0.6 秒弧线跳到左边界或右边界（不是瞬移）
     └─② LungeCharge  2 秒蓄力，地面绿圈闪烁
         └─③ LungeDash   沿正弦拱从这一侧飞到另一侧
             └─ Idle
```

### 6.2 为什么必须先走位到边界

最早的版本是「站在原地蓄力 3 秒 → 瞬移到边界 → 横穿」。两个问题：

1. **瞬移看不见过程**，玩家觉得 Boss 凭空出现在脸上；
2. **蓄力时 Boss 站在战场中间**，它的碰撞体比玩家宽，把玩家挡在绿圈外面——
   「站进圈里蹲下」这件事在物理上根本做不到。

改成先跳到边界再蓄力，中间 316px 的安全圈就彻底空出来了。

### 6.3 弧线飞扑

`X` 匀速、`Y` 走一个正弦拱：

```
y = 地面中心Y − LungeArcHeight × sin(π × 进度)
```

`LungeArcHeight = 70`。这个值调过两次：

- 120 时 Boss 的脚离地 56px，**从玩家头顶飞过去，看着完全不疼**；
- 70 时脚正好擦着站着玩家的头皮，视觉上对得上"被扫到"。

### 6.4 命中判定：按横扫的 X 轨迹，不按身体矩形

这是全篇最容易踩的坑。

飞扑改成弧线后，拱顶时 Boss 的身体底边在地面上方 56px，而玩家只有 64 高——
**纯几何求交会让玩家从 Boss 肚子底下穿过去，站着也不掉血。**

所以判定改成：

```csharp
// 只看这一帧走过的横向区间有没有和玩家的 X 重叠
float lo = min(上一帧X, 当前X) - 身体半宽;
float hi = max(上一帧X, 当前X) + 身体半宽;
if (玩家右边界 < lo || 玩家左边界 > hi) return;   // 没扫到
if (玩家顶边 > 地面 + 8) return;                  // 站在上层平台不算
```

Y 只用来排除"站在更高平台上"的情况，不参与命中。

**免伤规则**只有一条，写在需求里：站在战场中心 20% 宽（316px）的绿圈内 + 按 C 蹲下 → 完全不掉血。
这条规则在代码里是**独立的布尔判断**，不依赖几何碰撞，所以蹲下时即使身体和 Boss 重叠也能躲开。

### 6.5 飞扑期间必须关掉接触伤害

`_PhysicsProcess` 末尾那段：

```csharp
if (_st != St.Falling && _st != St.PhaseJump && _st != St.LungeDash
    && _st != St.Reposition)
    CheckContactDamage();
```

不关的话，Boss 从玩家身上压过去时，接触判定会**绕过绿圈规则**，变成"蹲下也照样掉血"。
`Reposition` 也一并关掉：那只是换位置，不是攻击，不该顺手把人撞死。

---

## 7. 技能 2：斜向穿梭冲锋

### 7.1 之字形弹到对面

```
Reposition（跳到出发侧边界）
 └─ ChargeUp    以 ChargeJumpVelocity 斜向上冲，X 速度恒定
     └─ ChargeDown  撞天花板后斜向下落
         ├─ 还没到对面边界 → ChargeUp（再弹一次）
         └─ 到了对面边界    → Idle
```

早期版本只弹一段就收工，看起来"跳两下就没了"。现在用 `ChargeReachedEnd()`
（X 到对面边界 6px 以内）决定是否继续，`ChargeMaxBounces = 10` 兜底防止速度调太慢时无限弹。

### 7.2 撞击光圈要跟着 Boss 跑

冲锋速度可以调得很高（跳跃初速度调到 1200 时，Boss 一秒能横甩几百像素）。
冲击波光圈如果钉死在撞击点，等它扩到最大时 Boss 早飞走了——**看起来就是"光圈追不上 Boss"**。

解决：光圈节点带 `FollowTarget`，每帧把自己对齐到 Boss 身上。

| 参数 | 效果 |
|---|---|
| `ChargeRingFollowY = true`（默认） | 光圈整个套在 Boss 身上，X / Y 都跟 |
| `ChargeRingFollowY = false` | 只锁 X，Y 停在撞击面，变成贴着天花板 / 地面滚的波纹 |

> 实现细节：光圈的 Y 锚点在**第一次 `_Process`** 时捕获，不能在 `_Ready` 里——
> 调用方是「先 `AddChild`、再设 `GlobalPosition`」，`_Ready` 那一刻位置还是 `(0,0)`。

---

## 8. 半血阶段

触发条件：血量 ≤ `PhaseHpPercent`（50%），**全局只触发一次**（`_phaseTwoDone`）。

```
PhaseJump   0.75 秒从地面飞回战场中心，途中翻成倒挂
 └─ PhaseHang   每 1 秒吐一只恐怖苹果，共 4 只
     └─ PhaseWait   挂 5 秒
         └─ Falling → Roar → Idle（回到技能 1 / 2 循环）
```

### 8.1 吐出来的怪是代码拼的

`SpawnHorrorApple()` 不用场景实例，直接 `new HorrorAppleEnemy` 拼节点树。有三个必须显式给的东西：

1. **`Name` 必须带序号**（`spit_apple_1`、`spit_apple_2`…）——
   同批 `AddChild` 的重名节点会被 Godot 改成 `@CharacterBody2D@59` 这种自动名，之后再也认不出来。
2. **`Sprite2D` 的 `Name` 必须显式写**——代码 `new` 出来的节点默认名为空，
   入树后变成 `@Sprite2D@xx`，而 `Enemy._Ready()` 里是 `GetNode<Sprite2D>("Sprite2D")` 硬取的。
3. **`HpBar` 必须自己挂**——`Enemy._Ready()` 里是 `GetNodeOrNull<TextureProgressBar>("HpBar")`，
   没有这个节点血条就永远不显示。

### 8.2 追踪只对 Boss 吐的生效

`HorrorAppleEnemy` 新增了两个导出字段：

| 字段 | 默认 | 说明 |
|---|---|---|
| `ChasePlayer` | `false` | 勾上后每次起跳前把朝向改成玩家那一侧 |
| `ChaseRange` | `1400` | 超出这个水平距离就不追 |

**默认 `false`**，所以场景里原有的 `horrorapple` 行为完全不变；
只有 `BeastAppleBoss.SpawnHorrorApple()` 里显式设成 `true`。

追踪只在"起跳前"和"站着等"两个时机改朝向，飞扑途中不改——飞到一半转身会变成原地抽搐。
贴身 24px 内也不翻面，否则左右两套帧会闪。

---

## 9. 死亡演出

### 9.1 流程

```
血量归零 → OnDeath()
  ├─ 关掉本体碰撞（尸体不挡子弹）
  ├─ 停掉可能还在的绿圈
  └─ EmitSignal(Defeated) → BeastArena 延迟 1.7 秒再拆墙收镜头

0.25 秒后 → PlayExplosion()
  ├─ ① 本体贴图 Visible = false
  ├─ ② 生成 BeastShatter（6 帧 × 0.1 秒 = 0.6 秒）
  ├─ ③ 两圈冲击波
  └─ ④ 16 块四散碎屑
```

### 9.2 为什么延迟 1.7 秒收镜头

爆炸 + 碎裂要放 0.6~2.4 秒。如果 `OnDefeated` 里当场拆墙、把镜头从 0.52 拉回 1.0，
碎裂会瞬间缩成一小块，演出全废。所以 `BeastArena` 用 `CreateTimer(1.7)` 延后 `TearDown()`。

### 9.3 碎裂素材是怎么来的

`beast_shatter.png` 是 Seedream 生成的 6 帧横排序列（纯黑底），
用 `tools/beast/split_shatter.py` 抠底 + 切帧。

**切帧不能等宽切**：模型把 6 帧摆得忽左忽右（前四帧挤在左边），等宽切会把第 2 帧切成两半。
脚本改成**按黑色空隙切**：统计内容列 → 得到若干内容段 → 反复合并"空隙最窄的相邻两段"直到正好 6 段
→ 每段量包围盒居中成正方形。

### 9.4 缩放对齐

`BeastShatter` 的缩放按「**一帧高度 = Boss 的 `BodyHeight`**」算：

```csharp
float s = TargetHeight / Sheet.GetHeight();   // 200 / 750
```

这样碎片和本体大小完全对得上，不会出现"200 高的 Boss 炸出 400 高的碎片"。

### 9.5 `BeastExplosion.cs` 目前是死代码

旧版死亡演出是一个 8 帧火球（`beast_explode.png`）。
改成本体碎裂后，`PlayExplosion()` 不再创建 `BeastExplosion`，
场景里的 `ExplosionSheet` 属性也变成了无效配置。

文件保留着，想加回"碎裂 + 火光"的双层演出随时可以用。

---

## 10. 数值总表

场景里覆盖过的值单独标出。

### 战斗区域

| 参数 | 值 | 说明 |
|---|---|---|
| `ArenaRect` | `(21970, 0, 1580, 500)` | 战场 AABB |
| `FloorY` | 493 | 地面顶边（实测 `StaticBody2D5`） |
| `CeilingY` | 37 | 天花板底边（同一个节点的另一个碰撞体） |
| `TriggerRangeX` | 250 | 玩家水平离多近触发 |
| `EdgeMargin` | 46 | 中心离左右边界留多少 |

### 开场 / 节奏

| 参数 | 值 | 说明 |
|---|---|---|
| `FallGravity` | 2800 | 坠落重力，比常规大，砸得狠 |
| `RoarTime` | 1.6 | 吼叫时长 |
| `IdleMin` / `IdleMax` | 1.2 / 2.0 | 两次技能之间歇多久 |
| `LungeChance` | 0.5 | 选技能 1 的概率 |

### 技能 1

| 参数 | 值 | 场景覆盖 |
|---|---|---|
| `LungeChargeTime` | 2.0 | **1.5** |
| `LungeSpeed` | 1500 | **2000** |
| `LungeArcHeight` | 70 | — |
| `DodgeZoneRatio` | 0.2 | — |
| `LungeRecover` | 1.0 | — |
| `RepositionTime` | 0.6 | — |
| `RepositionArcHeight` | 170 | — |

### 技能 2

| 参数 | 值 | 场景覆盖 |
|---|---|---|
| `ChargeXSpeed` | 430 | — |
| `ChargeJumpVelocity` | 820 | **1200** |
| `ChargeRecover` | 1.0 | — |
| `ChargeMaxBounces` | 10 | — |
| `ChargeRingDuration` | 0.26 | — |
| `ChargeRingFollowY` | `true` | — |

### 半血 / 死亡

| 参数 | 值 | 说明 |
|---|---|---|
| `PhaseHpPercent` | 50 | 触发血线 |
| `SpawnCount` | 4 | 吐几只 |
| `SpawnInterval` | 1.0 | 每只间隔 |
| `PhaseHangTime` | 5.0 | 吐完挂多久 |
| `ExplodeTime` | 2.4 | 死亡演出总时长 |
| `ShatterFrames` | 6 | 碎裂帧数 |
| `ShatterFrameTime` | 0.1 | 每帧时长（共 0.6 秒） |

---

## 11. 音效清单

**全部走 `beast_*` 前缀，和第一关的大苹果 Boss 完全分开。**
文件放 `assets/audio/sfx/`，支持 `.ogg` / `.wav` / `.mp3`，**文件缺失时静默跳过**。

| 名字 | 触发时机 |
|---|---|
| `beast_roar` | 落地吼叫、半血跳天花板 |
| `beast_charge` | 技能 1 蓄力期间 |
| `beast_jump` | 走位起跳、技能 2 起跳 |
| `beast_lunge` | 飞扑起跳瞬间 |
| `beast_slam` | 落地 / 撞天花板 / 飞扑落地 / 冲锋撞击 |
| `beast_dodge` | 玩家在绿圈里蹲下成功躲开 |
| `beast_spit` | 吐恐怖苹果 |
| `beast_hurt` | 本体被打中 |
| `beast_die` | 血量归零那一瞬 |
| `beast_explode` | 碎裂演出开始 |

`beast_hurt` / `beast_die` 通过 `Enemy` 新增的 `[Export] HurtSfx` / `DeathSfx` 设置
（默认值仍是 `enemy_hurt`，所以老怪一个字都不用改）。

BGM 目前仍复用 `boss_fight`。想给这只 Boss 单独一首，
把 `BeastArena` 的 `BattleBgm` 改个名字，再把对应的 `assets/audio/bgm/<名字>.ogg` 放进去即可。

---

## 12. 踩过的坑（按坑的严重程度排）

| 现象 | 根因 | 修法 |
|---|---|---|
| Boss 只吃第一下伤害 | 父类 `_hitInvuln` 的递减写在 `Enemy._PhysicsProcess`，Boss 不调父类 | `_hitInvuln` 改 `protected`，Boss 自己每帧减 |
| 半血完全不触发 | 只查了个从来没人设置的 `_phaseTwoPending` | 新增 `CheckPhaseTwo()` 每帧查血量 |
| 吐出来的怪空引用崩在 `Enemy._Ready` | 代码 `new Sprite2D()` 没给 `Name`，入树变自动名 | 显式 `Name = "Sprite2D"` |
| 吐了 4 只却只找到 1 只 | 4 个节点同名，被 Godot 改成自动名 | `Name = $"spit_apple_{++序号}"` |
| 飞扑时蹲下也掉血 | 接触伤害绕过了绿圈规则 | 飞扑 / 坠落 / 走位期间关掉 `CheckContactDamage` |
| 弧线飞扑站着也打不中 | 几何求交，Boss 从头顶飞过去了 | 改成按横扫的 X 轨迹判定 |
| 死亡时疯狂闪屏 | 玩家死 → 重置 Boss → 下一帧又开战 → 镜头在 0.52 / 1.0 之间每帧来回切 | `Box.IsAlive` 守卫 + 重置条件改成 `!IsAlive` |
| 冲锋光圈追不上 Boss | 光圈钉死在撞击点 | `FollowTarget` 跟随 |
| 撞天花板时光圈画在地上 | `LandImpact` 里写死了 `FloorY` | 冲锋撞击改走 `ChargeImpact`，跟随模式下不再有这个问题 |
| 死亡后 Boss 重生隐形 | 死亡时把 `_sprite.Visible` 设成了 `false` | `EnterDormant()` 里恢复 `Visible = true` |

---

## 13. 调试方法

这只 Boss 的状态多、位移大，光看代码判断对错很难。实际用的流程是：

1. **`dotnet build`** —— 先保证 0 警告 0 错误；
2. **写一个临时 C# 探针** —— 继承 `Node2D`，`_Ready` 里 `Instantiate` 出 `underground.tscn` 当子节点，
   然后用 `await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame)` 一帧帧推进，
   脚本化地摆玩家位置、改 Boss 参数、打血量；
3. **打数值，不打日志** —— 每步打印 `StateName` / `GlobalPosition` / `Hp`，
   和预期区间对照（比如"飞扑拱高应该是 70"）；
4. **真实渲染截图** —— `GetViewport().GetTexture().GetImage().SavePng()`，
   再用 `ViewImage` 逐张核对，因为"坐标对了"不等于"看起来对"；
5. **删掉探针** —— 验完立刻删，不留在工程里。

> 探针文件建议放 `tools/`，命名 `*Probe.cs`，别进 git。

`BeastAppleBoss` 暴露了两个只读属性专供探针使用：`StateName`（当前状态名）和 `PhaseTwoDone`。

---

## 14. 已知遗留

| 项 | 说明 |
|---|---|
| `BeastExplosion.cs` | 当前未被调用，保留备用 |
| `beast_explode.png` | 同上 |
| `ChargeXSpeed` 与 `ChargeJumpVelocity` 的比例 | 跳跃初速度调高后，每段横移距离变短，一次冲锋要弹 3~4 段才到对面；如果嫌碎，把 `ChargeXSpeed` 一起提上去 |
| 战斗重置 | 玩家死亡时 `BeastArena.ResetFight()` 会把 Boss 满血送回倒挂状态，**已打掉的血量不保留** |
