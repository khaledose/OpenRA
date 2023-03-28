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

using System.Linq;
using Eluant;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Scripting
{
	[ScriptPropertyGroup("Transports")]
	public class TransportProperties : ScriptActorProperties, Requires<CargoInfo>
	{
		readonly Cargo cargo;

		public TransportProperties(ScriptContext context, Actor self)
			: base(context, self)
		{
			cargo = self.Trait<Cargo>();
		}

		[Desc("Specifies whether transport has any passengers.")]
		public bool HasPassengers { get { return cargo.Passengers.Any(); } }

		[Desc("Specifies the amount of passengers.")]
		public int PassengerCount { get { return cargo.Passengers.Count(); } }

		[ScriptContext(ScriptContextType.Mission)]
		[Desc("Teleport an existing actor inside this transport.")]
		public void LoadPassenger(Actor a)
		{
			if (!a.IsIdle)
				throw new LuaException("LoadPassenger requires the passenger to be idle.");

			cargo.Load(Self, a);
		}

		[Desc("Remove the first actor from the transport.  This actor is not added to the world.")]
		public Actor UnloadPassenger() { return cargo.Unload(Self); }

		[ScriptActorPropertyActivity]
		[Desc("Command transport to unload passengers.")]
		public void UnloadPassengers(CPos? cell = null, int unloadRange = 5)
		{
			if (cell.HasValue)
			{
				var destination = Target.FromCell(Self.World, cell.Value);
				Self.QueueActivity(new UnloadCargo(Self, destination, WDist.FromCells(unloadRange)));
			}
			else
				Self.QueueActivity(new UnloadCargo(Self, WDist.FromCells(unloadRange)));
		}
	}
}
