"""从「带蛇的树」里把蛇抠出来：用「没蛇的树」做差分，只保留最大连通块。"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import pix

WITH = os.path.join(HERE, 'src', 'tree_with_snake.png')
WITHOUT = '像素蛇怪生成 (6).png'


def resize_bilinear(w, h, ch, px, nw, nh):
    out = bytearray(nw * nh * ch)
    sx = w / nw
    sy = h / nh
    for y in range(nh):
        fy = (y + 0.5) * sy - 0.5
        y0 = max(0, int(fy)); y1 = min(h - 1, y0 + 1); ty = fy - y0
        for x in range(nw):
            fx = (x + 0.5) * sx - 0.5
            x0 = max(0, int(fx)); x1 = min(w - 1, x0 + 1); tx = fx - x0
            for c in range(ch):
                a = px[(y0 * w + x0) * ch + c] * (1 - tx) + px[(y0 * w + x1) * ch + c] * tx
                b = px[(y1 * w + x0) * ch + c] * (1 - tx) + px[(y1 * w + x1) * ch + c] * tx
                out[(y * nw + x) * ch + c] = int(a * (1 - ty) + b * ty)
    return out


def label_components(W, H, mask):
    lab = [0] * (W * H)
    comps = []
    cur = 0
    for y in range(H):
        for x in range(W):
            o = y * W + x
            if not mask[o] or lab[o]:
                continue
            cur += 1
            stack = [(x, y)]
            lab[o] = cur
            px_count = 0
            minx, maxx, miny, maxy = x, x, y, y
            while stack:
                cx, cy = stack.pop()
                px_count += 1
                if cx < minx: minx = cx
                if cx > maxx: maxx = cx
                if cy < miny: miny = cy
                if cy > maxy: maxy = cy
                for nx2, ny2 in ((cx+1,cy),(cx-1,cy),(cx,cy+1),(cx,cy-1)):
                    if 0 <= nx2 < W and 0 <= ny2 < H:
                        no = ny2 * W + nx2
                        if mask[no] and not lab[no]:
                            lab[no] = cur
                            stack.append((nx2, ny2))
            comps.append((px_count, cur, minx, miny, maxx, maxy))
    comps.sort(reverse=True)
    return lab, comps


def main():
    w1, h1, c1, p1 = pix.load(WITH)
    w2, h2, c2, p2 = pix.load(WITHOUT)
    big = resize_bilinear(w1, h1, c1, p1, w2, h2)
    W, H = w2, h2

    mask = bytearray(W * H)
    for y in range(H):
        for x in range(W):
            o = y * W + x
            o2 = o * c2
            if p2[o2 + 3] <= 150:
                continue
            ow = o * c1
            d = abs(big[ow] - p2[o2]) + abs(big[ow + 1] - p2[o2 + 1]) + abs(big[ow + 2] - p2[o2 + 2])
            if d > 45:
                mask[o] = 1

    lab, comps = label_components(W, H, mask)
    print('连通块前 5：', [(c[0], c[2], c[3], c[4], c[5]) for c in comps[:5]])
    keep = comps[0][1]
    big_area = comps[0][0]

    # 只留最大块 + 面积大于它 8% 的次级块（防止蛇身某段被断开）
    keep_ids = {keep}
    for c in comps[1:]:
        if c[0] > big_area * 0.08:
            keep_ids.add(c[1])
    mask = bytearray(1 if lab[i] in keep_ids else 0 for i in range(W * H))
    print('保留块', len(keep_ids))

    xs = [i % W for i in range(W * H) if mask[i]]
    ys = [i // W for i in range(W * H) if mask[i]]
    x0, x1, y0, y1 = min(xs), max(xs) + 1, min(ys), max(ys) + 1
    print('蛇 bbox', x0, y0, x1, y1, '尺寸', x1 - x0, y1 - y0)

    # 填洞
    lo_x, lo_y = max(0, x0 - 2), max(0, y0 - 2)
    hi_x, hi_y = min(W, x1 + 2), min(H, y1 + 2)
    seen = bytearray(W * H)
    stack = []
    for x in range(lo_x, hi_x):
        for yy in (lo_y, hi_y - 1):
            if not mask[yy * W + x]: stack.append((x, yy))
    for y in range(lo_y, hi_y):
        for xx in (lo_x, hi_x - 1):
            if not mask[y * W + xx]: stack.append((xx, y))
    while stack:
        x, y = stack.pop()
        if x < lo_x or y < lo_y or x >= hi_x or y >= hi_y: continue
        o = y * W + x
        if mask[o] or seen[o]: continue
        seen[o] = 1
        stack += [(x+1,y),(x-1,y),(x,y+1),(x,y-1)]
    holes = 0
    for y in range(lo_y, hi_y):
        for x in range(lo_x, hi_x):
            o = y * W + x
            if not mask[o] and not seen[o]:
                mask[o] = 1; holes += 1
    print('填洞', holes)

    out = bytearray(W * H * 4)
    cnt = 0
    for y in range(lo_y, hi_y):
        for x in range(lo_x, hi_x):
            o = y * W + x
            if not mask[o]: continue
            ow = o * c1
            d = o * 4
            out[d], out[d+1], out[d+2], out[d+3] = big[ow], big[ow+1], big[ow+2], 255
            cnt += 1
    dst = os.path.join(HERE, 'snake_baked_full.png')
    pix.save(dst, W, H, 4, out)
    print(f'-> {dst}  不透明 {cnt} 像素')


if __name__ == '__main__':
    main()
