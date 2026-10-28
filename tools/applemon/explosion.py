"""
死亡爆炸的渲染器：比 render() 更像「真炸开」。

三个关键点（纯放射切块太像代码生成的，就是因为缺这些）：
  1. 不规则碎块 —— 每块的边界不是直射线，而是沿半径起伏的锯齿，
     碎块大小也故意不均（有的宽有的窄）
  2. 碎块自转 —— 飞出去的同时按各自方向轻微旋转
  3. 爆点闪光 —— 炸开瞬间中心有一团白光，2~3 帧内衰减
"""

import math

# 碎块的分界角度（度），故意不等分：有的 50°、有的 18°
BOUNDARIES = [-180, -146, -121, -103, -74, -58, -31, -6, 17, 44, 61, 88,
              112, 141, 158, 180]


def wedge_index(deg, boundaries=BOUNDARIES):
    """角度（度）落在第几块。"""
    for i in range(len(boundaries) - 1):
        if boundaries[i] <= deg < boundaries[i + 1]:
            return i
    return len(boundaries) - 2


def wedge_mid(i, boundaries=BOUNDARIES):
    a = math.radians((boundaries[i] + boundaries[i + 1]) * 0.5)
    return math.cos(a), math.sin(a)


def torn_angle(dx, dy, seed_a=1.0, seed_b=2.4):
    """
    把「点到中心的真实角度」揉一下，让碎块边界变成锯齿状。
    抖动幅度随半径变化，所以切线是弯的、不是直线。
    """
    r = math.hypot(dx, dy)
    jitter = (0.11 * math.sin(r * 0.085 + seed_a)
              + 0.055 * math.sin(r * 0.23 + seed_b))
    return math.degrees(math.atan2(dy, dx)) + math.degrees(jitter)


def render_explosion(sample, box, cell_size, content_h, foot,
                     dist, shrink, rot_deg, alpha, flash, blur,
                     src_cx, src_cy, kx, ky, cx_out, foot_out):
    """
    反向映射渲染一帧爆炸。

    sample / box 等由调用方传入，避免重复实现采样。
    """
    cols = cell_size
    cell = bytearray(cols * cols * 4)
    nb = len(BOUNDARIES) - 1

    # 每块自己的飞散方向 + 旋转中心（输出坐标）
    dirs = [wedge_mid(i) for i in range(nb)]
    # 每块的飞行距离系数：0.72~1.25 不等，避免碎块排成一个规整的圆环
    dist_k = []
    for i in range(nb):
        h = math.sin((i + 1) * 12.9898) * 43758.5453
        dist_k.append(0.60 + 0.95 * (h - math.floor(h)))
    pivots = []
    for i in range(nb):
        dxw, dyw = dirs[i]
        r_src = max(box[2] - box[0], box[3] - box[1]) * 0.5
        pxs = src_cx + dxw * r_src * 0.5
        pys = src_cy + dyw * r_src * 0.5
        pivots.append((cx_out + (pxs - src_cx) * kx,
                       foot_out + (pys - box[3]) * ky))

    cos_r = math.cos(math.radians(rot_deg))
    sin_r = math.sin(math.radians(rot_deg))

    for oy in range(cols):
        for ox in range(cols):
            px, py = float(ox), float(oy)

            # 用未位移的位置判断属于哪块碎片
            sx_base = (px - cx_out) / kx + src_cx
            sy_base = (py - foot_out) / ky + box[3]
            w = wedge_index(torn_angle(sx_base - src_cx, sy_base - src_cy))
            dxw, dyw = dirs[w]

            # 平移 → 围绕碎块自己的中心反向缩放 → 反向自转
            d_i = dist * dist_k[w]
            shx = dxw * d_i
            shy = dyw * d_i - d_i * 0.10        # 整体略微上飘
            ccx, ccy = pivots[w]

            vx = (px - shx - ccx) / shrink
            vy = (py - shy - ccy) / shrink
            rx = vx * cos_r - vy * sin_r
            ry = vx * sin_r + vy * cos_r

            px = ccx + rx
            py = ccy + ry

            # 沿飞行方向做多重采样 = 拖影，碎片飞得快才有「呼啸而过」的感觉
            taps = 1 if blur < 0.5 else 4
            ar = ag = ab = aa_sum = 0
            for t in range(taps):
                off = 0.0 if taps == 1 else (t / (taps - 1)) * blur
                bx = px - dxw * off
                by = py - dyw * off
                ssx = (bx - cx_out) / kx + src_cx
                ssy = (by - foot_out) / ky + box[3]
                sr, sg, sb, sa = sample(ssx, ssy)
                ar += sr * sa
                ag += sg * sa
                ab += sb * sa
                aa_sum += sa
            if aa_sum <= 0:
                continue
            r = int(ar / aa_sum)
            g = int(ag / aa_sum)
            b = int(ab / aa_sum)
            a = int(aa_sum / taps)
            if a <= 0:
                continue
            sx, sy = (px - cx_out) / kx + src_cx, (py - foot_out) / ky + box[3]

            # 爆点闪光：靠近中心加白热，越远越弱
            if flash > 0.001:
                d = math.hypot(sx - src_cx, sy - src_cy)
                # 半径收到 0.34，指数 3 → 只有爆点中心一小团白热，
                # 不然整只苹果都会被糊成粉白色，看着像发光而不是爆炸
                fall = max(0.0, 1.0 - d / (max(box[2] - box[0], box[3] - box[1]) * 0.34))
                fg = flash * fall ** 3
                if fg > 0.001:
                    r = int(r + (255 - r) * fg)
                    g = int(g + (238 - g) * fg)
                    b = int(b + (190 - b) * fg)

            aa = int(a * alpha)
            if aa <= 0:
                continue
            i = (oy * cols + ox) * 4
            cell[i], cell[i + 1], cell[i + 2], cell[i + 3] = r, g, b, aa

    return cell
