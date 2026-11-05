"""把 Seedream 出的两张品红底原图，抠图 → 裁到内容 → 降采样成游戏用的像素贴图。

尺寸基准：普通海盗苹果怪的本体是 106x123，
新怪「大 30%」→ 138x160，所以 ArtScale 保持 0.55 就自然大了三成。
"""
import pix
import sys

TARGET_BODY_H = 160
TARGET_SWORD_H = 154        # 参考图里刀的长度约等于苹果本体高度，稍微短一点
BG = (255, 0, 255)


def load_rgb(path):
    w, h, ch, px = pix.load(path)
    return w, h, pix.to_rgba(w, h, ch, px)


def chromakey(w, h, rgba):
    """品红抠图：按「洋红程度」给软 alpha，再清掉边缘渗色。"""
    out = bytearray(rgba)
    for i in range(w * h):
        r, g, b = out[i * 4], out[i * 4 + 1], out[i * 4 + 2]
        # 洋红程度：R、B 都高且 G 低
        mag = min(r, b) - g
        if mag >= 120:
            a = 0
        elif mag <= 45:
            a = 255
        else:
            a = int(255 * (1.0 - (mag - 45) / 75.0))
        out[i * 4 + 3] = a

    # 边缘渗色：不透明但仍偏洋红的像素，把 R/B 压向 G
    for i in range(w * h):
        if out[i * 4 + 3] == 0:
            continue
        r, g, b = out[i * 4], out[i * 4 + 1], out[i * 4 + 2]
        if r > 140 and b > 140 and g < 110:
            out[i * 4] = min(r, g + 45)
            out[i * 4 + 2] = min(b, g + 45)
    return out


def bbox(w, h, rgba, thr=8):
    x0, y0, x1, y1 = w, h, -1, -1
    for y in range(h):
        for x in range(w):
            if rgba[(y * w + x) * 4 + 3] > thr:
                if x < x0: x0 = x
                if x > x1: x1 = x
                if y < y0: y0 = y
                if y > y1: y1 = y
    return x0, y0, x1, y1


def resample(w, h, rgba, tw, th):
    """面积平均降采样（按 alpha 预乘，避免边缘发黑）。"""
    out = bytearray(tw * th * 4)
    sx = w / tw
    sy = h / th
    for ty in range(th):
        y0 = ty * sy
        y1 = y0 + sy
        iy0, iy1 = int(y0), min(h, int(y1 + 0.999))
        for tx in range(tw):
            x0 = tx * sx
            x1 = x0 + sx
            ix0, ix1 = int(x0), min(w, int(x1 + 0.999))
            ar = ag = ab = aa = 0.0
            wsum = 0.0
            for sy_ in range(iy0, iy1):
                wy = min(y1, sy_ + 1) - max(y0, sy_)
                if wy <= 0:
                    continue
                base = sy_ * w
                for sx_ in range(ix0, ix1):
                    wx = min(x1, sx_ + 1) - max(x0, sx_)
                    if wx <= 0:
                        continue
                    wt = wx * wy
                    i = (base + sx_) * 4
                    a = rgba[i + 3] / 255.0
                    ar += rgba[i] * a * wt
                    ag += rgba[i + 1] * a * wt
                    ab += rgba[i + 2] * a * wt
                    aa += a * wt
                    wsum += wt
            if wsum <= 0:
                continue
            o = (ty * tw + tx) * 4
            if aa <= 1e-6:
                out[o + 3] = 0
                continue
            out[o] = min(255, int(ar / aa + 0.5))
            out[o + 1] = min(255, int(ag / aa + 0.5))
            out[o + 2] = min(255, int(ab / aa + 0.5))
            out[o + 3] = min(255, int(aa / wsum * 255 + 0.5))
    return out


def process(src, target_h, out_path, name):
    w, h, rgba = load_rgb(src)
    rgba = chromakey(w, h, rgba)
    x0, y0, x1, y1 = bbox(w, h, rgba)
    cw, chh = x1 - x0 + 1, y1 - y0 + 1
    th = target_h
    tw = max(1, round(cw * th / chh))
    crop = bytearray(cw * chh * 4)
    for y in range(chh):
        s = ((y0 + y) * w + x0) * 4
        crop[y * cw * 4:(y + 1) * cw * 4] = rgba[s:s + cw * 4]
    small = resample(cw, chh, crop, tw, th)
    pix.save(out_path, tw, th, 4, small)
    print('%-14s 原图内容 %dx%d → %dx%d   %s' % (name, cw, chh, tw, th, out_path))
    return tw, th


if __name__ == '__main__':
    process('body_raw.png', TARGET_BODY_H, 'small_body.png', '本体')
    process('sword_raw.png', TARGET_SWORD_H, 'small_sword.png', '刀')
