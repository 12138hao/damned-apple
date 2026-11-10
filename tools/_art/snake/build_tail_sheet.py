"""把一张蛇尾贴图「掰」成 12 帧甩尾动画，拼成一张横向 sprite sheet。

思路：不重新画，也不做整体缩放。先把源图纵向放大到目标粗细，然后**逐列**复制并
按弧线做竖直位移 —— 每一列像素本身保持完整，所以鳞片不会被拉花，
尾巴看上去就是被"抬起 / 甩出去"。

帧序（世界像素，A>0 = 尾尖抬高）：
  0~4  蓄力：慢慢探出、抬起（A 从 4 到 70）
  5~8  甩击：从抬起翻成平扫，一直扫到墙（560）
  9~11 回抽：缩回树根
"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import pix

SRC = 'assets/eden/snake_tail.png'
OUT = 'assets/eden/snake_tail_sheet.png'

VERT = 1.0 / 3.0         # 整体压扁到 1/3（粗细 / 抬升 / 余量一起缩）
THICK = 1.35 * VERT      # 纵向放大 = 尾巴粗细
CELL_W = 560             # 每帧格子宽（= 一边限制区宽度）
UP_MARGIN = int(round(80 * VERT))    # 抬升余量
DOWN_MARGIN = int(round(24 * VERT))  # 下压余量

FRAMES = [
    # (长度, 抬升)
    (115, 4), (150, 20), (185, 38), (215, 56), (240, 70),   # 蓄力 0..4
    (300, 62), (390, 40), (480, 8), (560, -16),             # 甩击 5..8
    (470, -24), (330, -16), (170, -6),                      # 回抽 9..11
]


def scale_v(w, h, px, scale):
    nh = max(1, int(round(h * scale)))
    out = bytearray(w * nh * 4)
    for y in range(nh):
        sy = min(h - 1, int(y / scale))
        src = sy * w * 4
        out[y * w * 4:(y + 1) * w * 4] = px[src:src + w * 4]
    return nh, out


def main():
    w, h, ch, px = pix.load(SRC)
    assert ch == 4, '需要带 alpha 的 png'

    sh, scaled = scale_v(w, h, px, THICK)
    cell_h = sh + UP_MARGIN + DOWN_MARGIN
    base = UP_MARGIN                     # 未抬升时尾根下沿在格子里的 y
    print(f'源 {w}x{h} -> 加粗 {w}x{sh}；格子 {CELL_W}x{cell_h}，基准线 {base + sh}')

    n = len(FRAMES)
    sheet = bytearray(CELL_W * n * cell_h * 4)

    for fi, (length, lift) in enumerate(FRAMES):
        L = min(CELL_W, max(2, length))
        ox = fi * CELL_W
        for x in range(L):
            u = x / (L - 1)
            sx = int(round(u * (w - 1)))
            dy = -int(round(lift * VERT * (u ** 1.5)))
            for r in range(sh):
                sy = base + dy + r
                if sy < 0 or sy >= cell_h:
                    continue
                si = (r * w + sx) * 4
                di = ((sy * CELL_W * n) + ox + x) * 4
                sheet[di:di + 4] = scaled[si:si + 4]

    pix.save(OUT, CELL_W * n, cell_h, 4, sheet)
    print(f'-> {OUT}  {CELL_W * n}x{cell_h}  {n} 帧')


if __name__ == '__main__':
    main()
