"""
深挖单个 blip：谐波结构、音高走向、波形类型。

用法：python tools/probe_blip.py tools/ref/ref.wav 4801
（最后那个数是 blip 的起始毫秒，从 analyze_ref.py 的表里取）
"""

import math
import struct
import sys
import wave


def load(path):
    w = wave.open(path)
    raw = w.readframes(w.getnframes())
    sr = w.getframerate()
    w.close()
    return [x / 32768.0 for x in struct.unpack("<%dh" % (len(raw) // 2), raw)], sr


def goertzel(x, sr, freq):
    """单频点幅度（Goertzel 算法）。"""
    n = len(x)
    w = 2.0 * math.pi * freq / sr
    c = 2.0 * math.cos(w)
    s0 = s1 = s2 = 0.0
    for v in x:
        s0 = v + c * s1 - s2
        s2, s1 = s1, s0
    return math.sqrt(max(0.0, s1 * s1 + s2 * s2 - c * s1 * s2)) / n * 2.0


def f0_autocorr(x, sr, lo=200.0, hi=4000.0):
    mean = sum(x) / len(x)
    x = [v - mean for v in x]
    n0 = sum(v * v for v in x) or 1e-9
    best, bl = -1e9, 0
    for lag in range(max(2, int(sr / hi)), min(len(x) - 2, int(sr / lo)) + 1):
        s = 0.0
        for i in range(len(x) - lag):
            s += x[i] * x[i + lag]
        s /= n0
        if s > best:
            best, bl = s, lag
    return (sr / bl if bl else 0.0), best


def zero_cross_rate(x, sr):
    """过零数 → 估算主频（脉冲波每周期过零 2 次）。"""
    zc = 0
    for i in range(1, len(x)):
        if (x[i - 1] >= 0) != (x[i] >= 0):
            zc += 1
    return zc / (len(x) / sr) / 2.0


def main(path, start_ms, dur_ms=52):
    x, sr = load(path)
    a = int(sr * start_ms / 1000)
    seg = x[a:a + int(sr * dur_ms / 1000)]

    print(f"片段 {start_ms}ms 起，{len(seg) / sr * 1000:.1f}ms\n")

    f, c = f0_autocorr(seg, sr)
    print(f"自相关基频 : {f:.1f} Hz (相关度 {c:.2f})")
    print(f"过零估计   : {zero_cross_rate(seg, sr):.0f} Hz")

    print("\n谐波幅度（以 788Hz 的倍数扫）：")
    base = 788.0
    mags = []
    for k in range(1, 9):
        fk = base * k
        if fk > sr / 2:
            break
        m = goertzel(seg, sr, fk)
        mags.append((fk, m))
    peak = max(m for _, m in mags) or 1e-9
    for fk, m in mags:
        print(f"  {fk:7.0f} Hz  {m / peak:5.2f}  {'#' * int(m / peak * 40)}")

    print("\n音高走向（切成 4 段各估一次）：")
    q = len(seg) // 4
    for i in range(4):
        part = seg[i * q:(i + 1) * q]
        pf, pc = f0_autocorr(part, sr)
        print(f"  第{i + 1}段 ({i * q / sr * 1000:4.0f}~{(i + 1) * q / sr * 1000:4.0f}ms)"
              f"  {pf:6.1f} Hz  (相关度 {pc:.2f})")

    print("\n波形形态抽样（每 10 个采样一个点，看是否方波）：")
    step = max(1, len(seg) // 120)
    line = ""
    for i in range(0, len(seg), step):
        v = seg[i]
        line += "█" if v > 0.2 else ("▄" if v > 0 else ("▁" if v > -0.2 else " "))
    print("  " + line)


if __name__ == "__main__":
    p = sys.argv[1] if len(sys.argv) > 1 else "tools/ref/ref.wav"
    s = float(sys.argv[2]) if len(sys.argv) > 2 else 4801
    main(p, s)
