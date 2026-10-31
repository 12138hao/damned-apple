# 该死的苹果

[![test](https://github.com/12138hao/damned-apple/actions/workflows/test.yml/badge.svg)](https://github.com/12138hao/damned-apple/actions/workflows/test.yml)

> 大二上独立开发的横版 2D 游戏 demo

一个用 **Godot 4 + C#** 从零手写的横版动作 demo。没有用任何现成的角色控制器或战斗框架，移动、平台、战斗、Boss、对话全部是自己一行行写出来的。

▶ **[演示视频（B 站）](https://b23.tv/L69BoxH)** — 第一关通关演示　·　📐 **[架构说明](docs/ARCHITECTURE.md)** — 代码怎么分层、怎么设计的　·　🍎 **[野兽苹果 Boss 架构](docs/野兽苹果Boss架构.md)** — 第二关 Boss 的状态机与设计取舍

## 直接下载游玩

不想装引擎的话，去 **[Releases](https://github.com/12138hao/damned-apple/releases)** 下载 `damned-apple-v0.1.0-win64.zip`：

1. 解压（**要整个文件夹，不能只拖出 exe**）
2. 双击 `该死的苹果.exe`

**不需要安装 Godot，也不需要装 .NET 运行时。**
Windows 可能弹出 SmartScreen 蓝色提示（exe 没有代码签名），点「更多信息 → 仍要运行」即可。

---

## 玩法与操作

| 按键 | 动作 |
|---|---|
| `A` / `D` | 左右移动 |
| `W` / `S` | 上瞄 / 下蹲（单向平台下穿） |
| `空格` | 跳跃（按住跳得更高） |
| `鼠标左键` | 攻击（360° 跟随鼠标方向） |
| `1` `2` `3` `4` | 切换武器槽 |
| `E` | 对话 / 存档 / 拾取 |
| `R` | 上 / 下车 |

### 战斗

- **空手**：连点打连击，打满连段后一拳伤害和击退翻倍
- **持武器**：按住左键蓄力，松开挥出；蓄得越久伤害和击退越高，蓄满会变成突刺
- **载具**：骑乘状态下左键水平发射子弹，移动速度提升
- **受击**：有无敌帧和击退，被击退期间不会掉头，避免"被推歪就转身"

### 场景与流程

- 主城 `firstry` + 屋内 `house_inside` 两个场景，靠门切换
- 存档点按 `E` 存档：记录复活点、回满血量、存档点消失
- 死亡后从存档点复活，跨场景死亡会先切回存档所在场景
- 毒区持续掉血，掉出地图会直接判定死亡

### Boss 战

- 踏入战场边界自动封锁出口、拉近镜头、显示 Boss 血条
- 两段血量阶段（掉到 50% 换形态，解锁吐毒），技能分跳跃砸地、吐毒
- 击败后 Boss 倒地哭泣、播完剧情，地板塌陷把玩家送出场

---

## 技术细节

| | |
|---|---|
| 引擎 | Godot 4.7.2（**.NET / C# 版本**） |
| 语言 | C#（`net8.0`） |
| 渲染 | GL Compatibility |
| 规模 | 42 个脚本 / 8 个场景 / 约 4,900 行 C# |

几个自己写得比较得意的地方：

- **单向平台**：第 4 碰撞层专门给单向平台用，按下键时临时从碰撞遮罩里摘掉，持续时间结束后恢复，比用 `one_way_collision` 处理楼梯可靠得多
- **音频系统**：`AudioManager` 作为 autoload 单例，8 路音效播放器轮换 + BGM 淡入淡出，音效按名字查找文件，**文件缺失时静默跳过**，所以可以先搭系统后补素材
- **对话打字机**：利用 `Label.VisibleCharacters` 逐字显示，每 N 个字触发一次音效，音高带随机抖动
- **状态管理**：`GameState` 统一处理跨场景的血量、武器、载具、已读对话、已拾取道具、存档点
- **按模块拆分的玩家控制器**：按移动 / 战斗 / 载具 / 生命 / 视觉切成 7 个 `partial` 文件，单个文件不再动辄上千行
- **可测试的纯逻辑 + CI**：状态记录抽成不依赖 Godot 的 `GameRecords`，配套 xUnit 测试与 GitHub Actions，每次 push 自动跑

详细的模块划分、跨场景状态契约、动画帧段约定和逐帧更新顺序，见 **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**。

第二关 Boss「野兽苹果」的状态机、技能判定和踩过的坑，见 **[docs/野兽苹果Boss架构.md](docs/野兽苹果Boss架构.md)**。

---

## 目录结构

```
.
├── Box.cs                 玩家控制器（模块拆分后的主文件）
├── Box.Move.cs            移动 / 跳跃 / 单向平台
├── Box.Combat.cs          攻击 / 蓄力 / 连段
├── Box.Weapon.cs          武器槽与切换
├── Box.Vehicle.cs         载具上/下车
├── Box.Life.cs            血量 / 受击 / 死亡
├── Box.Visual.cs          贴图与表现
├── Enemy.cs               敌人基类（受击 / 击退 / 死亡 / 巡逻）
├── AppleEnemy.cs          追踪型敌人：苹果怪
├── BossEnemy.cs           Boss：双血量阶段 + 行为状态机 + 技能
├── BossArena.cs           战场：封锁、镜头、血条、战后剧情
├── AudioManager.cs        音频单例
├── DialogueUI.cs          气泡对话框 + 打字机
├── GameState.cs           跨场景状态
├── GameRecords.cs         纯 C# 状态记录（可单元测试）
├── Hud.cs / hud.tscn      血条、武器槽
├── firstry.tscn           主城
├── house_inside.tscn      屋内
├── tests/                 xUnit 测试工程
└── tools/                 素材生成脚本（Python，纯标准库）
```

---

## 运行

1. 安装 **Godot 4.7.2 .NET 版**（普通版无法运行 C# 项目）
2. 用 Godot 打开本目录，运行 `firstry.tscn`

命令行也行：

```bash
Godot_v4.7.2-stable_mono_win64.exe --path .
```

---

## 音频

仓库包含完整的音效与背景音乐，克隆下来就能直接听到声音。

音频系统是自己写的（`AudioManager.cs`）：8 路音效播放器轮换，BGM 淡入淡出，**音效按名字查找文件，找不到时静默跳过**——所以增删素材完全不用改代码。

| 目录 | 内容 |
|---|---|
| `assets/audio/sfx/` | jump / hurt / attack / swing / charge / death / enemy_hurt / pickup / collapse / slam / boss_cry / vehicle_shoot / blip |
| `assets/audio/bgm/` | town / house / boss_fight |

对话打字音 `blip` 是用 `tools/gen_blip_soft.py` 按实测参数合成的（纯标准库，不需要任何依赖），想换音高或密度改脚本里的参数重跑即可。

---

## 素材来源与授权

| 素材 | 来源 | 说明 |
|---|---|---|
| 代码、场景、关卡 | 本项目自制 | 版权归作者 |
| 美术素材 | 本项目自制 | 版权归作者 |
| 对话打字音 `blip` | `tools/gen_blip_soft.py` 参数合成 | 本项目自制，不含任何第三方录音 |
| 音效、背景音乐 | Pixabay / GamerSounds 游戏音效工厂 | 见下方说明 |

**Pixabay** — 依据 Pixabay Content License，素材可免费使用、**无需署名**、可修改改编。该协议唯一硬性限制是不得把素材原样单独再分发。

**GamerSounds 游戏音效工厂** — 站点声明所有音效素材「完全免费」，面向独立游戏开发、Demo 制作、个人学习等场景提供。

> 以上音频素材仅作为本项目 demo 的组成部分使用，**请勿从本仓库单独提取后另作他用**。如需在自己项目中使用，请前往上述来源站点按其授权条款获取。

---

## 版权

本项目为个人学习作品，代码与美术素材版权归作者所有，**未经许可请勿商用或二次分发**。

---

*开发于 2026 年秋，大二上学期。*
