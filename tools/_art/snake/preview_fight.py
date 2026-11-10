"""把抠好的蛇叠到 eden 那棵树上，按战斗镜头（zoom + 抬高）渲染一张 800x450 预览。"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import pix

TREE = '像素蛇怪生成 (6).png'
TREE_POS = (6952.0, 5.0)      # sprite 世界坐标（eden.tscn 里）
TREE_SIZE = (1536, 1536)

SNAKE = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'snake_coil_raw.png')
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, 'preview_fight.png')

SNAKE_W = float(sys.argv[3]) if len(sys.argv) > 3 else 220.0   # 蛇的世界宽度
TRUNK_X = 6964.0               # 树干中心世界 x
BOTTOM_Y = 700.0               # 蛇尾落地世界 y

CAM = (6964.0, 560.0)          # 战斗镜头中心
ZOOM = 0.62


def sample(img, iw, ih, p4, wx, wy, ox, oy, scale=1.0):
    ix = int((wx - ox) / scale)
    iy = int((wy - oy) / scale)
    if ix < 0 or iy < 0 or ix >= iw or iy >= ih:
        return (0, 0, 0, 0)
    o = (iy * iw + ix) * 4
    return (p4[o], p4[o + 1], p4[o + 2], p4[o + 3])


def over(dst, src):
    sa = src[3] / 255.0
    if sa <= 0:
        return dst
    da = dst[3] / 255.0
    oa = sa + da * (1 - sa)
    if oa <= 0:
        return (0, 0, 0, 0)
    r = (src[0] * sa + dst[0] * da * (1 - sa)) / oa
    g = (src[1] * sa + dst[1] * da * (1 - sa)) / oa
    b = (src[2] * sa + dst[2] * da * (1 - sa)) / oa
    return (r, g, b, oa * 255)


def main():
    tw, th, tch, tp = pix.load(TREE)
    sw, sh, sch, sp = pix.load(SNAKE)
    tch2 = tch

    sscale = SNAKE_W / sw
    sh_h = sh * sscale
    sleft = TRUNK_X - SNAKE_W / 2
    stop = BOTTOM_Y - sh_h

    W, H = 800, 450
    out = bytearray(W * H * 4)
    for py in range(H):
        wy = CAM[1] + (py - H / 2) / ZOOM
        for pxx in range(W):
            wx = CAM[0] + (pxx - W / 2) / ZOOM
            # 背景：从下往上 紫→粉→金 的近似渐变（真实背景是程序化画的，这里只作参照）
            t = min(1.0, max(0.0, (wy - 150) / 750.0))
            r = int(120 + 135 * t)
            g = int(110 + 120 * t)
            b = int(170 - 40 * t)
            px_out = (r, g, b, 255)

            tp4 = sample(TREE, tw, th, tp, wx, wy, TREE_POS[0] - TREE_SIZE[0] / 2,
                         TREE_POS[1] - TREE_SIZE[1] / 2, 1.0)
            px_out = over(px_out, tp4)

            if sleft <= wx <= sleft + SNAKE_W and stop <= wy <= BOTTOM_Y:
                sp4 = sample(SNAKE, sw, sh, sp, wx, wy, sleft, stop, sscale)
                px_out = over(px_out, sp4)

            d = (py * W + pxx) * 4
            out[d] = max(0, min(255, int(px_out[0])))
            out[d + 1] = max(0, min(255, int(px_out[1])))
            out[d + 2] = max(0, min(255, int(px_out[2])))
            out[d + 3] = 255

    pix.save(OUT, W, H, 4, out)
    print(f'-> {OUT}  蛇 {SNAKE_W}x{sh_h:.0f} 世界像素，占 y {stop:.0f}~{BOTTOM_Y:.0f}')


if __name__ == '__main__':
    main()
