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

using System.Collections.Generic;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class TerrainRendererInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new TerrainRenderer(init.World); }
	}

	public sealed class TerrainRenderer : IRenderTerrain, IWorldLoaded, INotifyActorDisposing
	{
		readonly Map map;
		Sprite skyImage;
		float2 skySz;

		readonly Dictionary<string, TerrainSpriteLayer> spriteLayers = new Dictionary<string, TerrainSpriteLayer>();
		Theater theater;
		bool disposed;

		public TerrainRenderer(World world)
		{
			map = world.Map;
		}

		void IWorldLoaded.WorldLoaded(World world, WorldRenderer wr)
		{
			theater = wr.Theater;

			if (map.SkyboxImage != null && map.Package.Contains(map.SkyboxImage))
			{
				skySz = new float2(Game.Renderer.Resolution.Width, Game.Renderer.Resolution.Width);
				using (var dataStream = map.Package.GetStream(map.SkyboxImage))
				{
					var png = new Png(dataStream);
					var sheetBuilder = new SheetBuilder(SheetType.BGRA, png.Width);
					skyImage = sheetBuilder.Add(png);
				}
			}

			foreach (var template in map.Rules.TileSet.Templates)
			{
				var palette = template.Value.Palette ?? TileSet.TerrainPaletteInternalName;
				spriteLayers.GetOrAdd(palette, pal =>
					new TerrainSpriteLayer(world, wr, theater.Sheet, BlendMode.Alpha, wr.Palette(palette), world.Type != WorldType.Editor));
			}

			foreach (var cell in map.AllCells)
				UpdateCell(cell);

			map.Tiles.CellEntryChanged += UpdateCell;
			map.Height.CellEntryChanged += UpdateCell;
		}

		public void UpdateCell(CPos cell)
		{
			var tile = map.Tiles[cell];
			var palette = TileSet.TerrainPaletteInternalName;
			if (map.Rules.TileSet.Templates.ContainsKey(tile.Type))
				palette = map.Rules.TileSet.Templates[tile.Type].Palette ?? palette;

			var sprite = theater.TileSprite(tile);
			foreach (var kv in spriteLayers)
				kv.Value.Update(cell, palette == kv.Key ? sprite : null, false);
		}

		void DrawSkybox(WorldRenderer wr, Viewport viewport)
		{
			if (skyImage == null)
				return;
			Game.Renderer.RgbaSpriteRenderer.DrawSprite(skyImage, float2.Zero, skySz);
		}

		void IRenderTerrain.RenderTerrain(WorldRenderer wr, Viewport viewport)
		{
			DrawSkybox(wr, viewport);

			foreach (var kv in spriteLayers.Values)
				kv.Draw(wr.Viewport);

			foreach (var r in wr.World.WorldActor.TraitsImplementing<IRenderOverlay>())
				r.Render(wr);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			map.Tiles.CellEntryChanged -= UpdateCell;
			map.Height.CellEntryChanged -= UpdateCell;

			foreach (var kv in spriteLayers.Values)
				kv.Dispose();

			disposed = true;
		}
	}
}
