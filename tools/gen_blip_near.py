"""
往原版靠的渐进方案。

从参考音频实测到的两个细节（只取类型特征，不取波形）：
  1. 基频与四次谐波的衰减速度不同 —— 尾段高频反而更突出
     （分段测：前 26ms 主频 787Hz，后 26ms 主频跳到 2940Hz）
     所以四次谐波的衰减时间要做得比基频更长，才有那种「玻璃感」
  2. 包络是「软起音 → 近平台 → 快收尾」，不是纯指数衰减
     （这也是它听起来不像蜂鸣器的原因之一）

六档从「最接近原版」渐变到「最像打字」：
  near_1  原版复刻：8ms 软起音、平台、无下滑、谐波等高衰减
  near_2  + 高次谐波延音
  near_3  + 10% 下滑
  near_4  起音缩到 5ms
  near_5  + 一点噪声瞬态
  near_6  更短更快，明显打字感

输出：tools/preview_audio/near_*.wav 及 _send.wav
"""

import math
import os
import random
import struct
import wave

SR = 44100
BIT = 32767
F0 = 678.0          # 基频（已按你的听感降过调）
GAP_MS = 68.0
COUNT = 14

# name: (起音ms, 下滑, 基频衰减ms, 谐波衰减倍数, 谐波幅度, 噪声量, 时长ms, 平台度)
#   平台度 1.0 = 完全平顶；0 = 纯指数衰减
VARIANTS = {
    "near_1": (8.0, 0.00, 90.0, 1.0, 0.55, 0.00, 51, 1.00),
    "near_2": (8.0, 0.00, 60.0, 2.5, 0.55, 0.00, 51, 1.00),
    "near_3": (7.0, 0.10, 55.0, 2.5, 0.55, 0.00, 50, 0.85),
    "near_4": (5.0, 0.14, 45.0, 2.2, 0.55, 0.00, 48, 0.70),
    "near_5": (4.5, 0.16, 40.0, 2.0, 0.52, 0.18, 46, 0.55),
    "near_6": (3.5, 0.22, 26.0, 1.8, 0.50, 0.30, 42, 0.30),
}


def render(atk_ms, glide, tau1_ms, harm_tau_ratio, harm_amp,
           noise_amt, dur_ms, sustain):
    n = max(16, int(SR * dur_ms / 1000))
    atk = max(2, int(SR * atk_ms / 1000))
    rel = max(2, int(SR * 0.004))
    tau1 = tau1_ms / 1000.0
    tau4 = tau1 * harm_tau_ratio
    rnd = random.Random(20261027)

    # 起音噪声瞬态（可选）
    click_n = int(SR * 0.0025)
    click = []
    prev = 0.0
    for i in range(click_n):
        w = rnd.uniform(-1.0, 1.0)
        hp = w - prev
        prev = w
        click.append(hp * math.exp(-i / max(1.0, click_n * 0.30)) * 0.5)

    ph1 = 0.0
    ph4 = 0.0
    out = []

    for i in range(n):
        t = i / SR
        prog = i / n

        # 音高：前 70% 不滑，最后 30% 才开始滑（原版就是前段稳、后段变化）
        if prog < 0.7:
            f = F0
        else:
            k = (prog - 0.7) / 0.3
            f = F0 * (1.0 - glide * k * k)

        ph1 += 2.0 * math.pi * f / SR
        ph4 += 2.0 * math.pi * f * 4.0 / SR

        # 包络：起音 → 衰减到 sustain 平台 → 收尾
        if i < atk:
            a = i / atk
        else:
            a = 1.0
        e1 = sustain + (1.0 - sustain) * math.exp(-t / tau1)
        e4 = sustain + (1.0 - sustain) * math.exp(-t / tau4)
        tail = min(1.0, (n - i) / rel)

        base = a * tail
        body = math.sin(ph1) * e1 * base
        body += harm_amp * math.sin(ph4) * e4 * base

        clk = click[i] * noise_amt if i < len(click) else 0.0
        out.append(body / (1.0 + harm_amp) + clk)

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
    rnd = random.Random(9999)

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
        one = render(*p)
        write_wav(os.path.join(out_dir, name + ".wav"), one)
        write_wav(os.path.join(out_dir, name + "_send.wav"), make_seq(one))
        print(f"{name}  起音{p[0]:.1f}ms 下滑{p[1]:.0%} 谐波延音×{p[3]:.1f} "
              f"噪声{p[5]:.2f} {p[6]}ms")

    print(f"\n输出目录：{out_dir}")


if __name__ == "__main__":
    main()
