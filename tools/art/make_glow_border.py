#!/usr/bin/env python3
"""High-res soft card glow for additive UI particles (glow_border20.png).

Soft halo in a 2:3 rounded proportion — no hard rim or obvious card outline.
Extra padding at the top so billboards aligned to cards do not clip the glow.
Run: python3 tools/art/make_glow_border.py
"""
import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = os.path.join(
    os.path.dirname(__file__), "..", "..", "PackOpenDemo", "Assets", "VFX", "Textures", "glow_border20.png"
)
SIZE = 512


def rounded_card_mask(size: int, card_w: int, card_h: int, radius: int, cx: int, cy: int) -> Image.Image:
    x0, y0 = cx - card_w // 2, cy - card_h // 2
    x1, y1 = cx + card_w // 2, cy + card_h // 2
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).rounded_rectangle([x0, y0, x1, y1], radius=radius, fill=255)
    return m


def blur_alpha(im: Image.Image, r: float) -> Image.Image:
    if r < 0.5:
        return im
    return im.filter(ImageFilter.GaussianBlur(r))


def build_glow(size: int = SIZE) -> Image.Image:
    # Smaller footprint + centre shifted down → transparent margin at top (avoids clipping on card).
    card_w = int(size * 0.42)
    card_h = int(card_w * 1.5)
    radius = int(size * 0.10)
    cx = size // 2
    cy = int(size * 0.56)

    sil = rounded_card_mask(size, card_w, card_h, radius, cx, cy)

    # Only soft blurs — no rim/edge subtract (that reads as a hard card stroke).
    body = blur_alpha(sil, size * 0.12)
    halo = blur_alpha(sil, size * 0.26)

    body = body.point(lambda a: int((a / 255.0) ** 1.55 * 140))
    halo = halo.point(lambda a: int((a / 255.0) ** 2.1 * 55))
    alpha = ImageChops.lighter(body, halo)

    # Extra fade near texture top so particles never lose the crown of the glow.
    top_pad = int(size * 0.18)
    fade = Image.new("L", (size, size), 255)
    px = fade.load()
    for y in range(top_pad):
        t = (y + 1) / (top_pad + 1)
        v = int(255 * (t ** 2.2))
        for x in range(size):
            px[x, y] = min(px[x, y], v)
    alpha = ImageChops.multiply(alpha, fade)

    rgb = Image.new("RGB", (size, size), (255, 248, 230))
    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    return out


def main():
    img = build_glow()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT)
    print(f"Wrote {OUT} ({img.size[0]}x{img.size[1]})")


if __name__ == "__main__":
    main()