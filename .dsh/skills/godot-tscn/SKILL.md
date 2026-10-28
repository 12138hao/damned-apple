---
name: godot-tscn
description: 安全编辑 Godot 4 场景/资源文件（.tscn/.tres）的规则：段落顺序、load_steps 同步、脚本 uid 从 .uid 抄、parent 路径语义、改完必须 headless 校验、大文件用精准替换。
whenToUse: 要往 firstry.tscn / house_inside.tscn / BossArena.tscn / hud.tscn 里加节点、改属性、接信号，或新建场景时。
---

# .tscn / .tres 编辑规则

场景文件是纯文本，可以手改，但结构错了 Godot 会静默丢节点。按下面顺序改，改完必验。

## 段落顺序（文件必须按这个序）

```
[gd_scene load_steps=N format=3 uid="uid://..."]
[ext_resource type="Script" uid="uid://..." path="res://Box.cs" id="1_abcde"]
[sub_resource type="..." id="..."]
[node name="X" type="Node2D" parent="."]     ← 可多个，顺序即树顺序
[connection signal="..." from="..." to="..." method="..."]   ← 必须在最后
```

- `load_steps` = ext_resource 数 + sub_resource 数 + 1。**加删资源必须同步这个数**，否则可能加载失败。
- `id` 形如 `1_abcde`，不允许重排或复用；新加的用一个没出现过的。
- `[connection]` 一定在文件末尾；插到中间会被当节点解析失败。

## 脚本引用带 uid

```ini
[ext_resource type="Script" uid="uid://xxxxxxxx" path="res://Box.cs" id="2_xyz"]
```

`uid` 从同目录的 `<脚本名>.cs.uid` 里抄（例如 `Box.cs.uid` 的内容就是 `uid://...`）。只写 `path` 不写 `uid` 能用，但 Godot 会打升级警告；两者都对不上时场景直接加载失败。

## parent 路径语义

- 根节点：没有 `parent`（或 `parent="."` 表示挂根下）。
- 深层：`parent="Player/Body"`，路径相对场景根。
- 节点名重复会变成 `@Node2D@123`，别制造重名。

## 改完必须校验

```powershell
& 'D:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --headless --quit
```

抓 `Parse Error` / `Failed to load` / `Invalid` / `Can't load` / `expected`。退出码 0 不等于没坏。

## 操作习惯

- `firstry.tscn` 有 53 KB、`Box.cs` 有 57 KB：**用精准替换（edit）改局部，不要整文件重写**。
- 本项目没有 git CLI（见 `workspace-hygiene`）：结构性改动前先 `Copy-Item x.tscn x.tscn.bak`，验过再删。
- 新增 `.png`/`.wav` 引用后，先跑一次 headless 让它生成 `.import`，再把新 `ext_resource` 写进场景；别手写 `.import`。
- 别手改 `uid://` 值；`@export` 属性的值要跟 C# 里的字段名一致（`[Export] public int Hp` → `Hp = 100`）。
