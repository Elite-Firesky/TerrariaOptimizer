# TerrariaOptimizer

Straightforward performance helpers for tModLoader. This mod focuses on safe, conservative changes that reduce CPU/visual pressure without risky engine hooks. Below is exactly what it does today.

## What It Actually Does

- Lowers background load when scenes get heavy.
- Reduces extreme projectile/dust/gore counts with simple, predictable rules.
- Avoids invasive reflection or deep engine rewrites.
- Keeps server throttling explicit and controlled via server config.
- When enabled, uses a safe multi‑core background planner to precompute heavy decisions (projectile trim candidates and server offscreen flags), applying results on the main thread.

## Systems Overview

- NPC AI Throttling (`NPCAIManager`)

  - When active NPCs exceed `MaxActiveNPCs`, non-town NPC AI updates are throttled to every 5 frames.
  - There is no special exemption for bosses in code; set `MaxActiveNPCs` high if you want to avoid boss throttling.

- Projectile Optimizer (`ProjectileOptimizer`)

  - Caps total active projectiles; removes older, low‑priority ones first.
  - Priority keeps player‑owned, friendly/minion, high‑damage, and near‑player projectiles.
  - Limits: `MAX_PROJECTILES_OPTIMAL=300`, reduced to `150` if `ParticleEffectReduction` is enabled.
  - If `MultiCoreUtilization` is enabled, uses a background plan to sort/select removal candidates off‑thread, then applies removals safely on the main thread (with synchronous fallback).

- Particle Reducer (`ParticleReducer`)

  - Tracks a simple “stress” score based on entities, dust/gore volume, and underwater state.
  - When stressed, culls dust and gore (client‑side) with size‑, distance‑, and light‑aware heuristics to reduce clutter and CPU cost.
  - Optional rain optimization (config‑controlled): conservatively deactivates a fraction of far‑away rain droplets under stress while keeping all rain within the camera view. Camera “near” uses the current zoomed viewport with a small margin; fully zoomed‑out views are treated as near.

- Lighting Optimizer (`LightingOptimizer`)

  - Culls lighting contributions from far offscreen projectiles and dust to reduce lighting work.
  - Logs a “resolution adjust” intent; it does not actually change internal lighting resolution.

- Tile Update Optimizer (`TileUpdateOptimizer`)

  - Does not hook the core tile update loop.
  - Freezes decorative tile animation on skip frames when the local player moves fast.
  - Samples visible tiles (stride) to inform texture usage tracking.

- Texture Optimizer (`TextureOptimizer`)

  - Maintains a lightweight LRU of texture IDs touched by tiles/dust/gore/NPC/projectiles.
  - Does not block texture loads; `ShouldLoadTexture(...)` currently returns `true` conservatively.

- Offscreen Entity Helper (`OffscreenEntityOptimizer`)

  - Provides `ShouldEntityUpdate(Vector2)` for client-side offscreen gating (interval-based updates when far from the player).
  - Used by other systems to de-prioritize far entities; does not force server behavior.

- Multiplayer Optimizer (`MultiplayerOptimizer`)

  - Server: optionally throttles NPC/Projectile net updates based on interval/distance when `NetworkTrafficReduction` is enabled.
  - Fast path: caches active player centers once per tick and reuses them across proximity checks, using squared distance comparisons.
  - Offscreen flags computation uses compact arrays (`bool[]`) for O(1) lookups with less GC than sets.
  - When `MultiCoreUtilization` is enabled, periodically precomputes offscreen flags for NPCs/projectiles off‑thread and uses them to make throttling decisions more efficiently.
  - Client: always ok to send updates; throttling is controlled server-side.
  - Emits periodic server-side metrics (throttled/forced counts).

  Notes on “near”:

  - Client: near uses the camera viewport (respecting zoom) with a buffer to preserve edge fidelity.
  - Server: exact client camera is unknown; we use a generous distance threshold to approximate camera range and avoid trimming visible entities. Treats fully zoomed‑out views as near.

- Memory Monitor & Pools (`MemoryMonitor`, `ObjectPoolManager`)
  - Monitors managed memory; triggers GC only when above a configured hard threshold.
  - Can clear object pools during aggressive cleanup.
  - Object pools for common lists/dictionaries reduce temporary allocations when enabled.

## Configuration (in-game Mod Config)

- Client (`OptimizationConfig`)

  - `DebugMode`, `NPCAIThrottling`, `MaxActiveNPCs`, `ProjectileOptimization`, `OffscreenOptimization`, `TileUpdateReduction`, `GarbageCollectionOptimization`, `MemoryMonitoring`, `ClientMemoryCleanupIntervalSeconds`, `ClientMemoryHardThresholdMB`, `ClientAllowForcedGC`, `MultiCoreUtilization` (enables safe background planning for projectile trim and server offscreen flags), `ParticleEffectReduction`, `RainOptimization` (optional), `RainCullStride`, `RainCullOnlyWhenStressed`, `LightingPerformanceMode`, `TextureOptimization`.

- Server (`OptimizationServerConfig`)
  - `ServerDebugMode`, `MemoryCleanupIntervalSeconds`, `MemoryHardThresholdMB`, `AllowForcedGC`, `NetworkTrafficReduction`, `NetworkUpdateInterval`, `NetworkOffscreenDistancePx`.

## Limitations & Non‑Goals

- No deep engine rewrites or reflection hacks.
- No client-side prediction or packet batching beyond simple intervals.
- Lighting “resolution adjust” is a log hint only; actual resolution is unchanged.
- Texture optimization tracks usage but does not suppress loads.

## Installation

1. Build the mod using tModLoader’s build tools.
2. Enable the mod in the Mod Browser.
3. Configure options in the in-game config menus (client/server).

## Troubleshooting

- If visuals or behavior feel off, disable individual systems and re-test.
- For servers, keep throttling conservative; increase `NetworkUpdateInterval` carefully.
- Use `DebugMode` to see periodic summaries in the log.
