#!/bin/bash
# concatenates parts/*.py into generate_art.py (dev helper)
cd "$(dirname "$0")"
cat parts/p1_toolkit.py parts/p2_characters.py parts/p3_enemies.py parts/p4_items.py parts/p4z_glue.py parts/p5_extras.py parts/p6_export.py parts/p7_font.py parts/p8_icon.py parts/main.py > generate_art.py
