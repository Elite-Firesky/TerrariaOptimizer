using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TerrariaOptimizer.Systems;

namespace TerrariaOptimizer
{
	public class TerrariaOptimizer : Mod
	{
		public TerrariaOptimizer()
		{
			Instance = this;
		}

		public static TerrariaOptimizer Instance { get; private set; }

		public override void Load()
		{
			// Initialization of optimization systems
			DebugUtility.LogAlways("TerrariaOptimizer mod loaded");
			DebugUtility.LogConfigStatus();
		}

		public override void Unload()
		{
			DebugUtility.LogAlways("TerrariaOptimizer mod unloaded");
			Instance = null;
		}
		
		public override void PostSetupContent()
		{
			// Perform any post-setup optimizations
			DebugUtility.LogAlways("TerrariaOptimizer post setup content completed");
		}
	}
}