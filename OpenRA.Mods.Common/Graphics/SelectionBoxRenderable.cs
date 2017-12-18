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

using System.Drawing;
using OpenRA.Graphics;

namespace OpenRA.Mods.Common.Graphics
{
	public struct SelectionBoxRenderable : IRenderable, IFinalizedRenderable
	{
		readonly WPos pos;
		readonly Rectangle visualBounds;
		readonly Color color;

		public SelectionBoxRenderable(Actor actor, Color color)
			: this(actor.CenterPosition, actor.VisualBounds, color) { }

		public SelectionBoxRenderable(WPos pos, Rectangle visualBounds, Color color)
		{
			this.pos = pos;
			this.visualBounds = visualBounds;
			this.color = color;
		}

		public WPos Pos { get { return pos; } }

		public PaletteReference Palette { get { return null; } }
		public int ZOffset { get { return 0; } }
		public bool IsDecoration { get { return true; } }

		public IRenderable WithPalette(PaletteReference newPalette) { return this; }
		public IRenderable WithZOffset(int newOffset) { return this; }
		public IRenderable OffsetBy(WVec vec) { return new SelectionBoxRenderable(pos + vec, visualBounds, color); }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var iz = 1 / wr.Viewport.Zoom;
			var screenPos = wr.Screen3DPxPosition(pos);
			var tl = screenPos + new float3(visualBounds.Left, visualBounds.Top, 0);
			var br = screenPos + new float3(visualBounds.Right, visualBounds.Bottom, 0);
			var tr = new float3(br.X, tl.Y, screenPos.Z);
			var bl = new float3(tl.X, br.Y, screenPos.Z);
			var u = new float2(4 * iz, 0);
			var v = new float2(0, 4 * iz);

			var wcr = Game.Renderer.WorldRgbaColorRenderer;
			wcr.DrawLine(new[] { tl + u, tl, tl + v }, iz, color, true);
			wcr.DrawLine(new[] { tr - u, tr, tr + v }, iz, color, true);
			wcr.DrawLine(new[] { br - u, br, br - v }, iz, color, true);
			wcr.DrawLine(new[] { bl + u, bl, bl - v }, iz, color, true);
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
