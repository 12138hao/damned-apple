"""苹果怪 4 帧精灵表（48x48 每帧，共 192x48）。

参考图特征：
  - 圆胖苹果身，左侧偏橙、右侧深红，左上角有奶油色高光斑
  - 深棕色描边
  - 头顶棕色果梗 + 一片绿叶
  - 两只大圆眼（深色瞳孔 + 白色高光），中间两个小鼻孔
  - 咧嘴大笑：白牙一排 + 深红口腔
  - 两侧小短手，底部两只小脚

帧序：0 正常、1 压扁、2 拉伸、3 压扁（走路时上下弹）
用法：python tools/gen_apple.py [--preview]
"""
import math
import os
import struct
import sys
import zlib

W = H = 48
FRAMES = 4

CLEAR = (0, 0, 0, 0)
OUTLINE = (0x3A, 0x12, 0x0C, 255)
RED = (0xE6, 0x2A, 0x1E, 255)
RED_D = (0xB4, 0x1A, 0x14, 255)
ORANGE = (0xF2, 0x6C, 0x1E, 255)
ORANGE_L = (0xF8, 0xA4, 0x4E, 255)
CREAM = (0xFF, 0xE4, 0xB8, 255)
STEM = (0x6E, 0x3A, 0x1C, 255)
STEM_L = (0x96, 0x58, 0x2C, 255)
LEAF = (0x8E, 0xC2, 0x3A, 255)
LEAF_D = (0x5A, 0x8A, 0x22, 255)
EYE = (0x2A, 0x10, 0x0C, 255)
WHITE = (0xFF, 0xFF, 0xFF, 255)
MOUTH_IN = (0x8E, 0x1C, 0x18, 255)
TONGUE = (0xD8, 0x44, 0x38, 255)


def new_frame():
    return [[CLEAR for _ in range(W)] for _ in range(H)]


def put(px, x, y, c):
    x, y = int(x), int(y)
    if 0 <= x < W and 0 <= y < H:
        px[y][x] = c


def blob(px, cx, cy, rx, ry, c, n=2.0):
    for y in range(H):
        for x in range(W):
            dx = abs((x + 0.5 - cx) / rx)
            dy = abs((y + 0.5 - cy) / ry)
            if dx ** n + dy ** n <= 1.0:
                px[y][x] = c


def inside(cx, cy, rx, ry, x, y, n=2.0):
    dx = abs((x + 0.5 - cx) / rx)
    dy = abs((y + 0.5 - cy) / ry)
    return dx ** n + dy ** n <= 1.0


def lerp(a, b, t):
    return a + (b - a) * t


# ------------------------------------------------------------------ 部件
def body(px, cx, cy, rx, ry):
    """先描边（外扩 1 像素），再填渐变果肉。"""
    for y in range(H):
        for x in range(W):
            if inside(cx, cy, rx + 1.0, ry + 1.0, x, y, 2.15):
                px[y][x] = OUTLINE
    left = cx - rx
    right = cx + rx
    for y in range(H):
        for x in range(W):
            if not inside(cx, cy, rx, ry, x, y, 2.15):
                continue
            t = (x - left) / (right - left)
            # 左边橙、右边红，再往下压暗一点
            r = int(lerp(ORANGE[0], RED[0], t))
            g = int(lerp(ORANGE[1], RED[1], t))
            b = int(lerp(ORANGE[2], RED[2], t))
            k = (y - (cy - ry)) / (2 * ry)
            if k > 0.62:
                d = (k - 0.62) / 0.38 * 0.28
                r = int(r * (1 - d))
                g = int(g * (1 - d))
                b = int(b * (1 - d))
            px[y][x] = (r, g, b, 255)


def highlight(px, cx, cy):
    """左上角奶油色高光斑 + 几个小亮点。"""
    blob(px, cx - 8.5, cy - 8.0, 3.6, 3.0, CREAM, 2.0)
    put(px, cx - 11, cy - 2, ORANGE_L)
    put(px, cx - 10, cy - 1, ORANGE_L)
    put(px, cx - 6, cy - 12, ORANGE_L)
    put(px, cx - 5, cy - 12, ORANGE_L)


def stem(px, cx, top):
    for y in range(top, top + 7):
        put(px, cx - 1, y, STEM)
        put(px, cx, y, STEM_L)
    put(px, cx - 2, top + 1, STEM)
    put(px, cx + 1, top + 1, OUTLINE)


def leaf(px, cx, top):
    """一片向右上翘的椭圆叶子 + 深色描边。"""
    lx, ly = cx + 9.5, top + 3.2
    for y in range(H):
        for x in range(W):
            dx = (x + 0.5 - lx) / 6.8
            dy = (y + 0.5 - ly) / 3.3
            dy += (x + 0.5 - lx) * 0.055          # 让叶子斜着翘
            if dx * dx + dy * dy <= 1.0:
                px[y][x] = LEAF if dy < 0.3 else LEAF_D
    # 描边
    edge = []
    for y in range(H):
        for x in range(W):
            if px[y][x] in (LEAF, LEAF_D):
                for ddx, ddy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + ddx, y + ddy
                    if 0 <= nx < W and 0 <= ny < H and px[ny][nx] == CLEAR:
                        edge.append((nx, ny))
    for x, y in edge:
        px[y][x] = OUTLINE
    # 叶脉
    for i in range(-4, 5):
        put(px, lx + i, ly + i * 0.35, LEAF_D)


def eye(px, ex, ey, rx, ry):
    blob(px, ex, ey, rx + 0.8, ry + 0.8, OUTLINE, 2.0)
    blob(px, ex, ey, rx, ry, EYE, 2.0)
    # 左上高光
    put(px, ex - 2, ey - 3, WHITE)
    put(px, ex - 1, ey - 3, WHITE)
    put(px, ex - 2, ey - 2, WHITE)


def mouth(px, cx, cy, half_w, depth):
    """咧嘴笑：上排白牙 + 深红口腔 + 一条舌头。"""
    cy = int(cy)
    for y in range(cy, cy + depth):
        k = (y - cy) / max(1, depth - 1)
        w = half_w * math.sqrt(max(0.0, 1 - k * k * 0.92))
        for x in range(int(cx - w), int(cx + w) + 1):
            if y < cy + 2:
                c = WHITE
            elif y > cy + depth - 3:
                c = TONGUE
            else:
                c = MOUTH_IN
            put(px, x, y, c)
    # 嘴的外描边
    for x in range(int(cx - half_w), int(cx + half_w) + 1):
        t = (x - cx) / half_w
        put(px, x, cy - 1, OUTLINE)
        yy = cy + depth - 1 + int(1.2 * (1 - t * t))
        put(px, x, yy, OUTLINE)


def arm(px, side, cx, cy, swing):
    """两侧小短手：从果身两侧伸出去，画在身体之后才不会被盖掉。"""
    if side < 0:
        xs = range(int(cx) - 22, int(cx) - 12)
    else:
        xs = range(int(cx) + 12, int(cx) + 22)
    y0 = cy + 7 + swing
    for i, x in enumerate(xs):
        k = i / 9.0
        yy = y0 + int(1.1 * (1 - (k - 0.5) ** 2 * 3))
        put(px, x, yy - 1, OUTLINE)
        put(px, x, yy, ORANGE)
        put(px, x, yy + 1, ORANGE_L)
        put(px, x, yy + 2, ORANGE)
        put(px, x, yy + 3, ORANGE_L)
        put(px, x, yy + 4, OUTLINE)


def foot(px, fx, fy):
    """底部小脚：圆角方块。"""
    for y in range(fy, fy + 6):
        for x in range(fx, fx + 11):
            corner = ((x == fx or x == fx + 10) and (y == fy or y == fy + 5))
            edge = (x == fx or x == fx + 10 or y == fy or y == fy + 5)
            put(px, x, y, CLEAR if corner else (OUTLINE if edge else ORANGE))
    for x in range(fx + 2, fx + 9):
        put(px, x, fy + 2, ORANGE_L)
    for x in range(fx + 3, fx + 8):
        put(px, x, fy + 4, ORANGE_L)


# ------------------------------------------------------------------ 帧
def draw(squash=0.0, arm_swing=0, lift=0):
    """squash: -1 拉长 / 0 正常 / +1 压扁。lift: 整体上移像素。"""
    px = new_frame()
    cx = 24.0
    cy = 26.5 + lift + squash * 1.2
    rx = 16.5 + squash * 1.2
    ry = 15.5 - squash * 1.6

    # 脚先画（会被身体压住一部分）
    foot(px, 10, 40 + lift)
    foot(px, 27, 40 + lift)

    # 果身
    body(px, cx, cy, rx, ry)
    highlight(px, cx, cy)

    # 手（画在身体之后，才不会被子盖住）
    arm(px, -1, cx, cy, arm_swing)
    arm(px, 1, cx, cy, arm_swing)

    # 果梗 + 叶子
    stem(px, cx, int(cy - ry - 5))
    leaf(px, cx, int(cy - ry - 4))

    # 眼睛
    eye(px, cx - 9.0, cy - 4.0, 4.0, 4.6)
    eye(px, cx + 9.0, cy - 4.0, 4.0, 4.6)

    # 鼻孔
    put(px, cx - 2, cy + 1, OUTLINE)
    put(px, cx + 2, cy + 1, OUTLINE)

    # 嘴
    mouth(px, cx, cy + 5, 10, 8)

    return px


def encode_png(w, h, rows):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        raw.extend(rows[y])

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data
                + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))

    return (b'\x89PNG\r\n\x1a\n'
            + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
            + chunk(b'IEND', b''))


def save_sheet(frames, path):
    w = W * len(frames)
    rows = []
    for y in range(H):
        row = bytearray()
        for f in frames:
            for x in range(W):
                row.extend(f[y][x])
        rows.append(row)
    with open(path, 'wb') as fp:
        fp.write(encode_png(w, H, rows))
    return w, H


def save_scaled(frames, path, scale=8, pad=6, bg=(0x2A, 0x2E, 0x3A, 255)):
    w = W * len(frames)
    cw, ch = (w + pad * 2) * scale, (H + pad * 2) * scale
    rows = []
    for y in range(ch):
        sy = y // scale - pad
        row = bytearray()
        for x in range(cw):
            sx = x // scale - pad
            if 0 <= sx < w and 0 <= sy < H:
                px = frames[sx // W][sy][sx % W]
                row.extend(bg if px[3] == 0 else px)
            else:
                row.extend(bg)
        rows.append(row)
    with open(path, 'wb') as fp:
        fp.write(encode_png(cw, ch, rows))
    return cw, ch


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(root, 'assets')
    tools_dir = os.path.join(root, 'tools')
    os.makedirs(out_dir, exist_ok=True)
    preview_only = '--preview' in sys.argv[1:]

    frames = [
        draw(squash=0.0, arm_swing=0, lift=0),
        draw(squash=1.0, arm_swing=1, lift=0),
        draw(squash=-1.0, arm_swing=-1, lift=-2),
        draw(squash=1.0, arm_swing=1, lift=0),
    ]

    if preview_only:
        path = os.path.join(tools_dir, 'preview_apple_sheet.png')
    else:
        path = os.path.join(out_dir, 'enemy_sheet.png')
    sw, sh = save_sheet(frames, path)
    pw, ph = save_scaled(frames, os.path.join(tools_dir, 'preview_apple.png'))
    print('精灵表：%s  (%dx%d, %d 帧)' % (path, sw, sh, len(frames)))
    print('预览图：tools/preview_apple.png  (%dx%d)' % (pw, ph))

    for i, f in enumerate(frames):
        xs = [x for y in range(H) for x in range(W) if f[y][x] != CLEAR]
        ys = [y for y in range(H) for x in range(W) if f[y][x] != CLEAR]
        if xs:
            print('  帧%d 包围盒 x %d..%d  y %d..%d' % (i, min(xs), max(xs), min(ys), max(ys)))

    table = {CLEAR: '.', OUTLINE: '#', RED: 'R', RED_D: 'r', ORANGE: 'o',
             ORANGE_L: 'O', CREAM: 'C', STEM: 'S', STEM_L: 's', LEAF: 'L',
             LEAF_D: 'l', EYE: 'e', WHITE: 'W', MOUTH_IN: 'm', TONGUE: 't'}
    print('\n--- 帧 0 ---')
    for y in range(H):
        print(''.join(table.get(frames[0][y][x], '?') for x in range(W)).rstrip())


if __name__ == '__main__':
    main()
