# Pack Opening Portfolio Demo: Workflow Plan
Reference: Toon Blast "Toon Cards" pack claim (see docs/00_reference_breakdown.md)

**Goal:** a short, polished, recordable demo of the pack-opening moment, built from scratch. The portfolio piece is the **TA work** (VFX, shaders, animation, optimization) and the **AI-assisted pipeline** you used to get there fast.

**Scope:** reward tile -> pack fly-in -> open (2 variants) -> card reveal -> NEW badge / duplicate-to-stars -> fly to Collection tab. **Out:** album, economy, sets, save, analytics, localization.

> Assumption: Unity + C#, mobile portrait. Tell me if different.

---

## 1. Division of labour

**You (manual TA, this is what the portfolio shows):**
- Pack fly-in path + trail
- Open variant A: light burst (2-card green pack)
- Open variant B: foil tear + drop + fire + staggered card backs (3-card blue pack)
- Card flip (edge-on swap) + rarity glow shader
- NEW badge pop, "Tap to Claim" pulse, star row
- Card fly-to-tab with trail + tab bounce; duplicate -> star x3
- Timing/easing tuning against the ref, profiling and optimization pass

**AI (everything else):**
- Project skeleton, `CLAUDE.md`, folder structure
- Mock data (a dozen cards, 2 pack types) and a tiny `PackRoller`
- Sequence **state machine** in C#, exposing named hooks/events that you animate against
- Placeholder UI (track screen, Collection tab, scrim) so you can work in context
- First-pass art: card frames, card faces, pack wrappers, stars, badges (you clean up)
- Tween/helper utilities, debug panel (open green / open blue / force duplicate / replay)
- Capture helper (record clip), perf report summary, README/case-study draft

## 2. Repo layout
```
PackOpenDemo/
  CLAUDE.md
  docs/00_reference_breakdown.md   # done
  docs/ta_checklist.md             # your tasks + done criteria
  Assets/
    Scripts/PackOpen/   # AI: state machine, data, roller, debug panel
    Art/Generated/      # AI raw output (+ manifest of prompts/seeds)
    Art/Final/          # yours; AI never writes here
    VFX/ Shaders/ Anim/ Prefabs/   # yours
  Captures/             # demo clips, before/after
```

## 3. Phases (about 2-3 weeks part-time)

### Phase 1: Foundation (Day 1-2, AI)
- Unity project, `CLAUDE.md`, folders, placeholder track screen with two pack tiles + bottom nav
- `PackOpenSequence` state machine with hooks:
  `OnTapPack -> OnPackFlyIn -> OnPackOpen(variant) -> OnCardsSpawn -> OnReveal -> OnNewBadge -> OnTapClaim -> OnFlyToTab -> OnDuplicateStars -> OnComplete`
- Mock data: 12 cards, rarities 1-3, green=2 cards, blue=3, duplicate rule (stars = rarity)
- Debug panel
- **Done when:** both packs "open" end-to-end with placeholder boxes and logged events.

### Phase 2: Art first pass (Day 2-4, AI + you)
- AI generates style sheet then assets into `Art/Generated/`; you pick, clean, move to `Art/Final/`
- Need list: pack wrappers (green, blue, torn strip), card back, card frame x3 rarities, ~12 faces, star, NEW badge, glow sprites
- **Done when:** every placeholder replaced with final or approved art.

### Phase 3: TA hero work (Day 4-12, you)
Order of effort, each with the AI helping on boilerplate only:
1. Fly-in + trail + scrim
2. Variant A burst
3. Variant B tear/drop/fire/stagger
4. Flip + rarity glow shader
5. NEW pop + Tap to Claim pulse
6. Fly-to-tab + tab bounce, duplicate-to-stars
- Compare side-by-side with the gif; log timing tweaks in `ta_checklist.md`.
- **Done when:** side-by-side capture reads equivalent to the ref.

### Phase 4: Optimize + capture (Day 12-15)
- Profile on a device (draw calls, overdraw, particle count, GC); AI summarizes profiler exports
- Record final clips: full flow, slow-mo breakdown, VFX-only, before/after optimization
- **Done when:** stable 60fps on target device and clips exported.

### Phase 5: Portfolio packaging (Day 15-17, AI drafts, you edit)
- Case study: goal, reference analysis, breakdown of techniques, AI workflow, what you built manually, metrics
- Add VFX breakdown GIFs and the ref-vs-yours comparison

## 4. How to run the AI side
- Claude Code in the repo, one small task per prompt, each with a "done when".
- AI proposes hook/slot names in `CLAUDE.md`; you animate against them, so the contract is stable.
- Manifest every generated asset (prompt, seed) for reproducibility.
- Keep the AI vs manual split visible in the case study; it is part of the story.

## 5. Risks
| Risk | Mitigation |
|---|---|
| Generated art style drift | Lock style sheet and seed policy first |
| Looks like a copy of Toon Blast | Use your own characters/theme/names; keep only the *mechanic and timing* |
| TA time sink | Variant A first; B is stretch if time is short |
| Perf on device | Profile early in Phase 3, not only at the end |

## 6. Next actions
1. Confirm engine (Unity?) and target device/orientation.
2. Pick your own theme (the reference uses cartoon-brand art, so the portfolio version should be original).
3. I generate `CLAUDE.md` + Phase 1 skeleton.


> Update: theme is "Pocket Fantasy", engine Unity 6 URP, tweens via DOTween. Implemented scaffold lives in `PackOpenDemo/`; see `docs/ta_checklist.md`.
