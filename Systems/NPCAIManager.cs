using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TerrariaOptimizer.Configs;

namespace TerrariaOptimizer.Systems
{
	public class NPCAIManager : ModSystem
	{
		private const int BASE_AI_UPDATE_RATE = 1; // Update every frame
		private const int THROTTLED_AI_UPDATE_RATE = 5; // Update every 5 frames
		
	private Dictionary<int, int> npcUpdateCounters = new Dictionary<int, int>();
	private static bool[] npcShouldUpdate = new bool[Main.maxNPCs]; // Use Main.maxNPCs instead of Main.npc.Length
	private static int updateCounter = 0;
	private static int lastThrottledCount = 0;
		
		public override void Load()
		{
			// Initialize the npcShouldUpdate array
			Array.Resize(ref npcShouldUpdate, Main.maxNPCs);
			for (int i = 0; i < npcShouldUpdate.Length; i++)
			{
				npcShouldUpdate[i] = true;
			}
			DebugUtility.LogAlways("NPCAIManager loaded");
		}
		
		public override void Unload()
		{
			// During unload, the mod instance may already be null, so we need to be careful
			try
			{
				if (TerrariaOptimizer.Instance != null)
				{
					TerrariaOptimizer.Instance.Logger.Info("[TerrariaOptimizer] NPCAIManager unloaded");
				}
			}
			catch
			{
				// Silently ignore logging errors during unload
			}
		}
		
		public override void PreUpdateNPCs()
		{
			updateCounter++;
			
			// Log every 60 frames (1 second at 60 FPS) to avoid spam
			if (updateCounter % 60 == 0)
			{
				DebugUtility.Log($"NPCAIManager: Running update #{updateCounter}");
				
				// Also log the current debug mode status
				var modConfig = ModContent.GetInstance<OptimizationConfig>();
				if (modConfig != null)
				{
					DebugUtility.Log($"NPCAIManager: Debug mode enabled: {modConfig.DebugMode}");
				}
				else
				{
					DebugUtility.Log("NPCAIManager: Config not available");
				}
			}
			
			var config = ModContent.GetInstance<OptimizationConfig>();
			
			// Re-enable NPC throttling with debugging
			if (!config.NPCAIThrottling)
			{
				if (updateCounter % 60 == 0)
				{
					DebugUtility.Log("NPCAIManager: NPC AI Throttling is disabled");
				}
				return;
			}
				
			int activeNPCCount = GetActiveNPCCount();
			
			// If we have too many NPCs, start throttling
			bool shouldThrottle = activeNPCCount > config.MaxActiveNPCs;
			
			if (updateCounter % 60 == 0)
			{
				DebugUtility.Log($"NPCAIManager: Active NPCs: {activeNPCCount}, Max: {config.MaxActiveNPCs}, Throttling: {shouldThrottle}");
			}
			
			if (shouldThrottle)
			{
				ApplyNPCThrottling();
			}
			else
			{
				// Reset all NPCs to update normally
				for (int i = 0; i < npcShouldUpdate.Length; i++)
				{
					npcShouldUpdate[i] = true;
				}
			}
		}
		
		private int GetActiveNPCCount()
		{
			int count = 0;
			for (int i = 0; i < Main.npc.Length; i++)
			{
				if (Main.npc[i].active && !Main.npc[i].townNPC)
					count++;
			}
			return count;
		}
		
	private void ApplyNPCThrottling()
	{
		int throttledCount = 0;
			for (int i = 0; i < Main.npc.Length; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.townNPC)
					continue;
					
				// Initialize counter if not present
				if (!npcUpdateCounters.ContainsKey(i))
					npcUpdateCounters[i] = 0;
				
				// Increment counter
				npcUpdateCounters[i]++;
				
				// Skip AI update if counter hasn't reached threshold
				if (npcUpdateCounters[i] < THROTTLED_AI_UPDATE_RATE)
				{
					// Mark NPC to skip AI update
					npcShouldUpdate[npc.whoAmI] = false;
					throttledCount++;
				}
				else
				{
					// Reset counter and allow normal AI update
					npcUpdateCounters[i] = 0;
					npcShouldUpdate[npc.whoAmI] = true;
				}
			}
			
			if (updateCounter % 60 == 0)
			{
				DebugUtility.Log($"NPCAIManager: Throttled {throttledCount} NPCs");
			}

			// store last throttled count for summary
			lastThrottledCount = throttledCount;
	}
		
		public override void PostUpdateNPCs()
		{
			// Re-enable NPC throttling cleanup
			// Reset any modified NPC states
			var config = ModContent.GetInstance<OptimizationConfig>();
			if (!config.NPCAIThrottling)
				return;
				
			// Clean up counters for inactive NPCs
			List<int> keysToRemove = new List<int>();
			foreach (var kvp in npcUpdateCounters)
			{
				if (kvp.Key >= Main.npc.Length || !Main.npc[kvp.Key].active)
				{
					keysToRemove.Add(kvp.Key);
				}
			}
			
			int removedCount = keysToRemove.Count;
			foreach (int key in keysToRemove)
			{
				npcUpdateCounters.Remove(key);
			}
			
			if (removedCount > 0 && updateCounter % 60 == 0)
			{
				DebugUtility.Log($"NPCAIManager: Cleaned up {removedCount} inactive NPC counters");
			}
		}
		
	public override void ClearWorld()
	{
			// Clear all counters when world is unloaded
			int clearedCount = npcUpdateCounters.Count;
			npcUpdateCounters.Clear();
			
			// Reset update flags
			for (int i = 0; i < npcShouldUpdate.Length; i++)
			{
				npcShouldUpdate[i] = true;
			}
			
		DebugUtility.Log($"NPCAIManager: Cleared world, reset {clearedCount} NPC counters");
	}

	public override void PostUpdatePlayers()
	{
		// Emit a 5-second summary of current throttling state
		if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
		{
			int activeNPCs = GetActiveNPCCount();
			var config = ModContent.GetInstance<OptimizationConfig>();
			bool throttling = activeNPCs > config.MaxActiveNPCs;
			DebugUtility.Log($"NPCAIManager Summary: active={activeNPCs}, max={config.MaxActiveNPCs}, throttling={throttling}, lastThrottled={lastThrottledCount}");
		}
	}
		
		// Method that can be called by other systems to check if an NPC should update
		public static bool ShouldNPCUpdate(int npcWhoAmI)
		{
			if (npcWhoAmI < 0 || npcWhoAmI >= npcShouldUpdate.Length)
				return true;
				
			bool shouldUpdate = npcShouldUpdate[npcWhoAmI];
			
			// Only log occasionally to avoid spam
			if (updateCounter % 300 == 0) // Every 5 seconds
			{
				DebugUtility.Log($"NPCAIManager: NPC {npcWhoAmI} should update: {shouldUpdate}");
			}
			
			return shouldUpdate;
		}
	}
}
