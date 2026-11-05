"""拼一张静止姿势的预览图：本体 + 持刀手 + 散手，4 倍最近邻放大，垫棋盘格。"""
import pix

SCALE = 4
PAD = 24          # 画布留边（美术像素）

# 刀握把在刀贴图里的位置（比例）——刀尖朝上，手在下方
GRIP_X_RATIO = 0.54
GRIP_Y_RATIO = 0.818

# 相对脚底中心的位置（美术像素）
BODY_FOOT = (0, 0)
SWORD_GRIP = (-62, -38)
FREE_HAND = (62, -38)


def load(path):
    w, h, ch, px = pix.load(path)
    return w, h, pix.to_rgba(w, h, ch, px)


def blit(dst, dw, dh, src, sw, sh, ox, oy, flip=False):
    """把 src 贴到 dst 的 (ox,oy)（左上角），带 alpha 混合。"""
    for y in range(sh):
        dy = oy + y
        if dy < 0 or dy >= dh:
            continue
        for x in range(sw):
            dx = ox + (sw - 1 - x if flip else x)
            if dx < 0 or dx >= dw:
                continue
            s = (y * sw + x) * 4
            a = src[s + 3]
            if a == 0:
                continue
            d = (dy * dw + dx) * 4
            if a == 255:
                dst[d:d + 4] = src[s:s + 4]
            else:
                k = a / 255.0
                for c in range(3):
                    dst[d + c] = int(dst[d + c] * (1 - k) + src[s + c] * k)
                dst[d + 3] = max(dst[d + 3], a)


def main():
    bw, bh, body = load('small_body.png')
    sw, sh, sword = load('small_sword.png')
    hw, hh, hand = load('../../assets/enemies/undead_apple/undead_apple_hand_l.png')

    # 手放大 1.3 倍（新怪比老怪大 30%），用最近邻保持像素感
    hs = 1.3
    nhw, nhh = int(hw * hs), int(hh * hs)
    hand13 = bytearray(nhw * nhh * 4)
    for y in range(nhh):
        for x in range(nhw):
            s = (int(y / hs) * hw + int(x / hs)) * 4
            d = (y * nhw + x) * 4
            hand13[d:d + 4] = hand[s:s + 4]
    hw, hh, hand = nhw, nhh, hand13

    # 画布：以脚底中心为原点
    W, H = 240, 220
    ORIGIN = (120, 180)     # 原点在画布里的位置
    canvas = bytearray(W * H * 4)
    # 棋盘格
    for y in range(H):
        for x in range(W):
            v = 44 if ((x // 8) + (y // 8)) % 2 else 32
            d = (y * W + x) * 4
            canvas[d] = canvas[d + 1] = canvas[d + 2] = v
            canvas[d + 3] = 255

    # 本体：底边对齐原点
    blit(canvas, W, H, body, bw, bh,
         ORIGIN[0] - bw // 2, ORIGIN[1] - bh)

    # 散手（右侧）
    blit(canvas, W, H, hand, hw, hh,
         ORIGIN[0] + FREE_HAND[0] - hw // 2, ORIGIN[1] + FREE_HAND[1] - hh // 2)

    # 刀：握把点对准 SWORD_GRIP
    gx = int(sw * GRIP_X_RATIO)
    gy = int(sh * GRIP_Y_RATIO)
    blit(canvas, W, H, sword, sw, sh,
         ORIGIN[0] + SWORD_GRIP[0] - gx, ORIGIN[1] + SWORD_GRIP[1] - gy)

    # 4 倍放大
    ZW, ZH = W * SCALE, H * SCALE
    zoom = bytearray(ZW * ZH * 4)
    for y in range(ZH):
        sy = y // SCALE
        for x in range(ZW):
            sx = x // SCALE
            s = (sy * W + sx) * 4
            d = (y * ZW + x) * 4
            zoom[d:d + 4] = canvas[s:s + 4]
    pix.save('preview_idle.png', ZW, ZH, 4, zoom)
    print('已写 preview_idle.png  %dx%d' % (ZW, ZH))


if __name__ == '__main__':
    main()
