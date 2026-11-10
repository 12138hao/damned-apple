"""把藤桥和瀑布拆成可复用的零件，并做无缝处理。

藤桥：原图 2880x864，柱子只在 x≈0-253 / 549-865 / 2001-2326 / 2706-2879 四处，
      中间是 1136px 的长跨 —— 不是等距的，整张图不能直接平铺。
      所以拆成「桥面条」（横向无缝，可无限重复）+「单根柱子」（当装饰单独摆）。
瀑布：拆成「水口」「下落水幕」（上下左右都无缝，可滚动循环）+「底部水花」。
"""
import sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "preview_jungle_bg")
BRIDGE = r"C:\Users\leader\Downloads\像素藤桥绘制 (2).png"
WATER = os.path.join(HERE, "bg_src", "waterfall_src.png")


def crop(w, h, ch, px, x0, y0, cw, chh):
    out = bytearray(cw * chh * ch)
    for y in range(chh):
        s = ((y0 + y) * w + x0) * ch
        d = y * cw * ch
        out[d:d + cw * ch] = px[s:s + cw * ch]
    return out


def mirror_h(w, h, ch, px):
    """横向镜像平铺：A + 左右翻转的 A。

    藤桥原图是 AI 画的，每一行的"周期"都不一样（量出来 36~256 都有），
    根本不存在一个能整除的周期，交叉淡化做出来还是能看到一条竖缝。
    镜像拼接是数学上天然无缝的：接缝两边像素永远连续，代价只是一点点对称感，
    对藤蔓这种高频有机纹理基本看不出来。
    """
    W = w * 2
    out = bytearray(W * h * ch)
    for y in range(h):
        for x in range(w):
            s = (y * w + x) * ch
            d = (y * W + x) * ch
            out[d:d + ch] = px[s:s + ch]
            dx = W - 1 - x
            d2 = (y * W + dx) * ch
            out[d2:d2 + ch] = px[s:s + ch]
    return W, h, out


def make_seamless_h(w, h, ch, px, blend):
    """横向无缝：把尾部 blend 列交叉淡化进头部，宽度减少 blend。"""
    W = w - blend
    out = bytearray(W * h * ch)
    for y in range(h):
        for x in range(W):
            for c in range(ch):
                a = px[(y * w + x) * ch + c]
                if x < blend:
                    b = px[(y * w + W + x) * ch + c]
                    t = x / blend
                    a = int(a * t + b * (1 - t))
                out[(y * W + x) * ch + c] = a
    return W, h, out


def make_seamless_v(w, h, ch, px, blend):
    """纵向无缝：把尾部 blend 行交叉淡化进头部，高度减少 blend。"""
    H = h - blend
    out = bytearray(w * H * ch)
    for y in range(H):
        for x in range(w):
            for c in range(ch):
                a = px[(y * w + x) * ch + c]
                if y < blend:
                    b = px[((H + y) * w + x) * ch + c]
                    t = y / blend
                    a = int(a * t + b * (1 - t))
                out[(y * w + x) * ch + c] = a
    return w, H, out


def stitch_v_shift(w, h, ch, px, blend, shift):
    """把同一段水幕接长：第二段横向错开 shift 再接到下面，接缝处交叉淡化。

    水幕的竖向条纹本来就是随机的，横向错开半幅之后，接缝处看不出是"同一个图案又来了"。
    比单纯上下镜像好，镜像会让一半的水往上流。
    """
    H = h * 2 - blend
    out = bytearray(w * H * ch)
    out[0:h * w * ch] = px[0:h * w * ch]
    for y in range(h - blend, H):
        sy = y - (h - blend)
        for x in range(w):
            sx = (x + shift) % w
            s = (sy * w + sx) * ch
            d = (y * w + x) * ch
            if y < h:
                t = (y - (h - blend)) / blend
                for c in range(ch):
                    out[d + c] = int(px[(y * w + x) * ch + c] * (1 - t) + px[s + c] * t)
            else:
                for c in range(ch):
                    out[d + c] = px[s + c]
    return w, H, out


def scale_nn(w, h, ch, px, nw, nh):
    out = bytearray(nw * nh * ch)
    for y in range(nh):
        sy = min(h - 1, y * h // nh)
        for x in range(nw):
            sx = min(w - 1, x * w // nw)
            s = (sy * w + sx) * ch
            d = (y * nw + x) * ch
            out[d:d + ch] = px[s:s + ch]
    return out


def blit(canvas, cw, chh, src, w, h, ch, ox, oy):
    """把带 alpha 的图叠到 canvas（RGBA）上，简单 over 合成。"""
    for y in range(h):
        dy = oy + y
        if dy < 0 or dy >= chh:
            continue
        for x in range(w):
            dx = ox + x
            if dx < 0 or dx >= cw:
                continue
            s = (y * w + x) * ch
            a = src[s + 3] if ch == 4 else 255
            if a == 0:
                continue
            d = (dy * cw + dx) * 4
            if a == 255:
                canvas[d] = src[s]; canvas[d + 1] = src[s + 1]; canvas[d + 2] = src[s + 2]
            else:
                inv = 255 - a
                canvas[d] = (src[s] * a + canvas[d] * inv) // 255
                canvas[d + 1] = (src[s + 1] * a + canvas[d + 1] * inv) // 255
                canvas[d + 2] = (src[s + 2] * a + canvas[d + 2] * inv) // 255
            canvas[d + 3] = 255


def main():
    os.makedirs(OUT, exist_ok=True)

    # ---------------- 藤桥 ----------------
    w, h, ch, px = pix.load(BRIDGE)
    print(f"藤桥 {w}x{h} ch={ch}")

    # 桥面：取 x 900..1980 这段没有柱头干扰的桥面，镜像拼接做到横向无缝
    deck = crop(w, h, ch, px, 900, 105, 1080, 195)
    dw, dh, deck = mirror_h(1080, 195, ch, deck)
    pix.save(os.path.join(OUT, "bridge_deck_tile.png"), dw, dh, ch, deck)
    print(f"  bridge_deck_tile.png  {dw}x{dh}  (0.5 倍时每 {dw//2}px 重复一次)")

    # 柱子：2001..2326 这根最完整，从桥面下方一点开始切
    pillar = crop(w, h, ch, px, 1995, 240, 340, 576)
    pix.save(os.path.join(OUT, "bridge_pillar.png"), 340, 576, ch, pillar)
    print(f"  bridge_pillar.png  340x576")

    # ---------------- 瀑布 ----------------
    w2, h2, ch2, px2 = pix.load(WATER)
    print(f"\n瀑布 {w2}x{h2} ch={ch2}")
    # alpha 状况
    a_top = sum(px2[x * 4 + 3] for x in range(0, w2, 37)) // len(range(0, w2, 37))
    a_bot = sum(px2[((h2 - 1) * w2 + x) * 4 + 3] for x in range(0, w2, 37)) // len(range(0, w2, 37))
    print(f"  顶部平均 alpha={a_top}  底部平均 alpha={a_bot}")

    lip = crop(w2, h2, ch2, px2, 0, 0, w2, 96)
    pix.save(os.path.join(OUT, "waterfall_lip.png"), w2, 96, ch2, lip)

    # 下落水幕：96..450。注意不能吃到 452 开始的水花，否则那一圈白色泡沫会被当成
    # 水幕的一部分反复重复，画面上每 150px 就横着亮一道。
    fall = crop(w2, h2, ch2, px2, 0, 96, w2, 354)
    fw, fh, fall = make_seamless_h(w2, 354, ch2, fall, 40)
    # 354px 的循环太短，一眼就被看出来；用横向错开的方式接长一倍
    fw, fh, fall = stitch_v_shift(fw, fh, ch2, fall, 90, fw // 2)
    fw, fh, fall = make_seamless_v(fw, fh, ch2, fall, 90)
    pix.save(os.path.join(OUT, "waterfall_fall.png"), fw, fh, ch2, fall)
    print(f"  waterfall_fall.png  {fw}x{fh}  上下左右都无缝，循环周期 {fh}px")

    splash = crop(w2, h2, ch2, px2, 0, 452, w2, 113)
    pix.save(os.path.join(OUT, "waterfall_splash.png"), w2, 113, ch2, splash)
    print(f"  waterfall_splash.png {w2}x113")

    # ---------------- 合成假想图 ----------------
    CW, CH = 1600, 450
    canvas = bytearray(CW * CH * 4)
    # 天空：把纯渐变铺满
    sw, sh, sch, spx = pix.load(os.path.join(OUT, "sky_clean.png"))
    sky_s = scale_nn(sw, sh, sch, spx, CW, CH)
    for i in range(CW * CH):
        canvas[i * 4] = sky_s[i * 3]
        canvas[i * 4 + 1] = sky_s[i * 3 + 1]
        canvas[i * 4 + 2] = sky_s[i * 3 + 2]
        canvas[i * 4 + 3] = 255

    # 瀑布：0.5 倍，从桥面下沿开始铺
    fw2, fh2 = fw // 2, fh // 2
    fall_s = scale_nn(fw, fh, ch2, fall, fw2, fh2)
    for ty in range(247, CH, fh2):
        for tx in range(0, CW, fw2):
            blit(canvas, CW, CH, fall_s, fw2, fh2, ch2, tx, ty)
    sw2, sh2 = w2 // 2, 113 // 2
    splash_s = scale_nn(w2, 113, ch2, splash, sw2, sh2)
    for tx in range(0, CW, sw2):
        blit(canvas, CW, CH, splash_s, sw2, sh2, ch2, tx, CH - sh2)

    # 柱子：0.5 倍，每 330px 一根。柱子比瀑布近，所以画在瀑布之后、桥面之前
    pw, ph2 = 340 // 2, 576 // 2
    pillar_s = scale_nn(340, 576, ch, pillar, pw, ph2)
    for tx in range(120, CW, 330):
        blit(canvas, CW, CH, pillar_s, pw, ph2, ch, tx, 210)

    # 桥面：0.5 倍镜像平铺，桥面顶放在 y=150
    ddw, ddh = dw // 2, dh // 2
    deck_s = scale_nn(dw, dh, ch, deck, ddw, ddh)
    for tx in range(0, CW, ddw):
        blit(canvas, CW, CH, deck_s, ddw, ddh, ch, tx, 150)

    # 云先不贴。从原图抠出来的云边缘有方块感（原图云本身很淡，
    # 阈值一拉就出现块状边），等做"可移动云层"时按天空色调重新生成。
    pix.save(os.path.join(OUT, "preview_mockup.png"), CW, CH, 4, canvas)
    print(f"\n  preview_mockup.png  {CW}x{CH}（天空+瀑布+桥面+柱子 合成示意）")

    # ---------------- 水幕滚动三帧 ----------------
    FH, FW, PANES = 300, 420, 3
    sheet = bytearray(FW * PANES * FH * 3)
    for i in range(PANES):
        off = i * fh // PANES
        for y in range(FH):
            sy = (y * fh // FH + off) % fh
            for x in range(FW):
                sx = x * fw // FW
                s = (sy * fw + sx) * ch2
                d = (y * (FW * PANES) + i * FW + x) * 3
                sheet[d] = fall[s]
                sheet[d + 1] = fall[s + 1]
                sheet[d + 2] = fall[s + 2]
    pix.save(os.path.join(OUT, "preview_waterfall_frames.png"), FW * PANES, FH, 3, sheet)
    print(f"  preview_waterfall_frames.png  {FW*PANES}x{FH}（同一张水幕滚动 0 / 1/3 / 2/3 周期）")


if __name__ == "__main__":
    main()
