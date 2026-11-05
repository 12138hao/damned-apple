"""老苹果：白底抠图 + 像素化。

源图是一张 1024×1024 的白底像素风插画（底部还带一小块灰色投影）。
难点在**胡子是白的** —— 不能简单地「白色当透明」，否则胡子会被一起抠掉。
所以用「从画布四边向内泛洪」的办法：只有和边框连通的浅色像素才算背景，
苹果内部那块白色（胡子、高光）被苹果本体的深色轮廓围住，泛洪到不了，天然保留。
"""

import collections
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

FF = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
SRC = r"D:\h\my-game\dsh-image-gen\image-3fc47137.jpg"
TMP = r"D:\h\my-game\docs\screenshots\_oa_src.png"
OUT = r"D:\h\my-game\assets\npc\old_apple.png"

# 可通过命令行覆盖：python build_old_apple.py --h=96 --steps=8 --out=xxx.png
TARGET_H = 96           # 输出贴图高度（游戏里放大 2 倍显示 = 屏幕 192）
BG_MIN = 150            # 背景判定：最暗通道不低于这个值
BG_SAT = 0.18           #            且饱和度不高于这个值
COLOR_STEPS = 12        # 色阶数（越小越「复古」；12 = 保留细节，3 = 硬像素风）
OUTLINE = True          # 是否加 1 像素深色描边 + 二值 alpha（硬像素风）。--outline=0 关掉


def is_bg(rgba, i):
    r, g, b = rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]
    mx = max(r, g, b)
    mn = min(r, g, b)
    if mx == 0:
        return True
    return mn >= BG_MIN and (mx - mn) / mx <= BG_SAT


def flood_clear(w, h, rgba):
    """从四边向内泛洪，把连通的浅色背景变透明。返回被清掉的像素数。"""
    seen = bytearray(w * h)
    q = collections.deque()

    def push(i):
        if not seen[i] and is_bg(rgba, i):
            seen[i] = 1
            q.append(i)

    for x in range(w):
        push(x)                 # 上边
        push((h - 1) * w + x)   # 下边
    for y in range(h):
        push(y * w)             # 左边
        push(y * w + w - 1)     # 右边

    n = 0
    while q:
        i = q.popleft()
        rgba[i * 4 + 3] = 0
        n += 1
        x, y = i % w, i // w
        if x > 0: push(i - 1)
        if x < w - 1: push(i + 1)
        if y > 0: push(i - w)
        if y < h - 1: push(i + w)
    return n


def is_shadow(rgba, i):
    """底部那块投影：低饱和的灰。苹果的深红描边饱和度高，
    胡子虽然也是灰白，但**明亮**（mn 常在 200 以上），所以两个条件卡住就不会误吃。"""
    r, g, b = rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]
    mx = max(r, g, b)
    mn = min(r, g, b)
    if mx == 0:
        return False
    return (mx - mn) / mx <= 0.20 and mn < 205


def erode_bottom_shadow(w, h, rgba):
    """按列从画面底部往上清：连续的低饱和灰 = 投影。
    碰到明亮（胡子）或高饱和（苹果描边）就停，所以不会把胡子啃掉。"""
    n = 0
    for x in range(w):
        for y in range(h - 1, -1, -1):
            i = y * w + x
            if rgba[i * 4 + 3] == 0:
                continue            # 已经是背景，继续往上找
            if not is_shadow(rgba, i):
                break               # 碰到实体（胡子/苹果），这一列到此为止
            rgba[i * 4 + 3] = 0
            n += 1
    return n


def bbox(w, h, rgba, thresh=24):
    x0, y0, x1, y1 = w, h, -1, -1
    for y in range(h):
        for x in range(w):
            if rgba[(y * w + x) * 4 + 3] > thresh:
                if x < x0: x0 = x
                if x > x1: x1 = x
                if y < y0: y0 = y
                if y > y1: y1 = y
    return x0, y0, x1, y1


def resize_area(w, h, rgba, tw, th):
    """面积平均降采样，alpha 预乘，避免边缘发黑。"""
    out = bytearray(tw * th * 4)
    for ty in range(th):
        sy0 = ty * h / th
        sy1 = (ty + 1) * h / th
        for tx in range(tw):
            sx0 = tx * w / tw
            sx1 = (tx + 1) * w / tw
            ar = ag = ab = aa = 0.0
            for sy in range(int(sy0), min(int(sy1) + 1, h)):
                for sx in range(int(sx0), min(int(sx1) + 1, w)):
                    i = (sy * w + sx) * 4
                    a = rgba[i + 3] / 255.0
                    if a <= 0:
                        continue
                    ar += rgba[i] * a
                    ag += rgba[i + 1] * a
                    ab += rgba[i + 2] * a
                    aa += a
            d = (ty * tw + tx) * 4
            if aa <= 0:
                continue
            out[d] = min(255, int(ar / aa))
            out[d + 1] = min(255, int(ag / aa))
            out[d + 2] = min(255, int(ab / aa))
            out[d + 3] = min(255, int(255 * aa / ((sy1 - sy0) * (sx1 - sx0))))
    return out


def quantize(w, h, rgba, steps):
    """把颜色压到有限色阶，做出像素画的「块面」感（alpha 不动）。"""
    out = bytearray(rgba)
    step = 255.0 / (steps - 1)
    lut = [int(round(round(v / step) * step)) for v in range(256)]
    for i in range(w * h):
        if out[i * 4 + 3] == 0:
            continue
        out[i * 4] = lut[out[i * 4]]
        out[i * 4 + 1] = lut[out[i * 4 + 1]]
        out[i * 4 + 2] = lut[out[i * 4 + 2]]
    return out


def binarize_alpha(w, h, rgba):
    """降采样会留下大片半透明毛边 → 一律二值化。
    像素画的剪影是硬的，半透明边会让它看起来像「缩小的插画」。"""
    for i in range(w * h):
        rgba[i * 4 + 3] = 255 if rgba[i * 4 + 3] >= 128 else 0


def pad_and_outline(w, h, rgba, outline=(58, 18, 22), pad=1):
    """四周留 1 像素，并把紧贴剪影的那一圈透明像素涂成深色描边。
    项目里其他精灵都有深描边，这一步对「像素感」的贡献比降分辨率还大。"""
    tw, th = w + pad * 2, h + pad * 2
    out = bytearray(tw * th * 4)

    for y in range(h):
        src = y * w * 4
        dst = ((y + pad) * tw + pad) * 4
        out[dst:dst + w * 4] = rgba[src:src + w * 4]

    def opaque(x, y):
        return 0 <= x < tw and 0 <= y < th and out[(y * tw + x) * 4 + 3] > 0

    # 先算好要涂的位置，避免边涂边判断导致描边层层外扩
    edge = []
    for y in range(th):
        for x in range(tw):
            if opaque(x, y):
                continue
            if opaque(x - 1, y) or opaque(x + 1, y) or opaque(x, y - 1) or opaque(x, y + 1):
                edge.append((x, y))

    for x, y in edge:
        i = (y * tw + x) * 4
        out[i], out[i + 1], out[i + 2], out[i + 3] = outline[0], outline[1], outline[2], 255
    return tw, th, out


def main():
    global TARGET_H, COLOR_STEPS, OUT, OUTLINE
    for a in sys.argv[1:]:
        if a.startswith("--h="):
            TARGET_H = int(a[4:])
        elif a.startswith("--steps="):
            COLOR_STEPS = int(a[8:])
        elif a.startswith("--out="):
            OUT = a[6:]
        elif a.startswith("--outline="):
            OUTLINE = a[10:] not in ("0", "false", "no")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    if not os.path.exists(TMP):
        subprocess.run([FF, "-y", "-loglevel", "error", "-i", SRC, TMP], check=True)

    w, h, ch, px = pix.load(TMP)
    rgba = pix.to_rgba(w, h, ch, px)
    print(f"源图 {w}x{h}")

    cleared = flood_clear(w, h, rgba)
    print(f"泛洪清掉 {cleared} 个背景像素（{cleared / (w * h) * 100:.1f}%）")

    shadow = erode_bottom_shadow(w, h, rgba)
    print(f"按列清掉 {shadow} 个投影像素")

    x0, y0, x1, y1 = bbox(w, h, rgba)
    cw, chh = x1 - x0 + 1, y1 - y0 + 1
    print(f"主体包围盒 x[{x0},{x1}] y[{y0},{y1}]  尺寸 {cw}x{chh}")

    crop = bytearray(cw * chh * 4)
    for y in range(chh):
        s = ((y0 + y) * w + x0) * 4
        crop[y * cw * 4:(y + 1) * cw * 4] = rgba[s:s + cw * 4]

    # 内部先缩到 TARGET_H - 2，四周各留 1 像素给描边
    th = TARGET_H - 2
    tw = max(1, round(cw * th / chh))
    small = resize_area(cw, chh, crop, tw, th)
    small = quantize(tw, th, small, COLOR_STEPS)
    if OUTLINE:
        binarize_alpha(tw, th, small)
        tw, th, small = pad_and_outline(tw, th, small)

    pix.save(OUT, tw, th, 4, small)
    print(f"输出 {OUT}  {tw}x{th}（含 1 像素描边）")


if __name__ == "__main__":
    main()
