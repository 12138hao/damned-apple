"""把无缝循环藤做成 eden 开头用的 600x80 单张：右端收尖 + 轻微下沉，不用整列淡出。"""
import sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

SRC = 'tools/_art/crawl_loop2/giant_vine_crawl_seamless.png'
OUT = 'assets/eden/giant_vine_arrival.png'
W, H = 600, 80
TAPER = 96          # 右端收尖长度
TIP_MIN = 0.10      # 尖端保留的最小横截面比例
DIP = 10            # 尖端往下沉多少像素（钻进草里）
FADE = 10           # 最末端 10px 再补一点 alpha 淡出


def col_bounds(w, h, px, x):
    top, bot = -1, -1
    for y in range(h):
        if px[(y * w + x) * 4 + 3] > 24:
            if top < 0:
                top = y
            bot = y
    return top, bot


def main():
    w, h, ch, px = pix.load(SRC)
    scale = H / h
    out = bytearray(W * H * 4)

    # 先按列采样出原始 600x80
    base = bytearray(W * H * 4)
    for y in range(H):
        sy = min(h - 1, int(y / scale))
        for x in range(W):
            sx = int(x / scale) % w
            o = (sy * w + sx) * 4
            base[(y * W + x) * 4:(y * W + x) * 4 + 4] = px[o:o + 4]

    # 右端收尖：按列的竖直中心压扁横截面；左段原样拷贝
    for x in range(W):
        if x < W - TAPER:
            for y in range(H):
                d = (y * W + x) * 4
                out[d:d + 4] = base[d:d + 4]
            continue
        t = (x - (W - TAPER)) / TAPER
        t = t * t * (3 - 2 * t)
        top, bot = col_bounds(W, H, base, x)
        if top < 0:
            continue
        yc = (top + bot) * 0.5
        k = 1.0 - (1.0 - TIP_MIN) * t
        for y in range(H):
            d = (y * W + x) * 4
            sy = yc + (y - yc) / k + DIP * t
            syi = int(round(sy))
            if syi < 0 or syi >= H:
                out[d:d + 4] = b'\x00\x00\x00\x00'
            else:
                s = (syi * W + x) * 4
                out[d:d + 4] = base[s:s + 4]

    # 最末端补 alpha 淡出
    for x in range(W - FADE, W):
        f = (W - 1 - x) / FADE
        f = f * f * (3 - 2 * f)
        for y in range(H):
            d = (y * W + x) * 4
            if out[d + 3]:
                out[d + 3] = int(out[d + 3] * f)

    pix.save(OUT, W, H, 4, out)
    print('->', OUT, W, H, ' 收尖', TAPER, 'px 尖端比例', TIP_MIN, '下沉', DIP)


if __name__ == '__main__':
    main()
