

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
