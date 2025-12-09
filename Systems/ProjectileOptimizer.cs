using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrariaOptimizer.Configs;

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
            
            // Create a list of projectiles with their indices and priority
            List<(int index, int age, bool isImportant)> projectilePriorities = new List<(int, int, bool)>();
            
            for (int i = 0; i < Main.projectile.Length; i++)
            {
                if (Main.projectile[i].active)
                {
                    // Determine if projectile is important (player projectiles, boss projectiles, etc.)
                    bool isImportant = IsImportantProjectile(Main.projectile[i]);
                    projectilePriorities.Add((i, Main.projectile[i].timeLeft, isImportant));
                }
            }
            
            // Sort by importance first, then by age (oldest first)
            projectilePriorities.Sort((a, b) => 
            {
                // Keep important projectiles
                if (a.isImportant && !b.isImportant) return -1;
                if (!a.isImportant && b.isImportant) return 1;
                
                // For same importance level, sort by age
                return a.age.CompareTo(b.age);
            });
            
            // Remove oldest non-important projectiles
            int removedCount = 0;
            for (int i = 0; i < projectilePriorities.Count && removedCount < projectilesToRemove; i++)
            {
                var projInfo = projectilePriorities[i];
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
