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
        private static uint _windowStartTick = 0;

        // Fast path: cache active player centers per server tick for reuse
        private static volatile Vector2[] _cachedPlayerCenters;

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
                    var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
                    interval = Math.Max(1, serverConfig?.NetworkUpdateInterval ?? 3);
                }
                DebugUtility.Log($"MultiplayerOptimizer Summary: interval={interval}, netUpdateCounter={networkUpdateCounter}");
            }

            // Server-side: every 5s emit a throttling summary when debug enabled
            if (Main.netMode == NetmodeID.Server)
            {
                var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
                bool shouldLog = (serverConfig?.ServerDebugMode ?? false) || DebugUtility.IsDebugEnabled();
                if (shouldLog && (Main.GameUpdateCount - _windowStartTick) >= 300u)
                {
                    DebugUtility.Log($"[Server] Net throttling: npc_throttled={_throttledNpc}, npc_forced={_forcedNpc}, proj_throttled={_throttledProj}, proj_forced={_forcedProj}");
                    _throttledNpc = 0;
                    _forcedNpc = 0;
                    _throttledProj = 0;
                    _forcedProj = 0;
                    _windowStartTick = Main.GameUpdateCount;
                }

                // Fast path: cache active player centers for this tick
                try
                {
                    var vecs = ObjectPoolManager.GetVector2List();
                    for (int i = 0; i < Main.maxPlayers; i++)
                    {
                        Player pl = Main.player[i];
                        if (pl != null && pl.active)
                        {
                            vecs.Add(pl.Center);
                        }
                    }
                    _cachedPlayerCenters = vecs.ToArray();
                    ObjectPoolManager.ReturnVector2List(vecs);
                }
                catch { }

                // Server-side: periodically precompute offscreen flags for NPCs and Projectiles
                if ((Main.GameUpdateCount % 30u) == 0u)
                {
                    float threshold = Math.Max(800, serverConfig.NetworkOffscreenDistancePx);

                    var players = ObjectPoolManager.GetPlayerSnapshotList();
                    var centers = _cachedPlayerCenters;
                    if (centers != null && centers.Length > 0)
                    {
                        for (int i = 0; i < centers.Length; i++)
                        {
                            players.Add(new BackgroundPlanner.PlayerSnapshot { center = centers[i] });
                        }
                    }
                    else
                    {
                        for (int i = 0; i < Main.maxPlayers; i++)
                        {
                            Player pl = Main.player[i];
                            if (pl != null && pl.active)
                            {
                                players.Add(new BackgroundPlanner.PlayerSnapshot { center = pl.Center });
                            }
                        }
                    }

                    var npcs = ObjectPoolManager.GetEntitySnapshotList();
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC n = Main.npc[i];
                        if (n != null && n.active)
                        {
                            npcs.Add(new BackgroundPlanner.EntitySnapshot { index = n.whoAmI, center = n.Center });
                        }
                    }

                    var projs = ObjectPoolManager.GetEntitySnapshotList();
                    for (int i = 0; i < Main.projectile.Length; i++)
                    {
                        Projectile p = Main.projectile[i];
                        if (p != null && p.active)
                        {
                            projs.Add(new BackgroundPlanner.EntitySnapshot { index = p.whoAmI, center = p.Center });
                        }
                    }

                    BackgroundPlanner.ScheduleOffscreenFlagsForNPCs(players, npcs, threshold);
                    BackgroundPlanner.ScheduleOffscreenFlagsForProjectiles(players, projs, threshold);

                    // Return pooled lists after scheduling (planner copies to arrays synchronously)
                    ObjectPoolManager.ReturnPlayerSnapshotList(players);
                    ObjectPoolManager.ReturnEntitySnapshotList(npcs);
                    ObjectPoolManager.ReturnEntitySnapshotList(projs);
                }
            }
        }

        public override void PreUpdateNPCs()
        {
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
            networkUpdateCounter++;
            if (DebugUtility.IsDebugEnabled() && networkUpdateCounter % 60 == 0)
            {
                DebugUtility.Log($"MultiplayerOptimizer: Network update counter: {networkUpdateCounter}");
            }
        }

        // Method to determine if network updates should be sent
        public static bool ShouldSendNetworkUpdate()
        {
            // Check if we're in multiplayer mode (either client or server)
            bool isMultiplayer = Main.netMode == NetmodeID.MultiplayerClient || Main.netMode == NetmodeID.Server;

            if (!isMultiplayer)
            {
                return false;
            }

            if (Main.netMode == NetmodeID.Server)
            {
                var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
                if (!serverConfig.NetworkTrafficReduction)
                {
                    return true;
                }
                // Gate server net updates to an interval
                int interval = Math.Max(1, serverConfig.NetworkUpdateInterval);
                bool shouldSendServer = networkUpdateCounter % interval == 0;
                return shouldSendServer;
            }
            // Client: always allow updates; throttling is server-controlled
            return true;
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
                var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
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

            var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
            if (!serverConfig.NetworkTrafficReduction)
                return false;

            bool far = BackgroundPlanner.IsNpcFar(npc.whoAmI);
            // Consider critical only when near the targeted player; far-away targeting is common and safe to throttle
            bool nearTarget = false;
            if (npc.target >= 0 && npc.target < Main.maxPlayers)
            {
                var pl = Main.player[npc.target];
                if (pl?.active == true)
                {
                    float threshold = Math.Max(800, serverConfig.NetworkOffscreenDistancePx);
                    float thresholdSq = threshold * threshold;
                    nearTarget = Vector2.DistanceSquared(npc.Center, pl.Center) <= thresholdSq;
                }
            }
            bool critical = npc.justHit || npc.lifeRegen < 0 || nearTarget;
            return far && !critical;
        }

        // Server-side: decide if projectile is safe to throttle net updates
        public static bool ShouldThrottleProjectile(Projectile proj)
        {
            if (Main.netMode != NetmodeID.Server)
                return false;

            var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
            if (!serverConfig.NetworkTrafficReduction)
                return false;

            bool far = BackgroundPlanner.IsProjectileFar(proj.whoAmI);
            // Treat as critical when near any player, or marked important
            bool nearPlayer = false;
            float threshold = Math.Max(800, serverConfig.NetworkOffscreenDistancePx);
            float thresholdSq = threshold * threshold;
            var centers = _cachedPlayerCenters;
            if (centers != null)
            {
                for (int i = 0; i < centers.Length; i++)
                {
                    if (Vector2.DistanceSquared(proj.Center, centers[i]) <= thresholdSq)
                    {
                        nearPlayer = true;
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    var pl = Main.player[i];
                    if (pl?.active == true)
                    {
                        if (Vector2.DistanceSquared(proj.Center, pl.Center) <= thresholdSq)
                        {
                            nearPlayer = true;
                            break;
                        }
                    }
                }
            }
            bool critical = proj.netImportant || nearPlayer || proj.hostile;
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
                if (npc.boss || npc.netAlways)
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
                        var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
                        float threshold = Math.Max(800, serverConfig?.NetworkOffscreenDistancePx ?? 1600);
                        float thresholdSq = threshold * threshold;
                        if (Vector2.DistanceSquared(npc.Center, pl.Center) <= thresholdSq)
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
                        var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
                        float threshold = Math.Max(800, serverConfig?.NetworkOffscreenDistancePx ?? 1600);
                        float thresholdSq = threshold * threshold;
                        if (Vector2.DistanceSquared(projectile.Center, owner.Center) <= thresholdSq)
                            return true;
                    }
                }

                // Hostile projectile approaching any player
                if (projectile.hostile)
                {
                    var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
                    float threshold = Math.Max(800, serverConfig?.NetworkOffscreenDistancePx ?? 1600);
                    float thresholdSq = threshold * threshold;
                    var centers = _cachedPlayerCenters;
                    if (centers != null)
                    {
                        for (int i = 0; i < centers.Length; i++)
                        {
                            if (Vector2.DistanceSquared(projectile.Center, centers[i]) <= thresholdSq)
                                return true;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < Main.maxPlayers; i++)
                        {
                            var pl = Main.player[i];
                            if (pl?.active == true && Vector2.DistanceSquared(projectile.Center, pl.Center) <= thresholdSq)
                                return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

    }
}
