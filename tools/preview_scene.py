"""把生成好的素材按真实视口（650x360）叠起来，检查构图。"""
import os
import struct
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'assets')


def over(dst, src):
    sr, sg, sb, sa = src
    if sa == 0:
        return dst
    if sa == 255:
        return src
    dr, dg, db, da = dst
    a = sa / 255.0
    ad = da / 255.0
    oa = a + ad * (1 - a)
    if oa <= 0:
        return (0, 0, 0, 0)
    return (int((sr * a + dr * ad * (1 - a)) / oa),
            int((sg * a + dg * ad * (1 - a)) / oa),
            int((sb * a + db * ad * (1 - a)) / oa),
            int(oa * 255))


def load(name):
    d = open(os.path.join(OUT, name), 'rb').read()
    pos = 8
    w = h = 0
    idat = b''
    while pos < len(d):
        ln = struct.unpack('>I', d[pos:pos + 4])[0]
        tag = d[pos + 4:pos + 8]
        data = d[pos + 8:pos + 8 + ln]
        if tag == b'IHDR':
            w, h = struct.unpack('>II', data[:8])
        elif tag == b'IDAT':
            idat += data
        pos += 12 + ln
    raw = zlib.decompress(idat)
    stride = w * 4
    rows = []
    for y in range(h):
        off = y * (stride + 1)
        rows.append([tuple(raw[off + 1 + i * 4: off + 5 + i * 4]) for i in range(w)])
    return rows


def main():
    VW, VH, OX, OY = 650, 360, 0, 288          # 相机贴底时看到的世界区域
    canvas = [[(0, 0, 0, 255) for _ in range(VW)] for _ in range(VH)]

    def blit(img, x, y):
        for yy in range(len(img)):
            cy = y + yy - OY
            if not (0 <= cy < VH):
                continue
            row = img[yy]
            for xx in range(len(row)):
                cx = x + xx - OX
                if not (0 <= cx < VW):
                    continue
                c = row[xx]
                if c[3] == 0:
                    continue
                canvas[cy][cx] = over(canvas[cy][cx], c)

    def blit_c(img, cx, cy):
        blit(img, int(cx - len(img[0]) / 2), int(cy - len(img) / 2))

    blit(load('sky_gradient.png'), 0, 0)
    blit_c(load('sun.png'), 520, 320)                       # 与场景 Sun 一致
    blit(load('mountains_far.png'), 0, 295)                 # (1600,415) 3200x240
    blit(load('mountains_near.png'), 0, 338)                # (1600,438) 3200x200

    blit_c(load('cloud_c.png'), 180, 300)
    blit_c(load('cloud_a.png'), 780, 318)
    blit_c(load('cloud_b.png'), 1420, 292)
    blit_c(load('cloud_a.png'), 300, 350)
    blit_c(load('cloud_b.png'), 2600, 336)

    gt = load('ground_tile.png')
    for x in range(0, VW, len(gt[0])):
        blit(gt, x, 530)

    raw = bytearray()
    for row in canvas:
        raw.append(0)
        for px in row:
            raw.extend(px)

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data
                + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', VW, VH, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
           + chunk(b'IEND', b''))
    with open(os.path.join(ROOT, 'tools', 'preview_scene.png'), 'wb') as fp:
        fp.write(png)
    print('preview_scene.png %dx%d' % (VW, VH))


if __name__ == '__main__':
    main()
