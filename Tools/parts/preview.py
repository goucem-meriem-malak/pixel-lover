#!/usr/bin/env python3
# usage: preview.py out.png scale name1 name2 ...   (reads from Assets/Resources/Sprites)
import sys, os
from PIL import Image
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
D = os.path.join(ROOT, 'docs', 'sprites')
out, scale = sys.argv[1], int(sys.argv[2])
names = sys.argv[3:]
ims = [Image.open(os.path.join(D, n + '.png')).convert('RGBA') for n in names]
pad = 4
cols = min(len(ims), int(sys.argv[2]) and 6)
rows = (len(ims) + cols - 1) // cols
cw = max(i.width for i in ims) * scale + pad * 2
ch = max(i.height for i in ims) * scale + pad * 2
sheet = Image.new('RGBA', (cols * cw, rows * ch), (120, 130, 150, 255))
for k, im in enumerate(ims):
    big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    # checker backing
    r, c = divmod(k, cols)
    for yy in range(0, ch, 16):
        for xx in range(0, cw, 16):
            if ((xx // 16) + (yy // 16)) % 2 == 0:
                for y in range(yy, min(yy + 16, ch)):
                    for x in range(xx, min(xx + 16, cw)):
                        sheet.putpixel((c * cw + x, r * ch + y), (138, 148, 168, 255))
    sheet.alpha_composite(big, (c * cw + pad, r * ch + pad + (ch - pad * 2 - big.height)))
sheet.convert('RGB').save(out)
