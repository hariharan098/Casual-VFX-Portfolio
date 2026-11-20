#!/usr/bin/env python3
"""Turn the flat magenta background of generated art into real transparency.

Usage: python3 tools/art/remove_magenta_bg.py [files...]   (default: every PNG in Assets/Art/Generated)
- Originals are copied once to PackOpenDemo/ArtSource/Raw (outside Assets) and always used as input,
  so the script is safe to re-run.
- Background = magenta connected to the image border (so pink inside the art is kept) plus any
  near-pure magenta pocket (holes in handles etc). Edges are feathered and de-spilled.
- Images whose corner is not magenta (e.g. card_back) are skipped.
"""
import sys, shutil
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2] / "PackOpenDemo"
GEN = ROOT / "Assets/Art/Generated"
RAW = ROOT / "ArtSource/Raw"


def is_magenta(p):
    return p[0] > 170 and p[2] > 170 and p[1] < 110


def process(name):
    src = RAW / name
    if not src.exists():
        RAW.mkdir(parents=True, exist_ok=True)
        shutil.copy(GEN / name, src)
    im = Image.open(src).convert("RGB")
    if not is_magenta(im.getpixel((3, 3))):
        shutil.copy(src, GEN / name)
        return "skipped (no magenta bg)"
    a = np.asarray(im).astype(np.int16)
    R, G, B = a[..., 0], a[..., 1], a[..., 2]
    loose = ((R > 170) & (B > 170) & (G < 110)).astype(np.uint8) * 255
    flood = Image.fromarray(loose, "L")
    h, w = loose.shape
    seeds = [(x, y) for x in range(0, w, 8) for y in (0, h - 1)] + [(x, y) for y in range(0, h, 8) for x in (0, w - 1)]
    for s in seeds:
        if flood.getpixel(s) == 255:
            ImageDraw.floodfill(flood, s, 128)
    bg = (np.asarray(flood) == 128) | ((R > 225) & (B > 225) & (G < 30))
    bg_img = Image.fromarray(bg.astype(np.uint8) * 255, "L")
    near = np.asarray(bg_img.filter(ImageFilter.MaxFilter(5))) > 0
    band = near & ~bg
    m = np.clip((np.minimum(R, B) - G - 60) / 150.0, 0, 1)  # how magenta an edge pixel is
    alpha = np.where(bg, 0.0, np.where(band, 1.0 - m, 1.0))
    spill = np.where(band, np.maximum(0, np.minimum(R, B) - G) * 0.9, 0)
    out = np.dstack([np.clip(R - spill, 0, 255), G, np.clip(B - spill, 0, 255), alpha * 255]).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(GEN / name)
    return f"ok ({bg.mean() * 100:.0f}% transparent)"


if __name__ == "__main__":
    names = [Path(f).name for f in sys.argv[1:]] or sorted(p.name for p in GEN.glob("*.png"))
    for n in names:
        print(n, process(n))
