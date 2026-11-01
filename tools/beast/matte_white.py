#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
白底转透明：只抠「从图像边缘能连通到的纯白」，主体内部的白色（獠牙、果肉）保留。

为什么不能整图按白色抠：这只苹果的獠牙和果肉本身就是白的，一刀切会把牙齿抠没。

用法：
  python matte_white.py front.png [--out front_alpha.png] [--crop] [--max 1024]
"""
import os
import sys
import zlib
import struct
from collections import deque

# ─────────────────────────────────────────── PNG 读写（纯标准库）

def read_png(path):
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "不是 PNG"
    pos = 8
    idat = b""
    w = h = bd = ct = 0
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        typ = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", body[:10])
        elif typ == b"IDAT":
            idat += body
        elif typ == b"IEND":
            break
        pos += 12 + ln

    assert bd == 8, "只支持 8 位深度，当前 %d" % bd
    ch = {0: 1, 2: 3, 4: 2, 6: 4}[ct]
    raw = zlib.decompress(idat)
    stride = w * ch
    rows = []
    prev = bytearray(stride)
    p = 0
    for _ in range(h):
        ft = raw[p]; p += 1
        line = bytearray(raw[p:p + stride]); p += stride
        if ft == 1:
            for i in range(ch, stride):
                line[i] = (line[i] + line[i - ch]) & 0xFF
        elif ft == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ft == 3:
            for i in range(stride):
                a = line[i - ch] if i >= ch else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 0xFF
        elif ft == 4:
            for i in range(stride):
                a = line[i - ch] if i >= ch else 0
                b = prev[i]
                c = prev[i - ch] if i >= ch else 0
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 0xFF
        rows.append(line)
        prev = line
    return w, h, ch, rows


def write_png(path, w, h, rows, ch):
    ct = {1: 0, 3: 2, 4: 6}[ch]
    raw = bytearray()
    for r in rows:
        raw.append(0)
        raw += bytes(r)

    def chunk(typ, body):
        return (struct.pack(">I", len(body)) + typ + body
                + struct.pack(">I", zlib.crc32(typ + body) & 0xFFFFFFFF))

    out = b"\x89PNG\r\n\x1a\n"
    out += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, ct, 0, 0, 0))
    out += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    out += chunk(b"IEND", b"")
    open(path, "wb").write(out)


# ─────────────────────────────────────────── 抠图

def is_white_bg(r, g, b, thr):
    """背景判定：三通道都很亮且彼此接近。纯白 255,255,255 一定命中。"""
    return r >= thr and g >= thr and b >= thr and (max(r, g, b) - min(r, g, b)) <= 10


def matte(src, dst, thr=246):
    w, h, ch, rows = read_png(src)
    assert ch in (3, 4), "需要 RGB/RGBA，当前通道数 %d" % ch

    # 统一成 RGB，alpha 先全部设为不透明
    rgb = []
    for r in rows:
        line = []
        for x in range(w):
            if ch == 3:
                line.append([r[x * 3], r[x * 3 + 1], r[x * 3 + 2]])
            else:
                line.append([r[x * 4], r[x * 4 + 1], r[x * 4 + 2]])
        rgb.append(line)

    alpha = [[255] * w for _ in range(h)]
    visited = [[False] * w for _ in range(h)]
    q = deque()

    # 从四条边出发
    for x in range(w):
        for y in (0, h - 1):
            q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            q.append((x, y))

    filled = 0
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h:
            continue
        if visited[y][x]:
            continue
        visited[y][x] = True
        c = rgb[y][x]
        if not is_white_bg(c[0], c[1], c[2], thr):
            continue
        alpha[y][x] = 0
        filled += 1
        q.append((x + 1, y))
        q.append((x - 1, y))
        q.append((x, y + 1))
        q.append((x, y - 1))

    # 边缘去白：紧邻透明区的像素如果偏白，按亮度给半透明并把白底反混合掉
    edge_fixed = 0
    for y in range(h):
        for x in range(w):
            if alpha[y][x] == 0:
                continue
            # 只在贴着透明区的地方处理
            touch = False
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and alpha[ny][nx] == 0:
                        touch = True
                        break
                if touch:
                    break
            if not touch:
                continue

            c = rgb[y][x]
            mn = min(c)
            if mn <= 185:
                continue    # 深色描边，不动

            # a = 该像素离纯白多远（越白越透明）
            a = (255 - mn) / 70.0
            a = max(0.0, min(1.0, a))
            if a <= 0.02:
                alpha[y][x] = 0
                edge_fixed += 1
                continue

            # 反混合：C = a*F + (1-a)*255  →  F = (C - (1-a)*255)/a
            for k in range(3):
                v = (c[k] - (1 - a) * 255) / a
                rgb[y][x][k] = max(0, min(255, int(round(v))))
            alpha[y][x] = int(round(a * 255))
            edge_fixed += 1

    out = []
    for y in range(h):
        line = bytearray()
        for x in range(w):
            c = rgb[y][x]
            line += bytes((c[0], c[1], c[2], alpha[y][x]))
        out.append(line)

    write_png(dst, w, h, out, 4)
    total = w * h
    print("  %s -> %s  %dx%d" % (os.path.basename(src), os.path.basename(dst), w, h))
    print("    抠掉背景 %.1f%%  边缘去白 %d 像素" % (filled * 100.0 / total, edge_fixed))
    return w, h


def crop_to_content(src, dst, pad=2):
    """按 alpha 裁到内容边界，去掉四周空白，游戏里好摆。"""
    w, h, ch, rows = read_png(src)
    assert ch == 4
    x0, y0, x1, y1 = w, h, -1, -1
    for y in range(h):
        for x in range(w):
            if rows[y][x * 4 + 3] > 0:
                if x < x0: x0 = x
                if x > x1: x1 = x
                if y < y0: y0 = y
                if y > y1: y1 = y
    if x1 < 0:
        print("  [警告] 全透明，跳过裁剪")
        return w, h
    x0 = max(0, x0 - pad); y0 = max(0, y0 - pad)
    x1 = min(w - 1, x1 + pad); y1 = min(h - 1, y1 + pad)
    nw, nh = x1 - x0 + 1, y1 - y0 + 1
    out = []
    for y in range(y0, y1 + 1):
        out.append(bytearray(rows[y][x0 * 4:(x1 + 1) * 4]))
    write_png(dst, nw, nh, out, 4)
    print("  裁剪 -> %dx%d" % (nw, nh))
    return nw, nh


if __name__ == "__main__":
    src = sys.argv[1]
    dst = sys.argv[2] if len(sys.argv) > 2 else src.replace(".png", "_alpha.png")
    thr = int(os.environ.get("THR", "246"))
    matte(src, dst, thr)
    if "--crop" in sys.argv:
        crop_to_content(dst, dst)
