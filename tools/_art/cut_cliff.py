"""把 Seedream 出的品红底衔接件抠成透明，并合到天空上看效果。

品红底抠图的两个坑（都在这里处理了）：
  1. 阈值不能只判"是不是纯品红"，JPEG 压缩会在边缘留一圈渗色，
     判据放宽到 r>150 且 b>150 且 g<110，把渗色一起收进去。
  2. 抠完之后边缘还剩品红倾向的像素（r、b 都远高于 g），会有一圈紫边，
     按 g 把 r、b 压回去：r = min(r, g + 40)。
"""
import sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "bg_src", "seedream_jpg2png.png")
OUT = os.path.join(HERE, "preview_jungle_bg")
SKY = os.path.join(OUT, "sky_clean.png")


def cut_magenta(w, h, px):
    out = bytearray(w * h * 4)
    for i in range(w * h):
        r, g, b = px[i * 3], px[i * 3 + 1], px[i * 3 + 2]
        if r > 150 and b > 150 and g < 110:
            continue                      # 背景，alpha 保持 0
        # 去紫边
        if r > g + 40 and b > g + 40:
            r = min(r, g + 45)
            b = min(b, g + 45)
        o = i * 4
        out[o] = r
        out[o + 1] = g
        out[o + 2] = b
        out[o + 3] = 255
    return out


def main():
    w, h, ch, px = pix.load(SRC)
    print(f"源 {w}x{h} ch={ch}")
    rgba = cut_magenta(w, h, px)
    pix.save(os.path.join(OUT, "cliff_cut.png"), w, h, 4, rgba)

    # 统计透明比例
    n0 = sum(1 for i in range(w * h) if rgba[i * 4 + 3] == 0)
    print(f"  透明像素 {n0 / (w * h) * 100:.1f}%")

    # 合到天空上，1:1 居中裁一段看看
    sw, sh, sch, spx = pix.load(SKY)
    CW, CH = 1000, 420
    canvas = bytearray(CW * CH * 3)
    k = 2
    for y in range(CH):
        sy = min(sh - 1, (y + 300) * 1)
        for x in range(CW):
            d = (y * CW + x) * 3
            canvas[d] = spx[sy * sw * 3]
            canvas[d + 1] = spx[sy * sw * 3 + 1]
            canvas[d + 2] = spx[sy * sw * 3 + 2]

    # 把岩壁缩到一半再贴
    nw, nh = w // k, h // k
    for y in range(min(nh, CH)):
        for x in range(min(nw, CW)):
            s = ((y * k) * w + x * k) * 4
            a = rgba[s + 3]
            if a == 0:
                continue
            d = (y * CW + x) * 3
            canvas[d] = rgba[s]
            canvas[d + 1] = rgba[s + 1]
            canvas[d + 2] = rgba[s + 2]

    pix.save(os.path.join(OUT, "preview_cliff_on_sky.png"), CW, CH, 3, canvas)
    print("  preview_cliff_on_sky.png 已生成")


if __name__ == "__main__":
    main()
