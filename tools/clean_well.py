# -*- coding: utf-8 -*-
"""清理古井贴图：把「绳子—横梁—木柱—井口」围出来的那块残留白底扣掉。

为什么不能直接「把所有近白像素变透明」：
  井口石砖的高光是浅灰的，粗暴抠白会在石头上打出洞。
所以只删「面积 >= 100 的连通白块」——残留背景一定是一大块连着的，
石头上的高光都是零散小点。

顺带做一次「白边清理」：紧挨着被删区域、颜色又很浅的中性像素也一起删掉，
否则边缘会留一圈 1 像素的白毛。

纯 stdlib。
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_rope_assets import read_png, write_png

SRC = 'D:/deepseek-harness/home/agent-workspace/assets/jungle/well2_256.png'
DST = 'assets/jungle/well.png'
PREVIEW = 'docs/screenshots/well_clean.png'

MIN_BLOCK = 100      # 面积大于这个才认为是残留背景


def is_white(c, lo=235, sat=6):
    r, g, b, a = c
    return a > 10 and r >= lo and g >= lo and b >= lo and (max(r, g, b) - min(r, g, b)) <= sat


def main():
    w, h, px = read_png(SRC)

    # ── ① 找连通白块 ──
    seen = [[False] * w for _ in range(h)]
    removed = [[False] * w for _ in range(h)]
    blocks = []
    for y in range(h):
        for x in range(w):
            if seen[y][x] or not is_white(px[y][x]):
                continue
            stack = [(x, y)]
            seen[y][x] = True
            pts = []
            while stack:
                cx, cy = stack.pop()
                pts.append((cx, cy))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and is_white(px[ny][nx]):
                        seen[ny][nx] = True
                        stack.append((nx, ny))
            if len(pts) >= MIN_BLOCK:
                blocks.append(pts)
                for (bx, by) in pts:
                    removed[by][bx] = True

    for pts in blocks:
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        print('删除白块：面积=%d  x[%d,%d] y[%d,%d]' % (len(pts), min(xs), max(xs), min(ys), max(ys)))
        for (bx, by) in pts:
            r, g, b, _ = px[by][bx]
            px[by][bx] = (r, g, b, 0)

    # ── ② 白边清理：贴着删除区的「白色 → 黑描边」过渡像素也删 ──
    # 这些像素是 (140,140,140) / (196,196,196) 这种中性灰 —— 白底和黑描边各半。
    #
    # 两个必须同时满足的限制，否则会啃坏井本身：
    #   1. **只在删除块的包围盒里做**（外扩 3 像素）。井口石砖的亮面 (224,224,224)
    #      也贴着透明背景，不限制范围的话整圈石头边缘都会被吃掉。
    #   2. **饱和度 <= 20**。麻绳是 (200,168,112)，饱和度很高，不会被误删。
    bx0 = min(min(p[0] for p in pts) for pts in blocks) - 3
    bx1 = max(max(p[0] for p in pts) for pts in blocks) + 3
    by0 = min(min(p[1] for p in pts) for pts in blocks) - 3
    by1 = max(max(p[1] for p in pts) for pts in blocks) + 3

    fringe_total = 0
    for _pass in range(3):
        fringe = []
        for y in range(max(0, by0), min(h, by1 + 1)):
            for x in range(max(0, bx0), min(w, bx1 + 1)):
                r, g, b, a = px[y][x]
                if a < 10:
                    continue
                near = any(0 <= x + dx < w and 0 <= y + dy < h and px[y + dy][x + dx][3] < 10
                           for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
                if not near:
                    continue
                lum = (r + g + b) / 3.0
                if lum >= 110 and (max(r, g, b) - min(r, g, b)) <= 20:
                    fringe.append((x, y))
        if not fringe:
            break
        for (fx, fy) in fringe:
            r, g, b, _ = px[fy][fx]
            px[fy][fx] = (r, g, b, 0)
        fringe_total += len(fringe)
    print('白边清理：%d 个像素（范围 x[%d,%d] y[%d,%d]）' % (fringe_total, bx0, bx1, by0, by1))

    # ── ③ 统计剩余近白（应该只剩石头高光，且都是零散小点） ──
    left = sum(1 for y in range(h) for x in range(w) if is_white(px[y][x]))
    print('剩余近白像素：%d' % left)

    os.makedirs('assets/jungle', exist_ok=True)
    write_png(DST, w, h, px)
    print('输出 ->', DST, '%dx%d' % (w, h))

    # ── ④ 预览：深色底上放大 2 倍，看有没有残留白斑 ──
    Z = 2
    bg = (32, 44, 36, 255)
    pw, ph = w * Z, h * Z
    out = [[bg] * pw for _ in range(ph)]
    for y in range(h):
        for x in range(w):
            c = px[y][x]
            if c[3] < 8:
                continue
            for dy in range(Z):
                for dx in range(Z):
                    out[y * Z + dy][x * Z + dx] = c
    os.makedirs('docs/screenshots', exist_ok=True)
    write_png(PREVIEW, pw, ph, out)
    print('预览 ->', PREVIEW, '%dx%d' % (pw, ph))


if __name__ == '__main__':
    main()
