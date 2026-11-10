"""把「旧贴图翻转拼接」和「新循环贴图直拼」按游戏内尺度上下对比。"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix


def blit(dst, dw, dh, src, sw, sh, ox, oy, flip=False):
    for y in range(sh):
        dy = oy + y
        if dy < 0 or dy >= dh:
            continue
        for x in range(sw):
            dx = ox + (sw - 1 - x if flip else x)
            if dx < 0 or dx >= dw:
                continue
            o = (y * sw + x) * 4
            a = src[o + 3]
            if a == 0:
                continue
            d = (dy * dw + dx) * 4
            if a == 255:
                dst[d:d + 4] = src[o:o + 4]
            else:
                t = a / 255
                dst[d] = int(src[o] * t + dst[d] * (1 - t))
                dst[d + 1] = int(src[o + 1] * t + dst[d + 1] * (1 - t))
                dst[d + 2] = int(src[o + 2] * t + dst[d + 2] * (1 - t))


def scaled(w, h, px4, s):
    nw, nh = int(w * s), int(h * s)
    out = bytearray(nw * nh * 4)
    for y in range(nh):
        sy = int(y / s)
        for x in range(nw):
            sx = int(x / s)
            o = (sy * w + sx) * 4
            out[(y * nw + x) * 4:(y * nw + x) * 4 + 4] = px4[o:o + 4]
    return nw, nh, out


def main(oldp, newp, out, tiles=3):
    ow, oh, och, op = pix.load(oldp)
    nw, nh, nch, np_ = pix.load(newp)
    osw, osh, os_ = scaled(ow, oh, bytearray(op), 0.375)      # 旧：2048->768
    nsw, nsh, ns_ = scaled(nw, nh, bytearray(np_), 126.75 / nh)

    W = max(osw, nsw) * tiles
    gap = 18
    H = osh + gap + nsh + 2 * gap
    canvas = bytearray(W * H * 4)
    for i in range(W * H):
        canvas[i * 4:i * 4 + 4] = bytes((22, 32, 26, 255))

    for k in range(tiles):
        blit(canvas, W, H, os_, osw, osh, k * osw, gap, flip=(k % 2 == 1))
    for k in range(tiles):
        blit(canvas, W, H, ns_, nsw, nsh, k * nsw, gap * 2 + osh, flip=False)

    pix.save(out, W, H, 4, canvas)
    print(f'{out} {W}x{H}  旧 {osw}x{osh}/片(翻转交替)  新 {nsw}x{nsh}/片(直拼)')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3])
