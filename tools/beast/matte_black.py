#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
黑底转透明 + 横向序列图等宽切帧。

Seedream 出的爆炸序列是纯黑底、8 帧横排，但每帧的包围盒不一样大。
Godot 的 Sprite2D + Hframes 要求每帧尺寸完全一致，所以这里：
  ① 黑底 → alpha（亮度当不透明度，颜色反混合掉黑底）
  ② 按等宽切 8 帧
  ③ 量出所有帧的最大内容框，每帧居中贴进同样大小的框
  ④ 横向拼回去

用法：
  python matte_black.py src.jpg out.png --frames 8
"""
import sys
import zlib
import struct

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


def matte_black(src, dst, frames, out_frame_h=None, floor=26):
    w, h, rgb = load_rgb(src)

    # ① 黑底转透明：alpha 取三通道最大值（保留所有亮色），颜色反混合
    #    floor 是「黑底噪点」的截止值：JPG 压缩会在纯黑区留下 5~20 的灰噪点，
    #    不掐掉的话整帧都会被当成内容，包围盒直接等于整幅图。
    for y in range(h):
        for x in range(w):
            c = rgb[y][x]
            a = max(c[0], c[1], c[2])
            if a <= floor:
                rgb[y][x] = [0, 0, 0, 0]
                continue
            # 把 [floor, 255] 线性拉回 [0, 255]，去掉噪点后剩下的才是真的发光
            k = (a - floor) / float(255 - floor)
            if k <= 0.0:
                rgb[y][x] = [0, 0, 0, 0]
                continue
            for i in range(3):
                v = c[i] / (a / 255.0) if a > 0 else 0
                rgb[y][x][i] = max(0, min(255, int(round(v))))
            rgb[y][x][3] = int(round(k * 255))

    # ② 等宽切帧
    fw = w // frames
    tiles = []
    for i in range(frames):
        x0 = i * fw
        # 量内容框
        bx0, by0, bx1, by1 = fw, h, -1, -1
        for y in range(h):
            for x in range(x0, x0 + fw):
                if rgb[y][x][3] > 8:
                    if x - x0 < bx0: bx0 = x - x0
                    if x - x0 > bx1: bx1 = x - x0
                    if y < by0: by0 = y
                    if y > by1: by1 = y
        if bx1 < 0:
            tiles.append((x0, 0, 0, fw, h))
            continue
        tiles.append((x0, bx0, by0, bx1 - bx0 + 1, by1 - by0 + 1))

    # ③ 统一框大小
    max_w = max(t[3] for t in tiles)
    max_h = max(t[4] for t in tiles)
    if out_frame_h:
        max_h = out_frame_h

    # 每帧统一成正方形（取宽高较大者），避免长宽比不同导致缩放不一致
    side = max(max_w, max_h)

    out_w = side * frames
    out_h = side
    out = [bytearray(out_w * 4) for _ in range(out_h)]

    for i, (x0, bx, by, bw, bh) in enumerate(tiles):
        if bw <= 0:
            continue
        ox = i * side + (side - bw) // 2
        oy = (side - bh) // 2
        for y in range(bh):
            for x in range(bw):
                c = rgb[by + y][x0 + bx + x]
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
    print("  %d 帧，每帧 %dx%d，总图 %dx%d" % (frames, side, side, out_w, out_h))
    print("  各帧原始内容框: %s" % str([(t[3], t[4]) for t in tiles]))


if __name__ == "__main__":
    src = sys.argv[1]
    dst = sys.argv[2]
    frames = 8
    if "--frames" in sys.argv:
        frames = int(sys.argv[sys.argv.index("--frames") + 1])
    matte_black(src, dst, frames)
