"""生成蓝天背景 / 太阳 / 远山 / 云 / 草地。纯手写 PNG，零第三方依赖。

输出到 assets/：
  sky_gradient.png   3200x648  竖直渐变蓝天（带 4x4 有序抖动消除色带）
  sun.png             160x160  太阳 + 三层光晕
  mountains_far.png  3200x240  远山（雾蓝色，正弦叠加，左右可接缝）
  mountains_near.png 3200x200  近山（青绿色）
  cloud_a/b/c.png              三朵不同形状的云（软边）
  ground_tile.png      64x40   草皮 + 泥土，左右可无缝平铺

用法：python tools/gen_scenery.py
"""
import math
import os
import random
import struct
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'assets')


# ---------------------------------------------------------------- PNG 工具
def new_img(w, h):
    return [[(0, 0, 0, 0) for _ in range(w)] for _ in range(h)]


def over(dst, src):
    """src 叠到 dst 上（Porter-Duff over）。"""
    sr, sg, sb, sa = src
    if sa == 0:
        return dst
    if sa == 255:
        return src
    dr, dg, db, da = dst
    a = sa / 255.0
    ad = da / 255.0
    oa = a + ad * (1 - a)
    if oa <= 0:
        return (0, 0, 0, 0)
    return (int((sr * a + dr * ad * (1 - a)) / oa),
            int((sg * a + dg * ad * (1 - a)) / oa),
            int((sb * a + db * ad * (1 - a)) / oa),
            int(oa * 255))


def put(img, x, y, c):
    if 0 <= y < len(img) and 0 <= x < len(img[0]):
        img[y][x] = over(img[y][x], c)


def save(img, name):
    h = len(img)
    w = len(img[0])
    raw = bytearray()
    for row in img:
        raw.append(0)
        for px in row:
            raw.extend(px)

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data
                + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    with open(os.path.join(OUT, name), 'wb') as fp:
        fp.write(png)
    print('  %-22s %4d x %4d' % (name, w, h))


# ---------------------------------------------------------------- 天空
# 关键高度参考：地面碰撞面在 y=535，所以渐变要把"天蓝"压在 0~350，
# "接近地平线的淡青"落在 480~540，下面再淡下去。
SKY_STOPS = [
    (0.00, (0x14, 0x3E, 0x8C)),
    (0.18, (0x28, 0x64, 0xBC)),
    (0.40, (0x4C, 0x93, 0xE0)),
    (0.60, (0x7E, 0xBC, 0xEE)),
    (0.76, (0xAC, 0xDA, 0xF6)),
    (0.86, (0xD2, 0xEE, 0xFB)),
    (1.00, (0xEE, 0xF9, 0xFE)),
]

BAYER4 = ((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5))


def sky_color(t):
    for i in range(len(SKY_STOPS) - 1):
        t0, c0 = SKY_STOPS[i]
        t1, c1 = SKY_STOPS[i + 1]
        if t <= t1:
            k = (t - t0) / (t1 - t0)
            return tuple(c0[j] + (c1[j] - c0[j]) * k for j in range(3))
    return SKY_STOPS[-1][1]


def make_sky(w, h):
    img = new_img(w, h)
    for y in range(h):
        t = y / (h - 1)
        base = sky_color(t)
        nxt = sky_color(min(1.0, (y + 1) / (h - 1)))
        row = img[y]
        for k in range(4):
            d = BAYER4[y & 3][k] / 16.0
            col = (int(base[0] + (nxt[0] - base[0]) * d),
                   int(base[1] + (nxt[1] - base[1]) * d),
                   int(base[2] + (nxt[2] - base[2]) * d), 255)
            for x in range(k, w, 4):
                row[x] = col
    return img


# ---------------------------------------------------------------- 太阳
def make_sun(size=160):
    img = new_img(size, size)
    c = size / 2.0
    for y in range(size):
        for x in range(size):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c)
            if d <= 24:
                col = (0xFF, 0xFB, 0xE2, 255)
            elif d <= 30:
                col = (0xFF, 0xF3, 0xB4, 255)
            elif d <= 42:
                k = (42 - d) / 12.0
                col = (0xFF, 0xE4, 0x8A, int(60 + 120 * k))
            elif d <= 68:
                k = (68 - d) / 26.0
                col = (0xFF, 0xDA, 0x74, int(72 * k * k))
            else:
                continue
            img[y][x] = col
    return img


# ---------------------------------------------------------------- 山
def make_mountains(w, h, color, amp, phase, base_ratio=0.52, freq=(2, 5, 11)):
    img = new_img(w, h)
    r, g, b = color
    for x in range(w):
        v = 0.0
        for i, f in enumerate(freq):
            v += math.sin(2 * math.pi * f * x / w + phase + i * 1.9) / (i + 1)
        top = int(h * base_ratio - v * amp)
        top = max(0, min(h - 1, top))
        for y in range(top, h):
            img[y][x] = (r, g, b, 255)
    return img


# ---------------------------------------------------------------- 云
def make_cloud(w, h, blobs, bottom_tint=0.30):
    img = new_img(w, h)
    for y in range(h):
        for x in range(w):
            cov = 0.0
            for (cx, cy, rx, ry) in blobs:
                dx = (x + 0.5 - cx) / rx
                dy = (y + 0.5 - cy) / ry
                d = math.hypot(dx, dy)
                if d < 1.0:
                    cov = max(cov, min(1.0, (1.0 - d) * 5.0))
            if cov <= 0.03:
                continue
            k = y / (h - 1)                       # 0 = 云顶，1 = 云底
            shade = bottom_tint * k * k
            r = int(255 - 66 * shade)
            g = int(255 - 44 * shade)
            b = int(255 - 16 * shade)
            img[y][x] = (r, g, b, int(255 * min(1.0, cov)))
    return img


# ---------------------------------------------------------------- 草地
def make_ground(w=64, h=160):
    """w 宽可横向平铺；顶部 13 行是草皮，往下全是泥土（一直铺到 h）。
    做 160 高是为了让地面在视口底部以下还有土，不会露出天空。"""
    img = new_img(w, h)
    rnd = random.Random(20261021)

    GRASS_DARK = (0x2A, 0x62, 0x2C)
    GRASS_MID = (0x3F, 0x82, 0x36)
    GRASS_LIGHT = (0x63, 0xA8, 0x42)
    GRASS_TIP = (0x93, 0xC9, 0x5C)
    GRASS_SHADE = (0x1E, 0x4A, 0x24)

    GRASS_TOP = 22          # 草皮底边（往下就是泥土）

    # 1) 草皮底：从亮绿往下渐变到暗绿（这样不会出现一条条竖纹）
    for y in range(GRASS_TOP):
        k = y / (GRASS_TOP - 1)
        c = (int(0x54 + (0x24 - 0x54) * k),
             int(0x9C + (0x56 - 0x9C) * k),
             int(0x42 + (0x28 - 0x42) * k), 255)
        for x in range(w):
            img[y][x] = c

    # 2) 草皮下沿再压一道暗边，做出厚度
    for x in range(w):
        img[GRASS_TOP - 1][x] = (GRASS_SHADE[0], GRASS_SHADE[1], GRASS_SHADE[2], 255)

    # 3) 草叶：每簇只影响顶部 4 行，簇间挖谷口，顶边才参差
    x = 0
    while x < w:
        thick = rnd.randint(2, 4)
        tip = rnd.randint(0, 7)
        col = rnd.choice([(0x63, 0xA8, 0x42), (0x4C, 0x8C, 0x38), (0x78, 0xB8, 0x4C),
                          (0x63, 0xA8, 0x42), (0x58, 0x9A, 0x3C)])
        for i in range(thick):
            xx = (x + i) % w
            for y in range(tip, min(tip + 4, GRASS_TOP)):
                img[y][xx] = (col[0], col[1], col[2], 255)
            img[tip][xx] = (GRASS_TIP[0], GRASS_TIP[1], GRASS_TIP[2], 255)
        x += thick

        gap = rnd.randint(1, 2)
        valley = rnd.randint(4, 8)
        for i in range(gap):
            xx = (x + i) % w
            for y in range(0, valley):
                img[y][xx] = (0, 0, 0, 0)
        x += gap

    # 3.5) 零星小野花（2 像素花瓣 + 1 像素花心）
    for _ in range(5):
        fx = rnd.randrange(w - 1)
        fy = rnd.randint(4, GRASS_TOP - 7)
        petal = rnd.choice([(0xFF, 0xF2, 0x7C), (0xFF, 0xFF, 0xFF), (0xF2, 0xA0, 0xCC)])
        put(img, fx, fy, (petal[0], petal[1], petal[2], 255))
        put(img, fx + 1, fy, (petal[0], petal[1], petal[2], 255))
        put(img, fx, fy + 1, (0xE0, 0xBE, 0x38, 255))

    # 4) 泥土：上浅下深
    for y in range(GRASS_TOP, h):
        k = (y - GRASS_TOP) / (h - 1 - GRASS_TOP)
        r = int(0x8C - 0x36 * k)
        g = int(0x64 - 0x2A * k)
        b = int(0x42 - 0x1C * k)
        for x in range(w):
            img[y][x] = (r, g, b, 255)

    # 5) 泥土里的石子与暗斑（铺满整个深度）
    for _ in range(40 + (h - 40) * 2):
        x = rnd.randrange(w)
        y = rnd.randrange(GRASS_TOP + 3, h)
        if rnd.random() < 0.62:
            c = (0x53, 0x3A, 0x26, 255)          # 暗斑
            pts = ((0, 0), (1, 0), (0, 1))
        else:
            c = (0xA8, 0x88, 0x64, 255)          # 小石子
            pts = ((0, 0), (1, 0), (0, 1), (1, 1))
        for dx, dy in pts:
            put(img, (x + dx) % w, y + dy, c)

    # 6) 几根垂下来的草根
    for _ in range(4):
        x = rnd.randrange(w)
        ln = rnd.randint(2, 4)
        for i in range(ln):
            put(img, x + (1 if i > ln // 2 else 0), GRASS_TOP + i,
                (0x35, 0x22, 0x16, 255))

    return img


# ---------------------------------------------------------------- main
def main():
    os.makedirs(OUT, exist_ok=True)
    print('生成场景素材：')

    save(make_sky(3200, 648), 'sky_gradient.png')
    save(make_sun(160), 'sun.png')

    save(make_mountains(3200, 240, (0x8F, 0xB2, 0xD2), 46, 0.0, 0.55), 'mountains_far.png')
    save(make_mountains(3200, 200, (0x5C, 0x8C, 0xA0), 38, 2.3, 0.60,
                        (3, 7, 13)), 'mountains_near.png')

    save(make_cloud(128, 56, [
        (34, 40, 26, 15), (60, 30, 24, 18), (88, 36, 28, 16), (104, 44, 18, 11),
    ]), 'cloud_a.png')

    save(make_cloud(104, 44, [
        (28, 30, 20, 12), (50, 24, 19, 14), (72, 30, 20, 12),
    ]), 'cloud_b.png')

    save(make_cloud(150, 62, [
        (32, 46, 26, 14), (58, 34, 24, 18), (86, 30, 26, 20),
        (114, 40, 24, 16), (132, 50, 16, 10),
    ]), 'cloud_c.png')

    save(make_ground(128, 160), 'ground_tile.png')
    print('完成。')


if __name__ == '__main__':
    main()
