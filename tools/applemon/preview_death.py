"""预览 6 帧死亡动画（深色底，2 倍放大）。"""
import os
import pickle
import subprocess

FFMPEG = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
D = r"D:\h\my-game\tools\applemon"
CELL = 160
BG = (40, 40, 44, 255)

frames = pickle.load(open(os.path.join(D, "death_frames.pkl"), "rb"))
n = len(frames)
S = 2
W, H = CELL * S * n, CELL * S
canvas = bytearray(W * H * 4)
for i in range(W * H):
    canvas[i * 4:i * 4 + 4] = bytes(BG)

for idx, cell in enumerate(frames):
    for y in range(CELL):
        for x in range(CELL):
            si = (y * CELL + x) * 4
            a = cell[si + 3]
            if a == 0:
                continue
            af = a / 255.0
            for sy_ in range(S):
                for sx_ in range(S):
                    px, py = idx * CELL * S + x * S + sx_, y * S + sy_
                    di = (py * W + px) * 4
                    for c in range(3):
                        canvas[di + c] = int(cell[si + c] * af + canvas[di + c] * (1 - af))
                    canvas[di + 3] = 255

raw = os.path.join(D, "preview_death.raw")
open(raw, "wb").write(bytes(canvas))
out = os.path.join(D, "preview_death.png")
subprocess.run([FFMPEG, "-y", "-loglevel", "error",
                "-f", "rawvideo", "-pix_fmt", "rgba", "-s", f"{W}x{H}",
                "-i", raw, "-frames:v", "1", out], check=True)
print("预览:", out, f"{W}x{H}")
