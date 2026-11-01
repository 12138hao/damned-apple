"""
去掉图一右下角的「豆包 AI 生成」水印。

水印位置：x ≈ 345~384，y ≈ 204~216（右下角，右边还被图片边缘裁掉了一半）
周围是几乎均匀的暗棕岩石，所以直接**从左边同高度的暗部横向克隆**一块过来，
再在接缝处做羽化，肉眼看不出来。

用法：python tools/cave_seam/kill_watermark.py
输出：tools/cave_seam/img1_clean.png
"""

import os
import struct
import zlib

SRC = r"C:/Users/leader/.workbuddy-ai/clipboard-images/clipboard-2026-10-30T15-47-02-793Z-3d2819ec.png"
OUT = os.environ.get("OUT", r"tools/cave_seam/img1_clean.png")

# 要覆盖的矩形（留了点余量）
RECT_X0, RECT_X1 = 334, 384
RECT_Y0, RECT_Y1 = 196, 216

CLONE_SHIFT = 52    # 从左边 x-CLONE_SHIFT 处取像素
FEATHER = 14        # 左边界的羽化宽度


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


def main():
    w, h, ch, rows = load_png(SRC)
    print(f"图一 {w}x{h} ch={ch}")

    out = [bytearray(r) for r in rows]
    for y in range(RECT_Y0, RECT_Y1):
        src = rows[y]
        dst = out[y]
        for x in range(RECT_X0, RECT_X1):
            # 越靠近左边界，越保留原像素（羽化）
            k = min(1.0, (x - RECT_X0 + 1) / FEATHER)
            sx = x - CLONE_SHIFT
            if sx < 0:
                continue
            so = sx * ch
            do = x * ch
            for c in range(3):
                dst[do + c] = int(round(src[do + c] * (1 - k) + src[so + c] * k))

    save_png(OUT, w, h, out, ch)
    print("已写出", OUT)


if __name__ == "__main__":
    main()
