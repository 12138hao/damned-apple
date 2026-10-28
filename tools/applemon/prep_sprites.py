"""
把三张 512x512 的苹果图处理成可用的素材：
  · left / right 是白底 RGB，需要抠成透明（从四边泛滥填充，不吃掉牙齿和眼白）
  · split-mouth 本身已经是透明底，直接用

输出：tools/applemon/<name>.rgba（512x512 RGBA 原始字节）+ 一张预览图
"""

import os
import subprocess
from collections import deque

FFMPEG = r"C:\Users\leader\.workbuddy-ai\binaries\node\workspace\node_modules\@ffmpeg-installer\win32-x64\ffmpeg.exe"
OUT = r"D:\h\my-game\tools\applemon"
W = H = 512

SOURCES = {
    "left": r"C:\Users\leader\Doubao\chats\2026-10-22\new-chat-1\apple_left_512.png",
    "right": r"C:\Users\leader\Doubao\chats\2026-10-22\new-chat-1\apple_right_512.png",
    "center": r"D:\workbuddyhuancun\2026-10-24-12-00-32\outputs\applemon\apple-split-mouth-512.png",
}

# 需要抠图的两张
NEED_KEY = {"left", "right"}


def load_rgba(path):
    """用 ffmpeg 把图片解成 512x512 RGBA 原始字节。"""
    p = subprocess.run(
        [FFMPEG, "-loglevel", "error", "-i", path,
         "-f", "rawvideo", "-pix_fmt", "rgba", "-"],
        capture_output=True, check=True)
    data = bytearray(p.stdout)
    assert len(data) == W * H * 4, f"尺寸不对: {len(data)}"
    return data


def is_whiteish(data, i, tol=4):
    r, g, b = data[i], data[i + 1], data[i + 2]
    return (255 - r) <= tol and (255 - g) <= tol and (255 - b) <= tol


def key_out_white(data, tol=4):
    """
    从四边泛滥填充，把连通的白底变透明。
    为什么不直接全图删白色：苹果的牙齿、反光高光也是白的，
    但它们被红色包住、和边界不连通，泛滥填充不会碰到它们。
    """
    n = W * H
    visited = bytearray(n)          # 0 未访问 / 1 背景
    dq = deque()

    def push(x, y):
        idx = y * W + x
        if visited[idx]:
            return
        if not is_whiteish(data, idx * 4, tol):
            return
        visited[idx] = 1
        dq.append(idx)

    for x in range(W):
        push(x, 0)
        push(x, H - 1)
    for y in range(H):
        push(0, y)
        push(W - 1, y)

    while dq:
        idx = dq.popleft()
        x, y = idx % W, idx // W
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < W and 0 <= ny < H:
                push(nx, ny)

    removed = 0
    for idx in range(n):
        if visited[idx]:
            data[idx * 4 + 3] = 0
            removed += 1

    # 边缘柔化已关闭：牙齿是画在白底上的（min 通道约 237），
    # 按「越白越透明」去柔化会把整排牙啃成半透明。这套美术本身有深色描边，
    # 硬边缘反而干净。
    soft = 0

    return removed, soft


def deshadow(data, alpha_cut=252):
    """
    从四边出发，把「能连到外面、且不是实体」的像素清掉：
      · 半透明（alpha < 252）——边缘羽化、抗锯齿
      · 低饱和的灰色（sat <= 32）——贴图自带的投影（实测是 159,156,173 这种灰紫）

    为什么不误伤：
      · 牙齿/高光是白色，但被红色包住、和外面不连通，泛滥填充走不进去
      · 嘴巴里的暗色是深红/黑但有饱和度或是封闭区域，同样走不进去
    """
    n = W * H
    seen = bytearray(n)
    dq = deque()

    def passable(idx):
        i = idx * 4
        if data[i + 3] < alpha_cut:
            return True
        r, g, b = data[i], data[i + 1], data[i + 2]
        sat = max(r, g, b) - min(r, g, b)
        bright = (r + g + b) // 3
        # 只认「中灰」：太亮的（牙齿、高光，>215）和太暗的都不碰
        return sat <= 25 and 100 <= bright <= 215

    def push(x, y):
        idx = y * W + x
        if seen[idx] or not passable(idx):
            return
        seen[idx] = 1
        dq.append(idx)

    for x in range(W):
        push(x, 0)
        push(x, H - 1)
    for y in range(H):
        push(0, y)
        push(W - 1, y)

    while dq:
        idx = dq.popleft()
        x, y = idx % W, idx // W
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < W and 0 <= ny < H:
                push(nx, ny)

    cleaned = 0
    for idx in range(n):
        if seen[idx]:
            data[idx * 4 + 3] = 0
            cleaned += 1
    return cleaned


def keep_largest_blob(data, opaque=128):
    """
    只保留最大的那块不透明连通区域，把边角散落的噪点全部丢掉。
    苹果本体是连成一片的，噪点都是孤立小块，这样一次清干净。
    """
    n = W * H
    label = [0] * n
    best, best_size, cur = -1, 0, 0

    for start in range(n):
        if label[start] or data[start * 4 + 3] < opaque:
            continue
        cur += 1
        label[start] = cur
        dq = deque([start])
        size = 0
        while dq:
            idx = dq.popleft()
            size += 1
            x, y = idx % W, idx // W
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if 0 <= nx < W and 0 <= ny < H:
                    j = ny * W + nx
                    if not label[j] and data[j * 4 + 3] >= opaque:
                        label[j] = cur
                        dq.append(j)
        if size > best_size:
            best, best_size = cur, size

    dropped = 0
    for idx in range(n):
        if data[idx * 4 + 3] >= opaque and label[idx] != best:
            data[idx * 4 + 3] = 0
            dropped += 1
    return dropped


def bbox(data):
    """找出不透明内容的外接矩形，用来对齐落脚点。"""
    minx, miny, maxx, maxy = W, H, -1, -1
    for y in range(H):
        row = y * W
        for x in range(W):
            if data[(row + x) * 4 + 3] > 8:
                if x < minx: minx = x
                if x > maxx: maxx = x
                if y < miny: miny = y
                if y > maxy: maxy = y
    return minx, miny, maxx, maxy


os.makedirs(OUT, exist_ok=True)
report = []
for name, path in SOURCES.items():
    data = load_rgba(path)
    if name in NEED_KEY:
        removed, soft = key_out_white(data)
    else:
        removed, soft = 0, 0
    shadow = deshadow(data)
    noise = keep_largest_blob(data)
    b = bbox(data)
    open(os.path.join(OUT, name + ".rgba"), "wb").write(bytes(data))
    report.append((name, removed, soft, shadow + noise, b))

print("名字      去白像素   边缘柔化   去阴影   内容外接框 (x1,y1,x2,y2)")
for name, removed, soft, shadow, b in report:
    print(f"{name:8s} {removed:8d} {soft:9d} {shadow:8d}   {b}")
