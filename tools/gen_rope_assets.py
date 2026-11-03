# -*- coding: utf-8 -*-
"""生成两个素材：W 键帽（复用 E 图标的盾牌）+ 像素绳索。
纯 stdlib，自带最小 PNG 读写（项目里没有 PIL）。"""
import struct, zlib, os, math

# ── 最小 PNG 解码 ────────────────────────────────────────────
def read_png(path):
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', '不是 PNG'
    p, idat = 8, b''
    w = h = bd = ct = None
    while p < len(data):
        ln = struct.unpack('>I', data[p:p + 4])[0]
        typ = data[p + 4:p + 8]
        chunk = data[p + 8:p + 8 + ln]
        p += 12 + ln
        if typ == b'IHDR':
            w, h, bd, ct = struct.unpack('>IIBB', chunk[:10])
        elif typ == b'IDAT':
            idat += chunk
        elif typ == b'IEND':
            break
    raw = zlib.decompress(idat)
    ch = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ct]
    bpp = ch * bd // 8
    stride = w * bpp
    out, prev, pos = bytearray(), bytearray(stride), 0
    for _ in range(h):
        f = raw[pos]; pos += 1
        line = bytearray(raw[pos:pos + stride]); pos += stride
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
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                pp = a + b - c
                pa, pb, pc = abs(pp - a), abs(pp - b), abs(pp - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        out += line
        prev = line
    # 统一转成 RGBA 元组列表
    px = []
    for y in range(h):
        row = []
        for x in range(w):
            o = (y * stride) + x * bpp
            if ch == 4:
                row.append(tuple(out[o:o + 4]))
            elif ch == 3:
                row.append((out[o], out[o + 1], out[o + 2], 255))
            elif ch == 1:
                row.append((out[o], out[o], out[o], 255))
            else:
                row.append((out[o], out[o], out[o], out[o + 1]))
        px.append(row)
    return w, h, px


def write_png(path, w, h, px):
    raw = bytearray()
    for y in range(h):
        raw.append(0)                      # filter type 0
        for x in range(w):
            raw += bytes(px[y][x])
    def chunk(typ, data):
        return (struct.pack('>I', len(data)) + typ + data +
                struct.pack('>I', zlib.crc32(typ + data) & 0xffffffff))
    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    open(path, 'wb').write(png)


# ── W 键帽 ───────────────────────────────────────────────────
def dist_seg(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    t = ((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy)
    t = max(0.0, min(1.0, t))
    return math.hypot(px - (ax + vx * t), py - (ay + vy * t))


def make_w(src, dst):
    w, h, px = read_png(src)
    # ① 抹掉原来的 E：用同一行 x=17 的底色逐行填（盾牌是竖向渐变）
    for y in range(5, 44):
        base = px[y][17]
        for x in range(16, 48):
            r, g, b, a = px[y][x]
            if (r + g + b) / 3.0 < 0.60 * 255:
                px[y][x] = base

    # ② 画 W：四条线段，半径 3px（约 6px 粗，和原 E 一致）
    pts = [(20, 8), (26, 40), (32, 18), (38, 40), (44, 8)]
    ink = (41, 41, 48)
    R = 3.0
    for y in range(4, 46):
        for x in range(16, 48):
            best = min(dist_seg(x, y, *pts[i], *pts[i + 1]) for i in range(len(pts) - 1))
            if best <= R:
                px[y][x] = (*ink, 255)
            elif best <= R + 0.9:
                k = 1.0 - (best - R) / 0.9
                r, g, b, a = px[y][x]
                m = k * 0.7
                px[y][x] = (round(r + (ink[0] - r) * m),
                            round(g + (ink[1] - g) * m),
                            round(b + (ink[2] - b) * m), a)
    write_png(dst, w, h, px)
    print('W 键帽 ->', dst)


# ── 像素绳索 ─────────────────────────────────────────────────
def make_rope(dst, w=12, h=500):
    body = (200, 168, 112)
    light = (226, 200, 146)
    dark = (150, 118, 70)
    strand = (108, 82, 46)
    clear = (0, 0, 0, 0)
    px = [[clear for _ in range(w)] for _ in range(h)]

    for y in range(h):
        for x in range(2, 10):
            px[y][x] = (*body, 255)
        px[y][2] = (*dark, 255)          # 左暗边
        px[y][9] = (*dark, 255)          # 右暗边
        px[y][3] = (*light, 255)         # 左侧高光
        px[y][4] = (*light, 255)
        # 缠绕纹：每 14 行一循环，深色点逐行横移 → 斜向绳股
        ph = y % 14
        if ph < 7:
            sx = 2 + ph
            for k in range(2):
                if 2 <= sx + k <= 9:
                    px[y][sx + k] = (*strand, 255)
        else:
            sx = 9 - (ph - 7)
            for k in range(2):
                if 2 <= sx + k <= 9:
                    px[y][sx + k] = (*strand, 255)

    write_png(dst, w, h, px)
    print('绳索 ->', dst, '%dx%d' % (w, h))


if __name__ == '__main__':
    out = os.path.join(os.path.dirname(__file__), 'out')
    os.makedirs(out, exist_ok=True)
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
    make_w(os.path.join(root, 'e-marker-64.png'), os.path.join(out, 'w-marker-64.png'))
    make_rope(os.path.join(out, 'rope.png'))
