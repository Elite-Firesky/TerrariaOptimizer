# TerrariaOptimizer

Straightforward performance helpers for tModLoader. This mod focuses on safe, conservative changes that reduce CPU/visual pressure without risky engine hooks. Below is exactly what it does today.

## What It Actually Does

- Lowers background load when scenes get heavy.
- Reduces extreme projectile/dust/gore counts with simple, predictable rules.
- Avoids invasive reflection or deep engine rewrites.
- Keeps server throttling explicit and controlled via server config.

## Systems Overview

- NPC AI Throttling (`NPCAIManager`)
  - When active NPCs exceed `MaxActiveNPCs`, non-town NPC AI updates are throttled to every 5 frames.
  - There is no special exemption for bosses in code; set `MaxActiveNPCs` high if you want to avoid boss throttling.

- Projectile Optimizer (`ProjectileOptimizer`)
  - Caps total active projectiles; removes older, low-priority ones first.
  - Priority keeps player-owned and high-damage projectiles.
  - Limits: `MAX_PROJECTILES_OPTIMAL=300`, reduced to `150` if `ParticleEffectReduction` is enabled.

- Particle Reducer (`ParticleReducer`)
  - Tracks a simple “stress” score based on entities, dust/gore volume, and underwater state.
  - When stressed, culls dust and gore (client-side) to reduce clutter and CPU cost.

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
  - Client: always ok to send updates; throttling is controlled server-side.
  - Emits periodic server-side metrics (throttled/forced counts).

- Memory Monitor & Pools (`MemoryMonitor`, `ObjectPoolManager`)
  - Monitors managed memory; triggers GC only when above a configured hard threshold.
  - Can clear object pools during aggressive cleanup.
  - Object pools for common lists/dictionaries reduce temporary allocations when enabled.

## Configuration (in-game Mod Config)

- Client (`OptimizationConfig`)
  - `DebugMode`, `NPCAIThrottling`, `MaxActiveNPCs`, `ProjectileOptimization`, `OffscreenOptimization`, `TileUpdateReduction`, `GarbageCollectionOptimization`, `MemoryMonitoring`, `ClientMemoryCleanupIntervalSeconds`, `ClientMemoryHardThresholdMB`, `ClientAllowForcedGC`, `MultiCoreUtilization` (currently not used), `ParticleEffectReduction`, `LightingPerformanceMode`, `TextureOptimization`.

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
