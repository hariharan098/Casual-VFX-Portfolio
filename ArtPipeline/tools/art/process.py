#!/usr/bin/env python3
"""Raw -> game-ready textures.

- removes the magenta background (flood from the edges, soft edge)
- crops to subject, fits to canvas
- stickers: adds a white die-cut border (alpha dilation), keeps transparency
- card_back: baked into a 512x720 rounded card
- outputs output/processed/<id>.png (+ rarity/star utility sprites)
"""
import json
from collections import deque
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops

ROOT = Path(__file__).resolve().parents[2]  # ArtPipeline/
SPEC = json.loads((ROOT / "tools/art/assets.json").read_text())
RAW = ROOT / "output/raw"
OUT = ROOT / "output/processed"
CARD_W, CARD_H = 512, 720
TOL = 90  # magenta distance tolerance


def key_out(im):
    im = im.convert("RGB")
    w, h = im.size
    px = im.load()

    def is_bg(c):
        return abs(c[0] - 255) + c[1] + abs(c[2] - 255) < TOL * 2

    seen = bytearray(w * h)
    q = deque()
    for x in range(w):
        q.append((x, 0)); q.append((x, h - 1))
    for y in range(h):
        q.append((0, y)); q.append((w - 1, y))
    mask = Image.new("L", (w, h), 255)
    mp = mask.load()
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y * w + x]:
            continue
        seen[y * w + x] = 1
        if not is_bg(px[x, y]):
            continue
        mp[x, y] = 0
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    mask = mask.filter(ImageFilter.GaussianBlur(1.2))
    rgba = im.convert("RGBA")
    rgba.putalpha(mask)
    return rgba


def fit(im, w, h, pad):
    bbox = im.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    if bbox:
        im = im.crop(bbox)
    s = min((w - 2 * pad) / im.width, (h - 2 * pad) / im.height)
    im = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    canvas.paste(im, ((w - im.width) // 2, (h - im.height) // 2), im)
    return canvas


def die_cut(im, border=22):
    a = im.getchannel("A").point(lambda v: 255 if v > 8 else 0)
    grown = a.filter(ImageFilter.MaxFilter(border * 2 + 1)).filter(ImageFilter.GaussianBlur(1.5))
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    white = Image.new("RGBA", im.size, (255, 255, 255, 255))
    out.paste(white, (0, 0), grown)
    out.alpha_composite(im)
    return out


def rounded_card(im, base=(30, 30, 60, 255)):
    card = Image.new("RGBA", (CARD_W, CARD_H), (0, 0, 0, 0))
    m = Image.new("L", (CARD_W, CARD_H), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, CARD_W - 1, CARD_H - 1], 36, fill=255)
    card.paste(Image.new("RGBA", card.size, base), (0, 0), m)
    inner = fit(im, CARD_W, CARD_H, 90)
    card.alpha_composite(inner)
    card.putalpha(ImageChops.multiply(card.getchannel("A"), m))
    return card


def star(size=128):
    import math
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c, r1, r2 = size / 2, size * .46, size * .2
    pts = [(c + (r1 if i % 2 == 0 else r2) * math.sin(i * math.pi / 5),
            c - (r1 if i % 2 == 0 else r2) * math.cos(i * math.pi / 5)) for i in range(10)]
    d.polygon(pts, fill=(255, 205, 40, 255), outline=(180, 110, 0, 255))
    return im


def new_badge():
    im = Image.new("RGBA", (192, 96), (0, 0, 0, 0))
    ImageDraw.Draw(im).rounded_rectangle([4, 4, 187, 91], 40, fill=(235, 40, 60, 255), outline=(255, 255, 255, 255), width=6)
    return im  # text is added in-engine (TMP) so it can be localized/animated


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for s in SPEC["stickers"]:
        src = RAW / f'{s["id"]}.png'
        if not src.exists():
            print("missing", src.name); continue
        fit(die_cut(fit(key_out(Image.open(src)), 1024, 1024, 60)), CARD_W, CARD_W, 8) \
            .save(OUT / f'{s["id"]}.png')
        print("ok", s["id"])
    for o in SPEC["other"]:
        src = RAW / f'{o["id"]}.png'
        if not src.exists():
            print("missing", src.name); continue
        im = key_out(Image.open(src))
        if o["kind"] == "back":
            rounded_card(im).save(OUT / f'{o["id"]}.png')
        elif o["kind"] == "pack":
            fit(im, 512, 900, 10).save(OUT / f'{o["id"]}.png')
        else:
            fit(im, 1024, 128, 4).save(OUT / f'{o["id"]}.png')
        print("ok", o["id"])
    star().save(OUT / "star.png")
    new_badge().save(OUT / "new_badge.png")


if __name__ == "__main__":
    main()
