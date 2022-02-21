﻿#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Yupgi_alert.Traits
{
	[Desc("Grants a condition based on the amount of actors with an eligible GrantHordeBonus trait around this actor.")]
	public class HordeBonusInfo : ConditionalTraitInfo
	{
		[Desc("The range within eligible GrantHordeBonus actors are considered.")]
		public readonly WDist Range = WDist.FromCells(2);

		[Desc("The maximum vertical range above terrain within the actors get considered.",
			"Ignored if 0 (actors are considered regardless of vertical distance).")]
		public readonly WDist MaximumVerticalOffset = WDist.Zero;

		[Desc("What diplomatic stances are considered.")]
		public readonly Stance ValidStances = Stance.Ally;

		[Desc("Specifies the eligible GrantHordeBonus trait type.")]
		public readonly string HordeType = "horde";

		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("The condition to grant.")]
		public readonly string Condition = null;

		public readonly int Minimum = 4;
		public readonly int Maximum = int.MaxValue;

		public readonly string EnableSound = null;
		public readonly string DisableSound = null;

		public override object Create(ActorInitializer init) { return new HordeBonus(init.Self, this); }
	}

	public class HordeBonus : ConditionalTrait<HordeBonusInfo>, ITick, INotifyCreated, INotifyAddedToWorld, INotifyRemovedFromWorld, INotifyOtherProduction
	{
		readonly Actor self;
		ConditionManager manager;

		int proximityTrigger;
		WPos cachedPosition;
		WDist cachedRange;
		WDist desiredRange;
		WDist cachedVRange;
		WDist desiredVRange;

		bool cachedDisabled = true;

		HashSet<Actor> sources;

		int token = ConditionManager.InvalidConditionToken;

		bool IsEnabled { get { return token != ConditionManager.InvalidConditionToken; } }

		public HordeBonus(Actor self, HordeBonusInfo info)
			: base(info)
		{
			this.self = self;
			cachedRange = Info.Range;
			cachedVRange = Info.MaximumVerticalOffset;
			sources = new HashSet<Actor>();
		}

		void INotifyCreated.Created(Actor self)
		{
			manager = self.Trait<ConditionManager>();
		}

		void INotifyAddedToWorld.AddedToWorld(Actor self)
		{
			cachedPosition = self.CenterPosition;
			proximityTrigger = self.World.ActorMap.AddProximityTrigger(cachedPosition, cachedRange, cachedVRange, ActorEntered, ActorExited);
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			self.World.ActorMap.RemoveProximityTrigger(proximityTrigger);
		}

		void ITick.Tick(Actor self)
		{
			var disabled = IsTraitDisabled;

			if (cachedDisabled != disabled)
			{
				desiredRange = disabled ? WDist.Zero : Info.Range;
				desiredVRange = disabled ? WDist.Zero : Info.MaximumVerticalOffset;
				cachedDisabled = disabled;
			}

			if (self.CenterPosition != cachedPosition || desiredRange != cachedRange || desiredVRange != cachedVRange)
			{
				cachedPosition = self.CenterPosition;
				cachedRange = desiredRange;
				cachedVRange = desiredVRange;
				self.World.ActorMap.UpdateProximityTrigger(proximityTrigger, cachedPosition, cachedRange, cachedVRange);
			}
		}

		void ActorEntered(Actor a)
		{
			if (a == self || a.Disposed || self.Disposed)
				return;

			var stance = self.Owner.Stances[a.Owner];
			if (!Info.ValidStances.HasStance(stance))
				return;

			if (a.TraitsImplementing<GrantHordeBonus>().All(h => h.Info.HordeType != Info.HordeType))
				return;

			sources.Add(a);
			UpdateConditionState();
		}

		void INotifyOtherProduction.UnitProducedByOther(Actor self, Actor producer, Actor produced, string productionType, TypeDictionary init)
		{
			// If the produced Actor doesn't occupy space, it can't be in range
			if (produced.OccupiesSpace == null)
				return;

			// We don't grant conditions when disabled
			if (IsTraitDisabled)
				return;

			// Work around for actors produced within the region not triggering until the second tick
			if ((produced.CenterPosition - self.CenterPosition).HorizontalLengthSquared <= Info.Range.LengthSquared)
			{
				var stance = self.Owner.Stances[produced.Owner];
				if (!Info.ValidStances.HasStance(stance))
					return;

				if (produced.TraitsImplementing<GrantHordeBonus>().All(h => h.Info.HordeType != Info.HordeType))
					return;

				sources.Add(produced);
				UpdateConditionState();
			}
		}

		void ActorExited(Actor a)
		{
			sources.Remove(a);
			UpdateConditionState();
		}

		void UpdateConditionState()
		{
			if (sources.Count() > Info.Minimum && sources.Count() < Info.Maximum)
			{
				if (!IsEnabled)
				{
					token = manager.GrantCondition(self, Info.Condition);
					Game.Sound.Play(SoundType.World, Info.EnableSound, self.CenterPosition);
				}
			}
			else
			{
				if (IsEnabled)
				{
					token = manager.RevokeCondition(self, token);
					Game.Sound.Play(SoundType.World, Info.DisableSound, self.CenterPosition);
				}
			}
		}
	}
}
