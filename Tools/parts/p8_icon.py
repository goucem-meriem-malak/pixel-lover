

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
