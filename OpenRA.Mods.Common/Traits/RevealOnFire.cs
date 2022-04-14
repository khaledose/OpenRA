#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Mods.Common.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Reveal this actor to the target's owner when attacking.")]
	public class RevealOnFireInfo : ConditionalTraitInfo
	{
		[Desc("The armament types which trigger revealing.")]
		public readonly string[] ArmamentNames = { "primary", "secondary" };

		[Desc("Player relationships relative to the target player this actor will be revealed to during firing.")]
		public readonly PlayerRelationship RevealForRelationships = PlayerRelationship.Ally;

		[Desc("Duration of the reveal.")]
		public readonly int Duration = 25;

		[Desc("Radius of the reveal around this actor.")]
		public readonly WDist Radius = new WDist(1536);

		[Desc("Can this actor be revealed through shroud generated by the `" + nameof(CreatesShroud) + "` trait?")]
		public readonly bool RevealGeneratedShroud = true;

		public override object Create(ActorInitializer init) { return new RevealOnFire(this); }
	}

	public class RevealOnFire : ConditionalTrait<RevealOnFireInfo>, INotifyAttack
	{
		readonly RevealOnFireInfo info;

		public RevealOnFire(RevealOnFireInfo info)
			: base(info)
		{
			this.info = info;
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			if (IsTraitDisabled)
				return;

			if (!info.ArmamentNames.Contains(a.Info.Name))
				return;

			var targetPlayer = GetTargetPlayer(target);

			if (targetPlayer != null && targetPlayer.WinState == WinState.Undefined)
			{
				self.World.AddFrameEndTask(w => w.Add(new RevealShroudEffect(self.CenterPosition, info.Radius,
					info.RevealGeneratedShroud ? Shroud.SourceType.Visibility : Shroud.SourceType.PassiveVisibility,
					targetPlayer, info.RevealForRelationships, duration: info.Duration)));
			}
		}

		Player GetTargetPlayer(in Target target)
		{
			if (target.Type == TargetType.Actor)
				return target.Actor.Owner;
			else if (target.Type == TargetType.FrozenActor && target.FrozenActor.Actor != null && !target.FrozenActor.Actor.IsDead)
				return target.FrozenActor.Actor.Owner;

			return null;
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }
	}
}
