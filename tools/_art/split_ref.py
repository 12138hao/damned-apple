"""把参考图拆成「刀+持刀手」和「苹果本体」两层，并做像素化。

参考图 1536x1536 本身已经是像素画（放大会看到方块），
所以这里只做三件事：抠白底 → 沿交界切成两层 → 按目标像素尺寸降采样。

切线 CUT 用「刀一侧在左边」的多段线描述，可以随时调。
"""
import pix
from collections import deque

SRC = 'ref.png'

# 切线（1536 坐标系，从下往上给点也行，代码里会按 y 排序）
CUT = [
    (140, 80),
    (155, 250),
    (180, 350),
    (222, 430),
    (258, 480),
    (300, 570),
    (360, 700),
    (420, 830),
    (470, 940),
    (515, 1010),
    (570, 1055),
    (598, 1110),
    (600, 1175),
    (586, 1228),
    (558, 1272),
    (515, 1312),
    (465, 1348),
    (425, 1372),
    (395, 1392),
]


def build_alpha(w, h, rgba, white=236):
    """从四边洪水填充近白背景。返回 alpha（0=透明）。"""
    alpha = bytearray([255]) * (w * h)
    q = deque()
    for x in range(w):
        q.append(x)
        q.append((h - 1) * w + x)
    for y in range(h):
        q.append(y * w)
        q.append(y * w + w - 1)
    seen = bytearray(w * h)
    while q:
        i = q.popleft()
        if seen[i]:
            continue
        seen[i] = 1
        if (rgba[i * 4] + rgba[i * 4 + 1] + rgba[i * 4 + 2]) / 3 >= white:
            alpha[i] = 0
        else:
            continue
        x, y = i % w, i // w
        if x > 0:
            q.append(i - 1)
        if x < w - 1:
            q.append(i + 1)
        if y > 0:
            q.append(i - w)
        if y < h - 1:
            q.append(i + w)
    return alpha


def cut_x(y, pts):
    """给 y 求切线的 x（分段线性）。超出范围就取端点。"""
    pts = sorted(pts, key=lambda p: p[1])
    if y <= pts[0][1]:
        return pts[0][0]
    if y >= pts[-1][1]:
        return pts[-1][0]
    for i in range(len(pts) - 1):
        x0, y0 = pts[i]
        x1, y1 = pts[i + 1]
        if y0 <= y <= y1:
            t = (y - y0) / max(1e-6, (y1 - y0))
            return x0 + (x1 - x0) * t
    return pts[-1][0]


def main():
    w, h, ch, px = pix.load(SRC)
    rgba = pix.to_rgba(w, h, ch, px)
    alpha = build_alpha(w, h, rgba)

    # ── 预览：把切线和两侧染色叠到原图上 ──
    prev = bytearray(rgba)
    for y in range(h):
        cx = int(cut_x(y, CUT))
        for x in range(max(0, cx - 2), min(w, cx + 3)):
            i = (y * w + x) * 4
            prev[i], prev[i + 1], prev[i + 2] = 0, 255, 60
    # 刀一侧染半透明红
    for y in range(0, h, 1):
        cx = int(cut_x(y, CUT))
        for x in range(0, max(0, cx)):
            i = (y * w + x) * 4
            if alpha[y * w + x]:
                prev[i] = (prev[i] + 255) // 2
                prev[i + 1] = prev[i + 1] // 2
                prev[i + 2] = prev[i + 2] // 2
    pix.save('prev_cut.png', w, h, 4, prev)
    print('已写 prev_cut.png')

    # ── 分两层 ──
    sword = bytearray(w * h * 4)
    body = bytearray(w * h * 4)
    sb = [w, h, -1, -1]
    bb = [w, h, -1, -1]
    for y in range(h):
        cx = int(cut_x(y, CUT))
        for x in range(w):
            i = y * w + x
            if not alpha[i]:
                continue
            d = i * 4
            s = i * 4
            if x < cx:
                sword[d:d + 4] = rgba[s:s + 4]
                sb[0] = min(sb[0], x); sb[1] = min(sb[1], y)
                sb[2] = max(sb[2], x); sb[3] = max(sb[3], y)
            else:
                body[d:d + 4] = rgba[s:s + 4]
                bb[0] = min(bb[0], x); bb[1] = min(bb[1], y)
                bb[2] = max(bb[2], x); bb[3] = max(bb[3], y)

    print('刀 包围盒 x[%d,%d] y[%d,%d]  %dx%d' % (sb[0], sb[2], sb[1], sb[3],
                                                sb[2] - sb[0] + 1, sb[3] - sb[1] + 1))
    print('本体 包围盒 x[%d,%d] y[%d,%d]  %dx%d' % (bb[0], bb[2], bb[1], bb[3],
                                                 bb[2] - bb[0] + 1, bb[3] - bb[1] + 1))

    def crop(buf, box):
        x0, y0, x1, y1 = box
        cw, chh = x1 - x0 + 1, y1 - y0 + 1
        out = bytearray(cw * chh * 4)
        for y in range(chh):
            src = ((y0 + y) * w + x0) * 4
            out[y * cw * 4:(y + 1) * cw * 4] = buf[src:src + cw * 4]
        return cw, chh, out

    cw, chh, cout = crop(sword, sb)
    pix.save('layer_sword.png', cw, chh, 4, cout)
    bw, bh, bout = crop(body, bb)
    pix.save('layer_body.png', bw, bh, 4, bout)
    print('已写 layer_sword.png / layer_body.png')


if __name__ == '__main__':
    main()
