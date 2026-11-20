# Setup notes
- Unity 6000.3.21f1, URP, portrait. Project: `PackOpenDemo/`.
- Open `Assets/Scenes/PackOpenDemo.unity`. Re-run `PackOpen > Setup Project` only to rebuild the scene (it overwrites it).
- Art: `Assets/Art/Generated` (AI, transparent PNGs) -> you retouch -> `Assets/Art/Final` (AI never writes there).
- Generated images come with a magenta background; `python3 tools/art/remove_magenta_bg.py` keys it out. Raw originals live in `PackOpenDemo/ArtSource/Raw` (outside Assets).
- Folders you own and AI leaves empty: `Shaders`, `VFX`, `Anim`, `Prefabs`, `Materials`, `Art/Final`.
- See `docs/ta_checklist.md` for the hook contract.
