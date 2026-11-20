# TA checklist (yours)

The AI side owns the sequence, data and input. You own everything that moves or renders.
Nothing in `Scripts/PackOpen/Core`, `Data`, or `UI` needs to change for your work.

## How to add your own stage
1. Install DOTween (Asset Store or OpenUPM). Run `Tools > Demigiant > DOTween Utility Panel > Create ASMDEF`.
2. Open `Assets/Scripts/PackOpen/Stages/PackOpen.Stages.asmdef` and add `DOTween.Modules` (and `DOTween` if listed) to References.
3. Write a class deriving from `PackOpenStageBase`, then drop it on the stage object in the scene (`PackOpenSequence/Stages/<StageName>`) and assign it in the sequence's `Slots` list in place of the placeholder.

```csharp
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace PackOpen.Stages
{
    public class PackFlyInStage : PackOpenStageBase
    {
        [SerializeField] Transform pack; // your prefab instance
        public override IEnumerator Play(PackOpenContext ctx)
        {
            var from = ctx.PackOrigin;                 // tapped tile
            var to = ctx.Anchors.packCenter;
            pack.position = from.position;
            if (ctx.Skip) { pack.position = to.position; yield break; }
            var t = pack.DOMove(to.position, 0.5f).SetEase(Ease.OutBack);
            yield return t.WaitForCompletion();
        }
    }
}
```

## Contract
- `IEnumerator Play(PackOpenContext ctx)`; finish with `yield break` or by running out of steps.
- Honor `ctx.Skip`: jump to the end state and return.
- Variant: `ctx.Variant` (`Burst` green 2 cards, `TearDrop` blue 3 cards).
- Data: `ctx.Results[i].Card` (name, rarity, face sprite), `.IsNew`, `.StarsFromDuplicate`.
- Anchors: `ctx.PackOrigin` (tapped tile), `ctx.Anchors.packCenter / cardSlots / tabTarget / starTarget`.
- Wait for the player: `yield return ctx.WaitForTap();`
- `DuplicateStars` only runs when at least one duplicate exists (`ctx.HasDuplicate`).
- Other listeners (audio, UI): subscribe to `PackOpenSequence.StageStarted / StageFinished / Completed`.

## Stages (order = play order)
- [ ] PackFlyIn: curve path, trail, scrim in
- [ ] PackOpen: Burst (green) light rays and stars / TearDrop (blue) foil strip tear, drop, fire
- [ ] CardsSpawn: stagger (~0.1s), card backs for blue
- [ ] Reveal: flip via edge-on swap + rarity glow shader
- [ ] NewBadge: pop with overshoot where `Results[i].IsNew`
- [ ] TapToClaim: pulse prompt, then `ctx.WaitForTap()`
- [ ] FlyToTab: staggered bezier + trail, tab bounce
- [ ] DuplicateStars: star x `StarsFromDuplicate` flies to `starTarget`
- [ ] Pack wrapper shader (quality pass)
- [ ] Perf pass on device (draw calls, overdraw, particles, GC)

## Testing
- Play `Assets/Scenes/PackOpenDemo.unity` in a 9:16 Game view. Tap a pack tile, or use the debug panel (backquote toggles it).
- Debug panel: Open Green / Open Blue, Force duplicate, Replay last (same cards, handy for tuning timing), Reset collection, Skip.
- Play Mode tests: Window > General > Test Runner > PlayMode (checks hook order with placeholders).

Timing notes vs the reference (`docs/00_reference_breakdown.md`): log tweaks here.
