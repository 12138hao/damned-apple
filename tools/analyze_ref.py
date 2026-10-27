"""
分析参考音频里的「哒哒」声，反推它的真实参数。

纯标准库。输出：
  - 每个 blip 的起止时间、时长、峰值
  - 基频估计（自相关法）
  - 相邻 blip 的间隔
  - 第一个 blip 的包络形状（判断是硬起硬收还是软衰减）
"""

import struct
import sys
import wave


def load(path):
    w = wave.open(path)
    n = w.getnframes()
    raw = w.readframes(n)
    w.close()
    s = struct.unpack("<%dh" % (len(raw) // 2), raw)
    return [x / 32768.0 for x in s], w.getframerate() if hasattr(w, "getframerate") else 44100


def detect_blips(x, sr, win_ms=3.0, thresh_ratio=0.06):
    """按能量找 blip 段，返回 [(start, end)] 采样下标。"""
    win = max(1, int(sr * win_ms / 1000))
    frames = len(x) // win

    energy = []
    for i in range(frames):
        chunk = x[i * win:(i + 1) * win]
        energy.append(sum(c * c for c in chunk) / win)

    peak = max(energy) if energy else 0.0
    thresh = peak * thresh_ratio

    segs = []
    in_seg = False
    start = 0
    gap = 0            # 连续静音帧计数，用来合并碎片
    for i, e in enumerate(energy):
        if e > thresh:
            if not in_seg:
                in_seg = True
                start = i
            gap = 0
        else:
            if in_seg:
                gap += 1
                if gap >= 3:          # 连续 9ms 静音才算结束
                    segs.append((start * win, min(len(x), (i - gap + 1) * win)))
                    in_seg = False
    if in_seg:
        segs.append((start * win, min(len(x), frames * win)))

    # 过滤掉过短的（<8ms）
    return [(a, b) for a, b in segs if b - a > int(sr * 0.008)]


def fundamental(x, sr, lo=300.0, hi=3000.0):
    """自相关估基频，返回 (频率, 相关度)。"""
    if len(x) < 64:
        return 0.0, 0.0
    # 取中段，避开起收音的过渡
    a = len(x) // 4
    seg = x[a:a + min(len(x) - a, int(sr * 0.012))]
    if len(seg) < 64:
        return 0.0, 0.0

    mean = sum(seg) / len(seg)
    seg = [v - mean for v in seg]

    best_lag, best_val = 0, -1e9
    lag_min = max(2, int(sr / hi))
    lag_max = min(len(seg) - 2, int(sr / lo))

    norm0 = sum(v * v for v in seg) or 1e-9
    for lag in range(lag_min, lag_max + 1):
        s = 0.0
        for i in range(len(seg) - lag):
            s += seg[i] * seg[i + lag]
        s /= norm0
        if s > best_val:
            best_val, best_lag = s, lag

    if best_lag <= 0:
        return 0.0, 0.0
    return sr / best_lag, best_val


def envelope(x, sr, step_ms=2.0, limit_ms=120.0):
    """打印前 limit_ms 的包络，判断起音/衰减形态。"""
    step = max(1, int(sr * step_ms / 1000))
    n = min(len(x), int(sr * limit_ms / 1000))
    peak = max((abs(v) for v in x[:n]), default=1e-9)
    out = []
    for i in range(0, n, step):
        chunk = x[i:i + step]
        p = max((abs(v) for v in chunk), default=0.0)
        out.append((i / sr * 1000.0, p / peak if peak else 0.0))
    return out


def main(path):
    x, sr = load(path)
    print(f"采样率 {sr}  时长 {len(x) / sr:.2f}s  峰值 {max(abs(v) for v in x):.3f}\n")

    segs = detect_blips(x, sr)
    print(f"检测到 {len(segs)} 个 blip\n")

    print(f"{'#':>3} {'开始ms':>8} {'时长ms':>7} {'间隔ms':>7} {'基频Hz':>7} {'相关度':>6} {'峰值':>5}")
    prev_end = None
    for i, (a, b) in enumerate(segs[:40]):
        seg = x[a:b]
        f, conf = fundamental(seg, sr)
        dur = (b - a) / sr * 1000
        gap = (a - prev_end) / sr * 1000 if prev_end is not None else 0
        peak = max(abs(v) for v in seg)
        print(f"{i:>3} {a / sr * 1000:>8.1f} {dur:>7.1f} {gap:>7.1f} {f:>7.0f} {conf:>6.2f} {peak:>5.2f}")
        prev_end = b

    if len(segs) > 1:
        gaps = [(segs[i + 1][0] - segs[i][1]) / sr * 1000 for i in range(min(len(segs) - 1, 30))]
        starts = [(segs[i + 1][0] - segs[i][0]) / sr * 1000 for i in range(min(len(segs) - 1, 30))]
        print(f"\n静音间隔 中位数 {sorted(gaps)[len(gaps) // 2]:.1f}ms")
        print(f"起音间距 中位数 {sorted(starts)[len(starts) // 2]:.1f}ms  "
              f"→ 约 {1000 / (sorted(starts)[len(starts) // 2] or 1):.0f} 声/秒")

    print("\n第一个 blip 的包络（每 2ms，相对峰值）：")
    for t, v in envelope(x[segs[0][0]:segs[0][1]], sr):
        bar = "#" * int(v * 40)
        print(f"  {t:5.1f}ms {v:4.2f} {bar}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "tools/ref/ref.wav")
