# -*- coding: utf-8 -*-
"""生成主菜单背景 800x450：夜空 + 月亮 + 远山 + 左侧房子剪影 + 右侧苹果树剪影。
纯 stdlib，复用 gen_rope_assets.py 的 write_png（项目里没有 PIL）。
程序绘制，非模型生成。"""
import os, sys, math, random
sys.path.insert(0, os.path.dirname(__file__))
from gen_rope_assets import write_png

W, H = 800, 450
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


px = [[(0, 0, 0, 255) for _ in range(W)] for _ in range(H)]

# ── 1. 夜空渐变（顶部深、靠近地平线略亮）──
TOP = (10, 14, 26)
HORIZON = (48, 60, 88)
for y in range(H):
    t = min(1.0, y / 390.0) ** 1.7
    c = lerp(TOP, HORIZON, t)
    row = [(*c, 255)] * W
    px[y] = row

# ── 2. 星星 ──
random.seed(20261103)
for _ in range(170):
    x = random.randint(0, W - 1)
    y = random.randint(0, 310)
    if y > 250 and random.random() < 0.75:
        continue
    b = random.randint(130, 255)
    px[y][x] = (b, b, min(255, b + 18), 255)
    if random.random() < 0.10:
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < W and 0 <= ny < H:
                px[ny][nx] = (b // 2, b // 2, b // 2, 255)

# ── 3. 月亮（带一圈晕）──
MX, MY, MR = 648, 76, 25
for y in range(MY - MR - 16, MY + MR + 16):
    for x in range(MX - MR - 16, MX + MR + 16):
        if not (0 <= x < W and 0 <= y < H):
            continue
        d = math.hypot(x - MX, y - MY)
        if d <= MR:
            v = 1.0
            if math.hypot(x - MX + 7, y - MY - 6) < 6:
                v = 0.93
            if math.hypot(x - MX - 8, y - MY + 8) < 4:
                v = 0.96
            px[y][x] = (int(236 * v), int(231 * v), int(206 * v), 255)
        elif d <= MR + 14:
            a = 1 - (d - MR) / 14
            old = px[y][x]
            px[y][x] = (min(255, old[0] + int(64 * a)),
                        min(255, old[1] + int(62 * a)),
                        min(255, old[2] + int(40 * a)), 255)

# ── 4. 远山（两层剪影）──
def mountain(base_y, amp, freq, phase, color):
    for x in range(W):
        h = amp * (math.sin(x * freq + phase) * 0.62
                   + math.sin(x * freq * 2.3 + phase * 1.7) * 0.38)
        top = int(base_y - abs(h))
        for y in range(max(0, top), base_y):
            px[y][x] = (*color, 255)


mountain(392, 44, 0.0062, 0.5, (29, 37, 57))
mountain(396, 28, 0.0115, 2.1, (21, 27, 43))

# ── 5. 地面 ──
GROUND_Y = 391
for y in range(GROUND_Y, H):
    px[y] = [(7, 10, 16, 255)] * W

# 地面上的草叶（随机小竖线）
random.seed(7788)
for _ in range(240):
    x = random.randint(0, W - 1)
    hgt = random.randint(3, 9)
    for k in range(hgt):
        yy = GROUND_Y - k
        if 0 <= yy < H:
            px[yy][x] = (10, 15, 22, 255)

# ── 6. 左侧房子剪影（呼应第一关）──
HOUSE_X, HOUSE_W = 92, 118
HOUSE_TOP = 300
HOUSE_BOTTOM = GROUND_Y + 2
# 墙体
for y in range(HOUSE_TOP, HOUSE_BOTTOM):
    for x in range(HOUSE_X - HOUSE_W // 2, HOUSE_X + HOUSE_W // 2):
        if 0 <= x < W:
            px[y][x] = (13, 17, 27, 255)
# 屋顶（三角）
ROOF_H = 52
for i in range(ROOF_H):
    y = HOUSE_TOP - ROOF_H + i
    half = int((HOUSE_W // 2 + 10) * (i + 1) / ROOF_H)
    for x in range(HOUSE_X - half, HOUSE_X + half):
        if 0 <= x < W and 0 <= y < H:
            px[y][x] = (11, 14, 23, 255)
# 窗户透出的暖光
for wy in range(320, 340):
    for wx in range(HOUSE_X - 26, HOUSE_X - 8):
        px[wy][wx] = (196, 148, 68, 255)
for wy in range(320, 340):
    for wx in range(HOUSE_X + 10, HOUSE_X + 28):
        px[wy][wx] = (196, 148, 68, 255)
# 窗框十字
for wy in range(320, 340):
    px[wy][HOUSE_X - 17] = (13, 17, 27, 255)
for wx in range(HOUSE_X - 26, HOUSE_X - 8):
    px[330][wx] = (13, 17, 27, 255)

# ── 7. 右侧苹果树剪影 ──
TREE_X = 690
# 树干（带轻微弯曲）
for y in range(196, GROUND_Y + 2):
    wob = int(4 * math.sin((y - 196) * 0.035))
    for x in range(TREE_X - 10 + wob, TREE_X + 11 + wob):
        if 0 <= x < W:
            px[y][x] = (8, 11, 18, 255)
# 分叉的枝
BRANCHES = [((700, 250), (760, 214), 5), ((686, 268), (626, 226), 5),
            ((704, 300), (762, 282), 4), ((680, 318), (622, 296), 4)]


def thick_line(a, b, r, color):
    ax, ay = a
    bx, by = b
    steps = int(max(abs(bx - ax), abs(by - ay))) + 1
    for s in range(steps + 1):
        t = s / steps
        cx = int(ax + (bx - ax) * t)
        cy = int(ay + (by - ay) * t)
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r:
                    x, y = cx + dx, cy + dy
                    if 0 <= x < W and 0 <= y < H:
                        px[y][x] = (*color, 255)


for a, b, r in BRANCHES:
    thick_line(a, b, r, (8, 11, 18))

# 树冠：几个圆叠加
CANOPY = [((690, 180), 66), ((628, 208), 48), ((752, 206), 46),
          ((660, 148), 42), ((722, 150), 40), ((690, 118), 34)]
for (cx, cy), r in CANOPY:
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            if not (0 <= x < W and 0 <= y < H):
                continue
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                # 树冠内部做一点点层次：越靠上越深
                v = 9 if y > cy - r * 0.4 else 7
                px[y][x] = (v, v + 3, v + 8, 255)

# 树冠上的苹果（暗红，只有几个，别太显眼）
APPLES = [(672, 152), (712, 176), (638, 196), (748, 200), (690, 214), (600, 222)]
for ax, ay in APPLES:
    for dy in range(-4, 5):
        for dx in range(-4, 5):
            if dx * dx + dy * dy <= 16:
                x, y = ax + dx, ay + dy
                if 0 <= x < W and 0 <= y < H:
                    hi = 1.0 if (dx + dy) < -1 else 0.72
                    px[y][x] = (int(138 * hi), int(34 * hi), int(34 * hi), 255)
    # 高光
    px[ay - 2][ax - 2] = (196, 92, 78, 255)

# ── 8. 左下角飘落的叶子（点缀，暗示“苹果被甩下人间”）──
random.seed(4242)
for _ in range(14):
    lx = random.randint(20, 560)
    ly = random.randint(120, 360)
    col = random.choice([(120, 96, 44), (96, 74, 34), (138, 110, 52)])
    for dy in range(-1, 2):
        for dx in range(-2, 3):
            x, y = lx + dx, ly + dy
            if 0 <= x < W and 0 <= y < H:
                px[y][x] = (*col, 255)

out = os.path.join(ROOT, 'assets', 'ui', 'mainmenu_bg.png')
write_png(out, W, H, px)
print('已生成', out, W, 'x', H)
