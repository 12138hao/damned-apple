"""
用三张「苹果碎裂」美术图生成 6 帧死亡动画。

三张图是递进的（裂开 → 掏空 → 只剩残片），但只有 3 帧不够顺，
所以在关键帧之间插入交叉溶解帧，让碎裂过程连起来。

缩放基准统一取第一张图的内容高度：这样后面两张残片自然显得越来越小，
不会因为各自填满格子而看起来一样大。
"""

import os
import subprocess

FFMPEG = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
DIR = r"D:\h\my-game\tools\applemon"

SOURCES = [
    r"C:\Users\leader\Downloads\生成苹果碎裂图.png",
    r"C:\Users\leader\Downloads\生成苹果碎裂图 (1).png",
    r"C:\Users\leader\Downloads\生成苹果碎裂图 (2).png",
]

W = H = 1536


def load_rgba(path):
    p = subprocess.run([FFMPEG, "-loglevel", "error", "-i", path,
                        "-f", "rawvideo", "-pix_fmt", "rgba", "-"],
                       capture_output=True, check=True)
    d = bytearray(p.stdout)
    assert len(d) == W * H * 4, (path, len(d))
    return d


def bbox(data, thr=8):
    minx, miny, maxx, maxy = W, H, -1, -1
    for y in range(H):
        row = y * W
        for x in range(W):
            if data[(row + x) * 4 + 3] > thr:
                if x < minx: minx = x
                if x > maxx: maxx = x
                if y < miny: miny = y
                if y > maxy: maxy = y
    return minx, miny, maxx, maxy


def render_cell(img, cell, cx_src, by_src, scale, dx=0.0, dy=0.0, alpha=1.0):
    """把整张图按统一基准映射进 cell（反向映射 + 双线性采样）。"""
    out = bytearray(cell * cell * 4)
    inv = 1.0 / scale
    for oy in range(cell):
        for ox in range(cell):
            ix = cx_src + (ox - cell * 0.5 - dx) * inv
            iy = by_src - (150.0 + dy - oy) * inv
            x0, y0 = int(ix), int(iy)
            if x0 < -1 or y0 < -1 or x0 > W or y0 > H:
                continue
            fx, fy = ix - x0, iy - y0
            ar = ag = ab = aa = 0.0
            for sy_ in (0, 1):
                yy = y0 + sy_
                if yy < 0 or yy >= H:
                    continue
                wy = fy if sy_ else 1.0 - fy
                for sx_ in (0, 1):
                    xx = x0 + sx_
                    if xx < 0 or xx >= W:
                        continue
                    wx = fx if sx_ else 1.0 - fx
                    w = wx * wy
                    if w <= 0:
                        continue
                    i = (yy * W + xx) * 4
                    a = img[i + 3]
                    ar += img[i] * w * a
                    ag += img[i + 1] * w * a
                    ab += img[i + 2] * w * a
                    aa += a * w
            if aa <= 0.5:
                continue
            i = (oy * cell + ox) * 4
            out[i] = int(ar / aa)
            out[i + 1] = int(ag / aa)
            out[i + 2] = int(ab / aa)
            out[i + 3] = int(min(255, aa * alpha))
    return out


def blend(a, b, t):
    """两帧交叉溶解（按 alpha 加权，避免边缘发灰）。"""
    n = len(a) // 4
    out = bytearray(len(a))
    for i in range(n):
        j = i * 4
        a1, a2 = a[j + 3], b[j + 3]
        w1, w2 = a1 * (1.0 - t), a2 * t
        s = w1 + w2
        if s <= 0.5:
            continue
        out[j] = int((a[j] * w1 + b[j] * w2) / s)
        out[j + 1] = int((a[j + 1] * w1 + b[j + 1] * w2) / s)
        out[j + 2] = int((a[j + 2] * w1 + b[j + 2] * w2) / s)
        out[j + 3] = int(min(255, s))
    return out


def build_death_frames(cell=160, content_h=130):
    imgs = [load_rgba(p) for p in SOURCES]
    boxes = [bbox(im) for im in imgs]
    for p, b in zip(SOURCES, boxes):
        print(f"  {os.path.basename(p)} 内容框 {b}")

    # 统一基准：第一张的内容高度撑满 content_h
    b1 = boxes[0]
    scale = content_h / (b1[3] - b1[1] + 1)
    cx = (b1[0] + b1[2]) * 0.5
    by = b1[3]
    print(f"  基准 scale={scale:.4f}  cx={cx:.0f}  by={by}")

    def R(idx, dx=0.0, dy=0.0, alpha=1.0, s_mul=1.0):
        return render_cell(imgs[idx], cell, cx, by, scale * s_mul, dx, dy, alpha)

    f1 = R(0)
    f2 = R(1)
    f3 = R(2)

    # 6 帧：三张关键帧 + 中间溶解帧，最后淡出
    frames = [
        f1,
        blend(f1, R(1, dy=-2, s_mul=1.03), 0.45),
        R(1, dy=-1),
        blend(f2, R(2, dy=-2, s_mul=0.98), 0.5),
        R(2, dy=-1),
        R(2, dy=-4, s_mul=0.94, alpha=0.55),
    ]
    return frames
