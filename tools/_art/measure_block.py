"""量素材的"大像素块"尺寸，用来决定游戏里的缩放倍数。"""
import sys, os, collections

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

SRC = {
    "bridge": r"C:\Users\leader\Downloads\像素藤桥绘制 (2).png",
    "waterfall": r"D:\1751291605\nt_qq\nt_data\Pic\2026-11\Ori\3760851df0fd7e9347939407c3a3e87f.png",
}


def block_estimate(w, h, ch, px, step=4):
    """统计水平方向"同色连续长度"的众数。像素画的块宽会形成明显峰。"""
    hist = collections.Counter()
    for y in range(0, h, step):
        row = y * w * ch
        run = 1
        prev = px[row:row + 3]
        for x in range(1, w):
            i = row + x * ch
            cur = px[i:i + 3]
            if cur == prev:
                run += 1
            else:
                hist[run] += 1
                run = 1
                prev = cur
        hist[run] += 1
    # 只看 2..12 的峰
    top = [(k, v) for k, v in hist.most_common(40) if 2 <= k <= 12]
    top.sort(key=lambda kv: -kv[1])
    return top[:8]


for name, p in SRC.items():
    w, h, ch, px = pix.load(p)
    print(f"[{name}] {w}x{h} ch={ch}")
    peaks = block_estimate(w, h, ch, px)
    for k, v in peaks:
        print(f"    连续长度 {k:2d} 出现 {v:7d} 次")
    # 加权估计
    tot = sum(v for k, v in peaks)
    est = sum(k * v for k, v in peaks) / tot if tot else 0
    print(f"    加权估计块宽 ≈ {est:.2f} px\n")
