---
name: asset-gen
description: 本项目 tools/*.py 素材生成流水线约定：纯标准库手写 PNG（struct+zlib，无 PIL）、128px 网格、先出 preview_*.png 再替换正式图、不手改 .import、中文文件名保持 UTF-8。
whenToUse: 要新增/修改美术素材（角色表、建筑、UI、场景贴图）、调整像素尺寸、或看到 assets/ 与 tools/ 不一致时。
---

# 素材生成流水线

## 约定（别破坏）

- `tools/*.py` **只用 Python 标准库**：`math os random struct zlib sys`，PNG 是手写的（`chunk()` + `zlib.compress` + 自己拼 IHDR/IDAT/IEND）。没有 PIL、没有 numpy，也不许为了省事引入。
- 每个脚本开头的 docstring 就是契约：输出路径、尺寸、是否可无缝平铺都写在里面。改尺寸必须同步改 docstring。
- 像素风格：**最近邻放大，不做插值**（`gen_breath_sheet.py` 里明确写了「不做插值，保持像素锐利」）。
- 网格：建筑/角色贴图按 **128px** 一档（文件名如 `楼1_米色平顶3层_透明128.png`、`08_粉色凸窗2层_128.png`）；小物件 32/48/64 不等，看脚本里的 `W = H = ...`。

## 标准流程

1. 改参数或写新脚本（复制最接近的现有脚本，别从零起）。
2. `py -3 tools/<脚本>.py`（个别带参数，如 `gen_breath_sheet.py <原图> [输出名]`，看 docstring）。
   - **注意**：这台机器上 `python` / `python3` 指向 Windows Store 的假 stub（退出码 9009），只有 `py -3` 能用。Python 3.12.8 实际装在 `E:\python\Python312\python.exe`。README 里写的 `python tools/xxx.py` 在 PowerShell 里会失败，用 `py -3` 代替。
3. **先看 `preview_*.png`**（tools/ 下有几十个 `preview_*.png`，是用来肉眼比对的）再决定要不要覆盖正式贴图。
4. 正式图替换后，跑一次 Godot 让它导入：

```powershell
& 'D:\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --path . --headless --quit
```

## 坑

- **不要手改 `.import` 文件**：那是 Godot 生成的，改错了下次导入会被覆盖或报错。要调导入参数就在编辑器里调，或者接受默认。
- 新增 `.png` 后必须让 Godot 跑一次才会生成 `.import`；在那之前场景里引用它会加载失败。
- 文件名有中文，写脚本/命令时注意编码是 UTF-8；PowerShell 里带中文路径用单引号包住。
- `tools/ref/`、`tools/preview_audio/`、`tools/icon/` 是中间产物，已在 `.gitignore` 里，不要挪进 `assets/`。
- 别把 `preview_*.png` 删了 —— 它们是唯一的可视回归基线。
