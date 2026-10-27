"""Q 版校服男生 8 帧精灵表（64x64 每帧，共 512x64）。

参考特征（按真人照片还原）：
  - 黑色短发，齐刘海参差不齐，两侧露出耳朵（不是头发）
  - 细圆框眼镜，镜片几乎透明，能看到眼睛
  - 白色 polo 衫 + 深蓝短袖
  - 领子从上到下三层：白 / 黑 / 红
  - 领口 V 形开口，深蓝门襟 + 两颗扣子
  - 左胸一枚小红点校徽
  - 深蓝运动裤 + 外侧白侧条
  - 人中两侧一点点灰色小胡子
比例：Q 版，头约占全身一半，脖子极短，腿尽量长。

帧序：0/1 站立、2~5 跑步、6 跳跃、7 下落、8 躺平（死亡）、9 天使（复活）
用法：python tools/gen_student.py            → 写入 assets/player_sheet.png
      python tools/gen_student.py --preview  → 只写 tools/preview_*.png
"""
import math
import os
import struct
import sys
import zlib

W = H = 64
FRAMES = 16

CLEAR = (0, 0, 0, 0)

# ---- 头发 ----
HAIR = (0x1C, 0x1C, 0x24, 255)
HAIR_HI = (0x3A, 0x3C, 0x4C, 255)

# ---- 皮肤 ----
SKIN = (0xF4, 0xD0, 0xB2, 255)
SKIN_SH = (0xD9, 0xA8, 0x88, 255)

# ---- 五官 ----
EYE = (0x22, 0x18, 0x26, 255)
EYE_HI = (0xFF, 0xFF, 0xFF, 255)
FRAME = (0x2E, 0x28, 0x24, 255)      # 细圆镜框
LENS = (0xF8, 0xDC, 0xC2, 255)       # 镜片（几乎透明）
LENS_HI = (0xFF, 0xFF, 0xFF, 255)
MOUTH = (0xC0, 0x74, 0x70, 255)
STACHE = (0x9C, 0x8A, 0x84, 255)     # 人中两侧的灰胡子

# ---- 衣服 ----
SHIRT = (0xF8, 0xF8, 0xFC, 255)
SHIRT_SH = (0xD6, 0xDA, 0xE4, 255)
NAVY = (0x22, 0x2A, 0x44, 255)       # 短袖 / 门襟
NAVY_HI = (0x38, 0x44, 0x66, 255)
BLACK = (0x12, 0x12, 0x18, 255)      # 领子中间那层
RED = (0xC4, 0x24, 0x30, 255)        # 领子下层 + 校徽
RED_HI = (0xE0, 0x4A, 0x50, 255)
BUTTON = (0xE8, 0xEA, 0xF0, 255)

# ---- 裤子 / 鞋 ----
PANTS = (0x20, 0x26, 0x3A, 255)
PANTS_HI = (0x33, 0x3C, 0x58, 255)
PANTS_SH = (0x16, 0x1A, 0x2A, 255)
STRIPE = (0xF2, 0xF4, 0xFA, 255)
SHOE = (0xDE, 0xE4, 0xEE, 255)       # 鞋面（浅灰蓝，和白裤子/深裤子都能分开）
SHOE_HI = (0xFF, 0xFF, 0xFF, 255)    # 鞋头高光
LACE = (0x8C, 0x94, 0xA6, 255)       # 鞋带
HEEL = (0xFF, 0xFF, 0xFF, 255)       # 后跟护片（白色，和鞋头、中底连成一体）
MIDSOLE = (0xFC, 0xFC, 0xFE, 255)    # 白色中底
OUTSOLE = (0x9C, 0xA4, 0xB4, 255)    # 灰色大底
PINK = (0xF0, 0x8C, 0xB4, 255)       # 手环
PINK_HI = (0xFF, 0xB8, 0xD0, 255)

# ---- 死亡 / 天使 ----
SKIN_PALE = (0xE4, 0xC4, 0xAC, 255)  # 奄奄一息的苍白脸色
WING = (0xFA, 0xFD, 0xFF, 255)       # 翅膀主色（近白）
WING_SH = (0xC4, 0xDC, 0xF4, 255)    # 翅膀阴影（淡蓝）
WING_EDGE = (0x8C, 0xAE, 0xD4, 255)  # 翅膀边缘
HALO = (0xFF, 0xD8, 0x4C, 255)       # 光环
HALO_HI = (0xFF, 0xF4, 0xB0, 255)    # 光环高光


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


def blob(px, cx, cy, rx, ry, c, n=2.6, ymax=None):
    """超椭圆（n=2 为普通椭圆，n 越大越接近圆角矩形）。
    ymax(x) 返回该列允许绘制的最大 y，用来切出发际线。"""
    for y in range(H):
        for x in range(W):
            dx = abs((x + 0.5 - cx) / rx)
            dy = abs((y + 0.5 - cy) / ry)
            if dx ** n + dy ** n > 1.0:
                continue
            if ymax is not None and y > ymax(x):
                continue
            px[y][x] = c


def oval(px, cx, cy, rx, ry, c):
    for y in range(H):
        for x in range(W):
            dx = (x + 0.5 - cx) / rx
            dy = (y + 0.5 - cy) / ry
            if dx * dx + dy * dy <= 1.0:
                px[y][x] = c


def oval_rot(px, cx, cy, rx, ry, deg, c):
    """旋转椭圆。deg 为逆时针角度（0 = 横躺，90 = 竖立）。"""
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    for y in range(H):
        for x in range(W):
            dx = x + 0.5 - cx
            dy = y + 0.5 - cy
            u = dx * ca + dy * sa
            v = -dx * sa + dy * ca
            if (u / rx) ** 2 + (v / ry) ** 2 <= 1.0:
                px[y][x] = c


def ring(px, cx, cy, rx, ry, thick, c):
    """空心椭圆环：外圈填 c，内圈挖空（只对已经画过的像素挖，避免擦掉背景）。"""
    for y in range(H):
        for x in range(W):
            dx = (x + 0.5 - cx) / rx
            dy = (y + 0.5 - cy) / ry
            d = dx * dx + dy * dy
            ix = (x + 0.5 - cx) / max(rx - thick, 0.6)
            iy = (y + 0.5 - cy) / max(ry - thick, 0.6)
            if d <= 1.0 and ix * ix + iy * iy > 1.0:
                px[y][x] = c


# ------------------------------------------------------------------ 腿
def leg(px, x0, oy, length=8, stripe=-1):
    """深蓝运动裤腿（外侧白条）+ 运动鞋。x0 左边缘，宽 8。"""
    top = 47 + oy
    bot = top + length - 1
    rect(px, x0, top, x0 + 7, bot, PANTS)
    # 内侧压暗，两条腿才分得开
    ix = x0 + 7 if stripe < 0 else x0
    rect(px, ix, top, ix, bot, PANTS_SH)
    # 外侧白侧条（一直通到裤脚）
    sx = x0 if stripe < 0 else x0 + 7
    rect(px, sx, top, sx, bot, STRIPE)
    # 裤脚收口
    rect(px, x0 + 1, bot, x0 + 6, bot, PANTS_SH)

    # ---- 运动鞋：白色鞋头 + 浅灰蓝鞋面 + 灰鞋带 + 藏青后跟 + 白中底 + 灰大底 ----
    s1, s2 = (x0 - 1, x0 + 7) if stripe < 0 else (x0, x0 + 8)
    sy = bot + 1
    if stripe < 0:
        toe, heel = (s1, s1 + 1), (s2 - 1, s2)
    else:
        toe, heel = (s2 - 1, s2), (s1, s1 + 1)
    rect(px, s1, sy, s2, sy, SHOE)                           # 鞋面
    rect(px, toe[0], sy, toe[1], sy, SHOE_HI)                # 鞋头
    rect(px, s1 + 3, sy, s1 + 5, sy, LACE)                   # 鞋带
    rect(px, heel[0], sy, heel[1], sy, HEEL)                 # 后跟护片
    rect(px, s1, sy + 1, s2, sy + 1, MIDSOLE)                # 白色中底
    rect(px, heel[0], sy + 1, heel[1], sy + 1, HEEL)         # 后跟延伸到中底
    rect(px, s1, sy + 2, s2, sy + 2, OUTSOLE)                # 灰色大底


def pants_top(px, oy, lx, rx):
    rect(px, 22, 43 + oy, 42, 47 + oy, PANTS)
    rect(px, 22, 43 + oy, 22, 47 + oy, PANTS_HI)
    rect(px, 42, 43 + oy, 42, 47 + oy, PANTS_HI)
    rect(px, 31, 45 + oy, 33, 47 + oy, PANTS_SH)             # 裆部阴影
    # 白侧条从裤腰一直连到裤脚，中间不留断点
    rect(px, lx, 44 + oy, lx, 47 + oy, STRIPE)
    rect(px, rx + 7, 44 + oy, rx + 7, 47 + oy, STRIPE)
    rect(px, 22, 43 + oy, 42, 43 + oy, PANTS_SH)             # 腰带阴影


# ------------------------------------------------------------------ 躯干
def torso(px, oy):
    # 白色 polo 衫身（顶部一直顶到领口，避免肩膀两侧出现缺口）
    rect(px, 23, 29 + oy, 41, 43 + oy, SHIRT)
    rect(px, 23, 29 + oy, 23, 43 + oy, SHIRT_SH)             # 左侧阴影
    rect(px, 23, 42 + oy, 41, 43 + oy, SHIRT_SH)             # 下摆阴影
    # 深蓝门襟 + 两颗扣子（整体上移 1 像素，别拖太长）
    rect(px, 31, 31 + oy, 33, 37 + oy, NAVY)
    rect(px, 31, 31 + oy, 31, 37 + oy, NAVY_HI)
    put(px, 32, 33 + oy, BUTTON)
    put(px, 32, 36 + oy, BUTTON)
    # 右胸小红点校徽（上移 1 像素）
    rect(px, 37, 34 + oy, 38, 35 + oy, RED)
    put(px, 38, 34 + oy, RED_HI)


def neck(px, oy):
    rect(px, 29, 26 + oy, 35, 31 + oy, SKIN)
    rect(px, 29, 27 + oy, 35, 28 + oy, SKIN_SH)              # 下巴投影


def collar(px, oy):
    """领子从上到下三层：黑 / 白 / 红，只围在脖子周围。
    中间 3 像素宽的门襟直通上去。"""
    y0 = 29 + oy
    x1, x2 = 25, 39
    rect(px, x1, y0, x2, y0, BLACK)                          # 上：黑
    rect(px, x1, y0 + 1, x2, y0 + 1, SHIRT)                  # 中：白
    rect(px, x1, y0 + 2, x2, y0 + 2, RED)                    # 下：红
    # 中间 3 像素宽的门襟直通上去
    rect(px, 31, y0, 33, y0 + 2, NAVY)


def arm(px, side, oy, swing):
    """深蓝短袖固定在肩头，只让小臂前后摆。
    整条手臂上下平移会让袖子脱离肩线，所以肩头必须钉死。"""
    x0 = 17 if side < 0 else 42
    y0 = 29 + oy                                             # 短袖固定
    # 深蓝短袖
    rect(px, x0, y0, x0 + 5, y0 + 6, NAVY)
    rect(px, x0, y0, x0 + 5, y0, NAVY_HI)                    # 肩头高光
    edge = x0 if side < 0 else x0 + 5
    rect(px, edge, y0, edge, y0 + 6, NAVY_HI if side < 0 else NAVY)
    rect(px, x0, y0 + 6, x0 + 5, y0 + 6, NAVY_HI)            # 袖口

    # 小臂 + 手：swing 负 = 前摆上抬，正 = 后摆下垂
    fy = y0 + 7 + swing
    if fy > y0 + 7:                                          # 下垂时补满袖口到小臂之间
        rect(px, x0 + 1, y0 + 7, x0 + 4, fy - 1, SKIN)
    rect(px, x0 + 1, fy, x0 + 4, fy + 6, SKIN)
    rect(px, x0 + 1, fy + 6, x0 + 4, fy + 7, SKIN_SH)        # 手
    # 左手腕上的粉色手环
    if side < 0:
        rect(px, x0 + 1, fy + 5, x0 + 4, fy + 5, PINK)
        put(px, x0 + 2, fy + 5, PINK_HI)


def arm_swing(px, side, oy):
    """挥棒姿势：右臂从肩部平滑向右下伸出并收细，末端握拳；左臂自然下垂。"""
    if side > 0:
        x0 = 42
        y0 = 30 + oy
        # 短袖（肩部）
        rect(px, x0, y0, x0 + 5, y0 + 6, NAVY)
        rect(px, x0, y0, x0 + 5, y0, NAVY_HI)
        rect(px, x0, y0 + 6, x0 + 5, y0 + 6, NAVY_HI)        # 袖口
        # 小臂：向右下延伸，前段粗、后段收细
        for i in range(8):
            x = x0 + 6 + i
            y = y0 + 1 + i // 3
            h = 4 if i < 4 else 3
            rect(px, x, y, x, y + h, SKIN)
            put(px, x, y, SKIN_SH)                           # 上沿阴影
        # 手（握拳，略粗）
        rect(px, x0 + 13, y0 + 3, x0 + 15, y0 + 7, SKIN)
        rect(px, x0 + 13, y0 + 3, x0 + 15, y0 + 3, SKIN_SH)
    else:
        arm(px, side, oy, 0)                                 # 左臂自然下垂


def draw_swing(px, oy=0):
    """挥棒姿势：身体略前倾，右臂前伸、左臂后摆。"""
    leg(px, 24, oy, length=8, stripe=-1)
    leg(px, 33, oy, length=8, stripe=1)
    pants_top(px, oy, 24, 33)
    neck(px, oy)
    torso(px, oy)
    arm_swing(px, -1, oy)
    arm_swing(px, 1, oy)
    collar(px, oy)
    head(px, oy + 1)


def arm_power(px, oy, lift):
    """右臂握棒，lift > 0 上举、< 0 下压；左臂自然下垂。"""
    arm(px, -1, oy, 0)
    x0 = 42
    y0 = 30 + oy
    rect(px, x0, y0, x0 + 5, y0 + 5, NAVY)                  # 短袖
    rect(px, x0, y0, x0 + 5, y0, NAVY_HI)
    y1 = y0 + 2 - lift
    for i in range(8):
        x = x0 + 6 + i
        rect(px, x, y1, x, y1 + 3, SKIN)                     # 小臂
        put(px, x, y1, SKIN_SH)
    rect(px, x0 + 13, y1, x0 + 15, y1 + 4, SKIN)             # 手
    rect(px, x0 + 13, y1, x0 + 15, y1, SKIN_SH)


def draw_hold(px, oy=0):
    """持棒站立：右臂把棒举在身前，左臂自然下垂。"""
    leg(px, 24, oy, length=8, stripe=-1)
    leg(px, 33, oy, length=8, stripe=1)
    pants_top(px, oy, 24, 33)
    neck(px, oy)
    torso(px, oy)
    arm_power(px, oy, 0)
    collar(px, oy)
    head(px, oy)


def draw_charge(px, oy=0):
    """举棒预备：右臂抬到肩上，身体后仰蓄势。"""
    leg(px, 24, oy, length=8, stripe=-1)
    leg(px, 33, oy, length=8, stripe=1)
    pants_top(px, oy, 24, 33)
    neck(px, oy)
    torso(px, oy)
    arm_power(px, oy, 4)
    collar(px, oy)
    head(px, oy - 1)


def draw_swing2(px, oy=0):
    """挥出：右臂压到身体前下方，身体前倾。"""
    leg(px, 24, oy, length=8, stripe=-1)
    leg(px, 33, oy, length=8, stripe=1)
    pants_top(px, oy, 24, 33)
    neck(px, oy)
    torso(px, oy)
    arm_power(px, oy, -5)
    collar(px, oy)
    head(px, oy + 1)
# ------------------------------------------------------------------ 头
def bang(x):
    """参差不齐的刘海：整片到 y=13，中间挖几处深浅不一、间距不均的缺口。
    上限 13，保证刘海绝不盖住镜片（镜片上沿在 y=14）。"""
    if x <= 21 or x >= 43:
        return 16
    table = {
        22: 13, 23: 13, 24: 12, 25: 11, 26: 11, 27: 13, 28: 13,
        29: 13, 30: 12, 31: 13, 32: 13, 33: 12, 34: 13, 35: 13,
        36: 11, 37: 12, 38: 13, 39: 13, 40: 12, 41: 13, 42: 13,
    }
    return table.get(x, 13)


def ear(px, cx, oy):
    """两侧的耳朵：盖掉鬓角的头发，露一圈皮肤，中间一道耳廓折痕。"""
    oval(px, cx, 19.0 + oy, 1.7, 2.9, SKIN)
    put(px, int(cx), 18 + oy, SKIN_SH)
    put(px, int(cx), 19 + oy, SKIN_SH)


def head(px, oy):
    # 1) 头发整体轮廓
    blob(px, 32, 14.5 + oy, 13.0, 12.5, HAIR, n=2.5)
    # 2) 脸（盖住下半张，剩下的边缘就是头发）
    blob(px, 32, 17.5 + oy, 10.0, 11.0, SKIN, n=2.4)
    # 3) 刘海（只在头发轮廓内重涂，天然不溢出）
    blob(px, 32, 14.5 + oy, 13.0, 12.5, HAIR, n=2.5,
         ymax=lambda x: bang(x) + oy)
    # 头顶高光
    rect(px, 29, 4 + oy, 31, 4 + oy, HAIR_HI)
    # 4) 两侧耳朵（盖掉鬓角，露出皮肤；内侧多 1 像素和脸接上）
    ear(px, 20.0, oy)
    ear(px, 44.0, oy)
    put(px, 21, 17 + oy, SKIN)
    put(px, 42, 17 + oy, SKIN)

    # ---- 细圆框眼镜：只画 1px 框，镜片接近透明 ----
    oval(px, 27.0, 18.5 + oy, 4.4, 4.2, FRAME)
    oval(px, 37.0, 18.5 + oy, 4.4, 4.2, FRAME)
    oval(px, 27.0, 18.5 + oy, 3.4, 3.2, LENS)
    oval(px, 37.0, 18.5 + oy, 3.4, 3.2, LENS)
    rect(px, 31, 17 + oy, 33, 17 + oy, FRAME)                # 鼻梁
    put(px, 24, 16 + oy, LENS_HI)                            # 镜片反光
    put(px, 34, 16 + oy, LENS_HI)
    # （镜腿已按要求删掉，只保留两个圆框）

    # ---- 眼睛（3x2，带高光）----
    rect(px, 26, 18 + oy, 28, 19 + oy, EYE)
    rect(px, 36, 18 + oy, 38, 19 + oy, EYE)
    put(px, 26, 18 + oy, EYE_HI)
    put(px, 36, 18 + oy, EYE_HI)

    # ---- 鼻 / 人中两侧的灰胡子 / 嘴 ----
    put(px, 32, 23 + oy, SKIN_SH)
    put(px, 31, 23 + oy, SKIN_SH)
    rect(px, 29, 24 + oy, 30, 24 + oy, STACHE)
    rect(px, 34, 24 + oy, 35, 24 + oy, STACHE)
    rect(px, 31, 25 + oy, 33, 25 + oy, MOUTH)


# ------------------------------------------------------------------ 死亡 / 天使
def wing(px, side, oy):
    """天使翅膀：三片羽毛从肩外侧向外上方展开。side=-1 左，1 右。
    根部放在身体边缘外侧，否则会被躯干整片盖住。"""
    bx = 21 if side < 0 else 43
    by = 31 + oy
    specs = ((65, 16.0, 4.0), (45, 18.0, 4.0), (25, 18.0, 3.6))
    for deg, rx, ry in specs:
        ang = deg if side < 0 else 180 - deg
        r = math.radians(ang)
        cx = bx + math.cos(r) * rx
        cy = by - math.sin(r) * ry
        oval_rot(px, cx, cy, rx + 1.4, ry + 1.4, ang, WING_EDGE)
    for deg, rx, ry in specs:
        ang = deg if side < 0 else 180 - deg
        r = math.radians(ang)
        cx = bx + math.cos(r) * rx
        cy = by - math.sin(r) * ry
        oval_rot(px, cx, cy, rx, ry, ang, WING)
        oval_rot(px, cx, cy, rx - 1.8, ry - 1.4, ang, WING_SH)


def halo(px, oy):
    """头顶金色光环。必须在 head() 之后画，否则会被头发盖掉大半。"""
    ring(px, 32, 3.5 + oy, 10.0, 3.0, 1.8, HALO)
    put(px, 25, 2 + oy, HALO_HI)
    put(px, 26, 2 + oy, HALO_HI)
    put(px, 38, 2 + oy, HALO_HI)


def draw_dead(px, oy=0):
    """仰面躺在地上：头朝左、脸朝上，双手放在腰间。
    按最新参考图：手臂加粗加长、左侧刘海参差、脸部区域占比更大。"""
    g = 58 + oy                       # 贴地线
    top = g - 21                      # 身体上沿
    bot = g - 1                       # 身体下沿

    # ---- 腿：深蓝底，上下边缘浅蓝，中间两条粗白侧条 ----
    rect(px, 41, top + 1, 54, bot - 1, PANTS)
    rect(px, 41, top + 1, 54, top + 1, PANTS_HI)
    rect(px, 41, bot - 1, 54, bot - 1, PANTS_HI)
    rect(px, 41, top + 2, 54, top + 3, STRIPE)          # 上腿白条
    rect(px, 41, top + 11, 54, top + 12, STRIPE)        # 下腿白条
    rect(px, 41, top + 7, 54, top + 7, PANTS_SH)        # 两条腿的分界

    # ---- 鞋：白底 + 中间一条中底横条 ----
    rect(px, 54, top + 1, 61, bot - 1, SHOE)
    rect(px, 54, top + 1, 61, top + 1, SHOE_HI)
    rect(px, 54, top + 8, 61, top + 9, MIDSOLE)
    rect(px, 54, bot - 1, 61, bot - 1, MIDSOLE)

    # ---- 躯干（白 polo，正面朝上）----
    rect(px, 23, top, 41, bot, SHIRT)
    rect(px, 23, bot, 41, bot, SHIRT_SH)
    # 领子：黑 / 白 / 红 三条竖线（从左到右）
    rect(px, 23, top, 23, bot, BLACK)
    rect(px, 24, top, 24, bot, SHIRT)
    rect(px, 25, top, 25, bot, RED)
    # 门襟：浅色竖条
    rect(px, 30, top + 2, 32, bot - 4, SHIRT_SH)
    # 校徽：红方块 + 旁边一个浅灰点（整体左移 3 像素）
    rect(px, 31, top + 3, 32, top + 4, RED)
    put(px, 31, top + 3, RED_HI)
    put(px, 34, top + 5, SHIRT_SH)

    # ---- 手臂：加粗加长（上侧短袖 / 下侧短袖 + 粗小臂）----
    rect(px, 26, top - 2, 30, top + 1, NAVY)
    rect(px, 26, top - 2, 30, top - 2, NAVY_HI)
    rect(px, 26, bot - 6, 30, bot, NAVY)                # 下侧短袖
    rect(px, 26, bot - 6, 30, bot - 6, NAVY_HI)
    rect(px, 31, bot - 6, 41, bot, SKIN_PALE)           # 粗小臂（长 10 厚 7）
    rect(px, 31, bot - 6, 41, bot - 6, SKIN_SH)         # 小臂上沿阴影

    # ---- 头：正圆，脸在上、头发铺在下 ----
    oval(px, 13, g - 12, 12.0, 12.0, HAIR)
    # 左侧刘海参差（锯齿）
    put(px, 1, g - 21, HAIR)
    put(px, 0, g - 19, HAIR)
    put(px, 1, g - 17, HAIR)
    put(px, 0, g - 15, HAIR)
    put(px, 1, g - 13, HAIR)
    oval(px, 13.5, g - 16, 10.7, 8.5, SKIN_PALE)        # 脸（占比更大）
    # 两只竖着的闭眼（都只占 1 像素宽）
    rect(px, 7, g - 21, 7, g - 16, EYE)
    rect(px, 18, g - 21, 18, g - 16, EYE)
    put(px, 12, g - 13, SKIN_SH)                        # 鼻梁
    # 嘴：1 像素细的横唇 + 下面两颗小牙
    rect(px, 11, g - 9, 14, g - 9, MOUTH)
    put(px, 11, g - 8, MOUTH)
    put(px, 13, g - 8, MOUTH)
    # 头发往肩膀方向垂一小块
    rect(px, 22, top - 2, 27, top + 1, HAIR)

# ------------------------------------------------------------------ 帧
def draw(mode="idle", legs=(24, 33, 8, 8), bob=0, swing=0):
    """legs = (左腿x, 右腿x, 左腿长, 右腿长)。
    腿长越短，脚抬得越高——用来做跑步的举腿。"""
    px = new_frame()
    oy = bob

    if mode == "dead":
        draw_dead(px, oy)
        return px

    if mode == "swing":
        draw_swing(px, oy)
        return px

    if mode == "hold":
        draw_hold(px, oy)
        return px

    if mode == "charge":
        draw_charge(px, oy)
        return px

    if mode == "swing2":
        draw_swing2(px, oy)
        return px

    # 天使形态：翅膀先画，会被后面的身体盖住根部
    if mode == "angel":
        wing(px, -1, oy)
        wing(px, 1, oy)

    if mode == "jump":
        lx, rx, ll, rl = 23, 34, 7, 6
    elif mode == "fall":
        lx, rx, ll, rl = 21, 36, 9, 9
    else:
        lx, rx, ll, rl = legs
    leg(px, lx, oy, length=ll, stripe=-1)
    leg(px, rx, oy, length=rl, stripe=1)

    pants_top(px, oy, lx, rx)
    neck(px, oy)          # 脖子必须在躯干之前，否则会在衣服上戳个洞
    torso(px, oy)

    if mode == "jump":
        arm(px, -1, oy, -4)
        arm(px, 1, oy, -4)
    elif mode == "fall":
        arm(px, -1, oy, 3)
        arm(px, 1, oy, 3)
    else:
        arm(px, -1, oy, swing)
        arm(px, 1, oy, -swing)

    collar(px, oy)
    head(px, oy)

    if mode == "angel":
        halo(px, oy)          # 必须在 head 之后，否则大半被头发盖掉

    return px


def encode_png(w, h, rows):
    """rows: 每行是 w*4 字节的 RGBA。filter 全部用 0（None）。"""
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


def save_scaled(frames, path, scale=6, pad=8, bg=(0x2A, 0x2E, 0x3A, 255)):
    """把所有帧拼成一条，放大 scale 倍，透明处铺深灰底，方便肉眼检查。"""
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

    # --preview：只出预览图，绝不碰游戏正在用的 assets/player_sheet.png
    preview_only = '--preview' in sys.argv[1:]

    frames = [
        draw("idle", (24, 33, 8, 8), 0, 0),
        draw("idle", (24, 33, 8, 8), 1, 0),
        # 跑步四帧：左抬腿 → 腾空 → 右抬腿 → 腾空
        draw("run", (25, 33, 4, 8), 0, 4),      # 左脚高抬内收，左臂后摆
        draw("run", (24, 33, 7, 7), -2, 1),     # 双脚离地，身体上浮
        draw("run", (24, 32, 8, 4), 0, -4),     # 右脚高抬内收，左臂前摆
        draw("run", (24, 33, 7, 7), -2, -1),    # 双脚离地，身体上浮
        draw("jump"),
        draw("fall"),
        draw("dead"),       # 帧 8：躺平（死亡）
        draw("angel"),      # 帧 9：天使形态（复活）
        draw("swing"),      # 帧 10：单手挥棒（旧）
        draw("hold"),       # 帧 11：双手持棒站立
        draw("charge"),     # 帧 12：举棒蓄力
        draw("swing2"),     # 帧 13：双手挥出
        draw("idle", (24, 33, 8, 8), -1, 0),   # 帧 14：向上看（踮脚）
        draw("idle", (24, 33, 8, 8), 1, 0),    # 帧 15：向下看（蹲一点）
    ]

    if preview_only:
        path = os.path.join(tools_dir, 'preview_sheet.png')
    else:
        path = os.path.join(out_dir, 'player_sheet.png')
    sw, sh = save_sheet(frames, path)

    pw, ph = save_scaled(frames, os.path.join(tools_dir, 'preview_student.png'))
    print('精灵表：%s  (%dx%d, %d 帧)' % (path, sw, sh, len(frames)))
    print('预览图：%s  (%dx%d)' % (os.path.join(tools_dir, 'preview_student.png'), pw, ph))
    if preview_only:
        print('（--preview 模式：assets/player_sheet.png 未被改动）')

    for i, f in enumerate(frames):
        xs = [x for y in range(H) for x in range(W) if f[y][x] != CLEAR]
        ys = [y for y in range(H) for x in range(W) if f[y][x] != CLEAR]
        if xs:
            print('  帧%d 包围盒 x %d..%d  y %d..%d  (脚底行 %d, 底边 %d, 相对中心 %+d)'
                  % (i, min(xs), max(xs), min(ys), max(ys),
                     max(ys), max(ys) + 1, max(ys) + 1 - H // 2))

    table = {CLEAR: '.', HAIR: '#', HAIR_HI: '+', SKIN: 's', SKIN_SH: 's',
             EYE: 'o', EYE_HI: 'o', FRAME: 'F', LENS: 'l', LENS_HI: 'l',
             MOUTH: 'p', STACHE: 'm', SHIRT: 'W', SHIRT_SH: 'w',
             NAVY: 'N', NAVY_HI: 'n', BLACK: 'B', RED: 'R', RED_HI: 'R',
             BUTTON: 'W', PANTS: 'N', PANTS_HI: 'n', PANTS_SH: 'n',
             STRIPE: '|', SHOE: 'K', SHOE_HI: 'k', LACE: '=', HEEL: 'H',
             MIDSOLE: 'M', OUTSOLE: 'O', PINK: 'x', PINK_HI: 'x'}
    for idx in (0, 6):
        print('\n--- 帧 %d ---' % idx)
        for y in range(H):
            line = ''.join(table.get(frames[idx][y][x], '?') for x in range(W))
            print(line.rstrip())


if __name__ == '__main__':
    main()
