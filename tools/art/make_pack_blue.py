#!/usr/bin/env python3
"""Build the final blue pack art set from the generated (keyed) pack.

Input : PackOpenDemo/Assets/Art/Generated/pack_blue.png  (transparent, 1024x1536)
Output: PackOpenDemo/Assets/Art/Final/Pack/
  pack_blue_full.png   full wrapper, cropped to the pack
  pack_blue_open.png   body after the tear: jagged torn top, white foil lip, card stack peeking out
  pack_blue_strip.png  the torn-off top crimp strip (jagged bottom edge matches the body exactly)

The three are cut with the SAME jagged line and the SAME crop origin, so swapping
full -> open + strip at the tear moment is seamless. Offsets are printed (and written to
pack_blue_layout.json) so the Unity rig can place them.
"""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2] / "PackOpenDemo"
SRC = ROOT / "Assets/Art/Generated/pack_blue.png"
OUT = ROOT / "Assets/Art/Final/Pack"

CROP = (80, 64, 940, 1460)     # whole pack (x0, y0, x1, y1) in source pixels
CUT_Y = 200                    # base height of the tear, just under the ribbed foil band
LIP = 7                        # px of exposed white foil backing along the tear
STACK_TOP = CUT_Y - 36         # top of the card stack peeking out of the opened pack
FOIL = np.array([236, 241, 250], dtype=np.float32)


def jagged_cut(width):
    rng = np.random.default_rng(7)
    step = 22
    kx = np.arange(0, width + step, step)
    offs = np.array([(-1) ** i * rng.uniform(4, 11) for i in range(len(kx))])
    c = CUT_Y + np.interp(np.arange(width), kx, offs)
    return c + rng.normal(0, 0.8, width)


def card_stack(size):
    """Red card back with white border, drawn as if seen from the open top of the pack."""
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x0, x1 = 128, 896
    top = STACK_TOP
    d.rounded_rectangle([x0 - 3, top - 3, x1 + 3, top + 220], 14, fill=(18, 22, 70, 255))      # outline
    d.rounded_rectangle([x0, top, x1, top + 220], 12, fill=(246, 246, 250, 255))               # white card border
    red = Image.new("RGBA", (x1 - x0 - 20, 200), (0, 0, 0, 0))
    rd = ImageDraw.Draw(red)
    for y in range(red.height):
        t = y / red.height
        rd.line([(0, y), (red.width, y)], fill=(int(232 - 60 * t), int(52 - 24 * t), int(62 - 20 * t), 255))
    mask = Image.new("L", red.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, red.width, red.height], 8, fill=255)
    layer.paste(red, (x0 + 10, top + 10), mask)
    d.line([(x0 + 18, top + 14), (x1 - 18, top + 14)], fill=(255, 150, 150, 255), width=3)   # top highlight
    return layer


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    src = Image.open(SRC).convert("RGBA")
    w, h = src.size
    a = np.asarray(src).astype(np.float32)
    yy = np.arange(h)[:, None].astype(np.float32)
    cut = jagged_cut(w)[None, :]
    d = yy - cut                                  # >0 below the tear (body), <0 above (strip)

    def piece(sign):
        cov = np.clip(sign * d + 0.5, 0, 1)       # antialiased coverage of this side
        lipk = np.clip(1 - (sign * d) / LIP, 0, 1) * (sign * d >= -0.5) * 0.85
        rgb = a[..., :3] * (1 - lipk[..., None]) + FOIL * lipk[..., None]
        out = np.dstack([rgb, a[..., 3] * cov])
        return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))

    body = piece(+1)
    strip = piece(-1)
    stack = card_stack(src.size)
    opened = Image.alpha_composite(stack, body)

    x0, y0, x1, y1 = CROP
    cut_max = int(np.ceil(cut.max())) + 3
    body_top = STACK_TOP - 4
    full = src.crop(CROP)
    open_img = opened.crop((x0, body_top, x1, y1))
    strip_img = strip.crop((x0, y0, x1, cut_max))
    full.save(OUT / "pack_blue_full.png")
    open_img.save(OUT / "pack_blue_open.png")
    strip_img.save(OUT / "pack_blue_strip.png")

    layout = {
        "fullSize": [full.width, full.height],
        "openTopOffset": body_top - y0, "openSize": list(open_img.size),
        "stripTopOffset": 0, "stripSize": list(strip_img.size),
    }
    (OUT / "pack_blue_layout.json").write_text(json.dumps(layout, indent=2))
    print(layout)


if __name__ == "__main__":
    main()
