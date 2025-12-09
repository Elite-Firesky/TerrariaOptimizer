using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using TerrariaOptimizer.Configs;

namespace TerrariaOptimizer.Systems
{
	public static class DebugUtility
	{
		public static void Log(string message)
		{
			// Only log if debug mode is enabled (client or server)
			if (IsDebugEnabled())
			{
				// Check if the mod instance is still available
				if (TerrariaOptimizer.Instance != null)
				{
					TerrariaOptimizer.Instance.Logger.Info($"[TerrariaOptimizer] {message}");
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
