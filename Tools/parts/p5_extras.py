

# ======================================================================= extras: backdrops, goal, bouquet, spikes, UI
import colorsys


def hue_shift(cv, hue_deg, sat_mul=1.0, val_mul=1.0, min_sat=0.18):
    """returns a recoloured copy: only saturated pixels change (outlines / greys are kept)."""
    n = cv.copy()
    for y in range(n.h):
        for x in range(n.w):
            c = n.p[y][x]
            if c is None:
                continue
            h, s, v = colorsys.rgb_to_hsv(c[0] / 255.0, c[1] / 255.0, c[2] / 255.0)
            if s < min_sat:
                continue
            r, g, b = colorsys.hsv_to_rgb(hue_deg / 360.0, max(0.0, min(1.0, s * sat_mul)), max(0.0, min(1.0, v * val_mul)))
            n.p[y][x] = (int(r * 255), int(g * 255), int(b * 255)) + ((c[3],) if len(c) > 3 else ())
    return n


def bg_hill():
    c = Canvas(96, 44)
    pal = (C('9be36a'), C('6fcf55'), C('4fb04a'))
    c.shaded(30, 44, 34, 40, pal, light=(-0.5, -0.9), th=(-0.45, 0.35))
    c.shaded(66, 44, 38, 30, (C('8ad866'), C('62c352'), C('46a446')), light=(-0.5, -0.9), th=(-0.45, 0.35))
    for (x, y) in ((22, 14), (24, 14), (23, 15), (58, 22), (60, 22), (59, 23)):
        c.set(x, y, C('c9f59a'))
    c.outline(C('2f7a3f'))
    return c


def bg_bush():
    c = Canvas(48, 18)
    pal = (C('7ee05a'), C('55c24a'), C('3b9c40'))
    c.shaded(11, 12, 10, 8, pal)
    c.shaded(24, 10, 12, 10, pal)
    c.shaded(37, 12, 10, 8, pal)
    for (x, y) in ((9, 6), (22, 3), (35, 6), (24, 4)):
        c.set(x, y, C('c9f59a'))
    c.outline(C('2f7a3f'))
    return c


def bg_cloud(tint='white'):
    c = Canvas(56, 24)
    if tint == 'white':
        pal = (C('ffffff'), C('f1f6ff'), C('d3def5'))
    else:
        pal = (C('fff4fa'), C('ffe0f0'), C('f2bfe0'))
    c.shaded(14, 16, 11, 7, pal, light=(-0.3, -0.9), th=(-0.6, 0.3))
    c.shaded(28, 12, 14, 10, pal, light=(-0.3, -0.9), th=(-0.6, 0.3))
    c.shaded(43, 16, 11, 7, pal, light=(-0.3, -0.9), th=(-0.6, 0.3))
    c.rect(14, 18, 43, 22, pal[1])
    c.rect(14, 21, 43, 22, pal[2])
    c.outline(C('b9c8ea') if tint == 'white' else C('e6a8d6'))
    return c


def bg_rainbow():
    w, h = 128, 66
    c = Canvas(w, h)
    cols = [C('ff5a6a'), C('ff9a4a'), C('ffe05a'), C('6bdc7a'), C('5ab4ff'), C('a98cff')]
    cx, cy = 63.5, 65.5
    for y in range(h):
        for x in range(w):
            r = math.hypot(x - cx, y - cy)
            if 42 <= r < 42 + 4 * len(cols) and y <= 65:
                i = int((r - 42) // 4)
                col = cols[i]
                c.p[y][x] = col + (190,)
    return c


def flag_pole():
    c = Canvas(6, 82)
    c.rect(2, 7, 3, 81, C('dfe6f5'))
    c.rect(3, 7, 3, 81, C('aab4d0'))
    c.rect(1, 0, 4, 5, C('ffd23c'))
    c.rect(2, 1, 2, 2, C('fff3a8'))
    c.outline(OUT)
    return c


def flag_cloth():
    c = Canvas(18, 12)
    c.rect(0, 0, 15, 9, C('ff5a7a'))
    c.poly([(15, 0), (17, 4.5), (15, 9.5)], C('ff5a7a'))
    c.rect(0, 0, 15, 1, C('ff93ab'))
    c.rect(0, 8, 15, 9, C('d93a60'))
    for (x, y) in ((5, 3), (6, 3), (8, 3), (9, 3), (4, 4), (5, 4), (6, 4), (7, 4), (8, 4), (9, 4), (10, 4),
                   (5, 5), (6, 5), (7, 5), (8, 5), (9, 5), (6, 6), (7, 6), (8, 6), (7, 7)):
        c.set(x, y, WHITE)
    c.outline(OUT)
    return c


def castle(pink=False):
    c = Canvas(64, 64)
    stone = (C('dfe4f2'), C('b9c1da'), C('8f99bb'))
    # main body
    c.rect(14, 28, 49, 63, stone[1])
    c.rect(14, 28, 17, 63, stone[0]); c.rect(46, 28, 49, 63, stone[2])
    for x in range(14, 50, 6):
        c.rect(x, 24, x + 3, 27, stone[1])               # battlements
    # towers
    for (x0, x1) in ((4, 19), (44, 59)):
        c.rect(x0, 18, x1, 63, stone[1])
        c.rect(x0, 18, x0 + 3, 63, stone[0]); c.rect(x1 - 3, 18, x1, 63, stone[2])
        c.rect(x0 - 1, 15, x1 + 1, 18, stone[0])
        for x in range(x0 - 1, x1 + 1, 5):
            c.rect(x, 11, x + 2, 14, stone[1])
    # roofs (cones)
    roof = (C('ff5a7a'), C('d93a60')) if pink else (C('e8503c'), C('b73528'))
    c.poly([(2, 15), (11.5, 0), (21, 15)], roof[0])
    c.poly([(42, 15), (51.5, 0), (61, 15)], roof[0])
    c.poly([(11.5, 3), (21, 15), (11.5, 15)], roof[1])
    c.poly([(51.5, 3), (61, 15), (51.5, 15)], roof[1])
    # mortar lines
    for y in range(32, 63, 6):
        for x in range(18, 46):
            if c.p[y][x] == stone[1]:
                c.p[y][x] = stone[2]
    # gate
    c.rect(26, 44, 37, 63, C('3a2f55'))
    c.ellipse(32, 44, 6, 6, C('3a2f55'))
    c.rect(26, 52, 37, 53, C('5a4a7a'))
    # windows
    for (x, y) in ((10, 28), (52, 28), (31, 32)):
        c.rect(x, y, x + 2, y + 5, C('3a2f55'))
        c.set(x + 1, y + 1, C('ffd23c'))
    # heart over the gate
    for (x, y) in ((29, 36), (30, 36), (33, 36), (34, 36), (28, 37), (29, 37), (30, 37), (31, 37), (32, 37), (33, 37), (34, 37), (35, 37),
                   (29, 38), (30, 38), (31, 38), (32, 38), (33, 38), (34, 38), (30, 39), (31, 39), (32, 39), (31, 40)):
        pass
    c.outline(OUT)
    return c


def bouquet():
    c = Canvas(16, 22)
    # stems
    for (x0, y0, x1, y1) in ((8, 9, 5, 15), (8, 9, 8, 15), (8, 9, 11, 15)):
        c.line(x0, y0, x1, y1, C('3fa34d'))
    # flower heads
    heads = ((4, 5, C('ff6b8e'), C('ffc2d1')), (8, 3, C('ffd23c'), C('fff3a8')), (12, 5, C('c586ff'), C('e8cdff')))
    for (cx, cy, col, hi) in heads:
        c.ellipse(cx, cy, 3.2, 3.2, col)
        c.set(cx - 1, cy - 1, hi); c.set(cx, cy - 1, hi)
        c.set(cx, cy, C('fff3a8') if col != C('ffd23c') else C('f08a2a'))
    # wrapping
    c.poly([(2, 12), (14, 12), (9.5, 21.5), (6.5, 21.5)], C('fff4fa'))
    c.poly([(2, 12), (8, 12), (6.5, 21.5)], C('ffd9ea'))
    c.rect(6, 15, 9, 16, C('ff5a7a'))
    c.outline(OUT)
    return c


def spike():
    c = Canvas(16, 8)
    for (x0, x1) in ((0, 7), (8, 15)):
        mid = (x0 + x1) / 2.0
        c.poly([(x0, 8), (mid + 0.5, 0), (x1 + 1, 8)], C('d5dcf0'))
        c.poly([(mid + 0.5, 0), (x1 + 1, 8), (mid + 0.5, 8)], C('9aa5c8'))
    c.outline(OUT)
    return c


def ui_panel():
    c = Canvas(16, 16)
    c.rect(0, 0, 15, 15, OUT)
    c.rect(1, 1, 14, 14, (255, 255, 255))
    c.rect(1, 1, 14, 2, (255, 255, 255))
    c.rect(2, 2, 13, 13, (236, 238, 250))
    c.rect(13, 2, 13, 13, (208, 212, 235))
    c.rect(2, 13, 13, 13, (208, 212, 235))
    c.rect(2, 2, 13, 2, (250, 251, 255))
    return c


def ui_button():
    c = Canvas(16, 16)
    c.rect(0, 0, 15, 15, OUT)
    c.rect(1, 1, 14, 14, (255, 255, 255))
    c.rect(2, 2, 13, 13, (240, 240, 250))
    c.rect(1, 1, 14, 2, (255, 255, 255))
    c.rect(1, 1, 2, 14, (255, 255, 255))
    c.rect(1, 13, 14, 14, (176, 176, 200))
    c.rect(13, 2, 14, 14, (176, 176, 200))
    return c


def build_extras():
    reg('bg_hill', bg_hill(), 'background hill, bottom aligned')
    reg('bg_bush', bg_bush())
    reg('bg_cloud', bg_cloud('white'))
    reg('bg_cloud_pink', bg_cloud('pink'))
    reg('bg_rainbow', bg_rainbow(), 'semi-transparent rainbow arc')
    reg('flag_pole', flag_pole(), 'bottom aligned')
    reg('flag_cloth', flag_cloth())
    reg('castle_stone', castle(False), 'boy world castle')
    reg('castle_pink', castle(True), 'girl world castle')
    reg('bouquet', bouquet())
    reg('spike', spike())
    reg('ui_panel', ui_panel(), '9-slice, border 5, tint with colour')
    reg('ui_button', ui_button(), '9-slice, border 5, tint with colour')
    # girl-world recolours of the interactive blocks
    reg('tile_question_pink', hue_shift(SPRITES['tile_question'], 330, 0.8, 1.05))
    reg('tile_brick_sky', hue_shift(SPRITES['tile_brick'], 268, 0.55, 1.15))
    reg('tile_block_used_sky', hue_shift(SPRITES['tile_block_used'], 268, 0.5, 1.1))


BUILDERS.append(build_extras)
