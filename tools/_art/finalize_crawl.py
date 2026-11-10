"""最终清理 + 游戏内尺度平铺预览。

清理：把 alpha>0 但仍偏品红的孤立像素判透明，再抹掉 1~2 像素的孤立小点。
预览：按游戏内缩放 s 平铺 N 次（最近邻放大 2 倍便于看），并在下方标注接缝位置。
"""
import sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix


def despeckle(w, h, px4):
    # 1) 残余品红像素 -> 透明
    killed = 0
    for i in range(w * h):
        o = i * 4
        if px4[o + 3] == 0:
            continue
        r, g, b = px4[o], px4[o + 1], px4[o + 2]
        if (min(r, b) - g) > 12 and min(r, b) > 20:
            px4[o:o + 4] = b'\x00\x00\x00\x00'
            killed += 1
    # 2) 孤立小点：邻域 8 个里不透明少于 2 个就删
    for _ in range(2):
        rm = []
        for y in range(h):
            for x in range(w):
                o = (y * w + x) * 4
                if px4[o + 3] < 60:
                    continue
                n = 0
                for dy in (-1, 0, 1):
                    yy = y + dy
                    if yy < 0 or yy >= h:
                        continue
                    for dx in (-1, 0, 1):
                        if dx == 0 and dy == 0:
                            continue
                        xx = x + dx
                        if xx < 0 or xx >= w:
                            continue
                        if px4[(yy * w + xx) * 4 + 3] > 60:
                            n += 1
                if n <= 1:
                    rm.append(o)
        for o in rm:
            px4[o:o + 4] = b'\x00\x00\x00\x00'
        killed += len(rm)
    return killed


def tile(w, h, px4, n, scale, bg=(30, 46, 34)):
    tw, th = int(w * n * scale), int(h * scale)
    out = bytearray(tw * th * 4)
    for i in range(tw * th):
        out[i * 4:i * 4 + 4] = bytes((bg[0], bg[1], bg[2], 255))
    for y in range(th):
        sy = int(y / scale)
        for x in range(tw):
            sx = int(x / scale) % w
            o = (sy * w + sx) * 4
            a = px4[o + 3]
            if a == 0:
                continue
            d = (y * tw + x) * 4
            if a == 255:
                out[d:d + 4] = px4[o:o + 4]
            else:
                t = a / 255
                out[d] = int(px4[o] * t + out[d] * (1 - t))
                out[d + 1] = int(px4[o + 1] * t + out[d + 1] * (1 - t))
                out[d + 2] = int(px4[o + 2] * t + out[d + 2] * (1 - t))
                out[d + 3] = 255
    return tw, th, out


def main(src, out, scale, n=4, up=2):
    w, h, ch, px = pix.load(src)
    px4 = bytearray(px)
    killed = despeckle(w, h, px4)
    print(f'{src} {w}x{h}  清理像素 {killed}')
    clean = os.path.splitext(out)[0] + '_clean.png'
    pix.save(clean, w, h, 4, px4)
    print(f'clean -> {clean}')
    tw, th, tiled = tile(w, h, px4, n, scale)
    # 最近邻放大 up 倍
    if up != 1:
        tw2, th2 = tw * up, th * up
        big = bytearray(tw2 * th2 * 4)
        for y in range(th2):
            sy = y // up
            for x in range(tw2):
                sx = x // up
                o = (sy * tw + sx) * 4
                d = (y * tw2 + x) * 4
                big[d:d + 4] = tiled[o:o + 4]
        tw, th, tiled = tw2, th2, big
    pix.save(out, tw, th, 4, tiled)
    print(f'preview -> {out}  {tw}x{th}  (1 世界像素 = {up} 像素)')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], float(sys.argv[3]),
         int(sys.argv[4]) if len(sys.argv) > 4 else 4,
         int(sys.argv[5]) if len(sys.argv) > 5 else 2)
