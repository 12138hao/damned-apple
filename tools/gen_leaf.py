# -*- coding: utf-8 -*-
"""生成 16x16 像素落叶贴图。程序绘制，非模型生成。"""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
from gen_rope_assets import write_png

S = 16
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))

px = [[(0, 0, 0, 0) for _ in range(S)] for _ in range(S)]

cx, cy = 7.5, 7.5
A, B = 6.3, 2.9
ANG = math.radians(-38)
COS, SIN = math.cos(ANG), math.sin(ANG)

BODY = (156, 196, 104)     # 叶肉（偏浅绿，粒子上色后变黄/橙）
EDGE = (118, 158, 74)      # 边缘
VEIN = (92, 128, 56)       # 叶脉
STEM = (96, 74, 40)        # 叶柄

for y in range(S):
    for x in range(S):
        dx, dy = x - cx + 0.5, y - cy + 0.5
        rx = dx * COS + dy * SIN
        ry = -dx * SIN + dy * COS
        t = (rx / A) ** 2 + (ry / B) ** 2
        if t <= 1.0:
            px[y][x] = (*(EDGE if t > 0.62 else BODY), 255)

# 主叶脉：沿长轴一条
for i in range(-6, 7):
    rx, ry = i * 0.92, 0.0
    x = int(cx + rx * COS - ry * SIN)
    y = int(cy + rx * SIN + ry * COS)
    if 0 <= x < S and 0 <= y < S and px[y][x][3] > 0:
        px[y][x] = (*VEIN, 255)

# 侧脉：几根斜向短线
for k in (-3.0, -0.5, 2.0):
    for j in range(1, 4):
        rx = k + j * 0.35
        ry = j * 0.62
        for sgn in (-1, 1):
            x = int(cx + rx * COS - ry * sgn * SIN)
            y = int(cy + rx * SIN + ry * sgn * COS)
            if 0 <= x < S and 0 <= y < S and px[y][x][3] > 0:
                px[y][x] = (*VEIN, 255)

# 叶柄：从叶根往右下伸出去一点
for i in range(3):
    x = int(cx - (A + i * 0.9) * COS)
    y = int(cy - (A + i * 0.9) * SIN)
    if 0 <= x < S and 0 <= y < S:
        px[y][x] = (*STEM, 255)

out = os.path.join(ROOT, 'assets', 'ui', 'leaf.png')
write_png(out, S, S, px)
print('已生成', out, f'{S}x{S}')
