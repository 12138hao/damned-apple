"""
生成「逆转裁判」风格对话音（文字送り音）——第三轮：PSG 脉冲波 + 高密度。

前两轮的问题：
  1. 间隔 85ms 太慢（12 声/秒）→ 听感是「啵…啵…啵」
     真机是每个字一声，间隔约 30ms（30 声/秒）→ 连成片的「嘟嘟嘟」
  2. 用了指数衰减，声音被磨圆了
     GBA 的 PSG 是硬起硬收（gate 包络），才有那种干脆的电子味
  3. 50% 方波太「空洞」，PSG 常用的 12.5% / 25% 占空比更薄更脆

用法：
  python tools/gen_blip.py
"""

import math
import os
import random
import struct
import wave

SR = 44100
BIT = 32767

# name: (占空比, 频率Hz, 时长ms, 间隔ms, 音量)
# 占空比 0.5 = 方波；0.25 / 0.125 是 GBA 常用的窄脉冲，更薄更「电子」
VARIANTS = {
    # —— 密度对比：同样的音，快慢两版 ——
    "aa_pulse25_fast": (0.25, 1100.0, 22, 32, 0.42),
    "aa_pulse25_slow": (0.25, 1100.0, 22, 60, 0.42),

    # —— 占空比对比 ——
    "aa_pulse12_fast": (0.125, 1100.0, 20, 32, 0.38),   # 最薄最脆
    "aa_pulse50_fast": (0.500, 1100.0, 18, 32, 0.32),   # 空洞方波

    # —— 音高对比（同占空比/密度）——
    "aa_pulse25_low":  (0.25, 780.0, 24, 32, 0.45),     # 低沉，大叔/反派
    "aa_pulse25_high": (0.25, 1500.0, 20, 32, 0.36),    # 尖细，少女/小孩
}

COUNT = 60          # 连放多少个（约 2 秒）


def pulse(phase: float, duty: float) -> float:
    """PSG 脉冲波：一个周期内前 duty 比例为 +1，其余 -1。"""
    frac = (phase / (2.0 * math.pi)) % 1.0
    return 1.0 if frac < duty else -1.0


def render(duty, freq, dur_ms, amp, pitch=1.0):
    """单个 blip：硬起硬收，几乎没有衰减（这是「电子味」的来源）。"""
    n = max(16, int(SR * dur_ms / 1000.0))
    attack_n = max(2, int(SR * 0.0015))   # 1.5ms
    release_n = max(2, int(SR * 0.002))   # 2ms

    f = freq * pitch
    phase = 0.0
    out = []

    for i in range(n):
        phase += 2.0 * math.pi * f / SR
        s = pulse(phase, duty)

        # gate 包络：起音 → 平顶 → 收尾，中间不衰减
        if i < attack_n:
            env = i / attack_n
        elif i >= n - release_n:
            env = (n - i) / release_n
        else:
            env = 1.0

        out.append(s * env * amp)

    return out


def write_wav(path: str, samples: list) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        frames = bytearray()
        for s in samples:
            frames += struct.pack("<h", int(max(-1.0, min(1.0, s)) * BIT))
        w.writeframes(bytes(frames))


def make_sequence(duty, freq, dur_ms, gap_ms, amp):
    """按打字节奏连放，带轻微音高抖动（否则像机械蜂鸣）。"""
    rnd = random.Random(20261027)
    total = int(SR * COUNT * gap_ms / 1000.0)
    buf = [0.0] * total

    for k in range(COUNT):
        pitch = 1.0 + rnd.uniform(-0.03, 0.03)
        vol = amp * rnd.uniform(0.88, 1.0)
        one = render(duty, freq, dur_ms, vol, pitch)
        start = int(SR * k * gap_ms / 1000.0)
        for i, s in enumerate(one):
            if start + i < total:
                buf[start + i] += s

    peak = max(1e-6, max(abs(x) for x in buf))
    if peak > 0.85:
        buf = [x * 0.85 / peak for x in buf]
    return buf


def main() -> None:
    out_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview_audio")
    print(f"{'文件':24s} 占空比  频率     时长   间隔")
    for name, (duty, freq, dur, gap, amp) in VARIANTS.items():
        write_wav(os.path.join(out_dir, name + ".wav"), render(duty, freq, dur, amp))
        write_wav(os.path.join(out_dir, name + "_send.wav"),
                  make_sequence(duty, freq, dur, gap, amp))
        print(f"{name:24s} {duty * 100:5.1f}%  {freq:6.0f}Hz  {dur:2d}ms  {gap:2d}ms")
    print(f"\n输出目录：{out_dir}")


if __name__ == "__main__":
    main()
