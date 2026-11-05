"""
程序合成泥浆音效，不依赖 numpy / 任何音频库。

两个文件：
  mud_splash.wav  入泥：低频闷响 + 泥浆喷溅 + 几声咕嘟
  mud_step.wav    泥里走路：黏稠的"吧唧"，带一点低频拖尾

设计思路（为什么这么写）：
  · 泥浆不像水，高频应该被吃掉 —— 所以噪声全部先过一阶低通，滤掉"沙沙"声。
  · 入泥的"咚"必须是一个下滑的音高（150→50Hz），固定频率听起来像敲鼓，不像掉进软东西。
  · 泥里走路没有清脆的鞋底声，只有一小段闷响 + 很短的气泡尾音。
"""

import math
import os
import random
import struct

SR = 44100


def write_wav(path, samples):
    """把 [-1,1] 的浮点样本写成 16-bit 单声道 WAV。"""
    data = b"".join(
        struct.pack("<h", max(-32768, min(32767, int(s * 32767)))) for s in samples
    )
    hdr = b"RIFF" + struct.pack("<I", 36 + len(data)) + b"WAVE"
    hdr += b"fmt " + struct.pack("<IHHIIHH", 16, 1, 1, SR, SR * 2, 2, 16)
    hdr += b"data" + struct.pack("<I", len(data))
    with open(path, "wb") as f:
        f.write(hdr + data)


def lowpass(xs, a):
    """一阶 IIR 低通。a 越小越闷。"""
    y = 0.0
    out = []
    for v in xs:
        y += a * (v - y)
        out.append(y)
    return out


def normalize(xs, peak=0.85):
    m = max(abs(v) for v in xs) or 1.0
    return [v / m * peak for v in xs]


def gen_splash():
    """入泥：0.55 秒。"""
    dur = 0.55
    n = int(SR * dur)
    rnd = random.Random(20261104)

    # 噪声源：白噪声过重低通，变成闷闷的"噗"
    noise = [rnd.uniform(-1.0, 1.0) for _ in range(n)]
    noise = lowpass(noise, 0.09)

    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR

        # ① 低频闷响：音高从 150Hz 指数滑到 50Hz
        f = 50.0 + 100.0 * math.exp(-t * 9.0)
        phase += 2.0 * math.pi * f / SR
        thump = math.sin(phase) * math.exp(-t * 13.0) * 0.62

        # ② 泥浆喷溅：快起音、慢衰减的闷噪声
        env_n = math.exp(-t * 8.5) * (1.0 - math.exp(-t * 260.0))
        splash = noise[i] * env_n * 0.85

        # ③ 咕嘟：三个短促的气泡脉冲，频率低、衰减快
        bubble = 0.0
        for start, freq, amp in ((0.055, 118.0, 0.20), (0.135, 92.0, 0.16), (0.235, 76.0, 0.11)):
            if t >= start:
                dt = t - start
                if dt < 0.09:
                    env_b = math.exp(-dt * 42.0)
                    bubble += math.sin(2.0 * math.pi * freq * dt) * env_b * amp

        out.append(thump + splash + bubble)

    return normalize(out, 0.86)


def gen_step():
    """泥里走路：0.17 秒，黏稠的一声"吧唧"。"""
    dur = 0.17
    n = int(SR * dur)
    rnd = random.Random(777)

    noise = [rnd.uniform(-1.0, 1.0) for _ in range(n)]
    lp = lowpass(noise, 0.42)          # 保留一点中频，不能太闷否则听不见
    # 再高通一次，去掉直流和极低频的"轰"
    hp = []
    prev_x = 0.0
    prev_y = 0.0
    for v in lp:
        y = 0.88 * (prev_y + v - prev_x)
        hp.append(y)
        prev_x = v
        prev_y = y

    out = []
    phase = 0.0
    for i in range(n):
        t = i / SR

        # 主音：中频噪声快衰，做出"吧唧"的接触感
        body = hp[i] * math.exp(-t * 26.0) * (1.0 - math.exp(-t * 900.0)) * 1.15

        # 低频黏稠拖尾：90Hz 慢一点衰减，听起来有"陷下去"的重量
        phase += 2.0 * math.pi * 88.0 / SR
        glue = math.sin(phase) * math.exp(-t * 22.0) * 0.30

        out.append(body + glue)

    return normalize(out, 0.72)


def main():
    out_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                           "assets", "audio", "sfx")
    os.makedirs(out_dir, exist_ok=True)

    jobs = [("mud_splash.wav", gen_splash()), ("mud_step.wav", gen_step())]
    for name, samples in jobs:
        p = os.path.join(out_dir, name)
        write_wav(p, samples)
        print("  %-16s %5.2f 秒  %6.1f KB" % (name, len(samples) / SR,
                                              os.path.getsize(p) / 1024))


if __name__ == "__main__":
    print("[SFX] 生成泥浆音效")
    main()
