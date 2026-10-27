"""
把截取出来的「哒」降调，并给一个削尖峰的柔和版。

做法是**重采样**（读源数据时放慢），不是滤波——音色完全不变，只是整体变低变长。
0.85 倍速 ≈ 降 2.8 个半音，56ms 变 66ms。

刺耳的来源其实是那次 3152Hz 的四次谐波（人耳 2~4kHz 最敏感），
降调会顺带把它压到 2680Hz 左右，所以降调同时也解决一部分刺耳。
另外附一个加了低通的版本，专门削这个峰。

产出（都在 tools/preview_audio/）：
  ref_low92.wav / _send.wav   微降（734Hz）
  ref_low85.wav / _send.wav   明显低一档（678Hz）
  ref_low78.wav / _send.wav   低沉（622Hz）
  ref_low85_soft.wav / _send.wav  降调 + 低通削尖
"""

import math
import os
import struct
import wave

SR = 44100
BIT = 32767
GAP_MS = 69.0
COUNT = 8
CUT_START_MS = 4799.0
CUT_LEN_MS = 56.0
FADE_MS = 3.0


def load(path):
    w = wave.open(path)
    raw = w.readframes(w.getnframes())
    sr = w.getframerate()
    w.close()
    return [x / 32768.0 for x in struct.unpack("<%dh" % (len(raw) // 2), raw)], sr


def write_wav(path, samples, sr=SR):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(b"".join(
            struct.pack("<h", int(max(-1.0, min(1.0, s)) * BIT)) for s in samples))


def normalize(samples, target=0.85):
    peak = max(1e-9, max(abs(v) for v in samples))
    return [v * target / peak for v in samples]


def fade(samples, sr, ms=FADE_MS):
    n = int(sr * ms / 1000)
    out = list(samples)
    for i in range(min(n, len(out))):
        k = i / n
        out[i] *= k
        out[-1 - i] *= k
    return out


def resample(samples, factor):
    """factor < 1 → 变慢变低。线性插值。"""
    n_out = max(2, int(len(samples) / factor))
    out = []
    last = len(samples) - 1
    for i in range(n_out):
        pos = i * factor
        i0 = int(pos)
        frac = pos - i0
        i1 = min(i0 + 1, last)
        out.append(samples[i0] * (1.0 - frac) + samples[i1] * frac)
    return out


def lowpass(samples, sr, cutoff_hz):
    """一阶低通，削掉高频尖峰。"""
    dt = 1.0 / sr
    rc = 1.0 / (2.0 * math.pi * cutoff_hz)
    a = dt / (rc + dt)
    y = 0.0
    out = []
    for v in samples:
        y += a * (v - y)
        out.append(y)
    return out


def make_seq(one, sr=SR, gap_ms=GAP_MS, count=COUNT):
    step = len(one) + int(sr * gap_ms / 1000)
    total = step * count
    buf = [0.0] * total
    for k in range(count):
        start = k * step
        for i, s in enumerate(one):
            if start + i < total:
                buf[start + i] += s
    return normalize(buf, 0.80)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out_dir = os.path.join(here, "preview_audio")
    src, sr = load(os.path.join(here, "ref", "ref.wav"))

    a = int(sr * CUT_START_MS / 1000)
    b = a + int(sr * CUT_LEN_MS / 1000)
    base = normalize(fade(src[a:b], sr), 0.85)

    plan = [
        ("ref_low92", 0.92, None),
        ("ref_low85", 0.85, None),
        ("ref_low78", 0.78, None),
        ("ref_low85_soft", 0.85, 2200.0),
    ]

    for name, factor, cutoff in plan:
        one = resample(base, factor)
        if cutoff:
            one = lowpass(one, sr, cutoff)
        one = normalize(fade(one, sr), 0.85)

        write_wav(os.path.join(out_dir, name + ".wav"), one, sr)
        write_wav(os.path.join(out_dir, name + "_send.wav"), make_seq(one, sr), sr)

        ms = len(one) / sr * 1000
        extra = f" + 低通{cutoff:.0f}Hz" if cutoff else ""
        print(f"{name:16s} 倍速 {factor:.2f}  约 {798 * factor:.0f}Hz  {ms:.0f}ms{extra}")


if __name__ == "__main__":
    main()
