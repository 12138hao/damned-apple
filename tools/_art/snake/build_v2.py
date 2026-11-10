"""把 v2 的四张（idle / open / dead / tail）统一抠图、对齐裁切、缩放成游戏用尺寸。

关键：三张蛇身必须用**同一个裁切矩形 + 同一个缩放**，否则换图时盘绕会错位。
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import pix
from build_crawl_loop import key_magenta, bbox
from finalize_crawl import despeckle
from extract_snake import resize_bilinear

V2 = os.path.join(HERE, 'v2')
REF = (1024, 1536)


def load_norm(path):
    w, h, ch, px = pix.load(path)
    px4 = key_magenta(w, h, ch, px)
    if (w, h) != REF:
        px4 = resize_bilinear(w, h, 4, px4, REF[0], REF[1])
        w, h = REF
    return w, h, px4


def crop_rect(w, h, px4, rect):
    x0, y0, x1, y1 = rect
    nw, nh = x1 - x0, y1 - y0
    out = bytearray(nw * nh * 4)
    for y in range(nh):
        src = ((y + y0) * w + x0) * 4
        out[y * nw * 4:(y + 1) * nw * 4] = px4[src:src + nw * 4]
    return nw, nh, out


def scale_to_width(w, h, px4, tw):
    th = max(1, round(h * tw / w))
    return tw, th, resize_bilinear(w, h, 4, px4, tw, th)


def main():
    sheets = {}
    for name in ('idle', 'open', 'dead'):
        d = os.path.join(V2) if name == 'idle' else os.path.join(V2, name)
        want = 'seedream-20261110-080350-02.png' if name == 'idle' else None
        once = [f for f in os.listdir(d) if f.endswith('-01.png')]
        p = os.path.join(d, want) if want else os.path.join(d, sorted(once)[0])
        sheets[name] = load_norm(p)
        print(name, '->', p)

    # ① 三张的并集 bbox（保证同一裁切窗口能装下所有姿态）
    ux0, uy0, ux1, uy1 = 10 ** 9, 10 ** 9, -1, -1
    for name, (w, h, px4) in sheets.items():
        x0, y0, x1, y1 = bbox(w, h, px4)
        print('  bbox', name, x0, y0, x1, y1)
        ux0, uy0 = min(ux0, x0), min(uy0, y0)
        ux1, uy1 = max(ux1, x1), max(uy1, y1)
    # 往上留一点头空间，往下留一点尾巴空间
    rect = (max(0, ux0 - 6), max(0, uy0 - 6), min(REF[0], ux1 + 6), min(REF[1], uy1 + 6))
    print('统一裁切', rect, '尺寸', rect[2] - rect[0], rect[3] - rect[1])

    TARGET_W = 172
    for name, (w, h, px4) in sheets.items():
        cw, chh, c = crop_rect(w, h, px4, rect)
        n = despeckle(cw, chh, c)
        nw, nh, s = scale_to_width(cw, chh, c, TARGET_W)
        out = os.path.join('assets', 'eden', f'snake_body_{name}.png')
        pix.save(out, nw, nh, 4, s)
        print(f'-> {out}  {nw}x{nh}  清{n}')

    # ② 尾巴单独处理
    tailp = os.path.join(V2, 'tail', sorted(f for f in os.listdir(os.path.join(V2, 'tail')) if f.endswith('-01.png'))[0])
    w, h, ch, px = pix.load(tailp)
    t4 = key_magenta(w, h, ch, px)
    x0, y0, x1, y1 = bbox(w, h, t4)
    t4 = crop_rect(w, h, t4, (x0, y0, x1, y1))[2]
    cw, chh = x1 - x0, y1 - y0
    despeckle(cw, chh, t4)
    nw, nh, s = scale_to_width(cw, chh, t4, 470)
    pix.save('assets/eden/snake_tail.png', nw, nh, 4, s)
    print(f'-> assets/eden/snake_tail.png  {nw}x{nh}')


if __name__ == '__main__':
    main()
