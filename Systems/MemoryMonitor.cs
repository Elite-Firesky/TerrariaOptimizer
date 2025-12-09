using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using TerrariaOptimizer.Configs;

namespace TerrariaOptimizer.Systems
{
    public class MemoryMonitor : ModSystem
    {
        // Defaults used when server config is absent; tuned for heavily modded environments
        private const int DEFAULT_CLEANUP_INTERVAL_SECONDS = 60; // Check every 60 seconds
        private const int DEFAULT_HARD_THRESHOLD_MB = 8192; // 8GB hard threshold
        
	private Stopwatch stopwatch = new Stopwatch();
	private long lastMemoryUsage = 0;
	private static int _debugCounter = 0;
		
        public override void PreUpdateNPCs()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
            
            if (!config.MemoryMonitoring)
            {
                // Diagnostics heartbeat if monitoring is disabled
                _debugCounter++;
                if (DebugUtility.IsDebugEnabled() && _debugCounter % 300 == 0)
                {
                    long managedBytes = GC.GetTotalMemory(false);
                    lastMemoryUsage = managedBytes;
                    int gen0 = GC.CollectionCount(0);
                    int gen1 = GC.CollectionCount(1);
                    DebugUtility.Log($"MemoryMonitor Summary: managed={managedBytes / (1024 * 1024)} MB, gen0={gen0}, gen1={gen1} (disabled)");
                }
                return;
            }

            // Resolve cadence: server uses server config; client uses client config
            int intervalSeconds = DEFAULT_CLEANUP_INTERVAL_SECONDS;
            if (Main.netMode == NetmodeID.Server && serverConfig != null)
            {
                intervalSeconds = Math.Clamp(serverConfig.MemoryCleanupIntervalSeconds, 5, 600);
            }
            else
            {
                intervalSeconds = Math.Clamp(config.ClientMemoryCleanupIntervalSeconds, 5, 600);
            }
            int cleanupIntervalTicks = Math.Max(1, intervalSeconds * 60);

            // Safe cadence based on update ticks
            if (Main.GameUpdateCount % cleanupIntervalTicks == 0)
            {
                CheckMemoryAndCleanup();
                if (DebugUtility.IsDebugEnabled())
                {
                    long currentMemory = lastMemoryUsage;
                    int gen0 = GC.CollectionCount(0);
                    int gen1 = GC.CollectionCount(1);
                    DebugUtility.Log($"MemoryMonitor Summary: managed={currentMemory / (1024 * 1024)} MB, gen0={gen0}, gen1={gen1} (enabled)");
                }
            }
        }
		
		private void CheckMemoryAndCleanup()
		{
			try
			{
                // Get current memory usage
                long currentMemory = GC.GetTotalMemory(false);
                
                // Check if memory usage is increasing rapidly
                long memoryDelta = currentMemory - lastMemoryUsage;
                lastMemoryUsage = currentMemory;
                
                // Determine hard threshold from server config when running as server; otherwise use defaults
                long hardThresholdBytes = (long)(DEFAULT_HARD_THRESHOLD_MB) * 1024L * 1024L;
                var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
                var clientConfig = ModContent.GetInstance<OptimizationConfig>();
                if (Main.netMode == NetmodeID.Server && serverConfig != null)
                {
                    hardThresholdBytes = (long)Math.Clamp(serverConfig.MemoryHardThresholdMB, 512, 32768) * 1024L * 1024L;
                }
                else if (clientConfig != null)
                {
                    hardThresholdBytes = (long)Math.Clamp(clientConfig.ClientMemoryHardThresholdMB, 512, 32768) * 1024L * 1024L;
                }

                // Only trigger cleanup when crossing the hard threshold to avoid stutters
                if (currentMemory > hardThresholdBytes)
                {
                    PerformMemoryCleanup(true);
                }
			}
			catch (Exception)
			{
				// Silently handle any memory monitoring errors
				// We don't want memory monitoring to cause crashes
			}
		}
		
		private void PerformMemoryCleanup(bool aggressive)
		{
            // Force garbage collection only when aggressive cleanup is needed and allowed
            var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
            var clientConfig = ModContent.GetInstance<OptimizationConfig>();
            bool allowForced = true;
            if (Main.netMode == NetmodeID.Server && serverConfig != null)
            {
                allowForced = serverConfig.AllowForcedGC;
            }
            else if (clientConfig != null)
            {
                allowForced = clientConfig.ClientAllowForcedGC;
            }

            if (aggressive && allowForced)
            {
                // Prefer optimized, non-blocking collection to minimize stutter
                try
                {
                    GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: false);
                }
                catch
                {
                    // Fallback to default collect if optimized overload unavailable
                    GC.Collect();
                }
            }
            
            // If aggressive cleanup is needed, also clean up object pools
            if (aggressive && allowForced)
            {
                // Clear object pools to free up memory
                ObjectPoolManager.IntListPool?.Clear();
                ObjectPoolManager.Vector2ListPool?.Clear();
                ObjectPoolManager.IntDictionaryPool?.Clear();
            }
		}
		
        public override void OnModLoad()
        {
            // Start timing if needed later; using tick cadence primarily
            stopwatch.Reset();
            stopwatch.Start();
        }
		
        public override void OnModUnload()
        {
            try { stopwatch.Stop(); } catch { }
        }
		
		// Method to get current memory usage for display purposes
		public static long GetCurrentMemoryUsage()
		{
			try
			{
				return GC.GetTotalMemory(false);
			}
			catch
			{
				return -1; // Error getting memory usage
			}
		}
		
		// Method to predict if we're approaching memory limits
        public static bool IsMemoryUnderPressure()
        {
            var config = ModContent.GetInstance<OptimizationConfig>();
            var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
            var clientConfig = config; // reuse
			
			if (!config.MemoryMonitoring)
				return false;
				
			try
			{
                long currentMemory = GC.GetTotalMemory(false);
                long softThresholdBytes = (long)(DEFAULT_HARD_THRESHOLD_MB) * 1024L * 1024L;
                if (Main.netMode == NetmodeID.Server && serverConfig != null)
                {
                    // Approximate soft threshold as 75% of hard threshold
                    softThresholdBytes = (long)Math.Clamp(serverConfig.MemoryHardThresholdMB, 512, 32768) * 1024L * 1024L;
                    softThresholdBytes = (long)(softThresholdBytes * 0.75);
                }
                else if (clientConfig != null)
                {
                    softThresholdBytes = (long)Math.Clamp(clientConfig.ClientMemoryHardThresholdMB, 512, 32768) * 1024L * 1024L;
                    softThresholdBytes = (long)(softThresholdBytes * 0.75);
                }
                return currentMemory > softThresholdBytes; // approaching threshold
            }
            catch
            {
                return false;
            }
        }
    }
}
