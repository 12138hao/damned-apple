"""量藤桥柱子的精确位置与跨度（只看桥面以下的纯柱子区域）。"""
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
            out.append((s, i - 1)); s = None
    if s is not None:
        out.append((s, len(vals) - 1))
    return out


def main():
    w, h, ch, px = pix.load(SRC)
    W4 = w * 4

    for band in [(420, 815), (700, 815), (760, 815)]:
        y0, y1 = band
        cols = []
        for x in range(w):
            n = 0
            for y in range(y0, y1):
                if px[y * W4 + x * 4 + 3] > 40:
                    n += 1
            cols.append(n)
        pr = runs(cols, lambda v: v > 0)
        print(f"y={y0}..{y1}  有内容的列段 {len(pr)} 个")
        for a, b in pr:
            print(f"    x {a:4d}..{b:4d}  宽 {b-a+1:4d}  柱心 {(a+b)//2}")
        cs = [(a + b) // 2 for a, b in pr]
        if len(cs) > 1:
            print(f"    间距: {[cs[i+1]-cs[i] for i in range(len(cs)-1)]}")
        print()

    # 桥面区域横向是否周期：取两段做相关性对比
    print("桥面(195..281)左右端对比：")
    for y in (200, 240, 280):
        row = y * W4
        diffs = [abs(px[row + x*4] - px[row + (x + w - 340)*4]) for x in range(340)]
        print(f"  y={y}: 首340列 vs 末340列 平均色差 {sum(diffs)/len(diffs):.1f}")


if __name__ == "__main__":
    main()
