# =============================================================== TILES / PIPES / ITEMS
def tile_dirt_canvas():
    c = Canvas(16, 16, wrap=True)
    base, dark, light = C('b97545'), C('8f5733'), C('d89a62')
    for y in range(16):
        for x in range(16):
            h = (x * 7 + y * 13 + (x * y) % 5) % 17
            c.set(x, y, dark if h == 0 else (light if h == 5 else base))
    for (x, y) in ((3, 4), (4, 4), (11, 9), (12, 9), (7, 13), (8, 13), (13, 2), (1, 10)):
        c.set(x, y, dark)
    for (x, y) in ((3, 3), (11, 8), (7, 12), (12, 2)):
        c.set(x, y, light)
    return c


def tile_grass_canvas():
    c = tile_dirt_canvas()
    for x in range(16):
        depth = 4 + (1 if x % 4 in (1, 2) else 0) + (1 if x % 8 == 2 else 0)
        for y in range(depth):
            if y == 0:
                col = C('a6ee5e') if x % 3 else C('c8fb86')
            elif y == 1:
                col = C('72d048')
            elif y < depth - 1:
                col = C('52b040')
            else:
                col = C('36883a')
            c.set(x, y, col)
    return c


def contour_cloud(w, h, top_centers, r, cy, fill_to):
    """cloud silhouette built from circles + body; returns (canvas, contour_y per x)"""
    c = Canvas(w, h, wrap=True)
    cont = {}
    for x in range(w):
        best = None
        for cx in top_centers:
            for off in (-w, 0, w):
                dx = x + 0.5 - (cx + off)
                if abs(dx) <= r:
                    y = cy - math.sqrt(r * r - dx * dx)
                    best = y if best is None or y < best else best
        cont[x] = int(math.floor(best)) if best is not None else cy
    return c, cont


def tile_cloud_top():
    c, cont = contour_cloud(16, 16, (4, 12), 5.0, 8.0, 16)
    for x in range(16):
        for y in range(cont[x], 16):
            d = y - cont[x]
            col = C('ffffff') if d < 2 else (C('f0f4ff') if y < 11 else C('e0e8fa'))
            c.set(x, y, col)
        c.set(x, cont[x], C('d3def6'))
    return c


def tile_cloud_body():
    c = Canvas(16, 16, wrap=True)
    for y in range(16):
        for x in range(16):
            c.set(x, y, C('e4ebfa'))
    for (cx, cy, r) in ((4, 5, 3), (12, 11, 3), (11, 3, 2)):
        c.ellipse(cx, cy, r, r - 0.5, C('f3f6ff'))
    for (x, y) in ((2, 11), (3, 11), (8, 8), (9, 8), (14, 14), (13, 14), (6, 1), (7, 1)):
        c.set(x, y, C('d3ddf6'))
    return c


def tile_plat_earth():
    c = Canvas(16, 16, wrap=True)
    for x in range(16):
        c.set(x, 0, C('a6ee5e') if x % 3 else C('c8fb86'))
        c.set(x, 1, C('6fcc48'))
        c.set(x, 2, C('4aa83e'))
        c.set(x, 3, C('6a4128'))
        for y in range(4, 10):
            c.set(x, y, C('d79a58') if y < 5 else C('c4854a'))
        c.set(x, 10, C('7a4a2a'))
        for y in range(11, 15):
            c.set(x, y, C('c4854a') if y < 13 else C('b0723e'))
        c.set(x, 15, C('5c3a22'))
    for (x, y) in ((3, 6), (4, 6), (10, 7), (11, 7), (6, 12), (7, 12), (13, 13), (14, 13)):
        c.set(x, y, C('a86e3a'))
    for (x, y) in ((2, 5), (13, 5), (2, 12), (13, 12)):
        c.set(x, y, C('4c3020'))
    return c


def tile_plat_sky():
    c = Canvas(16, 16, wrap=True)
    for x in range(16):
        top = None
        for cx in (4, 12):
            dx = x + 0.5 - cx
            if abs(dx) <= 4.6:
                y = 7.5 - math.sqrt(4.6 ** 2 - dx * dx)
                top = y if top is None or y < top else top
        bot = None
        for cx in (0, 8):
            for off in (-16, 0, 16):
                dx = x + 0.5 - (cx + off)
                if abs(dx) <= 4.0:
                    y = 8.5 + math.sqrt(4.0 ** 2 - dx * dx)
                    bot = y if bot is None or y > bot else bot
        t, b = int(math.floor(top)), int(math.floor(bot))
        for y in range(t, b + 1):
            if y == t:
                col = C('c9bdf0')
            elif y == b:
                col = C('c9bdf0')
            elif y < t + 3:
                col = C('ffffff')
            elif y > b - 3:
                col = C('e0d6fa')
            else:
                col = C('fff0fa')
            c.set(x, y, col)
    return c


def tile_brick():
    c = Canvas(16, 16)
    mortar, base, light, dark = C('7e3a30'), C('dc7440'), C('f4a864'), C('b4522c')
    for y in range(16):
        row = y // 4
        off = 0 if row % 2 == 0 else 4
        for x in range(16):
            if y % 4 == 3:
                col = mortar
            elif (x + off) % 8 == 7:
                col = mortar
            else:
                col = light if y % 4 == 0 else (base if y % 4 == 1 else dark)
            c.set(x, y, col)
    for i in range(16):
        c.set(i, 0, C('4b2326')); c.set(i, 15, C('4b2326')); c.set(0, i, C('4b2326')); c.set(15, i, C('4b2326'))
    return c


GLYPH_Q = (".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#..")


def tile_question():
    c = Canvas(16, 16)
    base, light, dark, edge = C('ffc83c'), C('ffe98a'), C('d98c1c'), C('6e3a14')
    c.rect(0, 0, 15, 15, base)
    c.rect(1, 1, 14, 1, light); c.rect(1, 1, 1, 14, light)
    c.rect(1, 14, 14, 14, dark); c.rect(14, 1, 14, 14, dark)
    for i in range(16):
        for (x, y) in ((i, 0), (i, 15), (0, i), (15, i)):
            c.set(x, y, edge)
    for (x, y) in ((2, 2), (13, 2), (2, 13), (13, 13)):
        c.set(x, y, C('a85a14')); 
    for gy, row in enumerate(GLYPH_Q):
        for gx, ch in enumerate(row):
            if ch == '#':
                c.set(5 + gx + 1, 4 + gy + 1, C('fff2b0'))
    for gy, row in enumerate(GLYPH_Q):
        for gx, ch in enumerate(row):
            if ch == '#':
                c.set(5 + gx, 4 + gy, C('7a3c10'))
    return c


def tile_block_used():
    c = Canvas(16, 16)
    base, light, dark, edge = C('a8765a'), C('c79a7c'), C('84583f'), C('4a2e22')
    c.rect(0, 0, 15, 15, base)
    c.rect(1, 1, 14, 1, light); c.rect(1, 1, 1, 14, light)
    c.rect(1, 14, 14, 14, dark); c.rect(14, 1, 14, 14, dark)
    for i in range(16):
        for (x, y) in ((i, 0), (i, 15), (0, i), (15, i)):
            c.set(x, y, edge)
    for (x, y) in ((2, 2), (13, 2), (2, 13), (13, 13)):
        c.set(x, y, dark)
    c.rect(5, 5, 10, 10, C('946648')); c.rect(5, 5, 10, 5, dark); c.rect(5, 5, 5, 10, dark)
    return c


PIPE_PAL = {
    'green': dict(out=C('1f4a36'), cols=[C('2f9a5a'), C('52c874'), C('9cf19a'), C('6fdc88'), C('52c874'), C('3fae66'), C('2b8c52'), C('21703f')]),
    'pink':  dict(out=C('6a2a52'), cols=[C('d85a96'), C('f784b8'), C('ffd0e6'), C('ffa6cc'), C('f784b8'), C('e66aa6'), C('c4467f'), C('9c3266')]),
}


def pipe_col(w, x, pal):
    u = (x - 1) / float(w - 2)
    stops = (0.08, 0.2, 0.32, 0.44, 0.62, 0.78, 0.9, 1.01)
    for i, s in enumerate(stops):
        if u < s:
            return pal['cols'][i]
    return pal['cols'][-1]


def pipe_cap(name):
    pal = PIPE_PAL[name]
    c = Canvas(32, 16)
    for x in range(32):
        for y in range(16):
            col = pipe_col(32, x, pal)
            if y == 1:
                col = shade(col, 1.15)
            if y == 14:
                col = shade(col, 0.8)
            c.set(x, y, col)
    for i in range(32):
        c.set(i, 0, pal['out']); c.set(i, 15, pal['out'])
    for i in range(16):
        c.set(0, i, pal['out']); c.set(31, i, pal['out'])
    return c


def pipe_body(name):
    pal = PIPE_PAL[name]
    c = Canvas(28, 16)
    for x in range(28):
        for y in range(16):
            c.set(x, y, pipe_col(28, x, pal))
    for i in range(16):
        c.set(0, i, pal['out']); c.set(27, i, pal['out'])
    return c


def coin(frame):
    c = Canvas(16, 16)
    rx = (5.6, 3.6, 1.2, 3.6)[frame]
    gold = (C('fff0a0'), C('ffd23c'), C('e69a1e'))
    if frame == 3:
        gold = (C('ffd23c'), C('f0b02a'), C('e69a1e'))
    c.shaded(8, 8, rx, 6.4, gold, light=(-0.55, -0.85) if frame != 3 else (0.55, -0.85), th=(-0.5, 0.55))
    if rx > 3:
        c.ellipse(8, 8, rx - 1.7, 4.7, C('f0b02a'))
        c.ellipse(8, 8, rx - 2.6, 3.8, C('ffd23c'))
        c.rect(8 if frame != 3 else 7, 5, 8 if frame != 3 else 8, 10, C('ffec8a'))
        c.set(4 if frame == 0 else 5, 4, C('fffbe0'))
    c.outline(C('8a4a12'))
    return c


def flower(frame):
    c = Canvas(16, 16)
    sway = 0 if frame == 1 else 1
    # stem + leaf
    for y in range(9, 15):
        c.set(8 + (sway if y < 11 else 0), y, C('3fae56'))
        c.set(7 + (sway if y < 11 else 0), y, C('6fd96e')) if False else None
    c.poly([(8.5, 13.5), (12.4, 11.0), (12.6, 13.2), (9.0, 14.4)], C('4fc15a'))
    c.poly([(8.0, 13.0), (4.2, 11.2), (4.0, 13.4), (7.2, 14.2)], C('4fc15a'))
    cx = 8 + sway * 0.0
    # petals
    for k in range(5):
        a = math.radians(90 + 72 * k)
        px = cx + 3.4 * math.cos(a)
        py = 6.0 - 3.4 * math.sin(a)
        c.shaded(px, py, 2.6, 2.6, (C('ffd2e4'), C('ff8fba'), C('e8609a')), th=(-0.4, 0.5))
    c.shaded(cx, 6.0, 2.4, 2.4, (C('fff3a0'), C('ffd84a'), C('e8a826')))
    if frame == 1:
        c.set(7, 6, C('2b2540')); c.set(9, 6, C('2b2540'))
    else:
        c.set(7, 6, C('2b2540')); c.set(9, 6, C('2b2540')); c.set(8, 7, C('d4583f'))
    c.outline()
    return c


def wings_elixir():
    """pink potion bottle with a tiny pair of wings (gives the princess wings)"""
    c = Canvas(16, 16)
    W = (C('ffffff'), C('e8f2ff'), C('b8d0f0'))
    for sgn in (1, -1):
        def X(v):
            return v if sgn == 1 else 16 - v
        c.poly([(X(4.6), 9.0), (X(0.4), 4.6), (X(1.2), 8.2), (X(0.8), 10.6), (X(4.6), 11.5)], W[0])
        c.set(int(X(1.5)) if sgn == 1 else int(X(1.5)) - 1, 6, W[2])
    # bottle
    c.rect(6, 4, 9, 5, C('d9b27a'))           # cork
    c.rect(6, 6, 9, 7, C('dff3ff'))           # neck
    c.shaded(8, 11.2, 4.4, 3.8, (C('ffb6e0'), C('ff6aa8'), C('d8427f')), light=(-0.5, -0.9))
    c.rect(5, 9, 10, 9, C('dff3ff'))
    c.set(6, 10, WHITE); c.set(6, 11, WHITE)
    c.set(7, 12, C('2b2540')); c.set(9, 12, C('2b2540')); c.rect(8, 13, 8, 13, C('d4583f'))
    c.outline()
    return c


def wings_overlay():
    """the princess's wings, drawn behind her (32x18, centred)"""
    c = Canvas(32, 18)
    W = (C('ffffff'), C('eaf4ff'), C('bcd4f4'))
    for sgn in (1, -1):
        def X(v):
            return v if sgn == 1 else 32 - v
        c.poly([(X(14.0), 12.0), (X(9.0), 1.0), (X(2.0), 2.5), (X(0.5), 8.0), (X(3.0), 12.5), (X(9.0), 15.5)], W[1])
        c.poly([(X(14.0), 11.0), (X(9.5), 3.0), (X(4.0), 4.0), (X(2.5), 8.0), (X(5.0), 11.0), (X(10.0), 13.0)], W[0])
        for (x0, y0, x1, y1) in ((13, 11, 8, 4), (12, 12, 5, 8), (11, 13, 4, 12)):
            c.line(X(x0), y0, X(x1), y1, W[2])
    c.outline(C('7d95c4'))
    return c


def mushroom():
    c = Canvas(16, 16)
    # stem
    c.rect(5, 9, 10, 14, C('fff0d8'))
    c.rect(9, 9, 10, 14, C('ecc9a2')); c.rect(5, 14, 10, 14, C('ecc9a2'))
    # cap
    c.shaded(8, 6.2, 7.2, 5.6, (C('ffb6d6'), C('ff6aa8'), C('d8427f')))
    c.rect(0, 10, 15, 15, None) if False else None
    c.rect(1, 9, 14, 9, C('d8427f'))
    for (x, y, r) in ((4.5, 5.2, 1.5), (11.2, 4.4, 1.3), (8.0, 2.8, 1.1), (13.0, 7.4, 0.9)):
        c.ellipse(x, y, r, r, C('fff6fa'))
    # face
    c.rect(6, 11, 6, 12, C('2b2540')); c.rect(9, 11, 9, 12, C('2b2540'))
    c.set(5, 13, C('ffa0b0')); c.set(10, 13, C('ffa0b0')); c.rect(7, 13, 8, 13, C('d4583f'))
    c.outline()
    return c


def fireflower():
    c = Canvas(16, 16)
    c.rect(7, 9, 8, 14, C('3fae56')); c.rect(7, 9, 7, 14, C('6fd96e'))
    c.poly([(8.5, 13.5), (13.2, 10.6), (13.4, 13.4), (9.2, 14.6)], C('4fc15a'))
    c.poly([(7.5, 13.0), (2.8, 10.6), (2.6, 13.4), (6.8, 14.6)], C('4fc15a'))
    for k in range(5):
        a = math.radians(90 + 72 * k)
        px = 8 + 3.6 * math.cos(a)
        py = 5.8 - 3.6 * math.sin(a)
        c.shaded(px, py, 2.8, 2.8, (C('ffd35a'), C('ff8a2a'), C('e2531e')), th=(-0.4, 0.5))
    c.shaded(8, 5.8, 2.6, 2.6, (C('fffbd0'), C('ffe680'), C('ffc040')))
    c.set(7, 5, C('2b2540')); c.set(9, 5, C('2b2540')); c.rect(7, 7, 8, 7, C('d4583f'))
    c.outline()
    return c


def fireball(frame):
    c = Canvas(8, 8)
    c.ellipse(4, 4, 2.9, 2.9, C('ff7a2a'))
    c.ellipse(4, 4, 2.0, 2.0, C('ffbe3c'))
    c.ellipse(4, 4, 1.1, 1.1, C('fff6c8'))
    if frame == 1:
        c.set(1, 1, C('ffb040')); c.set(2, 0, C('ff7a2a')); c.set(0, 2, C('ff7a2a'))
    else:
        c.set(6, 6, C('ffb040')); c.set(7, 5, C('ff7a2a')); c.set(5, 7, C('ff7a2a'))
    c.outline(C('a8421a'))
    return c


def debris():
    c = Canvas(8, 8)
    c.poly([(1.5, 2.5), (5.5, 1.5), (7, 4.5), (5, 6.5), (2, 6)], C('dc7440'))
    c.set(2, 3, C('f4a864')); c.set(3, 3, C('f4a864')); c.set(5, 5, C('b4522c')); c.set(4, 5, C('b4522c'))
    c.inner_outline(C('5a2a28'))
    return c


def ring(idx):
    bands = {
        0: (C('9de08a'), C('4fb35a'), C('2c7a3e')),
        1: (C('f4f7ff'), C('b8c2da'), C('7f8cae')),
        2: (C('fff0a0'), C('f4b730'), C('b97a1c')),
        3: (C('fff0a0'), C('f4b730'), C('b97a1c')),
        4: (C('ffffff'), C('d6deee'), C('98a6c4')),
    }[idx]
    c = Canvas(16, 16)
    c.shaded(8, 10.4, 5.6, 4.8, bands, light=(-0.5, -0.85), th=(-0.45, 0.45))
    c.ellipse(8, 10.8, 3.3, 2.8, None)
    # clear hole
    for y in range(16):
        for x in range(16):
            nx, ny = (x + 0.5 - 8) / 3.3, (y + 0.5 - 10.8) / 2.9
            if nx * nx + ny * ny <= 1.0:
                c.p[y][x] = None
    if idx == 0:
        for (x, y) in ((6.6, 4.3), (9.4, 4.3), (8.0, 2.6)):
            c.shaded(x, y, 1.7, 1.7, (C('b9f5a8'), C('5fd070'), C('2f9a4a')))
        c.set(8, 5, C('2f9a4a')); c.set(8, 6, C('2f9a4a'))
    elif idx == 1:
        c.rect(6, 5, 9, 6, C('dfe5f4')); c.set(6, 5, WHITE)
        c.set(12, 4, C('fff8c0')); c.set(11, 4, WHITE); c.set(13, 4, WHITE); c.set(12, 3, WHITE); c.set(12, 5, WHITE)
    elif idx == 2:
        rows = ((".XX..XX.", 1), ("XXXXXXXX", 2), ("XXXXXXXX", 3), (".XXXXXX.", 4), ("..XXXX..", 5), ("...XX...", 6))
        for ry, (row, yy) in enumerate(rows):
            for gx, ch in enumerate(row):
                if ch == 'X':
                    c.set(4 + gx, yy - 1 + 0, C('ff4a68'))
        c.set(5, 1, C('ffb0c0')); c.set(6, 1, C('ffb0c0')); c.set(5, 2, C('ffb0c0'))
        c.rect(5, 6, 10, 6, C('d8a02a')) if False else None
    elif idx == 3:
        pat = ("..XXXX..", ".XXXXXX.", "XXXXXXXX", ".XXXXXX.", "..XXXX..")
        for ry, row in enumerate(pat):
            for gx, ch in enumerate(row):
                if ch == 'X':
                    c.set(4 + gx, 1 + ry, C('2f7cf0') if (gx + ry) % 3 else C('5ea6ff'))
        c.set(5, 2, C('b8dcff')); c.set(6, 2, C('b8dcff')); c.set(5, 3, C('b8dcff'))
    elif idx == 4:
        pat = (".XXXXXX.", "XXXXXXXX", ".XXXXXX.", "..XXXX..", "...XX...")
        for ry, row in enumerate(pat):
            for gx, ch in enumerate(row):
                if ch == 'X':
                    c.set(4 + gx, 1 + ry, C('e8fbff') if (gx + ry) % 2 == 0 else C('b4ecff'))
        c.set(5, 2, WHITE); c.set(6, 2, WHITE); c.set(7, 1, WHITE); c.rect(6, 3, 9, 3, C('8fd8f4'))
        for (x, y) in ((1, 3), (14, 2), (13, 6), (2, 7)):
            c.set(x, y, C('fff6a8')); c.set(x - 1, y, C('fff6a8')) if x > 0 else None; c.set(x + 1, y, C('fff6a8')) if x < 15 else None
            c.set(x, y - 1, C('fff6a8')); c.set(x, y + 1, C('fff6a8'))
    c.outline()
    return c


def ringbox(opened):
    c = Canvas(12, 10)
    red, redl, redd, gold = C('c2304e'), C('e85a72'), C('8e1f3c'), C('f4b730')
    if not opened:
        c.rect(1, 2, 10, 5, redl); c.rect(1, 3, 10, 5, red)
        c.rect(1, 6, 10, 8, redd); c.rect(1, 6, 10, 6, gold)
        c.rect(5, 4, 6, 6, gold); c.set(5, 4, C('fff0a0'))
    else:
        c.rect(2, 0, 9, 3, redd); c.rect(2, 0, 9, 0, red); c.rect(3, 1, 8, 2, C('b02a48'))
        c.rect(1, 4, 10, 8, red)
        c.rect(1, 4, 10, 4, redl)
        c.rect(2, 4, 9, 4, C('ff8fa6'))
        c.rect(4, 4, 7, 4, C('5a1028'))
        c.rect(1, 6, 10, 6, gold)
        c.rect(1, 7, 10, 8, redd)
    c.outline()
    return c


HEART_ROWS = (".XXX...XXX.", "XXXXX.XXXXX", "XXXXXXXXXXX", "XXXXXXXXXXX", "XXXXXXXXXXX",
              ".XXXXXXXXX.", "..XXXXXXX..", "...XXXXX...", "....XXX....", ".....X.....")


def heart():
    c = Canvas(11, 10)
    for y, row in enumerate(HEART_ROWS):
        for x, ch in enumerate(row):
            if ch == 'X':
                c.set(x, y, C('ff4d6d'))
    c.inner_outline(C('9a1c44'))
    for (x, y) in ((2, 2), (3, 2), (2, 3)):
        c.set(x, y, C('ffb4c4'))
    for (x, y) in ((7, 3), (8, 4), (6, 5), (5, 6)):
        if c.get(x, y) is not None and c.get(x, y) != C('9a1c44'):
            c.set(x, y, C('e0345a'))
    return c


def sparkle(frame):
    c = Canvas(7, 7)
    if frame == 1:
        for i in range(7):
            if i != 3:
                c.set(3, i, C('ffe66a')); c.set(i, 3, C('ffe66a'))
        c.rect(2, 3, 4, 3, WHITE); c.rect(3, 2, 3, 4, WHITE)
        for (x, y) in ((3, 0), (3, 6), (0, 3), (6, 3)):
            c.set(x, y, C('ffc828'))
    else:
        for (x, y) in ((1, 1), (5, 1), (1, 5), (5, 5)):
            c.set(x, y, C('ffe66a'))
        c.rect(2, 3, 4, 3, C('ffe66a')); c.rect(3, 2, 3, 4, C('ffe66a'))
        c.set(3, 3, WHITE)
    return c


def build_items():
    d = tile_dirt_canvas()
    reg('tile_dirt', d, 'tiles seamlessly in x and y')
    reg('tile_grass', tile_grass_canvas(), 'grass top rows over dirt; tiles in x')
    reg('tile_cloud_top', tile_cloud_top(), 'cloud surface, transparent above bumps; tiles in x')
    reg('tile_cloud_body', tile_cloud_body(), 'cloud interior; tiles in x and y')
    reg('tile_plat_earth', tile_plat_earth(), 'one-way ledge, earth world, 1 tile')
    reg('tile_plat_sky', tile_plat_sky(), 'one-way ledge, cloud world, 1 tile')
    reg('tile_brick', tile_brick())
    reg('tile_question', tile_question())
    reg('tile_block_used', tile_block_used())
    for n in ('green', 'pink'):
        reg('pipe_%s_cap' % n, pipe_cap(n), 'pipe top 32x16 (2 units wide)')
        reg('pipe_%s_body' % n, pipe_body(n), 'pipe body 28x16, tile vertically, centred under the cap')
    for i in range(4):
        reg('coin_%d' % (i + 1), coin(i), 'spin frame, centred')
    reg('flower_1', flower(1), 'collectible flower, bottom = stem')
    reg('flower_2', flower(2))
    reg('item_mushroom', mushroom(), 'unused now (kept for reference)')
    reg('item_wings', wings_elixir(), 'power-up: wings elixir, bottom aligned')
    reg('wings_1', wings_overlay(), 'princess wings, drawn behind her')
    reg('item_fireflower', fireflower(), 'power-up, bottom aligned')
    reg('fireball_1', fireball(1)); reg('fireball_2', fireball(2))
    reg('brick_debris', debris())
    for i in range(5):
        reg('ring_%d' % i, ring(i), 'ring icon, centred')
    reg('ringbox_closed', ringbox(False)); reg('ringbox_open', ringbox(True), 'empty cushion; draw ring icon on top in code')
    reg('heart', heart(), 'centred')
    reg('sparkle_1', sparkle(1)); reg('sparkle_2', sparkle(2))
