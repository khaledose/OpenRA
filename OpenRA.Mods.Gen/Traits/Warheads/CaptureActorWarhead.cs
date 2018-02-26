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
using OpenRA.Traits;

namespace OpenRA.Mods.Yupgi_alert.Warheads
{
	[Desc("Allows the firer to capture targets. This warhead interacts with the Capturable trait.")]
	public class CaptureActorWarhead : WarheadAS
	{
		[Desc("Range of targets to be captured.")]
		public readonly WDist Range = new WDist(64);

		[Desc("Types of actors that it can capture, as long as the type also exists in the Capturable Type: trait.")]
		public readonly HashSet<string> CaptureTypes = new HashSet<string> { "building" };

		[Desc("If set, the target will be captured regardless of threshold.")]
		public readonly bool IgnoreCaptureThreshold = false;

		[Desc("Experience granted to the capturing actor.")]
		public readonly int Experience = 0;

		[Desc("Stance that the structure's previous owner needs to have for the capturing actor to receive Experience.")]
		public readonly Stance ExperienceStances = Stance.Enemy;

		[Desc("Experience granted to the capturing player.")]
		public readonly int PlayerExperience = 0;

		[Desc("Stance that the structure's previous owner needs to have for the capturing player to receive Experience.")]
		public readonly Stance PlayerExperienceStances = Stance.Enemy;

		public override void DoImpact(Target target, Actor firedBy, IEnumerable<int> damageModifiers)
		{
			var pos = target.CenterPosition;

			if (!IsValidImpact(pos, firedBy))
				return;

			var availableActors = firedBy.World.FindActorsInCircle(pos, Range);

			foreach (var a in availableActors)
			{
				if (!IsValidAgainst(a, firedBy))
					continue;

				var activeShapes = a.TraitsImplementing<HitShape>().Where(Exts.IsTraitEnabled);
				if (!activeShapes.Any())
					continue;

				var distance = activeShapes.Min(t => t.Info.Type.DistanceFromEdge(pos, a));

				if (distance > Range)
					continue;

				var capturable = a.TraitsImplementing<Capturable>().ToArray();
				var activeCapturable = capturable.FirstOrDefault(c => !c.IsTraitDisabled);
				var building = a.TraitOrDefault<Building>();
				var health = a.Trait<Health>();

				if (a.IsDead || activeCapturable.BeingCaptured)
					continue;

				if (building != null && !building.Lock())
					continue;

				firedBy.World.AddFrameEndTask(w =>
				{
					if (building != null && building.Locked)
						building.Unlock();

					if (a.IsDead || activeCapturable.BeingCaptured)
						return;

					var lowEnoughHealth = health.HP <= activeCapturable.Info.CaptureThreshold * health.MaxHP / 100;
					if (IgnoreCaptureThreshold || lowEnoughHealth || a.Owner.NonCombatant)
					{
						var oldOwner = a.Owner;

						a.ChangeOwner(firedBy.Owner);

						foreach (var t in a.TraitsImplementing<INotifyCapture>())
							t.OnCapture(a, firedBy, oldOwner, a.Owner);

						if (building != null && building.Locked)
							building.Unlock();

						if (firedBy.Owner.Stances[oldOwner].HasStance(ExperienceStances))
						{
							var exp = firedBy.TraitOrDefault<GainsExperience>();
							if (exp != null)
								exp.GiveExperience(Experience);
						}

						if (firedBy.Owner.Stances[oldOwner].HasStance(PlayerExperienceStances))
						{
							var exp = firedBy.Owner.PlayerActor.TraitOrDefault<PlayerExperience>();
							if (exp != null)
								exp.GiveExperience(PlayerExperience);
						}
					}
				});
			}
		}

		public override bool IsValidAgainst(Actor victim, Actor firedBy)
		{
			var capturable = victim.TraitsImplementing<Capturable>().ToArray();
			var activeCapturable = capturable.FirstOrDefault(c => !c.IsTraitDisabled);
			if (activeCapturable == null || !CaptureTypes.Overlaps(activeCapturable.Info.Types))
				return false;

			var playerRelationship = victim.Owner.Stances[firedBy.Owner];
			if (playerRelationship == Stance.Ally && !activeCapturable.Info.ValidStances.HasStance(Stance.Ally))
				return false;

			if (playerRelationship == Stance.Enemy && !activeCapturable.Info.ValidStances.HasStance(Stance.Enemy))
				return false;

			if (playerRelationship == Stance.Neutral && !activeCapturable.Info.ValidStances.HasStance(Stance.Neutral))
				return false;

			return base.IsValidAgainst(victim, firedBy);
		}
	}
}
