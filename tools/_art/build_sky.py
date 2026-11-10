"""从天空原图里分离出「干净天空」和「云」。

思路：天空是纯垂直渐变，所以每一行的中位数颜色就是"这一行本该有的颜色"。
比中位数明显亮、且低饱和的像素判为云；云的 alpha 用亮度差做软过渡（不是硬抠），
这样保留云边缘的半透明，飘起来不会有一圈白边。

产出：
  sky_clean.png   纯渐变天空（横向完全均匀，天衣无缝地平铺）
  sky_haze.png    只抹掉云、保留底部雾山的版本
  cloud_N.png     抠出来的云（带 alpha），拿去做可移动的云层
"""
import sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "bg_src", "sky_src.png")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview_jungle_bg")


def lum(r, g, b):
    return (r * 299 + g * 587 + b * 114) // 1000


def row_medians(w, h, px, pct=0.28):
    """每行的基色。

    不取 50% 中位数，而是取偏暗的 28% 分位：云和底部雾山都是"比天空亮"的东西，
    取中位数时一旦某行里山/云占到一半以上，颜色就会突然跳一档，形成一道横贯全屏的
    色阶台阶。取低分位等于"只信这一行里最像纯天空的那部分"，行与行之间就连续了。
    """
    med = []
    step = max(1, w // 260)
    for y in range(h):
        base = y * w * 3
        lum_list = []
        for x in range(0, w, step):
            i = base + x * 3
            lum_list.append((lum(px[i], px[i + 1], px[i + 2]), x))
        lum_list.sort()
        k = int(len(lum_list) * pct)
        # 取一个小区间的平均，避免单个像素噪声
        lo = max(0, k - 6)
        hi = min(len(lum_list), k + 6)
        acc = [0, 0, 0]
        for _, x in lum_list[lo:hi]:
            i = base + x * 3
            acc[0] += px[i]; acc[1] += px[i + 1]; acc[2] += px[i + 2]
        n = hi - lo
        med.append((acc[0] // n, acc[1] // n, acc[2] // n))
    return med


def median_filter(seq, radius):
    """逐通道中值滤波：专治单行的突变，比均值滤波更保边。"""
    out = []
    n = len(seq)
    for i in range(n):
        lo, hi = max(0, i - radius), min(n, i + radius + 1)
        ch = []
        for c in range(3):
            vals = sorted(seq[k][c] for k in range(lo, hi))
            ch.append(vals[len(vals) // 2])
        out.append(tuple(ch))
    return out


def smooth(seq, radius=3):
    out = []
    n = len(seq)
    for i in range(n):
        acc = [0, 0, 0]
        cnt = 0
        for k in range(max(0, i - radius), min(n, i + radius + 1)):
            c = seq[k]
            acc[0] += c[0]; acc[1] += c[1]; acc[2] += c[2]
            cnt += 1
        out.append((acc[0] // cnt, acc[1] // cnt, acc[2] // cnt))
    return out


def components(w, h, mask, min_area=350):
    """8 邻域连通块，只要 bbox 和面积。用显式栈避免递归爆栈。"""
    seen = bytearray(w * h)
    comps = []
    for start in range(w * h):
        if not mask[start] or seen[start]:
            continue
        stack = [start]
        seen[start] = 1
        area = 0
        x0, y0, x1, y1 = w, h, 0, 0
        while stack:
            p = stack.pop()
            area += 1
            y, x = divmod(p, w)
            if x < x0: x0 = x
            if x > x1: x1 = x
            if y < y0: y0 = y
            if y > y1: y1 = y
            for dy in (-1, 0, 1):
                ny = y + dy
                if ny < 0 or ny >= h:
                    continue
                for dx in (-1, 0, 1):
                    nx = x + dx
                    if nx < 0 or nx >= w:
                        continue
                    q = ny * w + nx
                    if mask[q] and not seen[q]:
                        seen[q] = 1
                        stack.append(q)
        if area >= min_area:
            comps.append((area, x0, y0, x1, y1))
    comps.sort(reverse=True)
    return comps


def main():
    os.makedirs(OUT, exist_ok=True)
    w, h, ch, px = pix.load(SRC)
    assert ch == 3, ch
    med = smooth(median_filter(row_medians(w, h, px), 14), 8)
    print(f"天空 {w}x{h}")
    print(f"  顶部基色 {med[0]}   中段 {med[h//2]}   底部 {med[-1]}")
    # 检查有没有残留台阶（相邻两行色差）
    jumps = []
    for y in range(1, h):
        d = sum(abs(med[y][c] - med[y - 1][c]) for c in range(3))
        if d > 6:
            jumps.append((y, d))
    print(f"  相邻行色差 >6 的位置: {jumps[:10] if jumps else '无'}")

    # 云的软 mask
    alpha = bytearray(w * h)
    t0, t1 = 5, 22          # 亮度差 5 以下完全透明，22 以上完全不透明
    for y in range(h):
        base = y * w * 3
        m = med[y]
        ml = lum(*m)
        for x in range(w):
            i = base + x * 3
            d = lum(px[i], px[i + 1], px[i + 2]) - ml
            if d <= t0:
                continue
            a = (d - t0) * 255 // (t1 - t0)
            alpha[y * w + x] = 255 if a > 255 else a

    n_cloud = sum(1 for a in alpha if a > 120)
    print(f"  云像素占比 {n_cloud / (w * h) * 100:.1f}%")

    # ① 纯渐变
    clean = bytearray(w * h * 3)
    for y in range(h):
        c = med[y]
        base = y * w * 3
        for x in range(w):
            i = base + x * 3
            clean[i] = c[0]; clean[i + 1] = c[1]; clean[i + 2] = c[2]
    pix.save(os.path.join(OUT, "sky_clean.png"), w, h, 3, clean)

    # ② 只抹云，保留底部雾山
    keep_from = int(h * 0.70)     # 70% 以下不动
    haze = bytearray(px)
    for y in range(keep_from):
        base = y * w * 3
        c = med[y]
        for x in range(w):
            p = y * w + x
            a = alpha[p]
            if not a:
                continue
            i = base + x * 3
            inv = 255 - a
            haze[i] = (px[i] * inv + c[0] * a) // 255
            haze[i + 1] = (px[i + 1] * inv + c[1] * a) // 255
            haze[i + 2] = (px[i + 2] * inv + c[2] * a) // 255
    pix.save(os.path.join(OUT, "sky_haze.png"), w, h, 3, haze)

    # ③ 抠云
    mask = bytearray(1 if a > 120 else 0 for a in alpha)
    comps = components(w, h, mask, 350)
    print(f"  连通云块 {len(comps)} 个，取前 4 大的：")
    # 云边缘的 alpha 是从"比天空亮多少"推出来的，天空本身的噪声也会给一点点 alpha，
    # 直接裁 bbox 会连成半透明方框。这里把弱 alpha 直接砍掉再拉伸，
    # 保证云的边缘以外是干净的全透明。
    cut, full = 70, 200
    for idx, (area, x0, y0, x1, y1) in enumerate(comps[:4], 1):
        cw, chh = x1 - x0 + 1, y1 - y0 + 1
        out = bytearray(cw * chh * 4)
        for yy in range(chh):
            for xx in range(cw):
                si = (y0 + yy) * w + (x0 + xx)
                a = alpha[si]
                if a <= cut:
                    continue
                a = min(255, (a - cut) * 255 // (full - cut))
                di = (yy * cw + xx) * 4
                si3 = si * 3
                out[di] = px[si3]
                out[di + 1] = px[si3 + 1]
                out[di + 2] = px[si3 + 2]
                out[di + 3] = a
        pix.save(os.path.join(OUT, f"cloud_{idx}.png"), cw, chh, 4, out)
        print(f"    cloud_{idx}.png  {cw}x{chh}  面积 {area}  @({x0},{y0})")


if __name__ == "__main__":
    main()
