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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	class BaseBuilderQueueManager
	{
		readonly string category;

		readonly BaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PowerManager playerPower;
		readonly PlayerResources playerResources;

		int waitTicks;
		Actor[] playerBuildings;
		int failCount;
		int failRetryTicks;
		int checkForBasesTicks;
		int cachedBases;
		int cachedBuildings;
		int minimumExcessPower;
		BitArray resourceTypeIndices;

		WaterCheck waterState = WaterCheck.NotChecked;

		public BaseBuilderQueueManager(BaseBuilderBotModule baseBuilder, string category, Player p, PowerManager pm,
			PlayerResources pr, BitArray resourceTypeIndices)
		{
			this.baseBuilder = baseBuilder;
			world = p.World;
			player = p;
			playerPower = pm;
			playerResources = pr;
			this.category = category;
			failRetryTicks = baseBuilder.Info.StructureProductionResumeDelay;
			minimumExcessPower = baseBuilder.Info.MinimumExcessPower;
			this.resourceTypeIndices = resourceTypeIndices;
		}

		public void Tick(IBot bot)
		{
			// If failed to place something N consecutive times, wait M ticks until resuming building production
			if (failCount >= baseBuilder.Info.MaximumFailedPlacementAttempts && --failRetryTicks <= 0)
			{
				var currentBuildings = world.ActorsHavingTrait<Building>().Count(a => a.Owner == player);
				var baseProviders = world.ActorsHavingTrait<BaseProvider>().Count(a => a.Owner == player);

				// Only bother resetting failCount if either a) the number of buildings has decreased since last failure M ticks ago,
				// or b) number of BaseProviders (construction yard or similar) has increased since then.
				// Otherwise reset failRetryTicks instead to wait again.
				if (currentBuildings < cachedBuildings || baseProviders > cachedBases)
					failCount = 0;
				else
					failRetryTicks = baseBuilder.Info.StructureProductionResumeDelay;
			}

			if (waterState == WaterCheck.NotChecked)
			{
				if (AIUtils.IsAreaAvailable<BaseProvider>(world, player, world.Map, baseBuilder.Info.MaxBaseRadius, baseBuilder.Info.WaterTerrainTypes))
					waterState = WaterCheck.EnoughWater;
				else
				{
					waterState = WaterCheck.NotEnoughWater;
					checkForBasesTicks = baseBuilder.Info.CheckForNewBasesDelay;
				}
			}

			if (waterState == WaterCheck.NotEnoughWater && --checkForBasesTicks <= 0)
			{
				var currentBases = world.ActorsHavingTrait<BaseProvider>().Count(a => a.Owner == player);

				if (currentBases > cachedBases)
				{
					cachedBases = currentBases;
					waterState = WaterCheck.NotChecked;
				}
			}

			// Only update once per second or so
			if (--waitTicks > 0)
				return;

			playerBuildings = world.ActorsHavingTrait<Building>().Where(a => a.Owner == player).ToArray();
			var excessPowerBonus = baseBuilder.Info.ExcessPowerIncrement * (playerBuildings.Count() / baseBuilder.Info.ExcessPowerIncreaseThreshold.Clamp(1, int.MaxValue));
			minimumExcessPower = (baseBuilder.Info.MinimumExcessPower + excessPowerBonus).Clamp(baseBuilder.Info.MinimumExcessPower, baseBuilder.Info.MaximumExcessPower);

			var active = false;
			foreach (var queue in AIUtils.FindQueues(player, category))
				if (TickQueue(bot, queue))
					active = true;

			// Add a random factor so not every AI produces at the same tick early in the game.
			// Minimum should not be negative as delays in HackyAI could be zero.
			var randomFactor = world.LocalRandom.Next(0, baseBuilder.Info.StructureProductionRandomBonusDelay);

			// Needs to be at least 4 * OrderLatency because otherwise the AI frequently duplicates build orders (i.e. makes the same build decision twice)
			waitTicks = active ? 4 * world.LobbyInfo.GlobalSettings.OrderLatency + baseBuilder.Info.StructureProductionActiveDelay + randomFactor
				: baseBuilder.Info.StructureProductionInactiveDelay + randomFactor;
		}

		bool TickQueue(IBot bot, ProductionQueue queue)
		{
			var currentBuilding = queue.AllQueued().FirstOrDefault();

			// Waiting to build something
			if (currentBuilding == null && failCount < baseBuilder.Info.MaximumFailedPlacementAttempts)
			{
				var item = ChooseBuildingToBuild(queue);
				if (item == null)
					return false;

				bot.QueueOrder(Order.StartProduction(queue.Actor, item.Name, 1));
			}
			else if (currentBuilding != null && currentBuilding.Done)
			{
				// Production is complete
				// Choose the placement logic
				var type = BuildingType.Building;

				// Check if Building is a defense and if we should place it towards the enemy or not.
				if (baseBuilder.Info.DefenseTypes.Contains(world.Map.Rules.Actors[currentBuilding.Item].Name) && world.LocalRandom.Next(100) < baseBuilder.Info.PlaceDefenseTowardsEnemyChance)
					type = BuildingType.Defense;
				else if (baseBuilder.Info.RefineryTypes.Contains(world.Map.Rules.Actors[currentBuilding.Item].Name))
					type = BuildingType.Refinery;
				else if (baseBuilder.Info.FragileTypes.Contains(world.Map.Rules.Actors[currentBuilding.Item].Name))
					type = BuildingType.Fragile;

				var location = ChooseBuildLocation(currentBuilding.Item, true, queue.Actor, type);
				if (location == null)
				{
					AIUtils.BotDebug("AI: {0} has nowhere to place {1}".F(player, currentBuilding.Item));
					bot.QueueOrder(Order.CancelProduction(queue.Actor, currentBuilding.Item, 1));
					failCount += failCount;

					// If we just reached the maximum fail count, cache the number of current structures
					if (failCount == baseBuilder.Info.MaximumFailedPlacementAttempts)
					{
						cachedBuildings = world.ActorsHavingTrait<Building>().Count(a => a.Owner == player);
						cachedBases = world.ActorsHavingTrait<BaseProvider>().Count(a => a.Owner == player);
					}
				}
				else
				{
					failCount = 0;
					bot.QueueOrder(new Order("PlaceBuilding", player.PlayerActor, Target.FromCell(world, location.Value), false)
					{
						// Building to place
						TargetString = currentBuilding.Item,

						// Actor ID to associate the placement with
						ExtraData = queue.Actor.ActorID,
						SuppressVisualFeedback = true
					});

					return true;
				}
			}

			return true;
		}

		ActorInfo GetProducibleBuilding(HashSet<string> actors, IEnumerable<ActorInfo> buildables, Func<ActorInfo, int> orderBy = null)
		{
			var available = buildables.Where(actor =>
			{
				// Are we able to build this?
				if (!actors.Contains(actor.Name))
					return false;

				if (!baseBuilder.Info.BuildingLimits.ContainsKey(actor.Name))
					return true;

				var producers = world.Actors.Where(a => a.Owner == player && a.TraitsImplementing<ProductionQueue>().Any());
				var productionQueues = producers.SelectMany(a => a.TraitsImplementing<ProductionQueue>());
				var activeProductionQueues = productionQueues.Where(pq => pq.AllQueued().Any());
				var queues = activeProductionQueues.Where(pq => pq.AllQueued().Where(q => q.Item == actor.Name).Any());

				return playerBuildings.Count(a => a.Info.Name == actor.Name) + queues.Count() < baseBuilder.Info.BuildingLimits[actor.Name];
			});

			if (orderBy != null)
				return available.MaxByOrDefault(orderBy);

			return available.RandomOrDefault(world.LocalRandom);
		}

		bool HasSufficientPowerForActor(ActorInfo actorInfo)
		{
			return playerPower == null || (actorInfo.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault)
				.Sum(p => p.Amount) + playerPower.ExcessPower) >= baseBuilder.Info.MinimumExcessPower;
		}

		ActorInfo ChooseBuildingToBuild(ProductionQueue queue)
		{
			var buildableThings = queue.BuildableItems();
			if (!buildableThings.Any())
				return null;

			return HackyChooseBuildingToBuild(queue, buildableThings);
		}

		ActorInfo HackyChooseBuildingToBuild(ProductionQueue queue, IEnumerable<ActorInfo> buildableThings)
		{
			// This gets used quite a bit, so let's cache it here
			var power = GetProducibleBuilding(baseBuilder.Info.PowerTypes, buildableThings,
				a => a.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(p => p.Amount));

			// First priority is to get out of a low power situation
			if (playerPower != null && playerPower.ExcessPower < minimumExcessPower)
			{
				if (power != null && power.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(p => p.Amount) > 0)
				{
					AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (low power)", queue.Actor.Owner, power.Name);
					return power;
				}
			}

			// Next is to build up a strong economy
			if (!baseBuilder.HasAdequateRefineryCount)
			{
				var refinery = GetProducibleBuilding(baseBuilder.Info.RefineryTypes, buildableThings);
				if (refinery != null && HasSufficientPowerForActor(refinery))
				{
					AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (refinery)", queue.Actor.Owner, refinery.Name);
					return refinery;
				}

				if (power != null && refinery != null && !HasSufficientPowerForActor(refinery))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);
					return power;
				}
			}

			// Make sure that we can spend as fast as we are earning
			if (baseBuilder.Info.NewProductionCashThreshold > 0 && baseBuilder.Info.ConstructionMinimumCash <= playerResources.Cash && playerResources.Cash > baseBuilder.Info.NewProductionCashThreshold)
			{
				var production = GetProducibleBuilding(baseBuilder.Info.ProductionTypes, buildableThings);
				if (production != null && HasSufficientPowerForActor(production))
				{
					AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (production)", queue.Actor.Owner, production.Name);
					return production;
				}

				if (power != null && production != null && !HasSufficientPowerForActor(production))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);
					return power;
				}
			}

			// Only consider building this if there is enough water inside the base perimeter and there are close enough adjacent buildings
			if (waterState == WaterCheck.EnoughWater && baseBuilder.Info.NewProductionCashThreshold > 0
				&& baseBuilder.Info.ConstructionMinimumCash <= playerResources.Cash
				&& playerResources.Resources > baseBuilder.Info.NewProductionCashThreshold
				&& AIUtils.IsAreaAvailable<GivesBuildableArea>(world, player, world.Map, baseBuilder.Info.CheckForWaterRadius, baseBuilder.Info.WaterTerrainTypes))
			{
				var navalproduction = GetProducibleBuilding(baseBuilder.Info.NavalProductionTypes, buildableThings);
				if (navalproduction != null && HasSufficientPowerForActor(navalproduction))
				{
					AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (navalproduction)", queue.Actor.Owner, navalproduction.Name);
					return navalproduction;
				}

				if (power != null && navalproduction != null && !HasSufficientPowerForActor(navalproduction))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);
					return power;
				}
			}

			// Create some head room for resource storage if we really need it
			if (playerResources.Resources > 0.8 * playerResources.ResourceCapacity)
			{
				var silo = GetProducibleBuilding(baseBuilder.Info.SiloTypes, buildableThings);
				if (silo != null && HasSufficientPowerForActor(silo))
				{
					AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (silo)", queue.Actor.Owner, silo.Name);
					return silo;
				}

				if (power != null && silo != null && !HasSufficientPowerForActor(silo))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);
					return power;
				}
			}

			// Select a strategy
			var strategy = GetProducibleBuilding(baseBuilder.Info.StrategyTypes, buildableThings);
			if (strategy != null &&
				!world.Actors.Where(a => baseBuilder.Info.StrategyTypes.Contains(a.Info.Name) && a.Owner == player).Any() &&
				HasSufficientPowerForActor(strategy))
			{
				AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (strategy)", queue.Actor.Owner, strategy.Name);
				return strategy;
			}

			if (power != null && strategy != null && !HasSufficientPowerForActor(strategy))
			{
				AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);
				return power;
			}

			// Build everything else
			foreach (var frac in baseBuilder.Info.BuildingFractions.Shuffle(world.LocalRandom))
			{
				var name = frac.Key;

				// Does this building have initial delay, if so have we passed it?
				if (baseBuilder.Info.BuildingDelays != null &&
					baseBuilder.Info.BuildingDelays.ContainsKey(name) &&
					baseBuilder.Info.BuildingDelays[name] > world.WorldTick)
					continue;

				// Can we build this structure?
				if (!buildableThings.Any(b => b.Name == name))
					continue;

				if (playerResources != null && playerResources.Cash <= baseBuilder.Info.ConstructionMinimumCash && !baseBuilder.Info.CashGeneratorTypes.Contains(name))
					continue;

				/* Core logic should be implemented as a seperate module, commenting this out for now.
				// Can we build this structure?
				if (!buildableThings.Any(b => b.Name == name))
				{
					// Check if it is defined in the core and buildable.
					if (ai.Info.CoreDefinitions == null || !ai.Info.CoreDefinitions.ContainsKey(name))
						//// Not even indirectly buildable with a "core".
						continue;
					if (!buildableThings.Any(b => b.Name == ai.Info.CoreDefinitions[name]))
						//// Indirectly buildable, but that core is not currently buildable.
						continue;
				} */

				// Do we want to build this structure?
				var producers = world.Actors.Where(a => a.Owner == queue.Actor.Owner && a.TraitsImplementing<ProductionQueue>().Any());
				var productionQueues = producers.SelectMany(a => a.TraitsImplementing<ProductionQueue>());
				var activeProductionQueues = productionQueues.Where(pq => pq.AllQueued().Any());
				var queues = activeProductionQueues.Where(pq => pq.AllQueued().Where(q => q.Item == name).Any());

				var count = playerBuildings.Count(a => a.Info.Name == name) + (queues == null ? 0 : queues.Count());
				if (count * 100 > frac.Value * playerBuildings.Length)
					continue;

				if (baseBuilder.Info.BuildingLimits.ContainsKey(name) && baseBuilder.Info.BuildingLimits[name] <= count)
					continue;

				// If we're considering to build a naval structure, check whether there is enough water inside the base perimeter
				// and any structure providing buildable area close enough to that water.
				// TODO: Extend this check to cover any naval structure, not just production.
				if (baseBuilder.Info.NavalProductionTypes.Contains(name)
					&& (waterState == WaterCheck.NotEnoughWater
						|| !AIUtils.IsAreaAvailable<GivesBuildableArea>(world, player, world.Map, baseBuilder.Info.CheckForWaterRadius, baseBuilder.Info.WaterTerrainTypes)))
					continue;

				if (!world.Map.Rules.Actors.ContainsKey(name))
				{
					AIUtils.BotDebug("{0} tryed to build an actor named {1}, no such actor exists.", queue.Actor.Owner, name);
					continue;
				}

				// Maybe we can't queue this because of InstantCashDrain logic?
				var actor = world.Map.Rules.Actors[name];
				if (playerResources != null)
				{
					var nonInstantCashQueues = productionQueues.Where(pq => !pq.Info.InstantCashDrain);
					if (!nonInstantCashQueues.Any())
					{
						var instantCashQueues = productionQueues.Where(pq => pq.Info.InstantCashDrain);
						if (instantCashQueues.Any())
						{
							var cost = instantCashQueues.Min(q => q.GetProductionCost(actor));
							if (playerResources.Cash < cost)
								continue;
						}
					}
				}

				// Will this put us into low power?
				if (playerPower != null && (playerPower.ExcessPower < minimumExcessPower || !HasSufficientPowerForActor(actor)))
				{
					// Try building a power plant instead
					if (power != null && power.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(pi => pi.Amount) > 0)
					{
						if (playerPower.PowerOutageRemainingTicks > 0)
							AIUtils.BotDebug("{0} decided to build {1}: Priority override (is low power)", queue.Actor.Owner, power.Name);
						else
							AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);

						return power;
					}
				}

				// Lets build this
				AIUtils.BotDebug("{0} decided to build {1}: Desired is {2} ({3} / {4}); current is {5} / {4}",
					queue.Actor.Owner, name, frac.Value, frac.Value * playerBuildings.Length, playerBuildings.Length, count);

				/* Core logic should be implemented as a seperate module, commenting this out for now.
				// If a core actor, return the core instead.
				if (ai.Info.CoreDefinitions != null && ai.Info.CoreDefinitions.ContainsKey(name))
					return world.Map.Rules.Actors[ai.Info.CoreDefinitions[name]];
				else
					return actor; */

				return actor;
			}

			// Too spammy to keep enabled all the time, but very useful when debugging specific issues.
			// AIUtils.BotDebug("{0} couldn't decide what to build for queue {1}.", queue.Actor.Owner, queue.Info.Group);
			return null;
		}

		CPos? ChooseBuildLocation(string actorType, bool distanceToBaseIsImportant, Actor producer, BuildingType type)
		{
			var actorInfo = world.Map.Rules.Actors[actorType];
			var bi = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			if (bi == null)
				return null;

			// Find the buildable cell that is closest to pos and centered around center
			Func<CPos, CPos, int, int, CPos?> findPos = (center, target, minRange, maxRange) =>
			{
				var cells = world.Map.FindTilesInAnnulus(center, minRange, maxRange);

				// Sort by distance to target if we have one
				if (center != target)
					cells = cells.OrderBy(c => (c - target).LengthSquared);
				else
					cells = cells.Shuffle(world.LocalRandom);

				foreach (var cell in cells)
				{
					if (!world.CanPlaceBuilding(cell, actorInfo, bi, null))
						continue;

					if (distanceToBaseIsImportant && !bi.IsCloseEnoughToBase(world, player, actorInfo, producer, cell))
						continue;

					return cell;
				}

				return null;
			};

			var baseCenter = baseBuilder.GetRandomBaseCenter();

			switch (type)
			{
				case BuildingType.Defense:

					// Build near the closest enemy structure
					var closestEnemyDefense = world.ActorsHavingTrait<Building>().Where(a => !a.Disposed && player.Stances[a.Owner] == Stance.Enemy)
						.ClosestTo(world.Map.CenterOfCell(baseBuilder.DefenseCenter));

					var targetCellDefense = closestEnemyDefense != null ? closestEnemyDefense.Location : baseCenter;
					return findPos(baseBuilder.DefenseCenter, targetCellDefense, baseBuilder.Info.MinimumDefenseRadius, baseBuilder.Info.MaximumDefenseRadius);

				case BuildingType.Fragile:

					// Build far from the closest enemy structure
					var closestEnemyFraigle = world.ActorsHavingTrait<Building>().Where(a => !a.Disposed && player.Stances[a.Owner] == Stance.Enemy)
						.ClosestTo(world.Map.CenterOfCell(baseBuilder.DefenseCenter));

					var targetCellFraigle = closestEnemyFraigle != null ? closestEnemyFraigle.Location : baseCenter;

					// MinFragilePlacementRadius introduced to push fragile buildings away from base center.
					// Resilient to nuke.
					var pos = findPos(baseCenter, targetCellFraigle, baseBuilder.Info.MinFragilePlacementRadius,
							distanceToBaseIsImportant ? baseBuilder.Info.MaxBaseRadius : world.Map.Grid.MaximumTileSearchRange);

					return pos;

				case BuildingType.Refinery:

					// Don't check for resources if the mod has docks
					if (!baseBuilder.Info.SupplyDockTypes.Any())
					{
						// Try and place the refinery near a resource field
						var nearbyResources = world.Map.FindTilesInAnnulus(baseCenter, baseBuilder.Info.MinBaseRadius, baseBuilder.Info.MaxBaseRadius)
							.Where(a => resourceTypeIndices.Get(world.Map.GetTerrainIndex(a)))
							.Shuffle(world.LocalRandom).Take(baseBuilder.Info.MaxResourceCellsToCheck);

						foreach (var r in nearbyResources)
						{
							var found = findPos(baseCenter, r, baseBuilder.Info.MinBaseRadius, baseBuilder.Info.MaxBaseRadius);
							if (found != null)
								return found;
						}
					}
					else
					{
						// Try and place the refinery near a supply dock
						var nearbyDocks = world.FindActorsInCircle(world.Map.CenterOfCell(baseCenter), WDist.FromCells(baseBuilder.Info.MaxBaseRadius))
							.Where(a => baseBuilder.Info.SupplyDockTypes.Contains(a.Info.Name))
							.Shuffle(world.LocalRandom).Take(baseBuilder.Info.MaxResourceCellsToCheck);

						foreach (var r in nearbyDocks)
						{
							var found = findPos(baseCenter, world.Map.CellContaining(r.CenterPosition), baseBuilder.Info.MinBaseRadius, baseBuilder.Info.MaxBaseRadius);
							if (found != null)
								return found;
						}
					}

					// Try and find a free spot somewhere else in the base
					return findPos(baseCenter, baseCenter, baseBuilder.Info.MinBaseRadius, baseBuilder.Info.MaxBaseRadius);

				case BuildingType.Building:
					return findPos(baseCenter, baseCenter, baseBuilder.Info.MinBaseRadius,
						distanceToBaseIsImportant ? baseBuilder.Info.MaxBaseRadius : world.Map.Grid.MaximumTileSearchRange);
			}

			// Can't find a build location
			return null;
		}
	}
}
