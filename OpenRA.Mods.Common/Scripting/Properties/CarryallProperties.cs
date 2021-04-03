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

using Eluant;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Scripting
{
	[ScriptPropertyGroup("Ability")]
	public class CarryallProperties : ScriptActorProperties, Requires<IMoveInfo>, Requires<CarryallInfo>
	{
		readonly Carryall carryall;

		public CarryallProperties(ScriptContext context, Actor self)
			: base(context, self)
		{
			carryall = Self.Trait<Carryall>();
		}

		[ScriptActorPropertyActivity]
		[Desc("Pick up the target actor.")]
		public void PickupUnit(Actor target)
		{
			var carryable = target.TraitOrDefault<Carryable>();
			if (carryable == null)
				throw new LuaException("Actor '{0}' cannot carry actor '{1}'!".F(Self, target));

			if (carryall.Carryable != null)
				return;

			Self.QueueActivity(new PickupUnit(Self, target, carryall.Info.BeforeLoadDelay));
		}

		[ScriptActorPropertyActivity]
		[Desc("Drop the actor being carried at the current location.")]
		public void DeliverUnit()
		{
			if (carryall.Carryable == null)
				return;

			Self.QueueActivity(new DeliverUnit(Self, carryall.Info.DropRange));
		}
	}
}
