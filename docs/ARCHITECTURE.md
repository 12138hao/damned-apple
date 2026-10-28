# 架构说明 · 该死的苹果

> 本文说明这个横版 2D 游戏项目的代码组织方式、模块边界和关键设计取舍。
> 目标是让第一次读代码的人能快速判断：**这个游戏项目底层逻辑是怎样，怎么设计的**

---

## 1. 技术栈与规模

| 项 | 值 |
|---|---|
| 引擎 | Godot 4.7.2（**.NET / C#** 版） |
| 语言 / 目标框架 | C#，`net8.0` |
| 渲染后端 | GL Compatibility |
| 基准分辨率 | 800 × 450（窗口 3 倍放大，`viewport` 拉伸模式） |
| 代码规模 | 36 个 `.cs`，约 4,900 行（截至 2026-10-28） |
| 场景 | 8 个 `.tscn` |
| 测试 | 独立 xUnit 工程 + GitHub Actions CI |

项目没有使用任何现成的角色控制器、战斗框架或状态机库，移动、碰撞处理、战斗判定、Boss 行为全部自己设计而成。

---

## 2. 分层

```
┌──────────────────────────────────────────────────────────┐
│  表现层    SheetAnimator / Breath / SwingTrail /          │
│            WeaponAura / Clouds / DeathDrop / DeathExplosion│
├──────────────────────────────────────────────────────────┤
│  玩法层    Box（玩家）/ Enemy 及子类 / Bullet /           │
│            Pickup / Door / PoisonZone / CollapseFloor …   │
├──────────────────────────────────────────────────────────┤
│  场景层    firstry / house_inside / underground /         │
│            BossArena                                      │
├──────────────────────────────────────────────────────────┤
│  全局服务  GameState（跨场景状态）· Hud（UI）·            │
│            AudioManager（音频）                            │
├──────────────────────────────────────────────────────────┤
│  纯逻辑    GameRecords（不依赖 Godot，可单元测试）        │
└──────────────────────────────────────────────────────────┘
```

依赖方向自上而下：**表现层只读玩法层的数据，玩法层不关心表现细节，纯逻辑层不知道 Godot 存在。**

---

## 3. 全局服务（Autoload）

`project.godot` 注册了三个自动加载节点，随场景切换常驻：

| 名称 | 脚本 | 职责 |
|---|---|---|
| `GameState` | `GameState.cs` | 跨场景状态：待恢复血量、出生点、武器、无敌时间、载具、存档点、「聊过 / 捡过」记录 |
| `Hud` | `hud.tscn` + `Hud.cs` | 血条、武器槽等常驻 UI，作为 `CanvasLayer` 独立于场景 |
| `AudioManager` | `AudioManager.tscn` + `AudioManager.cs` | 音效池 + BGM 淡入淡出，按文件名查找资源 |

### 3.1 GameState 的跨场景契约

切场景最怕状态丢失或重复生效，这里用一组「**先存后取、取完即清**」的配对方法解决：

```
切换前：PrepareTravel(hp, spawn, weapons, slot, invuln)   → 写入 Pending*
切换后：TakeHp / TakeSpawn / TakeWeapons / TakeInvuln      → 读取并清空
```

- `Pending*` 字段用 `-1` / `null` 表示「本次不覆盖，用场景默认值」。
- `TakeSpawn()` 读完立即置 `null`，保证出生点只生效一次，防止二次触发把玩家弹回去。
- 武器数组在写入时 `Clone()`，避免旧场景销毁时把数组一起回收。

这套契约是**单向**的：场景只通过 `GameState.I` 读写，不互相引用，所以新增场景不需要改动旧场景。

### 3.2 Hud / AudioManager

- `Hud` 以 `CanvasLayer` 挂全局，场景切换时 UI 不重建。
- `AudioManager` 是单例（`AudioManager.I`），调用方只管 `Play("jump")`，不关心资源路径；文件缺失时静默跳过——所以可以先搭系统、后补素材，不会因为缺音频报错。

---

## 4. 实体体系

```
CharacterBody2D
├── Box                 玩家控制器（按模块拆成 7 个 partial 文件）
└── Enemy               敌人基类（受击 / 击退 / 死亡 / 巡逻）
    ├── AppleEnemy      追踪型：帧段驱动行为
    ├── BossEnemy       双阶段 Boss：行为状态机 + 技能
    ├── SlimeEnemy
    ├── Enemy2 / Enemy3 变体（十几行，复用基类行为）
    └── Naiwa           轻量单位

Area2D
├── Bullet / Pickup / Door / PoisonZone / RevivePoint / KillZone / VehiclePickup
```

**分层意图**：`Enemy` 承担共性（血量、受击闪白、击退、死亡掉落），子类只实现「怎么动、怎么打」。新增敌人通常只需要写一个 10~50 行的子类。

`Box` 曾经是一个上千行的巨类。现在按职责拆成 7 个 `partial` 文件：`Box.cs`（主文件与装配）、`Box.Move.cs`（移动/跳跃/单向平台）、`Box.Combat.cs`（攻击/蓄力/连段）、`Box.Weapon.cs`（武器槽）、`Box.Vehicle.cs`（载具）、`Box.Life.cs`（血量/受击/死亡）、`Box.Visual.cs`（贴图与表现）。单个文件都在 300 行以内，改一个子系统不用再在千行文件里翻。

### 4.1 动画帧段约定

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

---

## 5. 场景切换与重生

```
Door / FallTrigger ──► GameState.PrepareTravel(...) ──► GetTree().ChangeSceneToFile(...)
                                                              │
新场景 _Ready() ──► GameState.TakeHp / TakeSpawn / TakeWeapons ─┘
```

- **存档点**：`RevivePoint` 记录 `RevivePointKey`、`ReviveScenePath`、`RevivePosition`，死亡时若不在存档场景，先切场景再复活。
- **掉落死亡**：`FallTrigger` / `KillZone` 直接触发重生流程，与存档点复用同一套逻辑。

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

代码里统一用 `Input.IsActionPressed` / `IsActionJustPressed` 判断，好处是改键位只动 `project.godot`，玩法脚本一行不用改；以后接手柄或做按键重映射，也不用碰逻辑。

### 6.2 战斗模块

| 模块 | 说明 |
|---|---|
| 近战 | 鼠标方向 360° 判定，按住蓄力、松手挥出，蓄满转为突刺 |
| 连段 | 空手连点累积连段，打满后一次重击 |
| 远程 | 载具状态下水平发射 `Bullet` |
| 武器数据 | `WeaponData : Resource`，伤害 / 冷却 / 贴图等数据与逻辑分离，4 个槽位切换 |
| 环境伤害 | `PoisonZone` 持续掉血，`CollapseFloor` 定时崩塌 |
| 无敌帧 | `Box` 内部管理，跨场景复活时通过 `PendingInvuln` 传递 |

**单向平台**没有用 `one_way_collision`，而是**单独占用第 4 碰撞层**：按下方向键时临时从碰撞遮罩里摘掉该层，持续时间结束后恢复。这样处理「站在单向平台上、按 S 下穿」时不会和电梯/斜坡类平台互相干扰。

### 6.3 每帧更新顺序

逻辑分成两条时间线：

- **`_PhysicsProcess`（固定步长，默认 60Hz）**：玩家 `Box`、敌人基类 `Enemy` 与 `BossEnemy`、`Bullet`、`Naiwa`。移动、碰撞、伤害判定都在这里，步长固定，手感不会随帧率抖动。
- **`_Process`（渲染帧）**：`SheetAnimator`、`Clouds`、`WeaponAura`，以及 `Door` / `Pickup` / `PoisonZone` / `RevivePoint` 这类触发物件。只做表现与检测，不参与物理推进。

同一帧内 Godot 按场景树顺序依次调用，所以节点在场景里的先后会决定「谁先读到谁的位置」。本项目把玩家和敌人的移动都放在物理帧，等于每帧先算位置、再做碰撞与判定，避免「先判定后移动」造成的一次性穿透。

物理参数沿用 Godot 默认（60 ticks/s），没有改写 `physics_ticks_per_second`。

---

## 7. Boss 战

`BossArena : Area2D` 是战场控制器，`BossEnemy : Enemy` 是 Boss 本体，职责分离：

- **BossArena**：玩家进入边界后封锁出口、拉近镜头、显示血条、播放战败剧情、战后地板塌陷把玩家送出。
- **BossEnemy**：两段血量（50% 换形态、解锁吐毒）、技能循环（跳跃砸地、吐毒）、受击与死亡表现。

把「战场规则」和「Boss 行为」拆开的好处是：换地图不用改 Boss，换 Boss 不用改战场。

---

## 8. 音频

`AudioManager` 的设计目标是「**素材没到齐也能跑**」：

- 8 路音效播放器轮换，避免连打时抢同一路。
- `Play(string name)` 按名字找 `assets/audio/sfx/<name>.*`，找不到就什么都不做。
- BGM 独立通道，切换场景时淡入淡出。
- 对话打字音的段落用 `blip` 合成音，音高随机抖动。

因此素材缺失、增删音频文件都不需要改代码。

---

## 9. 纯逻辑与可测试性

Godot 的 `Node` 不能脱离引擎实例化（`new` 会访问非法内存），所以凡是**需要单元测试的状态逻辑**都被抽到不依赖 Godot 的纯 C# 类里：

```
GameRecords.cs          纯 C#：聊过 / 捡过的记录集合（HashSet 语义）
tests/GameStateTests.cs xUnit：6 条测试，覆盖标记、幂等、空值、重置
tests/tests.csproj      独立测试工程，只 Include 纯逻辑文件，不引用 Godot 工程
.github/workflows/test.yml  每次 push 自动 dotnet test
```

这样做的两个收益：

1. **本地**：双击 `run-tests.cmd` 秒级验证，不用开游戏。
2. **线上**：CI 自动跑同一套测试，README 上有绿色徽章，是公开的工程证据。

> `GameState` 不自己维护集合，而是**组合** `GameRecords` 并转发方法调用，所以上面这些测试覆盖的就是运行时代码本身，不存在「测的是另一份实现」的问题。

---

## 10. 目录结构

```
my-game/
├── *.cs                    所有玩法脚本（Godot 项目惯例：脚本与场景同目录）
├── *.tscn                  场景
├── assets/                 贴图、音频
├── tools/                  素材生成脚本（纯 Python 标准库，手写 PNG 编码）
├── tests/                  xUnit 测试工程
├── docs/                   文档
├── run-tests.cmd           一键跑测试
└── project.godot           项目配置（含 autoload 注册）
```

---

## 11. 已知技术债

按修复优先级排列：

1. **`GameState` 直接依赖 `Godot` 类型**（`Vector2`、`WeaponData`）：导致它无法单元测试；可把位置等数据换成纯数据传参。
2. **无场景依赖注入**：节点通过路径查找互相获取引用，重命名节点容易断。

> 已解决：`Box` 巨类已按模块拆成 7 个 `partial` 文件；`GameState` 已改为组合 `GameRecords`，
> 记录逻辑有了真实的单元测试覆盖。

---

## 12. 扩展指引

| 想做什么 | 改哪里 |
|---|---|
| 加一种新敌人 | 继承 `Enemy`，实现移动/攻击；场景里挂脚本即可 |
| 加一个新场景 | 新建 `.tscn`，通过 `Door` 传 `scenePath`；跨场景状态用 `GameState.PrepareTravel` |
| 加音效 | 把文件丢进 `assets/audio/sfx/`，代码里 `AudioManager.I.Play("名字")` |
| 加武器 | 新建 `WeaponData` 资源，填进槽位；逻辑复用 `Box` 的攻击流程 |
| 加可测逻辑 | 写成不依赖 Godot 的纯 C# 类，在 `tests/` 加 `[Fact]` |

---

## 13. 设计取舍记录

- **不用现成角色控制器**：为了完全掌控手感（跳跃缓冲、土狼时间、蓄力节奏），代价是 `Box` 变大。
- **GL Compatibility 渲染**：兼容性优先、放弃部分 2D 光照与后处理。
- **状态全放 Autoload**：实现简单、跨场景天然可用；代价是全局可变状态，测试困难，也是第 9 节分离纯逻辑的动机。
- **素材程序化生成**：`tools/*.py` 用标准库手写 PNG 编码，零第三方依赖，可复现；代价是需要自己维护生成脚本。
- **数值分层：默认值在脚本，差异在场景**：所有可调参数（速度、跳跃力、血量、伤害、冷却）都用 `[Export]` 暴露，脚本里给一份默认值，具体关卡在场景实例上覆盖。**不做全局数值表**——每张地图的手感本来就该不同，强行统一会限制关卡设计；某张图要跳得更高，改那张图的节点属性即可，不需要新脚本。代价是数值分散在各场景，需要靠检查器而不是配置文件来统一管理，在 8 张图的规模下这个成本可以忽略。

---

*本文档随代码演进，最近更新：2026-10-28*
