using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrariaOptimizer.Configs;

namespace TerrariaOptimizer.Systems
{
    public class TileUpdateOptimizer : ModSystem
    {
        private const int TILE_UPDATE_INTERVAL = 3; // Update tiles every 3 frames instead of every frame
        private int tileUpdateCounter = 0;
        private int skippedFramesCounter = 0;
        private int[] lastTileFrameCounters = null;
        private const float FAST_MOVE_SPEED_THRESHOLD = 8f; // pixels per tick

        public override void Load()
        {
            DebugUtility.LogAlways("TileUpdateOptimizer loaded");
        }

        public override void Unload()
        {
            // Be defensive: avoid touching mod singletons or content systems during unload
            // Use null-propagation for any optional logging and ensure local state is reset
            try
            {
                TerrariaOptimizer.Instance?.Logger?.Info("[TerrariaOptimizer] TileUpdateOptimizer unloaded");
                // Reset local counters/state
                tileUpdateCounter = 0;
            }
            catch
            {
                // Silently ignore any errors during unload to comply with tML requirements
            }
        }

        public override void PreUpdateNPCs()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();

            if (!config.TileUpdateReduction)
            {
                if (DebugUtility.IsDebugEnabled() && tileUpdateCounter % 60 == 0)
                {
                    DebugUtility.Log("TileUpdateOptimizer: Tile update reduction is disabled");
                }
                return;
            }

            tileUpdateCounter++;
            if (DebugUtility.IsDebugEnabled() && tileUpdateCounter % 60 == 0)
            {
                DebugUtility.Log($"TileUpdateOptimizer: Tile update counter: {tileUpdateCounter}");
            }

            // Skip tile updates on certain frames
            if (tileUpdateCounter % TILE_UPDATE_INTERVAL != 0)
            {
                // This is a simplified approach - in reality, we'd need to hook into the tile update system
                // to selectively skip updates. For now, we'll just document the intended approach.
                if (DebugUtility.IsDebugEnabled() && tileUpdateCounter % 60 == 0)
                {
                    DebugUtility.Log("TileUpdateOptimizer: Would skip tile updates this frame (placeholder)");
                }
                skippedFramesCounter++;
            }

            // Periodically sample visible tiles to feed TextureOptimizer LRU
            if (config.TextureOptimization && Main.netMode != Terraria.ID.NetmodeID.Server && Main.GameUpdateCount % 300 == 0)
            {
                SampleVisibleTilesForTextures();
            }
        }

        // Method to determine if a tile update is necessary
        public bool ShouldUpdateTile(int x, int y)
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.TileUpdateReduction)
                return true;

            // Bounds check to avoid IndexOutOfRange
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY)
            {
                if (DebugUtility.IsDebugEnabled() && tileUpdateCounter % 60 == 0)
                {
                    DebugUtility.Log($"TileUpdateOptimizer: Tile coords out of bounds ({x},{y})");
                }
                return true; // don't throttle unknown tiles
            }

            // Always update important tiles (e.g., chests, doors) or tiles that actually exist
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || IsImportantTile(tile))
            {
                if (DebugUtility.IsDebugEnabled() && tileUpdateCounter % 60 == 0)
                {
                    DebugUtility.Log($"TileUpdateOptimizer: Tile at ({x},{y}) considered important (hasTile={tile.HasTile}), updating");
                }
                return true;
            }

            // Otherwise, use our reduced update schedule
            bool shouldUpdate = tileUpdateCounter % TILE_UPDATE_INTERVAL == 0;
            if (DebugUtility.IsDebugEnabled() && tileUpdateCounter % 60 == 0)
            {
                DebugUtility.Log($"TileUpdateOptimizer: Tile at ({x},{y}) shouldUpdate={shouldUpdate}");
            }
            return shouldUpdate;
        }

        public override void PostUpdatePlayers()
        {
            // During fast traversal, throttle global decorative tile animation counter to reduce background work
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (config.TileUpdateReduction && config.OffscreenOptimization)
            {
                Player p = Main.LocalPlayer;
                if (p != null)
                {
                    float vel = p.velocity.Length();
                    if (vel >= FAST_MOVE_SPEED_THRESHOLD)
                    {
                        // Freeze tile animation on skip frames
                        bool skip = (Main.GameUpdateCount % TILE_UPDATE_INTERVAL) != 0;
                        if (skip)
                        {
                            // Freeze decorative tile animation by restoring previous frame counters
                            if (lastTileFrameCounters == null)
                            {
                                lastTileFrameCounters = (int[])Main.tileFrameCounter.Clone();
                            }
                            int len = Math.Min(lastTileFrameCounters.Length, Main.tileFrameCounter.Length);
                            Array.Copy(lastTileFrameCounters, Main.tileFrameCounter, len);
                            skippedFramesCounter++;
                        }
                        else
                        {
                            // Update snapshot on normal frames
                            lastTileFrameCounters = (int[])Main.tileFrameCounter.Clone();
                        }
                    }
                }
            }

            if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
            {
                int snapshotLen = lastTileFrameCounters == null ? 0 : lastTileFrameCounters.Length;
                DebugUtility.Log($"TileUpdateOptimizer Summary: interval={TILE_UPDATE_INTERVAL}, skippedFramesLastWindow={skippedFramesCounter}, snapshotLen={snapshotLen}");
                // reset window counter
                skippedFramesCounter = 0;
            }
        }

        private bool IsImportantTile(Tile tile)
        {
            // Minimal safeguard set of tile types that should never be throttled
            // Use TileID constants for common interactive tiles
            // If tile.TileType is unavailable, default to treat as important
            try
            {
                int type = tile.TileType;
                return type == TileID.Containers
                    || type == TileID.OpenDoor
                || type == TileID.ClosedDoor
                || type == TileID.Switches
                || type == TileID.WorkBenches
                || type == TileID.Furnaces;
            }
            catch
            {
                return true; // be conservative if types are inaccessible
            }
        }

        private void SampleVisibleTilesForTextures()
        {
            var texOpt = ModContent.GetInstance<TextureOptimizer>();
            // Screen rectangle in world coordinates
            var screenPos = Main.screenPosition;
            int screenW = Main.screenWidth;
            int screenH = Main.screenHeight;
            // Convert to tile coordinates
            int minX = Math.Max(0, (int)(screenPos.X / 16));
            int minY = Math.Max(0, (int)(screenPos.Y / 16));
            int maxX = Math.Min(Main.maxTilesX - 1, (int)((screenPos.X + screenW) / 16));
            int maxY = Math.Min(Main.maxTilesY - 1, (int)((screenPos.Y + screenH) / 16));

            // Sample with stride to keep CPU low
            const int stride = 4;
            // Use a small set to avoid touching the same type repeatedly
            var seenTypes = new System.Collections.Generic.HashSet<int>();
            for (int x = minX; x <= maxX; x += stride)
            {
                for (int y = minY; y <= maxY; y += stride)
                {
                    Tile tile = Main.tile[x, y];
                    if (tile != null && tile.HasTile)
                    {
                        int type = tile.TileType;
                        if (seenTypes.Add(type))
                        {
                            texOpt.TouchTile(type);
                            // Limit touches per sampling pass
                            if (seenTypes.Count >= 64)
                                return;
                        }
                    }
                }
            }
        }
    }
}
