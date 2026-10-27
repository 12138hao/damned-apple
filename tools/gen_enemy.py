"""生成敌人精灵表：紫色史莱姆，4 帧 squash & stretch，32x32 每帧。

纯手写 PNG 编码，零第三方依赖。
用法：python tools/gen_enemy.py
"""
import os
import struct
import zlib

W = H = 32
FRAMES = 4

CLEAR = (0, 0, 0, 0)
BODY_DARK = (0x3B, 0x14, 0x78, 255)   # 深紫外轮廓
BODY = (0x8B, 0x5C, 0xF6, 255)        # 主体紫
BODY_HI = (0xB4, 0x9B, 0xFB, 255)     # 高光
EYE_W = (0xFF, 0xFF, 0xFF, 255)
EYE_B = (0x1A, 0x1A, 0x2E, 255)
MOUTH = (0x3B, 0x14, 0x78, 255)


def new_frame():
    return [[CLEAR for _ in range(W)] for _ in range(H)]


def ellipse(px, cx, cy, rx, ry, color):
    for y in range(H):
        for x in range(W):
            dx = (x + 0.5 - cx) / rx
            dy = (y + 0.5 - cy) / ry
            if dx * dx + dy * dy <= 1.0:
                px[y][x] = color


def put(px, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        px[y][x] = c


def slime(squash):
    """squash: 0 正常，正数压扁，负数拉高。"""
    px = new_frame()
    rx = 13.0 * (1.0 + squash * 0.13)
    ry = 12.0 * (1.0 - squash * 0.13)
    cy = 30.0 - ry

    ellipse(px, 16, cy, rx, ry, BODY_DARK)
    ellipse(px, 16, cy, rx - 1.7, ry - 1.7, BODY)
    ellipse(px, 10.5, cy - ry * 0.42, 3.0, 2.2, BODY_HI)

    ey = cy - ry * 0.10
    for ex in (11.0, 21.0):
        ellipse(px, ex, ey, 2.7, 3.1, EYE_W)
        ellipse(px, ex, ey + 0.5, 1.3, 1.7, EYE_B)

    my = int(cy + ry * 0.48)
    for dx in (-2, -1, 0, 1, 2):
        put(px, 16 + dx, my, MOUTH)
    return px


def chunk(tag, data):
    return (struct.pack('>I', len(data)) + tag + data
            + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))


def save_png(path, frames):
    w = W * len(frames)
    raw = bytearray()
    for y in range(H):
        raw.append(0)
        for f in frames:
            for x in range(W):
                raw.extend(f[y][x])
    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', w, H, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    with open(path, 'wb') as fp:
        fp.write(png)
    return w, H


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(root, 'assets')
    os.makedirs(out_dir, exist_ok=True)

    squashes = [0.0, 0.55, 0.0, -0.45]
    frames = [slime(s) for s in squashes]
    path = os.path.join(out_dir, 'enemy_sheet.png')
    w, h = save_png(path, frames)
    print('已生成：%s  (%dx%d, %d 帧)' % (path, w, h, len(frames)))

    # 放大预览，方便肉眼检查
    scale = 6
    prev_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'preview_enemy.png')
    pw = W * len(frames) * scale + len(frames) + 1
    rows = []
    for y in range(H * scale):
        row = []
        for f in frames:
            for x in range(W * scale):
                row.append(f[y // scale][x // scale])
            row.append((30, 30, 40, 255))
        rows.append(row)
    raw = bytearray()
    for row in rows:
        raw.append(0)
        for c in row:
            raw.extend(c)
    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', pw, H * scale, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    with open(prev_path, 'wb') as fp:
        fp.write(png)
    print('预览图：%s' % prev_path)


if __name__ == '__main__':
    main()
