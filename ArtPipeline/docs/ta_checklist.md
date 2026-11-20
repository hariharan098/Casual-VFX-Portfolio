# TA checklist (yours). Replace the PlaceholderStage on each slot with your own PackOpenStage.

Contract: `IEnumerator Play(PackOpenContext ctx)`; honor `ctx.Skip`. Variant in `ctx.Pack.OpenVariant`.

- [ ] PackFlyIn: curve path, trail, scrim in
- [ ] PackOpen / Burst (green, 2 cards): light rays + stars
- [ ] PackOpen / TearDrop (blue, 3 cards): foil strip tear, pack drop, fire
- [ ] CardsSpawn: stagger (~0.1s), card backs for blue
- [ ] Reveal: flip via edge-on swap + rarity glow shader
- [ ] NewBadge: pop with overshoot on `Results[i].IsNew`
- [ ] TapToClaim: pulse prompt, wait for tap
- [ ] FlyToTab: staggered bezier + trail, tab bounce
- [ ] DuplicateStars: star x `StarsFromDuplicate` flies up (runs only when a duplicate exists)
- [ ] Perf pass on device (draw calls, overdraw, particles, GC)
