using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using TerrariaOptimizer.Configs;

namespace TerrariaOptimizer.Systems
{
    public static class DebugUtility
    {
        private static readonly System.Collections.Generic.Dictionary<string, (uint lastTick, int suppressed)> _rateLimiter = new();

        public static void Log(string message)
        {
            // Only log if debug mode is enabled (client or server)
            if (IsDebugEnabled())
            {
                // Check if the mod instance is still available
                if (TerrariaOptimizer.Instance != null)
                {
                    // Lightweight rate limit to avoid spamming identical messages
                    uint now = Main.GameUpdateCount;
                    const uint window = 300u; // ~5 seconds at 60 FPS
                    if (_rateLimiter.TryGetValue(message, out var state))
                    {
                        if (now - state.lastTick < window)
                        {
                            _rateLimiter[message] = (state.lastTick, state.suppressed + 1);
                            return;
                        }
                    }
                    string suffix = "";
                    if (_rateLimiter.TryGetValue(message, out var flush) && flush.suppressed > 0)
                    {
                        suffix = $" (suppressed {flush.suppressed} repeats)";
                    }
                    TerrariaOptimizer.Instance.Logger.Info($"[TerrariaOptimizer] {message}{suffix}");
                    _rateLimiter[message] = (now, 0);
                }
            }
            else
            {
                // No-op when debug is disabled
            }
        }

        public static void LogAlways(string message)
        {
            // Check if the mod instance is still available
            if (TerrariaOptimizer.Instance != null)
            {
                TerrariaOptimizer.Instance.Logger.Info($"[TerrariaOptimizer] {message}");
            }
        }

        // Method to check if debug mode is enabled
        public static bool IsDebugEnabled()
        {
            // Server uses server-side config; client uses client-side config
            if (Main.netMode == NetmodeID.Server)
            {
                var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();
                return serverConfig != null && serverConfig.ServerDebugMode;
            }

            var clientConfig = ModContent.GetInstance<OptimizationConfig>();
            return clientConfig != null && clientConfig.DebugMode;
        }

        // Method to log config status
        public static void LogConfigStatus()
        {
            var clientConfig = ModContent.GetInstance<OptimizationConfig>();
            var serverConfig = ModContent.GetInstance<OptimizationServerConfig>();

            if (Main.netMode == NetmodeID.Server)
            {
                LogAlways($"Config Status (Server) - ServerDebugMode: {serverConfig?.ServerDebugMode ?? false}");
            }
            else
            {
                if (clientConfig != null)
                {
                    LogAlways($"Config Status (Client) - DebugMode: {clientConfig.DebugMode}, NPCAIThrottling: {clientConfig.NPCAIThrottling}, ProjectileOptimization: {clientConfig.ProjectileOptimization}");
                }
                else
                {
                    LogAlways("Config not available (Client)");
                }
            }
        }
    }
}
