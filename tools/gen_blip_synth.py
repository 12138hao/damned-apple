"""
独立合成「打字音」——不取自任何录音，全部由参数算出。

设计依据（从参考音频测得，只取「类型特征」，不取波形）：
  基频        678 Hz（降调后的舒适音高）
  谐波        + 四次谐波（空心电子感）
  二、三次谐波 不要（原版也没有）
  时长        51 ms
  包络        8 ms 软起音 → 平台 → 5 ms 收尾

刺耳的根源是四次谐波落在 2~4 kHz（人耳最敏感），所以这里做成
三个谐波强度档，供挑选，而不是靠低通硬削。

输出：tools/preview_audio/blip_a.wav / blip_b.wav / blip_c.wav
      以及各自的 _send.wav 连放演示
"""

import math
import os
import random
import struct
import wave

SR = 44100
BIT = 32767

F0 = 678.0          # 基频
HARM = 4            # 四次谐波
DUR_MS = 51.0
ATTACK_MS = 8.0
RELEASE_MS = 5.0
GAP_MS = 66.0       # 连放间隔
COUNT = 14

# 名字: 四次谐波的相对强度
VARIANTS = {
    "blip_a": 0.45,   # 谐波明显，有空心感，但比原版柔
    "blip_b": 0.28,   # 更闷更软，长时间听不累
    "blip_c": 0.65,   # 更亮更脆，接近原版的电子味
}


def render(harm_amp):
    n = int(SR * DUR_MS / 1000)
    atk = max(2, int(SR * ATTACK_MS / 1000))
    rel = max(2, int(SR * RELEASE_MS / 1000))

    # 归一化系数：保证基频 + 谐波不会超过 1.0
    norm = 1.0 / (1.0 + harm_amp)

    out = []
    for i in range(n):
        t = i / SR
        s = math.sin(2 * math.pi * F0 * t)
        s += harm_amp * math.sin(2 * math.pi * F0 * HARM * t)
        s *= norm

        if i < atk:
            env = i / atk
        elif i >= n - rel:
            env = (n - i) / rel
        else:
            env = 1.0

        out.append(s * env)

    peak = max(abs(v) for v in out) or 1e-9
    return [v / peak * 0.85 for v in out]


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
    rnd = random.Random(20261027)

    for k in range(count):
        # 每一声做微小的音高和音量抖动，模拟真人说话的不均匀
        pitch = 1.0 + rnd.uniform(-0.035, 0.035)
        vol = rnd.uniform(0.86, 1.0)
        shifted = resample(one, 1.0 / pitch)
        start = k * step
        for i, s in enumerate(shifted):
            if start + i < total:
                buf[start + i] += s * vol

    peak = max(abs(v) for v in buf) or 1e-9
    return [v / peak * 0.80 for v in buf]


def resample(samples, factor):
    """factor > 1 拉长（降调），< 1 缩短（升调）。"""
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


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out_dir = os.path.join(here, "preview_audio")

    for name, harm in VARIANTS.items():
        one = render(harm)
        write_wav(os.path.join(out_dir, name + ".wav"), one)
        write_wav(os.path.join(out_dir, name + "_send.wav"), make_seq(one))
        peak_hz = F0 * HARM
        print(f"{name}  基频 {F0:.0f}Hz  谐波 {peak_hz:.0f}Hz × {harm:.2f}  "
              f"{len(one) / SR * 1000:.0f}ms")

    print(f"\n输出目录：{out_dir}")


if __name__ == "__main__":
    main()
