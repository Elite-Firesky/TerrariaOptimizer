# TerrariaOptimizer — Honest Summary

## Purpose
Reduce scene pressure with simple, safe optimizations. No deep engine hacks, no marketing fluff.

## Systems (What They Actually Do)

1) NPC AI Throttling (`NPCAIManager`)
- When active NPCs exceed `MaxActiveNPCs`, non‑town NPC AI runs every 5 frames.
- No special boss exemption in code; tune `MaxActiveNPCs` as needed.

2) Projectile Management (`ProjectileOptimizer`)
- Caps active projectiles; removes older, low‑priority ones first.
- Prioritizes player‑owned and high‑damage projectiles.
- Limits: `300` optimal; `150` when `ParticleEffectReduction` is enabled.

3) Particle Effects (`ParticleReducer`)
- Tracks a stress indicator (entities, dust/gore, underwater).
- When stressed, culls dust and gore on the client.

4) Lighting (`LightingOptimizer`)
- Culls light from far offscreen projectiles and dust.
- “Resolution adjust” is a log hint only; no internal lighting resolution changes.

5) Tiles (`TileUpdateOptimizer`)
- Does not hook or skip the core tile update loop.
- Freezes decorative tile animation during fast movement on skip frames.
- Samples visible tiles to inform texture LRU (low‑overhead stride).

6) Textures (`TextureOptimizer`)
- Lightweight per‑category LRU tracking of texture IDs touched.
- No hard loading gate: `ShouldLoadTexture(...)` returns `true` (safe default).

7) Offscreen Entities (`OffscreenEntityOptimizer`)
- Provides a client‑side helper: interval updates for far entities.
- Used by other systems; does not force server behavior.

8) Multiplayer (`MultiplayerOptimizer`)
- Server: optional interval/distance throttling of NPC/Projectile net updates.
- Client: always ok to send; throttling is controlled server‑side.
- Emits periodic server‑side throttling metrics.

9) Memory & Pools (`MemoryMonitor`, `ObjectPoolManager`)
- Monitors managed memory; forces GC only above configured hard threshold.
- Can clear object pools during aggressive cleanup.
- Pools for common lists/dictionaries to cut temporary allocations.

## Configuration Flags
- Client: `DebugMode`, `NPCAIThrottling`, `MaxActiveNPCs`, `ProjectileOptimization`, `OffscreenOptimization`, `TileUpdateReduction`, `GarbageCollectionOptimization`, `MemoryMonitoring`, `ClientMemoryCleanupIntervalSeconds`, `ClientMemoryHardThresholdMB`, `ClientAllowForcedGC`, `MultiCoreUtilization` (not used for logic), `ParticleEffectReduction`, `LightingPerformanceMode`, `TextureOptimization`.
- Server: `ServerDebugMode`, `MemoryCleanupIntervalSeconds`, `MemoryHardThresholdMB`, `AllowForcedGC`, `NetworkTrafficReduction`, `NetworkUpdateInterval`, `NetworkOffscreenDistancePx`.

## Limits & Non‑Goals
- No packet batching or client‑side prediction beyond simple intervals.
- No reflection into internal lighting/tiles/AI engines.
- Texture tracking is informational; it does not block loads.

## Install & Use
- Build via tModLoader’s tools, enable the mod, and configure per your needs (client/server). Use `DebugMode` to see summaries.
