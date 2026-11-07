#!/usr/bin/env python3
"""从 UTTU Archive 抓取一套（或多套）Q 版 Spine 模型，直接放进 Assets/characters/<id>/。

用法：
    python tools/fetch_assets.py 312801 312802 312803
    python tools/fetch_assets.py 312801 --name 默认

做了什么：
    1. 下载 <id>.skel / <id>.atlas / <id>.webp
    2. webp 转 PNG（RGBA，保留 alpha）
    3. atlas 第一行页名从 .webp 改成 .png（不这么做纹理加载不到）
    4. 写 display.txt（给了 --name 才写）
    5. 打印校验信息：图集尺寸、region 数、旋转统计、atlas/skel 引用一致性

依赖：pip install pillow
注意：CDN 对 Python urllib 会直接断连，所以这里用 curl 子进程下载。
"""

import argparse
import os
import subprocess
import sys

BASE = "https://model.merui.net/chibi/{id}/{id}.{ext}"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0 Safari/537.36")
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "characters")


def download(url, out):
    """curl 下载。返回 HTTP 码。"""
    cmd = ["curl", "-sSL", "--fail", "-A", UA,
           "-e", "https://uttu.merui.net/", "-o", out, url]
    return subprocess.run(cmd, capture_output=True).returncode == 0


def convert(webp, png, atlas_path):
    try:
        from PIL import Image
    except ImportError:
        sys.exit("缺少 Pillow：pip install pillow")

    img = Image.open(webp).convert("RGBA")
    img.save(png)
    os.remove(webp)

    with open(atlas_path, encoding="utf-8") as f:
        lines = f.read().split("\n")
    lines[0] = os.path.basename(png)
    with open(atlas_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    return img.size


def report(pid, img_size, atlas_path, skel_path):
    names, rots = [], {}
    with open(atlas_path, encoding="utf-8") as f:
        for ln in f:
            s = ln.strip()
            if s.startswith("rotate:"):
                v = s.split(":", 1)[1].strip()
                rots[v] = rots.get(v, 0) + 1
            elif s and ":" not in s and not s.endswith(".png"):
                names.append(s)

    # skel 里的 region 名是明文，用 strings 思路粗略校验
    try:
        raw = open(skel_path, "rb").read()
        missing = [n for n in names if n.encode("utf-8") not in raw]
    except OSError:
        missing = []

    print(f"  {pid}: 图集 {img_size[0]}x{img_size[1]}, "
          f"region {len(names)} 个, 旋转 {rots or '无'}")
    if missing:
        print(f"  ⚠ {len(missing)} 个 region 未被 skel 引用：{missing[:5]}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("ids", nargs="+", help="装扮 id，例如 312801")
    ap.add_argument("--name", help="写入 display.txt 的显示名（仅单 id 时可用）")
    args = ap.parse_args()

    if args.name and len(args.ids) > 1:
        sys.exit("--name 只能配合单个 id 使用")

    for pid in args.ids:
        out_dir = os.path.normpath(os.path.join(ROOT, pid))
        os.makedirs(out_dir, exist_ok=True)
        print(f"[{pid}] → {out_dir}")

        ext_paths = {}
        for ext in ("skel", "atlas", "webp"):
            path = os.path.join(out_dir, f"{pid}.{ext}")
            if not download(BASE.format(id=pid, ext=ext), path):
                sys.exit(f"  下载 {ext} 失败")
            ext_paths[ext] = path

        size = convert(ext_paths["webp"], os.path.join(out_dir, f"{pid}.png"), ext_paths["atlas"])

        if args.name:
            with open(os.path.join(out_dir, "display.txt"), "w", encoding="utf-8") as f:
                f.write(args.name)

        report(pid, size, ext_paths["atlas"], ext_paths["skel"])

    print("\n完成。打开程序 → 设置 → 皮肤 → 刷新列表 即可看到。")


if __name__ == "__main__":
    main()
