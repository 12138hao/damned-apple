"""用自相关找藤桥的横向重复周期，用来把桥面切成"整数节"。"""
import sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

SRC = r"C:\Users\leader\Downloads\像素藤桥绘制 (2).png"


def profile_row(w, px, W4, y):
    row = y * W4
    vals = []
    for x in range(w):
        i = row + x * 4
        vals.append((px[i] * 299 + px[i + 1] * 587 + px[i + 2] * 114) // 1000)
    return vals


def autocorr_score(v, lag, x0=900, x1=1950):
    s = 0
    n = 0
    for x in range(x0, x1):
        d = v[x] - v[x + lag]
        s += d * d
        n += 1
    return s / n


def main():
    w, h, ch, px = pix.load(SRC)
    W4 = w * 4
    for y in (125, 140, 155, 220, 260):
        v = profile_row(w, px, W4, y)
        best = []
        for lag in range(30, 260):
            best.append((autocorr_score(v, lag), lag))
        best.sort()
        print(f"y={y:3d}  最匹配的 5 个周期: " + ", ".join(f"{lag}({sc:.0f})" for sc, lag in best[:5]))

    # 在候选周期上验证整段是否吻合
    print("\n用长跨度验证：把 x=900..1950 与 x-lag 对比，看差别")
    v = profile_row(w, px, W4, 140)
    for lag in (52, 57, 104, 114, 171):
        sc = autocorr_score(v, lag)
        print(f"  lag={lag:3d}  平均平方差 {sc:7.1f}")


if __name__ == "__main__":
    main()
