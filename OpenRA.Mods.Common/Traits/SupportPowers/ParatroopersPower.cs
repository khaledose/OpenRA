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
	public class ParatroopersPowerInfo : SupportPowerInfo
	{
		[FieldLoader.Require]
		public readonly Dictionary<int, string> UnitTypes = new Dictionary<int, string>();

		[FieldLoader.Require]
		public readonly Dictionary<int, int> SquadSizes = new Dictionary<int, int>();

		public readonly WVec SquadOffset = new WVec(-1536, 1536, 0);

		[NotificationReference("Speech")]
		[Desc("Notification to play when entering the drop zone.")]
		public readonly string ReinforcementsArrivedSpeechNotification = null;

		[Desc("Number of facings that the delivery aircraft may approach from.")]
		public readonly int QuantizedFacings = 32;

		[Desc("Spawn and remove the plane this far outside the map.")]
		public readonly WDist Cordon = new WDist(5120);

		[FieldLoader.Require]
		[Desc("Troops to be delivered. They will be distributed between the planes if SquadSize > 1.")]
		public readonly Dictionary<int, string[]> DropItems = new Dictionary<int, string[]>();

		[Desc("Risks stuck units when they don't have the Paratrooper trait.")]
		public readonly bool AllowImpassableCells = false;

		[ActorReference]
		[Desc("Actor to spawn when the paradrop starts.")]
		public readonly string CameraActor = null;

		[Desc("Amount of time (in ticks) to keep the camera alive while the passengers drop.")]
		public readonly int CameraRemoveDelay = 85;

		[Desc("Enables the player directional targeting")]
		public readonly bool UseDirectionalTarget = false;

		[Desc("Animation used to render the direction arrows.")]
		public readonly string DirectionArrowAnimation = null;

		[Desc("Palette for direction cursor animation.")]
		public readonly string DirectionArrowPalette = "chrome";

		[Desc("Weapon range offset to apply during the beacon clock calculation.")]
		public readonly WDist BeaconDistanceOffset = WDist.FromCells(4);

		public override object Create(ActorInitializer init) { return new ParatroopersPower(init.Self, this); }
	}

	public class ParatroopersPower : SupportPower
	{
		readonly ParatroopersPowerInfo info;

		public ParatroopersPower(Actor self, ParatroopersPowerInfo info)
			: base(self, info)
		{
			this.info = info;
		}

		public override void SelectTarget(Actor self, string order, SupportPowerManager manager)
		{
			if (info.UseDirectionalTarget)
			{
				Game.Sound.PlayToPlayer(SoundType.UI, manager.Self.Owner, Info.SelectTargetSound);
				Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech",
					Info.SelectTargetSpeechNotification, self.Owner.Faction.InternalName);

				self.World.OrderGenerator = new SelectDirectionalTarget(self.World, order, manager, Info.Cursor, info.DirectionArrowAnimation, info.DirectionArrowPalette);
			}
			else
				base.SelectTarget(self, order, manager);
		}

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
			base.Activate(self, order, manager);

			var facing = info.UseDirectionalTarget && order.ExtraData != uint.MaxValue ? (int)order.ExtraData : -1;
			SendParatroopers(self, order.Target.CenterPosition, facing);
		}

		public Pair<Actor[], Actor[]> SendParatroopers(Actor self, WPos target, int facing = -1)
		{
			var aircraft = new List<Actor>();
			var units = new List<Actor>();

			var info = Info as ParatroopersPowerInfo;

			if (facing < 0)
				facing = 256 * self.World.SharedRandom.Next(info.QuantizedFacings) / info.QuantizedFacings;

			var utLower = info.UnitTypes.First(ut => ut.Key == GetLevel()).Value.ToLowerInvariant();
			ActorInfo unitType;
			if (!self.World.Map.Rules.Actors.TryGetValue(utLower, out unitType))
				throw new YamlException("Actors ruleset does not include the entry '{0}'".F(utLower));

			var altitude = unitType.TraitInfo<AircraftInfo>().CruiseAltitude.Length;
			var dropRotation = WRot.FromFacing(facing);
			var delta = new WVec(0, -1024, 0).Rotate(dropRotation);
			target = target + new WVec(0, 0, altitude);
			var startEdge = target - (self.World.Map.DistanceToEdge(target, -delta) + info.Cordon).Length * delta / 1024;
			var finishEdge = target + (self.World.Map.DistanceToEdge(target, delta) + info.Cordon).Length * delta / 1024;

			Actor camera = null;
			Beacon beacon = null;
			var aircraftInRange = new Dictionary<Actor, bool>();

			Action<Actor> onEnterRange = a =>
			{
				// Spawn a camera and remove the beacon when the first plane enters the target area
				if (info.CameraActor != null && camera == null && !aircraftInRange.Any(kv => kv.Value))
				{
					self.World.AddFrameEndTask(w =>
					{
						camera = w.CreateActor(info.CameraActor, new TypeDictionary
						{
							new LocationInit(self.World.Map.CellContaining(target)),
							new OwnerInit(self.Owner),
						});
					});
				}

				RemoveBeacon(beacon);

				if (!aircraftInRange.Any(kv => kv.Value))
					Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech",
						info.ReinforcementsArrivedSpeechNotification, self.Owner.Faction.InternalName);

				aircraftInRange[a] = true;
			};

			Action<Actor> onExitRange = a =>
			{
				aircraftInRange[a] = false;

				// Remove the camera when the final plane leaves the target area
				if (!aircraftInRange.Any(kv => kv.Value))
					RemoveCamera(camera);
			};

			Action<Actor> onRemovedFromWorld = a =>
			{
				aircraftInRange[a] = false;

				// Checking for attack range is not relevant here because
				// aircraft may be shot down before entering. Thus we remove
				// the camera and beacon only if the whole squad is dead.
				if (aircraftInRange.All(kv => kv.Key.IsDead))
				{
					RemoveCamera(camera);
					RemoveBeacon(beacon);
				}
			};

			// Create the actors immediately so they can be returned
			var squadSize = info.SquadSizes.First(ss => ss.Key == GetLevel()).Value;
			for (var i = -squadSize / 2; i <= squadSize / 2; i++)
			{
				// Even-sized squads skip the lead plane
				if (i == 0 && (squadSize & 1) == 0)
					continue;

				// Includes the 90 degree rotation between body and world coordinates
				var so = info.SquadOffset;
				var spawnOffset = new WVec(i * so.Y, -Math.Abs(i) * so.X, 0).Rotate(dropRotation);

				aircraft.Add(self.World.CreateActor(false, info.UnitTypes.First(ut => ut.Key == GetLevel(), new TypeDictionary
				{
					new CenterPositionInit(startEdge + spawnOffset),
					new OwnerInit(self.Owner),
					new FacingInit(facing),
				}));
			}

			foreach (var p in info.DropItems)
			{
				units.Add(self.World.CreateActor(false, p.ToLowerInvariant(), new TypeDictionary
				{
					new OwnerInit(self.Owner)
				}));
			}

			self.World.AddFrameEndTask(w =>
			{
				PlayLaunchSounds();

				Actor distanceTestActor = null;

				var passengersPerPlane = (info.DropItems.First(di => di.Key == GetLevel()).Value.Length + squadSize - 1) / squadSize;
				var added = 0;
				var j = 0;
				for (var i = -info.SquadSize / 2; i <= info.SquadSize / 2; i++)
				{
					// Even-sized squads skip the lead plane
					if (i == 0 && (squadSize & 1) == 0)
						continue;

					// Includes the 90 degree rotation between body and world coordinates
					var so = info.SquadOffset;
					var spawnOffset = new WVec(i * so.Y, -Math.Abs(i) * so.X, 0).Rotate(dropRotation);
					var targetOffset = new WVec(i * so.Y, 0, 0).Rotate(dropRotation);
					var a = aircraft[j++];
					w.Add(a);

					var drop = a.Trait<ParaDrop>();
					drop.SetLZ(w.Map.CellContaining(target + targetOffset), !info.AllowImpassableCells);
					drop.OnEnteredDropRange += onEnterRange;
					drop.OnExitedDropRange += onExitRange;
					drop.OnRemovedFromWorld += onRemovedFromWorld;

					var cargo = a.Trait<Cargo>();
					foreach (var unit in units.Skip(added).Take(passengersPerPlane))
					{
						cargo.Load(a, unit);
						added++;
					}

					a.QueueActivity(new Fly(a, Target.FromPos(target + spawnOffset)));
					a.QueueActivity(new Fly(a, Target.FromPos(finishEdge + spawnOffset)));
					a.QueueActivity(new RemoveSelf());
					aircraftInRange.Add(a, false);
					distanceTestActor = a;
				}

				// Dispose any unused units
				for (var i = added; i < units.Count; i++)
					units[i].Dispose();

				if (Info.DisplayBeacon)
				{
					var distance = (target - startEdge).HorizontalLength;

					beacon = new Beacon(
						self.Owner,
						target - new WVec(0, 0, altitude),
						Info.BeaconPaletteIsPlayerPalette,
						Info.BeaconPalette,
						Info.BeaconImage,
						Info.BeaconPosters.First(bp => bp.Key == GetLevel()).Value,
						Info.BeaconPosterPalette,
						Info.BeaconSequence,
						Info.ArrowSequence,
						Info.CircleSequence,
						Info.ClockSequence,
						() => 1 - ((distanceTestActor.CenterPosition - target).HorizontalLength - info.BeaconDistanceOffset.Length) * 1f / distance,
						Info.BeaconDelay);

					w.Add(beacon);
				}
			});

			return Pair.New(aircraft.ToArray(), units.ToArray());
		}

		void RemoveCamera(Actor camera)
		{
			if (camera == null)
				return;

			camera.QueueActivity(new Wait(info.CameraRemoveDelay));
			camera.QueueActivity(new RemoveSelf());
			camera = null;
		}

		void RemoveBeacon(Beacon beacon)
		{
			if (beacon == null)
				return;

			Self.World.AddFrameEndTask(w =>
			{
				w.Remove(beacon);
				beacon = null;
			});
		}
	}
}
