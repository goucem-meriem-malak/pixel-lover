#!/usr/bin/env python3
"""Builds docs/images/art.png (the cast sheet used in the README) from docs/sprites/*.png. Run after generate_art.py."""
import os
from PIL import Image
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
D = os.path.join(ROOT, 'docs', 'sprites')
S = 4

def load(n): return Image.open(os.path.join(D, n + '.png')).convert('RGBA')

def princess_wings():
    p, w = load('princess_idle'), load('wings_1')
    im = Image.new('RGBA', (w.width, p.height), (0, 0, 0, 0))
    im.alpha_composite(w, (0, 3))
    im.alpha_composite(p, ((im.width - p.width) // 2, im.height - p.height))
    return im

rows = [
    ((130, 180, 235), ['knight_idle', 'knight_walk1', 'knight_walk2', 'knight_sit', 'knight_bare_idle', 'knight_bare_kneel',
                       'princess_idle', 'princess_walk1', 'princess_jump', princess_wings]),
    ((150, 200, 245), ['cat_orange_1', 'cat_gray_1', 'cat_black_1', 'cat_white_1', 'turtle_green_1', 'turtle_purple_1',
                       'turtle_green_shell', 'frog_1', 'frog_2', 'plant_1']),
    ((255, 214, 228), ['coin_1', 'flower_1', 'item_wings', 'item_fireflower', 'ring_0', 'ring_1', 'ring_2', 'ring_3', 'ring_4',
                       'ringbox_open', 'heart']),
    ((196, 176, 232), ['tile_grass', 'tile_brick', 'tile_question', 'tile_cloud_top', 'tile_brick_sky', 'tile_question_pink',
                       'pipe_green_cap', 'pipe_pink_cap']),
]
W = 1180
rowh = [160, 150, 140, 110]
sheet = Image.new('RGBA', (W, sum(rowh)), (0, 0, 0, 255))
y0 = 0
for (bg, names), h in zip(rows, rowh):
    sheet.paste(Image.new('RGBA', (W, h), bg + (255,)), (0, y0))
    ims = []
    for n in names:
        try:
            im = n() if callable(n) else load(n)
        except FileNotFoundError:
            continue
        ims.append(im.resize((im.width * S, im.height * S), Image.NEAREST))
    gap = 22
    total = sum(i.width for i in ims) + gap * (len(ims) - 1)
    x = max(10, (W - total) // 2)
    for im in ims:
        sheet.alpha_composite(im, (x, y0 + h - 14 - im.height))
        x += im.width + gap
    y0 += h
out = os.path.join(ROOT, 'docs', 'images', 'art.png')
sheet.convert('RGB').save(out)
print('wrote', out, sheet.size)
