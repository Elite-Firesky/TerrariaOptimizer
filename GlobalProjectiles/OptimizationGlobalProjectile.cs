using Terraria;
using Terraria.ModLoader;
using TerrariaOptimizer.Systems;

namespace TerrariaOptimizer.GlobalProjectiles
{
    public class OptimizationGlobalProjectile : GlobalProjectile
    {
        public override bool PreAI(Projectile projectile)
        {
            // Check if projectile should update based on off-screen optimization
            if (!OffscreenEntityOptimizer.ShouldEntityUpdate(projectile.Center))
            {
                if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
                {
                    DebugUtility.Log($"Skipping AI update for projectile {projectile.whoAmI} (off-screen)");
                }
                return false;
            }
            if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
            {
                DebugUtility.Log($"Allowing AI update for projectile {projectile.whoAmI}");
            }
            return true; // Allow normal AI update
        }

        public override void PostAI(Projectile projectile)
        {
            // Client-side: mark projectile texture as used to feed TextureOptimizer LRU
            if (Main.netMode != Terraria.ID.NetmodeID.Server)
            {
                var texOpt = ModContent.GetInstance<TextureOptimizer>();
                texOpt.TouchProjectile(projectile.type);
            }

            // Force immediate updates for critical projectile cases
            if (Main.netMode == Terraria.ID.NetmodeID.Server && Systems.MultiplayerOptimizer.ShouldForceImmediateUpdateProjectile(projectile))
            {
                projectile.netUpdate = true;
                Systems.MultiplayerOptimizer.RecordProjectileForced();
            }

            // Server-side: throttle net updates for low-priority offscreen projectiles (critical-aware)
            if (Main.netMode == Terraria.ID.NetmodeID.Server && Systems.MultiplayerOptimizer.ShouldThrottleProjectile(projectile))
            {
                if (!Systems.MultiplayerOptimizer.ShouldSendNetworkUpdate())
                {
                    projectile.netUpdate = false;
                    Systems.MultiplayerOptimizer.RecordProjectileThrottled();
                }
            }
        }
    }
}
