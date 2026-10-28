"""
把三个视图拼成 32 帧精灵表（单行 32 × 160×160 = 5120×160）。

帧段划分：
  0~3   呼吸（朝右）
  4~7   呼吸（朝左）
  8~13  跳跃行走（朝右，6 帧一个完整蹦跳循环）
  14~19 跳跃行走（朝左）
  20~27 飞扑撕咬（正视图，8 帧：蓄力→前冲→张嘴→咬合→回收）
  28~31 死亡炸开（4 帧，3×3 碎块向外飞散并淡出）

所有帧都是对三张静态图做「缩放 / 压扁拉伸 / 位移 / 碎块位移」得到的，
没有重新绘制，属于程序化合成。
"""

import os
import math
import subprocess
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from explosion import render_explosion

FFMPEG = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
D = r"D:\h\my-game\tools\applemon"
CELL = 160
CONTENT_H = 130          # 内容在格子里的目标高度
FOOT = 150               # 脚底在格子里的 y（底部留 10px）
FRAMES = 32

SOURCES = {}
for name in ("left", "right", "center"):
    SOURCES[name] = open(os.path.join(D, name + ".rgba"), "rb").read()

# 每个视图的内容外接框（由 prep_sprites.py 跑出来的结果）
BOXES = {
    "left":   (84, 27, 443, 475),
    "right":  (65, 27, 440, 481),
    "center": (48, 24, 463, 483),
}


def sample(src, box, sx, sy):
    """双线性采样，坐标越界返回 (0,0,0,0)。"""
    W = 512
    if sx < box[0] - 1 or sx > box[2] + 1 or sy < box[1] - 1 or sy > box[3] + 1:
        return 0, 0, 0, 0
    x0, y0 = int(math.floor(sx)), int(math.floor(sy))
    fx, fy = sx - x0, sy - y0
    out = [0.0, 0.0, 0.0, 0.0]
    for dy in (0, 1):
        yy = y0 + dy
        if yy < 0 or yy >= 512:
            continue
        wy = fy if dy else 1.0 - fy
        for dx in (0, 1):
            xx = x0 + dx
            if xx < 0 or xx >= 512:
                continue
            wx = fx if dx else 1.0 - fx
            w = wx * wy
            if w <= 0:
                continue
            i = (yy * W + xx) * 4
            a = src[i + 3]
            out[0] += src[i] * w * a
            out[1] += src[i + 1] * w * a
            out[2] += src[i + 2] * w * a
            out[3] += a * w
    if out[3] <= 0.5:
        return 0, 0, 0, 0
    return (int(out[0] / out[3]), int(out[1] / out[3]), int(out[2] / out[3]),
            int(out[3]))


def render(src_name, scale_x=1.0, scale_y=1.0, dx=0.0, dy=0.0,
           alpha=1.0, explode=0.0, shrink=1.0):
    """
    把某个视图渲染成一格 160×160 RGBA。

    explode > 0 时按 3×3 切块，每块沿「离中心的方向」向外平移；
    shrink < 1 时每块还会朝着自己的中心缩小——不缩的话块与块只是平移，
    看起来像方块在滑动，不像炸开。
    """
    src = SOURCES[src_name]
    box = BOXES[src_name]
    cw = box[2] - box[0] + 1
    ch = box[3] - box[1] + 1
    base = CONTENT_H / ch
    kx = base * scale_x
    ky = base * scale_y
    cx_src = (box[0] + box[2]) * 0.5
    cx_out = CELL * 0.5 + dx
    foot_out = FOOT + dy

    cell = bytearray(CELL * CELL * 4)

    # 炸开：按角度切成 8 瓣，每瓣沿自己的方向飞出去并缩小。
    # 用放射状切法而不是 3×3 网格 —— 网格的直边看起来像拼图在滑动。
    WEDGES = 12
    wx_src = (box[0] + box[2]) * 0.5
    wy_src = (box[1] + box[3]) * 0.5
    rad_src = max(cw, ch) * 0.5
    wedge_dir = {}
    wedge_pivot = {}
    if explode > 0:
        for w in range(WEDGES):
            ang = -math.pi + (w + 0.5) / WEDGES * math.tau
            dxw, dyw = math.cos(ang), math.sin(ang)
            wedge_dir[w] = (dxw, dyw)
            # 缩放中心取在「从整体中心往外 55% 半径」处，碎块才会朝外散开
            pxs = wx_src + dxw * rad_src * 0.55
            pys = wy_src + dyw * rad_src * 0.55
            wedge_pivot[w] = (cx_out + (pxs - cx_src) * kx,
                              foot_out + (pys - box[3]) * ky)

    for oy in range(CELL):
        for ox in range(CELL):
            px, py = float(ox), float(oy)
            if explode > 0:
                sx_base = (px - cx_out) / kx + cx_src
                sy_base = (py - foot_out) / ky + box[3]
                ang = math.atan2(sy_base - wy_src, sx_base - wx_src)
                w = int((ang + math.pi) / math.tau * WEDGES) % WEDGES
                dxw, dyw = wedge_dir[w]
                shx = dxw * explode
                shy = dyw * explode - explode * 0.12   # 整体略微上飘
                ccx, ccy = wedge_pivot[w]
                px = ccx + (px - shx - ccx) / shrink
                py = ccy + (py - shy - ccy) / shrink

            sx = (px - cx_out) / kx + cx_src
            sy = (py - foot_out) / ky + box[3]

            r, g, b, a = sample(src, box, sx, sy)
            if a <= 0:
                continue
            a = int(a * alpha)
            if a <= 0:
                continue
            i = (oy * CELL + ox) * 4
            cell[i], cell[i + 1], cell[i + 2], cell[i + 3] = r, g, b, a

    return cell


# ─────────── 帧定义 ───────────
# (视图, scale_x, scale_y, dx, dy, alpha, explode)
def breathe_frames(view):
    # 呼吸：纵向起伏，横向反向补偿，脚底不动
    return [(view, 1.00, 1.000, 0, 0, 1.0, 0, 1.0),
            (view, 0.99, 1.030, 0, 0, 1.0, 0, 1.0),
            (view, 1.00, 1.005, 0, 0, 1.0, 0, 1.0),
            (view, 1.01, 0.972, 0, 0, 1.0, 0, 1.0)]


def hop_frames(view):
    # 蹦跳循环：蹲 → 起跳 → 最高 → 下落 → 落地压扁 → 回弹
    return [(view, 1.10, 0.880, 0, 0, 1.0, 0, 1.0),
            (view, 0.92, 1.120, 0, -7, 1.0, 0, 1.0),
            (view, 0.97, 1.045, 0, -15, 1.0, 0, 1.0),
            (view, 1.00, 1.000, 0, -9, 1.0, 0, 1.0),
            (view, 1.12, 0.850, 0, 0, 1.0, 0, 1.0),
            (view, 0.99, 1.020, 0, -2, 1.0, 0, 1.0)]


def lunge_frames():
    # 6 帧：蓄力后仰 → 压紧 → 前冲拉伸 → 张嘴咬 → 回收 → 站定
    return [("center", 0.94, 1.060, 0, 0, 1.0, 0, 1.0),
            ("center", 1.02, 0.930, 0, 1, 1.0, 0, 1.0),
            ("center", 1.25, 0.930, 2, -5, 1.0, 0, 1.0),
            ("center", 1.32, 1.000, 3, -7, 1.0, 0, 1.0),
            ("center", 1.08, 1.020, 1, -2, 1.0, 0, 1.0),
            ("center", 1.00, 1.000, 0, 0, 1.0, 0, 1.0)]


# 死亡 6 帧：(内容缩放, 飞散距离, 碎块缩放, 自转角度, 透明度, 爆点闪光)
# 缩放逐步变小，是为了给碎片腾出飞散空间——格子只有 160 宽，飞太远会被裁掉。
DEATH = [
    # (内容缩放, 飞散距离, 碎块缩放, 自转°, 透明度, 爆点闪光, 拖影长度)
    (1.08,  0, 1.00,  0, 1.00, 0.00, 0),
    (1.04,  6, 0.97,  2, 1.00, 0.28, 0),
    (1.00, 14, 0.90,  6, 1.00, 0.42, 2),
    (0.96, 22, 0.78, 11, 0.95, 0.20, 4),
    (0.92, 30, 0.60, 17, 0.62, 0.07, 5),
    (0.86, 40, 0.46, 26, 0.32, 0.00, 6),
]


frames = []
frames += breathe_frames("right")   # 0~3
frames += breathe_frames("left")    # 4~7
frames += hop_frames("right")       # 8~13
frames += hop_frames("left")        # 14~19
frames += lunge_frames()            # 20~25

assert len(frames) == FRAMES - len(DEATH), (len(frames), len(DEATH))

sheet = bytearray(CELL * FRAMES * CELL * 4)
def put_cell(idx, cell):
    for y in range(CELL):
        dst = (y * CELL * FRAMES + idx * CELL) * 4
        src_off = y * CELL * 4
        sheet[dst:dst + CELL * 4] = cell[src_off:src_off + CELL * 4]


for idx, (view, sx, sy, dx, dy, al, ex, sk) in enumerate(frames):
    cell = render(view, sx, sy, dx, dy, al, ex, sk)
    put_cell(idx, cell)
    print(f"  帧 {idx:2d}  {view:6s} sx={sx:.2f} sy={sy:.2f} dy={dy:+d} "
          f"alpha={al:.2f} explode={ex:.0f}")

# 死亡段
base_idx = len(frames)
for k, (cs, dist, shrink, rot, alpha, flash, blur) in enumerate(DEATH):
    view = "right"
    box = BOXES[view]
    cw = box[2] - box[0] + 1
    ch = box[3] - box[1] + 1
    base = CONTENT_H * cs / ch
    kx = base
    ky = base
    src_cx = (box[0] + box[2]) * 0.5
    src_cy = (box[1] + box[3]) * 0.5
    src = SOURCES[view]

    def smp(x, y, _src=src, _box=box):
        return sample(_src, _box, x, y)

    cell = render_explosion(smp, box, CELL, CONTENT_H, FOOT,
                            dist, shrink, rot, alpha, flash, blur,
                            src_cx, src_cy, kx, ky, CELL * 0.5, FOOT)
    put_cell(base_idx + k, cell)
    print(f"  帧 {base_idx + k:2d}  死亡 cs={cs:.2f} 飞散={dist:2d} "
          f"碎缩={shrink:.2f} 自转={rot:2d}° α={alpha:.2f} 闪光={flash:.2f}")

raw = os.path.join(D, "sheet.raw")
open(raw, "wb").write(bytes(sheet))
out_png = os.path.join(D, "hop_apple_sheet.png")
subprocess.run([FFMPEG, "-y", "-loglevel", "error",
                "-f", "rawvideo", "-pix_fmt", "rgba",
                "-s", f"{CELL * FRAMES}x{CELL}", "-i", raw,
                "-frames:v", "1", out_png], check=True)
print("精灵表:", out_png, os.path.getsize(out_png), "字节",
      f"({CELL * FRAMES}x{CELL})")
