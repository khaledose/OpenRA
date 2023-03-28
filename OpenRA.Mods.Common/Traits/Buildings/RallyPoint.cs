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

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Used to waypoint units after production or repair is finished.")]
	public class RallyPointInfo : ITraitInfo
	{
		public readonly string Image = "rallypoint";

		[Desc("Width (in pixels) of the rallypoint line.")]
		public readonly int LineWidth = 1;

		[SequenceReference("Image")]
		public readonly string FlagSequence = "flag";

		[SequenceReference("Image")]
		public readonly string CirclesSequence = "circles";

		public readonly string Cursor = "ability";

		[PaletteReference("IsPlayerPalette")]
		[Desc("Custom indicator palette name")]
		public readonly string Palette = "player";

		[Desc("Custom palette is a player palette BaseName")]
		public readonly bool IsPlayerPalette = true;

		[Desc("A list of 0 or more offsets defining the initial rally point path.")]
		public readonly CVec[] Path = { };

		[NotificationReference("Speech")]
		[Desc("The speech notification to play when setting a new rallypoint.")]
		public readonly string Notification = null;

		public object Create(ActorInitializer init) { return new RallyPoint(init.Self, this); }
	}

	public class RallyPoint : IIssueOrder, IResolveOrder, INotifyOwnerChanged, INotifyCreated
	{
		const string OrderID = "SetRallyPoint";
		const uint ForceSet = 1;

		public List<CPos> Path;

		public RallyPointInfo Info;
		public string PaletteName { get; private set; }

		// Keep track of rally pointed acceptor actors
		bool dirty = true;

		Actor cachedResult = null;

		public void ResetPath(Actor self)
		{
			dirty = true;
			Path = Info.Path.Select(p => self.Location + p).ToList();
		}

		public RallyPoint(Actor self, RallyPointInfo info)
		{
			Info = info;
			ResetPath(self);
			PaletteName = info.IsPlayerPalette ? info.Palette + self.Owner.InternalName : info.Palette;
		}

		void INotifyCreated.Created(Actor self)
		{
			// Display only the first level of priority
			var priorityExits = self.Info.TraitInfos<ExitInfo>()
				.GroupBy(e => e.Priority)
				.FirstOrDefault();

			var exits = priorityExits != null ? priorityExits.ToArray() : new ExitInfo[0];
			self.World.Add(new RallyPointIndicator(self, this, exits));
			dirty = true;
		}

		public void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			if (Info.IsPlayerPalette)
				PaletteName = Info.Palette + newOwner.InternalName;

			ResetPath(self);
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get { yield return new RallyPointOrderTargeter(Info.Cursor); }
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, Target target, bool queued)
		{
			if (order.OrderID == OrderID)
			{
				Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", Info.Notification, self.Owner.Faction.InternalName);

				return new Order(order.OrderID, self, target, queued)
				{
					SuppressVisualFeedback = true,
					ExtraData = ((RallyPointOrderTargeter)order).ForceSet ? ForceSet : 0
				};
			}

			return null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderID)
				return;

			if (!order.Queued)
				Path.Clear();

			Path.Add(self.World.Map.CellContaining(order.Target.CenterPosition));
		}

		Actor GetRallyAcceptor(Actor self, CPos location)
		{
			if (!dirty)
			{
				if (cachedResult == null)
					return null;

				if (!cachedResult.IsDead && !cachedResult.Disposed)
					return cachedResult;
			}

			var actors = self.World.ActorMap.GetActorsAt(location).Where(
				a => a.TraitsImplementing<IAcceptsRallyPoint>().Count() > 0);

			dirty = false;

			if (!actors.Any())
			{
				cachedResult = null;
				return null;
			}

			// If we have multiple of them, let's just go for first.
			cachedResult = actors.First();
			return cachedResult;
		}

		// self isn't the rally point but the one that has rally point trait.
		// unit is the one that is to follow the rally point.
		public void QueueRallyOrder(Actor self, Actor unit)
		{
			if (unit.TraitOrDefault<IMove>() == null)
				throw new InvalidOperationException("How come rally point mover not have IMove trait? Actor: " + unit.ToString());

			foreach (var location in rallyPoint.Path)
			{
				var rallyAcceptor = GetRallyAcceptor(self, location);
				if (rallyAcceptor == null)
				{
					unit.QueueActivity(new AttackMoveActivity(unit, () => unit.Trait<IMove>().MoveTo(location, 1, evaluateNearestMovableCell: true, targetLineColor: Color.OrangeRed)));
					return;
				}

				var ars = rallyAcceptor.TraitsImplementing<IAcceptsRallyPoint>();
				if (ars.Count() > 1)
					throw new InvalidOperationException(
						"Actor {0} has multiple traits implementing IAcceptsRallyPoint!".F(rallyAcceptor.ToString()));

				var ar = ars.First();
				if (ar.IsAcceptableActor(unit, rallyAcceptor))
					unit.QueueActivity(ar.RallyActivities(unit, rallyAcceptor));
				else
					unit.QueueActivity(new AttackMoveActivity(unit, () => unit.Trait<IMove>().MoveTo(location, 1, evaluateNearestMovableCell: true, targetLineColor: Color.OrangeRed)));
			}
		}

		public static bool IsForceSet(Order order)
		{
			return order.OrderString == OrderID && order.ExtraData == ForceSet;
		}

		class RallyPointOrderTargeter : IOrderTargeter
		{
			readonly string cursor;

			public RallyPointOrderTargeter(string cursor)
			{
				this.cursor = cursor;
			}

			public string OrderID { get { return "SetRallyPoint"; } }
			public int OrderPriority { get { return 0; } }
			public bool TargetOverridesSelection(Actor self, Target target, List<Actor> actorsAt, CPos xy, TargetModifiers modifiers) { return true; }
			public bool ForceSet { get; private set; }
			public bool IsQueued { get; protected set; }

			public bool CanTarget(Actor self, Target target, List<Actor> othersAtTarget, ref TargetModifiers modifiers, ref string cursor)
			{
				if (target.Type != TargetType.Terrain)
					return false;

				IsQueued = modifiers.HasModifier(TargetModifiers.ForceQueue);

				var location = self.World.Map.CellContaining(target.CenterPosition);
				if (self.World.Map.Contains(location))
				{
					cursor = this.cursor;

					// Notify force-set 'RallyPoint' order watchers with Ctrl and only if this is the only building of its type selected
					if (modifiers.HasModifier(TargetModifiers.ForceAttack))
					{
						var selfName = self.Info.Name;
						if (!self.World.Selection.Actors.Any(a => a.Info.Name == selfName && a.ActorID != self.ActorID))
							ForceSet = true;
					}

					return true;
				}

				return false;
			}
		}
	}
}
