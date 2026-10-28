"""把抠好的三个视图合成一张灰底预览图，方便肉眼检查边缘和透明区域。"""
import os
import subprocess

FFMPEG = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
D = r"D:\h\my-game\tools\applemon"
S = 512


def preview(name):
    rgba = os.path.join(D, name + ".rgba")
    out = os.path.join(D, "prev_" + name + ".png")
    subprocess.run([
        FFMPEG, "-y", "-loglevel", "error",
        "-f", "lavfi", "-i", "color=c=0x3a3a3a:s=512x512",
        "-f", "rawvideo", "-pix_fmt", "rgba", "-s", "512x512", "-i", rgba,
        "-filter_complex", "[0][1]overlay",
        "-frames:v", "1", out,
    ], check=True)
    return out


parts = [preview(n) for n in ("left", "right", "center")]
out = os.path.join(D, "preview_keyed.png")
cmd = [FFMPEG, "-y", "-loglevel", "error"]
for p in parts:
    cmd += ["-i", p]
cmd += ["-filter_complex", "[0][1][2]hstack=inputs=3", "-frames:v", "1", out]
subprocess.run(cmd, check=True)
print("预览图:", out, os.path.getsize(out), "字节")
