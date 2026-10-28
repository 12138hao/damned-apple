"""把精灵表排成 2 行 16 列、深色底，方便肉眼检查。"""
import os
import subprocess

FFMPEG = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
D = r"D:\h\my-game\tools\applemon"
CELL = 160
FRAMES = 32
COLS = 16
ROWS = 2
BG = (42, 42, 46, 255)

sheet = open(os.path.join(D, "sheet.raw"), "rb").read()

out_w, out_h = CELL * COLS, CELL * ROWS
canvas = bytearray(out_w * out_h * 4)
for i in range(out_w * out_h):
    canvas[i * 4:i * 4 + 4] = bytes(BG)

for idx in range(FRAMES):
    col, row = idx % COLS, idx // COLS
    bx, by = col * CELL, row * CELL
    for y in range(CELL):
        s = (y * CELL * FRAMES + idx * CELL) * 4
        rowbytes = sheet[s:s + CELL * 4]
        d = ((by + y) * out_w + bx) * 4
        # alpha 混合到背景
        for x in range(CELL):
            si = x * 4
            a = rowbytes[si + 3]
            if a == 0:
                continue
            af = a / 255.0
            di = d + x * 4
            for c in range(3):
                canvas[di + c] = int(rowbytes[si + c] * af + canvas[di + c] * (1 - af))
            canvas[di + 3] = 255

raw = os.path.join(D, "preview_sheet.raw")
open(raw, "wb").write(bytes(canvas))
out = os.path.join(D, "preview_sheet.png")
subprocess.run([FFMPEG, "-y", "-loglevel", "error",
                "-f", "rawvideo", "-pix_fmt", "rgba",
                "-s", f"{out_w}x{out_h}", "-i", raw,
                "-frames:v", "1", out], check=True)
print("预览:", out, os.path.getsize(out), "字节", f"{out_w}x{out_h}")
