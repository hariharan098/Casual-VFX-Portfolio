#!/usr/bin/env python3
"""Generate raw art for every entry in assets.json.

Usage:
  python3 tools/art/generate.py --provider placeholder          # offline, runs the pipeline
  OPENAI_API_KEY=... python3 tools/art/generate.py --provider openai
  options: --only c01,c05   --force

Raw output: output/raw/<id>.png
Every run appends to output/manifest.json (prompt, seed, provider, model) so assets are reproducible.
"""
import argparse, base64, json, os, sys, time, urllib.request, hashlib
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]  # ArtPipeline/
SPEC = json.loads((ROOT / "tools/art/assets.json").read_text())
RAW = ROOT / "output/raw"
MANIFEST = ROOT / "output/manifest.json"


def entries():
    for s in SPEC["stickers"]:
        yield s["id"], f'{SPEC["style"]}. Subject: {s["subject"]}.', "sticker"
    for o in SPEC["other"]:
        yield o["id"], f'{SPEC["style"]}. Subject: {o["subject"]}.', o["kind"]


# ---------- providers ----------
def placeholder(prompt, seed, kind, size):
    """Procedural stand-in so downstream steps can be tested without an API."""
    h = int(hashlib.md5(str(seed).encode()).hexdigest(), 16)
    col = ((h >> 8) % 200 + 40, (h >> 16) % 200 + 40, (h >> 24) % 200 + 40)
    im = Image.new("RGB", (size, size), (255, 0, 255))
    d = ImageDraw.Draw(im)
    m = size // 6
    if kind == "pack":
        d.rounded_rectangle([m * 1.6, m * 0.4, size - m * 1.6, size - m * 0.4], 40, fill=col)
    elif kind == "strip":
        d.rectangle([m * 0.5, size * 0.45, size - m * 0.5, size * 0.55], fill=col)
    else:
        d.ellipse([m, m, size - m, size - m], fill=col)
        d.ellipse([size * .35, size * .38, size * .43, size * .46], fill=(255, 255, 255))
        d.ellipse([size * .57, size * .38, size * .65, size * .46], fill=(255, 255, 255))
    return im


def openai_provider(prompt, seed, kind, size):
    key = os.environ.get("OPENAI_API_KEY") or sys.exit("OPENAI_API_KEY not set")
    model = os.environ.get("OPENAI_IMAGE_MODEL", "gpt-image-1")
    body = {"model": model, "prompt": prompt + " Avoid: " + SPEC["negative"], "size": "1024x1024", "n": 1}
    req = urllib.request.Request(
        "https://api.openai.com/v1/images/generations", json.dumps(body).encode(),
        {"Authorization": f"Bearer {key}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=300) as r:
        data = json.load(r)
    from io import BytesIO
    return Image.open(BytesIO(base64.b64decode(data["data"][0]["b64_json"]))).convert("RGB")


PROVIDERS = {"placeholder": placeholder, "openai": openai_provider}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--provider", default="placeholder", choices=PROVIDERS)
    ap.add_argument("--only", default="")
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    only = set(filter(None, a.only.split(",")))
    RAW.mkdir(parents=True, exist_ok=True)
    log = json.loads(MANIFEST.read_text()) if MANIFEST.exists() else []
    for i, (id_, prompt, kind) in enumerate(entries()):
        if only and id_ not in only:
            continue
        out = RAW / f"{id_}.png"
        if out.exists() and not a.force:
            print("skip", id_); continue
        seed = SPEC["seed_base"] + i
        print("gen ", id_, a.provider)
        PROVIDERS[a.provider](prompt, seed, kind, SPEC["size"]).save(out)
        log.append({"id": id_, "provider": a.provider, "seed": seed, "prompt": prompt,
                    "time": time.strftime("%Y-%m-%d %H:%M:%S")})
    MANIFEST.write_text(json.dumps(log, indent=2))


if __name__ == "__main__":
    main()
