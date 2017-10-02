#region Copyright & License Information
/*
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	class ExternalCaptureActor : Activity
	{
		readonly ExternalCapturable capturable;
		readonly ExternalCaptures[] externalCaptures;
		readonly Mobile mobile;
		readonly Target target;
		readonly ConditionManager conditionManager;
		int capturingToken = ConditionManager.InvalidConditionToken;

		public ExternalCaptureActor(Actor self, Target target)
		{
			this.target = target;
			capturable = target.Actor.Trait<ExternalCapturable>();
			externalCaptures = self.TraitsImplementing<ExternalCaptures>().ToArray();
			mobile = self.Trait<Mobile>();
			conditionManager = self.TraitOrDefault<ConditionManager>();
		}

		public override Activity Tick(Actor self)
		{
			if (target.Type != TargetType.Actor)
				return NextActivity;

			var activeExternalCaptures = externalCaptures.FirstOrDefault(c => !c.IsTraitDisabled);
			if (IsCanceled || !self.IsInWorld || self.IsDead || !target.IsValidFor(self) || capturable.IsTraitDisabled || activeExternalCaptures == null || externalCaptures.All(c => c.IsTraitDisabled))
			{
				EndCapture(self);
				return NextActivity;
			}

			var capturesInfo = activeExternalCaptures.Info;
			var nearest = target.Actor.OccupiesSpace.NearestCellTo(mobile.ToCell);

			if ((nearest - mobile.ToCell).LengthSquared > 2)
				return ActivityUtils.SequenceActivities(new MoveAdjacentTo(self, target), this);

			if (!capturable.CaptureInProgress)
			{
				BeginCapture(self);
			}
			else
			{
				if (capturable.Captor != self) return NextActivity;

				if (capturable.CaptureProgressTime % 25 == 0)
				{
					self.World.Add(new FlashTarget(target.Actor, self.Owner));
					self.World.Add(new FlashTarget(self));
				}

				if (capturable.CaptureProgressTime == capturable.Info.CaptureCompleteTime * 25)
				{
					self.World.AddFrameEndTask(w =>
					{
						if (target.Actor.IsDead)
							return;

						var oldOwner = target.Actor.Owner;

						target.Actor.ChangeOwner(self.Owner);

						foreach (var t in target.Actor.TraitsImplementing<INotifyCapture>())
							t.OnCapture(target.Actor, self, oldOwner, self.Owner);

						EndCapture(self);

						if (capturesInfo.CaptureCompleteVoice != null)
							self.PlayVoice(capturesInfo.CaptureCompleteVoice);

						if (self.Owner.Stances[oldOwner].HasStance(capturesInfo.PlayerExperienceStances))
						{
							var exp = self.Owner.PlayerActor.TraitOrDefault<PlayerExperience>();
							if (exp != null)
								exp.GiveExperience(capturesInfo.PlayerExperience);
						}

						if (capturesInfo != null && capturesInfo.ConsumeActor)
							self.Dispose();
					});
				}
			}

			return this;
		}

		void BeginCapture(Actor self)
		{
			var capturesInfo = externalCaptures.FirstOrDefault(c => !c.IsTraitDisabled).Info;
			
			capturable.BeginCapture(self);
			if (conditionManager != null && !string.IsNullOrEmpty(capturesInfo.CapturingCondition) && capturingToken == ConditionManager.InvalidConditionToken)
				capturingToken = conditionManager.GrantCondition(self, capturesInfo.CapturingCondition);
		}

		void EndCapture(Actor self)
		{
			if (capturable.CaptureInProgress)
				capturable.EndCapture();
			if (capturingToken != ConditionManager.InvalidConditionToken)
				capturingToken = conditionManager.RevokeCondition(self, capturingToken);
		}
	}
}
