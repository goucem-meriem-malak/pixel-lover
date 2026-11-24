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
