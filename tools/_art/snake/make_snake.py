"""抠品红 + 裁边，输出 Boss 蛇本体（保留原始分辨率）。"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import pix
from build_crawl_loop import key_magenta, bbox, crop
from finalize_crawl import despeckle


def run(src, out, scale=1.0):
    w, h, ch, px = pix.load(src)
    px4 = key_magenta(w, h, ch, px)
    x0, y0, x1, y1 = bbox(w, h, px4)
    px4 = crop(w, px4, x0, y0, x1, y1)[2] if False else px4
    nw, nh, px4 = crop(w, px4, x0, y0, x1, y1)
    n = despeckle(nw, nh, px4)
    if scale != 1.0:
        tw, th = int(nw * scale), int(nh * scale)
        r = bytearray(tw * th * 4)
        for y in range(th):
            sy = int(y / scale)
            for x in range(tw):
                sx = int(x / scale)
                o = (sy * nw + sx) * 4
                r[(y * tw + x) * 4:(y * tw + x) * 4 + 4] = px4[o:o + 4]
        nw, nh, px4 = tw, th, r
    pix.save(out, nw, nh, 4, px4)
    print(f'{out}  {nw}x{nh}  bbox=({x0},{y0})-({x1},{y1})  清掉{n}个杂点')


if __name__ == '__main__':
    run(sys.argv[1], sys.argv[2], float(sys.argv[3]) if len(sys.argv) > 3 else 1.0)
