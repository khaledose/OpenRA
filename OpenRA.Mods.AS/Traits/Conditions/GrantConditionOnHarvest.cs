#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	public class GrantConditionOnHarvestInfo : TraitInfo, Requires<HarvesterInfo>
	{
		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("The condition to grant after harvesting.")]
		public readonly string Condition = null;

		[Desc("How long the condition lasts for.")]
		public readonly int Duration = 25;

		public override object Create(ActorInitializer init) { return new GrantConditionOnHarvest(init, this); }
	}

	public class GrantConditionOnHarvest : INotifyHarvesterAction, ITick
	{
		public readonly GrantConditionOnHarvestInfo Info;

		int token = Actor.InvalidConditionToken;
		int timer;

		public GrantConditionOnHarvest(ActorInitializer init, GrantConditionOnHarvestInfo info)
		{
			Info = info;
		}

		void ITick.Tick(Actor self)
		{
			if (token == Actor.InvalidConditionToken)
				return;

			if (--timer <= 0)
				token = self.RevokeCondition(token);
		}

		void INotifyHarvesterAction.Harvested(Actor self, string resourceType)
		{
			timer = Info.Duration;
			if (token == Actor.InvalidConditionToken)
				token = self.GrantCondition(Info.Condition);
		}

		void INotifyHarvesterAction.Docked() { }
		void INotifyHarvesterAction.Undocked() { }
		void INotifyHarvesterAction.MovingToResources(Actor self, CPos targetCell) { }
		void INotifyHarvesterAction.MovingToRefinery(Actor self, Actor refineryActor, bool forceDelivery) { }
		void INotifyHarvesterAction.MovementCancelled(Actor self) { }
	}
}
