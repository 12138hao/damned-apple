# 架构说明 · 该死的苹果

> 本文说明这个横版 2D 游戏项目的代码组织方式、模块边界和关键设计取舍。
> 目标是让第一次读代码的人能快速判断：**这个游戏底层逻辑是怎样设计的，为什么这么拆**

---

## 1. 技术栈与规模

| 项 | 值 |
|---|---|
| 引擎 | Godot 4.7.2（**.NET / C#** 版） |
| 语言 / 目标框架 | C#，`net8.0` |
| 渲染后端 | GL Compatibility |
| 基准分辨率 | 800 × 450（窗口 3 倍放大，`viewport` 拉伸模式） |
| 代码规模 | **102 个 `.cs`，约 20,700 行**（截至 2026-11-10） |
| 场景 | 22 个 `.tscn`（7 张关卡地图 + 15 个预制体与 UI） |
| 测试 | 独立 xUnit 工程 + GitHub Actions CI；另有 111 个无头回归探针 |

项目没有使用任何现成的角色控制器、战斗框架或状态机库，移动、碰撞处理、战斗判定、Boss 行为全部自己设计而成。

---

## 2. 分层

```
┌───────────────────────────────────────────────────────────────┐
│  表现层    SheetAnimator / Breath / SwingTrail / WeaponAura / │
│            Clouds / CloudLayer / DeathDrop / DeathExplosion /  │
│            EdenBackdrop / CliffBackdrop / GrasslandBackdrop    │
├───────────────────────────────────────────────────────────────┤
│  玩法层    Box（玩家）/ Enemy 及子类 / 各 Boss / Bullet /      │
│            Pickup / Door / PoisonZone / MudPit / Rope·Vine /   │
│            CollapseFloor / MagicCircle / FallingApple …        │
├───────────────────────────────────────────────────────────────┤
│  战场层    BossArena / BeastArena / FairyArena / SnakeArena     │
├───────────────────────────────────────────────────────────────┤
│  场景层    MainMenu / firstry / house_inside / underground /   │
│            rockhouseinside / jungle / jungleinside / eden      │
├───────────────────────────────────────────────────────────────┤
│  全局服务  GameState（跨场景状态）· SaveSystem（存档）·        │
│            Hud（UI）· AudioManager（音频）· CombatBalance（数值）│
├───────────────────────────────────────────────────────────────┤
│  纯逻辑    GameRecords（不依赖 Godot，可单元测试）             │
└───────────────────────────────────────────────────────────────┘
```

依赖方向自上而下：**表现层只读玩法层的数据，玩法层不关心表现细节，纯逻辑层不知道 Godot 存在。**

---

## 3. 全局服务

### 3.1 Autoload

`project.godot` 注册了三个自动加载节点，随场景切换常驻：

| 名称 | 脚本 | 职责 |
|---|---|---|
| `GameState` | `GameState.cs` | 跨场景状态：待恢复血量、出生点、武器、无敌时间、载具、存档点、「聊过 / 捡过」记录、通关标记、对话进行中标记 |
| `Hud` | `hud.tscn` + `Hud.cs` | 血条、武器槽等常驻 UI，作为 `CanvasLayer` 独立于场景 |
| `AudioManager` | `AudioManager.tscn` + `AudioManager.cs` | 音效池 + BGM 淡入淡出，按文件名查找资源 |

`GameState` 的跨场景契约、`Hud` 与 `AudioManager` 的细节见下。

### 3.2 GameState 的跨场景契约

切场景最怕状态丢失或重复生效，这里用一组「**先存后取、取完即清**」的配对方法解决：

```
切换前：PrepareTravel(hp, spawn, weapons, slot, invuln)   → 写入 Pending*
切换后：TakeHp / TakeSpawn / TakeWeapons / TakeInvuln      → 读取并清空
```

- `Pending*` 字段用 `-1` / `null` 表示「本次不覆盖，用场景默认值」。
- `TakeSpawn()` 读完立即置 `null`，保证出生点只生效一次，防止二次触发把玩家弹回去。
- 武器数组在写入时 `Clone()`，避免旧场景销毁时把数组一起回收。

这套契约是**单向**的：场景只通过 `GameState.I` 读写，不互相引用，所以新增场景不需要改动旧场景。

### 3.3 Hud / AudioManager / SaveSystem

- `Hud` 以 `CanvasLayer` 挂全局，场景切换时 UI 不重建。主菜单会把它整个隐藏。
- `AudioManager` 是单例（`AudioManager.I`），调用方只管 `PlaySfx("jump")`，不关心资源路径；文件缺失时静默跳过——所以可以先搭系统、后补素材，不会因为缺音频报错。
- `SaveSystem` 是静态类，把 `GameState` 序列化成 `user://save.json`（Windows 上是 `%APPDATA%\Godot\app_userdata\我的第一个游戏\save.json`），带版本号，「聊过 / 捡过」的记录以字符串数组存。

---

## 4. 实体体系

```
CharacterBody2D
├── Box                 玩家控制器（按模块拆成 10 个 partial 文件）
└── Enemy               敌人基类（受击 / 击退 / 死亡 / 巡逻 / 接触伤害）
    ├── AppleEnemy      第一关：追踪型
    ├── SlimeEnemy      第一关：跳跃型
    ├── Enemy2 / Enemy3 第一关：变体（十几行，复用基类行为）
    ├── HorrorAppleEnemy   第二关：巡逻
    ├── PirateApple        第三关：两段连斩
    ├── UndeadPirateApple  第三关：骷髅手投技
    ├── BossEnemy          第一关 Boss
    ├── BeastAppleBoss     第二关 Boss
    ├── AppleFairy         第三关 Boss
    ├── SnakeBoss          第四关 Boss
    └── SnakeMinion        第四关：召唤物

Area2D
├── Bullet / ChalkProjectile / FallingApple / PoisonBlob
├── Pickup / Door / PoisonZone / RevivePoint / KillZone / VehiclePickup
└── MudPit / Vine / MagicCircle / SpitMarker
```

**分层意图**：`Enemy` 承担共性（血量、受击闪红、击退、死亡掉落、接触伤害），子类只实现「怎么动、怎么打」。新增敌人通常只需要写一个 10~50 行的子类。

### 4.1 Box 的模块拆分

`Box` 曾经是一个上千行的巨类。现在按职责拆成 10 个 `partial` 文件：

| 文件 | 职责 |
|---|---|
| `Box.cs` | 主文件与装配、`[Export]` 参数、每帧更新顺序 |
| `Box.Move.cs` | 移动 / 跳跃 / 单向平台 / 攀爬 / 锁定镜头 |
| `Box.Combat.cs` | 攻击 / 蓄力 / 连段 / 命中判定 |
| `Box.Weapon.cs` | 武器槽、切换、伤害取值 |
| `Box.Ranged.cs` | 远程武器与冷却 |
| `Box.Grab.cs` | 被抓与挣脱 |
| `Box.Crouch.cs` | 蹲下与下穿 |
| `Box.Vehicle.cs` | 载具上 / 下车 |
| `Box.Life.cs` | 血量 / 受击 / 死亡 / 复活 |
| `Box.Visual.cs` | 贴图、动画帧、朝向 |

单个文件都在 300~460 行之间，改一个子系统不用再在千行文件里翻。

> **诚实说明**：`partial` 只是**物理拆分**，不是职责解耦——它们共享同一个实例的全部私有字段。真正的解耦应该是抽出 `Locomotion`、`WeaponController` 这类组件。详见第 12 节。

### 4.2 动画帧段约定

角色动画统一用 `Sprite2D` 的水平切片（`Hframes`）+ `Frame` 索引，不依赖 `AnimationPlayer`：一张 sprite sheet 按固定帧段划分动作，代码只负责切帧。

以苹果怪（`AppleEnemy`）为例，帧段全部用 `[Export]` 暴露，换贴图不用改代码：

| 帧段 | 用途 |
|---|---|
| `WalkRightStart` + 0~3（0–3） | 向右走 |
| `WalkLeftStart` + 0~3（4–7） | 向左走 |
| `IdleStart` + 0~2（8–10） | 待机 / 呼吸 |
| `DeathStart` ~ `DeathEnd`（11–15） | 死亡碎裂，播到末帧停住、不循环 |

玩家（`Box`）的基础 sheet 是 16 帧：帧 8 = 平躺、帧 9 = 天使形态，帧 16 / 18 = 朝左 / 朝右挥拳。切到载具贴图时 `Hframes` 会变成 8，代码在换表前会把帧号拉回合法范围，避免越界。

需要「循环播某一段」的表现（呼吸、水面波纹）用 `SheetAnimator`：挂到 `Sprite2D` 上，设 `StartFrame` / `FrameCount` / `Interval`，可选 `PingPong` 往返与 `Paused` 暂停。

### 4.3 黑蛇尾巴：帧序列是烘焙出来的

`SnakeBoss` 的甩尾只用到一张尾巴贴图，但动作需要「抬起来 → 横扫 → 收回」。

做法不是运行时拉伸，而是**离线烘焙**：`tools/_art/snake/build_tail_sheet.py` 把原图逐列做竖直位移，生成 12 帧（蓄力 0–4、横扫 5–8、回收 9–11），长度和抬起角度都写进像素里，拼成 `6720×94` 的长条。运行时只做两件事：切 `Frame`、左扫时 `FlipH`。缩放恒为 1，所以像素不会被拉糊。

代价是改动作必须重跑脚本；收益是动作节奏（0.45 秒完成一次甩击）完全可控、不会因为插值抖动。

### 4.4 判定贴合美术

两类「看得见打不到」的问题，解法不同：

| 对象 | 做法 |
|---|---|
| 玩家挥刀 | 每帧把刀身当作线段（握把 → 刀尖），量敌人中心到线段距离，小于半径才算命中 |
| 小怪受击 | `_Ready` 里预计算每套动画**每一帧的内容矩形**，换帧时同步 `RectangleShape2D` 的尺寸和位置 |

小怪那条尤其容易踩坑：节点自身 `Scale = 0.55`，而 `CollisionShape2D` 是它的子节点，物理形状会被父节点**再缩一次**。所以形状必须填**纹理像素**（110×117），缩放交给父节点——填「世界尺寸」会让判定只剩一半。

---

## 5. 场景切换与重生

```
Door / FallTrigger ──► GameState.PrepareTravel(...) ──► GetTree().ChangeSceneToFile(...)
                                                              │
新场景 _Ready() ──► GameState.TakeHp / TakeSpawn / TakeWeapons ─┘
```

- **存档点**：`RevivePoint`（可见，按 `E` 存，存完消失）和 `SpawnSavePoint`（隐形，进场景自动存）**两套机制不能混用**——同一个场景只用一种，否则后者每次进场景都会把前者的「已消失」状态撤销。
- **复活**：`RevivePoint` 记录 `RevivePointKey`、`ReviveScenePath`、`RevivePosition`，死亡时若不在存档场景，先切场景再复活。
- **掉落死亡**：`FallTrigger` / `KillZone` 直接触发重生流程，与存档点复用同一套逻辑。`FallTrigger` 还必须把 `CameraFixedY` 一起改到新位置，否则传送后镜头还在看旧高度。
- **过场演出**：`WellExit` 是纯演出脚本（升出井口 → 跨过前沿 → 落到地面），靠 `CallDeferred` 延后一帧启动，避免和其它节点的 `_Ready` 抢执行顺序。它踩过两个坑：井口节点的 `_Ready` 早于 `box` 时相机还是 null；以及直接写位置会被 `box` 自己的出生点覆盖。

---

## 6. 操作与战斗

### 6.1 输入映射

所有操作都走 `project.godot` 里 `[input]` 定义的自定义动作，代码只判断动作名，不直接读物理按键：

| 动作 | 用途 |
|---|---|
| `move_left` / `move_right` | 水平移动 |
| `jump` | 跳跃（按住跳得更高） |
| `attack` | 攻击（鼠标方向 360°） |
| `slot1` ~ `slot4` | 切换武器槽 |
| `look_up` / `look_down` | 上瞄 / 下蹲（`look_down` 兼做单向平台下穿） |
| `interact` | 交互：对话、存档、拾取 |
| `vehicle` | 上 / 下车 |

好处是改键位只动 `project.godot`，玩法脚本一行不用改；以后接手柄或做按键重映射，也不用碰逻辑。

### 6.2 战斗模块

| 模块 | 说明 |
|---|---|
| 近战 | 鼠标方向 360° 判定，按住蓄力、松手挥出，蓄满转为突刺 |
| 连段 | 空手连点累积连段，打满后一次重击 |
| 远程 | 粉笔投射物：每帧形状查询，**取离粉笔最近的敌人**结算，不依赖「谁先被物理引擎报告」 |
| 武器数据 | `WeaponData : Resource`，伤害 / 冷却 / 贴图与逻辑分离，4 个槽位切换 |
| 环境伤害 | `PoisonZone` / `PoisonBlob` 持续掉血，`CollapseFloor` 定时崩塌 |
| 无敌帧 | `Box` 内部管理，跨场景复活时通过 `PendingInvuln` 传递 |

**单向平台**没有用 `one_way_collision`，而是**单独占用第 4 碰撞层**：按下方向键时临时从碰撞遮罩里摘掉该层，持续时间结束后恢复。这样处理「站在单向平台上、按 S 下穿」时不会和电梯/斜坡类平台互相干扰。

### 6.3 每帧更新顺序

`Box._PhysicsProcess` 的顺序是硬约定，不能随便调换：

```
蹲下 → 下穿 → 攀爬 → 重力 → 镜头偏移 → 横向移动 → 跳跃
   → MoveAndSlide → 锁定镜头 → 切武器 → 瞄准 → 朝向 → 攻击输入 → 贴图
```

理由：朝向要先于动画、攻击判定要先于贴图切换，否则会出现「朝右却播左走帧」。

逻辑分成两条时间线：

- **`_PhysicsProcess`（固定步长，60Hz）**：玩家、敌人、Boss、子弹。移动、碰撞、伤害判定都在这里，步长固定，手感不随帧率抖动。
- **`_Process`（渲染帧）**：`SheetAnimator`、云层、`WeaponAura`，以及 `Door` / `Pickup` / `PoisonZone` / `RevivePoint` 这类触发物件。只做表现与检测，不参与物理推进。

**注意**：子类重写 `_PhysicsProcess` 时如果忘了调 `base`，基类里的逻辑会整段失效（受击闪红就踩过这个坑，现在抽成 `TickHitFeedback(dt)` 由子类显式调用）。

---

## 7. Boss 战与战场

四个 Boss 共用同一套契约，`Arena : Area2D` 管战场规则，Boss 本体管行为：

| 战场 | Boss | 特点 |
|---|---|---|
| `BossArena` | `BossEnemy`（大苹果） | 双阶段、战后地板塌陷送出场 |
| `BeastArena` | `BeastAppleBoss`（野兽苹果） | 扑击预警带、落地震波 |
| `FairyArena` | `AppleFairy`（苹果仙子） | 战斗时镜头拉远到 1200px 视野、分身同步出手 |
| `SnakeArena` | `SnakeBoss`（黑蛇） | 甩尾危险带、震落苹果三波、召唤、半血狂暴 |

四个战场的公共职责完全一致：

```
OnBodyEntered  → LockCamera() → BuildWalls() → ShowBar() → Boss.BeginBattle()
玩家死亡        → ResetFight()  ← 拆墙、还镜头、删血条、Boss 回满休眠、清空场上残留
Boss 死亡        → OnBossDefeated() ← 拆墙、还镜头、删血条、恢复场景曲、Monitoring = false
```

**把「战场规则」和「Boss 行为」拆开的好处**：换地图不用改 Boss，换 Boss 不用改战场。

### 7.1 三个必踩的坑

1. **镜头要完整还原**：战斗中把 `_cam.GlobalPosition` 直接怼到场地中心，会让相机的**本地 X** 变成非 0；而 `Box.Move.LockCameraHeight` 每帧只重写 Y，那个 X 会永久留下——打完 Boss 镜头一直偏。`UnlockCamera` 必须 `_cam.Position = Vector2.Zero` + `ResetSmoothing()`。
2. **异步技能要能被作废**：吐毒、召唤、苹果三波都是 `async void` + `await`，玩家中途死亡时改 `State` 是停不住它们的，而且最后一句 `BackToIdle()` 会把 `Dormant` 又改回 `Idle`。现在用 `_fightGen` 代次：技能开头记一次代数，每个 `await` 后面核对，作废就直接退出。
3. **判定框不能凭感觉填**：Boss 和小怪的碰撞体要么按美术实际尺寸算，要么按帧的内容矩形同步，否则就会出现「打到建模不掉血」。改动后一律用探针脚本实测确认。

---

## 8. 音频

`AudioManager` 的设计目标是「**素材没到齐也能跑**」：

- 8 路音效播放器轮换，避免连打时抢同一路。
- `PlaySfx(string name)` 按名字找 `assets/audio/sfx/<name>.*`，找不到就什么都不做。
- BGM 独立通道，`PlayBgm` 淡入淡出；`PlayBgmForScene(path)` 按 `SceneBgm` 映射表决定这一关放什么，找不到映射就沿用上一首。
- `CurrentBgm` 暴露当前曲名，剧情逻辑（比如结局时不要覆盖结尾曲）可以据此判断。
- 战斗曲由各 Arena 在开战时切、`OnBossDefeated` 时切回场景曲。

**音效名全部是可配字段**：黑蛇一个节点上有 9 个音效名字段（`SfxSpit` / `SfxTailWind` / `SfxAppleLand` …），加上继承来的 `HurtSfx` / `DeathSfx`；改音效只改检查器，不碰代码。

同类音效还有节流：`FallingApple` 用**所有苹果共用的静态时间戳**限制落地音最短间隔（默认 0.12 秒），避免 21 颗苹果同时砸地叠成噪音。

---

## 9. 数值配置：两类数值，两套机制

这是项目里改动最频繁、也最容易出错的部分，最后收敛成一条判据：

| 类型 | 例子 | 放在哪 | 为什么 |
|---|---|---|---|
| **规则数值**（全游戏必须一致） | 武器伤害、空手拳伤害 | 资源文件：`assets/weapons/*.tres`、`assets/balance/combat.tres` | 玩家在第一章和第四章砍同样的怪，伤害就该一样。改一处，全地图生效 |
| **手感数值**（按图调） | 移动速度、跳跃力、相机高度、Boss 招式参数 | `[Export]`，在场景实例上覆盖 | 每张地图的地形不同，跳跃高度本来就该不同 |

**演进过程值得记一笔**：最初所有数值都是 `Box` 上的 `[Export]`。但 `Box` 在每个场景里各有一份，于是「在 A 图的检查器里把武器伤害改成 50」只会写进 A 图的 `.tscn`，B 图完全不知道——这不是 bug，是设计错误。现在规则数值走资源，一处改全局生效。

`CombatBalance` 是个 `Resource` 单例（`CombatBalance.I`，按需 `GD.Load`），目前只放空手拳的伤害；武器则各自一份 `WeaponData`。

---

## 10. 纯逻辑、可测试性与无头回归

Godot 的 `Node` 不能脱离引擎实例化（`new` 会访问非法内存），所以凡是**需要单元测试的状态逻辑**都被抽到不依赖 Godot 的纯 C# 类里：

```
GameRecords.cs              纯 C#：聊过 / 捡过的记录集合（HashSet 语义）
tests/GameStateTests.cs     xUnit：覆盖标记、幂等、空值、重置
tests/tests.csproj          独立测试工程，只 Include 纯逻辑文件，不引用 Godot 工程
.github/workflows/test.yml  每次 push 自动 dotnet test
run-tests.cmd               本地一键跑
```

但单测只能覆盖纯逻辑，**「游戏能不能跑」这件事单测测不到**。所以另有一套无头探针：

```bash
Godot_v4.7.2-stable_mono_win64_console.exe --path . -s tools/_sd/probe_xxx.gd
```

`tools/_sd/` 下有 111 个 GDScript 脚本，直接用 `SceneTree` 驱动引擎跑真实场景，打印关键数字后退出。覆盖的例子：

| 探针 | 验什么 |
|---|---|
| `probe_hit_minion*.gd` | 小怪受击是否真的扣血、闪红 modulate 是否变化 |
| `probe_death_reset.gd` | 玩家死后空气墙 / 镜头 zoom / 血条 / Boss 状态是否全部回滚 |
| `probe_cam_restore.gd` | 打完 Boss 相机本地位置是否归零 |
| `probe_cam_clamp.gd` | 每张图的「半屏高 vs 镜头基准」是否会撞成负数区间 |
| `probe_ending.gd` | 对话 → 黑屏 → 字幕 → 回主菜单 → 通关标记落盘 |
| `probe_exports.gd` | Boss 的 55 个 `[Export]` 是否都真的暴露到检查器 |
| `shot_*.gd` | 定点抓帧，用来肉眼检查构图和特效 |

**收益**：改完不用打开编辑器手动跑一遍，几十秒就能知道「有没有改坏」，而且失败时给的是数字不是「感觉不对」。

---

## 11. 结局与通关状态

`EndingSequence` 挂在第四关根节点上，只做一件事：监听奶蛙的 `TalkFinished` 信号。

```
Npc 说完最后一句 → EmitSignal(TalkFinished)
  → 黑幕 alpha 0→1（FadeOutTime 2.5s）
  → 淡入结尾曲
  → Label 从屏幕下方滚到 -textH（ScrollSpeed）
  → GameState.Cleared = true → SaveSystem.Save() → ChangeSceneToFile(MainMenu)
```

- **只有把台词说完才触发**，中途走开被打断不发信号。
- 字幕高度自己按行数和字宽估算（Godot 的 `Label.FitContent` 在 C# 绑定里不存在）。
- 黑屏之后按 `E` / `ESC` 跳过，但**曲子照常播**，进主菜单也继续，直到下一关的 BGM 顶掉它。
- `MainMenu` 读到 `Cleared` 时，把「继续游戏」改成弹窗「游戏已通关，继续游戏则开启新游戏」，确认后 `NewGame()` + `SaveSystem.Delete()`。

---

## 12. 已知技术债

按修复优先级排列：

1. **`Box` 仍是 God Object**：10 个 `partial` 只是按文件切分，字段和行为仍强耦合在一个实例上。真正的解法是抽 `Locomotion`（移动/跳跃/攀爬）、`WeaponController`（武器/蓄力/远程）、`Damageable`（血量/受击/无敌帧）三个组件，用组合代替继承与 partial。
2. **`GameState` 直接依赖 `Godot` 类型**（`Vector2`、`WeaponData`）：导致它无法单元测试；可把位置等数据换成纯数据传参。
3. **无场景依赖注入**：节点通过路径查找互相获取引用，重命名节点容易断。
4. **Boss 参数过多**：黑蛇一个节点上有 55 个 `[Export]`（已按 8 组分类），但缺预设机制，想同时保留「简单/困难」两套数值只能手抄。
5. **无头探针没有纳入 CI**：现在靠本地手动跑；`-s` 脚本需要一个能跑 GUI 子系统的环境，接 GitHub Actions 需要额外的 headless 配置。

> 已解决：`Box` 千行巨类已拆分；`GameState` 已改为组合 `GameRecords` 并获得真实单测覆盖；
> 武器伤害已从「各场景 `[Export]`」收敛到全局资源。

---

## 13. 扩展指引

| 想做什么 | 改哪里 |
|---|---|
| 加一种新敌人 | 继承 `Enemy`，实现移动/攻击；参照 `docs/如何做一个新怪物.md` |
| 加一个新场景 | 新建 `.tscn`，通过 `Door.TargetScene` 指定目标；跨场景状态用 `GameState.PrepareTravel` |
| 加一个 Boss | 复制一个 `*Arena.tscn`，Boss 继承 `Enemy` 并实现 `BeginBattle / ResetFight`，战场照抄四个 Arena 的公共骨架 |
| 加音效 | 把文件丢进 `assets/audio/sfx/`，字段里填名字即可，文件缺失会静默跳过 |
| 加武器 | 新建 `WeaponData` 资源，填进槽位；逻辑复用 `Box.Combat` 的攻击流程 |
| 调数值 | 规则数值改 `.tres`；手感数值在场景实例的检查器里覆盖 |
| 加可测逻辑 | 写成不依赖 Godot 的纯 C# 类，在 `tests/` 加 `[Fact]` |
| 加一段过场 | 参照 `WellExit`（位移演出）或 `EndingSequence`（黑幕 + 字幕） |

---

## 14. 设计取舍记录

- **不用现成角色控制器**：为了完全掌控手感（跳跃缓冲、蓄力节奏、被击退不转身），代价是 `Box` 变大。
- **GL Compatibility 渲染**：兼容性优先、放弃部分 2D 光照与后处理。
- **状态全放 Autoload**：实现简单、跨场景天然可用；代价是全局可变状态、测试困难，也是第 10 节分离纯逻辑的动机。
- **数值分两类存储**（第 9 节）：这是踩过坑之后才收敛的结论——「全用 `[Export]`」会让全游戏一致的数值各存一份，「全用资源」又会让每张图的手感调不动。
- **动画不用 `AnimationPlayer`**：帧段 + `Frame` 索引的可控性更高（要卡在第几帧就卡在第几帧），也方便脚本化生成；代价是没有可视化时间轴，调动作要改数字或重跑烘焙脚本。
- **素材程序化生成**：`tools/_art/*.py` 用标准库手写 PNG 编码，零第三方依赖、可复现；代价是需要自己维护生成脚本。
- **Boss 技能用 `async void` + `await`**：写起来直观（等 0.3 秒再继续），代价是必须自己实现「代次作废」，否则死亡后旧协程会继续跑。

---

*本文档随代码演进，最近更新：2026-11-10*
