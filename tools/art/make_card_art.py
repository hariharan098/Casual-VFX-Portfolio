#!/usr/bin/env python3
"""Builds the shared card art set in PackOpenDemo/Assets/Art/Final/Card.

  art_cXX.png            512x768 illustration on a baked-in background (no frame). Masked at runtime.
  card_frame_front.png   shared front frame (border + star plate), transparent opening
  card_back.png          card back (generated art) cut to the same rounded silhouette
  card_mask.png          white rounded rect = the common silhouette used by the Mask

All files share the same 512x768 canvas and corner radius so one prefab can mask any art.
Run: python3 tools/art/make_card_art.py
"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "PackOpenDemo", "Assets", "Art")
GEN = os.path.join(ROOT, "Generated")
OUT = os.path.join(ROOT, "Final", "Card")
W, H = 512, 768
R = 46          # outer corner radius
SS = 3          # supersampling for clean edges

# (top, bottom, glow) per card, picked to read well behind the subject
PALETTES = {
    "c01": ((92, 190, 120), (30, 110, 90), (255, 240, 170)),
    "c02": ((140, 90, 210), (50, 30, 120), (255, 170, 255)),
    "c03": ((120, 215, 110), (30, 130, 80), (255, 255, 160)),
    "c04": ((250, 190, 90), (190, 100, 40), (255, 250, 190)),
    "c05": ((70, 120, 230), (30, 40, 140), (200, 230, 255)),
    "c06": ((235, 150, 70), (130, 60, 40), (255, 230, 150)),
    "c07": ((90, 190, 230), (40, 100, 170), (255, 255, 220)),
    "c08": ((60, 70, 170), (20, 20, 90), (170, 200, 255)),
    "c09": ((255, 140, 70), (170, 40, 60), (255, 240, 150)),
    "c10": ((240, 100, 170), (110, 40, 130), (255, 210, 240)),
    "c11": ((255, 110, 60), (120, 20, 50), (255, 220, 140)),
    "c12": ((130, 200, 255), (160, 100, 230), (255, 255, 255)),
}


def rounded_mask(size, inset, radius, ss=SS):
    w, h = size
    m = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(m).rounded_rectangle(
        [inset * ss, inset * ss, (w - inset) * ss - 1, (h - inset) * ss - 1], radius * ss, fill=255)
    return m.resize((w, h), Image.LANCZOS)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def background(top, bottom, glow):
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        c = lerp(top, bottom, y / (H - 1))
        for x in range(W):
            px[x, y] = c
    # sunburst rays from the subject centre
    cx, cy = W / 2, H * 0.42
    rays = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(rays)
    n = 14
    for i in range(n):
        a0 = (i / n) * 2 * math.pi
        a1 = a0 + math.pi / n
        d.polygon([(cx, cy), (cx + 1200 * math.cos(a0), cy + 1200 * math.sin(a0)),
                   (cx + 1200 * math.cos(a1), cy + 1200 * math.sin(a1))], fill=38)
    rays = rays.filter(ImageFilter.GaussianBlur(3))
    img = Image.composite(Image.new("RGB", (W, H), glow), img, rays)
    # soft radial glow
    g = Image.new("L", (W, H), 0)
    ImageDraw.Draw(g).ellipse([cx - 230, cy - 230, cx + 230, cy + 230], fill=150)
    g = g.filter(ImageFilter.GaussianBlur(70))
    img = Image.composite(Image.new("RGB", (W, H), glow), img, g)
    # vignette
    v = Image.new("L", (W, H), 0)
    ImageDraw.Draw(v).ellipse([-120, -80, W + 120, H + 80], fill=255)
    v = v.filter(ImageFilter.GaussianBlur(90))
    dark = Image.new("RGB", (W, H), (0, 0, 0))
    return Image.composite(img, Image.blend(img, dark, 0.45), v)


def make_art(cid):
    top, bottom, glow = PALETTES[cid]
    bg = background(top, bottom, glow).convert("RGBA")
    ill = Image.open(os.path.join(GEN, f"card_{cid}.png")).convert("RGBA")
    ill = ill.crop(ill.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox())
    box_w, box_h = 400, 440
    s = min(box_w / ill.width, box_h / ill.height)
    ill = ill.resize((max(1, int(ill.width * s)), max(1, int(ill.height * s))), Image.LANCZOS)
    cx, cy = W // 2, int(H * 0.42)
    x, y = cx - ill.width // 2, cy - ill.height // 2
    # ground shadow
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse([cx - ill.width * 0.38, y + ill.height - 22, cx + ill.width * 0.38, y + ill.height + 18],
                               fill=(0, 0, 0, 110))
    bg.alpha_composite(sh.filter(ImageFilter.GaussianBlur(10)))
    bg.alpha_composite(ill, (x, y))
    return bg


def make_frame():
    big = (W * SS, H * SS)
    img = Image.new("RGBA", big, (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    S = SS
    # dark outer outline, light border, inner dark line
    d.rounded_rectangle([0, 0, W * S - 1, H * S - 1], R * S, fill=(25, 30, 60, 255))
    d.rounded_rectangle([5 * S, 5 * S, (W - 5) * S - 1, (H - 5) * S - 1], (R - 5) * S, fill=(246, 248, 255, 255))
    # border shading: light band with a cool-blue lower half
    shade = Image.new("RGBA", big, (0, 0, 0, 0))
    ImageDraw.Draw(shade).rounded_rectangle([5 * S, H * S // 2, (W - 5) * S - 1, (H - 5) * S - 1], (R - 5) * S,
                                            fill=(150, 175, 225, 150))
    img.alpha_composite(shade)
    d = ImageDraw.Draw(img)
    # opening (punched out) with a thin dark inner line
    inset = 22
    d.rounded_rectangle([(inset - 3) * S, (inset - 3) * S, (W - inset + 3) * S - 1, (H - inset + 3) * S - 1],
                        (R - inset + 3) * S, fill=(25, 30, 60, 255))
    hole = Image.new("L", big, 0)
    ImageDraw.Draw(hole).rounded_rectangle([inset * S, inset * S, (W - inset) * S - 1, (H - inset) * S - 1],
                                           (R - inset) * S, fill=255)
    # bottom plate for the stars (inside the opening)
    plate = Image.new("RGBA", big, (0, 0, 0, 0))
    pd = ImageDraw.Draw(plate)
    top_y = 628
    for yy in range(top_y * S, (H - inset) * S):
        t = (yy - top_y * S) / ((H - inset - top_y) * S)
        c = lerp((70, 110, 215), (80, 60, 170), t)
        pd.line([(0, yy), (W * S, yy)], fill=c + (255,))
    pd.rectangle([0, top_y * S - 3 * S, W * S, top_y * S], fill=(255, 255, 255, 120))
    plate.putalpha(Image.composite(plate.getchannel("A"), Image.new("L", big, 0), hole))
    # cut the opening from the frame, then lay the plate on top
    a = img.getchannel("A")
    a = Image.composite(Image.new("L", big, 0), a, hole)
    img.putalpha(a)
    img.alpha_composite(plate)
    return img.resize((W, H), Image.LANCZOS)


def make_back():
    src = Image.open(os.path.join(GEN, "card_back.png")).convert("RGBA").resize((W, H), Image.LANCZOS)
    m = rounded_mask((W, H), 0, R)
    src.putalpha(m)
    return src


def main():
    os.makedirs(OUT, exist_ok=True)
    for cid in PALETTES:
        art = make_art(cid)
        art.save(os.path.join(OUT, f"art_{cid}.png"))
        print("art", cid)
    make_frame().save(os.path.join(OUT, "card_frame_front.png"))
    make_back().save(os.path.join(OUT, "card_back.png"))
    mask = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    mask.putalpha(rounded_mask((W, H), 4, R - 4))
    mask.save(os.path.join(OUT, "card_mask.png"))
    # contact sheet for eyeballing
    sheet = Image.new("RGBA", (W * 4 // 2, H * 3 // 2), (40, 40, 60, 255))
    for i, cid in enumerate(PALETTES):
        a = Image.open(os.path.join(OUT, f"art_{cid}.png")).copy()
        a = Image.composite(a, Image.new("RGBA", a.size, (0, 0, 0, 0)), mask.getchannel("A"))
        a.alpha_composite(Image.open(os.path.join(OUT, "card_frame_front.png")))
        a = a.resize((W // 2, H // 2), Image.LANCZOS)
        sheet.alpha_composite(a, ((i % 4) * (W // 2), (i // 4) * (H // 2)))
    sheet.save("/tmp/card_sheet.png")
    print("done ->", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
