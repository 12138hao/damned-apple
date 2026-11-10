"""把 Seedream 出的长条藤蔓做成真正首尾相接的循环贴图。

流程：品红抠图 -> 去溢色 -> 裁 bbox -> 在 [W-900, W-64] 里找与开头最匹配的循环长度 L
      -> 在接缝处做 B 像素交叉淡化 -> 输出 loop png + 三联平铺预览。
"""
import sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

MAG = (255, 0, 255)


def key_magenta(w, h, ch, px, d_in=70.0, d_out=150.0):
    """按到品红的 RGB 距离生成 alpha，并做全局去溢色。返回 RGBA bytearray。"""
    out = bytearray(w * h * 4)
    for i in range(w * h):
        o = i * ch
        r, g, b = px[o], px[o + 1], px[o + 2]
        d = math.sqrt((r - 255) ** 2 + g * g + (b - 255) ** 2)
        if d <= d_in:
            a = 0
        elif d >= d_out:
            a = 255
        else:
            a = int(255 * (d - d_in) / (d_out - d_in))
        # 品红残留（暗品红、边缘半透明带）判透明
        if (min(r, b) - g) > 30 and min(r, b) > 28:
            a = 0
        if a == 0:
            out[i * 4:i * 4 + 4] = b'\x00\x00\x00\x00'
            continue
        # 全局去溢色：把「红蓝比绿多出来的那部分」按比例压回去
        spill = min(r, b) - g
        if spill > 0:
            k = 0.78
            r = max(0, int(r - spill * k))
            b = max(0, int(b - spill * k))
        out[i * 4:i * 4 + 4] = bytes((r, g, b, a))
    return out


def bbox(w, h, px4):
    x0, x1, y0, y1 = w, -1, h, -1
    for y in range(h):
        for x in range(w):
            if px4[(y * w + x) * 4 + 3] > 24:
                if x < x0: x0 = x
                if x > x1: x1 = x
                if y < y0: y0 = y
                if y > y1: y1 = y
    if x1 < 0:
        raise SystemExit('抠图后全透明，检查背景色')
    return x0, y0, x1 + 1, y1 + 1


def crop(w, px4, x0, y0, x1, y1):
    nw, nh = x1 - x0, y1 - y0
    out = bytearray(nw * nh * 4)
    for y in range(nh):
        src = ((y + y0) * w + x0) * 4
        out[y * nw * 4:(y + 1) * nw * 4] = px4[src:src + nw * 4]
    return nw, nh, out


def col_feat(w, h, px4, x, step):
    """一列的降采样特征（含 alpha，用预乘让透明处不干扰）。"""
    f = []
    for y in range(0, h, step):
        o = (y * w + x) * 4
        a = px4[o + 3]
        f.append(px4[o] * a / 255)
        f.append(px4[o + 1] * a / 255)
        f.append(px4[o + 2] * a / 255)
        f.append(a)
    return f


def find_loop(w, h, px4, band=110, min_keep=0.55):
    step = 4
    feats = [col_feat(w, h, px4, x, step) for x in range(w)]
    lo = int(w * min_keep)
    scored = []
    for L in range(lo, w - band):
        cost = 0.0
        for x in range(0, band, 2):
            fa, fb = feats[x], feats[L + x]
            s = 0.0
            for i in range(len(fa)):
                d = fa[i] - fb[i]
                s += d * d
            cost += s
        scored.append((cost / ((band // 2) * len(fa)), L))
    scored.sort()
    return scored


def blend_seam(w, h, px4, L, B):
    """输出宽度 L 的循环图：前 B 列与 [L, L+B) 交叉淡化，权重 smoothstep。"""
    out = bytearray(L * h * 4)
    for y in range(h):
        row_s = y * w
        row_d = y * L
        for x in range(L):
            o = (row_s + x) * 4
            d = (row_d + x) * 4
            if x < B:
                t = x / B
                t = t * t * (3.0 - 2.0 * t)     # smoothstep
                o2 = (row_s + L + x) * 4
                a1, a2 = px4[o + 3], px4[o2 + 3]
                w1, w2 = t, 1.0 - t
                ta = a1 * w1 + a2 * w2
                if ta <= 0.5:
                    out[d:d + 4] = b'\x00\x00\x00\x00'
                    continue
                r = (px4[o] * a1 * w1 + px4[o2] * a2 * w2) / ta
                g = (px4[o + 1] * a1 * w1 + px4[o2 + 1] * a2 * w2) / ta
                b = (px4[o + 2] * a1 * w1 + px4[o2 + 2] * a2 * w2) / ta
                out[d:d + 4] = bytes((int(r), int(g), int(b), int(ta)))
            else:
                out[d:d + 4] = px4[o:o + 4]
    return out


def save_tiled(w, h, px4, n, path, bg=(26, 40, 30), scale=1):
    tw = w * n
    out = bytearray(tw * h * 4)
    for i in range(tw * h):
        out[i * 4:i * 4 + 4] = bytes((bg[0], bg[1], bg[2], 255))
    for k in range(n):
        ox = k * w
        for y in range(h):
            for x in range(w):
                o = (y * w + x) * 4
                a = px4[o + 3]
                if a == 0:
                    continue
                d = ((y * tw) + ox + x) * 4
                if a == 255:
                    out[d:d + 4] = px4[o:o + 4]
                else:
                    t = a / 255
                    out[d] = int(px4[o] * t + out[d] * (1 - t))
                    out[d + 1] = int(px4[o + 1] * t + out[d + 1] * (1 - t))
                    out[d + 2] = int(px4[o + 2] * t + out[d + 2] * (1 - t))
                    out[d + 3] = 255
    if scale != 1:
        sw, sh = int(tw * scale), int(h * scale)
        res = bytearray(sw * sh * 4)
        for y in range(sh):
            sy = int(y / scale)
            for x in range(sw):
                sx = int(x / scale)
                o = (sy * tw + sx) * 4
                res[(y * sw + x) * 4:(y * sw + x) * 4 + 4] = out[o:o + 4]
        tw, h, out = sw, sh, res
    pix.save(path, tw, h, 4, out)
    return tw, h


def seam_window(L, h, loop, half=340):
    """把循环图尾部 half 列 + 头部 half 列拼起来（接缝正好在正中），1:1。"""
    zw = half * 2
    out = bytearray(zw * h * 4)
    for y in range(h):
        for x in range(zw):
            sx = (L - half + x) % L
            o = (y * L + sx) * 4
            out[(y * zw + x) * 4:(y * zw + x) * 4 + 4] = loop[o:o + 4]
    return zw, out


def main(src, outdir, L=None, B=72):
    w, h, ch, px = pix.load(src)
    print(f'src {src} {w}x{h} ch={ch}')
    px4 = key_magenta(w, h, ch, px)
    x0, y0, x1, y1 = bbox(w, h, px4)
    print(f'bbox {x0},{y0} - {x1},{y1}  ({x1-x0}x{y1-y0})')
    w, h, px4 = crop(w, px4, x0, y0, x1, y1)

    scored = find_loop(w, h, px4, band=110)
    print('候选循环长度 top10（cost 越小越接得上）:')
    for c, l in scored[:10]:
        print(f'   L={l:5d} ({l/w*100:5.1f}% W)  cost={c:8.1f}')
    if L is None:
        L = scored[0][1]
    print(f'选用 L={L}  B={B}')

    loop = blend_seam(w, h, px4, L, B)
    base = os.path.splitext(os.path.basename(src))[0]
    lp = os.path.join(outdir, base + f'_loop{L}.png')
    pix.save(lp, L, h, 4, loop)
    print('loop ->', lp, f'{L}x{h}')

    zw, win = seam_window(L, h, loop)
    zp = os.path.join(outdir, base + f'_seam{L}.png')
    tw2, th2 = save_tiled(zw, h, win, 1, zp, scale=1.0)
    print('seamwin ->', zp, f'{tw2}x{th2}')

    tp = os.path.join(outdir, base + f'_tile3_{L}.png')
    tw, th = save_tiled(L, h, loop, 3, tp, scale=0.42)
    print('tile3 ->', tp, f'{tw}x{th}')
    return L, h


if __name__ == '__main__':
    a = sys.argv[1:]
    L = int(a[2]) if len(a) > 2 else None
    B = int(a[3]) if len(a) > 3 else 72
    main(a[0], a[1] if len(a) > 1 else '.', L, B)
