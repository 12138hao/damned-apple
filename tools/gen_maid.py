"""女仆猫耳角色 8 帧精灵表（48x48 每帧）。

参考：黑短袖女仆装 + 白领 + 白围裙 + 白丝 + 棕玛丽珍鞋 + 红项圈 + 猫耳 + 双马尾。
帧序：0/1 站立、2~5 跑步、6 跳跃、7 下落

用法：python tools/gen_maid.py
"""
import os
import struct
import zlib

W = H = 48
FRAMES = 8

CLEAR = (0, 0, 0, 0)
EAR = (0x8B, 0x6B, 0x4A, 255)
EAR_IN = (0xE8, 0xA6, 0xA6, 255)
HAIR = (0x18, 0x18, 0x22, 255)
HAIR_HI = (0x3E, 0x3E, 0x56, 255)
SKIN = (0xF6, 0xD2, 0xB2, 255)
SKIN_SH = (0xDC, 0xAA, 0x88, 255)
EYE = (0x24, 0x16, 0x2C, 255)
EYE_HI = (0xFF, 0xFF, 0xFF, 255)
BLUSH = (0xEE, 0x9C, 0x9C, 255)
CHOKER = (0xC2, 0x32, 0x32, 255)
BELL = (0xF2, 0xC4, 0x1E, 255)
DRESS = (0x20, 0x20, 0x2C, 255)
DRESS_HI = (0x34, 0x34, 0x46, 255)
WHITE = (0xF8, 0xF8, 0xFA, 255)
WHITE_SH = (0xCC, 0xCC, 0xDA, 255)
SHOE = (0x6B, 0x44, 0x23, 255)
SHOE_HI = (0x94, 0x64, 0x3A, 255)


def new_frame():
    return [[CLEAR for _ in range(W)] for _ in range(H)]


def put(px, x, y, c):
    x, y = int(x), int(y)
    if 0 <= x < W and 0 <= y < H:
        px[y][x] = c


def rect(px, x1, y1, x2, y2, c):
    for y in range(int(y1), int(y2) + 1):
        for x in range(int(x1), int(x2) + 1):
            put(px, x, y, c)


def ellipse(px, cx, cy, rx, ry, c):
    for y in range(H):
        for x in range(W):
            dx = (x + 0.5 - cx) / rx
            dy = (y + 0.5 - cy) / ry
            if dx * dx + dy * dy <= 1.0:
                px[y][x] = c


# ------------------------------------------------------------------ 部件
def ponytail(px, side, oy, swing):
    """side -1 左 / +1 右。从耳后垂下，末端外飘。"""
    for i in range(20):
        y = 13 + i + oy
        w = 4 if i < 8 else 3
        drift = (i // 8) * side + int(swing * (i / 19.0) * 2)
        x0 = 24 + side * 10 + drift
        if side < 0:
            rect(px, x0 - w + 1, y, x0, y, HAIR)
        else:
            rect(px, x0, y, x0 + w - 1, y, HAIR)
    put(px, 24 + side * 11 + side * 2, 30 + oy, HAIR_HI)


def leg(px, x0, oy, length=5):
    """白丝长筒袜 + 玛丽珍鞋，宽 5。length 控制腿长（跳跃时缩短 = 收腿）。"""
    top = 41 + oy
    rect(px, x0, top, x0 + 4, top + length - 1, WHITE)
    rect(px, x0, top, x0 + 4, top, WHITE_SH)
    put(px, x0 + 2, top + 1, HAIR)                   # 袜口小蝴蝶结
    sy = top + length
    rect(px, x0 - 1, sy, x0 + 4, sy + 1, SHOE)
    rect(px, x0 - 1, sy, x0 + 4, sy, SHOE_HI)


def skirt(px, oy):
    for i in range(9):
        y = 32 + i + oy
        half = 5 + i
        rect(px, 24 - half, y, 24 + half, y, DRESS)
        put(px, 24 - half, y, DRESS_HI)
    rect(px, 24 - 13, 40 + oy, 24 + 13, 40 + oy, WHITE_SH)   # 裙摆蕾丝


def torso(px, oy):
    rect(px, 19, 23 + oy, 29, 31 + oy, DRESS)
    rect(px, 19, 23 + oy, 29, 24 + oy, WHITE)         # 白领
    rect(px, 20, 26 + oy, 28, 33 + oy, WHITE)         # 围裙
    rect(px, 20, 26 + oy, 28, 26 + oy, WHITE_SH)
    put(px, 23, 31 + oy, WHITE_SH)
    put(px, 25, 31 + oy, WHITE_SH)
    # 胸前蝴蝶结
    rect(px, 22, 25 + oy, 26, 25 + oy, WHITE)
    put(px, 21, 24 + oy, WHITE)
    put(px, 27, 24 + oy, WHITE)
    put(px, 24, 25 + oy, WHITE_SH)


def arm(px, side, oy, swing):
    x0 = 16 if side < 0 else 30
    y0 = 25 + oy + swing
    rect(px, x0, y0, x0 + 2, y0 + 5, DRESS)
    put(px, x0, y0, DRESS_HI)
    rect(px, x0, y0 + 4, x0 + 2, y0 + 4, WHITE)       # 白袖口
    rect(px, x0, y0 + 5, x0 + 2, y0 + 6, SKIN)        # 手


def head(px, oy):
    # 脖子 + 红项圈 + 铃铛
    rect(px, 22, 20 + oy, 26, 22 + oy, SKIN_SH)
    # 后脑头发
    ellipse(px, 24, 15.5 + oy, 7.6, 6.8, HAIR)
    # 脸
    ellipse(px, 24, 17 + oy, 6.0, 6.0, SKIN)
    # 刘海（参差三档）
    for x in range(17, 32):
        h = 11 + (x % 3)
        rect(px, x, 10 + oy, x, h + oy, HAIR)
    # 鬓发
    rect(px, 17, 12 + oy, 18, 20 + oy, HAIR)
    rect(px, 30, 12 + oy, 31, 20 + oy, HAIR)
    # 眼睛（2 宽 3 高 + 高光）
    rect(px, 20, 16 + oy, 21, 18 + oy, EYE)
    rect(px, 27, 16 + oy, 28, 18 + oy, EYE)
    put(px, 20, 16 + oy, EYE_HI)
    put(px, 27, 16 + oy, EYE_HI)
    # 腮红 + 嘴
    put(px, 18, 19 + oy, BLUSH)
    put(px, 30, 19 + oy, BLUSH)
    put(px, 24, 20 + oy, (0xC0, 0x6E, 0x72, 255))
    # 猫耳（直角三角形，尖朝上内收）
    for i in range(6):
        y = 5 + i + oy
        w = i + 1
        rect(px, 20 - w, y, 20, y, EAR)
        rect(px, 28, y, 28 + w, y, EAR)
    for i in range(2):
        y = 9 + i + oy
        rect(px, 19 - i, y, 20, y, EAR_IN)
        rect(px, 28, y, 29 + i, y, EAR_IN)
    # 项圈最后画，压在脸的下缘
    rect(px, 21, 21 + oy, 27, 22 + oy, CHOKER)
    rect(px, 21, 21 + oy, 27, 21 + oy, (0xD8, 0x48, 0x48, 255))
    put(px, 24, 22 + oy, BELL)


# ------------------------------------------------------------------ 帧
def draw(mode="idle", legphase=0, bob=0, swing=0):
    px = new_frame()
    oy = bob

    ponytail(px, -1, oy, swing)
    ponytail(px, 1, oy, swing)

    if mode == "jump":
        leg(px, 18, oy, length=3)
        leg(px, 26, oy, length=3)
    elif mode == "fall":
        leg(px, 16, oy, length=4)
        leg(px, 28, oy, length=4)
    else:
        lx, rx = {1: (18, 27), 0: (19, 26), -1: (20, 25)}[legphase]
        leg(px, lx, oy)
        leg(px, rx, oy)

    skirt(px, oy)
    torso(px, oy)

    if mode == "jump":
        arm(px, -1, oy, -3)
        arm(px, 1, oy, -3)
    elif mode == "fall":
        arm(px, -1, oy, 1)
        arm(px, 1, oy, 1)
    else:
        arm(px, -1, oy, swing)
        arm(px, 1, oy, -swing)

    head(px, oy)
    return px


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(root, 'assets')
    os.makedirs(out_dir, exist_ok=True)

    frames = [
        draw("idle", 0, 0, 0),
        draw("idle", 0, 1, 0),
        draw("run", 1, 0, 1),
        draw("run", 0, -1, 0),
        draw("run", -1, 0, -1),
        draw("run", 0, -1, 0),
        draw("jump", 0, 0, 0),
        draw("fall", 0, 0, 0),
    ]

    w = W * len(frames)
    raw = bytearray()
    for y in range(H):
        raw.append(0)
        for f in frames:
            for x in range(W):
                raw.extend(f[y][x])

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data
                + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', w, H, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    path = os.path.join(out_dir, 'player_sheet.png')
    with open(path, 'wb') as fp:
        fp.write(png)
    print('已生成：%s  (%dx%d, %d 帧)' % (path, w, H, len(frames)))

    table = {CLEAR: '.', HAIR: '#', HAIR_HI: '+', SKIN: 's', SKIN_SH: 's',
             EYE: 'o', EYE_HI: 'O', WHITE: 'W', WHITE_SH: 'w',
             DRESS: '#', DRESS_HI: '+', SHOE: 'b', SHOE_HI: 'b',
             EAR: 'b', EAR_IN: 'p', CHOKER: 'r', BELL: 'y', BLUSH: 'p',
             (0xD8, 0x48, 0x48, 255): 'r', (0xC0, 0x6E, 0x72, 255): 'p'}
    for idx in (0, 2, 6):
        print('\n--- 帧 %d ---' % idx)
        for y in range(H):
            line = ''.join(table.get(frames[idx][y][x], '?') for x in range(W))
            print(line.rstrip())


if __name__ == '__main__':
    main()
