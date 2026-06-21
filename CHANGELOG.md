# Changelog

## 1.1.0 - 2026-06-21

- Added a 30-second internal performance capture.
- Reports frame-time percentiles, stutters, MEC coroutine scheduler cost,
  AttackRange optimizer cost, population, coroutine counts, and managed GC.
- Added an internal three-phase bottleneck isolation test for friendly Spine
  animation/mesh work and Physics2D bodies.
- Added a low-effects mode that freezes animated water reflection and
  disables the GUI camera's blur, vignette, and chromatic-aberration passes.
- Added friendly-body collision optimization alongside the AttackRange
  optimization.
- Added a crowd-display mode that renders one representative friendly
  unit mesh for every ten real units in armies above 500, while keeping all
  units active in simulation and combat.
- Simplified controls: `F6` toggles core optimization, `F7` cycles
  `Ultra -> ratio -> off`, and `F8`/`F9` increase or decrease the display
  ratio by ten.
- Removed the upper crowd-display ratio limit while always keeping at least
  one representative friendly unit visible.
- Crowd display now keeps at least one visible representative of every
  friendly unit type, regardless of the selected ratio.
- Ultra mode renders exactly one representative of each friendly unit type
  and is the default crowd-display state for new installations.
- Crowd and Ultra modes now also suppress per-unit particles, floating unit
  text, and buff icons for hidden units while preserving all gameplay effects.
- Hidden units now immediately recycle newly requested buff-icon objects,
  preventing global cards from expanding the visual object pool by thousands
  of entries. Icons are recreated only when a unit becomes visible.
- Batched Physics2D transform synchronization after the game's central
  movement update, avoiding an immediate physics broadphase refresh for every
  individual moving unit.
- Keeps diagnostics inactive during normal play to avoid measurement overhead.

## 1.0.0 - 2026-06-19

- Replaced high-volume friendly `AttackRange` trigger colliders with a
  centralized range scanner during heavy battles.
- Reduced repeated object-pool disk logging.
- Added a compact `FPS | Optimizer` HUD.
- Added `F6` optimizer toggle and `F7` HUD toggle.
