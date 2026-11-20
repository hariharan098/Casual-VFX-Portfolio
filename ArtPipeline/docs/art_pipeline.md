# Art pipeline (Sticker Swap)
1. Edit `tools/art/assets.json` (style, subjects, rarity).
2. `OPENAI_API_KEY=... python3 tools/art/generate.py --provider openai --force`   (offline test: `--provider placeholder`)
   - subset: `--only c01,c09`; every run is logged in `Assets/Art/Generated/manifest.json`
3. `python3 tools/art/process.py`  -> keys out magenta bg, adds die-cut white border, builds card back, pack, strip, star, NEW badge into `Assets/Art/Generated/processed/`
4. Unity auto-imports as sprites (`GeneratedArtImporter`). Menu: PackOpen > Reimport Generated Art.
5. You review, retouch and move keepers to `Assets/Art/Final/`. Scripts never write to Final.
Currently contains PLACEHOLDER art from the offline provider; regenerate with --force.
