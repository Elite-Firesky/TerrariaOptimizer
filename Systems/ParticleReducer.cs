using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrariaOptimizer.Configs;
using Terraria.ID;

namespace TerrariaOptimizer.Systems
{
    public class ParticleReducer : ModSystem
    {
        private const int MAX_PARTICLES_OPTIMAL = 1000;
        private const int MAX_PARTICLES_REDUCED = 500;
        private static int _updateCounter = 0;
        private static int _stressIndicator = 0;
        private static int _lastEntityCount = 0;
        // Hooks removed; use safe culling instead to avoid unsupported hook references

        public override void Load()
        {
            DebugUtility.LogAlways("ParticleReducer loaded");
        }

        public override void Unload()
        {
            DebugUtility.LogAlways("ParticleReducer unloaded");
        }

        public override void PreUpdateNPCs()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.ParticleEffectReduction)
            {
                DebugUtility.Log("ParticleReducer: Particle effect reduction is disabled");
                return;
            }

            _updateCounter++;
            DebugUtility.Log($"ParticleReducer: Update counter: {_updateCounter}");

            // Check system performance every 30 frames for quicker response in heavy biomes
            if (_updateCounter % 30 == 0)
            {
                // Combine entity counts with dust/gore volume to reflect heavy particle scenes
                int totalEntities = GetTotalEntityCount();
                (int dustCount, int goreCount) = GetActiveParticleCountsAndTouchTextures();
                bool underwater = Main.netMode != NetmodeID.Server && Main.LocalPlayer != null && Main.LocalPlayer.wet;

                // Stress score: base on entities, dust/gore, and underwater state
                int score = 0;
                score += totalEntities > 600 ? 2 : (totalEntities > 400 ? 1 : 0);
                score += dustCount > 600 ? 2 : (dustCount > 300 ? 1 : 0);
                score += goreCount > 150 ? 1 : 0;
                score += underwater ? 1 : 0;

                if (score >= 3)
                {
                    _stressIndicator = Math.Min(_stressIndicator + 1, 10);
                    DebugUtility.Log($"ParticleReducer: Stress indicator increased to {_stressIndicator} (entities={totalEntities}, dust={dustCount}, gore={goreCount}, underwater={underwater})");
                }
                else
                {
                    _stressIndicator = Math.Max(_stressIndicator - 1, 0);
                    DebugUtility.Log($"ParticleReducer: Stress indicator decreased to {_stressIndicator} (entities={totalEntities}, dust={dustCount}, gore={goreCount}, underwater={underwater})");
                }
                _lastEntityCount = totalEntities;
            }

            // Check system performance
            if (IsPerformanceStressed())
            {
                DebugUtility.Log("ParticleReducer: Performance is stressed, reducing particle effects");
                ReduceParticleEffects();
            }
        }

        public override void PostUpdatePlayers()
        {
            if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
            {
                DebugUtility.Log($"ParticleReducer Summary: stress={_stressIndicator}, lastEntityCount={_lastEntityCount}");
            }
        }

        private int GetTotalEntityCount()
        {
            // Count active NPCs
            int npcCount = 0;
            for (int i = 0; i < Main.npc.Length; i++)
            {
                if (Main.npc[i].active)
                    npcCount++;
            }

            // Count active projectiles
            int projectileCount = 0;
            for (int i = 0; i < Main.projectile.Length; i++)
            {
                if (Main.projectile[i].active)
                    projectileCount++;
            }

            DebugUtility.Log($"ParticleReducer: Total entities: NPCs={npcCount}, Projectiles={projectileCount}");
            return npcCount + projectileCount;
        }

        private (int dustCount, int goreCount) GetActiveParticleCountsAndTouchTextures()
        {
            if (Main.netMode == NetmodeID.Server)
                return (0, 0);
            int dust = 0, gore = 0;
            var texOpt = ModContent.GetInstance<TextureOptimizer>();
            int sampledDust = 0, sampledGore = 0;
            const int maxDustSamples = 200; // cap texture touches per evaluation
            const int maxGoreSamples = 50;
            for (int i = 0; i < Main.dust.Length; i++)
            {
                var d = Main.dust[i];
                if (d != null && d.active)
                {
                    dust++;
                    if (sampledDust < maxDustSamples)
                    {
                        texOpt.TouchDust(d.type);
                        sampledDust++;
                    }
                }
            }
            for (int g = 0; g < Main.gore.Length; g++)
            {
                var gr = Main.gore[g];
                if (gr != null && gr.active)
                {
                    gore++;
                    if (sampledGore < maxGoreSamples)
                    {
                        texOpt.TouchGore(gr.type);
                        sampledGore++;
                    }
                }
            }
            return (dust, gore);
        }

        private bool IsPerformanceStressed()
        {
            // Consider it stressed if our stress indicator is above a threshold
            bool isStressed = _stressIndicator > 7;
            DebugUtility.Log($"ParticleReducer: Is performance stressed: {isStressed} (stress indicator: {_stressIndicator})");
            return isStressed;
        }

        private void ReduceParticleEffects()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            // This is where we would modify particle systems to reduce effects
            // In practice, we might need to use reflection or hooks to modify
            // internal particle engine parameters

            // Cull existing dust/gore to lower visual/CPU load when stressed
            TrimParticles();
        }

        private static bool ShouldDropParticle(Vector2 position)
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.ParticleEffectReduction)
                return false;

            // Base drop rate depends on stress level
            float dropChance = 0f; // 0 means keep all
            if (_stressIndicator >= 8)
                dropChance = 0.8f;
            else if (_stressIndicator >= 5)
                dropChance = 0.5f;
            else if (_stressIndicator >= 2)
                dropChance = 0.25f;

            // Increase drop chance if far offscreen on client
            if (Main.netMode != NetmodeID.Server)
            {
                Player p = Main.LocalPlayer;
                if (p != null)
                {
                    float distSq = Vector2.DistanceSquared(position, p.Center);
                    if (distSq > 1000f * 1000f)
                    {
                        dropChance = Math.Min(1f, dropChance + 0.2f);
                    }
                    // Underwater areas: slightly raise drop chance to reduce dense particle spam
                    if (p.wet)
                    {
                        dropChance = Math.Min(1f, dropChance + 0.15f);
                    }
                }
            }

            return Main.rand.NextFloat() < dropChance;
        }


        private void TrimParticles()
        {
            // Client-side only; servers don't render particles
            if (Main.netMode == NetmodeID.Server)
                return;

            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.ParticleEffectReduction)
                return;

            Player p = Main.LocalPlayer;
            if (p == null)
                return;

            int removedDust = 0;
            int removedGore = 0;

            int stride = _stressIndicator >= 8 ? 4 : (_stressIndicator >= 5 ? 8 : 16);

            // Dust culling
            for (int i = 0; i < Main.dust.Length; i += stride)
            {
                var d = Main.dust[i];
                if (d == null || !d.active)
                    continue;
                if (ShouldDropParticle(d.position))
                {
                    d.active = false;
                    removedDust++;
                }
            }

            // Gore culling
            for (int i = 0; i < Main.gore.Length; i += stride)
            {
                var g = Main.gore[i];
                if (g == null || !g.active)
                    continue;
                if (ShouldDropParticle(g.position))
                {
                    g.active = false;
                    removedGore++;
                }
            }

            if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
            {
                DebugUtility.Log($"ParticleReducer Summary: culled dust={removedDust}, gore={removedGore}, stress={_stressIndicator}");
            }
        }

        // Method to determine if particles should be reduced
        public bool ShouldReduceParticles()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.ParticleEffectReduction)
                return false;

            bool shouldReduce = IsPerformanceStressed();
            DebugUtility.Log($"ParticleReducer: Should reduce particles: {shouldReduce}");
            return shouldReduce;
        }

        // Additional particle reduction methods
        public void OptimizeParticleEffects()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.ParticleEffectReduction)
                return;

            // Potential optimizations:
            // 1. Reduce particle spawn rate
            // 2. Limit total particle count
            // 3. Simplify particle rendering
            // 4. Remove distant particles
            DebugUtility.Log("ParticleReducer: Optimizing particle effects");
        }
    }
}
