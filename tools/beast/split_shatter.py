#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把 Seedream 出的「黑底 + 横向 N 帧」序列图处理成 Godot 能用的精灵表。

和 matte_black.py 的区别：
  matte_black 是**等宽**切帧，只适合模型把每帧摆得一样匀的情况。
  这次的碎裂序列，模型摆得忽左忽右（前四帧挤在左边、后两帧散在中间），
  等宽切会把第二帧切成两半。所以这里改成**按黑色空隙切**：
    ① 黑底转透明（alpha = 三通道最大值，颜色反混合掉黑底）
    ② 统计每一列有没有内容，得到若干「内容段」
    ③ 段数比 N 多 → 反复合并「空隙最窄的相邻两段」，直到正好 N 段
    ④ 每段量内容框，统一成正方形居中，横向拼回去

用法：
  python split_shatter.py in.png out.png --frames 6
"""
import sys

from matte_white import read_png, write_png


def load_rgb(path):
    w, h, ch, rows = read_png(path)
    rgb = []
    for r in rows:
        line = []
        for x in range(w):
            if ch == 3:
                line.append([r[x * 3], r[x * 3 + 1], r[x * 3 + 2], 255])
            else:
                line.append([r[x * 4], r[x * 4 + 1], r[x * 4 + 2], r[x * 4 + 3]])
        rgb.append(line)
    return w, h, rgb


def matte(rgb, w, h, floor=24):
    """黑底转透明。floor 掐掉 JPG 在纯黑区留下的 5~20 灰噪点。"""
    for y in range(h):
        for x in range(w):
            c = rgb[y][x]
            a = max(c[0], c[1], c[2])
            if a <= floor:
                rgb[y][x] = [0, 0, 0, 0]
                continue
            k = (a - floor) / float(255 - floor)
            if k <= 0.0:
                rgb[y][x] = [0, 0, 0, 0]
                continue
            for i in range(3):
                rgb[y][x][i] = max(0, min(255, int(round(c[i] / (a / 255.0)))))
            rgb[y][x][3] = int(round(k * 255))


def content_runs(rgb, w, h, alpha_thr=10):
    """返回 [(x0, x1), ...]，每段是一串「至少有一列有内容」的连续列。"""
    cols = [False] * w
    for x in range(w):
        for y in range(h):
            if rgb[y][x][3] > alpha_thr:
                cols[x] = True
                break

    runs = []
    x = 0
    while x < w:
        if not cols[x]:
            x += 1
            continue
        x0 = x
        while x < w and cols[x]:
            x += 1
        runs.append((x0, x - 1))
    return runs


def merge_to(runs, target):
    """合并相邻段，直到正好 target 段。每次合并「空隙最窄」的那一对。"""
    runs = list(runs)
    while len(runs) > target:
        best = None
        for i in range(len(runs) - 1):
            gap = runs[i + 1][0] - runs[i][1]
            if best is None or gap < best[0]:
                best = (gap, i)
        _, i = best
        runs[i] = (runs[i][0], runs[i + 1][1])
        del runs[i + 1]
    return runs


def main(src, dst, frames):
    w, h, rgb = load_rgb(src)
    matte(rgb, w, h)

    runs = content_runs(rgb, w, h)
    print("  原始内容段 %d 个: %s" % (len(runs), runs))
    if len(runs) < frames:
        print("  !! 段数少于 %d，可能有两帧粘在一起" % frames)
    runs = merge_to(runs, frames)
    print("  合并后 %d 段: %s" % (len(runs), runs))

    # 每段量内容框
    boxes = []
    for (x0, x1) in runs:
        bx0, by0, bx1, by1 = x1, h, x0, -1
        for y in range(h):
            for x in range(x0, x1 + 1):
                if rgb[y][x][3] > 10:
                    if x < bx0: bx0 = x
                    if x > bx1: bx1 = x
                    if y < by0: by0 = y
                    if y > by1: by1 = y
        if bx1 < 0:
            boxes.append(None)
        else:
            boxes.append((bx0, by0, bx1 - bx0 + 1, by1 - by0 + 1))
    print("  各帧内容框: %s" % str([(b[2], b[3]) if b else None for b in boxes]))

    side = max(max(b[2], b[3]) for b in boxes if b)
    out_w, out_h = side * frames, side
    out = [bytearray(out_w * 4) for _ in range(out_h)]

    for i, b in enumerate(boxes):
        if not b:
            continue
        bx, by, bw, bh = b
        ox = i * side + (side - bw) // 2
        oy = (side - bh) // 2
        for y in range(bh):
            for x in range(bw):
                c = rgb[by + y][bx + x]
                if c[3] == 0:
                    continue
                tx, ty = ox + x, oy + y
                if 0 <= tx < out_w and 0 <= ty < out_h:
                    o = tx * 4
                    out[ty][o] = c[0]
                    out[ty][o + 1] = c[1]
                    out[ty][o + 2] = c[2]
                    out[ty][o + 3] = c[3]

    write_png(dst, out_w, out_h, out, 4)
    print("  输出 %d 帧，每帧 %dx%d，总图 %dx%d" % (frames, side, side, out_w, out_h))


if __name__ == "__main__":
    src, dst = sys.argv[1], sys.argv[2]
    frames = 6
    if "--frames" in sys.argv:
        frames = int(sys.argv[sys.argv.index("--frames") + 1])
    main(src, dst, frames)
