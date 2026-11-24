#!/usr/bin/env python3
"""
Pixel Love - procedural pixel-art generator.
Run:  python3 Tools/generate_art.py
Writes every sprite to docs/sprites/<name>.png, plus Assets/Scripts/PixelArtData.cs, PixelFontData.cs and Assets/Icons (nearest-neighbour, hard pixels).
All game sprites are meant to be drawn at 16 pixels per world unit.
"""
import math, os, random, shutil
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
OUT_DIR = os.path.join(ROOT, 'docs', 'sprites')
LOGO_SRC = os.path.join(ROOT, 'Tools', 'logo_source.png')
os.makedirs(OUT_DIR, exist_ok=True)

SPRITES = {}   # name -> Canvas  (filled by the build functions)
NOTES = {}     # name -> note text for SPRITES.md


def C(h):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))


OUT = C('2b2540')       # shared dark navy outline
WHITE = (255, 255, 255)


class Canvas:
    def __init__(s, w, h, wrap=False):
        s.w, s.h, s.wrap = w, h, wrap
        s.p = [[None] * w for _ in range(h)]

    # ---------------------------------------------------------------- basics
    def get(s, x, y):
        x = int(x); y = int(y)
        if s.wrap:
            x %= s.w
        if 0 <= x < s.w and 0 <= y < s.h:
            return s.p[y][x]
        return None

    def set(s, x, y, c):
        x = int(x); y = int(y)
        if s.wrap:
            x %= s.w
        if 0 <= x < s.w and 0 <= y < s.h:
            s.p[y][x] = c

    def rect(s, x0, y0, x1, y1, c):
        for y in range(int(y0), int(y1) + 1):
            for x in range(int(x0), int(x1) + 1):
                s.set(x, y, c)

    def symrect(s, x0, x1, y0, y1, c):
        """rect x0..x1 plus its horizontal mirror image."""
        s.rect(x0, y0, x1, y1, c)
        s.rect(s.w - 1 - x1, y0, s.w - 1 - x0, y1, c)

    def symset(s, x, y, c):
        s.set(x, y, c)
        s.set(s.w - 1 - x, y, c)

    def ellipse(s, cx, cy, rx, ry, c):
        x0, x1 = int(math.floor(cx - rx)) - 1, int(math.ceil(cx + rx)) + 1
        y0, y1 = int(math.floor(cy - ry)) - 1, int(math.ceil(cy + ry)) + 1
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                nx = (x + 0.5 - cx) / rx
                ny = (y + 0.5 - cy) / ry
                if nx * nx + ny * ny <= 1.0:
                    s.set(x, y, c(x, y) if callable(c) else c)

    def shaded(s, cx, cy, rx, ry, pal, light=(-0.6, -0.8), th=(-0.35, 0.40)):
        """banded cel shading: pal = (light, base, dark)"""
        x0, x1 = int(math.floor(cx - rx)) - 1, int(math.ceil(cx + rx)) + 1
        y0, y1 = int(math.floor(cy - ry)) - 1, int(math.ceil(cy + ry)) + 1
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                nx = (x + 0.5 - cx) / rx
                ny = (y + 0.5 - cy) / ry
                if nx * nx + ny * ny <= 1.0:
                    v = nx * light[0] + ny * light[1]
                    s.set(x, y, pal[0] if v < th[0] else (pal[1] if v < th[1] else pal[2]))

    def line(s, x0, y0, x1, y1, c, th=1):
        dx, dy = x1 - x0, y1 - y0
        n = int(max(abs(dx), abs(dy)) * 2) + 1
        for i in range(n + 1):
            t = i / n
            x = x0 + dx * t
            y = y0 + dy * t
            for oy in range(th):
                for ox in range(th):
                    s.set(int(round(x)) + ox - (th - 1) // 2, int(round(y)) + oy - (th - 1) // 2, c)

    def poly(s, pts, c):
        ys = [p[1] for p in pts]
        xs = [p[0] for p in pts]
        for y in range(int(min(ys)) - 1, int(max(ys)) + 2):
            for x in range(int(min(xs)) - 1, int(max(xs)) + 2):
                px, py = x + 0.5, y + 0.5
                inside = False
                j = len(pts) - 1
                for i in range(len(pts)):
                    xi, yi = pts[i]; xj, yj = pts[j]
                    if (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / (yj - yi + 1e-9) + xi:
                        inside = not inside
                    j = i
                if inside:
                    s.set(x, y, c(x, y) if callable(c) else c)

    def blit(s, o, ox=0, oy=0):
        for y in range(o.h):
            for x in range(o.w):
                if o.p[y][x] is not None:
                    s.set(x + ox, y + oy, o.p[y][x])

    def mirror_left(s):
        for y in range(s.h):
            for x in range(s.w // 2):
                s.p[y][s.w - 1 - x] = s.p[y][x]

    def flip_h(s):
        n = Canvas(s.w, s.h)
        for y in range(s.h):
            for x in range(s.w):
                n.p[y][x] = s.p[y][s.w - 1 - x]
        return n

    def copy(s):
        n = Canvas(s.w, s.h, s.wrap)
        n.p = [row[:] for row in s.p]
        return n

    def outline(s, col=OUT, diag=False):
        add = []
        for y in range(s.h):
            for x in range(s.w):
                if s.p[y][x] is None:
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)) + (((1, 1), (-1, 1), (1, -1), (-1, -1)) if diag else ()):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < s.w and 0 <= ny < s.h and s.p[ny][nx] is not None and s.p[ny][nx] != col:
                            add.append((x, y)); break
        for x, y in add:
            s.p[y][x] = col
        return s

    def inner_outline(s, col=OUT):
        """recolour border pixels of the shape (keeps the canvas size / silhouette)."""
        add = []
        for y in range(s.h):
            for x in range(s.w):
                if s.p[y][x] is not None:
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        nx, ny = x + dx, y + dy
                        if not (0 <= nx < s.w and 0 <= ny < s.h) or s.p[ny][nx] is None:
                            add.append((x, y)); break
        for x, y in add:
            s.p[y][x] = col
        return s

    def to_image(s):
        im = Image.new('RGBA', (s.w, s.h), (0, 0, 0, 0))
        px = im.load()
        for y in range(s.h):
            for x in range(s.w):
                c = s.p[y][x]
                if c is not None:
                    px[x, y] = (c[0], c[1], c[2], c[3] if len(c) > 3 else 255)
        return im


def reg(name, canvas, note=''):
    SPRITES[name] = canvas
    if note:
        NOTES[name] = note


def save_all():
    for name, cv in SPRITES.items():
        cv.to_image().save(os.path.join(OUT_DIR, name + '.png'))


def shade(c, f):
    """multiply colour brightness"""
    return tuple(max(0, min(255, int(v * f))) for v in c[:3])
# =============================================================== CHARACTERS (22x26)
STEEL = (C('d5deec'), C('94a1b9'), C('5f6b88'))
GOLD = (C('ffe58a'), C('f4b730'), C('b97a1c'))
REDP = (C('f4786a'), C('d9393f'), C('9a2036'))
SKIN = (C('ffe6cf'), C('fccaa2'), C('e3a07e'))
HAIRB = (C('b27040'), C('7c4828'), C('51301d'))
BOOT = (C('8f6038'), C('6a4228'))
BELT = C('8c5732')
DARKV = C('3d4262')
SLIT = C('1d1a30')
BLUSH = C('f59a98')


def leg(c, x0, y_top, y_boot, outward):
    # steel greave, gold knee, brown boot
    for y in range(y_top, y_boot):
        row = STEEL[2] if y == y_top else STEEL[1]
        c.rect(x0, y, x0 + 2, y, row)
        c.set(x0, y, STEEL[0] if y != y_top else STEEL[1])
    c.rect(x0, y_top + 1, x0 + 2, y_top + 1, GOLD[1])
    c.set(x0, y_top + 1, GOLD[0])
    bx0, bx1 = (x0 - 1, x0 + 2) if outward < 0 else (x0, x0 + 3)
    c.rect(bx0, y_boot, bx1, y_boot, BOOT[0])
    c.rect(bx0, y_boot, bx1, y_boot, BOOT[1])
    c.set(bx0 if outward < 0 else bx1, y_boot, BOOT[1])
    c.set(bx1 if outward < 0 else bx0, y_boot, BOOT[0])


def torso(c, x0=7, x1=14, y0=16, y1=19):
    c.rect(x0, y0, x1, y1, STEEL[1])
    c.rect(x0, y0, x0 + 1, y1, STEEL[0])
    c.rect(x1 - 1, y0, x1, y1, STEEL[2])
    c.rect(x0 + 1, y0, x1 - 1, y0, GOLD[1])           # gold collar
    cx = (x0 + x1) // 2
    c.rect(cx, y0 + 1, cx + 1, y0 + 2, GOLD[1])        # chest emblem
    c.set(cx, y0 + 1, GOLD[0])
    c.rect(x0, y1 + 1, x1, y1 + 1, BELT)               # belt
    c.rect(cx, y1 + 1, cx + 1, y1 + 1, GOLD[1])
    c.set(cx, y1 + 1, GOLD[0])
    c.rect(x0, y1 + 2, x1, y1 + 2, STEEL[2])           # tassets


def pauldron(c, cx, cy):
    c.ellipse(cx, cy, 3.2, 2.5, GOLD[2])
    c.shaded(cx, cy - 0.1, 2.6, 1.9, STEEL)
    c.set(int(cx), int(cy) - 1, STEEL[0])


def arm(c, x0, y0, y1):
    c.rect(x0, y0, x0 + 1, y1, STEEL[1])
    c.set(x0, y0, STEEL[0])
    c.rect(x0, y1 - 1, x0 + 1, y1, STEEL[2])           # gauntlet
    c.rect(x0, y1 - 2, x0 + 1, y1 - 2, GOLD[1])        # cuff


def helmet_head(c, cx=11, cy=9.5):
    c.shaded(cx, cy, 7.6, 7.1, STEEL)
    # gold brow band + visor
    y = int(cy - 2)
    c.rect(cx - 6, y, cx + 5, y, GOLD[1])
    c.rect(cx - 5, y + 1, cx + 4, y + 5, DARKV)
    c.rect(cx - 1, y + 1, cx, y + 5, C('646c8f'))
    for sx in (-4, -2, 1, 3):
        c.rect(cx + sx, y + 2, cx + sx, y + 5, SLIT)
    c.rect(cx - 4, y + 6, cx + 3, y + 7, STEEL[2])      # chin guard
    c.rect(cx - 3, y + 6, cx + 2, y + 6, STEEL[1])
    # rivets + shine
    c.set(cx - 6, y + 3, GOLD[0]); c.set(cx + 5, y + 3, GOLD[1])
    for dx, dy in ((-5, -4), (-4, -4), (-5, -3), (-3, -5)):
        c.set(cx + dx, int(cy) + dy, C('f4f7ff'))
    # crest mount
    c.rect(cx - 1, int(cy - 7.5), cx, int(cy - 6), GOLD[1])
    c.set(cx - 1, int(cy - 7.5), GOLD[0])


def plume(c, cx=11, cy=9.5):
    top = cy - 7
    pts = [(cx - 1, top + 1.5), (cx - 1.5, top - 0.6), (cx - 3.2, top - 1.8), (cx - 5.4, top - 1.6),
           (cx - 7.2, top - 0.2), (cx - 8.2, top + 2.2), (cx - 8.4, top + 5.0), (cx - 7.6, top + 7.2),
           (cx - 6.6, top + 5.4), (cx - 6.2, top + 3.4), (cx - 5.0, top + 1.8), (cx - 3.4, top + 0.8),
           (cx - 2.2, top + 1.6)]
    c.poly(pts, REDP[1])
    pts2 = [(cx - 2.0, top + 0.6), (cx - 3.3, top - 0.6), (cx - 5.2, top - 0.6), (cx - 6.6, top + 0.6)]
    c.poly(pts2, REDP[0])
    pts3 = [(cx - 7.2, top + 3.6), (cx - 7.6, top + 6.8), (cx - 6.6, top + 5.4)]
    c.poly(pts3, REDP[2])
    c.line(cx - 5.0, top + 1.4, cx - 2.6, top + 1.6, REDP[2])


def bare_head(c, cx=11, cy=9.5):
    c.shaded(cx, cy + 0.5, 6.9, 6.3, SKIN)
    hair = [(cx - 7.4, cy + 1.5), (cx - 7.6, cy - 3.5), (cx - 6, cy - 6.2), (cx - 3, cy - 7.6), (cx, cy - 7.9),
            (cx + 3, cy - 7.6), (cx + 6, cy - 6.2), (cx + 7.6, cy - 3.5), (cx + 7.4, cy + 1.5),
            (cx + 6.2, cy - 1.6), (cx + 4.2, cy - 2.6), (cx + 2.4, cy - 1.2), (cx, cy - 3.4),
            (cx - 2.4, cy - 1.2), (cx - 4.2, cy - 2.6), (cx - 6.2, cy - 1.6)]
    c.poly(hair, HAIRB[1])
    c.poly([(cx - 6, cy - 5.6), (cx - 3, cy - 7), (cx, cy - 7.2), (cx - 2, cy - 5.2), (cx - 4.5, cy - 4)], HAIRB[0])
    # face
    ey = int(cy + 0.5)
    for ex in (cx - 4, cx + 3):
        c.rect(ex, ey, ex + 1, ey + 2, SLIT)
        c.set(ex, ey, WHITE)
    c.rect(cx - 6, ey + 3, cx - 5, ey + 3, BLUSH)
    c.rect(cx + 4, ey + 3, cx + 5, ey + 3, BLUSH)
    c.rect(cx - 1, ey + 4, cx, ey + 4, C('c4584f'))
    c.set(cx - 2, ey + 3, C('c4584f')); c.set(cx + 1, ey + 3, C('c4584f'))


def sword(c, dy=0):
    c.rect(19, 9 + dy, 19, 17 + dy, C('f2f6ff'))
    c.rect(20, 10 + dy, 20, 17 + dy, STEEL[1])
    c.set(19, 8 + dy, C('f2f6ff')); c.set(20, 9 + dy, STEEL[0])
    c.rect(18, 18 + dy, 20, 18 + dy, GOLD[1]); c.set(18, 18 + dy, GOLD[0])
    c.set(19, 19 + dy, BELT)


def knight(pose='idle', helmet=True):
    c = Canvas(22, 26)
    up = pose == 'jump'
    if pose == 'idle':
        leg(c, 7, 21, 24, -1); leg(c, 12, 21, 24, +1)
    elif pose == 'walk1':
        leg(c, 6, 21, 24, -1); leg(c, 13, 21, 23, +1)
    elif pose == 'walk2':
        leg(c, 7, 21, 23, -1); leg(c, 12, 21, 24, +1)
    elif pose == 'jump':
        leg(c, 7, 21, 22, -1)
        c.rect(5, 23, 7, 23, STEEL[1]); c.rect(4, 24, 7, 24, BOOT[0])
        leg(c, 12, 21, 22, +1)
        c.rect(14, 23, 16, 23, STEEL[1]); c.rect(14, 24, 17, 24, BOOT[0])
    torso(c)
    if up:
        arm(c, 2, 13, 17); arm(c, 18, 12, 16)
        pauldron(c, 5.6, 17.2); pauldron(c, 16.4, 17.2)
    else:
        arm(c, 3, 17, 22); arm(c, 17, 17, 22)
        pauldron(c, 5.8, 17.2); pauldron(c, 16.2, 17.2)
        c.set(19, 20, STEEL[2]); c.set(19, 21, STEEL[2])
    if helmet:
        plume(c); helmet_head(c)
    else:
        bare_head(c)
    sword(c, dy=-5 if up else 0)
    if up:
        c.rect(19, 12, 19, 13, STEEL[2])
    c.outline()
    return c


def knight_sit():
    c = Canvas(22, 26)
    # legs stretched right
    c.rect(8, 23, 17, 24, STEEL[1])
    c.rect(8, 23, 17, 23, STEEL[0])
    c.rect(8, 24, 17, 24, STEEL[2])
    c.rect(13, 23, 14, 24, GOLD[1]); c.set(13, 23, GOLD[0])
    c.rect(17, 21, 20, 24, BOOT[0]); c.rect(17, 24, 20, 24, BOOT[1]); c.rect(17, 21, 17, 24, BOOT[1])
    torso(c, 7, 14, 17, 20)
    arm(c, 3, 18, 23)
    pauldron(c, 5.8, 18.2); pauldron(c, 16.2, 18.2)
    arm(c, 16, 19, 22)
    plume(c, 11, 11.0); helmet_head(c, 11, 11.0)
    c.outline()
    return c


def knight_kneel():
    c = Canvas(22, 26)
    # left knee on ground, shin back
    c.rect(6, 22, 8, 24, STEEL[1]); c.rect(6, 22, 6, 24, STEEL[0])
    c.rect(2, 23, 5, 24, STEEL[2]); c.rect(2, 24, 5, 24, STEEL[2])
    c.rect(1, 23, 2, 24, BOOT[0])
    c.rect(6, 24, 8, 24, GOLD[1])
    # right leg bent forward
    c.rect(9, 21, 14, 22, STEEL[1]); c.rect(9, 21, 14, 21, STEEL[0]); c.rect(9, 22, 14, 22, STEEL[2])
    c.rect(13, 21, 14, 22, GOLD[1]); c.set(13, 21, GOLD[0])
    c.rect(13, 23, 15, 24, STEEL[1]); c.rect(13, 23, 13, 24, STEEL[0])
    c.rect(13, 24, 17, 24, BOOT[0]); c.set(17, 24, BOOT[1])
    torso(c, 6, 13, 16, 19)
    # left arm resting
    arm(c, 3, 18, 22)
    pauldron(c, 5.6, 17.4)
    # right arm extended forward, open palm up at (19,15)
    pauldron(c, 13.6, 17.4)
    c.rect(14, 16, 16, 18, STEEL[1]); c.rect(14, 16, 16, 16, STEEL[0])
    c.rect(16, 15, 18, 16, STEEL[1]); c.rect(16, 15, 18, 15, STEEL[0])
    c.rect(16, 16, 17, 16, GOLD[1])
    c.rect(18, 15, 20, 15, STEEL[0])
    c.set(20, 14, STEEL[1]); c.set(19, 16, STEEL[2])
    bare_head(c, 10, 10.0)
    c.outline()
    return c


# ---------------------------------------------------------------- princess
GOWN = (C('c4c8e0'), C('9a9fc0'), C('6f7499'))
BLUE = (C('9fdcff'), C('2f8fe8'), C('1d5cb0'))
EYE = C('3a2530')


def princess(pose='idle'):
    c = Canvas(22, 26)
    up = pose == 'jump'
    # back hair (long wavy)
    c.shaded(11, 11, 9.0, 8.8, HAIRB, light=(-0.5, -0.9))
    for y0, xs in ((16, (1, 2)), (17, (1, 2)), (18, (2,)), (19, (3, 2)), (20, (3,)), (21, (4,))):
        for x in xs:
            c.symset(x + 1, y0, HAIRB[1])
    c.symset(2, 15, HAIRB[2]); c.symset(2, 18, HAIRB[0]); c.symset(3, 20, HAIRB[2])
    # gown
    hem = {'idle': (23, 23), 'walk1': (23, 22), 'walk2': (22, 23), 'jump': (22, 22)}[pose]
    widen = 1 if up else 0
    rows = {17: (8, 13), 18: (8, 13), 19: (7, 14), 20: (6, 15), 21: (5, 16), 22: (4 - widen, 17 + widen),
            23: (3 - widen, 18 + widen)}
    for y, (x0, x1) in rows.items():
        c.rect(x0, y, x1, y, GOWN[1])
        c.set(x0, y, GOWN[0]); c.set(x1, y, GOWN[2])
        if x1 - x0 > 7:
            c.set(x0 + 1, y, GOWN[0])
            c.set(x1 - 1, y, GOWN[2])
    # fold lines
    for fx in (9, 12):
        c.rect(fx, 20, fx, 22, GOWN[2])
    c.set(8, 21, GOWN[2]); c.set(13, 21, GOWN[2])
    # hem: gold stripe, wave per side
    for x in range(3 - widen, 19 + widen):
        hy = hem[0] if x < 11 else hem[1]
        if x < 11 and hy == 22 or x >= 11 and hy == 22:
            pass
        c.set(x, hy, GOLD[1] if (x % 2 == 0) else GOLD[0])
        for yy in range(hy + 1, 24):
            c.set(x, yy, None)
        for yy in range(23, hy, -1):
            pass
    # clear skirt pixels below the hem so hem waves
    for x in range(2, 20):
        hy = hem[0] if x < 11 else hem[1]
        for yy in range(hy + 1, 24):
            c.set(x, yy, None)
    # shoes peeking
    if pose in ('idle',):
        c.rect(6, 24, 8, 24, BOOT[1]); c.rect(13, 24, 15, 24, BOOT[1])
    elif pose == 'walk1':
        c.rect(4, 24, 7, 24, BOOT[1]); c.rect(14, 23, 15, 23, BOOT[1])
    elif pose == 'walk2':
        c.rect(6, 23, 7, 23, BOOT[1]); c.rect(14, 24, 17, 24, BOOT[1])
    # bodice + neckline
    c.rect(8, 16, 13, 16, GOLD[1]); c.set(8, 16, GOLD[0])
    c.rect(10, 17, 11, 17, BLUE[1]); c.set(10, 17, BLUE[0])
    c.rect(9, 18, 12, 18, GOLD[1])
    # sleeves + arms
    if up:
        c.rect(3, 13, 4, 16, SKIN[1]); c.rect(17, 13, 18, 16, SKIN[1])
        c.set(3, 13, SKIN[0]); c.set(17, 13, SKIN[0])
    else:
        c.rect(4, 19, 5, 21, SKIN[1]); c.rect(16, 19, 17, 21, SKIN[1])
        c.set(4, 19, SKIN[0]); c.set(16, 19, SKIN[0])
        c.set(4, 21, SKIN[2]); c.set(17, 21, SKIN[2])
    c.shaded(6.2, 17.8, 2.9, 2.3, GOWN)
    c.shaded(15.8, 17.8, 2.9, 2.3, GOWN)
    c.rect(4, 19, 7, 19, GOLD[1]) if False else None
    # face
    c.shaded(11, 11.4, 6.9, 5.8, SKIN, light=(-0.4, -0.9), th=(-0.55, 0.65))
    # bangs
    c.shaded(11, 6.4, 7.4, 3.7, HAIRB, light=(-0.5, -0.9))
    for bx, by in ((7, 8), (6, 8), (14, 8), (15, 8)):
        c.set(bx, by, HAIRB[1])
    c.shaded(5.3, 9.6, 1.5, 3.2, HAIRB); c.shaded(16.7, 9.6, 1.5, 3.2, HAIRB)
    c.rect(10, 6, 11, 8, HAIRB[0]) if False else None
    c.set(10, 8, SKIN[1]); c.set(11, 8, SKIN[1])
    c.set(10, 7, HAIRB[1]); c.set(11, 7, HAIRB[1])
    # eyes (big, dark), blush, mouth
    for ex in (7, 13):
        c.rect(ex, 10, ex + 1, 12, EYE)
        c.set(ex, 10, WHITE); c.set(ex + 1, 12, C('7a4a52'))
    c.rect(5, 13, 6, 13, BLUSH); c.rect(15, 13, 16, 13, BLUSH)
    c.rect(10, 14, 11, 14, C('c4584f'))
    # crown
    c.rect(8, 3, 13, 3, GOLD[1]); c.rect(8, 4, 13, 4, GOLD[2])
    c.set(8, 2, GOLD[1]); c.set(10, 2, GOLD[1]); c.set(11, 2, GOLD[1]); c.set(13, 2, GOLD[1])
    c.set(10, 1, GOLD[0]); c.set(11, 1, GOLD[1])
    c.set(8, 3, GOLD[0]); c.rect(10, 3, 11, 3, BLUE[1]); c.set(10, 3, BLUE[0])
    c.outline()
    return c


def build_characters():
    reg('knight_idle', knight('idle'), 'chibi knight (helmet), front-facing; mirror in code')
    reg('knight_walk1', knight('walk1'))
    reg('knight_walk2', knight('walk2'))
    reg('knight_jump', knight('jump'))
    reg('knight_sit', knight_sit(), 'helmeted knight sitting, legs forward (right)')
    reg('knight_bare_idle', knight('idle', helmet=False), 'knight without helmet')
    reg('knight_bare_kneel', knight_kneel(), 'kneeling on left knee, open palm centre at pixel (19,15) from top-left')
    reg('princess_idle', princess('idle'))
    reg('princess_walk1', princess('walk1'))
    reg('princess_walk2', princess('walk2'))
    reg('princess_jump', princess('jump'))
# =============================================================== ENEMIES
CAT_COLORS = {
    'orange': dict(fur=(C('ffc27a'), C('f28f3b'), C('c9622a')), belly=C('fff0d2'), stripe=C('c9622a'), eye='dark', out=OUT),
    'gray':   dict(fur=(C('dfe5f0'), C('a5afc6'), C('7482a3')), belly=C('f2f5fb'), stripe=C('7482a3'), eye='dark', out=OUT),
    'black':  dict(fur=(C('7a74b0'), C('4a4580'), C('322e62')), belly=C('6a65a0'), stripe=C('322e62'), eye='yellow', out=C('15112b')),
    'white':  dict(fur=(C('ffffff'), C('f6f0fb'), C('d6c9ea')), belly=C('ffffff'), stripe=C('e2c8ea'), eye='dark', out=OUT),
}


def cat(color, frame):
    k = CAT_COLORS[color]
    fur, belly = k['fur'], k['belly']
    c = Canvas(16, 14)
    # tail
    for (x, y) in ((13, 10), (14, 9), (14, 8), (14, 7), (13, 6)):
        c.set(x, y, fur[1])
    c.set(13, 6, fur[2]); c.set(14, 8, fur[2])
    # body / head blob
    c.shaded(8, 7.0, 6.2, 4.5, fur, light=(-0.5, -0.9), th=(-0.45, 0.55))
    # ears
    for sgn in (1, -1):
        def X(v):
            return v if sgn == 1 else 16 - v
        c.poly([(X(2.2), 4.6), (X(2.6), 0.9), (X(6.2), 3.4)], fur[1])
        c.poly([(X(3.2), 3.6), (X(3.4), 2.2), (X(4.9), 3.2)], C('f79bb0'))
        c.set(int(X(2.6)) if sgn == 1 else int(X(2.6)) - 1, 2, fur[0])
    # forehead markings
    if color in ('orange', 'gray'):
        for (x, y) in ((7, 3), (8, 3), (7, 4), (8, 4), (5, 4), (10, 4)):
            c.set(x, y, k['stripe'])
        c.set(7, 5, k['stripe']); c.set(8, 5, k['stripe'])
    elif color == 'white':
        c.rect(7, 3, 8, 4, k['stripe'])
    else:
        c.rect(7, 4, 8, 4, k['stripe'])
    # belly + paws
    c.ellipse(8, 10.4, 3.3, 1.5, belly)
    lp, rp = (12.8, 11.8) if frame == 1 else (11.8, 12.8)
    c.shaded(5.0, lp - 0.1, 2.0, 1.2, (belly, belly, fur[0]))
    c.shaded(11.0, rp - 0.1, 2.0, 1.2, (belly, belly, fur[0]))
    c.set(4, int(lp), fur[2]); c.set(11, int(rp), fur[2])
    # eyes
    for ex in (4, 9):
        if k['eye'] == 'yellow':
            c.rect(ex, 6, ex + 2, 8, C('ffe14a'))
            c.rect(ex + 1, 6, ex + 1, 8, C('15112b'))
            c.set(ex, 6, WHITE)
        else:
            c.rect(ex, 6, ex + 2, 8, C('2b2540'))
            c.set(ex, 6, WHITE); c.set(ex + 2, 8, C('5b4f8a'))
    # angry brows
    for (x, y) in ((3, 5), (4, 5), (5, 5), (6, 6)):
        c.set(x, y, C('2b2540'))
    for (x, y) in ((12, 5), (11, 5), (10, 5), (9, 6)):
        c.set(x, y, C('2b2540'))
    # nose, mouth, fangs
    c.rect(7, 9, 8, 9, C('ff8aa5'))
    c.rect(7, 10, 8, 10, C('2b2540'))
    c.set(6, 10, WHITE); c.set(9, 10, WHITE)
    c.outline(k['out'])
    return c


def turtle(color, frame):
    """low crawling turtle (16x12), faces left; the code mirrors it"""
    skin = (C('b6ec7e'), C('7cc656'), C('4e9a42')) if color == 'green' else (C('d8b6ff'), C('a67ae6'), C('7650b8'))
    shell = (C('7fd86a'), C('3fa656'), C('26703e')) if color == 'green' else (C('ff9fd0'), C('e0589a'), C('a03670'))
    c = Canvas(16, 12)
    # back foot + front foot
    fa, fb = (10.4, 7.6) if frame == 1 else (9.2, 8.8)
    c.shaded(fa, 10.3, 1.9, 1.3, (C('ffb35c'), C('f08a3a'), C('b45a2a')))
    c.shaded(fb + 0.0, 10.3, 1.9, 1.3, (C('ffb35c'), C('f08a3a'), C('b45a2a')))
    # tail
    c.set(15, 8, skin[1]); c.set(15, 9, skin[2])
    # shell dome
    c.shaded(9.6, 6.6, 5.6, 4.6, shell, light=(-0.5, -0.9))
    c.rect(4, 9, 15, 11, None)
    ln = shell[2]
    for (x, y) in ((8, 4), (11, 4), (9, 6), (12, 6), (7, 7), (10, 8), (13, 8)):
        c.set(x, y, ln)
    for (x, y) in ((7, 3), (8, 3)):
        c.set(x, y, shell[0])
    # tan belly rim
    c.rect(5, 9, 14, 9, C('ffdf96')); c.rect(5, 9, 14, 9, C('ffdf96'))
    # head, sticking out to the left
    c.shaded(3.4, 6.6, 3.1, 2.9, skin, light=(-0.5, -0.9))
    c.rect(2, 5, 3, 7, WHITE); c.rect(2, 6, 2, 7, C('2b2540'))
    c.rect(1, 4, 3, 4, C('2b2540'))
    c.rect(1, 8, 2, 8, C('c4584f'))
    c.outline()
    return c


def turtle_shell(color):
    shell = (C('7fd86a'), C('3fa656'), C('26703e')) if color == 'green' else (C('ff9fd0'), C('e0589a'), C('a03670'))
    c = Canvas(16, 10)
    c.shaded(8, 6.0, 7.3, 5.4, shell, light=(-0.5, -0.9))
    c.rect(0, 7, 15, 9, None)                      # flat bottom
    ln = shell[2]
    for (x0, y0, x1, y1) in ((5, 2, 10, 2), (4, 3, 5, 2), (11, 3, 10, 2), (4, 3, 6, 5), (11, 3, 9, 5), (6, 5, 9, 5),
                             (3, 3, 1, 4), (12, 3, 14, 4)):
        c.line(x0, y0, x1, y1, ln)
    for (x, y) in ((6, 3), (7, 3)):
        c.set(x, y, shell[0])
    c.rect(1, 7, 14, 8, C('ffdf96'))
    c.rect(1, 8, 14, 8, C('e8b86c'))
    for x in (3, 6, 9, 12):
        c.set(x, 7, C('e8b86c'))
    c.outline()
    return c


FROG_SKIN = (C('b8f070'), C('72c94a'), C('3f9a3c'))
FROG_BELLY = (C('fff6b0'), C('ffe666'), C('e8c040'))


def frog(frame):
    """ribbit! green frog with a yellow belly and big eyes (16x14), faces forward"""
    c = Canvas(16, 14)
    hop = frame == 2
    top = 5 if hop else 6
    # hind legs
    if hop:
        c.poly([(1.0, 13.8), (5.0, 8.0), (6.5, 11.5), (4.5, 13.8)], FROG_SKIN[1])
        c.poly([(15.0, 13.8), (11.0, 8.0), (9.5, 11.5), (11.5, 13.8)], FROG_SKIN[1])
        c.rect(0, 13, 3, 13, FROG_SKIN[2]); c.rect(13, 13, 15, 13, FROG_SKIN[2])
    else:
        c.shaded(3.2, 11.4, 2.7, 2.2, FROG_SKIN)
        c.shaded(12.8, 11.4, 2.7, 2.2, FROG_SKIN)
        c.rect(0, 13, 3, 13, FROG_SKIN[2]); c.rect(12, 13, 15, 13, FROG_SKIN[2])
    # body
    c.shaded(8, top + 4.2, 6.2, 4.4, FROG_SKIN, light=(-0.5, -0.9))
    c.shaded(8, top + 5.4, 3.7, 2.7, FROG_BELLY, light=(-0.5, -0.9))
    # front feet
    c.shaded(5.2, 13, 1.5, 0.9, FROG_SKIN); c.shaded(10.8, 13, 1.5, 0.9, FROG_SKIN)
    # eyes on top
    for ex in (4.4, 11.6):
        c.shaded(ex, top + 0.6, 2.6, 2.5, (C('fffbd0'), C('ffe666'), C('e8c040')), th=(-0.3, 0.55))
        x = int(ex)
        c.rect(x, top, x + 1, top + 2, C('2b2540'))
        c.set(x, top, WHITE)
    # cheeks + smile
    c.set(3, top + 5, C('ff9a9a')); c.set(12, top + 5, C('ff9a9a'))
    for x in range(5, 11):
        c.set(x, top + 5, C('2b2540'))
    c.set(4, top + 4, C('2b2540')); c.set(11, top + 4, C('2b2540'))
    c.outline()
    return c


def plant(frame):
    red = (C('ff7d70'), C('e5384a'), C('a82246'))
    c = Canvas(16, 24)
    # leaves + stem
    c.poly([(5.5, 21.6), (1.6, 17.4), (1.2, 20.2), (3.2, 22.2)], C('4fc15a'))
    c.poly([(10.5, 21.6), (14.4, 17.4), (14.8, 20.2), (12.8, 22.2)], C('4fc15a'))
    c.set(2, 19, C('2f8f45')); c.set(13, 19, C('2f8f45'))
    c.rect(6, 14, 9, 22, C('4fc15a'))
    c.rect(6, 14, 6, 22, C('8be37a')); c.rect(9, 14, 9, 22, C('2f8f45'))
    # head
    c.shaded(8, 8.4, 7.2, 6.8, red, light=(-0.5, -0.9))
    # spots
    for (x, y, r) in ((4.5, 4.6, 1.4), (11.3, 4.0, 1.3), (8.0, 2.9, 1.1), (13.0, 8.0, 0.9)):
        c.ellipse(x, y, r, r, C('fff2f2'))
    # mouth
    dark = C('4a0f2e')
    if frame == 1:
        c.ellipse(8, 10.0, 5.9, 3.5, dark)
        c.ellipse(8, 12.2, 3.2, 1.3, C('ff7aa8'))
        for x in (3, 5, 7, 9, 11):
            c.rect(x, 7, x + 1, 7, WHITE); c.set(x, 8, WHITE) if x in (3, 7, 11) else None
        for x in (4, 6, 8, 10, 12):
            c.set(x, 12 if x not in (6, 8, 10) else 12, None) if False else None
        for x in (4, 12):
            c.rect(x, 12, x, 12, WHITE)
        for x in (5, 11):
            c.set(x, 12, WHITE)
        c.rect(6, 7, 9, 7, WHITE)
    else:
        c.ellipse(8, 9.4, 6.0, 1.5, dark)
        for x in (3, 5, 7, 9, 11):
            c.set(x, 8, WHITE); c.set(x, 9, WHITE)
        for x in (4, 6, 8, 10):
            c.set(x, 10, WHITE)
        c.ellipse(8, 10.8, 2.6, 0.7, C('ff7aa8'))
    c.outline()
    return c


def build_enemies():
    for col in CAT_COLORS:
        for f in (1, 2):
            reg('cat_%s_%d' % (col, f), cat(col, f), 'cat 16x14, pivot bottom' if f == 1 else '')
    for col in ('green', 'purple'):
        for f in (1, 2):
            reg('turtle_%s_%d' % (col, f), turtle(col, f), 'low crawling turtle 16x12, faces left')
        reg('turtle_%s_shell' % col, turtle_shell(col), 'stomped shell 16x10')
    reg('plant_1', plant(1), 'piranha plant, mouth open (bottom row = stem base)')
    reg('plant_2', plant(2), 'piranha plant, mouth closed')
    for f in (1, 2):
        reg('frog_%d' % f, frog(f), 'frog, sitting / hopping')
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


BUILDERS = []
def write_sprites_md():
    pass
BUILDERS.extend([build_characters, build_enemies, build_items])


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


# ======================================================================= export: PNG previews + C# data file
import base64, io

# pivot rules (x, y) with y=0 at the bottom of the sprite
def pivot_for(name):
    if name.startswith(('knight', 'princess', 'cat_', 'turtle_', 'frog_', 'plant_', 'item_', 'flower_', 'flag_pole',
                        'bg_hill', 'bg_bush', 'castle', 'bouquet', 'spike', 'bg_rainbow', 'bg_cloud')):
        return (0.5, 0.0)
    return (0.5, 0.5)


def write_cs():
    cs_path = os.path.join(ROOT, 'Assets', 'Scripts', 'PixelArtData.cs')
    lines = ['// <auto-generated> by Tools/generate_art.py - do not edit by hand.',
             '// Every sprite of the game, embedded as a base64 PNG (nothing to import).',
             'using System.Collections.Generic;', '',
             'public static class PixelArtData', '{',
             '    public struct Entry { public string b64; public float px, py; public Entry(string b, float x, float y) { b64 = b; px = x; py = y; } }',
             '    public static readonly Dictionary<string, Entry> All = new Dictionary<string, Entry>', '    {']
    for name in sorted(SPRITES):
        im = SPRITES[name].to_image()
        buf = io.BytesIO(); im.save(buf, 'PNG', optimize=True)
        b64 = base64.b64encode(buf.getvalue()).decode()
        px, py = pivot_for(name)
        lines.append('        { "%s", new Entry("%s", %sf, %sf) },' % (name, b64, px, py))
    if os.path.exists(LOGO_SRC):
        buf = io.BytesIO(); Image.open(LOGO_SRC).convert('RGBA').save(buf, 'PNG', optimize=True)
        lines.append('        { "logo", new Entry("%s", 0.5f, 0.5f) },' % base64.b64encode(buf.getvalue()).decode())
    lines += ['    };', '}', '']
    with open(cs_path, 'w') as f:
        f.write('\n'.join(lines))
    print('wrote', cs_path, os.path.getsize(cs_path), 'bytes')


# ======================================================================= 5x7 bitmap font (no font file needed)
GLYPHS = {
 'A': ".###.|#...#|#...#|#####|#...#|#...#|#...#",
 'B': "####.|#...#|#...#|####.|#...#|#...#|####.",
 'C': ".####|#....|#....|#....|#....|#....|.####",
 'D': "####.|#...#|#...#|#...#|#...#|#...#|####.",
 'E': "#####|#....|#....|####.|#....|#....|#####",
 'F': "#####|#....|#....|####.|#....|#....|#....",
 'G': ".###.|#...#|#....|#.###|#...#|#...#|.###.",
 'H': "#...#|#...#|#...#|#####|#...#|#...#|#...#",
 'I': ".###.|..#..|..#..|..#..|..#..|..#..|.###.",
 'J': "..###|...#.|...#.|...#.|...#.|#..#.|.##..",
 'K': "#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#",
 'L': "#....|#....|#....|#....|#....|#....|#####",
 'M': "#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#",
 'N': "#...#|##..#|#.#.#|#..##|#...#|#...#|#...#",
 'O': ".###.|#...#|#...#|#...#|#...#|#...#|.###.",
 'P': "####.|#...#|#...#|####.|#....|#....|#....",
 'Q': ".###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#",
 'R': "####.|#...#|#...#|####.|#.#..|#..#.|#...#",
 'S': ".####|#....|#....|.###.|....#|....#|####.",
 'T': "#####|..#..|..#..|..#..|..#..|..#..|..#..",
 'U': "#...#|#...#|#...#|#...#|#...#|#...#|.###.",
 'V': "#...#|#...#|#...#|#...#|#...#|.#.#.|..#..",
 'W': "#...#|#...#|#...#|#.#.#|#.#.#|##.##|#...#",
 'X': "#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#",
 'Y': "#...#|#...#|.#.#.|..#..|..#..|..#..|..#..",
 'Z': "#####|....#|...#.|..#..|.#...|#....|#####",
 '0': ".###.|#...#|#..##|#.#.#|##..#|#...#|.###.",
 '1': "..#..|.##..|..#..|..#..|..#..|..#..|.###.",
 '2': ".###.|#...#|....#|...#.|..#..|.#...|#####",
 '3': "####.|....#|....#|.###.|....#|....#|####.",
 '4': "...#.|..##.|.#.#.|#..#.|#####|...#.|...#.",
 '5': "#####|#....|####.|....#|....#|#...#|.###.",
 '6': ".###.|#....|#....|####.|#...#|#...#|.###.",
 '7': "#####|....#|...#.|..#..|.#...|.#...|.#...",
 '8': ".###.|#...#|#...#|.###.|#...#|#...#|.###.",
 '9': ".###.|#...#|#...#|.####|....#|....#|.###.",
 ' ': ".....|.....|.....|.....|.....|.....|.....",
 '.': ".....|.....|.....|.....|.....|.##..|.##..",
 ',': ".....|.....|.....|.....|.##..|..#..|.#...",
 '!': "..#..|..#..|..#..|..#..|..#..|.....|..#..",
 '?': ".###.|#...#|....#|...#.|..#..|.....|..#..",
 '-': ".....|.....|.....|#####|.....|.....|.....",
 ':': ".....|.##..|.##..|.....|.##..|.##..|.....",
 "'": "..#..|..#..|.#...|.....|.....|.....|.....",
 '<': "...#.|..#..|.#...|#....|.#...|..#..|...#.",
 '>': ".#...|..#..|...#.|....#|...#.|..#..|.#...",
 '/': "....#|...#.|...#.|..#..|.#...|.#...|#....",
 '+': ".....|..#..|..#..|#####|..#..|..#..|.....",
 '&': ".##..|#..#.|#.#..|.#...|#.#.#|#..#.|.##.#",
 '(': "..#..|.#...|#....|#....|#....|.#...|..#..",
 ')': "..#..|...#.|....#|....#|....#|...#.|..#..",
 '_': ".....|.....|.....|.....|.....|.....|#####",
 '^': ".#.#.|#####|#####|#####|.###.|..#..|.....",   # heart
 '*': "..#..|#.#.#|.###.|#####|.###.|#.#.#|..#..",   # sparkle
}


def write_font_cs():
    path = os.path.join(ROOT, 'Assets', 'Scripts', 'PixelFontData.cs')
    lines = ['// <auto-generated> by Tools/generate_art.py - 5x7 bitmap font glyphs ("#" = pixel).',
             'using System.Collections.Generic;', '',
             'public static class PixelFontData', '{',
             '    public static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>', '    {']
    for ch, g in GLYPHS.items():
        key = "'\\''" if ch == "'" else "'%s'" % ch
        lines.append('        { %s, "%s" },' % (key, g))
    lines += ['    };', '}', '']
    open(path, 'w').write('\n'.join(lines))
    # preview strip
    txt = "ABCDEFGHIJKLM NOPQRSTUVWXYZ 0123456789 .,!?-:'<>/+&()_^*"
    W = len(txt) * 6 + 2
    im = Image.new('RGBA', (W, 11), (40, 36, 70, 255))
    px = im.load()
    for i, ch in enumerate(txt):
        rows = GLYPHS[ch].split('|')
        for y, r in enumerate(rows):
            for x, v in enumerate(r):
                if v == '#':
                    px[1 + i * 6 + x, 2 + y] = (255, 255, 255, 255)
    im.resize((W * 6, 11 * 6), Image.NEAREST).save(os.path.join(ROOT, 'docs', 'sprites', '_font_preview.png'))
    print('font ok')


# ======================================================================= app icon files (Unity Player Settings > Icon)
def write_icons():
    if not os.path.exists(LOGO_SRC):
        return
    d = os.path.join(ROOT, 'Assets', 'Icons')
    os.makedirs(d, exist_ok=True)
    logo = Image.open(LOGO_SRC).convert('RGBA')
    sky = (92, 157, 240, 255)
    # default / legacy icon: crown on sky-blue, nearest-neighbour so pixels stay crisp
    base = Image.new('RGBA', (512, 512), sky)
    crown = logo.resize((352, 352), Image.NEAREST)
    base.alpha_composite(crown, (80, 80))
    base.save(os.path.join(d, 'AppIcon.png'))
    # adaptive icon layers (432x432, crown inside the 66% safe zone)
    fg = Image.new('RGBA', (432, 432), (0, 0, 0, 0))
    fg.alpha_composite(logo.resize((240, 240), Image.NEAREST), (96, 96))
    fg.save(os.path.join(d, 'AppIcon_Foreground.png'))
    Image.new('RGBA', (432, 432), sky).save(os.path.join(d, 'AppIcon_Background.png'))
    print('icons ok')


def main():
    for fn in BUILDERS:
        fn()
    save_all()
    write_cs()
    write_font_cs()
    write_icons()
    # logo
    if os.path.exists(LOGO_SRC):
        shutil.copyfile(LOGO_SRC, os.path.join(OUT_DIR, 'logo.png'))
        os.makedirs(os.path.join(ROOT, 'docs', 'images'), exist_ok=True)
        shutil.copyfile(LOGO_SRC, os.path.join(ROOT, 'docs', 'images', 'logo.png'))
    write_sprites_md()
    print('wrote', len(SPRITES), 'sprites to', OUT_DIR)


if __name__ == '__main__':
    main()
