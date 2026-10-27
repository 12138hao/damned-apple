"""
在「接近原版」的基础上削刺耳。

刺耳的物理原因：四次谐波 2712 Hz 正好落在人耳最敏感的 2~4 kHz。
但它也是「玻璃感」的来源，所以不能简单删掉，只能用不同手法把它变柔。

五种思路：
  soft_a  单纯降幅度       —— 最直接，代价是玻璃感变淡
  soft_b  双谐波微失谐      —— 两条相差 1.8% 的谐波互相干涉，听感变成「闪」而不是「刺」
  soft_c  整体降调          —— 基频降到 600，谐波跟着降到 2400，离开最敏感区
  soft_d  缩短谐波延音      —— 保留亮度，但让它快点消失，刺感不持续
  soft_e  窄带凹陷          —— 只在 2.4~3.0 kHz 挖一个坑，其余频段不动

基础参数沿用 near_3 那一档（起音 7ms、下滑 10%、谐波延音 ×2.5、50ms）。
"""

import math
import os
import random
import struct
import wave

SR = 44100
BIT = 32767
GAP_MS = 68.0
COUNT = 14

ATK_MS = 7.0
GLIDE = 0.10
TAU1_MS = 55.0
SUSTAIN = 0.85
DUR_MS = 50

# name: (基频, 谐波幅度, 谐波延音倍数, 失谐比例, 凹陷中心Hz, 凹陷强度0~1)
VARIANTS = {
    "soft_a": (678.0, 0.32, 2.5, 0.000, 0.0, 0.0),
    "soft_b": (678.0, 0.34, 2.5, 0.018, 0.0, 0.0),
    "soft_c": (600.0, 0.45, 2.5, 0.000, 0.0, 0.0),
    "soft_d": (678.0, 0.45, 1.5, 0.000, 0.0, 0.0),
    "soft_e": (678.0, 0.50, 2.5, 0.000, 2700.0, 0.85),
}


def notch(samples, center, q=1.2):
    """双二阶带阻，在 center 附近挖一个坑。"""
    w0 = 2.0 * math.pi * center / SR
    alpha = math.sin(w0) / (2.0 * q)
    b0, b1, b2 = 1.0, -2.0 * math.cos(w0), 1.0
    a0, a1, a2 = 1.0 + alpha, -2.0 * math.cos(w0), 1.0 - alpha

    out = []
    x1 = x2 = y1 = y2 = 0.0
    for x in samples:
        y = (b0 / a0) * x + (b1 / a0) * x1 + (b2 / a0) * x2 \
            - (a1 / a0) * y1 - (a2 / a0) * y2
        out.append(y)
        x2, x1 = x1, x
        y2, y1 = y1, y
    return out


def mix(clean, filtered, amount):
    return [c * (1 - amount) + f * amount for c, f in zip(clean, filtered)]


def render(f0, harm_amp, harm_tau, detune, notch_hz, notch_amt):
    n = max(16, int(SR * DUR_MS / 1000))
    atk = max(2, int(SR * ATK_MS / 1000))
    rel = max(2, int(SR * 0.004))
    tau1 = TAU1_MS / 1000.0
    tau4 = tau1 * harm_tau

    ph1 = ph4a = ph4b = 0.0
    out = []

    for i in range(n):
        t = i / SR
        prog = i / n

        if prog < 0.7:
            f = f0
        else:
            k = (prog - 0.7) / 0.3
            f = f0 * (1.0 - GLIDE * k * k)

        ph1 += 2.0 * math.pi * f / SR
        ph4a += 2.0 * math.pi * f * 4.0 / SR
        ph4b += 2.0 * math.pi * f * 4.0 * (1.0 + detune) / SR

        a = i / atk if i < atk else 1.0
        e1 = SUSTAIN + (1.0 - SUSTAIN) * math.exp(-t / tau1)
        e4 = SUSTAIN + (1.0 - SUSTAIN) * math.exp(-t / tau4)
        tail = min(1.0, (n - i) / rel)
        base = a * tail

        body = math.sin(ph1) * e1
        if detune > 0:
            body += harm_amp * 0.5 * (math.sin(ph4a) + math.sin(ph4b)) * e4
        else:
            body += harm_amp * math.sin(ph4a) * e4
        body *= base / (1.0 + harm_amp)

        out.append(body)

    if notch_hz > 0:
        out = mix(out, notch(out, notch_hz), notch_amt)

    peak = max(abs(v) for v in out) or 1e-9
    return [v / peak * 0.85 for v in out]


def resample(samples, factor):
    n_out = max(2, int(len(samples) * factor))
    out = []
    last = len(samples) - 1
    for i in range(n_out):
        pos = i / factor
        i0 = int(pos)
        frac = pos - i0
        i1 = min(i0 + 1, last)
        out.append(samples[min(i0, last)] * (1 - frac) + samples[i1] * frac)
    return out


def write_wav(path, samples):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(
            struct.pack("<h", int(max(-1.0, min(1.0, s)) * BIT)) for s in samples))


def make_seq(one, count=COUNT, gap_ms=GAP_MS):
    step = len(one) + int(SR * gap_ms / 1000)
    total = step * count
    buf = [0.0] * total
    rnd = random.Random(4242)

    for k in range(count):
        pitch = 1.0 + rnd.uniform(-0.03, 0.03)
        vol = rnd.uniform(0.85, 1.0)
        s = resample(one, 1.0 / pitch)
        start = k * step
        for i, v in enumerate(s):
            if start + i < total:
                buf[start + i] += v * vol

    peak = max(abs(v) for v in buf) or 1e-9
    return [v / peak * 0.80 for v in buf]


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out_dir = os.path.join(here, "preview_audio")

    for name, p in VARIANTS.items():
        f0, ha, ht, det, nhz, namt = p
        one = render(*p)
        write_wav(os.path.join(out_dir, name + ".wav"), one)
        write_wav(os.path.join(out_dir, name + "_send.wav"), make_seq(one))
        desc = f"基频{f0:.0f} 谐波{ha:.2f} 延音×{ht:.1f}"
        if det:
            desc += f" 失谐{det:.1%}"
        if nhz:
            desc += f" 凹陷{nhz:.0f}Hz/{namt:.0%}"
        print(f"{name}  {desc}")

    print(f"\n输出目录：{out_dir}")


if __name__ == "__main__":
    main()
