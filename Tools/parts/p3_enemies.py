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
