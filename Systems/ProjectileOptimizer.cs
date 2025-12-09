using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrariaOptimizer.Configs;
using static TerrariaOptimizer.Systems.BackgroundPlanner;

namespace TerrariaOptimizer.Systems
{
    public class ProjectileOptimizer : ModSystem
    {
        private const int MAX_PROJECTILES_OPTIMAL = 300;
        private const int MAX_PROJECTILES_REDUCED = 150;
        private static int updateCounter = 0;
        private static int lastRemovedCount = 0;

        public override void Load()
        {
            DebugUtility.LogAlways("ProjectileOptimizer loaded");
        }

        public override void Unload()
        {
            // During unload, the mod instance may already be null, so we need to be careful
            try
            {
                if (TerrariaOptimizer.Instance != null)
                {
                    TerrariaOptimizer.Instance.Logger.Info("[TerrariaOptimizer] ProjectileOptimizer unloaded");
                }
            }
            catch
            {
                // Silently ignore logging errors during unload
            }
        }

        public override void PreUpdateProjectiles()
        {
            updateCounter++;

            // Log every 60 frames (1 second at 60 FPS) to avoid spam
            if (updateCounter % 60 == 0)
            {
                DebugUtility.Log($"ProjectileOptimizer: Running update #{updateCounter}");

                // Also log the current debug mode status
                var modConfig = ModContent.GetInstance<OptimizationConfig>();
                if (modConfig != null)
                {
                    DebugUtility.Log($"ProjectileOptimizer: Debug mode enabled: {modConfig.DebugMode}");
                }
                else
                {
                    DebugUtility.Log("ProjectileOptimizer: Config not available");
                }
            }

            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.ProjectileOptimization)
            {
                if (updateCounter % 60 == 0)
                {
                    DebugUtility.Log("ProjectileOptimizer: Projectile optimization is disabled");
                }
                return;
            }

            int projectileCount = GetActiveProjectileCount();

            // Log every 60 frames (1 second at 60 FPS) to avoid spam
            if (updateCounter % 60 == 0)
            {
                DebugUtility.Log($"ProjectileOptimizer: Active projectiles: {projectileCount}");
            }

            // If we have too many projectiles, start removing oldest ones
            if (projectileCount > MAX_PROJECTILES_OPTIMAL)
            {
                ReduceProjectileCount(projectileCount);
            }
        }

        private int GetActiveProjectileCount()
        {
            int count = 0;
            for (int i = 0; i < Main.projectile.Length; i++)
            {
                if (Main.projectile[i].active)
                    count++;
            }
            return count;
        }

        private void ReduceProjectileCount(int currentCount)
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            // Use the correct config setting for particle effect reduction
            int maxProjectiles = config.ParticleEffectReduction ? MAX_PROJECTILES_REDUCED : MAX_PROJECTILES_OPTIMAL;

            if (currentCount <= maxProjectiles)
            {
                if (updateCounter % 60 == 0)
                {
                    DebugUtility.Log($"ProjectileOptimizer: Projectile count {currentCount} is within limit {maxProjectiles}");
                }
                return;
            }

            int projectilesToRemove = currentCount - maxProjectiles;

            if (updateCounter % 60 == 0)
            {
                DebugUtility.Log($"ProjectileOptimizer: Need to remove {projectilesToRemove} projectiles");
            }

            // If a background plan exists and MultiCoreUtilization is enabled, consume it first
            if (config.MultiCoreUtilization && BackgroundPlanner.TryConsumeProjectileTrimPlan(out var planned))
            {
                int removed = 0;
                for (int i = 0; i < planned.Length && removed < projectilesToRemove; i++)
                {
                    int idx = planned[i];
                    if (idx >= 0 && idx < Main.projectile.Length && Main.projectile[idx].active)
                    {
                        // Skip important projectiles just in case
                        if (IsImportantProjectile(Main.projectile[idx]))
                            continue;
                        Main.projectile[idx].active = false;
                        removed++;
                    }
                }
                lastRemovedCount = removed;
                if (updateCounter % 60 == 0)
                {
                    DebugUtility.Log($"ProjectileOptimizer (BG plan): Removed {removed} projectiles");
                }
                return;
            }

            // Create a list of projectile snapshots for sorting (main-thread)
            List<ProjectileSnapshot> snapshots = new List<ProjectileSnapshot>();

            for (int i = 0; i < Main.projectile.Length; i++)
            {
                if (Main.projectile[i].active)
                {
                    bool isImportant = IsImportantProjectile(Main.projectile[i]);
                    snapshots.Add(new ProjectileSnapshot
                    {
                        index = i,
                        timeLeft = Main.projectile[i].timeLeft,
                        isImportant = isImportant
                    });
                }
            }

            // If MultiCoreUtilization is enabled, schedule a background plan for next tick
            if (config.MultiCoreUtilization)
            {
                BackgroundPlanner.ScheduleProjectileTrim(snapshots, projectilesToRemove, Main.GameUpdateCount);
            }

            // Synchronous fallback: sort and remove now
            snapshots.Sort((a, b) =>
            {
                if (a.isImportant && !b.isImportant) return -1;
                if (!a.isImportant && b.isImportant) return 1;
                return a.timeLeft.CompareTo(b.timeLeft);
            });

            // Remove oldest non-important projectiles
            int removedCount = 0;
            for (int i = 0; i < snapshots.Count && removedCount < projectilesToRemove; i++)
            {
                var projInfo = snapshots[i];
                // Skip important projectiles
                if (projInfo.isImportant) continue;

                // Remove the projectile
                Main.projectile[projInfo.index].active = false;
                removedCount++;
            }

            if (updateCounter % 60 == 0)
            {
                DebugUtility.Log($"ProjectileOptimizer: Removed {removedCount} projectiles");
            }

            // store last removed for summary
            lastRemovedCount = removedCount;
        }

        private bool IsImportantProjectile(Projectile projectile)
        {
            // Player projectiles are important
            if (projectile.owner < 255 && projectile.owner >= 0)
            {
                // Only log occasionally to avoid spam
                if (updateCounter % 300 == 0)
                {
                    DebugUtility.Log($"ProjectileOptimizer: Projectile {projectile.whoAmI} is important (player projectile)");
                }
                return true;
            }

            // Friendly/minion projectiles are usually gameplay-relevant
            if (projectile.friendly || projectile.minion)
            {
                if (updateCounter % 300 == 0)
                {
                    DebugUtility.Log($"ProjectileOptimizer: Projectile {projectile.whoAmI} is important (friendly/minion)");
                }
                return true;
            }

            // Boss projectiles might be important
            // This is a simplified check - in reality, you might want to check specific projectile types
            if (projectile.damage > 50)
            {
                // Only log occasionally to avoid spam
                if (updateCounter % 300 == 0)
                {
                    DebugUtility.Log($"ProjectileOptimizer: Projectile {projectile.whoAmI} is important (high damage: {projectile.damage})");
                }
                return true;
            }

            // Near any active player: keep
            float nearSq = 600f * 600f;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                var pl = Main.player[i];
                if (pl?.active == true)
                {
                    if (Vector2.DistanceSquared(projectile.Center, pl.Center) <= nearSq)
                    {
                        if (updateCounter % 300 == 0)
                        {
                            DebugUtility.Log($"ProjectileOptimizer: Projectile {projectile.whoAmI} is important (near player)");
                        }
                        return true;
                    }
                }
            }

            // Only log occasionally to avoid spam
            if (updateCounter % 300 == 0)
            {
                DebugUtility.Log($"ProjectileOptimizer: Projectile {projectile.whoAmI} is not important");
            }
            return false;
        }
        public override void PostUpdatePlayers()
        {
            if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
            {
                int projectileCount = GetActiveProjectileCount();
                DebugUtility.Log($"ProjectileOptimizer Summary: active={projectileCount}, lastRemoved={lastRemovedCount}");
            }
        }
    }
}
