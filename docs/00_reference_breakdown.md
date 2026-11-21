# Reference Breakdown: Toon Blast "Toon Cards" pack claim (Valentine's Pass)
Source: Refs/Screen Recording 2026-11-20 at 3.48.13 PM.gif (133 frames @100ms, ~13.3s). Times are approximate (sampled every ~0.4s). Cut points to be refined with a full frame dump.

## Context screen: Event pass track
- Two-column reward track (left free, right premium-style), numbered nodes 11-14 down a center line, check marks on claimed rewards.
- Reward tiles: coins, **green Toon Cards pack (row 12 left, claimed)**, **blue Toon Cards pack (row 12 right, shows "Claim" tag)**, boosters, gloves.
- Bottom nav has a **Collection** tab (card icon), highlighted while cards fly to it.
- Locked reward tap -> tooltip "Earn badges in levels to get this reward!"

## Flow A: Green pack (2 cards)
| t (s) | State | Notes for TA |
|---|---|---|
| 0.0-0.3 | Tap on claimable pack tile | tile pressed |
| 0.3-1.0 | Scrim darkens track; pack scales up from tile and flies to screen center, sparkle trail | curve path + trail particles |
| ~1.3 | **Open burst**: light rays + star particles, pack gone, **2 cards** appear (Cutlery, Toaster) | cards face-up already (no flip in small pack) |
| 1.3-3.2 | Hold. Cards show rarity stars (3), **NEW** red badge on first-time card, glow on NEW. Text "Tap to Claim" | idle pulse on glow |
| 3.4 | Tap. Cards shrink/fly toward Collection tab | |
| 3.8 | Sparkle burst on Collection tab icon | tab bounce |
| 4.3 | Duplicate (Cutlery) converts to **star x3** that flies up, orange trail | duplicate -> stars economy |
| 4.7 | Scrim fades, track restored, tile now checked | |

## Flow B: Blue pack (3 cards, more elaborate)
| t (s) | State | Notes for TA |
|---|---|---|
| 6.0-6.4 | Blue pack flies in from tile, tilted, then settles upright center | rotation settle |
| 6.8 | **Top strip tears off** (blue foil strip flies away), sparkles | separate mesh/sprite for strip |
| 7.2 | Pack shakes/holds | anticipation |
| 7.7 | Pack drops down screen, **fire/glow** at opening, **card backs** (red, Toon Cards logo) pop up and stagger | stagger ~0.1s |
| 8.1-8.5 | 3 card backs lined up in a row, fire glow at pack opening, pack sinks off bottom | |
| 8.9 | **Flip midpoint**: cards edge-on (thin vertical line) | scaleX 1->0->1 flip, swap sprite at 0 |
| 9.4 | Faces revealed with white glow border: Television (2 stars), Blocks (2), Planets (1) | rarity -> star count/glow |
| 9.8 | **NEW** badges pop on all three, sparkle shower. "Tap to Claim" | badge pop with overshoot |
| 10.6 | Tap. Cards fly one by one (staggered) down to Collection tab, sparkle trails | bezier + trail |
| 11.1+ | Returns to track, tile checked. | |

## Reusable building blocks (what TA must build once)
1. Reward-pack fly-to-center (curve + trail)
2. Scrim in/out
3. Pack open variants: **burst** (small pack), **tear + drop + fire** (big pack)
4. Card back -> face **flip** with glow
5. Rarity visuals: star row, glow tint
6. **NEW** badge pop
7. "Tap to Claim" prompt (pulse)
8. Card fly-to-Collection-tab with trail + tab bounce/sparkle
9. Duplicate -> star conversion (star x N flying up)

## Data/logic implied
- Pack tiers: green = 2 cards, blue = 3 cards (card count differs; likely rarity weights differ)
- Cards have: name, set, rarity (1-3 stars), NEW flag (first copy)
- Duplicate = converted to stars (x3 for a 3-star card; x(stars)?) <- **confirm**
- Collection tab is the destination; badge/notification likely on tab (not visible)

## Still missing from refs (record next)
- Album/Collection screen, set pages, progress
- Duplicate reveal inline (does card show "Duplicate" tag?)
- Set completion celebration and rewards
- What stars are spent on


> Flow update (Nov 21): demo now uses only the blue pack, no duplicates/NEW badge/tap-to-claim. Flow: PackAppear -> PackTear -> PackMoveDown -> CardsMoveOut -> CardsFlip -> Hold -> FlyToWallet. See `docs/ta_checklist.md`.
