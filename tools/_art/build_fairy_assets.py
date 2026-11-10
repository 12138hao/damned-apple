"""把仙子 Boss 的所有素材处理成游戏可用：抠背景、切帧、统一基线。

三类来源，三种处理方式：
  1. idle_sheet.png   已经是 RGBA 透明，直接抄
  2. cast_sheet.png   白底 + 白裙子 —— 不能按颜色抠，只能用**从四边泛洪**，
                      白裙子被黑描边围住，泛洪进不去
  3. 其余 Seedream 图  品红底，按颜色抠 + 去紫边

俯冲/喘气两张是"四帧横排"，要按内容列切分，再统一到同一格尺寸、
同一脚底基线，否则动画会抖。
"""
import sys, os, collections

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pix

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "bg_src", "seedream")
OUT = r"D:\h\my-game\assets\boss\fairy"
PROVIDED = r"D:\workbuddyhuancun\2026-11-05-15-03-54\pixel-fairy"


def key_magenta(w, h, ch, px):
    out = bytearray(w * h * 4)
    for i in range(w * h):
        r, g, b = px[i * ch], px[i * ch + 1], px[i * ch + 2]
        if r > 150 and b > 150 and g < 110:
            continue
        if r > g + 40 and b > g + 40:
            r = min(r, g + 45); b = min(b, g + 45)
        o = i * 4
        out[o] = r; out[o + 1] = g; out[o + 2] = b; out[o + 3] = 255
    return out


def key_white_flood(w, h, ch, px):
    """从四边泛洪吃白底。白裙子在里面，被黑描边挡住，不会被吃。"""
    out = bytearray(w * h * 4)
    for i in range(w * h):
        o = i * 4
        out[o] = px[i * ch]; out[o + 1] = px[i * ch + 1]; out[o + 2] = px[i * ch + 2]
        out[o + 3] = 255

    def is_white(i):
        r, g, b = px[i * ch], px[i * ch + 1], px[i * ch + 2]
        return r > 232 and g > 232 and b > 232

    seen = bytearray(w * h)
    stack = []
    for x in range(w):
        for y in (0, h - 1):
            i = y * w + x
            if is_white(i) and not seen[i]:
                seen[i] = 1; stack.append(i)
    for y in range(h):
        for x in (0, w - 1):
            i = y * w + x
            if is_white(i) and not seen[i]:
                seen[i] = 1; stack.append(i)

    while stack:
        p = stack.pop()
        out[p * 4 + 3] = 0
        y, x = divmod(p, w)
        for dy in (-1, 0, 1):
            ny = y + dy
            if ny < 0 or ny >= h: continue
            for dx in (-1, 0, 1):
                nx = x + dx
                if nx < 0 or nx >= w: continue
                q = ny * w + nx
                if not seen[q] and is_white(q):
                    seen[q] = 1; stack.append(q)
    return out


def tight(w, h, ch, px):
    minx, maxx, miny, maxy = w, 0, h, 0
    for y in range(h):
        base = y * w * 4
        for x in range(w):
            if px[base + x * 4 + 3] > 24:
                if x < minx: minx = x
                if x > maxx: maxx = x
                if y < miny: miny = y
                if y > maxy: maxy = y
    if maxx < minx:
        return w, h, px
    cw, chh = maxx - minx + 1, maxy - miny + 1
    out = bytearray(cw * chh * 4)
    for y in range(chh):
        s = ((miny + y) * w + minx) * 4
        out[y * cw * 4:(y + 1) * cw * 4] = px[s:s + cw * 4]
    return cw, chh, out


def slice_frames(w, h, ch, px, gap=24):
    """按内容列把横排的四帧切开。"""
    cols = []
    for x in range(w):
        hit = False
        for y in range(0, h, 2):
            if px[(y * w + x) * 4 + 3] > 24:
                hit = True; break
        cols.append(hit)
    runs, s = [], None
    for i, b in enumerate(cols):
        if b and s is None: s = i
        elif not b and s is not None:
            runs.append((s, i - 1)); s = None
    if s is not None: runs.append((s, w - 1))
    # 合并间隔太小的碎片（手臂、法杖之类）
    merged = []
    for a, b in runs:
        if merged and a - merged[-1][1] <= gap:
            merged[-1] = (merged[-1][0], b)
        else:
            merged.append((a, b))
    return merged


def assemble(w, h, px, boxes, ch_out=4):
    """把每帧统一到同一格：水平居中、底部对齐。"""
    fw = max(b[1] - b[0] + 1 for b in boxes)
    fh = h
    n = len(boxes)
    out = bytearray(fw * n * fh * 4)
    for idx, (x0, x1) in enumerate(boxes):
        bw = x1 - x0 + 1
        ox = (fw - bw) // 2
        for y in range(fh):
            for x in range(bw):
                s = (y * w + (x0 + x)) * 4
                if px[s + 3] == 0: continue
                d = ((idx * fh + y) * fw + ox + x) * 4
                out[d:d + 4] = px[s:s + 4]
    return fw, fh * n, out


def main():
    os.makedirs(OUT, exist_ok=True)

    # 1) idle：已经是透明 RGBA，直接抄
    src = os.path.join(PROVIDED, "match_final", "anim_idle", "idle_sheet_8_x6.png")
    w, h, ch, px = pix.load(src)
    pix.save(os.path.join(OUT, "idle_sheet.png"), w, h, ch, px)
    print(f"idle_sheet  {w}x{h}  8 帧  (原样)")

    # 2) cast：白底泛洪
    src = os.path.join(PROVIDED, "cast", "anim_cast", "cast_sheet_32_x3.png")
    w, h, ch, px = pix.load(src)
    rgba = key_white_flood(w, h, ch, px)
    pix.save(os.path.join(OUT, "cast_sheet.png"), w, h, 4, rgba)
    n0 = sum(1 for i in range(w * h) if rgba[i * 4 + 3] == 0)
    print(f"cast_sheet  {w}x{h}  32 帧 8x4  透明 {n0 / (w * h) * 100:.0f}%")

    # 3) 单张物件：品红抠图 + 紧凑裁切
    items = {
        "stem": "seedream-20261107-232605-01.png",
        "water_wave": "seedream-20261107-232855-01.png",
        "magic_circle": "seedream-20261107-232838-01.png",
    }
    for name, fn in items.items():
        p = os.path.join(SRC, fn)
        w, h, ch, px = pix.load(p)
        rgba = key_magenta(w, h, ch, px)
        cw, chh, cut = tight(w, h, 4, rgba)
        pix.save(os.path.join(OUT, f"{name}.png"), cw, chh, 4, cut)
        print(f"{name:12s} {w}x{h} -> {cw}x{chh}")

    # 4) 四帧横排：俯冲 / 喘气
    for name, fn in (("dive_sheet", "seedream-20261107-232925-01.png"),
                     ("pant_sheet", "seedream-20261107-233010-01.png")):
        p = os.path.join(SRC, fn)
        w, h, ch, px = pix.load(p)
        rgba = key_magenta(w, h, ch, px)
        boxes = slice_frames(w, h, 4, rgba)
        print(f"{name}: 切出 {len(boxes)} 帧 {boxes}")
        if len(boxes) != 4:
            print("  !! 帧数不是 4，按实际数量导出")
        fw, fh, sheet = assemble(w, h, rgba, boxes)
        n = fh // h
        pix.save(os.path.join(OUT, f"{name}.png"), fw, fh, 4, sheet)
        print(f"  -> {fw}x{fh}  每帧 {fw}x{h}")


if __name__ == "__main__":
    main()
