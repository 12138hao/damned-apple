"""分析横向藤蔓贴图的可平铺性：列特征自相关找周期 + 首尾列差异。"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix


def col_profile(w, h, ch, px):
    """每列的 (不透明像素数, 顶部y, 底部y, 平均亮度)"""
    prof = []
    for x in range(w):
        n = 0; top = -1; bot = -1; s = 0
        for y in range(h):
            o = (y * w + x) * ch
            a = px[o + 3] if ch == 4 else 255
            if a > 40:
                if top < 0:
                    top = y
                bot = y
                n += 1
                s += px[o] + px[o + 1] + px[o + 2]
        prof.append((n, top, bot, (s / (n * 3)) if n else 0.0))
    return prof


def main(path):
    w, h, ch, px = pix.load(path)
    prof = col_profile(w, h, ch, px)
    print(f"{path}  {w}x{h} ch={ch}")

    # 1) 首尾列直接对比
    a, b = prof[0], prof[-1]
    print(f"  col0      count={a[0]:4d} top={a[1]:4d} bot={a[2]:4d} lum={a[3]:6.1f}")
    print(f"  colW-1    count={b[0]:4d} top={b[1]:4d} bot={b[2]:4d} lum={b[3]:6.1f}")
    print(f"  delta     count={b[0]-a[0]:+4d} top={b[1]-a[1]:+4d} bot={b[2]-a[2]:+4d} lum={b[3]-a[3]:+6.1f}")

    # 2) 用「顶部轮廓」做归一化自相关找周期
    tops = [p[1] if p[1] >= 0 else h for p in prof]
    mean = sum(tops) / w
    var = sum((t - mean) ** 2 for t in tops) / w
    best = []
    for lag in range(120, w // 2):
        s = 0.0
        for x in range(w - lag):
            s += (tops[x] - mean) * (tops[x + lag] - mean)
        c = s / ((w - lag) * var)
        best.append((c, lag))
    best.sort(reverse=True)
    print("  top-contour autocorr top8 (corr, lag):")
    for c, lag in best[:8]:
        print(f"    {c:.4f}  lag={lag:5d}  ({lag/w:.3f} W)")

    # 3) 全列向量相关（用 count 更稳）
    cnts = [p[0] for p in prof]
    m2 = sum(cnts) / w
    v2 = sum((c - m2) ** 2 for c in cnts) / w
    best2 = []
    for lag in range(120, w // 2):
        s = 0.0
        for x in range(w - lag):
            s += (cnts[x] - m2) * (cnts[x + lag] - m2)
        best2.append((s / ((w - lag) * v2), lag))
    best2.sort(reverse=True)
    print("  thickness autocorr top8:")
    for c, lag in best2[:8]:
        print(f"    {c:.4f}  lag={lag:5d}  ({lag/w:.3f} W)")


if __name__ == "__main__":
    main(sys.argv[1])
