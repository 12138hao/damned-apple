"""生成第 2 关贴图素材：地面砖 + 天空背景。

纯手写 PNG 编码，零第三方依赖。
用法：python tools/gen_art.py
"""
import math
import os
import random
import struct
import zlib


def chunk(tag, data):
    return (struct.pack('>I', len(data)) + tag + data
            + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))


def write_png(path, rows, w, h):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        raw.extend(rows[y])
    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    with open(path, 'wb') as fp:
        fp.write(png)
    return len(png)


# ---------------------------------------------------------------- 地面砖
def ground_tile(w=32, h=40):
    """32 宽 x 40 高，横向可无缝平铺（左右边缘不画跨界噪点）。"""
    GRASS_LIT = (0x6C, 0xC8, 0x5A, 255)
    GRASS = (0x4C, 0xA8, 0x42, 255)
    GRASS_DARK = (0x36, 0x82, 0x30, 255)
    DIRT = (0x7A, 0x52, 0x30, 255)
    DIRT_DARK = (0x5C, 0x3D, 0x24, 255)
    DIRT_LIT = (0x96, 0x6B, 0x45, 255)
    STONE = (0x8B, 0x6B, 0x4A, 255)

    rows = []
    for y in range(h):
        row = bytearray()
        for x in range(w):
            if y == 0:
                c = GRASS_LIT
            elif y <= 6:
                c = GRASS
            elif y <= 8:
                c = GRASS_DARK
            elif y <= 10:
                c = DIRT_DARK
            else:
                c = DIRT
            row.extend(c)
        rows.append(row)

    # 噪点：确定性伪随机，避开左右各 1 列以保证平铺无缝
    rnd = random.Random(20261021)
    for _ in range(70):
        x = rnd.randint(1, w - 2)
        y = rnd.randint(11, h - 1)
        c = STONE if rnd.random() < 0.45 else DIRT_DARK
        rows[y][x * 4:x * 4 + 4] = bytes(c)

    # 草根不规则下沿
    for x in range(1, w - 1):
        if rnd.random() < 0.35:
            rows[9][x * 4:x * 4 + 4] = bytes(GRASS_DARK)

    # 顶部草叶尖
    for x in range(1, w - 1, 3):
        if rnd.random() < 0.5:
            rows[0][x * 4:x * 4 + 4] = bytes((0x8A, 0xDE, 0x74, 255))

    # 少量泥土高光
    for _ in range(18):
        x = rnd.randint(1, w - 2)
        y = rnd.randint(12, h - 2)
        rows[y][x * 4:x * 4 + 4] = bytes(DIRT_LIT)

    return rows, w, h


# ---------------------------------------------------------------- 天空
def sky(w=3200, h=648):
    C_TOP = (0x0F, 0x14, 0x28)
    C_MID = (0x2A, 0x3A, 0x6A)
    C_BOT = (0x5A, 0x6A, 0x9A)
    MOUNT_FAR = (0x1E, 0x2A, 0x4E, 255)
    MOUNT_NEAR = (0x14, 0x1C, 0x36, 255)

    def lerp(a, b, t):
        return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))

    rows = []
    for y in range(h):
        t = y / (h - 1)
        if t < 0.62:
            c = lerp(C_TOP, C_MID, t / 0.62)
        else:
            c = lerp(C_MID, C_BOT, (t - 0.62) / 0.38)
        rows.append(bytearray(bytes((c[0], c[1], c[2], 255)) * w))

    # 星星
    rnd = random.Random(7777)
    for _ in range(260):
        x = rnd.randint(0, w - 1)
        y = rnd.randint(0, int(h * 0.55))
        b = rnd.randint(140, 255)
        rows[y][x * 4:x * 4 + 4] = bytes((b, b, min(255, b + 10), 255))

    # 远山（高）
    for x in range(w):
        top = int(430 + 46 * math.sin(x / 300.0) + 22 * math.sin(x / 97.0 + 1.1))
        top = max(300, min(h - 1, top))
        for y in range(top, h):
            rows[y][x * 4:x * 4 + 4] = bytes(MOUNT_FAR)

    # 近山（低）
    for x in range(w):
        top = int(500 + 34 * math.sin(x / 190.0 + 2.2) + 16 * math.sin(x / 63.0 + 0.4))
        top = max(380, min(h - 1, top))
        for y in range(top, h):
            rows[y][x * 4:x * 4 + 4] = bytes(MOUNT_NEAR)

    return rows, w, h


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(root, 'assets')
    os.makedirs(out, exist_ok=True)

    rows, w, h = ground_tile()
    size = write_png(os.path.join(out, 'ground_tile.png'), rows, w, h)
    print('ground_tile.png  %dx%d  %.1f KB' % (w, h, size / 1024.0))

    rows, w, h = sky()
    size = write_png(os.path.join(out, 'sky.png'), rows, w, h)
    print('sky.png          %dx%d  %.1f KB' % (w, h, size / 1024.0))


if __name__ == '__main__':
    main()
