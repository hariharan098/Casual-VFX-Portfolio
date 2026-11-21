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
    public class PackAppearStage : PackOpenStageBase
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
- Data: `ctx.Results[i].Card` (name, rarity, face sprite), `ctx.Pack` (wrapper, tearStrip, cardBack sprites).
- Anchors: `ctx.PackOrigin` (tapped tile), `ctx.Anchors.packCenter / packExit / cardSlots / walletTarget`.
- Need a tap in a stage? `yield return ctx.WaitForTap();` (not used in the default flow).
- Other listeners (audio, UI): subscribe to `PackOpenSequence.StageStarted / StageFinished / Completed`.

## Pack tear rig (done, tune to taste)
- Art: `Assets/Art/Final/Pack/` (`pack_blue_full`, `pack_blue_open`, `pack_blue_strip`), rebuilt with `python3 tools/art/make_pack_blue.py`. All three share one crop, so the swap is seamless.
- Shader: `Assets/Shaders/PackVertexAnim.shader` (`PackOpen/UI/PackVertexAnim`). Properties: `_Skew`, `_SkewY`, `_BendX`, `_BendY`, `_WaveAmpX/Y`, `_WaveFreq`, `_WavePhase`, `_PivotX/Y`. Offsets are in the Image's local pixels. Needs `UIMeshSubdivide` on the Image for bend and wave. Keep these sprites out of atlases.
- Stages: `PackAppearStage`, `PackTearStage` (DOTween) share a `PackRig`. Every duration, angle and ease is an Inspector field; `PackTearStage.onTear` is the hook for your tear VFX.
- Scene: `PackOpen > Setup Pack Rig` adds the rig and wires the two stages (safe to re-run, does not rebuild the scene).
- DOTween: `PackOpen.Stages` references `DOTween.dll` directly (asmdef `precompiledReferences`). Skip DOTween's "Create ASMDEF" button, it would conflict. Only core DOTween is used (no UI module extensions).

## Flow (order = play order)
Tap tile -> PackAppear -> PackTear -> PackMoveDown -> CardsMoveOut -> CardsFlip -> Hold -> FlyToWallet.
- [x] PackAppear: pack enters the screen (first pass, DOTween). TODO: trail, scrim in
- [x] PackTear: wobble + squash + shader skew, swap, strip peel (first pass). TODO: tear VFX, timing vs reference
- [ ] PackMoveDown: pack drops to `packExit`
- [ ] CardsMoveOut: card backs come out one by one into `cardSlots` (stagger ~0.1s)
- [ ] CardsFlip: flip via edge-on swap + rarity glow shader
- [ ] Hold: short delay (placeholder default 0.8s; tweak on the `Hold` stage object)
- [ ] FlyToWallet: staggered fly to `walletTarget`, trail, wallet bounce
- [ ] Pack wrapper shader (quality pass)
- [ ] Perf pass on device (draw calls, overdraw, particles, GC)

## Testing
- Play `Assets/Scenes/PackOpenDemo.unity` in a 9:16 Game view. Tap a pack tile, or use the debug panel (backquote toggles it).
- Debug panel: Open Pack, Replay last (same cards, handy for tuning timing), Skip.
- Play Mode tests: Window > General > Test Runner > PlayMode (checks the 7 stages fire in order with placeholders).

Timing notes vs the reference (`docs/00_reference_breakdown.md`): log tweaks here.
