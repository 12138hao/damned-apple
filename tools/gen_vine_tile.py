# -*- coding: utf-8 -*-
"""生成可攀爬藤蔓的「可平铺贴图」+ 预览条。

为什么要「可平铺」：藤蔓长度是变量（50~2000 都可能），
用一张定长贴图去拉伸会把像素拉糊，所以做成 24x32 的循环小片，
游戏里按 32 像素一格往上堆，堆多少格由 Length 决定。

无缝的关键：主干的横向偏移用 sin(2*pi*y/H)，一个周期正好等于贴图高度，
所以第 0 行和第 H 行的偏移、斜率都完全一致，接起来看不出缝。

纯 stdlib，自带最小 PNG 读写（项目里没有 PIL）。
"""
import struct, zlib, math, os

# ── 最小 PNG 读写 ────────────────────────────────────────────
def write_png(path, w, h, px):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
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


CLEAR = (0, 0, 0, 0)
OUTLINE = (26, 44, 22, 255)
DARK    = (52, 92, 40, 255)
BASE    = (76, 130, 56, 255)
LIGHT   = (112, 172, 74, 255)
HILITE  = (152, 204, 102, 255)

W, H = 24, 32          # 一片藤蔓的尺寸（也是平铺周期）
CX = 12                # 主干基准 x
AMP = 2.2              # 主干摆动幅度


def stem_x(y):
    """某一行的主干中心 x。周期 = H，保证上下接缝连续。"""
    return CX + AMP * math.sin(2.0 * math.pi * y / H)


def put(px, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        px[y][x] = c


def draw_leaf(px, x0, y0, direction, length, thick):
    """一片叶子：从 (x0,y0) 朝 direction(±1) 长出去，杏仁形 + 描边 + 叶脉。"""
    for t in range(length + 1):
        k = t / float(length)
        # 宽度沿长度做正弦，两头尖中间胖
        half = thick * math.sin(math.pi * k)
        cx = x0 + direction * t
        cy = y0 - t // 3                      # 叶尖往上翘一点，别水平长
        for dy in range(-int(round(half)), int(round(half)) + 1):
            px_x = int(cx)
            if abs(dy) >= int(round(half)):
                put(px, px_x, cy + dy, OUTLINE)       # 上下沿描边
            elif t <= 1:
                put(px, px_x, cy + dy, DARK)          # 叶柄
            else:
                put(px, px_x, cy + dy, BASE)
        # 叶脉：中间一条暗线
        if t >= 2:
            put(px, int(cx), cy, DARK)
    # 叶尖描边
    put(px, x0 + direction * (length + 1), y0 - (length + 1) // 3, OUTLINE)
    # 叶面高光：上沿内侧一条亮线
    for t in range(2, max(3, length // 2) + 2):
        k = t / float(length)
        half = thick * math.sin(math.pi * k)
        put(px, x0 + direction * t, y0 - t // 3 - int(round(half)) + 1, LIGHT)


def make_tile():
    px = [[CLEAR for _ in range(W)] for _ in range(H)]

    # ① 主干：一条带描边的竖线，逐行跟着 stem_x 摆
    for y in range(H):
        cx = stem_x(y)
        for x in range(W):
            d = x - cx
            if abs(d) <= 1.0:
                px[y][x] = BASE
            elif abs(d) <= 2.0:
                px[y][x] = OUTLINE
        # 左侧高光（像素风：靠左 1 列）
        hx = int(round(cx - 1.0))
        if 0 <= hx < W:
            px[y][hx] = LIGHT
        # 缠绕纹：每 8 行错开一格，暗示「藤」是扭着的
        if y % 8 in (0, 1):
            px[y][int(round(cx + 1.0))] = DARK

    # ② 两片叶子，左右交替（一片朝左上方，一片朝右下方）
    draw_leaf(px, int(round(stem_x(7))), 8, -1, 10, 3)
    draw_leaf(px, int(round(stem_x(23))), 24, +1, 10, 3)

    # ③ 零星小刺，打破规律感（位置固定，保证平铺后仍然一致）
    for (sx, sy) in ((int(round(stem_x(13))) + 2, 13), (int(round(stem_x(29))) - 2, 29)):
        put(px, sx, sy, DARK)
        put(px, sx + (1 if sx > CX else -1), sy - 1, OUTLINE)

    return px


def make_preview(tile, repeats=6, zoom=3):
    """把贴图竖着拼 repeats 次再放大，放在深色底上看接缝。"""
    tw, th = W * zoom, H * zoom
    ph, pw = th * repeats, tw + 6 * zoom
    bg = (28, 40, 30, 255)
    px = [[bg for _ in range(pw)] for _ in range(ph)]
    for r in range(repeats):
        for y in range(H):
            for x in range(W):
                c = tile[y][x]
                if c[3] == 0:
                    continue
                for dy in range(zoom):
                    for dx in range(zoom):
                        px[r * th + y * zoom + dy][x * zoom + dx] = c
    return pw, ph, px


def main():
    tile = make_tile()
    os.makedirs('assets/jungle', exist_ok=True)
    write_png('assets/jungle/vine_tile.png', W, H, tile)
    print('藤蔓平铺片 ->', 'assets/jungle/vine_tile.png', '%dx%d' % (W, H))

    pw, ph, px = make_preview(tile)
    os.makedirs('docs/screenshots', exist_ok=True)
    write_png('docs/screenshots/vine_preview.png', pw, ph, px)
    print('预览条 ->', 'docs/screenshots/vine_preview.png', '%dx%d' % (pw, ph))


if __name__ == '__main__':
    main()
