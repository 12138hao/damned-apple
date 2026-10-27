"""血条 UI 贴图：深色底槽 + 红色前景条（像素风，120x14）。

用法：python tools/gen_ui.py
输出：assets/ui/hp_bar_bg.png、assets/ui/hp_bar_fill.png
"""
import os
import struct
import zlib

W, H = 120, 14


def new_img():
    return [[(0, 0, 0, 0) for _ in range(W)] for _ in range(H)]


def put(img, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        img[y][x] = c


def rect(img, x1, y1, x2, y2, c):
    for y in range(int(y1), int(y2) + 1):
        for x in range(int(x1), int(x2) + 1):
            put(img, x, y, c)


def save(img, path):
    raw = bytearray()
    for y in range(H):
        raw.append(0)
        for x in range(W):
            raw.extend(img[y][x])

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data
                + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as fp:
        fp.write(png)


def make_bg():
    """底槽：外深色描边 + 内深灰，底部再压暗一层。"""
    img = new_img()
    edge = (0x12, 0x12, 0x1A, 255)
    inner = (0x2C, 0x2C, 0x3A, 255)
    inner_lo = (0x22, 0x22, 0x2E, 255)
    rect(img, 0, 0, W - 1, H - 1, edge)
    rect(img, 1, 1, W - 2, H - 2, inner)
    rect(img, 1, H - 3, W - 2, H - 2, inner_lo)
    # 四角削掉 1 像素，像素风圆角
    for cx, cy in ((0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1)):
        put(img, cx, cy, (0, 0, 0, 0))
    return img


def make_fill():
    """前景：纯红色，上高光 + 主体 + 下暗部，不带竖纹。"""
    img = new_img()
    hi = (0xF2, 0x6C, 0x72, 255)
    mid = (0xD6, 0x30, 0x3A, 255)
    lo = (0x9A, 0x1C, 0x24, 255)
    rect(img, 1, 1, W - 2, H - 2, mid)
    rect(img, 1, 1, W - 2, 2, hi)
    rect(img, 1, H - 3, W - 2, H - 2, lo)
    for cx, cy in ((0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1)):
        put(img, cx, cy, (0, 0, 0, 0))
    return img


def make_border():
    """金色描边：只画最外一圈，中间全透明，叠在血条上层。"""
    img = new_img()
    hi = (0xFF, 0xF0, 0xB0, 255)     # 上/左：亮金
    lo = (0xA8, 0x7C, 0x22, 255)     # 下/右：暗金
    rect(img, 0, 0, W - 1, 0, hi)
    rect(img, 0, 0, 0, H - 1, hi)
    rect(img, 0, H - 1, W - 1, H - 1, lo)
    rect(img, W - 1, 0, W - 1, H - 1, lo)
    for cx, cy in ((0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1)):
        put(img, cx, cy, (0, 0, 0, 0))
    return img


# ============================================================ 怪物头顶小血条
EW, EH = 40, 8


def _new_small():
    return [[(0, 0, 0, 0) for _ in range(EW)] for _ in range(EH)]


def _put_small(img, x, y, c):
    if 0 <= x < EW and 0 <= y < EH:
        img[y][x] = c


def _rect_small(img, x1, y1, x2, y2, c):
    for y in range(int(y1), int(y2) + 1):
        for x in range(int(x1), int(x2) + 1):
            _put_small(img, x, y, c)


def _save_small(img, path):
    raw = bytearray()
    for y in range(EH):
        raw.append(0)
        for x in range(EW):
            raw.extend(img[y][x])

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data
                + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', EW, EH, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    with open(path, 'wb') as fp:
        fp.write(png)


def make_enemy_bg():
    img = _new_small()
    _rect_small(img, 0, 0, EW - 1, EH - 1, (0x12, 0x12, 0x1A, 255))
    _rect_small(img, 1, 1, EW - 2, EH - 2, (0x2C, 0x2C, 0x3A, 255))
    _rect_small(img, 1, EH - 2, EW - 2, EH - 2, (0x22, 0x22, 0x2E, 255))
    for cx, cy in ((0, 0), (EW - 1, 0), (0, EH - 1), (EW - 1, EH - 1)):
        _put_small(img, cx, cy, (0, 0, 0, 0))
    return img


def make_enemy_fill():
    img = _new_small()
    _rect_small(img, 1, 1, EW - 2, EH - 2, (0xD6, 0x30, 0x3A, 255))
    _rect_small(img, 1, 1, EW - 2, 1, (0xF2, 0x6C, 0x72, 255))
    _rect_small(img, 1, EH - 3, EW - 2, EH - 2, (0x9A, 0x1C, 0x24, 255))
    for cx, cy in ((0, 0), (EW - 1, 0), (0, EH - 1), (EW - 1, EH - 1)):
        _put_small(img, cx, cy, (0, 0, 0, 0))
    return img


def make_enemy_border():
    img = _new_small()
    hi = (0xFF, 0xF0, 0xB0, 255)
    lo = (0xA8, 0x7C, 0x22, 255)
    _rect_small(img, 0, 0, EW - 1, 0, hi)
    _rect_small(img, 0, 0, 0, EH - 1, hi)
    _rect_small(img, 0, EH - 1, EW - 1, EH - 1, lo)
    _rect_small(img, EW - 1, 0, EW - 1, EH - 1, lo)
    for cx, cy in ((0, 0), (EW - 1, 0), (0, EH - 1), (EW - 1, EH - 1)):
        _put_small(img, cx, cy, (0, 0, 0, 0))
    return img


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(root, 'assets', 'ui')
    save(make_bg(), os.path.join(out, 'hp_bar_bg.png'))
    save(make_fill(), os.path.join(out, 'hp_bar_fill.png'))
    save(make_border(), os.path.join(out, 'hp_bar_border.png'))
    _save_small(make_enemy_bg(), os.path.join(out, 'enemy_bar_bg.png'))
    _save_small(make_enemy_fill(), os.path.join(out, 'enemy_bar_fill.png'))
    _save_small(make_enemy_border(), os.path.join(out, 'enemy_bar_border.png'))
    print('已生成：玩家血条 3 张 (%dx%d) + 怪物血条 3 张 (%dx%d)' % (W, H, EW, EH))


if __name__ == '__main__':
    main()
