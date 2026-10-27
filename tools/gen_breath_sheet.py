"""把单帧角色贴图生成为「呼吸循环」精灵表。

原理：以脚底为锚点做纵向缩放，生成 N 帧正弦起伏，拼成横排精灵表。
不做插值（最近邻取样），保持像素锐利。

用法：python tools/gen_breath_sheet.py <原图> [输出名]
默认：assets/milkfrog_transparent.png → assets/naiwa_sheet.png
"""
import math
import os
import struct
import sys
import zlib

# 手写的呼吸缩放曲线：吸气鼓起 → 回正 → 呼气收腹 → 回正
SCALES = [1.000, 1.030, 1.006, 0.974]
FRAMES = len(SCALES)


def load_png(path):
    d = open(path, 'rb').read()
    pos = 8
    w = h = 0
    ct = 0
    idat = b''
    while pos < len(d):
        ln = struct.unpack('>I', d[pos:pos + 4])[0]
        tag = d[pos + 4:pos + 8]
        data = d[pos + 8:pos + 8 + ln]
        if tag == b'IHDR':
            w, h, _bd, ct = struct.unpack('>IIBB', data[:10])
        elif tag == b'IDAT':
            idat += data
        pos += 12 + ln

    raw = zlib.decompress(idat)
    bpp = 4 if ct == 6 else 3
    stride = w * bpp

    def paeth(a, b, c):
        p = a + b - c
        pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
        return a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)

    prev = bytearray(stride)
    rows = []
    for y in range(h):
        f = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1: y * (stride + 1) + 1 + stride])
        if f == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 255
        elif f == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 255
        elif f == 3:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 255
        elif f == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                c = prev[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + paeth(a, prev[i], c)) & 255
        rows.append(bytes(line))
        prev = line

    if bpp == 4:
        return w, h, rows

    # RGB → RGBA
    rgba = []
    for r in rows:
        line = bytearray()
        for x in range(w):
            line.extend(r[x * 3:x * 3 + 3])
            line.append(255)
        rgba.append(bytes(line))
    return w, h, rgba


def find_feet_row(w, h, rows):
    """从下往上找第一条有非透明像素的行。"""
    for y in range(h - 1, -1, -1):
        line = rows[y]
        for x in range(w):
            if line[x * 4 + 3] > 8:
                return y
    return h - 1


def breathe_frame(w, h, rows, feet_y, scale):
    """以脚底为锚点纵向缩放（最近邻）。"""
    out = []
    for y in range(h):
        src = feet_y - (feet_y - y) / scale
        src = max(0, min(h - 1, int(round(src))))
        out.append(rows[src])
    return out


def encode_sheet(frames, w, h):
    sw = w * len(frames)
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for f in frames:
            raw.extend(f[y])

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data
                + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))

    return (b'\x89PNG\r\n\x1a\n'
            + chunk(b'IHDR', struct.pack('>IIBBBBB', sw, h, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
            + chunk(b'IEND', b''))


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, 'assets', 'milkfrog_transparent.png')
    out_name = sys.argv[2] if len(sys.argv) > 2 else 'naiwa_sheet.png'
    out = os.path.join(root, 'assets', out_name)

    w, h, rows = load_png(src)
    feet = find_feet_row(w, h, rows)
    print('原图 %dx%d  脚底行=%d' % (w, h, feet))

    frames = []
    for i, sc in enumerate(SCALES):
        frames.append(breathe_frame(w, h, rows, feet, sc))
        print('  帧%d 缩放=%.4f' % (i, sc))

    png = encode_sheet(frames, w, h)
    with open(out, 'wb') as fp:
        fp.write(png)
    print('已生成：%s  (%dx%d, %d 帧)' % (out, w * FRAMES, h, FRAMES))


if __name__ == '__main__':
    main()
