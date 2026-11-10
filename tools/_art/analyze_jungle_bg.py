"""分析新丛林背景素材的结构：藤桥柱距/桥面高度、瀑布分层、天空渐变。

只做测量，不产出成品。跑法：
    python tools/_art/analyze_jungle_bg.py
"""
import sys, os, struct

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

DL = r"C:\Users\leader\Downloads"
QQ = r"D:\1751291605\nt_qq\nt_data\Pic\2026-11\Ori"


def col_alpha_profile(w, h, px):
    """每列的不透明像素数。用来找藤桥柱子之间的空隙。"""
    prof = [0] * w
    for x in range(w):
        n = 0
        base = x * 4
        for y in range(h):
            if px[base + y * w * 4 + 3] > 40:
                n += 1
        prof[x] = n
    return prof


def row_alpha_profile(w, h, px):
    prof = [0] * h
    for y in range(h):
        n = 0
        row = y * w * 4
        for x in range(w):
            if px[row + x * 4 + 3] > 40:
                n += 1
        prof[y] = n
    return prof


def runs(vals, pred):
    """连续的满足 pred 的区间 [(start,end), ...]（闭区间）"""
    out, s = [], None
    for i, v in enumerate(vals):
        ok = pred(v)
        if ok and s is None:
            s = i
        elif not ok and s is not None:
            out.append((s, i - 1))
            s = None
    if s is not None:
        out.append((s, len(vals) - 1))
    return out


def main():
    print("=" * 60)
    print("1) 藤桥  像素藤桥绘制 (2).png")
    p = os.path.join(DL, "像素藤桥绘制 (2).png")
    w, h, ch, px = pix.load(p)
    print(f"   {w}x{h} ch={ch}")
    cp = col_alpha_profile(w, h, px)
    rp = row_alpha_profile(w, h, px)
    print(f"   列覆盖: min={min(cp)} max={max(cp)}")
    empty_cols = runs(cp, lambda v: v == 0)
    print(f"   全透明列区间（柱子空隙）: {empty_cols}")
    solid_cols = runs(cp, lambda v: v > 0)
    print(f"   有内容的列区间: {solid_cols[:12]}")
    print(f"   行覆盖: 前 20 行 = {rp[:20]}")
    print(f"   行覆盖: 后 40 行 = {rp[-40:]}")
    solid_rows = runs(rp, lambda v: v > 0)
    print(f"   有内容的行区间: {solid_rows[:8]}")

    print("=" * 60)
    print("2) 瀑布  3760851df0fd7e9347939407c3a3e87f.png")
    p = os.path.join(QQ, "3760851df0fd7e9347939407c3a3e87f.png")
    w2, h2, ch2, px2 = pix.load(p)
    print(f"   {w2}x{h2} ch={ch2}")
    # 每行平均亮度，找"水花区"（底部白色多）
    for y in range(0, h2, max(1, h2 // 20)):
        row = y * w2 * ch2
        s = 0
        n = min(w2, 200)
        for x in range(0, w2, max(1, w2 // n)):
            i = row + x * ch2
            s += (px2[i] * 299 + px2[i + 1] * 587 + px2[i + 2] * 114) // 1000
        print(f"   y={y:4d} 平均亮度={s / n:6.1f}")

    print("=" * 60)
    print("3) 天空  尺寸与顶部/底部颜色")
    sky = r"C:\Users\leader\.workbuddy-ai\clipboard-images\clipboard-2026-11-06T06-55-11-034Z-0a80f04e.jpg"
    print("   源:", sky, " 需要先 ffmpeg 转 png 才能读")


if __name__ == "__main__":
    main()
