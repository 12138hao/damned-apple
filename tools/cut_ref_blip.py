"""
从参考音频里截出「哒」的一声，同时给一个按测量参数合成的版本。

测量结果（tools/analyze_ref.py / probe_blip.py）：
  基频    798 Hz（+3152 Hz 四次谐波，二三次谐波几乎为零 → 空心电子音）
  时长    50.9 ms
  间隔    68.8 ms（起音间距 119.7ms，约 8.4 声/秒）
  包络    软起音约 8~10ms，平台期，收尾约 5ms

产出：
  tools/preview_audio/ref_cut.wav        直接截取（最保真，带一点点背景）
  tools/preview_audio/synth_match.wav    按参数合成（干净）
  各自 *_send.wav：按参考节奏连放 8 声
"""

import math
import os
import struct
import wave

SR = 44100
BIT = 32767

# 参考音频里最干净的那一串（第 6~12 个），取第 9 个
CUT_START_MS = 4799.0
CUT_LEN_MS = 56.0
FADE_MS = 3.0

F0 = 798.0          # 基频
F_HI = 3152.0       # 四次谐波
DUR_MS = 51.0
GAP_MS = 69.0       # 参考音频的静音间隔
COUNT = 8


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


def synth_one():
    """按测量参数合成：基频 + 四次谐波，软起音 + 快收尾。"""
    n = int(SR * DUR_MS / 1000)
    attack = int(SR * 0.008)     # 8ms 软起音
    release = int(SR * 0.005)    # 5ms 收尾
    out = []
    for i in range(n):
        t = i / SR
        # 测量显示二三次谐波几乎为零，所以只叠基频和四次
        s = math.sin(2 * math.pi * F0 * t) + 0.77 * math.sin(2 * math.pi * F_HI * t)
        s /= 1.77

        if i < attack:
            env = i / attack
        elif i >= n - release:
            env = (n - i) / release
        else:
            env = 1.0
        out.append(s * env)
    return out


def make_seq(one, gap_ms=GAP_MS, count=COUNT, sr=SR):
    total = int(sr * (len(one) / sr + count * gap_ms / 1000))
    buf = [0.0] * total
    step = int(sr * (len(one) / sr + gap_ms / 1000))
    for k in range(count):
        start = k * step
        for i, s in enumerate(one):
            if start + i < total:
                buf[start + i] += s
    return normalize(buf, 0.80)


def main():
    out_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview_audio")
    src, sr = load(os.path.join(os.path.dirname(os.path.abspath(__file__)), "ref", "ref.wav"))

    a = int(sr * CUT_START_MS / 1000)
    b = a + int(sr * CUT_LEN_MS / 1000)
    cut = normalize(fade(src[a:b], sr), 0.85)

    syn = normalize(synth_one(), 0.85)

    write_wav(os.path.join(out_dir, "ref_cut.wav"), cut, sr)
    write_wav(os.path.join(out_dir, "ref_cut_send.wav"), make_seq(cut, sr=sr), sr)

    write_wav(os.path.join(out_dir, "synth_match.wav"), syn)
    write_wav(os.path.join(out_dir, "synth_match_send.wav"), make_seq(syn))

    print(f"ref_cut.wav         {len(cut) / sr * 1000:.1f}ms  截取")
    print(f"synth_match.wav     {len(syn) / SR * 1000:.1f}ms  合成")


if __name__ == "__main__":
    main()
