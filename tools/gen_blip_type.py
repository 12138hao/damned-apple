"""
合成「打字感」更强的对话音——重点是瞬态，不是音高。

为什么之前像「哔哔哔」：
  软起音(8ms) + 平台段 + 方形包络 = 一个持续音，人耳判为「蜂鸣」
真正的打字音靠三样东西：
  1. 起音瞬态：2~4ms 的噪声「咔」，模拟机械碰撞
  2. 快速衰减：指数衰减，没有平台段，声音一下就没了
  3. 音高下滑：短促的 down-chirp，「哒」而不是「嘀」

六个方案在「噪声占比 / 衰减速度 / 下滑幅度 / 时长」上各不相同。

输出：tools/preview_audio/type_*.wav 及各自的 _send.wav
"""

import math
import os
import random
import struct
import wave

SR = 44100
BIT = 32767

# 名字: (噪声量, 音体起始Hz, 下滑比例, 音体衰减ms, 总时长ms, 音体占比)
#   噪声量   0~1，起音「咔」的强度
#   下滑比例 0.5 表示音高在结束时降到起始的一半
#   衰减ms   音体指数衰减的时间常数，越小越「干脆」
VARIANTS = {
    # 噪 + 音，强瞬态 + 明显下滑 —— 最像「打字」
    "type_da":    (0.55, 820.0, 0.42, 13.0, 40, 0.45),
    # 噪为主，音体很弱 —— 像机械键盘 / 木鱼
    "type_click": (0.90, 640.0, 0.30, 9.0, 28, 0.22),
    # 极短，颗粒感最强
    "type_tick":  (0.75, 900.0, 0.35, 7.0, 22, 0.30),
    # 纯音体，无噪声，靠下滑和衰减做「哒」
    "type_drop":  (0.00, 880.0, 0.55, 15.0, 38, 1.00),
    # 双段：先「嘀」后「嗒」
    "type_dual":  (0.35, 760.0, 0.20, 11.0, 46, 0.70),
    # 柔和版：噪声少、衰减慢一点，长时间听不累
    "type_soft":  (0.30, 700.0, 0.35, 18.0, 44, 0.55),
}

GAP_MS = 62.0
COUNT = 16


def render(noise_amt, f0, glide, decay_ms, dur_ms, body_amt):
    n = max(8, int(SR * dur_ms / 1000))
    rnd = random.Random(20261027)
    decay = decay_ms / 1000.0

    # 起音瞬态：2.5ms 的噪声包络（快速打开又关掉）
    click_n = max(4, int(SR * 0.0025))
    click = []
    prev = 0.0
    for i in range(click_n):
        # 一阶差分让噪声偏高频，像「咔」而不是「噗」
        w = rnd.uniform(-1.0, 1.0)
        hp = w - prev
        prev = w
        env = math.exp(-i / max(1.0, click_n * 0.30))
        click.append(hp * env * 0.5)

    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR
        prog = i / n

        # ---- 音体：音高下滑 + 指数衰减 ----
        f = f0 * (1.0 - glide * (prog ** 0.7))
        phase += 2.0 * math.pi * f / SR
        body = math.sin(phase) + 0.30 * math.sin(2.0 * phase)
        body_env = math.exp(-t / decay)
        # 收尾 2ms 线性归零，避免爆音
        tail = min(1.0, (n - i) / max(1.0, SR * 0.002))
        body = body * body_env * tail * body_amt

        # ---- 瞬态叠加 ----
        clk = click[i] * noise_amt if i < len(click) else 0.0

        out.append(body + clk)

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
    rnd = random.Random(555)

    for k in range(count):
        pitch = 1.0 + rnd.uniform(-0.04, 0.04)
        vol = rnd.uniform(0.82, 1.0)
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
        print(f"{name:12s} {p[4]:2d}ms  噪声 {p[0]:.2f}  音体 {p[1]:.0f}Hz 下滑 {p[2]:.0%}")

    print(f"\n输出目录：{out_dir}")


if __name__ == "__main__":
    main()
