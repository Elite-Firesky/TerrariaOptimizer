using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TerrariaOptimizer.Configs
{
    public class OptimizationConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.Headers.PerformanceSettings")]

        [DefaultValue(false)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.DebugMode.Tooltip")]
        public bool DebugMode { get; set; } = false;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.NPCAIThrottling.Tooltip")]
        public bool NPCAIThrottling { get; set; } = true;

        [DefaultValue(50)]
        [Range(10, 200)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.MaxActiveNPCs.Tooltip")]
        public int MaxActiveNPCs { get; set; } = 50;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.ProjectileOptimization.Tooltip")]
        public bool ProjectileOptimization { get; set; } = true;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.OffscreenOptimization.Tooltip")]
        public bool OffscreenOptimization { get; set; } = true;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.TileUpdateReduction.Tooltip")]
        public bool TileUpdateReduction { get; set; } = true;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.GarbageCollectionOptimization.Tooltip")]
        public bool GarbageCollectionOptimization { get; set; } = true;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.MemoryMonitoring.Tooltip")]
        public bool MemoryMonitoring { get; set; } = true;

        [Header("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.Headers.MemorySettings")]

        [DefaultValue(60)]
        [Range(5, 600)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.ClientMemoryCleanupIntervalSeconds.Tooltip")]
        public int ClientMemoryCleanupIntervalSeconds { get; set; } = 60;

        [DefaultValue(8192)]
        [Range(512, 32768)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.ClientMemoryHardThresholdMB.Tooltip")]
        public int ClientMemoryHardThresholdMB { get; set; } = 8192;

        [DefaultValue(false)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.ClientAllowForcedGC.Tooltip")]
        public bool ClientAllowForcedGC { get; set; } = false;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.MultiCoreUtilization.Tooltip")]
        public bool MultiCoreUtilization { get; set; } = true;

        [Header("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.Headers.VisualSettings")]

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.ParticleEffectReduction.Tooltip")]
        public bool ParticleEffectReduction { get; set; } = true;

        // Optional rain optimization: conservatively cull far-away droplets under stress
        [DefaultValue(false)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.RainOptimization.Tooltip")]
        public bool RainOptimization { get; set; } = false;

        [DefaultValue(4)]
        [Range(2, 12)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.RainCullStride.Tooltip")]
        public int RainCullStride { get; set; } = 4;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.RainCullOnlyWhenStressed.Tooltip")]
        public bool RainCullOnlyWhenStressed { get; set; } = true;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.LightingPerformanceMode.Tooltip")]
        public bool LightingPerformanceMode { get; set; } = true;

        [DefaultValue(true)]
        [TooltipKey("$Mods.TerrariaOptimizer.Configs.OptimizationConfig.TextureOptimization.Tooltip")]
        public bool TextureOptimization { get; set; } = true;
    }
}
