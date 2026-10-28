"""把精灵表导成 GIF（深色底），方便直接看动画。"""
import os
import subprocess

FFMPEG = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
D = r"D:\h\my-game\tools\applemon"
CELL = 160
FRAMES = 32
BG = (38, 38, 42, 255)

sheet = open(os.path.join(D, "sheet.raw"), "rb").read()

frames_raw = bytearray()
for idx in range(FRAMES):
    cell = bytearray(CELL * CELL * 4)
    for y in range(CELL):
        s = (y * CELL * FRAMES + idx * CELL) * 4
        cell[y * CELL * 4:(y + 1) * CELL * 4] = sheet[s:s + CELL * 4]
    # 铺到深色底上
    out = bytearray(CELL * CELL * 4)
    for i in range(CELL * CELL):
        a = cell[i * 4 + 3]
        af = a / 255.0
        for c in range(3):
            out[i * 4 + c] = int(cell[i * 4 + c] * af + BG[c] * (1 - af))
        out[i * 4 + 3] = 255
    frames_raw += out

raw = os.path.join(D, "frames.raw")
open(raw, "wb").write(bytes(frames_raw))

out = os.path.join(D, "hop_apple_anim.gif")
subprocess.run([FFMPEG, "-y", "-loglevel", "error",
                "-f", "rawvideo", "-pix_fmt", "rgba", "-s", f"{CELL}x{CELL}",
                "-r", "12", "-i", raw,
                "-vf", "scale=320:320:flags=neighbor",
                "-frames:v", str(FRAMES), out], check=True)
print("GIF:", out, os.path.getsize(out), "字节")
