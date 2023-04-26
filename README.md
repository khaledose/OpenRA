# OpenRA Engine Modification for Over Powered Mod

If you are looking for the mod, please visit https://github.com/forcecore/yupgi_alert0/ and http://www.moddb.com/mods/over-powered-mod. My mod is like a show case for the engine modification.

This fork is like experimental branch of Linux Debian distribution: I push the boundaries of OpenRA on what it can do at the cost of compatibility from main OpenRA. AttacqueSuperior features more stable higher quality traits and modder friendly: https://github.com/AttacqueSuperior/Engine. Some of my modules are tamed and imported into their engine too! (And I use their stuff too hehe)

I do hope that one day these experimental stuff get popular and find its way into main OpenRA code...

## Notable modification to OpenRA.Game.exe, in this branch
* Supports "sky box". (needed game.exe modification argh)
* You can rename it to OpenRA.{modname}.exe and it will launch the mod.

## Commit tags
In commit message, I put these string as tags:
* [SPAWN]: code modifications for spawners or spawned ones. Used for aircraft carriers and V3-like AA shootable missiles.
* [GRINDER]: Yuri grinder logic related commits
* [IRON_EFF]: iron curtain flashing effect
* [RADIO_ACTIVITY]: radio activity layer stuff (desolator rad eruption, nuke radiation...)
* [SPACE]: codes for space themed maps
* [CMIN]: Chrono miner (ore teleporter)
* [HARV]: Harvester AI related stuff
* [IDEPLOY]: (un)deploy notification interface
* [DOCKS]: multi-dock support

# OpenRA

A Libre/Free Real Time Strategy game engine supporting early Westwood classics.

* Website: [http://www.openra.net](http://www.openra.net)
* IRC: \#openra on irc.freenode.net
* Repository: [https://github.com/OpenRA/OpenRA](https://github.com/OpenRA/OpenRA) ![Continuous Integration](https://github.com/OpenRA/OpenRA/workflows/Continuous%20Integration/badge.svg)

Please read the [FAQ](http://wiki.openra.net/FAQ) in our [Wiki](http://wiki.openra.net) and report problems at [http://bugs.openra.net](http://bugs.openra.net).

Join the [Forums](https://forum.openra.net/) for discussion.

## Play

Distributed mods include a reimagining of

* Command & Conquer: Red Alert
* Command & Conquer: Tiberian Dawn
* Dune 2000

EA has not endorsed and does not support this product.

Check our [Playing the Game](https://github.com/OpenRA/OpenRA/wiki/Playing-the-game) Guide to win multiplayer matches.

## Contribute

* Please read [INSTALL.md](https://github.com/OpenRA/OpenRA/blob/bleed/INSTALL.md) and [Compiling](http://wiki.openra.net/Compiling) on how to set up an OpenRA development environment.
* See [Hacking](http://wiki.openra.net/Hacking) for a (now very outdated) overview of the engine.
* Read and follow our [Code of Conduct](https://github.com/OpenRA/OpenRA/blob/bleed/CODE_OF_CONDUCT.md).
* To get your patches merged, please adhere to the [Contributing](https://github.com/OpenRA/OpenRA/blob/bleed/CONTRIBUTING.md) guidelines.

## Mapping

* We offer a [Mapping](http://wiki.openra.net/Mapping) Tutorial as you can change gameplay drastically with custom rules.
* For scripted mission have a look at the [Lua API](http://wiki.openra.net/Lua-API).
* If you want to share your maps with the community, upload them at the [OpenRA Resource Center](http://resource.openra.net).

## Modding

* Download a copy of the [OpenRA Mod SDK](https://github.com/OpenRA/OpenRAModSDK/) to start your own mod.
* Check the [Modding Guide](http://wiki.openra.net/Modding-Guide) to create your own classic RTS.
* There exists an auto-generated [Trait documentation](https://docs.openra.net/en/latest/release/traits/) to get started with yaml files.
* Some hints on how to create new OpenRA compatible [Pixelart](http://wiki.openra.net/Pixelart).
* Upload total conversions at [our ModDB profile](http://www.moddb.com/games/openra/mods).

## Support

* Sponsor a [mirror server](https://github.com/OpenRA/OpenRAWeb/tree/master/content/packages) if you have some bandwidth to spare.
* You can immediately set up a [Dedicated](http://wiki.openra.net/Dedicated) Game Server.

## License
Copyright 2007-2020 The OpenRA Developers (see [AUTHORS](https://github.com/OpenRA/OpenRA/blob/bleed/AUTHORS))
This file is part of OpenRA, which is free software. It is made
available to you under the terms of the GNU General Public License
as published by the Free Software Foundation, either version 3 of
the License, or (at your option) any later version. For more
information, see [COPYING](https://github.com/OpenRA/OpenRA/blob/bleed/COPYING).
