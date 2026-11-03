#!/usr/bin/env python3
"""从 UTTU Archive 下载卡戎（Charon）的 Q 版 Spine 素材。

用法:
    python tools/fetch_assets.py                # 下载全部三套装扮
    python tools/fetch_assets.py --only 312801  # 只处理一套

做的事:
    1. GET https://model.merui.net/chibi/{id}/{id}.skel|.atlas|.webp
    2. webp -> png（SkiaSharp 解码 png 最稳）
    3. 把 atlas 第一行的贴图页名改成 {id}.png
    4. 删除 .webp 中间文件
"""
import argparse
import os
from urllib.request import Request, urlopen

from PIL import Image

BASE = 'https://model.merui.net/chibi/{id}/{id}.{ext}'
HEADERS = {'User-Agent': 'Mozilla/5.0 (CharonMascot asset fetcher)'}
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, 'src', 'CharonMascot', 'Assets', 'characters')

GARMENTS = {
    '312801': '默认',
    '312802': '沉默与悲鸣',
    '312803': '亡者的恩赐',
}


def download(gid, dst):
    os.makedirs(dst, exist_ok=True)
    for ext in ('skel', 'atlas', 'webp'):
        url = BASE.format(id=gid, ext=ext)
        data = urlopen(Request(url, headers=HEADERS), timeout=60).read()
        with open(os.path.join(dst, f'{gid}.{ext}'), 'wb') as f:
            f.write(data)
        print(f'  ↓ {gid}.{ext}  {len(data) / 1024:.0f} KB')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--only', nargs='*', help='只处理指定的装扮 id')
    args = ap.parse_args()
    for gid in (args.only or list(GARMENTS)):
        print(f'== {gid} ({GARMENTS.get(gid, "?")})')
        dst = os.path.join(OUT_DIR, gid)
        download(gid, dst)

        webp = os.path.join(dst, f'{gid}.webp')
        png = os.path.join(dst, f'{gid}.png')
        Image.open(webp).convert('RGBA').save(png)
        os.remove(webp)

        atlas = os.path.join(dst, f'{gid}.atlas')
        lines = open(atlas, encoding='utf-8').read().split('\n')
        lines[0] = f'{gid}.png'
        open(atlas, 'w', encoding='utf-8').write('\n'.join(lines))
        print('  ✓ 完成')


if __name__ == '__main__':
    main()
