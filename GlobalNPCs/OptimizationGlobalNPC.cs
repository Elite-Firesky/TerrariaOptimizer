using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using TerrariaOptimizer.Systems;

namespace TerrariaOptimizer.GlobalNPCs
{
    public class OptimizationGlobalNPC : GlobalNPC
    {
        public override bool PreAI(NPC npc)
        {
            // Re-enable NPC AI throttling with debugging
            // First check if NPC AI should be updated based on our throttling system
            // Use the NPCAIManager to determine if this NPC should update
            // Additional offscreen gating on client to reduce work for distant NPCs
            if (Main.netMode != NetmodeID.Server && !OffscreenEntityOptimizer.ShouldEntityUpdate(npc.Center))
            {
                if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
                {
                    DebugUtility.Log($"Skipping offscreen NPC update {npc.whoAmI} ({npc.FullName})");
                }
                return false;
            }

            if (!NPCAIManager.ShouldNPCUpdate(npc.whoAmI))
            {
                if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
                {
                    DebugUtility.Log($"Skipping AI update for NPC {npc.whoAmI} ({npc.FullName})");
                }
                return false; // Skip AI update
            }

            if (DebugUtility.IsDebugEnabled() && Main.GameUpdateCount % 300 == 0)
            {
                DebugUtility.Log($"Allowing AI update for NPC {npc.whoAmI} ({npc.FullName})");
            }
            return true; // Allow normal AI update
        }

        public override void PostAI(NPC npc)
        {
            // Client-side: mark NPC texture as used to feed TextureOptimizer LRU
            if (Main.netMode != NetmodeID.Server)
            {
                var texOpt = ModContent.GetInstance<TextureOptimizer>();
                texOpt.TouchNpc(npc.type);
            }

            // Force immediate updates for critical NPC cases
            if (Main.netMode == NetmodeID.Server && Systems.MultiplayerOptimizer.ShouldForceImmediateUpdateNpc(npc))
            {
                npc.netUpdate = true;
                Systems.MultiplayerOptimizer.RecordNpcForced();
            }

            // Server-side: throttle net updates for low-priority offscreen NPCs (critical-aware)
            if (Main.netMode == NetmodeID.Server && Systems.MultiplayerOptimizer.ShouldThrottleNpc(npc))
            {
                if (!Systems.MultiplayerOptimizer.ShouldSendNetworkUpdate())
                {
                    npc.netUpdate = false;
                    Systems.MultiplayerOptimizer.RecordNpcThrottled();
                }
            }
        }
    }
}
