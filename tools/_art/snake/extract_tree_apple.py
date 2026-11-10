"""从「像素蛇怪生成 (6).png」这棵树里抠一颗苹果出来，存成 assets/eden/tree_apple.png。

树图里的苹果约 30px，周围全是绿叶。做法：
  ① 先按「绿占优」把叶片打成透明（r 偏高的黄绿嫩芽不会被误杀）；
  ② 只保留最大的连通块（把边缘残留的叶子碎片丢掉）；
  ③ 按不透明像素裁紧，留 1px 余量。
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import pix

SRC = '像素蛇怪生成 (6).png'
# 右下角最外侧那颗，四周大部分是透明的
BOX = (1272, 940, 1345, 1012)


def main():
    w, h, ch, px = pix.load(SRC)
    x0, y0, x1, y1 = BOX
    cw, chh = x1 - x0, y1 - y0
    buf = bytearray(cw * chh * 4)
    for y in range(chh):
        for x in range(cw):
            si = ((y + y0) * w + (x + x0)) * 4
            di = (y * cw + x) * 4
            r, g, b, a = px[si], px[si + 1], px[si + 2], px[si + 3]
            if a < 40 or (g > r * 1.15 and g > b * 1.15):
                continue                      # 透明 / 绿叶 → 丢掉
            buf[di:di + 4] = px[si:si + 4]

    # 只留最大连通块
    seen = bytearray(cw * chh)
    best, bestn = None, 0
    for y in range(chh):
        for x in range(cw):
            if seen[y * cw + x] or buf[(y * cw + x) * 4 + 3] < 100:
                continue
            st = [(x, y)]; seen[y * cw + x] = 1
            cells = []
            while st:
                cx, cy = st.pop(); cells.append((cx, cy))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < cw and 0 <= ny < chh and not seen[ny * cw + nx] \
                            and buf[(ny * cw + nx) * 4 + 3] >= 100:
                        seen[ny * cw + nx] = 1; st.append((nx, ny))
            if len(cells) > bestn:
                bestn, best = len(cells), cells
    keep = set(best)
    for y in range(chh):
        for x in range(cw):
            if (x, y) not in keep:
                buf[(y * cw + x) * 4:(y * cw + x) * 4 + 4] = bytes(4)

    # 裁紧
    bx0, by0, bx1, by1 = 10 ** 9, 10 ** 9, -1, -1
    for y in range(chh):
        for x in range(cw):
            if buf[(y * cw + x) * 4 + 3] > 60:
                bx0 = min(bx0, x); by0 = min(by0, y)
                bx1 = max(bx1, x); by1 = max(by1, y)
    bx0, by0 = max(0, bx0 - 1), max(0, by0 - 1)
    bx1, by1 = min(cw - 1, bx1 + 1), min(chh - 1, by1 + 1)
    nw, nh = bx1 - bx0 + 1, by1 - by0 + 1
    out = bytearray(nw * nh * 4)
    for y in range(nh):
        for x in range(nw):
            si = ((y + by0) * cw + (x + bx0)) * 4
            out[(y * nw + x) * 4:(y * nw + x) * 4 + 4] = buf[si:si + 4]

    dst = 'assets/eden/tree_apple.png'
    pix.save(dst, nw, nh, 4, out)
    print(f'-> {dst}  {nw}x{nh}  最大连通块 {bestn}px')


if __name__ == '__main__':
    main()
