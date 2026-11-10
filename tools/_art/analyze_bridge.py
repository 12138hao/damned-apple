"""分析藤桥的桥面厚度、柱子位置与"一节"的宽度。"""
import sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

SRC = r"C:\Users\leader\Downloads\像素藤桥绘制 (2).png"


def runs(vals, pred):
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
    w, h, ch, px = pix.load(SRC)
    W4 = w * 4

    # 每行不透明像素数
    rows = []
    for y in range(h):
        base = y * W4
        n = 0
        for x in range(w):
            if px[base + x * 4 + 3] > 40:
                n += 1
        rows.append(n)

    print("行覆盖 >> 找桥面下沿")
    for y in range(95, 260, 5):
        print(f"  y={y:4d} 覆盖={rows[y]:5d}  ({rows[y]/w*100:5.1f}%)")

    # 桥面下沿 = 覆盖率从接近满宽掉下来的第一行
    full = [y for y in range(h) if rows[y] > w * 0.92]
    print(f"  接近满宽的行: {full[0]}..{full[-1]}  共 {len(full)} 行")

    deck_bottom = full[-1]
    print(f"  桥面下沿 y≈{deck_bottom}")

    # 桥面以下，逐列覆盖率 → 柱子
    y0 = deck_bottom + 3
    cols = []
    for x in range(w):
        n = 0
        for y in range(y0, h):
            if px[y * W4 + x * 4 + 3] > 40:
                n += 1
        cols.append(n)
    print(f"  桥面以下(y>{y0})列覆盖: min={min(cols)} max={max(cols)}")
    pillars = runs(cols, lambda v: v > 0)
    print(f"  柱子列区间({len(pillars)} 根):")
    for a, b in pillars:
        print(f"    x {a}..{b}  宽 {b - a + 1}")
    gaps = runs(cols, lambda v: v == 0)
    print(f"  空隙列区间({len(gaps)} 个): {gaps[:10]}")

    if len(pillars) >= 2:
        centers = [(a + b) // 2 for a, b in pillars]
        print(f"  柱心: {centers}")
        print(f"  柱心间距: {[centers[i+1]-centers[i] for i in range(len(centers)-1)]}")


if __name__ == "__main__":
    main()
