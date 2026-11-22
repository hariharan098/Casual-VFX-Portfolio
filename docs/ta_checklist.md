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
- [x] PackMoveDown: hop then drop to `packExit` (first pass, DOTween)
- [x] CardsMoveOut: face-down cards pop from behind the pack into `cardSlots`, overlapping (stagger 0.18s, travel 0.55s); pack sinks. TODO: opening fire/glow VFX
- [x] CardsFlip: staggered edge-on flip, Back/Front toggled at the midpoint. TODO: rarity glow shader
- [x] Hold: `HoldStage`, default 0.8s
- [x] FlyToWallet: one by one, pull-back + curve + shrink + fade, wallet punch. TODO: trail VFX
- [ ] Pack wrapper shader (quality pass)
- [ ] Perf pass on device (draw calls, overdraw, particles, GC)

## Scene layout
`PackOpen > Clean Scene` (also run at the end of Setup Project; safe to rerun, keeps your edits):
- `Canvas > Scrim` (dark, black at 0.75 alpha) / `Cards` / `PackRig` / `Anchors` (incl. empty `WalletTarget`) / `TapLayer` (full-screen tap area, `TapToOpen`) > `Label` ('Tap to Open', bottom).
- The tap area is the whole screen. The label hides during a run and pulses again when it finishes; tap again for a new pack.
- Debug panel starts hidden, backquote toggles it.

## Prefabs
Run `PackOpen > Setup Pack Rig` once; it creates (never overwrites) these and wires all stages:
- `Assets/Prefabs/Card.prefab`: `Front` (ArtMask with Mask > Art, shared Frame, Stars, Name) and `Back`. `CardView.Bind/ShowBack/ShowFront`. All cards share one silhouette (`Art/Final/Card/card_mask.png`), so any art sprite fits. Card art = `CardData.face` (illustration + baked background, regenerate with `tools/art/make_card_art.py`).
- `Assets/Prefabs/Pack_Blue.prefab`: the PackRig hierarchy. Swap `PackData.packFull / packOpen / packStrip` to reskin it (the layout stays).
- `CardsRig` (scene object `Cards`, behind the pack in sibling order) holds the Card prefab reference and the spawned cards.
- Tuning fields live on each stage component (stagger, travel time, eases, sink, flip time, fly time).

## Testing
- Play `Assets/Scenes/PackOpenDemo.unity` in a 9:16 Game view. Tap a pack tile, or use the debug panel (backquote toggles it).
- Debug panel: Open Pack, Replay last (same cards, handy for tuning timing), Skip.
- Play Mode tests: Window > General > Test Runner > PlayMode (checks the 7 stages fire in order with placeholders).

Timing notes vs the reference (`docs/00_reference_breakdown.md`): log tweaks here.
