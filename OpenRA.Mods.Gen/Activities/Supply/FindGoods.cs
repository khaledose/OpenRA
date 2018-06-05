﻿using System;
using System.Drawing;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Yupgi_alert.Traits;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.Yupgi_alert.Activities
{
    public class FindGoods : Activity
    {
        readonly SupplyCollector collector;
		readonly SupplyCollectorInfo collectorInfo;
		readonly IMove move;
		readonly Mobile mobile;

		public FindGoods(Actor self)
		{
			collector = self.Trait<SupplyCollector>();
			collectorInfo = self.Info.TraitInfo<SupplyCollectorInfo>();
            move = self.Trait<IMove>();
			mobile = self.TraitOrDefault<Mobile>();
		}

        public override Activity Tick(Actor self)
        {
            if (IsCanceled || NextActivity != null)
                return NextActivity;

            if (collector.collectionBuilding == null || !collector.collectionBuilding.IsInWorld || !collectorInfo.CollectionStances.HasStance(self.Owner.Stances[collector.collectionBuilding.Owner]) || collector.collectionBuilding.Trait<SupplyDock>().IsEmpty)
            {
				collector.collectionBuilding = collector.ClosestTradeBuilding(self);
            }

            if (collector.collectionBuilding == null || !collector.collectionBuilding.IsInWorld)
            {
                return ActivityUtils.SequenceActivities(new Wait(collectorInfo.SearchForCollectionBuildingDelay), this);
            }

			var dock = collector.collectionBuilding;
			var center = collector.deliveryBuilding;
			self.SetTargetLine(Target.FromActor(dock), Color.Green, false);

			CPos cell;
			var dockTrait = dock.Trait<SupplyDock>();
			var centerTrait = center == null || !center.IsInWorld ? null : center.Trait<SupplyCenter>();
			var offsets = (mobile == null || collectorInfo.IsAircraft) && dockTrait.Info.AircraftCollectionOffsets.Any() ? dockTrait.Info.AircraftCollectionOffsets : dockTrait.Info.CollectionOffsets;
			var deliveryOffsets = centerTrait != null ? centerTrait.Info.DeliveryOffsets : null;
			if (mobile != null)
				cell = self.ClosestCell(offsets.Select(c => dock.Location + c).Where(c => mobile.CanEnterCell(c) && (centerTrait == null || !deliveryOffsets.Select(d => center.Location + d).Contains(c))));
			else
				cell = self.ClosestCell(offsets.Select(c => dock.Location + c).Where(c => centerTrait == null || !deliveryOffsets.Select(d => center.Location + d).Contains(c)));

			if (!offsets.Select(c => dock.Location + c).Where(c => centerTrait == null || !deliveryOffsets.Select(d => center.Location + d).Contains(c)).Contains(self.Location))
			{
                return ActivityUtils.SequenceActivities(move.MoveTo(cell, 2), this);
            }

			if (self.TraitOrDefault<IFacing>() != null)
			{
				if (dockTrait.Info.Facing >= 0 && self.Trait<IFacing>().Facing != dockTrait.Info.Facing)
				{
					return ActivityUtils.SequenceActivities(new Turn(self, dockTrait.Info.Facing), this);
				}
				else if (dockTrait.Info.Facing == -1)
				{
					var facing = (dock.CenterPosition - self.CenterPosition).Yaw.Facing;
					if (self.Trait<IFacing>().Facing != facing)
					{
						return ActivityUtils.SequenceActivities(new Turn(self, facing), this);
					}
				}
			}

			if (!collector.Waiting)
			{
				collector.Waiting = true;
				return ActivityUtils.SequenceActivities(new Wait(collectorInfo.CollectionDelay), this);
			}

			var wsb = self.TraitsImplementing<WithSpriteBody>().Where(t => !t.IsTraitDisabled).FirstOrDefault();
			var wsco = self.TraitOrDefault<WithSupplyCollectionOverlay>();
			if (wsb != null && wsco != null && !collector.DeliveryAnimPlayed)
			{
				if (!wsco.Visible)
				{
					wsco.Visible = true;
					wsco.Anim.PlayThen(wsco.Info.Sequence, () => wsco.Visible = false);
					collector.DeliveryAnimPlayed = true;
					return ActivityUtils.SequenceActivities(new Wait(wsco.Info.WaitDelay), this);
				}
			}

			collector.Waiting = false;
			collector.DeliveryAnimPlayed = false;
			var cash = Math.Min(collectorInfo.Capacity - collector.Amount, dockTrait.Amount);
			collector.Amount = cash;
			dockTrait.Amount = dockTrait.Amount - cash;
			collector.CheckConditions(self);
			dockTrait.CheckConditions(dock);

			return new DeliverGoods(self);
        }
    }
}
