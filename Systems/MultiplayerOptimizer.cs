using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrariaOptimizer.Configs;

namespace TerrariaOptimizer.Systems
{
    public class MultiplayerOptimizer : ModSystem
    {
        private static int networkUpdateCounter = 0;
        private int _debugCounter = 0;
        // Server metrics for visibility (throttled vs forced updates)
        private static int _throttledNpc = 0;
        private static int _forcedNpc = 0;
        private static int _throttledProj = 0;
        private static int _forcedProj = 0;
        private static int _windowStartTick = 0;

        public override void Load()
        {
            DebugUtility.LogAlways("MultiplayerOptimizer loaded");
        }

        public override void Unload()
        {
            DebugUtility.LogAlways("MultiplayerOptimizer unloaded");
        }

        public override void PreUpdatePlayers()
        {
            _debugCounter++;
            if (DebugUtility.IsDebugEnabled() && _debugCounter % 300 == 0)
            {
                int interval = 3;
                if (Main.netMode == NetmodeID.Server)
                {
                    var serverConfig = ModContent.GetInstance<TerrariaOptimizer.Configs.OptimizationServerConfig>();
                    interval = Math.Max(1, serverConfig?.NetworkUpdateInterval ?? 3);
                }
                DebugUtility.Log($"MultiplayerOptimizer Summary: interval={interval}, netUpdateCounter={networkUpdateCounter}");
            }

            // Server-side: every 5s emit a throttling summary when debug enabled
            if (Main.netMode == NetmodeID.Server)
            {
                var serverConfig = ModContent.GetInstance<TerrariaOptimizer.Configs.OptimizationServerConfig>();
                bool shouldLog = (serverConfig?.ServerDebugMode ?? false) || DebugUtility.IsDebugEnabled();
                if (shouldLog && Main.GameUpdateCount - _windowStartTick >= 300)
                {
                    DebugUtility.Log($"[Server] Net throttling: npc_throttled={_throttledNpc}, npc_forced={_forcedNpc}, proj_throttled={_throttledProj}, proj_forced={_forcedProj}");
                    _throttledNpc = 0;
                    _forcedNpc = 0;
                    _throttledProj = 0;
                    _forcedProj = 0;
                    _windowStartTick = Main.GameUpdateCount;
                }
            }
        }

        public override void PreUpdateNPCs()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            // Check if we're in multiplayer mode (either client or server)
            bool isMultiplayer = Main.netMode == NetmodeID.MultiplayerClient || Main.netMode == NetmodeID.Server;

            if (!isMultiplayer)
            {
                if (DebugUtility.IsDebugEnabled() && networkUpdateCounter % 60 == 0)
                {
                    DebugUtility.Log("MultiplayerOptimizer: Not in multiplayer mode");
                }
                return; // Not in multiplayer
            }

            if (!config.MultiCoreUtilization)
            {
                if (DebugUtility.IsDebugEnabled() && networkUpdateCounter % 60 == 0)
                {
                    DebugUtility.Log("MultiplayerOptimizer: Multi-core utilization is disabled");
                }
                return;
            }

            networkUpdateCounter++;
            if (DebugUtility.IsDebugEnabled() && networkUpdateCounter % 60 == 0)
            {
                DebugUtility.Log($"MultiplayerOptimizer: Network update counter: {networkUpdateCounter}");
            }
        }

        // Optimize network traffic by batching updates
        public void OptimizeNetworkTraffic()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.MultiCoreUtilization)
            {
                DebugUtility.Log("MultiplayerOptimizer: Multi-core utilization is disabled, skipping network traffic optimization");
                return;
            }

            // This would involve modifying how network packets are sent
            // by batching similar updates together
            DebugUtility.Log("MultiplayerOptimizer: Optimizing network traffic");
        }

        // Method to determine if network updates should be sent
        public static bool ShouldSendNetworkUpdate()
        {
            // Check if we're in multiplayer mode (either client or server)
            bool isMultiplayer = Main.netMode == NetmodeID.MultiplayerClient || Main.netMode == NetmodeID.Server;

            if (!isMultiplayer)
            {
                DebugUtility.Log("MultiplayerOptimizer: Not in multiplayer mode, no network updates needed");
                return false;
            }

            if (Main.netMode == NetmodeID.Server)
            {
                var serverConfig = ModContent.GetInstance<TerrariaOptimizer.Configs.OptimizationServerConfig>();
                if (!serverConfig.NetworkTrafficReduction)
                {
                    return true;
                }
                // Gate server net updates to an interval
                int interval = Math.Max(1, serverConfig.NetworkUpdateInterval);
                bool shouldSendServer = networkUpdateCounter % interval == 0;
                return shouldSendServer;
            }

            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.MultiCoreUtilization)
            {
                DebugUtility.Log("MultiplayerOptimizer: Multi-core utilization is disabled, sending updates normally");
                return true;
            }

            // Client-side: rely on multi-core toggle path for diagnostics only
            int clientInterval = 3;
            bool shouldSend = networkUpdateCounter % clientInterval == 0;
            DebugUtility.Log($"MultiplayerOptimizer: Should send network update: {shouldSend}");
            return shouldSend;
        }

        // Consider offscreen entities low priority for networking when allowed
        public static bool IsLowPriorityEntity(Vector2 center)
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.OffscreenOptimization)
                return false;

            // On server, determine offscreen status by distance to any active player
            if (Main.netMode == NetmodeID.Server)
            {
                var serverConfig = ModContent.GetInstance<TerrariaOptimizer.Configs.OptimizationServerConfig>();
                float threshold = Math.Max(800, serverConfig.NetworkOffscreenDistancePx);
                float thresholdSq = threshold * threshold;
                float minDistSq = float.MaxValue;
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player pl = Main.player[i];
                    if (pl != null && pl.active)
                    {
                        float dsq = Vector2.DistanceSquared(center, pl.Center);
                        if (dsq < minDistSq)
                            minDistSq = dsq;
                    }
                }
                return minDistSq > thresholdSq;
            }

            // On client, reuse offscreen gating
            return !OffscreenEntityOptimizer.ShouldEntityUpdate(center);
        }

        // Server-side: decide if NPC is safe to throttle net updates
        public static bool ShouldThrottleNpc(NPC npc)
        {
            if (Main.netMode != NetmodeID.Server)
                return false;

            var serverConfig = ModContent.GetInstance<TerrariaOptimizer.Configs.OptimizationServerConfig>();
            if (!serverConfig.NetworkTrafficReduction)
                return false;

            // Critical conditions: near any player or targeted player in close range
            float threshold = Math.Max(800, serverConfig.NetworkOffscreenDistancePx);
            float thresholdSq = threshold * threshold;
            float minDistSq = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player pl = Main.player[i];
                if (pl != null && pl.active)
                {
                    float dsq = Vector2.DistanceSquared(npc.Center, pl.Center);
                    if (dsq < minDistSq)
                        minDistSq = dsq;
                }
            }

            bool far = minDistSq > thresholdSq;
            bool critical = npc.justHit || npc.lifeRegen < 0 || npc.target >= 0;
            return far && !critical;
        }

        // Server-side: decide if projectile is safe to throttle net updates
        public static bool ShouldThrottleProjectile(Projectile proj)
        {
            if (Main.netMode != NetmodeID.Server)
                return false;

            var serverConfig = ModContent.GetInstance<TerrariaOptimizer.Configs.OptimizationServerConfig>();
            if (!serverConfig.NetworkTrafficReduction)
                return false;

            float threshold = Math.Max(800, serverConfig.NetworkOffscreenDistancePx);
            float thresholdSq = threshold * threshold;
            float minDistSq = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player pl = Main.player[i];
                if (pl != null && pl.active)
                {
                    float dsq = Vector2.DistanceSquared(proj.Center, pl.Center);
                    if (dsq < minDistSq)
                        minDistSq = dsq;
                }
            }

            bool far = minDistSq > thresholdSq;
            bool critical = proj.owner >= 0 || proj.friendly || proj.hostile;
            return far && !critical;
        }

        // Metrics recorders (server only)
        public static void RecordNpcThrottled()
        {
            if (Main.netMode == NetmodeID.Server) _throttledNpc++;
        }

        public static void RecordNpcForced()
        {
            if (Main.netMode == NetmodeID.Server) _forcedNpc++;
        }

        public static void RecordProjectileThrottled()
        {
            if (Main.netMode == NetmodeID.Server) _throttledProj++;
        }

        public static void RecordProjectileForced()
        {
            if (Main.netMode == NetmodeID.Server) _forcedProj++;
        }

        // Force immediate NPC network updates for important or near-critical cases
        public static bool ShouldForceImmediateUpdateNpc(NPC npc)
        {
            try
            {
                if (npc == null)
                    return false;

                // Bosses or marked as important should always sync immediately
                if (npc.boss || npc.netAlways || npc.netImportant)
                    return true;

                // Damage or regen loss events warrant immediate sync
                if (npc.justHit || npc.lifeRegen < 0)
                    return true;

                // Close to targeted player should sync more eagerly
                if (npc.target >= 0 && npc.target < Main.maxPlayers)
                {
                    var pl = Main.player[npc.target];
                    if (pl?.active == true)
                    {
                        var serverConfig = ModContent.GetInstance<TerrariaOptimizer.Configs.OptimizationServerConfig>();
                        float threshold = Math.Max(800, serverConfig?.NetworkOffscreenDistancePx ?? 1600);
                        if (Vector2.Distance(npc.Center, pl.Center) <= threshold)
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        // Force immediate projectile updates when near players or marked important
        public static bool ShouldForceImmediateUpdateProjectile(Projectile projectile)
        {
            try
            {
                if (projectile == null)
                    return false;

                if (projectile.netImportant)
                    return true;

                // Owned friendly projectile near its owner should sync more eagerly
                if (projectile.friendly && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
                {
                    var owner = Main.player[projectile.owner];
                    if (owner?.active == true)
                    {
                        var serverConfig = ModContent.GetInstance<TerrariaOptimizer.Configs.OptimizationServerConfig>();
                        float threshold = Math.Max(800, serverConfig?.NetworkOffscreenDistancePx ?? 1600);
                        if (Vector2.Distance(projectile.Center, owner.Center) <= threshold)
                            return true;
                    }
                }

                // Hostile projectile approaching any player
                if (projectile.hostile)
                {
                    var serverConfig = ModContent.GetInstance<TerrariaOptimizer.Configs.OptimizationServerConfig>();
                    float threshold = Math.Max(800, serverConfig?.NetworkOffscreenDistancePx ?? 1600);
                    for (int i = 0; i < Main.maxPlayers; i++)
                    {
                        var pl = Main.player[i];
                        if (pl?.active == true && Vector2.Distance(projectile.Center, pl.Center) <= threshold)
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }


        // Distribute workload across multiple threads when possible
        public void DistributeWorkload()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.MultiCoreUtilization)
            {
                DebugUtility.Log("MultiplayerOptimizer: Multi-core utilization is disabled, skipping workload distribution");
                return;
            }

            // This would involve using threading for non-game-state calculations
            // such as physics simulations, pathfinding, etc.
            DebugUtility.Log("MultiplayerOptimizer: Distributing workload");
        }

        // Additional multiplayer optimization methods
        public void OptimizeMultiplayerPerformance()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.MultiCoreUtilization)
            {
                DebugUtility.Log("MultiplayerOptimizer: Multi-core utilization is disabled, skipping multiplayer performance optimization");
                return;
            }

            DebugUtility.Log("MultiplayerOptimizer: Optimizing multiplayer performance");
        }
    }
}
