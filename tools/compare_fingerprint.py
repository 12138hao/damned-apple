"""
对比两段音频的频谱指纹：参考音频里的原始「哒」 vs 我处理后的 blip.wav。

做三件事：
  1. 各自扫出频谱峰（Goertzel，5Hz 步长）
  2. 找出「频率缩放多少倍时两个频谱最相似」，以及相似度
  3. 用一段无关音频做对照，证明这个相似度不是随便就能拿到的

纯标准库。
"""

import math
import os
import struct
import sys
import wave

SR = 44100
F_LO, F_HI, F_STEP = 150.0, 5000.0, 5.0


def load(path):
    w = wave.open(path)
    raw = w.readframes(w.getnframes())
    sr = w.getframerate()
    w.close()
    return [x / 32768.0 for x in struct.unpack("<%dh" % (len(raw) // 2), raw)], sr


def goertzel(x, sr, freq):
    n = len(x)
    w = 2.0 * math.pi * freq / sr
    c = 2.0 * math.cos(w)
    s0 = s1 = s2 = 0.0
    for v in x:
        s0 = v + c * s1 - s2
        s2, s1 = s1, s0
    return math.sqrt(max(0.0, s1 * s1 + s2 * s2 - c * s1 * s2)) / n * 2.0


def spectrum(x, sr):
    """返回 [(频率, 幅度)]，幅度归一化到峰值 1。"""
    pts = []
    f = F_LO
    while f <= F_HI:
        pts.append((f, goertzel(x, sr, f)))
        f += F_STEP
    peak = max(m for _, m in pts) or 1e-9
    return [(f, m / peak) for f, m in pts]


def peaks(spec, count=6, min_ratio=0.12):
    """找局部极大值，按幅度取前 count 个。"""
    out = []
    for i in range(2, len(spec) - 2):
        f, m = spec[i]
        if m < min_ratio:
            continue
        if m >= spec[i - 1][1] and m >= spec[i + 1][1] and m >= spec[i - 2][1] and m >= spec[i + 2][1]:
            out.append((f, m))
    out.sort(key=lambda t: -t[1])
    return out[:count]


def match_scale(spec_a, spec_b, lo=0.65, hi=1.15, step=0.005):
    """找缩放系数 s，使 spec_b(s*f) 与 spec_a(f) 最相似。返回 (s, 相关系数)。"""
    best = (0.0, -2.0)
    dict_b = {round(f, 3): m for f, m in spec_b}
    s = lo
    while s <= hi:
        xs, ys = [], []
        for f, m in spec_a:
            fb = round(f * s, 3)
            if fb in dict_b:
                xs.append(m)
                ys.append(dict_b[fb])
        if len(xs) > 100:
            n = len(xs)
            mx, my = sum(xs) / n, sum(ys) / n
            num = sum((a - mx) * (b - my) for a, b in zip(xs, ys))
            dx = math.sqrt(sum((a - mx) ** 2 for a in xs))
            dy = math.sqrt(sum((b - my) ** 2 for b in ys))
            r = num / (dx * dy) if dx * dy else 0.0
            if r > best[1]:
                best = (s, r)
        s += step
    return best


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ref, sr = load(os.path.join(here, "ref", "ref.wav"))
    a = int(sr * 4799.0 / 1000)
    orig = ref[a:a + int(sr * 56.0 / 1000)]          # 参考里的原始「哒」
    mine, _ = load(os.path.join(here, "..", "assets", "audio", "sfx", "blip.wav"))
    ctrl, _ = load(os.path.join(here, "..", "assets", "audio", "sfx", "charge.wav"))

    print(f"参考原始  时长 {len(orig) / sr * 1000:.1f}ms")
    print(f"我的 blip 时长 {len(mine) / sr * 1000:.1f}ms")
    print(f"时长比     {len(mine) / len(orig):.3f}\n")

    sp_orig = spectrum(orig, sr)
    sp_mine = spectrum(mine, sr)
    sp_ctrl = spectrum(ctrl[:len(mine) * 2], sr)

    print("参考原始的谱峰：")
    for f, m in peaks(sp_orig):
        print(f"  {f:7.0f} Hz   幅度 {m:.2f}")
    print("\n我的 blip 的谱峰：")
    for f, m in peaks(sp_mine):
        print(f"  {f:7.0f} Hz   幅度 {m:.2f}")

    s1, r1 = match_scale(sp_orig, sp_mine)
    s2, r2 = match_scale(sp_orig, sp_ctrl)

    print(f"\n【参考 vs 我的】最相似缩放 {s1:.3f}，相关系数 {r1:.3f}")
    print(f"【参考 vs 无关音效 jump】最相似缩放 {s2:.3f}，相关系数 {r2:.3f}")


if __name__ == "__main__":
    main()
