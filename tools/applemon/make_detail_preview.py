"""挑几段放大检查：跳跃循环 1x、死亡炸开 2x，上下排在一张图上。"""
import os
import subprocess

FFMPEG = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
D = r"D:\h\my-game\tools\applemon"
CELL = 160
FRAMES = 32
BG = (42, 42, 46, 255)

sheet = open(os.path.join(D, "sheet.raw"), "rb").read()

W, H = 1280, 640
canvas = bytearray(W * H * 4)
for i in range(W * H):
    canvas[i * 4:i * 4 + 4] = bytes(BG)


def blit(src_idx, dst_x, dst_y, scale):
    for y in range(CELL):
        base = (y * CELL * FRAMES + src_idx * CELL) * 4
        for x in range(CELL):
            si = base + x * 4
            a = sheet[si + 3]
            if a == 0:
                continue
            af = a / 255.0
            for sy_ in range(scale):
                for sx_ in range(scale):
                    px = dst_x + x * scale + sx_
                    py = dst_y + y * scale + sy_
                    if not (0 <= px < W and 0 <= py < H):
                        continue
                    di = (py * W + px) * 4
                    for c in range(3):
                        canvas[di + c] = int(sheet[si + c] * af + canvas[di + c] * (1 - af))
                    canvas[di + 3] = 255


# 上排：跳跃循环 8~13，1 倍
for i, frame in enumerate(range(8, 14)):
    blit(frame, i * CELL + 100, 20, 1)

# 下排：死亡炸开 28~31，2 倍
for i, frame in enumerate(range(26, 32)):
    blit(frame, i * 180 + 40, 200, 2)

raw = os.path.join(D, "preview_detail.raw")
open(raw, "wb").write(bytes(canvas))
out = os.path.join(D, "preview_detail.png")
subprocess.run([FFMPEG, "-y", "-loglevel", "error",
                "-f", "rawvideo", "-pix_fmt", "rgba",
                "-s", f"{W}x{H}", "-i", raw, "-frames:v", "1", out], check=True)
print("细节预览:", out, f"{W}x{H}")
