#region Copyright & License Information
/*
 * Made by Boolbada of OP Mod then rectified by Mods.AS devs then modified again by Boolbada
 * Long story haha
 *
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

/* Works without base engine modification */

using System.Collections.Generic;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Yupgi_alert.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Yupgi_alert.Projectiles
{
	[Desc("Not a sprite, but an engine effect.")]
	public class RadBeamInfo : IProjectileInfo
	{
		[Desc("The thickness of the beam. (in WDist)")]
		public readonly WDist Thickness = new WDist(16);

		[Desc("The amplitude of the beam (in WDist).")]
		public readonly WDist Amplitude = new WDist(128);

		[Desc("The wavelength of the beam. (in WDist)")]
		public readonly WDist WaveLength = new WDist(512);

		[Desc("Draw each cycle with this many quantization steps")]
		public readonly int QuantizationCount = 8;

		[Desc("Equivalent to sequence ZOffset. Controls Z sorting.")]
		public readonly int ZOffset = 0;

		[Desc("Duration of the beam.")]
		public readonly int BeamDuration = 15;

		public readonly bool ScaleAmplitudeWithDuration = true;

		public readonly bool UsePlayerColor = false;

		[Desc("Beam color in (A),R,G,B.")]
		public readonly Color Color = Color.FromArgb(128, 0, 255, 0);

		[Desc("Impact animation.")]
		public readonly string HitAnim = null;

		[Desc("Sequence of impact animation to use.")]
		[SequenceReference(nameof(HitAnim), allowNullImage: true)]
		public readonly string HitAnimSequence = "idle";

		[PaletteReference]
		public readonly string HitAnimPalette = "effect";

		public IProjectile Create(ProjectileArgs args)
		{
			var c = UsePlayerColor ? args.SourceActor.Owner.Color : Color;
			return new RadBeam(args, this, c);
		}
	}

	public class RadBeam : IProjectile
	{
		readonly ProjectileArgs args;
		readonly RadBeamInfo info;
		readonly Animation hitanim;
		int ticks = 0;
		bool doneDamage;
		bool animationComplete;
		WPos target;

		public RadBeam(ProjectileArgs args, RadBeamInfo info, Color color)
		{
			this.args = args;
			this.info = info;
			target = args.PassiveTarget;

			if (!string.IsNullOrEmpty(info.HitAnim))
				hitanim = new Animation(args.SourceActor.World, info.HitAnim);
		}

		public void Tick(World world)
		{
			// Beam tracks target
			if (args.GuidedTarget.IsValidFor(args.SourceActor))
				target = args.GuidedTarget.CenterPosition;

			if (!doneDamage)
			{
				if (hitanim != null)
					hitanim.PlayThen(info.HitAnimSequence, () => animationComplete = true);
				else
					animationComplete = true;

				args.Weapon.Impact(Target.FromPos(target), new WarheadArgs(args));
				doneDamage = true;
			}

			if (hitanim != null)
				hitanim.Tick();

			if (++ticks >= info.BeamDuration && animationComplete)
				world.AddFrameEndTask(w => w.Remove(this));
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (wr.World.FogObscures(target) &&
				wr.World.FogObscures(args.Source))
				yield break;

			if (ticks < info.BeamDuration)
			{
				WDist amp = info.ScaleAmplitudeWithDuration
					? info.Amplitude * ticks / info.BeamDuration
					: info.Amplitude;
				yield return new RadBeamRenderable(args.Source, info.ZOffset, target - args.Source, info.Thickness, info.Color, amp, info.WaveLength, info.QuantizationCount);
			}

			if (hitanim != null)
				foreach (var r in hitanim.Render(target, wr.Palette(info.HitAnimPalette)))
					yield return r;
		}
	}
}
