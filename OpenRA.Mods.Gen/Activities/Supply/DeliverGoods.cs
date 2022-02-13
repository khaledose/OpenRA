﻿#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Yupgi_alert.Traits;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.Yupgi_alert.Activities
{
    class DeliverGoods : Activity
    {
        readonly SupplyCollector collector;
		readonly SupplyCollectorInfo collectorInfo;
        readonly IMove move;
		readonly Mobile mobile;
		readonly Color? targetLineColor;

		public DeliverGoods(Actor self, Color? targetLineColor = null)
		{
			collector = self.Trait<SupplyCollector>();
			collectorInfo = self.Info.TraitInfo<SupplyCollectorInfo>();
            move = self.Trait<IMove>();
			mobile = self.TraitOrDefault<Mobile>();
			this.targetLineColor = targetLineColor;
		}

        public override bool Tick(Actor self)
        {
            if (IsCanceling)
                return true;

            if (collector.deliveryBuilding == null || !collector.deliveryBuilding.IsInWorld || !collectorInfo.DeliveryStances.HasStance(self.Owner.Stances[collector.deliveryBuilding.Owner]))
            {
				collector.deliveryBuilding = collector.ClosestDeliveryBuilding(self);
            }

            if (collector.deliveryBuilding == null || !collector.deliveryBuilding.IsInWorld)
            {
                QueueChild(new Wait(collectorInfo.SearchForDeliveryBuildingDelay));
				return false;
            }

			var center = collector.deliveryBuilding;

			CPos cell;
			var centerTrait = center.Trait<SupplyCenter>();
			if (mobile != null)
				cell = self.ClosestCell(centerTrait.Info.DeliveryOffsets.Where(c => mobile.CanEnterCell(center.Location + c)).Select(c => center.Location + c));
			else
				cell = self.ClosestCell(centerTrait.Info.DeliveryOffsets.Select(c => center.Location + c));

			if (!centerTrait.Info.DeliveryOffsets.Select(c => center.Location + c).Contains(self.Location))
            {
                QueueChild(move.MoveTo(cell, 2));
				return false;
            }

			if (self.Trait<IFacing>() != null)
			{
				if (centerTrait.Info.Facing >= 0 && self.Trait<IFacing>().Facing != centerTrait.Info.Facing)
				{
					QueueChild(new Turn(self, centerTrait.Info.Facing));
					return false;
				}
				else if (centerTrait.Info.Facing == -1)
				{
					var facing = (center.CenterPosition - self.CenterPosition).Yaw.Facing;
					if (self.Trait<IFacing>().Facing != facing)
					{
						QueueChild(new Turn(self, facing));
						return false;
					}
				}
			}

			if (!collector.Waiting)
			{
				collector.Waiting = true;
				QueueChild(new Wait(collectorInfo.DeliveryDelay));
				return false;
			}

			var amount = collector.Amount;
			if (amount < 0)
			{
				self.QueueActivity(new FindGoods(self));
				return true;
			}
			
			if (centerTrait.CanGiveResource(amount))
            {
				var wsb = self.TraitsImplementing<WithSpriteBody>().Where(t => !t.IsTraitDisabled).FirstOrDefault();
				var wsda = self.Info.TraitInfoOrDefault<WithSupplyDeliveryAnimationInfo>();
				var rs = self.TraitOrDefault<RenderSprites>();
				if (rs != null && wsb != null && wsda != null && !collector.DeliveryAnimPlayed)
				{
					wsb.PlayCustomAnimation(self, wsda.DeliverySequence);
					collector.DeliveryAnimPlayed = true;
					QueueChild(new Wait(wsda.WaitDelay));
					return false;
				}

				var wsdo = self.TraitOrDefault<WithSupplyDeliveryOverlay>();
				if (wsb != null && wsdo != null && !collector.DeliveryAnimPlayed)
				{
					if (!wsdo.Visible)
					{
						wsdo.Visible = true;
						wsdo.Anim.PlayThen(wsdo.Info.Sequence, () => wsdo.Visible = false);
						collector.DeliveryAnimPlayed = true;
						QueueChild(new Wait(wsdo.Info.WaitDelay));
						return false;
					}
				}

				collector.Waiting = false;
				collector.DeliveryAnimPlayed = false;
				centerTrait.GiveResource(amount, self.Info.Name);

				collector.Amount = 0;
				collector.CheckConditions(self);
			}
			else
			{
				QueueChild(new Wait(collectorInfo.DeliveryDelay));
				return false;
			}

			self.QueueActivity(new FindGoods(self));
			return true;
        }

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			if (targetLineColor != null && collector.deliveryBuilding != null)
				yield return new TargetLineNode(Target.FromActor(collector.deliveryBuilding), targetLineColor.Value);
		}
    }
}
