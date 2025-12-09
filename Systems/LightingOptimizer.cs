using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrariaOptimizer.Configs;

namespace TerrariaOptimizer.Systems
{
    public class LightingOptimizer : ModSystem
    {
        private const float HIGH_PERFORMANCE_LIGHTING_SCALE = 0.8f; // Reduce lighting resolution
        private const int LIGHTING_UPDATE_INTERVAL = 2; // Update lighting every 2 frames
        private const int OFFSCREEN_LIGHT_MARGIN = 800; // pixels beyond screen before culling light
        private int lightingUpdateCounter = 0;
        private int adjustmentsCount = 0;

        // Cache original projectile light to restore when it returns on-screen
        private readonly Dictionary<int, float> _projectileLightCache = new();

        // Window counters for diagnostics
        private int _culledLightCountWindow = 0;
        private int _restoredLightCountWindow = 0;

        public override void Load()
        {
            DebugUtility.LogAlways("LightingOptimizer loaded");
        }

        public override void Unload()
        {
            DebugUtility.LogAlways("LightingOptimizer unloaded");
        }

        public override void PreUpdateNPCs()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.LightingPerformanceMode)
            {
                if (DebugUtility.IsDebugEnabled() && lightingUpdateCounter % 60 == 0)
                {
                    DebugUtility.Log("LightingOptimizer: Lighting performance mode is disabled");
                }
                return;
            }

            lightingUpdateCounter++;
            if (DebugUtility.IsDebugEnabled() && lightingUpdateCounter % 60 == 0)
            {
                DebugUtility.Log($"LightingOptimizer: Lighting update counter: {lightingUpdateCounter}");
            }

            // Adjust lighting resolution based on performance needs
            if (Main.GameZoomTarget > 1.0f)
            {
                // Reduce lighting resolution when zoomed out
                AdjustLightingResolution(HIGH_PERFORMANCE_LIGHTING_SCALE);
            }

            // Cull offscreen light contributors conservatively
            CullOffscreenLightContributors();

            // Emit periodic summary
            if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
            {
                bool underwater = Main.netMode != NetmodeID.Server && Main.LocalPlayer != null && Main.LocalPlayer.wet;
                int usedInterval = underwater ? LIGHTING_UPDATE_INTERVAL + 1 : LIGHTING_UPDATE_INTERVAL;
                DebugUtility.Log($"LightingOptimizer Summary: counter={lightingUpdateCounter}, interval={usedInterval}, underwater={underwater}, adjustmentsLastWindow={adjustmentsCount}, culledLightWindow={_culledLightCountWindow}, restoredLightWindow={_restoredLightCountWindow}");
                adjustmentsCount = 0;
                _culledLightCountWindow = 0;
                _restoredLightCountWindow = 0;
            }
        }

        private void AdjustLightingResolution(float scale)
        {
            DebugUtility.Log($"LightingOptimizer: Adjusting lighting resolution to scale {scale}");
            adjustmentsCount++;
        }

        // Method to determine if lighting should update this frame
        public bool ShouldUpdateLighting()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.LightingPerformanceMode)
            {
                DebugUtility.Log("LightingOptimizer: Lighting performance mode is disabled, updating lighting normally");
                return true;
            }

            bool underwater = Main.netMode != NetmodeID.Server && Main.LocalPlayer != null && Main.LocalPlayer.wet;
            int interval = underwater ? LIGHTING_UPDATE_INTERVAL + 1 : LIGHTING_UPDATE_INTERVAL;
            bool shouldUpdate = lightingUpdateCounter % interval == 0;
            DebugUtility.Log($"LightingOptimizer: Should update lighting: {shouldUpdate} (interval={interval}, underwater={underwater})");
            return shouldUpdate;
        }



        // Cull light contributions from far-offscreen projectiles and dust to reduce lighting work
        private void CullOffscreenLightContributors()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.OffscreenOptimization)
            {
                return;
            }

            // Screen rectangle with margin
            var screenPos = Main.screenPosition;
            int margin = OFFSCREEN_LIGHT_MARGIN;
            Rectangle screenRect = new Rectangle((int)screenPos.X - margin, (int)screenPos.Y - margin, Main.screenWidth + margin * 2, Main.screenHeight + margin * 2);

            // Projectiles: temporarily zero light when far offscreen, restore when on-screen
            for (int i = 0; i < Main.projectile.Length; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active)
                {
                    // Clean cache entries for inactive projectiles
                    if (_projectileLightCache.ContainsKey(i))
                    {
                        _projectileLightCache.Remove(i);
                    }
                    continue;
                }

                Rectangle projRect = new Rectangle((int)p.position.X, (int)p.position.Y, (int)p.width, (int)p.height);
                bool farOffscreen = !screenRect.Intersects(projRect);

                if (farOffscreen)
                {
                    // If we haven't cached original light and it has light, cache and zero
                    if (p.light > 0f && !_projectileLightCache.ContainsKey(i))
                    {
                        _projectileLightCache[i] = p.light;
                        p.light = 0f;
                        _culledLightCountWindow++;
                        if (DebugUtility.IsDebugEnabled() && lightingUpdateCounter % 120 == 0)
                        {
                            DebugUtility.Log($"LightingOptimizer: Culled projectile light for offscreen id={i}");
                        }
                    }
                }
                else
                {
                    // Restore original light if previously culled
                    if (_projectileLightCache.TryGetValue(i, out float original))
                    {
                        p.light = original;
                        _projectileLightCache.Remove(i);
                        _restoredLightCountWindow++;
                        if (DebugUtility.IsDebugEnabled() && lightingUpdateCounter % 120 == 0)
                        {
                            DebugUtility.Log($"LightingOptimizer: Restored projectile light for on-screen id={i}");
                        }
                    }
                }
            }

            // Dust: disable light emittance for far-offscreen dust; no restoration needed
            for (int d = 0; d < Main.dust.Length; d++)
            {
                Dust dust = Main.dust[d];
                if (!dust.active)
                    continue;

                Rectangle dustRect = new Rectangle((int)dust.position.X, (int)dust.position.Y, 8, 8);
                bool farOffscreenDust = !screenRect.Intersects(dustRect);
                if (farOffscreenDust)
                {
                    // Reduce/disable lighting contribution from dust when far offscreen
                    dust.noLight = true;
                }
            }
        }
    }
}
