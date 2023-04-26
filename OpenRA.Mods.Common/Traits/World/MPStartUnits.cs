#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Used by SpawnMPUnits. Attach these to the world actor. You can have multiple variants by adding @suffixes.")]
	public class MPStartUnitsInfo : TraitInfo<MPStartUnits>
	{
		[Desc("Internal class ID.")]
		public readonly string Class = "none";

		[Desc("Exposed via the UI to the player.")]
		public readonly string ClassName = "Unlabeled";

		[Desc("Only available when selecting one of these factions.", "Leave empty for no restrictions.")]
		public readonly HashSet<string> Factions = new HashSet<string>();

		[Desc("The actor at the center, usually the mobile construction vehicle.")]
		[ActorReference]
		public readonly string BaseActor = null;

		[Desc("A group of units ready to defend or scout.")]
		[ActorReference]
		public readonly string[] SupportActors = { };

		[Desc("A group of buildings ready to work.")]
		public readonly string[] SupportBuildings = { };

		[Desc("A group of proxy actors that will be at the start.")]
		public readonly string[] SupportProxyActors = { };

		[Desc("Inner radius for spawning support actors")]
		public readonly int InnerSupportRadius = 2;

		[Desc("Outer radius for spawning support actors")]
		public readonly int OuterSupportRadius = 4;

		[Desc("Inner radius for spawning support buildings")]
		public readonly int InnerBuildingRadius = 3;

		[Desc("Outer radius for spawning support buildings")]
		public readonly int OuterBuildingRadius = 5;

		[Desc("Initial facing of BaseActor. Leave undefined for random facings.")]
		public readonly WAngle? BaseActorFacing = new WAngle(512);

		[Desc("Offset of base actor from actual spawn position.")]
		public readonly CVec BaseActorOffset = CVec.Zero;

		[Desc("Initial facing of SupportActors. Leave undefined for random facings.")]
		public readonly WAngle? SupportActorsFacing = null;
	}

	public class MPStartUnits { }
}
