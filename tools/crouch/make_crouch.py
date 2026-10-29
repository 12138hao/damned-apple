"""
把「蹲下」立绘处理成玩家精灵表的第 20 帧。

要点：
  · 原图背景是不透明的深色 (42,46,58)，而角色衣服是 (34,42,68)，两者只差 10，
    所以泛滥填充的容差必须很小（6），否则会把海军蓝背心一起吃掉
  · 原图是正面站姿，压制到 65% 高度来表现「蹲下」（不改画，只做纵向压缩）
  · 脚底对齐到 y=57、水平居中到 32，和现有帧一致
"""

import subprocess

FFMPEG = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
D = r"D:\h\my-game"

SRC = r"C:\Users\leader\Desktop\b9ffb5b5c314c2309b18e930d4ea3fee.png"
SW, SH = 609, 804
SHEET = D + r"\assets\player_sheet_v2.png"
CW, CH, FRAMES = 64, 64, 20

BG = (42, 46, 58)
TOL = 6

# 目标：脚底 y、高度、中心 x
FOOT_Y = 57
TARGET_H = 38          # 压到 68% 高度，表现蹲下
TARGET_W = 31          # 宽度和站立帧（31）保持一致，否则整个人会缩小
CENTER_X = 32


def load(path):
    p = subprocess.run([FFMPEG, "-loglevel", "error", "-i", path,
                        "-f", "rawvideo", "-pix_fmt", "rgba", "-"],
                       capture_output=True, check=True)
    return bytearray(p.stdout)


img = load(SRC)
print("读入", len(img), "字节", "期望", SW * SH * 4)


def is_bg(i):
    r, g, b = img[i], img[i + 1], img[i + 2]
    return (abs(r - BG[0]) <= TOL and abs(g - BG[1]) <= TOL and abs(b - BG[2]) <= TOL)


from collections import deque

seen = bytearray(SW * SH)
dq = deque()


def push(x, y):
    idx = y * SW + x
    if seen[idx] or not is_bg(idx * 4):
        return
    seen[idx] = 1
    dq.append(idx)


for x in range(SW):
    push(x, 0); push(x, SH - 1)
for y in range(SH):
    push(0, y); push(SW - 1, y)

while dq:
    idx = dq.popleft()
    x, y = idx % SW, idx // SW
    for nx, ny in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
        if 0 <= nx < SW and 0 <= ny < SH:
            push(nx, ny)

removed = 0
for idx in range(SW * SH):
    if seen[idx]:
        img[idx * 4 + 3] = 0
        removed += 1
print(f"去背景 {removed} 像素（容差 {TOL}）")

# 只保留最大连通块，去掉噪点
label = [0] * (SW * SH)
best, best_size, cur = -1, 0, 0
for start in range(SW * SH):
    if label[start] or img[start * 4 + 3] < 128:
        continue
    cur += 1
    label[start] = cur
    q = deque([start]); size = 0
    while q:
        idx = q.popleft(); size += 1
        x, y = idx % SW, idx // SW
        for nx, ny in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
            if 0 <= nx < SW and 0 <= ny < SH:
                j = ny * SW + nx
                if not label[j] and img[j * 4 + 3] >= 128:
                    label[j] = cur; q.append(j)
    if size > best_size:
        best, best_size = cur, size
for idx in range(SW * SH):
    if img[idx * 4 + 3] >= 128 and label[idx] != best:
        img[idx * 4 + 3] = 0

# 内容框
minx, miny, maxx, maxy = SW, SH, -1, -1
for y in range(SH):
    for x in range(SW):
        if img[(y * SW + x) * 4 + 3] > 8:
            minx = min(minx, x); maxx = max(maxx, x)
            miny = min(miny, y); maxy = max(maxy, y)
print(f"内容框 {minx},{miny} ~ {maxx},{maxy}  尺寸 {maxx-minx+1}x{maxy-miny+1}")

cw = maxx - minx + 1
chh = maxy - miny + 1
# 横纵分别缩放：宽度对齐站立帧，高度压扁 → 看起来像蹲下而不是变小
sx_scale = TARGET_W / cw
sy_scale = TARGET_H / chh
print(f"缩放 x={sx_scale:.4f} y={sy_scale:.4f} → 目标 {cw*sx_scale:.0f}x{chh*sy_scale:.0f}")

# 采样进 64x64 格子
cell = bytearray(CW * CH * 4)
for oy in range(CH):
    for ox in range(CW):
        # 反向映射到原图
        sx = (ox - CENTER_X) / sx_scale + (minx + maxx) * 0.5
        sy = (oy - FOOT_Y) / sy_scale + maxy
        x0, y0 = int(sx), int(sy)
        if x0 < 0 or y0 < 0 or x0 >= SW - 1 or y0 >= SH - 1:
            continue
        fx, fy = sx - x0, sy - y0
        ar = ag = ab = aa = 0.0
        for dy in (0, 1):
            for dx in (0, 1):
                w = (fx if dx else 1 - fx) * (fy if dy else 1 - fy)
                if w <= 0:
                    continue
                i = ((y0 + dy) * SW + x0 + dx) * 4
                a = img[i + 3]
                ar += img[i] * w * a
                ag += img[i + 1] * w * a
                ab += img[i + 2] * w * a
                aa += a * w
        if aa <= 0.5:
            continue
        i = (oy * CW + ox) * 4
        cell[i] = int(ar / aa)
        cell[i + 1] = int(ag / aa)
        cell[i + 2] = int(ab / aa)
        cell[i + 3] = int(min(255, aa))

# 拼接：原 20 帧 + 新 1 帧
sheet = load(SHEET)
assert len(sheet) == (CW * FRAMES) * CH * 4, len(sheet)
out = bytearray(CW * (FRAMES + 1) * CH * 4)
for y in range(CH):
    drow = y * CW * (FRAMES + 1) * 4
    out[drow:drow + CW * FRAMES * 4] = sheet[y * CW * FRAMES * 4:(y + 1) * CW * FRAMES * 4]
    out[drow + CW * FRAMES * 4: drow + CW * (FRAMES + 1) * 4] = cell[y * CW * 4:(y + 1) * CW * 4]

raw = D + r"\tools\crouch_sheet.raw"
open(raw, "wb").write(bytes(out))
out_png = D + r"\tools\crouch\player_sheet_v3.png"
import os
os.makedirs(D + r"\tools\crouch", exist_ok=True)
subprocess.run([FFMPEG, "-y", "-loglevel", "error",
                "-f", "rawvideo", "-pix_fmt", "rgba",
                "-s", f"{CW*(FRAMES+1)}x{CH}", "-i", raw,
                "-frames:v", "1", out_png], check=True)
print("输出：", out_png, f"{CW*(FRAMES+1)}x{CH}")
