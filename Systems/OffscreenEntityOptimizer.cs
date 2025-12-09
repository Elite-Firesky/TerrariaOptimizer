using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using TerrariaOptimizer.Configs;

namespace TerrariaOptimizer.Systems
{
    public class OffscreenEntityOptimizer : ModSystem
    {
        private const int OFFSCREEN_UPDATE_INTERVAL = 5; // Update offscreen entities every 5 frames
        private const float OFFSCREEN_DISTANCE = 1000f; // Distance threshold for offscreen entities
        private static int _debugCounter = 0;
        
        public override void PreUpdateNPCs()
        {
            // No heavy work needed here; keep counters for periodic summary if desired
            _debugCounter++;
            if (DebugUtility.IsDebugEnabled() && _debugCounter % 300 == 0)
            {
                DebugUtility.Log($"OffscreenEntityOptimizer Summary: interval={OFFSCREEN_UPDATE_INTERVAL}, distance={OFFSCREEN_DISTANCE}, enabled={ModContent.GetInstance<OptimizationConfig>().OffscreenOptimization}");
            }
        }
		
        public override void PreUpdateProjectiles()
        {
            // No heavy work needed here; keep counters for periodic summary if desired
            _debugCounter++;
            if (DebugUtility.IsDebugEnabled() && _debugCounter % 300 == 0)
            {
                DebugUtility.Log($"OffscreenEntityOptimizer Summary: interval={OFFSCREEN_UPDATE_INTERVAL}, distance={OFFSCREEN_DISTANCE}, enabled={ModContent.GetInstance<OptimizationConfig>().OffscreenOptimization}");
            }
        }
		
		// Method to determine if an entity should update based on offscreen status
        public static bool ShouldEntityUpdate(Vector2 entityPosition)
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.OffscreenOptimization)
                return true;
            
            // Avoid gating server logic based on local player context
            if (Main.netMode == NetmodeID.Server)
                return true;

            Player player = Main.LocalPlayer;
            if (player == null)
                return true;

            // Check if entity is far from player (offscreen)
            float distanceSq = Vector2.DistanceSquared(entityPosition, player.Center);
            if (distanceSq > OFFSCREEN_DISTANCE * OFFSCREEN_DISTANCE)
            {
                // Reduce update frequency for offscreen entities
                return Main.GameUpdateCount % OFFSCREEN_UPDATE_INTERVAL == 0;
            }

            return true; // On-screen entities update normally
        }
	}
}
