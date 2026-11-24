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
