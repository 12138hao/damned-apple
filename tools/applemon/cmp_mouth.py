"""对比：原图 vs 抠图结果，聚焦嘴巴区域，检查上槽牙是否被吃掉。"""
import os
import subprocess

FFMPEG = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
D = r"D:\h\my-game\tools\applemon"
SRC = r"C:\Users\leader\Doubao\chats\2026-10-22\new-chat-1\apple_right_512.png"

# 嘴巴在右视图的右侧，裁出来放大看
CROP = "crop=260:260:200:180,scale=520:520:flags=neighbor"

subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-i", SRC,
                "-vf", CROP, "-frames:v", "1",
                os.path.join(D, "cmp_orig.png")], check=True)

# 抠好的结果铺到红底上（透明处显示红，牙齿丢失会很明显）
subprocess.run([FFMPEG, "-y", "-loglevel", "error",
                "-f", "lavfi", "-i", "color=c=0xd02020:s=512x512",
                "-f", "rawvideo", "-pix_fmt", "rgba", "-s", "512x512",
                "-i", os.path.join(D, "right.rgba"),
                "-filter_complex", "[0][1]overlay," + CROP,
                "-frames:v", "1", os.path.join(D, "cmp_keyed.png")], check=True)

out = os.path.join(D, "cmp_side.png")
subprocess.run([FFMPEG, "-y", "-loglevel", "error",
                "-i", os.path.join(D, "cmp_orig.png"),
                "-i", os.path.join(D, "cmp_keyed.png"),
                "-filter_complex", "[0][1]hstack=inputs=2",
                "-frames:v", "1", out], check=True)
print("左右对比图:", out)
