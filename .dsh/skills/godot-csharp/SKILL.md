---
name: godot-csharp
description: 本项目的 Godot 4.7.2 .NET/C# 构建与无头校验约定：本机 exe 路径、dotnet 版本、`dotnet build` + `--headless --quit` 的固定顺序、退出码不可靠、GL Compatibility 限制。
whenToUse: 改完任何 .cs 之后要确认还能编过；要跑游戏；要判断某个 Godot/C# 报错是不是真问题；或新建脚本、改脚本名时。
---

# Godot C# 工程约定（本机实测）

## 工具链（已核实，别猜）

| 用途 | 路径 |
|---|---|
| 需要 stdout/stderr | `D:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe` |
| 手动开编辑器 | 同目录 `..._mono_win64.exe`（不带 `_console`） |
| .NET SDK | PATH 上的 `dotnet`（10.0.300-preview 编 net8.0，正常，不用管那个 NETSDK1057 警告） |
| 备用非 mono 版 | `D:\Godot\_standard_4.7.2_backup\` —— **跑不了 C# 工程，不要用** |

脚本里一律走 `_console.exe`：不带 console 的版本在 PowerShell 里抓不到输出。

## 改完代码的固定校验顺序

```powershell
dotnet build MyGame.sln -v:m --nologo
& 'D:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --headless --quit
```

1. `dotnet build` 必须 0 error（实测 ~13s），产物落在 `.godot\mono\temp\bin\Debug\MyGame.dll`。
2. 再跑 headless（实测 ~7s，正常输出只有两行，其中一行是 `[Hud] 已绑定玩家 Hp=100/100`）。
3. `--headless --quit` **不保证重新编译 C#**，所以顺序不能反：先 build，再 headless，headless 只证明场景/资源能加载、脚本能挂上。

## 退出码不能当验收

脚本错误（空引用、方法签名不对、`_Ready` 抛异常）经常仍然返回 **exit 0**，只在 stderr 打印 `ERROR` / `SCRIPT ERROR` / `Failed to load` / `Cannot open`。判定成功 = 退出码 0 **且** 输出里没有这些关键字。

```powershell
$out = & $exe --path . --headless --quit 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $out -match 'SCRIPT ERROR|ERROR:|Failed to load|Parse Error') { $out }
```

## 常用变体

- 只验某个场景：`& $exe --path . --headless --quit firstry.tscn`
- 真跑起来看画面：`& $exe --path .`（主场景由 `project.godot` 的 `run/main_scene="uid://..."` 决定）
- 改完 `.tscn`/`.tres` 也跑同一条 headless 命令。

## 硬约束

- 渲染是 **GL Compatibility**（`renderer/rendering_method="gl_compatibility"`，`config/features=PackedStringArray("4.7","C#","GL Compatibility")`）：2D 光照阴影、部分后处理、CSG 高级特性不可用，提方案时别引入。
- `TargetFramework` 是 `net8.0`（android 才是 net9.0）。
- **编辑器开着时不要同时跑 headless**：两边抢 `.godot` 导入缓存，轻则重复导入重则报锁错误。要么先关编辑器，要么接受偶发怪报错并重跑一次。
- 不要删 `.godot/`（重新导入几分钟），不要用 `dotnet run`（Godot 工程没有可用入口）。

## 改脚本名/加脚本

每个 `.cs` 旁边有同名 `.uid`（内容是 `uid://...`）。改文件名必须同步改 `.uid` 的引用方，否则场景里的 `ext_resource` 断链，headless 会报 `Failed to load`。加脚本时优先复制现有节点的挂载方式，不要手写 `uid`。
