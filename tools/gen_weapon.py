"""武器贴图：棒球棍（32x32，斜放，握把在左下、棒头在右上）。

用法：python tools/gen_weapon.py
输出：assets/weapon.png
"""
import math
import os
import struct
import zlib

W = H = 48          # 放大到 1.5 倍（原 32）

CLEAR = (0, 0, 0, 0)
EDGE = (0x38, 0x22, 0x14, 255)       # 描边
GRIP = (0x6B, 0x44, 0x23, 255)       # 握把
GRIP_HI = (0x8E, 0x60, 0x36, 255)
BAT = (0xC8, 0x9A, 0x5A, 255)        # 棒身
BAT_HI = (0xE8, 0xC8, 0x8A, 255)
BAT_SH = (0x9A, 0x6E, 0x38, 255)


def new_img():
    return [[CLEAR for _ in range(W)] for _ in range(H)]


def disc(px, cx, cy, r, c):
    for y in range(H):
        for x in range(W):
            if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                px[y][x] = c


def put(px, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        px[y][x] = c


def radius(k):
    """握把段（前 30%）保持细，之后快速变粗成棒身。已按 1.5 倍放大。"""
    if k < 0.30:
        return 1.6
    return 1.6 + 3.7 * (k - 0.30) / 0.70


def draw_bat(px):
    """握把端 (7, 25) → 棒头 (25, 7)。"""
    x0, y0 = 10.5, 37.5
    x1, y1 = 37.5, 10.5
    steps = 110

    # 1) 描边：整体外扩 1 像素
    for i in range(steps + 1):
        k = i / steps
        cx = x0 + (x1 - x0) * k
        cy = y0 + (y1 - y0) * k
        disc(px, cx, cy, radius(k) + 1.5, EDGE)

    # 2) 本体
    for i in range(steps + 1):
        k = i / steps
        cx = x0 + (x1 - x0) * k
        cy = y0 + (y1 - y0) * k
        c = GRIP if k < 0.30 else BAT
        disc(px, cx, cy, radius(k), c)

    # 3) 高光：沿左上侧一条
    for i in range(steps + 1):
        k = i / steps
        cx = x0 + (x1 - x0) * k
        cy = y0 + (y1 - y0) * k
        r = radius(k)
        c = GRIP_HI if k < 0.30 else BAT_HI
        disc(px, cx - r * 0.45, cy - r * 0.45, max(r * 0.42, 0.5), c)

    # 4) 棒头右下加深
    for i in range(int(steps * 0.85), steps + 1):
        k = i / steps
        cx = x0 + (x1 - x0) * k
        cy = y0 + (y1 - y0) * k
        r = radius(k)
        disc(px, cx + r * 0.5, cy + r * 0.5, max(r * 0.38, 0.5), BAT_SH)

    # 5) 握把底端圆头
    disc(px, x0 - 0.9, y0 + 0.9, 2.7, EDGE)
    disc(px, x0 - 0.9, y0 + 0.9, 1.7, GRIP)


def save(img, path):
    raw = bytearray()
    for y in range(H):
        raw.append(0)
        for x in range(W):
            raw.extend(img[y][x])

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data
                + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    with open(path, 'wb') as fp:
        fp.write(png)


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    img = new_img()
    draw_bat(img)
    path = os.path.join(root, 'assets', 'weapon.png')
    save(img, path)

    # 顺手存一张放大预览
    S = 12
    out = bytearray()
    bg = (0x2A, 0x2E, 0x3A, 255)
    for y in range(H * S):
        out.append(0)
        for x in range(W * S):
            px = img[y // S][x // S]
            out.extend(bg if px[3] == 0 else px)

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data
                + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', W * S, H * S, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(out), 9))
           + chunk(b'IEND', b''))
    with open(os.path.join(root, 'tools', 'preview_weapon.png'), 'wb') as fp:
        fp.write(png)
    print('已生成：assets/weapon.png  (%dx%d)' % (W, H))


if __name__ == '__main__':
    main()
