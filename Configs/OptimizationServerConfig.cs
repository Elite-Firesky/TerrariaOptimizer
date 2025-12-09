using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TerrariaOptimizer.Configs
{
    public class OptimizationServerConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        [Header("$Mods.TerrariaOptimizer.Configs.OptimizationServerConfig.Headers.ServerSettings")]

        [DefaultValue(false)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationServerConfig.ServerDebugMode.Tooltip")]
        public bool ServerDebugMode { get; set; } = false;

        [DefaultValue(60)]
        [Range(5, 600)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationServerConfig.MemoryCleanupIntervalSeconds.Tooltip")]
        public int MemoryCleanupIntervalSeconds { get; set; } = 60;

        [DefaultValue(8192)]
        [Range(512, 32768)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationServerConfig.MemoryHardThresholdMB.Tooltip")]
        public int MemoryHardThresholdMB { get; set; } = 8192;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationServerConfig.AllowForcedGC.Tooltip")]
        public bool AllowForcedGC { get; set; } = true;

        [Header("$Mods.TerrariaOptimizer.Configs.OptimizationServerConfig.Headers.Networking")]

        // Suppress non-critical netUpdate sends for distant offscreen entities
        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationServerConfig.NetworkTrafficReduction.Tooltip")]
        public bool NetworkTrafficReduction { get; set; } = true;

        // Interval in ticks for server to allow netUpdate on low-priority entities
        [DefaultValue(3)]
        [Range(1, 10)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationServerConfig.NetworkUpdateInterval.Tooltip")]
        public int NetworkUpdateInterval { get; set; } = 3;

        // Offscreen distance threshold in pixels to consider entity low-priority for networking
        [DefaultValue(1600)]
        [Range(800, 4000)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationServerConfig.NetworkOffscreenDistancePx.Tooltip")]
        public int NetworkOffscreenDistancePx { get; set; } = 1600;
    }
}
