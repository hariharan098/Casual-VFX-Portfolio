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

## Camera and VFX scale
- Camera orthographic size is 9.6 (set by `Clean Scene` only while it is still 5, so your own value survives reruns). The canvas is then 100 px per world unit, matching how the VFX were authored. Tune the size by eye: bigger = smaller VFX relative to the UI.
- `Clean Scene` works on the scene that is open (keeps unsaved edits) and does not save: press Cmd+S afterwards.
- Spawn new VFX under the Canvas and position them from the anchors (`PackOrigin`, `CardSlot_i`, `PackCenter`, `WalletTarget`), never with hardcoded world units.
- Particle systems with Scaling Mode = Local ignore parent scale; switch to Hierarchy before scaling a VFX parent.
- Sorting: keep Canvas order 0 and give VFX renderers a higher order so they sit over the pack and cards.
- Replays: stop or destroy VFX started from `onTear`, `onCardRevealed`, `onHoldStart`, `onCardArrived` when a new run starts.
- `TapToOpen` reads `sequence.IsRunning`, so the label always returns; tick `skipOnTap` to let a tap skip a running sequence. The label uses a relative bottom anchor (y 0.115).

## Tear timing (PackTear), based on the reference frames 64-71
Lean (`wobbleTime` 0.45s, `wobbleAngle` 4 degrees clockwise, tiny squash/skew) -> swap -> slow peel from the right corner (`ripTime` 0.6s: strip rotates and curls, force builds with `InQuad`, the pack is pulled `pullAngle` 7 degrees the other way) -> throw up and to the left off screen (`liftTime` 0.4s, `InCubic`, to `flyOffset`) while the pack recoils to upright (`recoilTime` 0.45s). The strip hinge is on the left (`PackRig.stripPivotX` = 0).
- Tear VFX starts at the top-right corner (`tearOffset`), follows the strip while it peels, and stays at the pack's top edge once thrown (`vfxDetachOnThrow`). `vfxStopTime` controls when it stops emitting; `vfxLingerTime` (0.25s, about 2-3 reference frames) keeps the remaining sparks visible after that, then fades them out and switches the VFX off.
- `Setup Pack Rig` applies this preset once to older scenes (`tuningVersion` 2), then never overwrites your tuning. Setting `tuningVersion` back to 0 re-applies it.
- `Setup Pack Rig` works on the open scene and does not save: press Cmd+S afterwards.

## Pack VFX hookup
`PackOpen > Setup Pack Rig` creates `Canvas/VFX` (right after `PackRig`, under `TapLayer`) with scene instances of `VFX/Prefabs/Final/VFX_CardPack_Idle` and `VFX_CardPack_Fast`, each with a `UiVfx` component, and wires them to the stages.
- Idle: starts when `PackAppear` starts, follows the pack position, winds down `idleStopLead` before the tear.
- Fast: starts at the tear moment, follows the flying strip, pops in (scale), fades near the end, then stops.
- Tune on the `PackAppear` / `PackTear` components (`idleOffset`, `tearOffset`, pop, fade) and on each `UiVfx` (`scaleMultiplier`). Not children of the pack on purpose: the pack's scale and squash would distort the particles.
- `UiVfx` drives the standard ParticleSystem components, so `ZParticleSystem` (Assembly-CSharp) is not referenced from the asmdefs. Skip hides both immediately.

## Prefabs
Run `PackOpen > Setup Pack Rig` once; it creates (never overwrites) these and wires all stages:
- `Assets/Prefabs/Card.prefab`: `Front` / `Back` art plus nested `VFX_Card_Burst` (`VFX_Back`) and `VFX_CardPack_Idle` (`VFX_Front`). **Sorting:** each uses a nested `Canvas` with `overrideSorting` — `CardView.SortBackVfx` (0), `SortCardArt` (10), `SortFrontVfx` (20). All cards share the same three orders so every back/burst layer draws under every card face (UI "layer", not a second camera). `EnforceCanvasLayers()` runs from setup and spawn. Idle glow *behind* the frame: parent idle under `VFX_Back` so it gets sort 0; under `VFX_Front` it stays on top of art (sort 20). `CardView.PlayRevealVfx()` runs on the same frame as `ShowFront()` in `CardsFlipStage`; idle stays on through Hold until `FlyToWallet` calls `HideRevealVfx()`. `Setup Pack Rig` adds `UiVfx` and wires `burstVfx` / `revealIdleVfx` on the prefab.
- All cards share one silhouette (`Art/Final/Card/card_mask.png`), so any art sprite fits. Card art = `CardData.face` (illustration + baked background, regenerate with `tools/art/make_card_art.py`).
- `Assets/Prefabs/Pack_Blue.prefab`: the PackRig hierarchy. Swap `PackData.packFull / packOpen / packStrip` to reskin it (the layout stays).
- `CardsRig` (scene object `Cards`, behind the pack in sibling order) holds the Card prefab reference and the spawned cards.
- Tuning fields live on each stage component (stagger, travel time, eases, sink, flip time, fly time).

## Testing
- Play `Assets/Scenes/PackOpenDemo.unity` in a 9:16 Game view. Tap a pack tile, or use the debug panel (backquote toggles it).
- Debug panel: Open Pack, Replay last (same cards, handy for tuning timing), Skip.
- Play Mode tests: Window > General > Test Runner > PlayMode (checks the 7 stages fire in order with placeholders).

Timing notes vs the reference (`docs/00_reference_breakdown.md`): log tweaks here.

## Pack entry (PackAppearStage)
- Pack swings in from the right (`sideOffset` 700,-150 canvas px), tilted -50° and unwinding, overshoots the centre, then lands with a hop (`bounceHop`) and squash/stretch (`bounceSquash`).
- `flyTime` + `bounceTime` set the total (~0.85 s). `enterFromSide` off = old behaviour from the tapped tile.
- Preset is applied once by `PackOpen > Setup Pack Rig` (`tuningVersion`); later hand-tuning is kept. Save the scene afterward.
