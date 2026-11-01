"""
只改「图一」，让它的右边缘接上图二时不突兀。图二一个像素都不碰。

思路：不叠图、不遮罩，而是让图一**越靠近右边缘，颜色越变成图二的调子**。
这样两张图真正碰在一起的时候，图一那边已经提前过渡完了。

分三步，全部按 smoothstep 从 0 渐变到 1（x = W-RAMP 处权重 0，最右边权重 1）：

  1. 调色板对齐：统计图一右段 vs 图二左段的每通道均值/标准差，
     做一次仿射变换 out = (v - 均值1) * k + 均值2
     —— 把橙红、高对比度拉成图二那种偏棕、低对比的样子
  2. 压高光：图一底部那条橙色地面线亮度 110+，图二同位置只有 24，
     单独把亮部压下去（暗部不动，所以不会出现黑柱）
  3. 轻微雾化：最边缘往一个暗暖色靠一点点，模拟洞穴里的空气透视

注意两个坑：
  · 图一最右侧自带 20~30px 暗角，统计时要避开最外面那几列，否则算出来全是黑的
  · 统计要整段一起算（全局），不能逐行算 —— 逐行会把图一自己的明暗结构抹平

用法：python tools/cave_seam/recolor_edge.py
输出：tools/cave_seam/out_E.png（原始 384x216，RGB）
"""

import os
import struct
import zlib

IMG1 = r"C:/Users/leader/.workbuddy-ai/clipboard-images/clipboard-2026-10-30T15-47-02-793Z-3d2819ec.png"
IMG2 = r"tools/cave_seam/img2_216.png"
OUT = os.environ.get("OUT", r"tools/cave_seam/out_E.png")

RAMP = int(os.environ.get("RAMP", 170))   # 从右边往里多少像素开始过渡
EDGE_SAMPLE = 6     # 贴边取样列数
Y_SMOOTH = 13       # 逐行锚点的纵向平滑窗口
CONTRAST = 0.42     # 最边缘的对比度压缩到多少（0.6 = 偏离贴边色的部分只剩 6 成）
DESAT = 0.50        # 最边缘往自身灰度靠多少（压掉橙红）
HIGHLIGHT = 0.70    # 额外压高光（亮度超过 45 的部分）
HIGHLIGHT_FLOOR = 30


# ───────────────────────── PNG 读写 ─────────────────────────
def load_png(path):
    data = open(path, "rb").read()
    pos, w, h, idat, ct = 8, 0, 0, b"", 0
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        typ = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, _bd, ct = struct.unpack(">IIBB", chunk[:10])
        elif typ == b"IDAT":
            idat += chunk
        pos += 12 + ln
    ch = {0: 1, 2: 3, 4: 2, 6: 4}[ct]
    raw = zlib.decompress(idat)
    stride = w * ch
    rows, prev, i = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[i]
        i += 1
        line = bytearray(raw[i:i + stride])
        i += stride
        if f == 1:
            for x in range(ch, stride):
                line[x] = (line[x] + line[x - ch]) & 255
        elif f == 2:
            for x in range(stride):
                line[x] = (line[x] + prev[x]) & 255
        elif f == 3:
            for x in range(stride):
                a = line[x - ch] if x >= ch else 0
                line[x] = (line[x] + ((a + prev[x]) >> 1)) & 255
        elif f == 4:
            for x in range(stride):
                a = line[x - ch] if x >= ch else 0
                b = prev[x]
                c = prev[x - ch] if x >= ch else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 255
        rows.append(line)
        prev = line
    return w, h, ch, rows


def save_png(path, w, h, rows, ch):
    raw = b"".join(b"\x00" + bytes(r) for r in rows)

    def chunk(t, d):
        return (struct.pack(">I", len(d)) + t + d
                + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF))

    ct = {1: 0, 3: 2, 4: 6}[ch]
    ihdr = struct.pack(">IIBBBBB", w, h, 8, ct, 0, 0, 0)
    blob = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    open(path, "wb").write(blob)


# ───────────────────────── 工具 ─────────────────────────
def region_stats(rows, ch, x0, x1):
    """整段（所有行）的每通道均值与标准差"""
    h = len(rows)
    s = [0.0, 0.0, 0.0]
    s2 = [0.0, 0.0, 0.0]
    n = 0
    for y in range(h):
        r = rows[y]
        for x in range(x0, x1):
            o = x * ch
            for c in range(3):
                v = r[o + c]
                s[c] += v
                s2[c] += v * v
            n += 1
    m = [s[c] / n for c in range(3)]
    sd = [max(1.0, ((s2[c] / n) - m[c] * m[c]) ** 0.5) for c in range(3)]
    return tuple(m), tuple(sd)


def smoothstep(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def clamp255(v):
    return 0 if v < 0 else (255 if v > 255 else int(round(v)))


# ───────────────────────── 主处理 ─────────────────────────
def row_edge(rows, ch, y, x0, n):
    """第 y 行 [x0, x0+n) 的平均 RGB"""
    r = rows[y]
    s = [0.0, 0.0, 0.0]
    for x in range(x0, x0 + n):
        o = x * ch
        for c in range(3):
            s[c] += r[o + c]
    return tuple(v / n for v in s)


def smooth_seq(seq, win):
    out, half = [], win // 2
    for i in range(len(seq)):
        acc = [0.0, 0.0, 0.0]
        n = 0
        for j in range(max(0, i - half), min(len(seq), i + half + 1)):
            for c in range(3):
                acc[c] += seq[j][c]
            n += 1
        out.append(tuple(v / n for v in acc))
    return out


def main():
    w1, h1, ch1, rows1 = load_png(IMG1)
    w2, h2, ch2, rows2 = load_png(IMG2)
    print(f"图一 {w1}x{h1} ch={ch1}   图二 {w2}x{h2} ch={ch2}")

    # 1. 逐行锚点：图一贴边那几列 / 图二贴边那几列
    e1 = smooth_seq([row_edge(rows1, ch1, y, w1 - EDGE_SAMPLE, EDGE_SAMPLE) for y in range(h1)], Y_SMOOTH)
    e2 = smooth_seq([row_edge(rows2, ch2, y, 0, EDGE_SAMPLE) for y in range(h1)], Y_SMOOTH)

    # 2. 逐像素：以贴边色为锚，往右压缩对比度 + 去饱和 + 压高光
    x0 = max(0, w1 - RAMP)
    out = []
    for y in range(h1):
        src = rows1[y]
        dst = bytearray(src)
        a1 = e1[y]
        a2 = e2[y]
        for x in range(x0, w1):
            t = smoothstep((x - x0 + 1) / RAMP)
            o = x * ch1
            r, g, b = src[o], src[o + 1], src[o + 2]
            lum = 0.299 * r + 0.587 * g + 0.114 * b
            scale = 1.0 + (CONTRAST - 1.0) * t      # 1.0 → CONTRAST
            for c, v in enumerate((r, g, b)):
                # 锚定到图二同行的贴边色，偏离量按 scale 压缩
                nv = a2[c] + (v - a1[c]) * scale
                # 去饱和：往自身灰度靠
                nv += (lum - nv) * (DESAT * t)
                # 压高光：只有超过 HIGHLIGHT_FLOOR 的亮度才压
                excess = max(0.0, lum - HIGHLIGHT_FLOOR) / (255.0 - HIGHLIGHT_FLOOR)
                nv *= 1.0 - HIGHLIGHT * t * excess
                dst[o + c] = clamp255(v * (1 - t) + nv * t)
        out.append(dst)

    save_png(OUT, w1, h1, out, ch1)
    print("已写出", OUT)

    # 3. 报告
    def edge_mean(rows, ch, xa, xb):
        m, _ = region_stats(rows, ch, xa, xb)
        return tuple(round(v) for v in m)

    print("接缝处平均色：")
    print("  图一最右 10 列（原）:", edge_mean(rows1, ch1, w1 - 10, w1))
    print("  图一最右 10 列（改）:", edge_mean(out, ch1, w1 - 10, w1))
    print("  图二最左 10 列      :", edge_mean(rows2, ch2, 0, 10))
    print("  图一 w-40~-30（原） :", edge_mean(rows1, ch1, w1 - 40, w1 - 30))
    print("  图一 w-40~-30（改） :", edge_mean(out, ch1, w1 - 40, w1 - 30))


if __name__ == "__main__":
    main()
