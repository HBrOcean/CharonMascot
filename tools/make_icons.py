#!/usr/bin/env python3
"""用 UTTU 官方头像裁出应用图标（512 主图标 / 32 托盘图标 / ICO）。

用法: python tools/make_icons.py
输出: src/CharonMascot/Assets/icons/{app.png, tray.png, app.ico}
"""
import os
from urllib.request import Request, urlopen

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'src', 'CharonMascot', 'Assets', 'icons')
SRC_URL = 'https://assets.merui.net/headicon_img_l2d/312801.webp'
SRC_LOCAL = os.path.join(ROOT, 'tools', '_icon_src.webp')


def fetch_source():
    if not os.path.exists(SRC_LOCAL):
        data = urlopen(Request(SRC_URL, headers={'User-Agent': 'Mozilla/5.0'}), timeout=60).read()
        with open(SRC_LOCAL, 'wb') as f:
            f.write(data)
    return Image.open(SRC_LOCAL).convert('RGBA')


def rounded(img, radius_ratio=0.23):
    w, h = img.size
    mask = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(mask)
    r = int(min(w, h) * radius_ratio)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=r, fill=255)
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    out.paste(img, (0, 0), mask)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    im = fetch_source()
    w, h = im.size

    # 头部区域：立绘中头在上方约 4%~24%，横向取中间 40%
    box_w = int(w * 0.52)
    cx = int(w * 0.5)
    y0 = int(h * 0.02)
    box = (cx - box_w // 2, y0, cx + box_w // 2, y0 + box_w)
    crop = im.crop(box).resize((512, 512), Image.LANCZOS)
    app = rounded(crop).convert('RGBA')
    app.save(os.path.join(OUT, 'app.png'))
    print('✓ app.png 512x512')

    tray = crop.resize((64, 64), Image.LANCZOS).convert('RGBA')
    tray.save(os.path.join(OUT, 'tray.png'))
    print('✓ tray.png 64x64')

    app.save(os.path.join(OUT, 'app.ico'), sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print('✓ app.ico')


if __name__ == '__main__':
    main()
