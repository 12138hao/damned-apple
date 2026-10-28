---
name: workspace-hygiene
description: 本项目的工作区卫生约定：195 个 .bak 的成因与安全清理方法、.gitignore 现状、git.exe 不在 PATH 时的替代做法、哪些目录不该手删。
whenToUse: 要清理备份文件、要「先把改动存起来」、要用 git/版本管理、或发现仓库根目录又多了 .bak 时。
---

# 工作区卫生

## 现状（已核实）

- 仓库根有 **195 个 `*.bak*`，共 3.7 MB**，是「改 C# / 场景前手动备份」留下的。
- `.gitignore` 第 16–19 行已经忽略 `*.bak` 和 `*.bak[0-9]*`，所以它们**不在仓库里**，纯属本地磁盘垃圾。
- 仓库是 `https://github.com/12138hao/damned-apple.git`，分支 `main`；但**本机没有 `git.exe`**（PATH 和 `D:\` 三层内都没扫到），所以现在只能靠 .bak 做版本管理。
- `.dsh/skills/` 没有被忽略，可以正常提交。

## 清理（先列，再删，最后确认）

```powershell
$root = 'D:\h\my-game'
$cands = Get-ChildItem $root -File -Filter '*.bak*' |
  Where-Object { Test-Path (Join-Path $root ($_.Name -replace '\.bak\d*$', '')) }
$cands | Measure-Object Length -Sum | Select-Object Count, @{n='MB';e={[math]::Round($_.Sum/1MB,1)}}
$cands | Select-Object -First 20 Name, Length, LastWriteTime
```

只删「基文件仍然存在」的那些（上面已经过滤）。确认清单后再执行，先用 `-WhatIf` 空跑一遍：

```powershell
$cands | Remove-Item -WhatIf
$cands | Remove-Item
```

反过来永远不要删：基文件已不存在的 `.bak`（可能是唯一副本）、`tools/slam_原始.mp3.bak` 这类非代码备份、`.godot/`、`build/`、`dist/`（后者是导出产物，要删就整目录重导）。

## 更好的做法

1. 装 Git（`git --version` 能用之后）：丢掉的 `.bak` 由提交历史替代。
2. 真要临时备份，统一放一个 `.tmp-backups/` 目录并加进 `.gitignore`，别散在仓库根。
3. 一次只备份「马上要动的那个文件」，改完验完立刻删，别攒。

## 与其它 skill 的关系

- 改 `.tscn` 前的那次备份见 `godot-tscn`；改完用 `godot-csharp` 的 headless 命令验证，通过了就可以删掉这次备份。
