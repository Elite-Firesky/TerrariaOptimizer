using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrariaOptimizer.Configs;

namespace TerrariaOptimizer.Systems
{
    public class TextureOptimizer : ModSystem
    {
        // Category base offsets to avoid id collisions across asset types
        private const int BASE_NPC = 1_000_000;
        private const int BASE_PROJ = 2_000_000;
        private const int BASE_DUST = 3_000_000;
        private const int BASE_GORE = 4_000_000;
        private const int BASE_TILE = 5_000_000;

        private const int TEXTURE_CACHE_CLEANUP_INTERVAL = 600; // Every 10 seconds
        private const int MAX_TEXTURE_CACHE_SIZE = 128; // Maximum cached entries in LRU
        private const int PRESSURE_TRIM_PERCENT = 25; // Trim 25% under pressure

        // Lightweight LRU for tracking texture usage without touching actual Texture2D
        private readonly Dictionary<int, LinkedListNode<int>> _lruIndex = new Dictionary<int, LinkedListNode<int>>();
        private readonly LinkedList<int> _lruOrder = new LinkedList<int>();
        private readonly Dictionary<int, object> _cacheMarker = new Dictionary<int, object>(); // placeholder markers
        private int cleanupCounter = 0;

        public override void PreUpdateNPCs()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.TextureOptimization)
            {
                // Still emit periodic heartbeat for visibility
                cleanupCounter++;
                if (DebugUtility.IsDebugEnabled() && cleanupCounter % 300 == 0)
                {
                    DebugUtility.Log("TextureOptimizer: Texture optimization is disabled");
                }
                return;
            }

            cleanupCounter++;
            // Periodically clean up texture cache
            if (cleanupCounter >= TEXTURE_CACHE_CLEANUP_INTERVAL)
            {
                CleanupTextureCache();
                cleanupCounter = 0;
            }

            // Emit a summary every ~5 seconds
            if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
            {
                DebugUtility.Log($"TextureOptimizer Summary: cacheSize={_cacheMarker.Count}, lruSize={_lruOrder.Count}, cleanupCounter={cleanupCounter}");
            }
        }

        private void CleanupTextureCache()
        {
            // Size-based trimming
            if (_lruOrder.Count > MAX_TEXTURE_CACHE_SIZE)
            {
                int target = MAX_TEXTURE_CACHE_SIZE - (MAX_TEXTURE_CACHE_SIZE / 2);
                TrimLRU(_lruOrder.Count - target);
            }

            // Pressure-aware trimming
            if (MemoryMonitor.IsMemoryUnderPressure() && _lruOrder.Count > 0)
            {
                int trimCount = Math.Max(1, (_lruOrder.Count * PRESSURE_TRIM_PERCENT) / 100);
                TrimLRU(trimCount);
            }
        }

        // Method to determine if a texture should be loaded/rendered
        public static bool ShouldLoadTexture(Vector2 worldPosition)
        {
            // Temporarily always return true to diagnose freezing issue
            //var config = ModContent.GetInstance<OptimizationConfig>();
            //
            //if (!config.TextureOptimization)
            //	return true;
            //	
            // For now, always return true to avoid issues
            return true;
        }

        // Method to get cached texture with lazy loading
        public object GetCachedTexture(int textureId)
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            if (!config.TextureOptimization)
                return null;

            // Touch if present, otherwise insert
            if (_cacheMarker.TryGetValue(textureId, out object marker))
            {
                Touch(textureId);
                return marker;
            }

            // Insert new marker and touch
            object newMarker = new object();
            _cacheMarker[textureId] = newMarker;
            Touch(textureId);

            // Enforce capacity immediately
            if (_lruOrder.Count > MAX_TEXTURE_CACHE_SIZE)
            {
                TrimLRU(_lruOrder.Count - MAX_TEXTURE_CACHE_SIZE);
            }

            return newMarker;
        }

        // Convenience wrappers to namespace ids by category
        public void TouchNpc(int npcType) => GetCachedTexture(BASE_NPC + npcType);
        public void TouchProjectile(int projType) => GetCachedTexture(BASE_PROJ + projType);
        public void TouchDust(int dustType) => GetCachedTexture(BASE_DUST + dustType);
        public void TouchGore(int goreType) => GetCachedTexture(BASE_GORE + goreType);
        public void TouchTile(int tileType) => GetCachedTexture(BASE_TILE + tileType);

        public override void ClearWorld()
        {
            // Temporarily disable texture optimization to diagnose freezing issue
            // Clear texture cache when world is unloaded
            _lruOrder.Clear();
            _lruIndex.Clear();
            _cacheMarker.Clear();
        }

        private void Touch(int textureId)
        {
            if (_lruIndex.TryGetValue(textureId, out var node))
            {
                // Move to front
                _lruOrder.Remove(node);
                _lruOrder.AddFirst(node);
            }
            else
            {
                var newNode = new LinkedListNode<int>(textureId);
                _lruOrder.AddFirst(newNode);
                _lruIndex[textureId] = newNode;
            }
        }

        private void TrimLRU(int count)
        {
            for (int i = 0; i < count && _lruOrder.Count > 0; i++)
            {
                var last = _lruOrder.Last;
                if (last == null) break;
                int id = last.Value;
                _lruOrder.RemoveLast();
                _lruIndex.Remove(id);
                _cacheMarker.Remove(id);
            }
        }
    }
}
